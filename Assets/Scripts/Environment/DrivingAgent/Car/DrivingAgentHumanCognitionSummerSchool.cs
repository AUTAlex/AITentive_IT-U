using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.XR;

public class DrivingAgentHumanCognitionSummerSchool : DrivingAgentHumanCognitionBase<float>
{
    public override bool IsVisible { get; set; }


    private int _maxCars = 0;


    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(BelievableObject.EstimatedVelocity);
        sensor.AddObservation(CarController.engineRPMRaw);

        sensor.AddObservation(BelievableObject.GetBeliefState());
        sensor.AddOneHotObservation((int)GameManager.CurrentObservedTargetLane, 3);
        
        sensor.AddObservation(GameManager.CarSpeed);
        sensor.AddObservation(GameManager.CurrentObservedTargetSpeed);

        sensor.AddObservation(GameManager.CurrentObservedDistanceToOverheadSign); //needed to decide when to change lanes (e.g. should lane be changed when distance is 100 units or only at 0 units)
        sensor.AddObservation(GameManager.CurrentObservedDistanceToSpeedSign); //needed to decide when to adapt speed (e.g. should speed already adapted when distance is 100 units or only at 0 units)
        sensor.AddObservation(1 - BelievableObject.Entropy01);
        sensor.AddObservation(IsVisible);

        AddTraficToBeliefState(sensor);

        //Debug.Log($"Reward: {GetReward()}\t Position: {(int)GetProjectedCarPositionIn(_projectionTravelTime).x}\t Observed Target Lane: {GameManager.CurrentObservedTargetLane}\t Observed Target Position: {GameManager.CurrentObservedTargetPosition.x}\t Observed Target Speed: {(int)GameManager.CurrentObservedTargetSpeed}\t Speed: {(int)CarSpeed}\t Distance Overhead Sign:{GameManager.CurrentObservedDistanceToOverheadSign}\t Distance Speed Sign:{GameManager.CurrentObservedDistanceToSpeedSign}");
    }

    protected override void SetRewards()
    {
        //Replace with custom logic
        float reward = GetReward();

        SetReward(reward);
    }

    protected override float GetReward()
    {
        float projectedDistanceToTargetLaneCenter = Math.Abs(GetProjectedCarPositionIn(_projectionTravelTime).x - GameManager.CurrentObservedTargetPosition.x);
        float laneReward = GaussianReward(projectedDistanceToTargetLaneCenter, LANESIGMA);

        float maxDistanceToTargetSpeed = Math.Max(Math.Abs(GameManager.CurrentObservedTargetSpeed), Math.Abs(GameManager.CurrentObservedTargetSpeed - MaxCarSpeed));
        float distanceToTargetSpeed = Mathf.Abs(CarSpeed - GameManager.CurrentObservedTargetSpeed);
        float normalizedSpeedDistanceReward = (1 - distanceToTargetSpeed / maxDistanceToTargetSpeed);

        float reward = normalizedSpeedDistanceReward * laneReward;
        float rpmReward = (CarController.engineRPM - 900) / (7000 * 1000);

        return reward <= 0.0001 ? rpmReward > 0 ? rpmReward : 0 : reward;
    }

    protected override void InitBelievableObject()
    {
        //Register custom belief representation of type BelievableObject here, to adapt the beliefState type change TYPE in DrivingAgentHumanCognitionBase<TYPE> 
        BelievableObject = BeliefUpdater.RegisterBelievableObject<AbsoluteBelievableObject1D>(gameObject, BelievableObjectConfig);
    }

    protected override void AssignAction(ActionBuffers actionBuffers)
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

    protected override float GetDistanceBetweenTrueAndBeliefPosition()
    {
        return Math.Abs(BelievableObject.GetBeliefState() - transform.position.x);
    }

    protected override void OnFixedUpdate()
    {
        base.OnFixedUpdate();

        UpdateBeliefStateVisualization();
    }


    private void Start()
    {
        _maxCars = GameManager.ActiveScenarioSettings.MaxCars;
    }

    private void UpdateBeliefStateVisualization()
    {
        BeliefCarPosition.transform.localPosition = new Vector3(BelievableObject.GetBeliefState(), transform.localPosition.y, transform.localPosition.z);

        if (ShowBeliefState)
        {
            SpeedText.text = GameManager.CurrentObservedTargetSpeed.ToString();
        }
    }

    private void AddTraficToBeliefState(VectorSensor sensor)
    {
        List<Vector2> distances = GetDistanceVectors();

        for (int i = 0; i < _maxCars; i++)
        {
            if (i < distances.Count)
            {
                sensor.AddObservation(distances[i]);
            }
            else
            {
                sensor.AddObservation(Vector2.zero);
            } 
        }
    }

    private List<Vector2> GetDistanceVectors()
    {
        List<Vector2> distances = new();

        Vector2 carPosition = new Vector2(
            BeliefCarPosition.transform.localPosition.x,
            BeliefCarPosition.transform.localPosition.z
        );

        foreach (BelievableObject<Vector2> believableObject
                 in BeliefUpdater.BelievableObjects.OfType<BelievableObject<Vector2>>().ToArray())
        {
            Vector2 distance = carPosition - believableObject.GetBeliefState();

            if (distance.magnitude <= 100f)
            {
                distances.Add(distance);
            }
        }

        return distances;
    }

    private void PrintVector2List(List<Vector2> vectors)
    {
        string result = "[" + string.Join(", ", vectors.Select(v => $"({v.x}, {v.y})")) + "]";
        Debug.Log(result);
    }
}
