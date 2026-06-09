using Newtonsoft.Json;
using System;
using UnityEngine;


[Serializable, JsonObject]
public class BelievableObjectConfig
{
    public BelievableObjectConfig(float updatePeriod, int numberOfSamples, double sigma, double sigmaMean, bool useEstimatedVelocity, bool useSampleVelocity, double observationProbability, float rangeMin, float rangeMax, int numberOfBins = 0)
    {
        UpdatePeriod = updatePeriod;
        NumberOfSamples = numberOfSamples;
        Sigma = sigma;
        SigmaMean = sigmaMean;
        UseEstimatedVelocity = useEstimatedVelocity;
        UseSampleVelocity = useSampleVelocity;
        ObservationProbability = observationProbability;
        NumberOfBins = numberOfBins;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
    }

    [field: SerializeField, Tooltip("Update period of the location probabilities of the car over the x.axis."), ProjectAssign(Header = "Human Cognition Model")]
    public float UpdatePeriod { get; set; } = 0.1f;

    [field: SerializeField, Tooltip("Defines how much samples should be taken to calculate the probability distributions."), ProjectAssign]
    public int NumberOfSamples { get; set; } = 100;

    [field: SerializeField, Tooltip("Sigma value used for the calculation of the normal distribution of the location probabilities."), ProjectAssign]
    public double Sigma { get; set; } = 0.1;

    [field: SerializeField, Tooltip("Sigma value used for the calculation of the mean value which is used to calculate the normal distribution of the location probabilities."), ProjectAssign, Hidden(FieldName = "UseSampleVelocity")]
    public double SigmaMean { get; set; } = 0.01;

    [field: SerializeField, Tooltip("Describes O(s,a,o) of the formula b`(s_) = O(s_,a,o) SUM(s_e_S){ T(s,a,s_)*b(s)}."), ProjectAssign]
    public double ObservationProbability { get; set; } = 0.9;

    [field: SerializeField, Tooltip("Specify the number of bins in which the lanes should be divided."), ProjectAssign]
    public int NumberOfBins { get; set; } = 1000;

    [field: SerializeField, Tooltip("If true, a velocity sample from the normal distribution using SigmaMean is used for the car probability update instead of the true velocity.."), ProjectAssign]
    public bool UseSampleVelocity { get; set; }

    [field: SerializeField, Tooltip("If true, the agent uses the last observed velocity instead of the current velocity. This assumes that the agent does not directly observe or account for the immediate effects of its steering actions on velocity."), ProjectAssign, Hidden(FieldName = "UseSampleVelocity")]
    public bool UseEstimatedVelocity { get; set; }

    [field: SerializeField, Tooltip("Defines the lower bound of the discretized belief state range."), ProjectAssign]
    public float RangeMax;

    [field: SerializeField, Tooltip("Defines the upper bound of the discretized belief state range."), ProjectAssign]
    public float RangeMin;
}