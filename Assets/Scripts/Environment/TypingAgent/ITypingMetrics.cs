using System.Collections.Generic;

public interface ITypingMetrics
{
    string CurrentSentence { set; }
    Dictionary<string, double> Performance { get; }
    int Priority { set; }
    string TargetSentence { set; }

    void EndTyping();
    float GetAccuracy();
    float GetFinalAccuracy();
    float GetGrossWPM(double? durationMinutes = null);
    float GetNetWPM(double? durationMinutes = null);
    float GetTypingDurationExcludingPausedTime();
    float GetTypingDurationIncludingPausedTime();
    void PauseTyping();
    void RecordCorrectKeystroke();
    void RecordIncorrectKeystroke();
    void ResumeTyping();
    void StartTyping();
}