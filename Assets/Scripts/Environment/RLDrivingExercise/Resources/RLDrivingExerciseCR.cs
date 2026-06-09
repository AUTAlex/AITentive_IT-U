using System;
using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.XR;

public class RLDrivingExerciseCR : RLDrivingExercise, ICRTask
{
    [field: SerializeField, Tooltip("Update period of the location probabilities of the car over the x.axis."), ProjectAssign(Header = "Human Cognition Model")]
    public float UpdatePeriod { get; set; } = 0.1f;

    [field: SerializeField, Tooltip("Defines how much samples should be taken to calculate the probability distributions."), ProjectAssign]
    public int NumberOfSamples { get; set; } = 100;

    [field: SerializeField, Tooltip("Sigma value used for the calculation of the normal distribution of the location probabilities."), ProjectAssign]
    public double Sigma { get; set; } = 0.1;

    [field: SerializeField, Tooltip("Describes O(s,a,o) of the formula b`(s_) = O(s_,a,o) SUM(s_e_S){ T(s,a,s_)*b(s)}."), ProjectAssign]
    public double ObservationProbability { get; set; } = 0.9;

    [field: SerializeField, Tooltip("Specify the number of bins in which the lanes should be divided."), ProjectAssign]
    public int NumberOfBins { get; set; } = 100;

    [field: SerializeField, Tooltip("If enabled, the car becomes visible or invisible at random intervals."), ProjectAssign]
    public bool RandomizeCarVisibility { get; set; } = false;

    [field: SerializeField, Tooltip("Maximum time in seconds between random visibility changes."), ProjectAssign, Hidden(FieldName = "RandomizeCarVisibility")]
    public float MaxVisibilityInterval { get; set; } = 10;

    public bool IsVisible {
        get 
        {
            return _isVisible;
        }
        
        set 
        {
            _eyeCanvas.SetActive(value);
            _isVisible = value;
        } 
    }

    public double[] GetLocationProbabilities()
    {
        return _carLocationProbabilities;
    }


    private double[] _carLocationProbabilities;
    private System.Random _rand;
    private GameObject _eyeCanvas;
    private bool _isVisible = false;
    private float _updateTimer = 0.0f;
    private float _visibilityTimer = 0.0f;
    private float _currentInterval = 0.0f;
    private float _traveledDistanceX = 0.0f;


    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        InitializeCarLocationProbabilities(NumberOfBins, RANGEMIN, RANGEMAX);
    }


    protected override void AddObservationsToSensor(VectorSensor sensor)
    {
        // 1. Agent's lateral position (normalized)
        float laneHalfWidth = RANGEMAX;
        sensor.AddObservation(GetCarBeliefPositionOnXAxis() / laneHalfWidth); // [-1, 1]

        // 2. Obstacle visibility flag
        sensor.AddObservation(IsVisible);

        // 3. Relative lateral position to obstacle (normalized)
        sensor.AddObservation(_obstacleDistanceX); // [-2, 2]

        // 4. Relative longitudinal distance to obstacle (normalized)
        sensor.AddObservation(_obstacleDistanceZ); // [-1, 1] or [0, 1] if always ahead

        //Debug.Log($"CRx: {GetCarBeliefPositionOnXAxis() / laneHalfWidth}; _xPosition: {_xPosition}");
    }

    protected override void OnFixedUpdate()
    {
        base.OnFixedUpdate();
        UpdateBeliefState();
        UpdateVisibility();
    }

    protected new void Awake()
    {
        base.Awake();

        _rand = new System.Random();
        _eyeCanvas = transform.parent.transform.GetChildByName("Camera").GetChildByName("Eye_Canvas").gameObject;
    }


    private void UpdateBeliefState()
    {
        _updateTimer += Time.fixedDeltaTime;
        _traveledDistanceX += _carDeltaX;

        if (UpdatePeriod < _updateTimer)
        {
            _updateTimer = 0;
            UpdateCarLocationProbabilities();

            _traveledDistanceX = 0;
        }
    }

    private void UpdateVisibility()
    {
        _visibilityTimer += Time.fixedDeltaTime;
        _currentInterval = _currentInterval == 0 ? GetNextInterval() : _currentInterval;

        if (RandomizeCarVisibility && _visibilityTimer >= _currentInterval)
        {
            IsVisible = !IsVisible;
            _visibilityTimer = 0;
            _currentInterval = GetNextInterval();
        }
    }

    private float GetNextInterval()
    {
        return (float)_rand.NextDouble() * MaxVisibilityInterval;
    }

    private void InitializeCarLocationProbabilities(int numberOFBins, float rangeMin, float rangeMax)
    {
        _carLocationProbabilities = new double[numberOFBins];

        for (int i = 0; i < numberOFBins; i++)
        {
            if (PositionConverter.ContinuousValueToBin(transform.localPosition.x, rangeMin, rangeMax, numberOFBins) == i)
            {
                _carLocationProbabilities[i] = 1;
            }
            else
            {
                _carLocationProbabilities[i] = 0;
            }
        }
    }

    private void UpdateCarLocationProbabilities()
    {
        if (_carLocationProbabilities == null)
        {
            return;
        }

        float[] normal = CRUtil.GetNormalDistributionForVelocity(NumberOfSamples, _traveledDistanceX, Sigma, _rand);

        double[] currentCarLocationProbabilities = (double[])_carLocationProbabilities.Clone();

        NativeArray<float> normalNative = new NativeArray<float>(normal, Allocator.TempJob);
        NativeArray<double> currentCarLocationProbabilitiesNative = new NativeArray<double>(currentCarLocationProbabilities, Allocator.TempJob);
        NativeArray<double> carLocationProbabilitiesNative = new NativeArray<double>(NumberOfBins, Allocator.TempJob);

        ObjectIn1DLocationProbabilitiesUpdateJob carLocationProbabilitiesUpdateJob = new()
        {
            NormalDistributionForVelocity = normalNative,
            CurrentObjectLocationProbabilities = currentCarLocationProbabilitiesNative,
            ObjectLocationProbabilities = carLocationProbabilitiesNative,
            RangeMin = RANGEMIN,
            RangeMax = RANGEMAX,
            NumberOFBins = NumberOfBins,
            ObjectPosition = PositionConverter.ContinuousValueToBinCenter(transform.localPosition.x, RANGEMIN, RANGEMAX, NumberOfBins),
            IsVisibleInstance = IsVisible,
            ObservationProbability = ObservationProbability
        };

        JobHandle jobHandle = carLocationProbabilitiesUpdateJob.Schedule(NumberOfBins, 16);
        jobHandle.Complete();

        //direct assignment of the array would overwrite the reference to the original array (e.g. reference of subclass)
        Array.Copy(carLocationProbabilitiesNative.ToArray(), _carLocationProbabilities, NumberOfBins);

        //Normalize the updated belief b`(s_)
        double sum = _carLocationProbabilities.Sum();

        if (sum == 0)
        {
            Debug.LogError($"The sum of probabilities is equal to 0. Usually this can happen when the a velocity value of the normal distribution is to big resulting in an index error in the edge bin correction logic. Try to reduce the sigma value. Distribution before update: [{string.Join(",", currentCarLocationProbabilities)}]");
        }

        //Debug.Log($"_traveledDistanceX: {_traveledDistanceX}; Prob: [{string.Join(",", currentCarLocationProbabilities.Select(p => p.ToString("F3")))}]");

        for (int i = 0; i < NumberOfBins; i++)
        {
            _carLocationProbabilities[i] = _carLocationProbabilities[i] / sum;
        }

        normalNative.Dispose();
        currentCarLocationProbabilitiesNative.Dispose();
        carLocationProbabilitiesNative.Dispose();
    }

    private float GetCarBeliefPositionOnXAxis()
    {
        if (_carLocationProbabilities == null)
        {
            return 0;
        }

        return PositionConverter.BinToContinuousValue(GetCarBeliefBin(), RANGEMIN, RANGEMAX, NumberOfBins);
    }

    private int GetCarBeliefBin()
    {
        if (_carLocationProbabilities == null)
        {
            return -1;
        }

        double maxValue = _carLocationProbabilities.Max();
        int maxIndex = _carLocationProbabilities.ToList().IndexOf(maxValue);

        return maxIndex;
    }
}