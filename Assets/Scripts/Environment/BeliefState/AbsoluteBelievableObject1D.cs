using System;
using UnityEngine;
using UnityEngine.XR;

public class AbsoluteBelievableObject1D : BelievableObject<float>, IBinState
{
    public BeliefState1D BeliefState { get; private set; }

    public override float Entropy01 => BeliefState.Entropy01;

    public double[] ProbabilityDistribution { get => BeliefState.ProbabilityDistribution; }


    private double _currentSigmaMean = 0;

    private System.Random _rand;


    public override void InitBeliefState(System.Random rand, BelievableObjectConfig believableObjectConfig)
    {
        BeliefState = new(0, believableObjectConfig.NumberOfBins, believableObjectConfig.ObservationProbability, believableObjectConfig.RangeMin, believableObjectConfig.RangeMax, rand);
        _rand = rand;
        BelievableObjectConfig = believableObjectConfig;
    }

    //Updates the s_carLocationProbabilities based on the following formula: b`(s_) = O(s,a,o) SUM(s_e_S){ T(s,a,s_)*b(s)}
    public override void UpdateBeliefState(float updateTime)
    {
        //_estimatedVelocity is only updated based on the true velocity if the current instance is active
        if (IsVisible)
        {
            EstimatedVelocity = GetComponent<Rigidbody>().linearVelocity.x * updateTime;
            _currentSigmaMean = BelievableObjectConfig.SigmaMean;
        }
        else
        {
            float currentVelocityX = !BelievableObjectConfig.UseEstimatedVelocity ? GetComponent<Rigidbody>().linearVelocity.x * updateTime : EstimatedVelocity;

            _currentSigmaMean = _currentSigmaMean / 2;

            //adding inaccuracy every step the focus is not on the task
            NormalDistribution normalDistributionMeanX = new NormalDistribution(currentVelocityX, _currentSigmaMean);

            float meanX = (float)normalDistributionMeanX.Sample(_rand);

            EstimatedVelocity = meanX;
        }

        float meanVelocity = BelievableObjectConfig.UseSampleVelocity ? EstimatedVelocity : GetComponent<Rigidbody>().linearVelocity.x * updateTime;

        try
        {
            BeliefState.PropagateWithGaussianVelocity(IsVisible, meanVelocity, BelievableObjectConfig.Sigma, transform.localPosition.x, BelievableObjectConfig.NumberOfSamples);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.LogError(ex.Message);
        }
    }

    public override float GetBeliefState()
    {
        return BeliefState.GetBeliefPosition();
    }

    public override double GetProbabilityForTrueState()
    {
        return BeliefState.GetProbability(GetBinTruePosition());
    }

    public override bool IsBeliefStateEqualToTrueState()
    {
        return BeliefState.GetBeliefBin() == GetBinTruePosition();
    }


    private int GetBinTruePosition()
    {
        return BeliefState.GetBin(transform.localPosition.x);
    }
}
