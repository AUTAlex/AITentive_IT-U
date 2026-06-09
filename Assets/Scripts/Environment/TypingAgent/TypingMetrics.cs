using System.Collections.Generic;
using Algorithms;
using UnityEngine;

public class TypingMetrics : ITypingMetrics
{
    public string TargetSentence { private get; set; } = string.Empty;
    public string CurrentSentence { private get; set; } = string.Empty;

    public int Priority { private get; set; } = 0;

    private float _typingStartTime = 0f;
    private int _totalKeystrokes = 0;
    private int _correctKeystrokes = 0;
    private int _incorrectKeystrokes = 0;
    private float _typingEndTime = 0f;
    private float _typingPausedTime = 0f;
    private float _totalPausedTime = 0f;


    // Call this when a typing session starts
    public void StartTyping()
    {
        _typingStartTime = Time.time;
    }

    // Call this when a typing session ends
    public void EndTyping()
    {
        _typingEndTime = Time.time;
    }

    public void PauseTyping()
    {
        _typingPausedTime = Time.time;
    }

    public void ResumeTyping()
    {
        _totalPausedTime += _typingPausedTime != 0 ? Time.time - _typingPausedTime : 0;
        _typingPausedTime = 0;
    }

    // Record a correct keystroke
    public void RecordCorrectKeystroke()
    {
        _totalKeystrokes++;
        _correctKeystrokes++;
    }

    // Record an incorrect keystroke
    public void RecordIncorrectKeystroke()
    {
        _totalKeystrokes++;
        _incorrectKeystrokes++;
    }

    public float GetAccuracy()
    {
        return _totalKeystrokes > 0 ? (float)_correctKeystrokes / (float)_totalKeystrokes * 100 : 0;
    }

    public float GetGrossWPM(double? durationMinutes = null)
    {
        double typingDurationMinutes = durationMinutes == null ? (Time.time - _typingStartTime) / 60.0 : durationMinutes.GetValueOrDefault();
        double grossWPM = typingDurationMinutes > 0 ? (double)_totalKeystrokes / 5 / typingDurationMinutes : 0;

        return (float)grossWPM;
    }

    public float GetNetWPM(double? durationMinutes = null)
    {
        double typingDurationMinutes = durationMinutes == null ? (Time.time - _typingStartTime) / 60.0 : durationMinutes.GetValueOrDefault();
        int levenshteinDistance = TextDistance.CalculateLevenshtein(TargetSentence.ToLower(), CurrentSentence.ToLower());
        double grossWPM = typingDurationMinutes > 0 ? (double)_totalKeystrokes / 5 / typingDurationMinutes : 0;
        double netWPM = typingDurationMinutes > 0 ? grossWPM - (double)levenshteinDistance / typingDurationMinutes : 0;

        return (float)netWPM;
    }

    public float GetTypingDurationExcludingPausedTime()
    {
        return Time.time - _typingStartTime - _totalPausedTime;
    }

    public float GetTypingDurationIncludingPausedTime()
    {
        return Time.time - _typingStartTime;
    }

    public float GetFinalAccuracy()
    {
        return (float)(TargetSentence.Length > 0 ? TextDistance.CalculateAccuracy(TargetSentence.ToLower(), CurrentSentence.ToLower()) : 0);
    }

    // Dictionary containing all calculated performance metrics
    public Dictionary<string, double> Performance
    {
        get
        {
            double typingDurationMinutes = (_typingEndTime - _typingStartTime) / 60.0;
            int levenshteinDistance = TextDistance.CalculateLevenshtein(TargetSentence.ToLower(), CurrentSentence.ToLower());
            double grossWPM = typingDurationMinutes > 0 ? _totalKeystrokes / 5f / typingDurationMinutes : 0;
            double netWPM = typingDurationMinutes > 0 ? grossWPM - (double)levenshteinDistance / typingDurationMinutes : 0;
            double accuracy = _totalKeystrokes > 0 ? (double)_correctKeystrokes / _totalKeystrokes : 0;
            double priorityWeightedNetWPM = netWPM * Priority;
            double priorityWeightedGrossWPM = grossWPM * Priority;
            double priorityAccuracyWeightedGrossWPM = grossWPM * Priority * accuracy;
            double typingDuration = _typingEndTime - _typingStartTime;

            return new Dictionary<string, double>
            {
                { "GrossWordsPerMinute",  grossWPM},
                { "NetWordsPerMinute",  netWPM},
                { "Accuracy",  accuracy },
                { "FinalAccuracy", TargetSentence.Length > 0 ? TextDistance.CalculateAccuracy(TargetSentence.ToLower(), CurrentSentence.ToLower()) : 0 },
                { "KeystrokeErrorRate", _totalKeystrokes > 0 ? (double)_incorrectKeystrokes / _totalKeystrokes : 0 },
                { "TypingDuration", typingDuration },
                { "TypingStartTime", _typingStartTime },
                { "TypingEndTime", _typingEndTime},
                { "LevenshteinDistance", levenshteinDistance },
                { "PriorityWeightedNetWPM", priorityWeightedNetWPM},
                { "PriorityWeightedGrossWPM", priorityWeightedGrossWPM},
                { "PriorityAccuracyWeightedGrossWPM", priorityAccuracyWeightedGrossWPM},
                { "Priority", Priority},
                { "CorrectKeystrokes", _correctKeystrokes},
                { "IncorrectKeystrokes", _incorrectKeystrokes},
                { "TotalKeystrokes", _totalKeystrokes},
                { "ScheduleReward", 2 * (TargetSentence.Length - TextDistance.CalculateLevenshtein(TargetSentence.ToLower(), CurrentSentence.ToLower())) - typingDuration}
            };
        }
    }
}
