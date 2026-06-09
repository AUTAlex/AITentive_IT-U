using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ObstacleBehavior : Agent
{
    public const float ResetLength = 45;
    public const float MaxSpeed = 70;

    public GameObject obstacle; // Assign in Inspector

    private PrometeoCarController cont = null;
    private Vector3 startPosition;

    private bool isMoving = false;

    public float DecisionTime = 0.1f;
    private float DecisionTimer = 0.1f;

    bool crashed = false;

    void Start()
    {

        Application.runInBackground = true;
        cont = GetComponent<PrometeoCarController>();
        startPosition = transform.position;
    }

    bool ObstacleReward = false;

    void FixedUpdate()
    {
        if (cont == null) return;

        cont.Acc = cont.carSpeed < MaxSpeed;

        if (transform.position.z >= 38 && !ObstacleReward)
        {
            AddReward(1f + 0.3f * loopsSurvived++);
            ObstacleReward = true;
            Academy.Instance.StatsRecorder.Add("Custom/_loopsSurvived", loopsSurvived);
        }


        // Car reaches end of track: wrap to start and reset obstacle
        if (transform.position.z > ResetLength)
        {

            transform.position = new Vector3(transform.position.x, transform.position.y, 0);

            // Place a new obstacle ahead
            if (obstacle != null)
            {
                if (Random.value < 1f)
                {
                    ObstacleReward = false;
                    int randomState = Random.Range(1, 12); // 0 to 12 inclusive
                    float x = GetXFromState(randomState);

                    obstacle.transform.position = new Vector3(x, 1.0f, 40);
                }
                else
                {
                    obstacle.transform.position = new Vector3(0f, -10f, 0f); // Hide
                }
            }
        }


        //   CrashedIntoObstacle();


        /*  DecisionTimer -= Time.deltaTime;

          if (DecisionTimer < 0f)
          {
              RequestDecision();
              DecisionTimer = DecisionTime;
          }*/
    }

    float GetXFromState(int state)
    {
        return -5.5f + state * 1f; // assuming state 0 = -5.5, state 1 = -4.5, ..., state 12 = 6.5
    }

    private void OnTriggerEnter(Collider other)
    {
        //   if (other.CompareTag("Obstacle"))
        {
            //   Debug.Log("Hit obstacle!");
            // Optional: set a flag, penalize, or end the episode
            AddReward(-1f);
            //  Debug.Log("Crashed");
            EndEpisode();

        }
    }

    public override void OnEpisodeBegin()
    {
        ResetEpisode();
    }



    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 obstaclePos = obstacle.transform.position;
        Vector3 agentPos = transform.position;

        float laneHalfWidth = 6.5f;
        float trackLength = 45f;

        // 1. Agent's lateral position (normalized)
        sensor.AddObservation(agentPos.x / laneHalfWidth); // [-1, 1]

        // 2. Obstacle visibility flag
        sensor.AddObservation(obstaclePos.y > 0 ? 1f : 0f);

        // 3. Relative lateral position to obstacle (normalized)
        float relX = (obstaclePos.x - agentPos.x) / laneHalfWidth; // [-2, 2]
        sensor.AddObservation(relX);

        // 4. Relative longitudinal distance to obstacle (normalized)
        float relZ = 1 - ((obstaclePos.z - agentPos.z) / trackLength); // [-1, 1] or [0, 1] if always ahead
        sensor.AddObservation(relZ);

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        TakeAction(actions.DiscreteActions[0]);

        previousAction = actions.DiscreteActions[0];
        episodeReward = GetCumulativeReward();
        currentStep++;

        ApplyRewards();

    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActions[0] = 0; // Turn Left
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActions[0] = 1; // Turn Right
        }
        else
        {
            discreteActions[0] = 2; // Level Out (no turn)
        }
    }

    int loopsSurvived = 0;
    private void ResetEpisode()
    {
        // Debug.Log("New Episode");
        //float randomX = Random.Range(-5.5f, 5.5f);

        int randomState = Random.Range(0, 13); // 0 to 12 inclusive
        float randomX = GetXFromState(randomState);
        transform.position = new Vector3(randomX, startPosition.y, startPosition.z);
        transform.rotation = Quaternion.identity;


        loopsSurvived = 0;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;


            rb.angularVelocity = Vector3.zero;
        }

        if (cont != null)
        {
            cont.LevelOut();
            cont.Acc = true;
        }
        episodeCount++;
        currentStep = 0;

        crashed = false;

        RequestDecision();
    }

    private float GetLongitudinalDistance(Vector3 obstaclePos, float agentZ)
    {
        if (obstaclePos.y < 0f || obstaclePos.z <= agentZ)
            return -1f; // Inactive or behind agent

        float x = obstaclePos.x;
        bool match = true;

        return match ? ((obstaclePos.z - agentZ) / 40) : -1f;
    }


    private void CrashedIntoObstacle()
    {
        if (obstacle == null || obstacle.transform.position.y < 0f) return;

        float dz = Mathf.Abs(transform.position.z - obstacle.transform.position.z);
        float dx = Mathf.Abs(transform.position.x - obstacle.transform.position.x);

        if (dz < 5f && dx < 3f)
        {
            AddReward(-1f);
            //  Debug.Log("Crashed");
            EndEpisode();
        }
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
            StartCoroutine(WaitforReward());
        }
        else
        {

            //   targetX = Mathf.Clamp(targetX, -6.5f, 6.5f);
            //   if (float.IsNaN(targetX) || float.IsInfinity(targetX)) return;
            //   MoveToX(targetX);

            targetX = Mathf.Clamp(targetX, -6.5f, 6.5f);
            StopAllCoroutines();
            StartCoroutine(MoveToXOverTime(targetX));
        }
    }

    private void ApplyRewards()
    {

        Vector3 obstaclePos = obstacle.transform.position;
        Vector3 agentPos = transform.position;

        float laneHalfWidth = 6.5f;
        float trackLength = 45f;

        // 1. Agent's lateral position (normalized)
        var agentLat = agentPos.x / laneHalfWidth;
        var isObstacle = obstaclePos.y > 0;
        var relX = (obstaclePos.x - agentPos.x) / laneHalfWidth;
        var relZ = 1 - ((obstaclePos.z - agentPos.z) / trackLength);

        if (obstaclePos.y > 0)
        {
            /*  if (Mathf.Abs(obstaclePos.x - agentPos.x) < 2.5f)
                  AddReward(-1f);
              else*/
            AddReward(Mathf.Abs(relX) * relZ);
        }



        if (transform.position.x < -5.5f || transform.position.x > 5.5f)
        {
            AddReward(-10f);
            EndEpisode();
        }
    }





    private IEnumerator WaitforReward()
    {
        yield return new WaitForSeconds(0.1f);
        ApplyRewards();
        RequestDecision();
    }

    private IEnumerator MoveToXOverTime(float targetX)
    {
        isMoving = true;
        float speed = 8f; // tweak this for realism
        float tolerance = 0.01f;

        while (Mathf.Abs(transform.position.x - targetX) > tolerance)
        {
            float step = speed * Time.deltaTime;
            float newX = Mathf.MoveTowards(transform.position.x, targetX, step);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            yield return null;
        }

        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        isMoving = false;

        // Now that movement has finished, apply rewards and request next decision
        ApplyRewards();
        RequestDecision();
    }

    private void MoveToX(float targetX)
    {
        this.transform.position = new Vector3(targetX, this.transform.position.y, this.transform.position.z);
    }

    int previousAction = -1;
    float episodeReward = 0f;
    int currentStep = 0;
    int episodeCount = 0;

    void OnGUI()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.5f)); // semi-transparent black

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.richText = true;
        labelStyle.normal.textColor = Color.white;

        float width = 330f;
        float height = 360f;
        Rect panelRect = new Rect(Screen.width - width - 10f, 10f, width, height);
        GUI.Box(panelRect, GUIContent.none, boxStyle);

        GUILayout.BeginArea(panelRect);

        GUILayout.Label("<b>Agent Observations</b>", labelStyle);
        GUILayout.Space(5f);

        Vector3 agentPos = transform.position;
        Vector3 obstaclePos = obstacle.transform.position;
        float laneHalfWidth = 6.5f;
        float trackLength = 45f;

        float agentXNorm = agentPos.x / laneHalfWidth;
        float obsVisible = obstaclePos.y > 0 ? 1f : 0f;
        float relX = (obstaclePos.x - agentPos.x) / laneHalfWidth;
        float relZ = (obstaclePos.z - agentPos.z) / trackLength;
        float agentZNorm = agentPos.z / trackLength;

        GUILayout.Label($"<b>Agent X (normalized):</b> {agentXNorm:F2}", labelStyle);
        GUILayout.Label($"<b>Obstacle Visible:</b> {obsVisible}", labelStyle);
        GUILayout.Label($"<b>Relative X to Obstacle:</b> {relX:F2}", labelStyle);
        GUILayout.Label($"<b>Relative Z to Obstacle:</b> {relZ:F2}", labelStyle);
        GUILayout.Label($"<b>Agent Z (normalized):</b> {agentZNorm:F2}", labelStyle);

        GUILayout.Space(10f);
        GUILayout.Label("<b>Status</b>", labelStyle);

        string actionName = previousAction == 0 ? "Left" :
                            previousAction == 1 ? "Right" :
                            previousAction == 2 ? "Level" : "N/A";

        GUILayout.Label($"<b>Current Action:</b> {actionName}", labelStyle);
        GUILayout.Label($"<b>Episode Reward:</b> {episodeReward:F3}", labelStyle);
        GUILayout.Label($"<b>Step in Episode:</b> {currentStep}", labelStyle);
        GUILayout.Label($"<b>Episode #:</b> {episodeCount}", labelStyle);

        GUILayout.EndArea();
    }


    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }


}