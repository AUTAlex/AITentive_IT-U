using Supervisor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GazeRecorder : MonoBehaviour
{
    [field: SerializeField, Tooltip("Gaze recordings are saved to Scores/Data/{File Name}"), ProjectAssign]
    public string FileNameForGazeRecords { get; set; } = "gazeRecord.csv";

    private BufferedCsvWriter<GazeRecord> _gazeRecording;

    private DateTime _dateTime;

    private int _sampleNumber;

    private double _recordingStartMs;

    private SupervisorAgent _supervisorAgent;

    private string _pathScores;


    public void RecordGaze(Vector2 gaze, List<IStateInformation> stateInformation)
    {
        GazeRecord gazeRecord = new GazeRecord
        {
            Date = _dateTime,
            Timestamp = Time.realtimeSinceStartupAsDouble * 1000.0 - _recordingStartMs,
            SampleNumber = _sampleNumber,
            GazeX = gaze.x,
            GazeY = gaze.y,
            StateInformation = stateInformation.Copy()
        };

        //Debug.Log($"gaze: {gaze}");
        _gazeRecording.Add(gazeRecord);

        _sampleNumber++;
    }


    private void Start()
    {
        _sampleNumber = 0;
        _recordingStartMs = Time.realtimeSinceStartupAsDouble * 1000;
        _supervisorAgent = SupervisorAgent.GetSupervisor();
        InitPaths();
        _gazeRecording = new(filePath: Path.Combine(_pathScores, FileNameForGazeRecords));
        _dateTime = DateTime.Now;
    }

    private void OnDisable()
    {
        _gazeRecording.Dispose();
    }

    private void InitPaths()
    {
        string workingDirectory = Util.GetWorkingDirectory();

        SupervisorSettings supervisorSettings = new SupervisorSettings(
            _supervisorAgent is Supervisor.SupervisorAgentRandom,
            _supervisorAgent.SetConstantDecisionRequestInterval,
            _supervisorAgent.DecisionRequestIntervalInSeconds,
            _supervisorAgent is Supervisor.SupervisorAgentRandom ? ((Supervisor.SupervisorAgentRandom)_supervisorAgent).DecisionRequestIntervalRangeInSeconds : 0,
            _supervisorAgent.DifficultyIncrementInterval,
            _supervisorAgent.DecisionPeriod,
            _supervisorAgent.AdvanceNoticeInSeconds);

        Hyperparameters hyperparameters = new Hyperparameters
        {
            tasks = _supervisorAgent.TaskNames,
        };

        _pathScores = Path.Combine(workingDirectory, "Scores", Util.GetScoreString(supervisorSettings, hyperparameters));
    }
}

public class GazeRecord
{
    public GazeRecord()
    {
    }

    public GazeRecord(DateTime dateTime, double timestamp, int sampleNumber, float gazeX, float gazeY, List<IStateInformation> stateInformation)
    {
        Date = dateTime;
        Timestamp = timestamp;
        SampleNumber = sampleNumber;
        GazeX = gazeX;
        GazeY = gazeY;
        StateInformation = stateInformation;
    }

    public DateTime Date { get; set; }

    public double Timestamp { get; set; }

    public int SampleNumber { get; set; }

    public float GazeX { get; set; }

    public float GazeY { get; set; }

    public string AOI { get; set; }

    public List<IStateInformation> StateInformation { get; set; }
}
