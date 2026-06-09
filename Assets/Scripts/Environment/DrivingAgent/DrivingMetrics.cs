using Algorithms;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DrivingMetrics
{
    public bool HasCrashed { private get; set; } = false;

    public float TargetSpeed { private get; set; } = 100f;

    public float TargetLanePosition { private get; set; } = 0f;


    private int _measureCount = 0;

    private float _sumSpeedDistance = 0f;

    private float _sumSquaredSpeedDistance = 0f;

    private float _sumLaneDistance = 0f;

    private float _sumSquaredLaneDistance = 0f;


    public void RecordMeasurement(float speed, float lanePosition)
    {
        float laneDistance = Mathf.Abs(lanePosition - TargetLanePosition);
        float speedDistance = Mathf.Abs(speed - TargetSpeed);

        _sumLaneDistance += laneDistance;
        _sumSpeedDistance += speedDistance;
        _sumSquaredLaneDistance += Mathf.Pow(laneDistance, 2);
        _sumSquaredSpeedDistance += Mathf.Pow(speedDistance, 2);

        _measureCount++;
    }

    public Dictionary<string, double> AccumulatedPerformance
    {
        get
        {
            float meanSpeedDistance = _sumSpeedDistance / _measureCount;
            float sdSpeedDistance = (float)Math.Sqrt(_sumSquaredSpeedDistance/_measureCount-Math.Pow(meanSpeedDistance, 2));
            float meanLaneDistance = _sumLaneDistance / _measureCount;
            float sdLaneDistance = (float)Math.Sqrt(_sumSquaredLaneDistance / _measureCount - Math.Pow(meanLaneDistance, 2));

            return new Dictionary<string, double>
            {
                { "MeanSpeedDistance",  meanSpeedDistance},
                { "SdSpeedDistance",  sdSpeedDistance},
                { "MeanLaneDistance",  meanLaneDistance},
                { "SdLaneDistance",  sdLaneDistance},
                { "Crashed", Convert.ToDouble(HasCrashed)}
            };
        }
    }
}
