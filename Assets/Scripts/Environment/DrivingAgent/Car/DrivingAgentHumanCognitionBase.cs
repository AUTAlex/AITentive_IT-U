using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.UI;


public abstract class DrivingAgentHumanCognitionBase<TBeliefState> : DrivingAgent, ICRTask
{
    [field: SerializeField, ProjectAssign]
    public BelievableObjectConfig BelievableObjectConfig { get; set; }

    [field: SerializeField, Tooltip("Observation is active for this agent independent of the focus/supervisor agent."), ProjectAssign]
    public bool FullVision { get; set; } = false;

    [field: SerializeField, Tooltip("Visualizes the current belief position of the car (gray car) and the observed speed limit."), ProjectAssign]
    public bool ShowBeliefState { get; set; }

    [field: SerializeField, Tooltip("If true, information about rewards are logged."), ProjectAssign]
    public bool LogRewardDetails { get; set; } = false;

    [field: SerializeField]
    public Text SpeedText { get; set; }

    [SerializeField]
    private Material GhostMaterial;

    public BelievableObject<TBeliefState> BelievableObject { get; set; }


    protected GameObject BeliefCarPosition;

    protected BeliefUpdater BeliefUpdater;

    protected int LastObservedSpeed;


    public abstract override void CollectObservations(VectorSensor sensor);

    protected abstract void InitBelievableObject();

    protected abstract float GetDistanceBetweenTrueAndBeliefPosition();


    private const float NULLOBSERVATIONPENALTY = 0.3f;

    private VisionAgent _visionAgent;

    private bool? _isVisible = null;

    public virtual bool IsVisible
    {
        get
        {
            if(_isVisible != null)
            {
                return _isVisible.Value;
            }

            if (FullVision)
            {
                return true;
            }
            else
            {
                if (UseVisionAgent)
                {
                    if (GetPixelVisionAgent())
                    {
                        return GetProjectionAtGaze() != null && GetRoadGameObjectAtGaze() != null;
                    }
                    
                    return FocusStateSpace.Encoding[0] == 1;
                }
                else
                {
                    return IsActive;
                }
            }
        }

        set 
        { 
            _isVisible = value;
        }
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        GameManager.CurrentObservedTargetLane = GameManager.GetTargetLane(this.gameObject).Value;
        GameManager.CurrentObservedTargetSpeed = GameManager.CurrentTargetSpeed;
        LastObservedSpeed = 0;

        if (ShowBeliefState)
        {
            ConfigurableJoint hoodCameraJoint = transform.GetChildByName("HoodCamera").gameObject.GetComponent<ConfigurableJoint>();
            hoodCameraJoint.autoConfigureConnectedAnchor = false;
            hoodCameraJoint.connectedAnchor = new Vector3(0, 2, -7);
        }
    }


    protected new void Awake()
    {
        base.Awake();

        BeliefUpdater = GetComponent<BeliefUpdater>();
        InitBelievableObject();

        _visionAgent = gameObject.GetFirstParentWithEnabledComponent<VisionAgent>();
        if ( _visionAgent != null ) _visionAgent.NullObservationPenalty = NULLOBSERVATIONPENALTY;

        BeliefCarPosition = GhostVisualizationUtil.InstantiateBeliefStateVisualization(gameObject, GhostMaterial, transform.parent);
        SetGhostCarVisible(false);

        if (ShowBeliefState)
        {
            SetGhostCarVisible(true);
            SpeedText.transform.parent.parent.gameObject.SetActive(true);
        }
        else
        {
            GetComponent<ProbabilityBarChart>().enabled = false;
        }
    }

    private void SetGhostCarVisible(bool showGhostCar)
    {
        foreach (Renderer renderer in BeliefCarPosition.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = showGhostCar;
        }
    }

    protected float GetVisionFactor()
    {
        float carPositionFactor = (1 - BelievableObject.Entropy01) * 0.5f;
        float targetLaneFactor = GameManager.WasNextTargetLaneObserved || GameManager.WasCurrentTargetLaneObserved ? 0.25f : 0;
        float targetSpeedFactor = GameManager.WasNextTargetSpeedObserved || GameManager.WasCurrentTargetSpeedObserved ? 0.25f : 0;

        float factor = carPositionFactor + targetLaneFactor + targetSpeedFactor;

        if (LogRewardDetails)
        {
            Debug.Log($"[Reward Debug] Driving Reward = {GetReward()}; Vision Factor = {factor}; Entropy01 = {BelievableObject.Entropy01}; Probability of Car Position Bin: {BelievableObject.GetProbabilityForTrueState()}; Composition: Car Position = {carPositionFactor}, Target Lane Observation = {targetLaneFactor}, Target Speed Observation: {targetSpeedFactor}, Projection at Gaze: {GetProjectionAtGaze()}");
        }

        return factor;
    }

    private PixelVisionAgent GetPixelVisionAgent()
    {
        return _visionAgent != null && _visionAgent is PixelVisionAgent ? (PixelVisionAgent)_visionAgent : null;
    }

    private bool IsFocusedByPixelVisionAgent()
    {
        return GetPixelVisionAgent() && ReferenceEquals(GetPixelVisionAgent().FocusedTask, this);
    }

    private void UpdateCurrentGoal()
    {
        if (IsFocusedByPixelVisionAgent())
        {
            UpdateCurrentGoalPixelVisionAgent();
        }
        else if (IsVisible)
        {
            UpdateCurrentGoalFocusAgent();
        }   
    }

    private void UpdateProjectionTravelTime()
    {
        if (IsFocusedByPixelVisionAgent())
        {
            GameObject gameObject = GetRoadGameObjectAtGaze();

            if (gameObject != null)
            {
                _projectionTravelTime = GetTimeToReachObjectZ(gameObject);
            }
            else
            {
                _projectionTravelTime = 0;
            }
        }
    }

    private GameObject GetRoadGameObjectAtGaze()
    {
        return GetPixelVisionAgent().ObjectsAtGaze.FirstOrDefault(x => x != null && x.name == "RoadTile(Clone)");
    }

    private GameObject GetProjectionAtGaze()
    {
        return GetPixelVisionAgent().ObjectsAtGaze.FirstOrDefault(x => x != null && x.name == "GhostCar");
    }

    private void UpdateCurrentGoalPixelVisionAgent()
    {
        List<GameObject> observedObjects = GetPixelVisionAgent().ObjectsAtGaze;

        GameManager.UpdateCurrentObservedTargetSpeed(this.gameObject, observedObjects);
        GameManager.UpdateCurrentObservedTargetPosition(this.gameObject, observedObjects);
        GameManager.UpdateCurrentObservedDistanceToOverheadSign(this.gameObject, observedObjects);
        GameManager.UpdateCurrentObservedDistanceToSpeedSign(this.gameObject, observedObjects);
        
        int observedCarSpeed = GetSpeedTextValue(observedObjects);
        LastObservedSpeed = observedCarSpeed != - 1 ? observedCarSpeed : LastObservedSpeed;
        //Debug.Log(_lastObservedSpeed);
    }

    private void UpdateCurrentGoalFocusAgent()
    {
        GameManager.UpdateCurrentObservedTargetSpeed(this.gameObject);
        GameManager.UpdateCurrentObservedTargetPosition(this.gameObject);
        GameManager.UpdateCurrentObservedDistanceToOverheadSign(this.gameObject);
        GameManager.UpdateCurrentObservedDistanceToSpeedSign(this.gameObject);
    }


    protected override void OnFixedUpdate()
    {
        base.OnFixedUpdate();

        UpdateCurrentGoal();
        UpdateProjectionTravelTime();

        //Debug.Log($"Distance Overhead Sign: {GameManager.GetDistanceToNextObject<OverheadSign>(gameObject).GetValueOrDefault()} \t Distance Speed Sign: {GameManager.GetDistanceToNextObject<SpeedSign>(gameObject).GetValueOrDefault()}");
        //if(!_carLocationProbabilities.IsNullOrEmpty()) Debug.Log($"_entropy: {_entropy}; GetVisionFactor(): {GetVisionFactor()}; GetCarPositionProbability: {GetCarPositionProbability()}; Highest Probability: {_carLocationProbabilities.Max()}; IsVisible: {IsVisible}; PositionAware: {GetCarBin() == GetCarBeliefBin()}; GetDistanceBetweenTrueAndBeliefPosition(): {GetDistanceBetweenTrueAndBeliefPosition()}");
    }

    protected override Vector3 GetProjectedCarPositionIn(float seconds)
    {
        Vector3 velocity = GetComponent<Rigidbody>().linearVelocity;

        Vector3 projectionDistance = new Vector3(BelievableObject.EstimatedVelocity / BelievableObjectConfig.UpdatePeriod, velocity.y, velocity.z) * seconds;
        Vector3 projectedCarPosition = BeliefCarPosition.transform.position + (projectionDistance.z < 4 ? new Vector3(projectionDistance.x, projectionDistance.y, 4) : projectionDistance);

        return projectedCarPosition;
    }


    private float GetTimeToReachObjectZ(GameObject gameObject)
    {
        Vector3 velocity = GetComponent<Rigidbody>().linearVelocity;

        Renderer renderer = gameObject.GetComponent<Renderer>();
        Vector3 center = renderer.bounds.center;
        float halfLength = renderer.bounds.size.z * 0.5f;
        Vector3 nearEdge = gameObject.transform.position - gameObject.transform.forward * halfLength;

        float distanceZ = Mathf.Abs(nearEdge.z - transform.position.z);
        
        if (velocity.z < 0.01)
        {
            return 0;
        }

        return distanceZ / Mathf.Abs(velocity.z);
    }

    private int GetSpeedTextValue(List<GameObject> observedObjects)
    {
        GameObject speedTextObject = observedObjects.Find(obj => obj != null && obj.name == "SpeedText");

        if (speedTextObject == null)
            return -1;

        TMP_Text textComponent = speedTextObject.GetComponent<TMP_Text>();

        if (textComponent == null)
            return -1;

        if (int.TryParse(textComponent.text, out int value))
            return value;

        return -1;
    }
}
