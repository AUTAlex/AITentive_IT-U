using Algorithms;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Haptics;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Utils;

public class DrivingAgent : Task
{
    [field: SerializeField, Header("Settings"), ProjectAssign]
    public float MaxCarSpeed { get; set; } = 150;

    [field: SerializeField, Tooltip("Visualizes the next car position based on current velocity."), ProjectAssign]
    public bool ShowProjectedCarPositionOnX { get; set; }

    [field: SerializeField, Header("Realistic Car Controller")]
    // public GameObject carPrefab;
    public RCC_CarControllerV3 CarController { get; set; }

    [field: SerializeField]
    public GameManager GameManager { get; set; }

    [field: SerializeField]
    public RoadManager RoadManager { get; set; }

    [field: SerializeField]
    public Shader Shader { get; set; }

    public float CarSpeed { get; set; } = 0f;

    public override List<string> IgnoredTagsForVision
    {
        get
        {
            return new() { "PlayerCar" };
        }
    }

    public override List<string> TransparentTagsForVision
    {
        get
        {
            return new() { "Observable" };
        }
    }


    protected float _projectionTravelTime = 2;


    private GameObject _projectedCarPosition;


    public override IStateInformation StateInformation
    {
        get
        {
            _drivingStateInformation ??= new DrivingStateInformation();

            _drivingStateInformation.DistanceToNextSpeedSign = GameManager.GetDistanceToNextObject<SpeedSign>(gameObject).GetValueOrDefault();
            _drivingStateInformation.DistanceToNextOverheadSign = GameManager.GetDistanceToNextObject<OverheadSign>(gameObject).GetValueOrDefault(); 
            _drivingStateInformation.VelocityX = GetComponent<Rigidbody>().linearVelocity.x;
            _drivingStateInformation.DistanceToTargetPosition = Vector3.Distance(transform.position, TargetPosition);
            _drivingStateInformation.DistanceToTargetPositionX = Math.Abs(transform.position.x - TargetPosition.x);
            _drivingStateInformation.DistanceToTargetSpeed = Mathf.Abs(CarController.speed - GameManager.CurrentTargetSpeed);
            _drivingStateInformation.WasCurrentTargetLaneObserved = GameManager.WasCurrentTargetLaneObserved;
            _drivingStateInformation.WasCurrentTargetSpeedObserved = GameManager.WasCurrentTargetSpeedObserved;
            _drivingStateInformation.WasNextTargetLaneObserved = GameManager.WasNextTargetLaneObserved;
            _drivingStateInformation.WasNextTargetSpeedObserved = GameManager.WasNextTargetSpeedObserved;
            _drivingStateInformation.CurrentSpeed = CarController.speed;
            _drivingStateInformation.TargetSpeed = GameManager.CurrentTargetSpeed;
            _drivingStateInformation.CurrentXPosition = transform.position.x;
            _drivingStateInformation.TargetXPosition = TargetPosition.x;

            return _drivingStateInformation;
        }
        set
        {
            _drivingStateInformation = value as DrivingStateInformation;
        }
    }


    protected Vector3 TargetPosition = Vector3.zero;

    protected float _steerInput = 0f;

    protected float _brakeInput = 0f;

    protected float _accelerationInput = 0f;

    protected float _handbrakeInput = 0f;

    protected int _stepCountDecisionRequester;

    protected float _fixedUpdateTimer;

    protected const float LANESIGMA = 2f;


    private DrivingStateInformation _drivingStateInformation;

    private int _currentStep = 0;

    private Vector2 _currentInput;

    private float? _acceleration = null;

    private DrivingMetrics _drivingMetrics;


    public override Dictionary<string, double> AccumulatedPerformance => _drivingMetrics != null ? _drivingMetrics.AccumulatedPerformance : null;

    public override void OnMove(InputValue value) 
    {
        _currentInput = value.Get<Vector2>();
    }

    public void OnAccelerate(InputValue value)
    {
        _acceleration = value.Get<float>();
    }

    public override void OnEpisodeBegin()
    {
        //base.OnEpisodeBegin();
        Debug.Log("Begin Episode");

        GameManager.RestartSimulation();
        CarController.externalController = true;
        CarController.maxspeed = MaxCarSpeed;
        _currentStep = 0;
    }

    public override void ResetAccumulatedPerformance() 
    {
        _drivingMetrics = new();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(GetComponent<Rigidbody>().linearVelocity.x); //the AMS should learn that this value should be near to 0 in case of a switch
        sensor.AddObservation(transform.position);
        sensor.AddObservation(TargetPosition);
        sensor.AddObservation(CarSpeed);
        sensor.AddObservation(GameManager.CurrentTargetSpeed);
        sensor.AddObservation(CarController.engineRPMRaw);
    }

    public override void OnActionReceivedInternal(ActionBuffers actionBuffers)
    {
        List<dynamic> actions = new();
        var continuousActionsOut = actionBuffers.ContinuousActions;

        actions.Add(continuousActionsOut[0]);
        actions.Add(continuousActionsOut[1]);

        ITask.InvokeOnAction(actions, this);

        CarSpeed = CarController.speed;
        TargetPosition = GameManager.GetClosestPointOnLineSegment(transform.position);

        // Move the agent using the action.
        MoveAgent(actionBuffers);
        SetRewards();
        _fixedUpdateTimer = 0;
    }

    public override void AddTrueObservationsToSensor(VectorSensor sensor)
    {
        sensor.AddObservation(GetComponent<Rigidbody>().linearVelocity.x); //the AMS should learn that this value should be near to 0 in case of a switch
        sensor.AddObservation(transform.position);
        sensor.AddObservation(TargetPosition);
        sensor.AddObservation(GameManager.CurrentObservedTargetPosition);
        sensor.AddObservation(GameManager.GetDistanceToNextObject<OverheadSign>(gameObject).GetValueOrDefault());
        sensor.AddObservation(CarSpeed);
        sensor.AddObservation(GameManager.CurrentTargetSpeed);
        sensor.AddObservation(GameManager.CurrentObservedTargetSpeed);
        sensor.AddObservation(GameManager.GetDistanceToNextObject<SpeedSign>(gameObject).GetValueOrDefault());
    }

    public override void UpdateDifficultyLevel() { }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;

        continuousActionsOut[0] = _currentInput.x;
        continuousActionsOut[1] = _currentInput.y;

        if (_acceleration.HasValue)
        {
            continuousActionsOut[1] = _acceleration.Value;
        }
    }


    protected virtual void SetRewards()
    {
        float reward = GetReward();

        TaskRewardForFocusAgent.Enqueue((reward, Priority));
        TaskRewardForSupervisorAgent.Enqueue((reward, Priority));
        SetReward(reward);
    }

    protected new void Awake()
    {
        base.Awake();
        _projectedCarPosition = CreateGhostCar(new Vector3(2.5f, 1.5f, 1f), transform.parent, ShowProjectedCarPositionOnX);
    }

    protected override void OnEnable()
    {
        GameManager.OnFinishReachedAction += CatchFinishReached;
        _stepCountDecisionRequester = 0;
        _drivingMetrics = new();

        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GameManager.OnFinishReachedAction -= CatchFinishReached;
    }

    protected override void OnFixedUpdate()
    {
        if (_currentStep > 45000 && CarController.speed < 10)
        {
            SetPenalty();
        }

        if (IsActive || IsAutonomous)
        {
            if (_stepCountDecisionRequester >= DecisionPeriod)
            {
                _stepCountDecisionRequester = 0;
                RequestDecision();
            }
        }

        _projectedCarPosition.transform.position = GetProjectedCarPositionIn(_projectionTravelTime);

        _stepCountDecisionRequester++;
        _currentStep++;
        _fixedUpdateTimer += Time.fixedDeltaTime;
    }

    protected override void OnUpdate()
    {
        if(_drivingMetrics != null)
        {
            _drivingMetrics.TargetLanePosition = TargetPosition.x;
            _drivingMetrics.TargetSpeed = GameManager.CurrentTargetSpeed;
            _drivingMetrics.RecordMeasurement(CarSpeed, transform.position.x);
        }
    }

    protected virtual float GetReward()
    {
        float projectedDistanceToTargetLaneCenter = Math.Abs(GetProjectedCarPositionIn(_projectionTravelTime).x - TargetPosition.x);
        float laneReward = GaussianReward(projectedDistanceToTargetLaneCenter, LANESIGMA);

        float maxDistanceToTargetSpeed = Math.Max(Math.Abs(GameManager.CurrentTargetSpeed), Math.Abs(GameManager.CurrentTargetSpeed - MaxCarSpeed));
        float distanceToTargetSpeed = Mathf.Abs(CarSpeed - GameManager.CurrentTargetSpeed);
        float normalizedSpeedDistanceReward = (1 - distanceToTargetSpeed / maxDistanceToTargetSpeed);

        float reward = normalizedSpeedDistanceReward * laneReward;
        float rpmReward = (CarController.engineRPM - 900) / (7000 * 1000);

        return reward <= 0.0001 ? rpmReward > 0 ? rpmReward : 0 : reward;
    }

    protected virtual Vector3 GetProjectedCarPositionIn(float seconds)
    {
        Vector3 velocity = GetComponent<Rigidbody>().linearVelocity;

        return transform.position + velocity * seconds;
    }

    protected virtual void AssignAction(ActionBuffers actionBuffers)
    {
        //0 => steering
        //1 => speed
        //2 => break

        var actionBuffersContinuousActions = actionBuffers.ContinuousActions;

        _steerInput = actionBuffersContinuousActions[0];
        _accelerationInput = actionBuffersContinuousActions[1];
        _brakeInput = actionBuffersContinuousActions[1];
        _handbrakeInput = 0f;

        //  Clamping inputs.
        _steerInput = Mathf.Clamp(_steerInput, -1f, 1f) * CarController.direction;
        _accelerationInput = Mathf.Clamp01(_accelerationInput);
        _brakeInput = Mathf.Abs(Mathf.Clamp(_brakeInput, -1, 0f));
    }

    protected float GaussianReward(float distance, float sigma)
    {
        return Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));
    }


    private void OnCollisionEnter(Collision collision)
    {
        _drivingMetrics.HasCrashed = true;
        SetPenalty(collision);
    }

    private void SetPenalty(Collision collision = null)
    {
        TaskRewardForSupervisorAgent.Enqueue((-50, Priority));

        if (collision != null)
        {
            Debug.Log($"End Episode, SetPenalty was triggered: car collided with {collision.gameObject.name}.");
        }
        else
        {
            Debug.Log("End Episode, SetPenalty was triggered.");
        }

        EndEpisode();
    }

    private void CatchFinishReached(object sender, bool aborted)
    {
        EndEpisode();
    }

    private void MoveAgent(ActionBuffers actionBuffers)
    {
        AssignAction(actionBuffers);
        FeedRCC();
    }

    private void FeedRCC()
    {
        if (!CarController.canControl)
        {
            return;
        }

        // Feeding throttleInput of the RCC.
        if (!CarController.changingGear && !CarController.cutGas)
            CarController.throttleInput =
                (CarController.direction == 1 ? Mathf.Clamp01(_accelerationInput) : Mathf.Clamp01(_brakeInput));
        else
            CarController.throttleInput = 0f;

        if (!CarController.changingGear && !CarController.cutGas)
            CarController.brakeInput =
                (CarController.direction == 1 ? Mathf.Clamp01(_brakeInput) : Mathf.Clamp01(_accelerationInput));
        else
            CarController.brakeInput = 0f;

        CarController.steerInput = _steerInput;

        CarController.handbrakeInput = _handbrakeInput;
    }

    private GameObject CreateGhostCar(Vector3 carSize, Transform parent = null, bool isVisible = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "GhostCar";
        go.transform.localScale = carSize;
        if (parent) go.transform.SetParent(parent, false);
        go.tag = "Observable";

        go.GetComponent<BoxCollider>().isTrigger = true;
        var rend = go.GetComponent<Renderer>();

        if (!isVisible)
        {
            var depthOnly = new Material(Shader);
            rend.material = depthOnly; // invisible to color, writes depth
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        if (this is not ICRTask)
        {
            go.GetComponent<BoxCollider>().enabled = false;
        }

        return go;
    }

}