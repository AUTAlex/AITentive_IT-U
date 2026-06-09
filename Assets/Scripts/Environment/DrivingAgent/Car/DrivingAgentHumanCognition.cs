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


public class DrivingAgentHumanCognition : DrivingAgentHumanCognitionBase<float>
{

    public void AddBeliefObservationsToSensor(VectorSensor sensor)
    {
        sensor.AddObservation(1-BelievableObject.Entropy01);
        sensor.AddObservation(new Vector3(BelievableObject.GetBeliefState(), transform.position.y, transform.position.z)); // needed to observe a potential crash
        sensor.AddObservation(BelievableObject.EstimatedVelocity);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(BelievableObject.EstimatedVelocity);
        sensor.AddObservation(new Vector3(GetProjectedCarPositionIn(_projectionTravelTime).x, transform.position.y, transform.position.z));
        sensor.AddOneHotObservation((int)GameManager.CurrentObservedTargetLane, 3);
        sensor.AddObservation(GameManager.CurrentObservedDistanceToOverheadSign); //needed to decide when to change lanes (e.g. should lane be changed when distance is 100 units or only at 0 units)
        sensor.AddObservation(LastObservedSpeed);
        sensor.AddObservation(GameManager.CurrentObservedTargetSpeed);
        sensor.AddObservation(GameManager.CurrentObservedDistanceToSpeedSign); //needed to decide when to adapt speed (e.g. should speed already adapted when distance is 100 units or only at 0 units)
        sensor.AddObservation(1- BelievableObject.Entropy01);
        sensor.AddObservation(IsVisible);
        sensor.AddObservation(CarController.engineRPMRaw);

        UpdateTensorBoard();
        //Debug.Log($"Target Position: {(int)GameManager.GetClosestPointOnLineSegment(gameObject.transform.position).x}\t Target Lane: {(int)GameManager.CurrentObservedTargetPosition.x}\t Position: {(int)GetProjectedCarPositionIn(_projectionTravelTime).x}\t Target Speed: {(int)GameManager.CurrentObservedTargetSpeed}\t Speed: {(int)CarSpeed}\t Distance Overhead Sign:{GameManager.CurrentObservedDistanceToOverheadSign}\t Distance Speed Sign:{GameManager.CurrentObservedDistanceToSpeedSign}");
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        BeliefCarPosition.transform.localPosition = new Vector3(BelievableObject.GetBeliefState(), transform.localPosition.y, transform.localPosition.z);
    }

    protected override void InitBelievableObject()
    {
        BelievableObject = BeliefUpdater.RegisterBelievableObject<AbsoluteBelievableObject1D>(gameObject, BelievableObjectConfig);
    }

    protected override void SetRewards()
    {
        float reward = GetReward();
        float focusAgentReward = reward >= 0 ? reward * GetVisionFactor() : reward * (1 - GetVisionFactor());

        TaskRewardForFocusAgent.Enqueue((focusAgentReward, Priority));
        TaskRewardForSupervisorAgent.Enqueue((reward, Priority));
        SetReward(reward);
    }

    protected override void OnFixedUpdate()
    {
        base.OnFixedUpdate();

        UpdateBeliefStateVisualization();
    }


    private void UpdateBeliefStateVisualization()
    {
        BeliefCarPosition.transform.localPosition = new Vector3(BelievableObject.GetBeliefState(), transform.localPosition.y, transform.localPosition.z);

        if (ShowBeliefState)
        {
            SpeedText.text = GameManager.CurrentObservedTargetSpeed.ToString();
        }
    }

    protected override float GetDistanceBetweenTrueAndBeliefPosition()
    {
        return Math.Abs(BelievableObject.GetBeliefState() - transform.position.x);
    }


    private void UpdateTensorBoard()
    {
        StatsRecorder statsRecorder = Academy.Instance.StatsRecorder;
        statsRecorder.Add("Custom/BeliefEntropy", (float)BelievableObject.Entropy01);
        statsRecorder.Add("Custom/IsVisible", IsVisible ? 1f : 0f);
        statsRecorder.Add("Custom/VisionFactor", GetVisionFactor());
        statsRecorder.Add("Custom/CarPositionProbability", (float)BelievableObject.GetProbabilityForTrueState());
        statsRecorder.Add("Custom/PositionAware", BelievableObject.IsBeliefStateEqualToTrueState() ? 1f : 0f);
        statsRecorder.Add("Custom/DistanceBetweenTrueAndBeliefPosition", GetDistanceBetweenTrueAndBeliefPosition());
    }
}
