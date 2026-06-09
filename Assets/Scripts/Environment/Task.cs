using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.OnScreen.OnScreenStick;


public abstract class Task : Agent, ITask
{        
    [field: SerializeField, Tooltip("Determines if the supervisor should end its episode if the episode of the task ends"), ProjectAssign(Header = "General Task Settings")]
    public bool IsTerminatingTask { get; set; }

    [field: SerializeField, Tooltip("If autonomous is True than there is no supervised control."), ProjectAssign]
    public bool IsAutonomous { get; set; }

    [field: SerializeField, Tooltip("The frequency with which the agent requests a decision. A DecisionPeriod of 5 means that the Agent will request" +
    " a decision every 5 Academy steps."), ProjectAssign]
    public virtual int DecisionPeriod { get; set; } = 4;

    [field: SerializeField, Tooltip("If active the observations of the agents are provided by the vision- instead of the supervisor agent."), ProjectAssign(Header = "Vision Agent Settings")]
    public bool UseVisionAgent { get; set; }

    [field: SerializeField, Tooltip("Specifies the elements of a task that the focus agent can concentrate on.")]
    public VisualStateSpace FocusStateSpace { get; set; }

    public event Action<ITask, int>? OnTermination;


    public virtual bool IsIdle 
    {
        get
        { 
            return _isIdle;
        }
        set
        {
            if (IsIdle && !value)
            {
                ActivationCount += 1;
            }

            InitComponents();
            _isIdle = value;
            SetIdle(value);
        } 
    }

    public virtual List<string> IgnoredTagsForVision
    {
        get
        {
            return new();
        }
    }

    public virtual List<string> TransparentTagsForVision
    {
        get
        {
            return new();
        }
    }

    public virtual bool IsPaused {  get; set; }

    public int Priority { get; set; } = 1;

    public float TimeLastPerformedAction { get; set; }

    public int ActivationCount { get; set; } = 0;

    public Queue<(float, int)> TaskRewardForSupervisorAgent { get; protected set; }

    public Queue<(float, int)> TaskRewardForFocusAgent { get; protected set; }

    public virtual bool IsActive { get; set; }

    public bool IsSuggested { get; set; }

    public bool IsTaskProcessed { get; set; }

    public virtual string StateDescription => "State Info not available...";

    public abstract IStateInformation StateInformation { get; set; }

    public IStateInformation StateInformationOnEndEpisode { get; set; }

    public float RewardOfLastEpisode { get; set; }

    public int PriorityLastEpisode { get; set; }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public abstract void AddTrueObservationsToSensor(VectorSensor sensor);

    public abstract void OnMove(InputValue value);
    
    public abstract void UpdateDifficultyLevel();

    public static event Action<ITask> OnEndEpisode;

    public virtual Dictionary<string, double> AccumulatedPerformance => throw new NotImplementedException();

    public virtual Dictionary<string, double> Performance => throw new NotImplementedException();

    public virtual Dictionary<string, string> PerformanceNotes => throw new NotImplementedException();

    public Dictionary<string, string> EnvironmentNotes { get; set; }

    public virtual void ResetAccumulatedPerformance() => throw new NotImplementedException();


    private bool _isIdle = false;

    private List<MonoBehaviour> _enabledBehaviours;

    private List<Rigidbody> _rigidBodies;

    private List<Rigidbody2D> _rigidBodies2D;


    /// <summary>
    /// Can not be overriden by child class. Use OnActionReceivedInternal instead.
    /// </summary>
    /// <param name="actionBuffers"></param>
    public sealed override void OnActionReceived(ActionBuffers actionBuffers)
    {
        TimeLastPerformedAction = Time.time;
        OnActionReceivedInternal(actionBuffers);
    }

    public new void EndEpisode()
    {
        RewardOfLastEpisode = GetCumulativeReward();
        PriorityLastEpisode = Priority;
        StateInformationOnEndEpisode = StateInformation;

        OnTermination?.Invoke(this, CompletedEpisodes);

        OnEndEpisode?.Invoke(this);
        base.EndEpisode();
        EnvironmentNotes = new();
    }

    public (float, int) PollRewardOfLastEpisode()
    {
        float rewardOfLastEpisode = RewardOfLastEpisode;
        int priorityLastEpisode = PriorityLastEpisode;

        RewardOfLastEpisode = 0;
        PriorityLastEpisode = 0;

        return (rewardOfLastEpisode, priorityLastEpisode);
    }

    /// <summary>
    /// base.Awake must be called in case of overriding
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        TaskRewardForFocusAgent = new();
        TaskRewardForSupervisorAgent = new();
        EnvironmentNotes = new();
        CheckForIllegalOverride();
        //TimeLastPerformedAction = Time.time;
    }

    public abstract void OnActionReceivedInternal(ActionBuffers actionBuffers);

    /// <summary>
    /// Should not be overriden, use OnFixedUpdate instead.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        if (!IsPaused)
        {
            OnFixedUpdate();
        }
    }

    /// <summary>
    /// Allows for defining a custom FixedUpdate logic in the child class.
    /// </summary>
    protected virtual void OnFixedUpdate() { }

    /// <summary>
    /// Should not be overriden, use OnUpdate instead.
    /// </summary>
    protected virtual void Update()
    {
        if (!IsPaused) 
        {
            OnUpdate();
        }
    }

    protected virtual void OnUpdate() { }


    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    private void InitComponents()
    {
        if (_enabledBehaviours == null)
        {
            MonoBehaviour[] behaviours = transform.parent.GetComponentsInChildren<MonoBehaviour>();
            _enabledBehaviours = behaviours.Where(x => x.enabled).ToList();
            _rigidBodies = transform.parent.GetComponentsInChildren<Rigidbody>().ToList();
            _rigidBodies2D = transform.parent.GetComponentsInChildren<Rigidbody2D>().ToList();
        }
    }

    private void SetIdle(bool idle)
    {
        if (_enabledBehaviours != null)
        {
            foreach (var behaviour in _enabledBehaviours)
            {
                behaviour.enabled = !idle;
            }
        }

        if (_rigidBodies2D != null)
        {
            foreach (var rb in _rigidBodies2D)
            {
                rb.simulated = !idle;
            }
        }

        if (_rigidBodies != null)
        {
            foreach (var rb in _rigidBodies)
            {
                rb.isKinematic = idle;
            }
        }
    }

    private void CheckForIllegalOverride()
    {
        MethodInfo fixedUpdateMethod = GetType().GetMethod("FixedUpdate",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (fixedUpdateMethod.DeclaringType != typeof(Task))
        {
            Debug.LogError($"{GetType().Name} should NOT override FixedUpdate! Use OnFixedUpdate instead.");
        }
    }
}