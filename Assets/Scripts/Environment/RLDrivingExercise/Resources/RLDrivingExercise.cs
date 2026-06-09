using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class RLDrivingExercise : Task
{
    public const float ResetLength = 45;
    public const float MaxSpeed = 70;

    [field: SerializeField, ProjectAssign(Header = "Settings")]
    public float TimeScale { get; set; } = 1;

    [field: SerializeField, ProjectAssign]
    public float NominalSpeed { get; set; } = 3f;

    [field: SerializeField, ProjectAssign]
    public float DecisionTime { get; set; } = 0.1f;

    public GameObject Obstacle; // Assign in Inspector

    public const float RANGEMIN = -6.5f;
    public const float RANGEMAX = 6.5f;

    protected float _obstacleDistanceX;
    protected float _obstacleDistanceZ;
    protected float _carDeltaX = 0;
    protected float _xPosition;

    private PrometeoCarController _cont = null;
    private Vector3 _startPosition;

    private bool _hasObstacleReward = false;
    private int _loopsSurvived = 0;

    private int _previousAction = -1;
    private float _episodeReward = 0f;
    private float _totalReward = 0f;
    private int _currentStep = 0;
    private int _totalSteps = 0;
    private int _episodeCount = 0;
    private int _completedEpisodes = 0;

    private Vector2 _currentInput;
    private bool _visibilityFlag;

    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private Texture2D _bgTex;

    private RLDrivingExerciseStateInformation _rlDrivingExerciseStateInformation;


    public override IStateInformation StateInformation
    {
        get
        {
            _rlDrivingExerciseStateInformation ??= new RLDrivingExerciseStateInformation();

            return _rlDrivingExerciseStateInformation;
        }
        set
        {
            _rlDrivingExerciseStateInformation = value as RLDrivingExerciseStateInformation;
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetEpisode();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 obstaclePos = Obstacle.transform.position;
        Vector3 agentPos = transform.position;

        float laneHalfWidth = RANGEMAX;
        float trackLength = 45f;

        _xPosition = agentPos.x / laneHalfWidth;
        _visibilityFlag = obstaclePos.y > 0;
        _obstacleDistanceX = (obstaclePos.x - agentPos.x) / laneHalfWidth;
        _obstacleDistanceZ = (obstaclePos.z - agentPos.z) / trackLength;

        AddObservationsToSensor(sensor);
    }

    public override void AddTrueObservationsToSensor(VectorSensor sensor)
    {
    }

    public override void OnMove(InputValue value)
    {
        _currentInput = value.Get<Vector2>();
    }

    public override void UpdateDifficultyLevel()
    {
    }

    public override void OnActionReceivedInternal(ActionBuffers actions)
    {
        TakeAction(actions.DiscreteActions[0]);

        _previousAction = actions.DiscreteActions[0];
        _episodeReward = GetCumulativeReward();
        _currentStep++;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        if (_currentInput.x == -1)
        {
            discreteActions[0] = 0; // Turn Left
        }
        else if (_currentInput.x == 1)
        {
            discreteActions[0] = 1; // Turn Right
        }
        else
        {
            discreteActions[0] = 2; // Level Out (no turn)
        }
    }


    protected override void OnFixedUpdate()
    {
        if (_cont == null) return;

        ApplySpeedLimit();
        HandleObstacleReward();
        HandleTrackWrap();
        HandlePerfectScore();
    }

    protected virtual void AddObservationsToSensor(VectorSensor sensor)
    {
        // 1. Agent's lateral position (normalized)
        sensor.AddObservation(_xPosition); // [-1, 1]

        // 2. Obstacle visibility flag
        sensor.AddObservation(_visibilityFlag);

        // 3. Relative lateral position to obstacle (normalized)
        sensor.AddObservation(_obstacleDistanceX); // [-2, 2]

        // 4. Relative longitudinal distance to obstacle (normalized)
        sensor.AddObservation(_obstacleDistanceZ); // [-1, 1] or [0, 1] if always ahead

        //Debug.Log($"Observations: _xPosition: {_xPosition} \t _visibilityFlag: {_visibilityFlag} \t _obstacleDistanceX: {_obstacleDistanceX} \t _obstacleDistanceZ: {_obstacleDistanceZ}");
    }


    private void Awake()
    {
        Academy.Instance.OnEnvironmentReset += () =>
        {
            Time.captureFramerate = 0;
            Time.fixedDeltaTime = 0.02f;

            QualitySettings.vSyncCount = 1;     // re-enable vsync
            Application.targetFrameRate = 60;   // cap FPS so visuals = realtime
        };

        _bgTex = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.5f));
        base.Awake();
    }

    private void Start()
    {
        Application.runInBackground = true;
        _cont = GetComponent<PrometeoCarController>();
        _startPosition = transform.position;
        Time.timeScale = TimeScale;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void OnDestroy()
    {
        if (_bgTex != null)
        {
            Destroy(_bgTex);
            _bgTex = null;
        }
    }

    private void ApplySpeedLimit()
    {
        _cont.Acc = _cont.carSpeed < MaxSpeed;
    }

    private void HandleObstacleReward()
    {
        if (transform.position.z >= 38 && !_hasObstacleReward)
        {
            AddReward(1f + 0.3f * _loopsSurvived++);
            _hasObstacleReward = true;
            Academy.Instance.StatsRecorder.Add("Custom/_loopsSurvived", _loopsSurvived);
        }
    }

    // Car reaches end of track: wrap to start and reset obstacle
    private void HandleTrackWrap()
    {
        if (transform.position.z > ResetLength)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, 0);

            // Place a new obstacle ahead
            if (Obstacle != null)
            {
                if (Random.value < 1f)
                {
                    _hasObstacleReward = false;
                    int randomState = Random.Range(1, 13); // 0 to 12 inclusive
                    float x = GetXFromState(randomState);

                    Obstacle.transform.position = new Vector3(x, 1.0f, 40);
                }
                else
                {
                    Obstacle.transform.position = new Vector3(0f, -10f, 0f); // Hide
                }
            }
        }
    }

    private void HandlePerfectScore()
    {
        if (_currentStep >= 10000)
        {
            AddReward(500);
            Academy.Instance.StatsRecorder.Add("Custom/_perfectScore", 1);
            LogAndEndEpisode();
        }
    }

    private float GetXFromState(int state)
    {
        return -5.5f + state * 1f; // assuming state 0 = -5.5, state 1 = -4.5, ..., state 12 = 6.5
    }

    private void OnTriggerEnter(Collider other)
    {
        AddReward(-100f);
        Academy.Instance.StatsRecorder.Add("Custom/_perfectScore", 0);
        LogAndEndEpisode();
    }

    private void ResetEpisode()
    {
        int randomState = Random.Range(0, 13); // 0 to 12 inclusive
        float randomX = GetXFromState(randomState);
        transform.position = new Vector3(randomX, _startPosition.y, _startPosition.z);
        transform.rotation = Quaternion.identity;


        _loopsSurvived = 0;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (_cont != null)
        {
            _cont.LevelOut();
            _cont.Acc = true;
        }

        _episodeCount++;
        _currentStep = 0;

        RequestDecision();
    }

    private void TakeAction(int action)
    {
        float targetX = transform.position.x;

        switch (action)
        {
            case 0: targetX -= 1f; break;
            case 1: targetX += 1f; break;
            case 2: targetX += 0; break;
        }

        if (action == 2)
        {
            ApplyRewards();
            RequestDecision();
            _carDeltaX = 0;
        }
        else
        {
            targetX = Mathf.Clamp(targetX, RANGEMIN, RANGEMAX);
            StartCoroutine(MoveToXOverTime(targetX));
        }
    }

    private void ApplyRewards()
    {
        Vector3 obstaclePos = Obstacle.transform.position;
        float reward = GetReward();

        if (obstaclePos.y > 0)
        {
            //Debug.Log($"Reward: {reward}");
            AddReward(reward);
        }

        if (transform.position.x < -5.5f || transform.position.x > 5.5f)
        {
            AddReward(-500f);
            Academy.Instance.StatsRecorder.Add("Custom/_perfectScore", 0);
            LogAndEndEpisode();;
        }
    }

    private float GetReward()
    {
        Vector3 obstaclePos = Obstacle.transform.position;
        Vector3 agentPos = transform.position;

        float laneHalfWidth = RANGEMAX;
        float trackLength = 45f;

        float obstacleDistanceX = (obstaclePos.x - agentPos.x) / laneHalfWidth;
        float obstacleDistanceZ = (obstaclePos.z - agentPos.z) / trackLength;

        return Mathf.Abs((2f * obstacleDistanceX) * (0.5f * obstacleDistanceZ));
    }

    private void LogAndEndEpisode()
    {
        float epReturn = GetCumulativeReward();                // final, up-to-date
        Academy.Instance.StatsRecorder.Add("Custom/EpisodeReturn", epReturn);
        _totalReward += epReturn;
        _totalSteps += _currentStep;
        _completedEpisodes++;                                  // track completed, not started
        StopAllCoroutines();
        EndEpisode();
    }

    private IEnumerator MoveToXOverTime(float targetX)
    {
        float tolerance = 0.01f;

        while (Mathf.Abs(transform.position.x - targetX) > tolerance)
        {
            float step = NominalSpeed * Time.fixedDeltaTime;
            float newX = Mathf.MoveTowards(transform.position.x, targetX, step);
            _carDeltaX = newX- transform.position.x;
            GetComponent<Rigidbody>().MovePosition(new Vector3(newX, transform.position.y, transform.position.z));
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Rigidbody>().MovePosition(new Vector3(targetX, transform.position.y, transform.position.z));

        // Now that movement has finished, apply rewards and request next decision
        ApplyRewards();
        RequestDecision();
    }

    private void EnsureGuiInited()
    {
        // Must be called from inside OnGUI only
        if (_labelStyle == null || _boxStyle == null)
        {
            // Accessing GUI.skin.* is only legal inside OnGUI
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _bgTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true
            };
            _labelStyle.normal.textColor = Color.white;
        }
    }

    private void OnGUI()
    {
#if !UNITY_EDITOR
        return;
#endif

        EnsureGuiInited(); // <-- initialize styles here (legal place)

        // --- GUI scaling matrix ---
        float referenceHeight = 1080f;
        float scale = Screen.height / referenceHeight;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        // --------------------------

        const float width = 330f;
        const float height = 380f;

        // --- Correctly position panel using scaled screen size ---
        float scaledScreenWidth = Screen.width / scale;
        float scaledScreenHeight = Screen.height / scale;
        Rect panelRect = new Rect(scaledScreenWidth - width - 10f, scaledScreenHeight - height - 10f, width, height);
        // ---------------------------------------------------------

        GUI.Box(panelRect, GUIContent.none, _boxStyle);
        GUILayout.BeginArea(panelRect);

        GUILayout.Label("<b>Agent Observations</b>", _labelStyle);
        GUILayout.Space(5f);

        float trackLength = 45f;
        Vector3 agentPos = transform.position;
        float agentZNorm = agentPos.z / trackLength;

        var hasEpisodes = _completedEpisodes > 0;

        float avgReward = hasEpisodes ? _totalReward / _completedEpisodes : 0f;
        float avgSteps = hasEpisodes ? (float)_totalSteps / _completedEpisodes : 0f;

        GUILayout.Label($"<b>Agent X (normalized):</b> {_xPosition:F2}", _labelStyle);
        GUILayout.Label($"<b>Obstacle Visible:</b> {_visibilityFlag}", _labelStyle);
        GUILayout.Label($"<b>Relative X to Obstacle:</b> {_obstacleDistanceX:F2}", _labelStyle);
        GUILayout.Label($"<b>Relative Z to Obstacle:</b> {_obstacleDistanceZ:F2}", _labelStyle);
        GUILayout.Label($"<b>Agent Z (normalized):</b> {agentZNorm:F2}", _labelStyle);

        GUILayout.Space(10f);
        GUILayout.Label("<b>Status</b>", _labelStyle);

        string actionName = _previousAction == 0 ? "Left" :
                            _previousAction == 1 ? "Right" :
                            _previousAction == 2 ? "Level" : "N/A";

        GUILayout.Label($"<b>Current Action:</b> {actionName}", _labelStyle);
        GUILayout.Label($"<b>Episode Reward:</b> {_episodeReward:F3}", _labelStyle);
        GUILayout.Label($"<b>Average Reward:</b> {avgReward:F3}", _labelStyle);
        GUILayout.Label($"<b>Step in Episode:</b> {_currentStep}", _labelStyle);
        GUILayout.Label($"<b>Average Steps per Episode:</b> {avgSteps:F3}", _labelStyle);
        GUILayout.Label($"<b>Episode #:</b> {_episodeCount}", _labelStyle);
        GUILayout.Label($"<b>TimeScale #:</b> {Time.timeScale}", _labelStyle);

        GUILayout.EndArea();

        GUI.matrix = oldMatrix;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}