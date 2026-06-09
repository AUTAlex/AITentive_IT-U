using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrivingStateInformation : IStateInformation
{
    [Measure]
    public float DistanceToNextSpeedSign { get; set; }

    [Measure]
    public float DistanceToNextOverheadSign { get; set; }

    [Measure]
    public float VelocityX { get; set; }

    [Measure]
    public float DistanceToTargetPosition { get; set; }

    [Measure]
    public float DistanceToTargetPositionX { get; set; }

    [Measure]
    public float DistanceToTargetSpeed { get; set; }

    [Measure]
    public bool WasCurrentTargetSpeedObserved { get; set; }

    [Measure]
    public bool WasNextTargetSpeedObserved { get; set; }

    [Measure]
    public bool WasCurrentTargetLaneObserved { get; set; }

    [Measure]
    public bool WasNextTargetLaneObserved { get; set; }

    [Measure]
    public float TargetSpeed { get; set; }

    [Measure]
    public float CurrentSpeed { get; set; }

    [Measure]
    public float TargetXPosition { get; set; }

    [Measure]
    public float CurrentXPosition { get; set; }


    public Array AveragePerformedActionsDiscretizedSpace { get; set; }
    public Dictionary<Type, Array> AverageReactionTimesDiscretizedSpace { get; set; }

    public Vector3 ActionRangeMax => throw new NotImplementedException();

    public Vector3 ActionRangeMin => throw new NotImplementedException();

    public int NumberOfActionBinsPerAxis => throw new NotImplementedException();

    public int[] BehaviorDimensions => throw new NotImplementedException();

    public List<dynamic> PerformedActions => throw new NotImplementedException();

    //TODO: Add properties for driving state information
    public int[] GetDiscretizedRelationalStateInformation(IStateInformation sourceTaskState, int timeBin = 0)
    {
        throw new System.NotImplementedException();
    }

    public int[] GetDiscretizedStateInformation()
    {
        throw new System.NotImplementedException();
    }

    public int[] GetRelationalDimensions(Type type, int numberOfTimeBins = 1)
    {
        throw new NotImplementedException();
    }

    public void UpdateStateInformation(IStateInformation stateInformation)
    {
        throw new NotImplementedException();
    }

    public void UpdateMeasurementSettings(IStateInformation stateInformation)
    {
        throw new NotImplementedException();
    }

    public IStateInformation GetCopyOfCurrentState()
    {
        DrivingStateInformation copy = new();
        copy.UpdateStateInformation(this);

        return copy;
    }

    public bool ActionIsInUsualRange(List<dynamic> actionsPerformedSoFar, List<dynamic> performedAction)
    {
        throw new NotImplementedException();
    }
}
