using Supervisor;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;


public enum DisplayAlignment
{
    Vertical,
    Horizontal
}


public abstract class VisionAgent : Agent
{
    [field: SerializeField, Tooltip("Defines the reward function of the focus agent. \"r_{number}\" is interpreted as the values returned by " +
        "the \"GetTaskReward\" function of the specific tasks. {number} start with 0 and enumerates the tasks displayed from left to right. All " +
        "function of the Math library can be used (without Math. prefix)."), ProjectAssign]
    public string RewardFunction { get; set; } = "r_0";

    [field: SerializeField, Tooltip("Shows the current focused and encoded objects."), ProjectAssign]
    public bool ShowFocusedObject { get; set; } = false;

    [field: SerializeField, Tooltip("The distance between the task displays in CM."), ProjectAssign]
    public float DistanceBetweenTasksDisplays { get; set; } = 0;

    [field: SerializeField, Tooltip("Must be defined for the training. For all other modes, the size is determined by the provided model.")]
    public int VectorObservationSize { get; set; }

    [field: SerializeField]
    public GameObject[] TaskGameObjects { get; set; }

    [field: SerializeField]
    public GameObject[] TaskGameObjectsProjectSettingsOrdering { get; set; }

    [field: SerializeField, Tooltip("Used to determine the ppi of the screen needed to accurately calculate the distances of the eye movement."), ProjectAssign]
    public List<DisplayConfiguration> DisplayConfigurations { get; set; }

    [field: SerializeField, ProjectAssign]
    public DisplayAlignment DisplayAlignment { get; set; }

    [field: SerializeField, Tooltip("The supervisor agent decides which task should be observable. The actions to look at the POI of the visual state" +
    " space of the inactive task are masked."), ProjectAssign]
    public bool IsSupervisorGuided { get; set; } = false;

    [field: SerializeField]
    public bool MultiScreen { get; set; } = false;

    [field: SerializeField]
    public bool UseVisionAgentPerTask { get; set; } = false;

    public bool IsPaused { get; set; }

    public float NullObservationPenalty { get; set; } = 0;


    protected const float PREPERATIONTIME = 0.135f;
    protected const float EXECUTIONTIMEEMMA = 0.07f;
    protected const float SACCADETIMEEMMA = 0.002f;

    protected List<VisualStateSpace> FocusableObjects { get; set; }

    protected Supervisor.SupervisorAgent _supervisorAgent;

    protected int _numberOfFocusableObjects;

    protected int _currentTaskIndex { get; set; }


    private int _terminationCounter = 0;


    public ITask[] Tasks
    {
        get
        {
            return ITask.GetTasksFromGameObjects(TaskGameObjects);
        }
    }

    public ITask[] TasksProjectSettingsOrdering
    {
        get
        {
            return ITask.GetTasksFromGameObjects(TaskGameObjectsProjectSettingsOrdering);
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!UseVisionAgentPerTask)
        {
            if (CRUtil.IsSingleView(_supervisorAgent))
            {
                WriteDiscreteActionMaskSingleView(actionMask);
            }
            else
            {
                WriteDiscreteActionMaskMultiView(actionMask);
            }
        }
    }

    public override void Initialize()
    {
        _supervisorAgent = SupervisorAgent.GetSupervisor();

        FocusableObjects = CRUtil.GetFocusableGameObjectsOfTasks(Tasks.ToList());

        _numberOfFocusableObjects = CRUtil.GetNumberFocusableObjects(_supervisorAgent, Tasks);
        //Debug.Log("_numberOfFocusableObjects: " + _numberOfFocusableObjects);
    }


    protected abstract void WriteDiscreteActionMaskSingleView(IDiscreteActionMask actionMask);

    protected abstract void WriteDiscreteActionMaskMultiView(IDiscreteActionMask actionMask);

    protected override void OnEnable()
    {
        base.OnEnable();
        _terminationCounter = 0;

        foreach (ITask task in Tasks)
        {
            task.OnTermination += CatchEndEpisode;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        foreach (ITask task in Tasks)
        {
            task.OnTermination -= CatchEndEpisode;
        }
    }

    protected virtual float GetReward()
    {
        Dictionary<string, object> parameters = new();

        for (int i = 0; i < Tasks.Length; i++)
        {
            parameters.Add($"r_{i}", TasksProjectSettingsOrdering[i].TaskRewardForFocusAgent.IsNullOrEmpty() ? 0 : TasksProjectSettingsOrdering[i].TaskRewardForFocusAgent.DequeueAll().Select(x => x.Item1).Sum());
        }

        float reward = (float)FunctionInterpreter.Interpret(RewardFunction, parameters);

        if (reward < 0)
        {
            Debug.LogWarning($"Negative Focus Reward: {reward}");
        }

        return reward;
    }

    protected float VisualDistance(float distance)
    {
        return 180 * (Mathf.Atan(distance / GetDisplayConfigurationForCurrentState().DistanceToUserMeter) / Mathf.PI);
    }

    protected float CalculateEMMAEncodingTime(float eccentricity, float frequency = 0.1f)
    {
        float K = 0.006f;
        float k = 0.4f;

        return K * -Mathf.Log(frequency) * Mathf.Exp(k * eccentricity);
    }

    protected DisplayConfiguration GetDisplayConfigurationForCurrentState()
    {
        return MultiScreen ? DisplayConfigurations[_currentTaskIndex] : DisplayConfigurations[0];
    }

    protected float CalculateDistanceBetweenPositions(int targetTaskIndex, Vector2Int sourcePosition, Vector2Int targetPosition)
    {
        if (!MultiScreen)
        {
            return Vector2Int.Distance(sourcePosition, targetPosition) + DistanceBetweenTasksDisplays * Mathf.Abs(_currentTaskIndex - targetTaskIndex);
        }

        return CRUtil.CalculateDistanceBetweenPositionsCM(_currentTaskIndex, targetTaskIndex, DistanceBetweenTasksDisplays, sourcePosition, targetPosition, DisplayAlignment, DisplayConfigurations);
    }


    private void CatchEndEpisode(ITask sender, int episodeId)
    {
        if (Tasks.Count() == 1)
        {
            CatchEndEpisodeForSingleTask(sender);
        }
        else
        {
            CatchEndEpisodeForMultipleTasks(sender, episodeId);
        }
    }

    private void CatchEndEpisodeForSingleTask(ITask task)
    {
        SetRewardsForTask(task);

        EndEpisode();
    }

    private void CatchEndEpisodeForMultipleTasks(ITask sender, int episodeId)
    {
        if (sender.IsTerminatingTask && episodeId >= _terminationCounter) //only the first task with IsTerminatingTask for a certain episode can enter
        {
            _terminationCounter++;

            foreach (ITask task in Tasks) 
            {
                SetRewardsForTask(task);
            }

            EndEpisode();
        }
    }

    private void SetRewardsForTask(ITask task)
    {
        while (task.TaskRewardForFocusAgent.Count > 0)
        {
            SetReward(task.TaskRewardForFocusAgent.DequeueAll().Select(x => x.Item1).Sum());
        }
    }
}
