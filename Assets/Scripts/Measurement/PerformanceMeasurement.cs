using System;
using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using CsvHelper.Configuration;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;
using Unity.MLAgents.Actuators;
using Supervisor;
using UnityEngine.InputSystem.XR;
using System.Linq;
using CsvHelper.TypeConversion;
using Newtonsoft.Json;
using System.Reflection;
using static UnityEngine.EventSystems.EventTrigger;

public class PerformanceMeasurement : MonoBehaviour
{
    [field: SerializeField, Tooltip("Scores are saved to Scores/Data/{File Name}"), ProjectAssign]
    public string FileNameForScores { get; set; } = "_scores.csv";

    [field: SerializeField, Tooltip("Name of the Player."), ProjectAssign]
    public string PlayerName { get; set; } = "Alexander Lingler";

    [field: SerializeField, Tooltip("Max number of Episodes for performance measurement. If value <= 0 no max number is used."), ProjectAssign]
    public int MaxNumberEpisodes { get; set; } = 0;

    [field: SerializeField, Tooltip("Lower boundary for the measurement of the _scores. Episodes with a lower score are not recorded."), ProjectAssign]
    public int MinimumDurationForMeasurement { get; set; } = 20;

    [field: SerializeField]
    public bool IsTrainingMode { private get; set; }

    [field: SerializeField]
    public bool IsSupervised { private get; set; }

    [field: ProjectAssign(Hide = true)]
    public bool IsAbcSimulation { get; set; }

    [field: ProjectAssign(Hide = true)]
    public int SimulationId { get; set; }

    [field: SerializeField, ProjectAssign]
    public bool MeasurePerformance { get; set; }


    private Supervisor.SupervisorAgent _supervisorAgent;

    private ITask[] _tasks;

    private BallAgent _ballAgent;

    private Unity.MLAgents.Policies.BehaviorParameters _behaviorParameters;

    private string _dateTime;

    private BufferedCsvWriter<Score> _accumulatedScores;

    private BufferedCsvWriter<Score> _taskScores;

    private Dictionary<Tuple<int, int>, BufferedCsvWriter<SwitchingData>> _switchingData;

    private List<ITask> _tasksActionReceivedFrom;

    private int _episodeCount;

    private string _pathScores;

    private string _pathSwitchingData;

    private SwitchingData _switchingDataEntry;

    private int _targetTask;

    private int _sourceTask;

    private bool _wasDecisionRequestedBySystem;

    private int _switchingId;

    private Dictionary<Type, int> _completedEpisodesBeforeUpdate;

    private Dictionary<Type, HashSet<string>> _performanceVariables;

    private bool _savePerformanceOnTaskLevel;


    private void OnEnable()
    {
        if (!IsTrainingMode)
        {
            Debug.Log("Performance will be collected...");
            _savePerformanceOnTaskLevel = _supervisorAgent.VisualTaskQueue.gameObject.activeInHierarchy && _supervisorAgent.VisualTaskQueue.enabled;

            Supervisor.SupervisorAgent.EndEpisodeEvent += AddPerformanceOfSupervisorEpisode;
            Supervisor.SupervisorAgent.OnTaskSwitchCompleted += CreateSwitchingData;
            if (_supervisorAgent.Mode == Mode.Suggestion) { VisualTaskQueue.OnTaskSwitch += CreateSwitchingData; }

            if (_supervisorAgent.Tasks.Length > 1)
            {
                ITask.OnAction += AddSwitchingData;
            }
            else
            {
                ITask.OnAction += AddStateInformationData;
            }

            ITask.OnAction += ValidateSwitchingData;

            if (_savePerformanceOnTaskLevel) { Task.OnEndEpisode += AddPerformanceOfTaskEpisode; }
        }

        if (!MeasurePerformance && !IsAbcSimulation)
        {
            MaxNumberEpisodes = 0;
            MinimumDurationForMeasurement = 0;
            FileNameForScores = "scores.csv";
        }
    }

    private void OnDisable()
    {
        if (!IsTrainingMode)
        {
            Supervisor.SupervisorAgent.EndEpisodeEvent -= AddPerformanceOfSupervisorEpisode;
            Supervisor.SupervisorAgent.OnTaskSwitchCompleted -= CreateSwitchingData;
            if (_supervisorAgent.Mode == Mode.Suggestion) { VisualTaskQueue.OnTaskSwitch -= CreateSwitchingData; }

            if (_supervisorAgent.Tasks.Length > 1)
            {
                ITask.OnAction -= AddSwitchingData;
            }
            else
            {
                ITask.OnAction -= AddStateInformationData;
            }

            ITask.OnAction -= ValidateSwitchingData;

            DisposeData();

            if (_savePerformanceOnTaskLevel) { Task.OnEndEpisode -= AddPerformanceOfTaskEpisode; }
        }
    }

    private void Awake()
    {
        _dateTime = DateTime.Now.ToString();
        _supervisorAgent = SupervisorAgent.GetSupervisor();
        _behaviorParameters = this.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        _tasksActionReceivedFrom = new();
        _completedEpisodesBeforeUpdate = new();
        _performanceVariables = new();

        LogToFile.LogPropertiesFieldsOfObject(this);
    }

    private void Start()
    {
        InitPaths();
        InitData();
        _switchingId = 1;
        _tasks = _supervisorAgent.Tasks;

        _sourceTask = _targetTask = _supervisorAgent.GetActiveTaskNumber();

        string modelName = GetModelName();
        _episodeCount = GetNumberOfNotAbortedEpisodesFromCSV(Path.Combine(_pathScores, FileNameForScores), PlayerName, modelName);

        if (MaxNumberEpisodes > 0)
        {
            if (_episodeCount >= MaxNumberEpisodes)
            {
                Debug.Log("MaxNumberEpisodes already reached, quit application.");
                Core.Exit();
            }
        }
    }

    private void InitData()
    {
        _accumulatedScores = IsAbcSimulation ? new(Path.Combine(Util.GetScoreDataPath(), string.Format("{0}{1}", SimulationId, FileNameForScores)), true, 1) : new(Path.Combine(_pathScores, FileNameForScores), false, 1);
        _taskScores = IsAbcSimulation ? new(Path.Combine(Util.GetScoreDataPath(), string.Format("{0}task{1}", SimulationId, FileNameForScores)), true, 1) : new(Path.Combine(_pathScores, "task" + FileNameForScores), false, 1);
        _switchingData = new Dictionary<Tuple<int, int>, BufferedCsvWriter<SwitchingData>>();
    }

    private void DisposeData()
    {
        _accumulatedScores.Dispose();
        _taskScores.Dispose();
        foreach (var writer in _switchingData.Values)
        {
            writer.Dispose();
        }
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
        _pathSwitchingData = Path.Combine(workingDirectory, "Scores", Util.GetScoreString(supervisorSettings, hyperparameters));
    }

    private Vector2 GetCurrentJoystickAxis()
    {
        Vector2 axis = new Vector2();

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            axis = gamepad.leftStick.ReadValue();
            //stickL.x will be -1.0..1.0 (for full left to full right)
            //stickL.y will be -1.0..1.0 (for full down to full up)
        }

        return axis;
    }

    private void CreateSwitchingData(double timeBetweenSwitches, int targetTask, bool isNewEpisode, bool wasDecisionRequestedBySystem, Switcher switcher)
    {
        int sourceTask = _targetTask;

        if (sourceTask == targetTask)
        {
            return;
        }

        _sourceTask = _targetTask;
        _targetTask = targetTask;
        _wasDecisionRequestedBySystem = wasDecisionRequestedBySystem;

        (IStateInformation stateInformationA, IStateInformation stateInformationB) = GetStateInformationForSwitchingData();

        _switchingDataEntry = new SwitchingData
        {
            DateTime = _dateTime,
            PlayerName = PlayerName,
            Switcher = switcher.ToString(),
            EpisodeId = _supervisorAgent.EpisodeCount,
            SwitchingId = _switchingId,
            SourceTaskId = _sourceTask,
            TargetTaskId = _targetTask,
            SourceTaskPriority = _tasks[_sourceTask].Priority,
            TargetTaskPriority = _tasks[_targetTask].Priority,
            TimeOnPreviousTask = timeBetweenSwitches,
            StateA = stateInformationA.ShallowCopy(),
            StateB = stateInformationB.ShallowCopy(),
            Supervisor = !_supervisorAgent.UsesHeuristic,
            ModelName = GetModelName(),
            SuggestionIsChosen = _supervisorAgent.GetActiveTask() == _supervisorAgent.GetSuggestedTask(),
            WasDecisionRequestedBySystem = wasDecisionRequestedBySystem
        };

        Tuple<int, int> tuple = MeasurementUtil.GetOrderedTaskTupleFileLevel(_sourceTask, _targetTask, _supervisorAgent.Tasks);

        if (!_switchingData.ContainsKey(tuple))
        {
            _switchingData[tuple] = new(Path.Combine(_pathSwitchingData, String.Format("{0}{1}_{2}", "switching", MeasurementUtil.GetTupleName(tuple, _tasks), FileNameForScores)));
        }
        _switchingData[tuple].Add(_switchingDataEntry);

        _switchingId += 1;
    }

    private (IStateInformation, IStateInformation) GetStateInformationForSwitchingData()
    {
        Tuple<int, int> tuple = MeasurementUtil.GetOrderedTaskTupleSwitchLevel(_sourceTask, _targetTask, _supervisorAgent.Tasks);

        if (tuple.Item2 == 99)
        {
            return (_tasks[tuple.Item1].StateInformation, _tasks[tuple.Item1].StateInformation);
        }

        IStateInformation stateInformationA = tuple.Item1 == _sourceTask && _wasDecisionRequestedBySystem ? _tasks[tuple.Item1].StateInformationOnEndEpisode : _tasks[tuple.Item1].StateInformation;
        IStateInformation stateInformationB = tuple.Item2 == _sourceTask && _wasDecisionRequestedBySystem ? _tasks[tuple.Item2].StateInformationOnEndEpisode : _tasks[tuple.Item2].StateInformation;

        return (stateInformationA, stateInformationB);
    }

    private void ValidateSwitchingData(List<dynamic> performedActions, ITask task, double timeSinceLastSwitch = -1)
    {
        if (!_tasksActionReceivedFrom.Contains(task))
        {
            _tasksActionReceivedFrom.Add(task);
        }
    }

    private bool SwitchingDataIsValid()
    {
        foreach (ITask task in _supervisorAgent.Tasks)
        {
            if (!_tasksActionReceivedFrom.Contains(task))
            {
                Debug.LogWarning(string.Format("SwitchingData is not valid. No received actions from Task {0}.", task.GetType().Name));
                return false;
            }
        }

        return true;
    }

    private void AddSwitchingData(List<dynamic> performedActions, ITask task, double timeSinceLastSwitch = -1)
    {
        if (_sourceTask == _targetTask)
        {
            return;
        }

        (IStateInformation stateInformationA, IStateInformation stateInformationB) = GetStateInformationForSwitchingData();

        _switchingDataEntry.EpisodeId = _supervisorAgent.EpisodeCount;
        _switchingDataEntry.JoystickAxisX = GetCurrentJoystickAxis().x;
        _switchingDataEntry.JoystickAxisY = GetCurrentJoystickAxis().y;
        _switchingDataEntry.ReactionTime = _supervisorAgent.TimeSinceLastSwitch;
        _switchingDataEntry.StateA = stateInformationA.ShallowCopy();
        _switchingDataEntry.StateB = stateInformationB.ShallowCopy();

        Tuple<int, int> tuple = MeasurementUtil.GetOrderedTaskTupleFileLevel(_sourceTask, _targetTask, _supervisorAgent.Tasks);

        _switchingData[tuple].Add(new SwitchingData(_switchingDataEntry));
    }

    private void AddStateInformationData(List<dynamic> performedActions, ITask task, double timeSinceLastSwitch = -1)
    {
        if (_switchingDataEntry == null)
        {
            _switchingDataEntry = new SwitchingData
            {
                DateTime = _dateTime,
                PlayerName = PlayerName,
                Switcher = Switcher.System.ToString(),
                EpisodeId = _supervisorAgent.EpisodeCount,
                SwitchingId = _switchingId,
                SourceTaskId = _sourceTask,
                SourceTaskPriority = _tasks[_sourceTask].Priority,
                TimeOnPreviousTask = -1,
                StateA = _tasks[0].StateInformation.ShallowCopy(),
                Supervisor = !_supervisorAgent.UsesHeuristic,
                ModelName = null,
                SuggestionIsChosen = _supervisorAgent.GetActiveTask() == _supervisorAgent.GetSuggestedTask()
            };
        }

        _switchingDataEntry.EpisodeId = _supervisorAgent.EpisodeCount;
        _switchingDataEntry.JoystickAxisX = GetCurrentJoystickAxis().x;
        _switchingDataEntry.JoystickAxisY = GetCurrentJoystickAxis().y;
        _switchingDataEntry.ReactionTime = _supervisorAgent.TimeSinceLastSwitch;
        _switchingDataEntry.StateA = _tasks[0].StateInformation.ShallowCopy();

        if (!_switchingData.ContainsKey(new(0, 0)))
        {
            _switchingData[new(0, 0)] = new(Path.Combine(_pathSwitchingData, String.Format("{0}{1}_{2}", "switching", MeasurementUtil.GetTupleName(new(0, 0), _tasks), FileNameForScores)));
        }
        _switchingData[new(0, 0)].Add(new SwitchingData(_switchingDataEntry));
    }

    /// <summary>
    /// Adds reached accumulated performance (see Score class) to _scores.
    /// </summary>
    /// <param name="aborted"></param>
    private void AddPerformanceOfSupervisorEpisode(object sender, bool aborted)
    {
        Score score = new Score
        {
            DateTime = _dateTime,
            PlayerName = PlayerName,
            EpisodeId = _supervisorAgent.EpisodeCount,
            Duration = _supervisorAgent.FixedEpisodeDuration,
            SupervisorReward = _supervisorAgent.RewardLastEpisode,
            Supervisor = !_supervisorAgent.UsesHeuristic,
            ModelName = GetModelName(),
            FocusActivePlatform = _supervisorAgent.FocusActiveTask,
            Aborted = aborted
        };

        score.TaskPerformance = GetAccumulatedTaskPerformance();

        _accumulatedScores.Add(score);
        CheckEpisodeCount(aborted);
    }

    /// <summary>
    /// Adds reached performance on task level (see Score class) to _scores.
    /// </summary>
    private void AddPerformanceOfTaskEpisode(ITask task)
    {
        try
        {
            Score score = new Score
            {
                DateTime = _dateTime,
                PlayerName = PlayerName,
                EpisodeId = _supervisorAgent.EpisodeCount,
                Duration = _supervisorAgent.FixedEpisodeDuration,
                SupervisorReward = _supervisorAgent.GetCumulativeReward(),
                Supervisor = !_supervisorAgent.UsesHeuristic,
                ModelName = GetModelName(),
                FocusActivePlatform = _supervisorAgent.FocusActiveTask,
                TaskPerformance = GetTaskPerformance(task),
                TaskPerformanceNotes = task.PerformanceNotes,
                EnvironmentNotes = task.EnvironmentNotes
            };

            _taskScores.Add(score);
        }
        catch (NotImplementedException)
        {
            Debug.LogWarning($"Cannot not save the performance on episode level of task {task.GetType()} since it is not implemented.");
        }
    }

    private Dictionary<string, double> GetTaskPerformance(ITask task)
    {
        Dictionary<string, double> summedTaskPerformance = new();

        AddTaskPerformance(task, summedTaskPerformance, task.Performance);

        return summedTaskPerformance;
    }

    private void CheckEpisodeCount(bool aborted)
    {
        if (_supervisorAgent.FixedEpisodeDuration > MinimumDurationForMeasurement)
        {
            _episodeCount++;

            if (MaxNumberEpisodes > 0 && !aborted)
            {
                Debug.Log(string.Format("Performance Measurement active: {0}/{1} episodes completed!", _episodeCount, MaxNumberEpisodes));


                if (_episodeCount == MaxNumberEpisodes)
                {
                    Debug.Log("MaxNumberEpisodes reached, quit application.");
                    Core.Exit();
                }
            }
        }
    }

    private Dictionary<string, double> GetAccumulatedTaskPerformance()
    {
        Dictionary<string, double> accumulatedTaskPerformance = new();

        List<Type> types = _supervisorAgent.Tasks
                    .Select(obj => obj.GetType())
                    .Distinct()
                    .ToList();

        foreach (Type type in types)
        {
            List<ITask> tasksOfType = _supervisorAgent.Tasks
                                        .Where(obj => obj.GetType() == type)
                                        .ToList();

            try
            {
                Dictionary<string, double> summedTaskPerformance = SumColumnsForTaskType(tasksOfType);
                accumulatedTaskPerformance.AddDistinctiveRange(AccumulateColumnsForTaskType(summedTaskPerformance, type));
            }
            catch (NotImplementedException)
            {
                Debug.LogWarning($"Cannot not save task specific performance of {type.Name} on supervisor level.");
            }
        }

        return accumulatedTaskPerformance;
    }

    private Dictionary<string, double> SumColumnsForTaskType(List<ITask> tasksOfType)
    {
        Dictionary<string, double> summedTaskPerformance = new();

        for (int i = 0; i < tasksOfType.Count; i++)
        {
            AddTaskPerformance(tasksOfType[i], summedTaskPerformance, tasksOfType[i].AccumulatedPerformance);
            tasksOfType[i].ResetAccumulatedPerformance();
        }

        return summedTaskPerformance;
    }

    private void AddTaskPerformance(ITask task, Dictionary<string, double> summedTaskPerformance, Dictionary<string, double> performance)
    {
        if (!_performanceVariables.ContainsKey(task.GetType())) { _performanceVariables[task.GetType()] = new(); }

        foreach (KeyValuePair<string, double> entry in performance)
        {
            if (summedTaskPerformance.ContainsKey(entry.Key))
            {
                summedTaskPerformance[entry.Key] += entry.Value;
            }
            else
            {
                summedTaskPerformance[entry.Key] = entry.Value;
            }

            _performanceVariables[task.GetType()].Add(entry.Key);
        }
    }

    private Dictionary<string, double> AccumulateColumnsForTaskType(Dictionary<string, double> summedTaskPerformance, Type type)
    {
        Dictionary<string, double> accumulatedTaskPerformance = new();

        foreach (string variableName in _performanceVariables[type])
        {
            accumulatedTaskPerformance[variableName] = summedTaskPerformance[variableName] / GetDivisorPerType(type);
        }

        return accumulatedTaskPerformance;
    }

    private int GetDivisorPerType(Type type)
    {
        int numberOfCompletedTasks;

        if (!_completedEpisodesBeforeUpdate.ContainsKey(type)) { _completedEpisodesBeforeUpdate[type] = 0; }

        if (_supervisorAgent.VisualTaskQueue.isActiveAndEnabled)
        {
            numberOfCompletedTasks = _supervisorAgent.Tasks.Where(x => x.GetType() == type).Select(x => ((Task)x).ActivationCount).Sum();
            int completedEpisodesBeforeUpdate = _completedEpisodesBeforeUpdate[type];
            _completedEpisodesBeforeUpdate[type] = numberOfCompletedTasks;

            return numberOfCompletedTasks - completedEpisodesBeforeUpdate;
        }
        else
        {
            return _supervisorAgent.Tasks.Where(x => x.GetType() == type).Count();
        }
    }

    private string GetModelName()
    {
        if (!IsSupervised)
        {
            return "NoSupervisor";
        }
        else if (_supervisorAgent.GetType() == typeof(Supervisor.SupervisorAgentRandom))
        {
            return "RandomSupervisor";
        }
        else
        {
            return _behaviorParameters.Model != null ? _behaviorParameters.Model.name : null;
        }
    }

    private int GetNumberOfNotAbortedEpisodesFromCSV(string path, string playerName, string modelName)
    {
        int episodeCount = 0;

        if (File.Exists(path))
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<Score>();

                foreach (Score score in records)
                {
                    if ((!score.Aborted && score.ModelName == modelName && score.PlayerName == playerName && score.Duration > MinimumDurationForMeasurement))
                    {
                        episodeCount++;
                    }
                }
            }
        }

        Debug.Log(string.Format("Performance Measurement active: {0}/{1} episodes completed for model {2} and player {3}!", episodeCount, MaxNumberEpisodes, modelName, playerName));

        return episodeCount;
    }
}


public class Score
{
    public string DateTime { get; set; }
    public string PlayerName { get; set; }
    public int EpisodeId { get; set; }
    public float Duration { get; set; }
    public float SupervisorReward { get; set; }
    public bool Supervisor { get; set; }
    public string ModelName { get; set; }
    public bool FocusActivePlatform { get; set; }
    public bool Aborted { get; set; }
    public Dictionary<string, double> TaskPerformance { get; set; }
    public Dictionary<string, string> TaskPerformanceNotes { get; set; }
    public Dictionary<string, string> EnvironmentNotes { get; set; }
}


public class SwitchingData
{
    public SwitchingData() { }

    public SwitchingData(SwitchingData switchingData)
    {
        DateTime = switchingData.DateTime;
        PlayerName = switchingData.PlayerName;
        EpisodeId = switchingData.EpisodeId;
        SwitchingId = switchingData.SwitchingId;
        SourceTaskId = switchingData.SourceTaskId;
        TargetTaskId = switchingData.TargetTaskId;
        SourceTaskPriority = switchingData.SourceTaskPriority;
        TargetTaskPriority = switchingData.TargetTaskPriority;
        ReactionTime = switchingData.ReactionTime;
        TimeOnPreviousTask = switchingData.TimeOnPreviousTask;
        JoystickAxisX = switchingData.JoystickAxisX;
        JoystickAxisY = switchingData.JoystickAxisY;
        StateA = switchingData.StateA;
        StateB = switchingData.StateB;
        Supervisor = switchingData.Supervisor;
        ModelName = switchingData.ModelName;
        SuggestionIsChosen = switchingData.SuggestionIsChosen;
        Switcher = switchingData.Switcher;
        WasDecisionRequestedBySystem = switchingData.WasDecisionRequestedBySystem;
    }

    public string DateTime { get; set; }
    public string PlayerName { get; set; }
    public string Switcher { get; set; }
    public int EpisodeId { get; set; }
    public int SwitchingId { get; set; }
    public int SourceTaskId { get; set; }
    public int TargetTaskId { get; set; }
    public int SourceTaskPriority { get; set; }
    public int TargetTaskPriority { get; set; }
    public double ReactionTime { get; set; }
    public double TimeOnPreviousTask { get; set; }
    public float JoystickAxisX { get; set; }
    public float JoystickAxisY { get; set; }
    public bool Supervisor { get; set; }
    public string ModelName { get; set; }
    public bool SuggestionIsChosen { get; set; }
    public bool WasDecisionRequestedBySystem { get; set; }
    public IStateInformation StateA { get; set; }
    public IStateInformation StateB { get; set; }
}
