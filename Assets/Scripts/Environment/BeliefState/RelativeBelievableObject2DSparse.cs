using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;


public class RelativeBelievableObject2DSparse : BelievableObject<Vector2>, IBinState
{
    public double[] ProbabilityDistribution { get => _beliefStateXAxis.ProbabilityDistribution.Concat(_beliefStateZAxis.ProbabilityDistribution).ToArray(); }


    private BeliefState1D _beliefStateXAxis;

    private BeliefState1D _beliefStateZAxis;

    private float _estimatedVelocityX = 0;

    private float _estimatedVelocityZ = 0;

    private double _currentSigmaMean = 0;

    private System.Random _rand;

    private Vector3 _lastObservedCarPosition;

    public override float Entropy01 => throw new NotImplementedException();

    public override void InitBeliefState(System.Random rand, BelievableObjectConfig believableObjectConfig)
    {
        _beliefStateXAxis = new(0, believableObjectConfig.NumberOfBins, believableObjectConfig.ObservationProbability, believableObjectConfig.RangeMin, believableObjectConfig.RangeMax, rand);
        _beliefStateZAxis = new(0, believableObjectConfig.NumberOfBins, believableObjectConfig.ObservationProbability, believableObjectConfig.RangeMin, believableObjectConfig.RangeMax, rand);
        _rand = rand;
        BelievableObjectConfig = believableObjectConfig;
        _lastObservedCarPosition = transform.localPosition;
    }

    //Updates the s_carLocationProbabilities based on the following formula: b`(s_) = O(s,a,o) SUM(s_e_S){ T(s,a,s_)*b(s)}
    public override void UpdateBeliefState(float updateTime)
    {
        //_estimatedVelocity is only updated based on the true velocity if the current instance is active
        if (IsVisible)
        {
            _estimatedVelocityX = GetComponent<Rigidbody>().linearVelocity.x * updateTime;
            _estimatedVelocityZ = GetComponent<Rigidbody>().linearVelocity.z * updateTime;
            _currentSigmaMean = BelievableObjectConfig.SigmaMean;
            _lastObservedCarPosition = transform.localPosition;
        }
        else
        {
            float currentVelocityX = !BelievableObjectConfig.UseEstimatedVelocity ? GetComponent<Rigidbody>().linearVelocity.x * updateTime : _estimatedVelocityX;
            float currentVelocityZ = !BelievableObjectConfig.UseEstimatedVelocity ? GetComponent<Rigidbody>().linearVelocity.z * updateTime : _estimatedVelocityZ;

            _lastObservedCarPosition.x = _lastObservedCarPosition.x + currentVelocityX;
            _lastObservedCarPosition.z = _lastObservedCarPosition.z + currentVelocityZ;

            _currentSigmaMean = _currentSigmaMean / 2;

            //adding inaccuracy every step the focus is not on the task
            NormalDistribution normalDistributionMeanX = new NormalDistribution(currentVelocityX, _currentSigmaMean);
            NormalDistribution normalDistributionMeanZ = new NormalDistribution(currentVelocityZ, _currentSigmaMean);

            float meanX = (float)normalDistributionMeanX.Sample(_rand);
            float meanZ = (float)normalDistributionMeanZ.Sample(_rand);

            _estimatedVelocityX = meanX;
            _estimatedVelocityZ = meanZ;
        }

        float meanDeltaVelocityX = BelievableObjectConfig.UseSampleVelocity ? GetComponent<Rigidbody>().linearVelocity.x * updateTime - _estimatedVelocityX : 0;
        float meanDeltaVelocityZ = BelievableObjectConfig.UseSampleVelocity ? GetComponent<Rigidbody>().linearVelocity.z * updateTime - _estimatedVelocityZ : 0;

        try
        {
            _beliefStateXAxis.PropagateWithGaussianVelocity(IsVisible, meanDeltaVelocityX, BelievableObjectConfig.Sigma, 0, BelievableObjectConfig.NumberOfSamples);
            _beliefStateZAxis.PropagateWithGaussianVelocity(IsVisible, meanDeltaVelocityZ, BelievableObjectConfig.Sigma, 0, BelievableObjectConfig.NumberOfSamples);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.LogError(ex.Message);
        }

        //Vector2 beliefStateDelta = new Vector2(_beliefStateXAxis.GetBeliefPosition(), _beliefStateZAxis.GetBeliefPosition());
        //Debug.Log($"beliefStateDelta: {beliefStateDelta}, ");
    }

    public override Vector2 GetBeliefState()
    {
        Vector3 carPosition = IsVisible ? transform.localPosition : _lastObservedCarPosition;
        Vector2 beliefState = new Vector2(carPosition.x - _beliefStateXAxis.GetBeliefPosition(), carPosition.z - _beliefStateZAxis.GetBeliefPosition());

        return beliefState;
    }

    public override double GetProbabilityForTrueState()
    {
        throw new NotImplementedException();
    }

    public override bool IsBeliefStateEqualToTrueState()
    {
        throw new NotImplementedException();
    }
}
