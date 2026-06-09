using System;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.XR;

public class BeliefState1D
{
    public float Entropy01 
    { 
        get 
        { 
            return Util.Entropy01(ProbabilityDistribution); 
        } 
    }

    public double[] ProbabilityDistribution { get; private set; }


    private int _numberOfBins;

    private double _observationProbability;

    private float _rangeMin;

    private float _rangeMax;

    private System.Random _rand;


    public BeliefState1D(float trueState, int numberOfBins, double observationProbability, float rangeMin, float rangeMax, System.Random random)
    {
        _numberOfBins = numberOfBins;
        _observationProbability = observationProbability;
        _rangeMin = rangeMin;
        _rangeMax = rangeMax;
        _rand = random;
        InitializeProbabilityDistribution(trueState);
    }

    public void InitializeProbabilityDistribution(float trueState)
    {
        ProbabilityDistribution = new double[_numberOfBins];

        for (int i = 0; i < _numberOfBins; i++)
        {
            if (PositionConverter.ContinuousValueToBin(trueState, _rangeMin, _rangeMax, _numberOfBins) == i)
            {
                ProbabilityDistribution[i] = 1;
            }
            else
            {
                ProbabilityDistribution[i] = 0;
            }
        }
    }

    public float GetBeliefPosition()
    {
        if (ProbabilityDistribution == null)
        {
            return 0;
        }

        return PositionConverter.BinToContinuousValue(GetBeliefBin(), _rangeMin, _rangeMax, _numberOfBins);
    }

    public int GetBeliefBin()
    {
        if (ProbabilityDistribution == null)
        {
            return -1;
        }

        double maxValue = ProbabilityDistribution.Max();
        int maxIndex = ProbabilityDistribution.ToList().IndexOf(maxValue);

        return maxIndex;
    }

    public int GetBin(float value)
    {
        return PositionConverter.ContinuousValueToBin(PositionConverter.ContinuousValueToBinCenter(value, _rangeMin, _rangeMax, _numberOfBins), _rangeMin, _rangeMax, _numberOfBins);
    }

    public double GetProbability(int bin)
    {
        if (ProbabilityDistribution.IsNullOrEmpty())
        {
            return 0;
        }

        return ProbabilityDistribution[bin];
    }

    public void PropagateWithGaussianVelocity(bool isObservable, float meanVelocity, double velocityStandardDeviation, float trueState, int numberOfSamples)
    {
        float[] normal = CRUtil.GetNormalDistributionForVelocity(numberOfSamples, meanVelocity, velocityStandardDeviation, _rand);

        double[] currentProbabilityDistribution = (double[])ProbabilityDistribution.Clone();

        NativeArray<float> normalNative = new NativeArray<float>(normal, Allocator.TempJob);
        NativeArray<double> currentCarLocationProbabilitiesNative = new NativeArray<double>(currentProbabilityDistribution, Allocator.TempJob);
        NativeArray<double> carLocationProbabilitiesNative = new NativeArray<double>(_numberOfBins, Allocator.TempJob);

        ObjectIn1DLocationProbabilitiesUpdateJob carLocationProbabilitiesUpdateJob = new()
        {
            NormalDistributionForVelocity = normalNative,
            CurrentObjectLocationProbabilities = currentCarLocationProbabilitiesNative,
            ObjectLocationProbabilities = carLocationProbabilitiesNative,
            RangeMin = _rangeMin,
            RangeMax = _rangeMax,
            NumberOFBins = _numberOfBins,
            ObjectPosition = PositionConverter.ContinuousValueToBinCenter(trueState, _rangeMin, _rangeMax, _numberOfBins),
            IsVisibleInstance = isObservable,
            ObservationProbability = _observationProbability
        };

        JobHandle jobHandle = carLocationProbabilitiesUpdateJob.Schedule(_numberOfBins, 16);
        jobHandle.Complete();

        //direct assignment of the array would overwrite the reference to the original array (e.g. reference of subclass)
        Array.Copy(carLocationProbabilitiesNative.ToArray(), ProbabilityDistribution, _numberOfBins);

        //Normalize the updated belief b`(s_)
        double sum = ProbabilityDistribution.Sum();

        if (sum == 0)
        {
            throw new ArgumentOutOfRangeException("The sum of probabilities is equal to 0. Usually this can happen when the a velocity value of the normal distribution is to big resulting in an index error in the edge bin correction logic. Try to reduce the sigma value.");
        }

        for (int i = 0; i < _numberOfBins; i++)
        {
            ProbabilityDistribution[i] = ProbabilityDistribution[i] / sum;
        }

        normalNative.Dispose();
        currentCarLocationProbabilitiesNative.Dispose();
        carLocationProbabilitiesNative.Dispose();
    }
}
