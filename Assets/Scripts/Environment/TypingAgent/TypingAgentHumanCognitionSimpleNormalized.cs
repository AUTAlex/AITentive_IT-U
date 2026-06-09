using System;
using Algorithms;
using UnityEngine;

/// <summary>
/// Reward range: [-inf, 1]
/// </summary>
public class TypingAgentHumanCognitionSimpleNormalized : TypingAgentHumanCognitionSimple
{
    private const float NULLOBSERVATIONPENALTY = 0.5f;

    internal const float SPEEDTYPINGREWARDRATIO = 0.5f;

    internal const float WPMMAX = 85;

    internal float _normalizedTypingRewardSum;


    private float _previousLevenshteinDistance;
    private float _previousFrequencyDistance;
    private float _previousLengthDistance;


    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        VisionAgent visionAgent = gameObject.GetFirstParentWithEnabledComponent<VisionAgent>();
        if (visionAgent != null) visionAgent.NullObservationPenalty = NULLOBSERVATIONPENALTY / GetMaxPossibleReward(_currentQnA.Answer.Length);

        _normalizedTypingRewardSum = 0;
        _previousLevenshteinDistance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), "");
        _previousFrequencyDistance = TextDistance.CalculateLetterFrequencyDistance("", _currentQnA.Answer.ToLower());
        _previousLengthDistance = _currentQnA.Answer.Length;
    }

    /// <summary>
    /// Reward range: [-3, SPEEDTYPINGREWARDRATIO]
    /// The agent is rewarded for generating text that aligns with the target in length and content variation, using normalized length distance and 
    /// normalized frequency distance. An empty output receives the maximum negative reward to prevent the agent from converging to a local optimum
    /// where submitting nothing avoids penalties for incorrect text. In addition, the final reward incorporates the Levenshtein distance to assess
    /// textual correctness and explicitly incentivizes fast writing. When the text is written correctly and at the maximum words-per-minute rate 
    /// (WPMMAX), the agent receives a reward scaled by **SPEEDTYPINGREWARDRATIO**, encouraging both accuracy and speed in general writing behavior.
    /// </summary>
    /// <returns></returns>
    public override float GetFinalReward()
    {
        float speedReward = Mathf.Clamp01((_typingMetrics.GetGrossWPM(_timeOfEpisode/60f) * _typingMetrics.GetFinalAccuracy()) / WPMMAX);
        speedReward = _normalizedTypingRewardSum > 0 ? speedReward : 0;
        
        return _normalizedTypingRewardSum * speedReward * (1 - SPEEDTYPINGREWARDRATIO);
    }


    protected override float GetReward()
    {
        float typingReward = GetTrueFingerPosition() == '\0' ? -0.5f : base.GetReward();
        float typingRewardNorm = typingReward / GetMaxPossibleReward(_currentQnA.Answer.Length);
        _normalizedTypingRewardSum += typingRewardNorm;

        Debug.Log($"Cumulative Reward: {GetCumulativeReward()}");
        return GetPenaltyWeightedReward(typingRewardNorm) * SPEEDTYPINGREWARDRATIO;
    }


    private float GetPenaltyWeightedReward(float reward)
    {
        if (reward >= 0)
        {
            return reward;
        }

        float levenshteinDistance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower());
        float frequencyDistance = TextDistance.CalculateLetterFrequencyDistance(AnswerText.text.ToLower(), _currentQnA.Answer.ToLower());
        float lengthDistance = Math.Abs(AnswerText.text.Length - (float)_currentQnA.Answer.Length);

        int penaltyCounter = 4;

        UpdatePenaltyCounter(levenshteinDistance, _previousLevenshteinDistance, ref penaltyCounter);
        _previousLevenshteinDistance = levenshteinDistance;

        UpdatePenaltyCounter(frequencyDistance, _previousFrequencyDistance, ref penaltyCounter);
        _previousFrequencyDistance = frequencyDistance;

        UpdatePenaltyCounter(lengthDistance, _previousLengthDistance, ref penaltyCounter);
        _previousLengthDistance = lengthDistance;

        return reward / penaltyCounter;
    }

    private void UpdatePenaltyCounter(float current, float previous, ref int counter)
    {
        if (current >= previous)
        {
            counter--;
        }
    }

    private float GetMaxPossibleReward(int N)
    {
        return ((N + 1) * (N + 2)) / 4f - 0.5f;
    }
}
