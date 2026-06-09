using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using TMPro;
using Unity.MLAgents.Policies;
using UnityEngine.InputSystem;
using System.Linq;
using System;
using UnityEngine.Rendering.Universal;


namespace Supervisor
{
    public enum Mode
    {
        Force,
        Notification,
        Suggestion
    }


    public class SupervisorAgent : Agent, ISupervisorAgent
    {
        [field: SerializeField, Tooltip("Score is shown in the top right corner if true."), ProjectAssign]
        public bool ShowReward { get; set; }

        [field: SerializeField, Tooltip("Focus active platform in heuristic mode."), ProjectAssign]
        public bool FocusActiveTask { get; set; }

        [field: SerializeField, Tooltip("Hide inactive platform."), ProjectAssign]
        public bool HideInactiveTasks { get; set; }

        [field: SerializeField, Tooltip("In case a task switch happens, the next requested decision is after SetConstantDecisionRequestInterval + " +
            "AdvanceNoticeInSeconds."), ProjectAssign]
        public float AdvanceNoticeInSeconds { get; set; }

        [field: SerializeField, Tooltip("Mode of the supervisor: " +
            "\nForce -> automatic switch, the user cannot decide if the switch should be performed;\n " +
            "\nNotification -> the user will be notified about upcoming switch and can perform the switch during 1 second " +
            "If the switch was not performed by the user, the switch is performed after expiry of this 1 second;\n " +
            "\nSuggestion: the supervisor only suggestion a switch, the decision remains by the user")]
        public Mode Mode { get; set; }

        [field: SerializeField, Tooltip("Defines if the interval in which the agent should perform an action is constant (true) or a minimum value " +
            "(false)."), ProjectAssign]
        public bool SetConstantDecisionRequestInterval { get; set; }

        [field: SerializeField, Tooltip("Defines the interval in which the agent should perform an action."), ProjectAssign]
        public float DecisionRequestIntervalInSeconds { get; set; }

        [field: SerializeField, Tooltip("The Interval in which the _dragDifficulty level should be increased."), ProjectAssign]
        public int DifficultyIncrementInterval { get; set; } = 15;

        [field: SerializeField, Tooltip("The frequency with which the agent requests a decision. A DecisionPeriod of 5 means that the Agent will " +
            "request a decision every 5 Academy steps. The DecisionPeriod is ignored if SetConstantDecisionRequestInterval is true.")]
        public int DecisionPeriod { get; set; } = 5;

        [field: SerializeField, Tooltip("Defines the reward function of the supervisor agent. \"r_{number}\" is interpreted as the values returned by " +
            "the \"TaskRewardForSupervisorAgent\" queue of the specific tasks. {number} start with 0 and enumerates the tasks displayed from left to " +
            "right. All function of the Math library can be used (without Math. prefix). Furthermore, the following variables can be used: " +
            "\n\"t_s:\": time since last switch;\n " +
            "\n\"t_d\": decision request interval in seconds;\n " +
            "\n\"a_{number}:\": 1 if task is active 0 otherwise;\n " +
            "\n\"r_s{number}\": accumulated reward since last switch;\n " +
            "\n\"r_n{number}\": number of rewards since last switch;\n " +
            "\n\"r_e{number}\": task reward of last episode (collected only once, this is not the reward of the supervisor queue);\n " +
            "\n\"p_{number}\": priority of the task;" +
            "\n\"n_s:\": total number of tasks in task schedule.\n "), ProjectAssign]
        public string RewardFunction { get; set; } = "r_0";

        [field: SerializeField, ProjectAssign]
        public float TimeScale { get; set; } = 1;

        [field: SerializeField, ProjectAssign]
        public int StartCountdownAt { get; set; } = 5;

        [field: SerializeField, Tooltip("Must be defined for the training. For all other modes, the size is determined by the provided model.")]
        public int VectorObservationSize { get; set; }

        [field: SerializeField]
        public Text CumulativeRewardText { get; set; }

        [field: SerializeField]
        public Text CurrentRewardText { get; set; }

        [field: SerializeField]
        public Text RewardLastEpisodeText { get; set; }

        [field: SerializeField]
        public VerticalLayoutGroup TaskRewardsCanvas { get; set; }

        [field: SerializeField]
        public TextMeshProUGUI TextMeshProUGUI { get; set; }

        public int EpisodeCount { get; protected set; }

        public float FixedEpisodeDuration { get; protected set; }

        public bool UsesHeuristic { get; protected set; }

        [field: SerializeField]
        public GameObject[] TaskGameObjects { get; set; }

        [field: SerializeField]
        public GameObject[] TaskGameObjectsProjectSettingsOrdering { get; set; }

        [field: SerializeField]
        public VisualTaskQueue VisualTaskQueue { get; set; }

        public float TimeSinceLastSwitch { get; set; }

        public float RewardLastEpisode { get; set; } = 0;

        public bool IsNextAdvanceNotificationIgnored { get; set; }

        public bool WasDecisionRequestedBySystem { get; set; }

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

        public string[] TaskNames
        {
            get
            {
                string[] taskName = new string[Tasks.Length];

                for (int i = 0; i < Tasks.Length; i++)
                {
                    taskName[i] = Tasks[i].GetType().Name;
                }

                return taskName;
            }
        }


        private int _activeInstance;

        protected int _previousActiveInstance;

        protected int _pendingInstance;

        protected bool _isDifficultyUpdatedInCurrentInterval;

        protected float _fixedUpdateTimer = 0.0f;

        protected float _advanceNoticeTimer = 0.0f;

        protected Dictionary<InputAction, bool> _wasReleased;

        protected int _switchCount;

        protected AudioSource _audioSource;

        protected bool _isUserInput;

        protected float _fixedNotificationExecutionTimer = 0.0f;


        [field: SerializeField]
        private GameObject _textPrefab;

        private Camera _mainCamera;

        private SupervisorControls _controls;

        private int _stepCounterDecisionRequester;

        private bool _taskSwitched;

        private bool _notificationActive;

        private float _collectedReward;

        private float _lastCollectedReward;

        private (int, float)[] _rewardsSinceLastSwitch;

        private ITask _previouslySelectedByUser;

        private bool _isForcedTaskSwitchPending;

        private int _suggestedInstance;

        private int _notifiedInstance;

        private List<Text> _taskRewardsText;

        private float[] _taskRewards;

        private int _terminationCounter;


        public static event EventHandler<bool> EndEpisodeEvent;
        public delegate void StartEpisodeAction();
        public static event StartEpisodeAction OnStartEpisode;

        public delegate void TaskSwitchAction(double timeBetweenSwitches, int targetTask, bool isNewEpisode, bool wasRequestedBySystem, Switcher switcher = default);
        public static event TaskSwitchAction OnTaskSwitchCompleted;

        public delegate void TaskSwitchToAction(ITask task);
        public static event TaskSwitchToAction OnTaskSwitchTo;

        public delegate void TaskSwitchFromAction(ITask task);
        public static event TaskSwitchToAction OnTaskSwitchFrom;

        public delegate void SetRewardAction(float reward);
        public static event SetRewardAction OnSetReward;


        public static SupervisorAgent GetSupervisor(GameObject gameObject = null)
        {
            gameObject = gameObject ?? GameObject.Find("Supervisor");
            return gameObject.GetComponents<SupervisorAgent>().Where(x => x.enabled).First();
        }

        public ITask GetActiveTask()
        {
            foreach (ITask task in Tasks)
            {
                if (task.IsActive)
                {
                    return task;
                }
            }

            return null;
        }

        public ITask GetSuggestedTask()
        {
            foreach (ITask task in Tasks)
            {
                if (task.IsSuggested)
                {
                    return task;
                }
            }

            return null;
        }

        public List<T> GetTask<T>()
        {
            List<T> result = new();

            foreach (ITask task in Tasks)
            {
                if (task.GetType() == typeof(T))
                {
                    result.Add((T)task);
                }
            }

            return result;
        }

        public List<ITask> GetActiveTasks()
        {
            List<ITask> result = new();

            foreach (ITask task in Tasks)
            {
                if (task.IsActive)
                {
                    result.Add(task);
                }
            }

            return result;
        }

        public int GetActiveTaskNumber()
        {
            return _activeInstance;
        }

        public int GetPreviousActiveTaskNumber()
        {
            return _previousActiveInstance;
        }

        public int GetTaskNumber(ITask task)
        {
            return Array.IndexOf(Tasks, task);
        }

        public override void OnEpisodeBegin()
        {
            OnStartEpisode?.Invoke();

            EpisodeCount++;

            Debug.Log(string.Format("Start Episode {0}!", EpisodeCount));
            
            FixedEpisodeDuration = 0;
            _isDifficultyUpdatedInCurrentInterval = false;
            _isForcedTaskSwitchPending = false;
            _activeInstance = _notifiedInstance = _suggestedInstance = 0;

            if (Mode == Mode.Notification)
            {
                GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly;
                _isUserInput = false;
            }

            Act(_activeInstance);

            _switchCount = 1;
            TimeSinceLastSwitch = 0;

            ResetRewardSinceLastSwitch();

            RunCountDown();
        }

        public override void Initialize()
        {
            _stepCounterDecisionRequester = 0;
            _mainCamera = Camera.main;
            UsesHeuristic = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType.Equals(Unity.MLAgents.Policies.BehaviorType.HeuristicOnly);
            _wasReleased = new Dictionary<InputAction, bool>();

            for (int i = 0; i < Tasks.Length; i++)
            {
                Tasks[i].IsActive = false;
            }

            Tasks[0].IsActive = true;
            _rewardsSinceLastSwitch = new (int, float)[Tasks.Length];

            InitMode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            foreach (ITask task in Tasks)
            {
                task.AddTrueObservationsToSensor(sensor);
                sensor.AddObservation(task.Priority);
            }

            //Debug.Log($"Priority: {Tasks[0].Priority}, DurationExcludingPausedTime: {((TypingAgent)Tasks[0]).GetTypingDurationExcludingPausedTime()}");

            sensor.AddOneHotObservation(GetActiveTaskNumber(), TaskGameObjects.Length);
            sensor.AddObservation(_switchCount);
            sensor.AddObservation(TimeSinceLastSwitch);
            sensor.AddObservation(GetActiveTask() != null ? Time.time - GetActiveTask().TimeLastPerformedAction : 0);
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            //SumOfDiscreteBranchSizes only restricts the actions to be performed for the training. If the model was trained with more actions,
            //actionBuffers length still can be greater than SumOfDiscreteBranchSizes. The if condition is added to prevent an error in this case,
            //for instance if the task runs autonomously anyways and the input of the supervisor should be ignored.
            if (actionBuffers.DiscreteActions[0] < GetComponent<BehaviorParameters>().BrainParameters.ActionSpec.SumOfDiscreteBranchSizes)
            {
                Act(actionBuffers.DiscreteActions[0]);
                ResolveInteraction(actionBuffers.DiscreteActions[0]);
            }
        }

        public IEnumerator DelayedAgentSwitchTo(float t, int activeInstance)
        {
            ITask task = VisualTaskQueue.enabled && GetActiveTask() != null ? GetActiveTask() : Tasks[activeInstance];
            StartCoroutine(Notification(task));

            yield return StartCoroutine(Delay(t, activeInstance));
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActionsOut = actionsOut.DiscreteActions;

            //If discreteActionsOut[0] wont be set, then discreteActionsOut[0] = 0. Therefore the following line sets discreteActionsOut[0] to the
            //PreviousAction to prevent this default behavior.
            discreteActionsOut[0] = _activeInstance;

            ControlGamepad(discreteActionsOut);
            ControlKeyboard(discreteActionsOut);
            if (VisualTaskQueue.enabled) ControlTaskQueue(discreteActionsOut);
        }

        public void FocusActiveInstance()
        {
            FocusInstance(GetActiveTask());
        }

        public void FocusInstance(ITask selectedTask)
        {
            foreach (ITask task in Tasks)
            {
                Camera camera = task.GetGameObject().transform.parent.GetChildByName("Camera").GetComponent<Camera>();

                if (task == selectedTask)
                {
                    camera.enabled = true;
                    camera.rect = new Rect(0, 0, 1, 1);
                    task.GetGameObject().GetComponentsInChildren<Canvas>().ToList().ForEach(canvas => canvas.enabled = true);
                }
                else
                {
                    camera.enabled = false;
                    task.GetGameObject().GetComponentsInChildren<Canvas>().ToList().ForEach(canvas => canvas.enabled = false);
                }
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            for (int i = 0; i < Tasks.Length; i++)
            {
                actionMask.SetActionEnabled(branch: 0, actionIndex: i, isEnabled: !Tasks[i].IsIdle);
            }

            //At least one action must be enabled
            if (Tasks.All(x => x.IsIdle)) 
            {
                Debug.LogWarning("All tasks are in idle mode!");
                actionMask.SetActionEnabled(branch: 0, actionIndex: 0, isEnabled: true); 
            }
        }


        protected virtual float PeekLastRewards()
        {
            Dictionary<string, object> parameters = new();

            float reward = PeekRewardOfLastEpisode();

            for (int i = 0; i < Tasks.Length; i++)
            {
                (float taskReward, int priority) = TasksProjectSettingsOrdering[i].TaskRewardForSupervisorAgent.IsNullOrEmpty() ? (0, 0) : TasksProjectSettingsOrdering[i].TaskRewardForSupervisorAgent.ElementAt(TasksProjectSettingsOrdering[i].TaskRewardForSupervisorAgent.Count - 1);
                parameters.Add($"a_{i}", TasksProjectSettingsOrdering[i].IsActive ? 1 : 0);
                parameters.Add($"r_{i}", taskReward);
                parameters.Add($"r_n{i}", _rewardsSinceLastSwitch[i].Item1);
                parameters.Add($"r_s{i}", _rewardsSinceLastSwitch[i].Item2);
                parameters.Add($"p_{i}", priority);
                parameters.Add($"r_e{i}", 0);
            }

            return reward + CalculateReward(parameters);
        }

        protected virtual float DequeueReward()
        {
            Dictionary<string, object> parameters = new();

            float reward = DequeueRewardOfLastEpisode();

            for (int i = 0; i < Tasks.Length; i++)
            {
                int numberOfRewards = TasksProjectSettingsOrdering[i].TaskRewardForSupervisorAgent.Count;
                List<(float, int)> rewardPriorityTuple = TasksProjectSettingsOrdering[i].TaskRewardForSupervisorAgent.DequeueAll().ToList();
                int rewardCount = rewardPriorityTuple.Count;
                float taskReward = rewardPriorityTuple.Count > 0 ? rewardPriorityTuple.Sum(x => x.Item1) : 0;
                int priority = rewardPriorityTuple.Count > 0 ? rewardPriorityTuple.Max(x => x.Item2) : 0;

                List<int> list = rewardPriorityTuple.Select(x => x.Item2).ToList();
                if (list.Any(o => o != list[0]))
                {
                    Debug.LogWarning("Different priorities in the reward queue of the task " + i + " detected.");
                }

                _taskRewards[i] = taskReward/rewardCount;
                parameters.Add($"r_{i}", rewardPriorityTuple.Count == 0 ? 0 : taskReward);
                parameters.Add($"a_{i}", TasksProjectSettingsOrdering[i].IsActive ? 1 : 0);
                _rewardsSinceLastSwitch[i] = (_rewardsSinceLastSwitch[i].Item1 + numberOfRewards, _rewardsSinceLastSwitch[i].Item2 + taskReward);
                parameters.Add($"r_n{i}", _rewardsSinceLastSwitch[i].Item1);
                parameters.Add($"r_s{i}", _rewardsSinceLastSwitch[i].Item2);
                parameters.Add($"p_{i}", rewardPriorityTuple.Count == 0 ? 0 : priority);
                parameters.Add($"r_e{i}", 0);
            }

            return reward + CalculateReward(parameters); ;
        }

        private float DequeueRewardOfLastEpisode()
        {
            Dictionary<string, object> parameters = new();

            for (int i = 0; i < Tasks.Length; i++)
            {
                (float rewardLastEpisode, int priority) = Tasks[i].PollRewardOfLastEpisode();
                parameters.Add($"r_{i}", 0);
                parameters.Add($"r_n{i}", 0);
                parameters.Add($"r_s{i}", 0);
                parameters.Add($"a_{i}", 1);
                parameters.Add($"r_e{i}", rewardLastEpisode);
                parameters.Add($"p_{i}", priority);
            }

            return CalculateReward(parameters);
        }

        private float PeekRewardOfLastEpisode()
        {
            Dictionary<string, object> parameters = new();

            for (int i = 0; i < Tasks.Length; i++)
            {
                parameters.Add($"r_{i}", 0);
                parameters.Add($"r_n{i}", 0);
                parameters.Add($"r_s{i}", 0);
                parameters.Add($"a_{i}", 1);
                parameters.Add($"r_e{i}", Tasks[i].RewardOfLastEpisode);
                parameters.Add($"p_{i}", Tasks[i].PriorityLastEpisode);
            }

            return CalculateReward(parameters);
        }

        private float CalculateReward(Dictionary<string, object> parameters)
        {
            parameters.Add("t_s", TimeSinceLastSwitch);
            parameters.Add("t_d", DecisionRequestIntervalInSeconds);
            if (VisualTaskQueue.enabled) { parameters.Add("n_s", VisualTaskQueue.NumberOfTasksInSchedule); }

            float reward = (float)FunctionInterpreter.Interpret(RewardFunction, parameters);

            return float.IsNaN(reward) ? 0 : reward ;
        }

        protected virtual void SwitchAgentTo(int activeInstance, Switcher switcher = Switcher.Supervisor)
        {
            UpdateAgentsActiveStatus(activeInstance, VisualTaskQueue.enabled ? VisualTaskQueue.VisualizeTaskSelection : VisualizeActiveStatusOfTasks);
            ResetRewardSinceLastSwitch();

            if (_switchCount != 0) { PropagateTimeBetweenSwitches(activeInstance, switcher); }
            _switchCount += 1;

            if (FocusActiveTask) { FocusActiveInstance(); }

            if (HideInactiveTasks) { HideInactiveInstance(); }
        }

        protected virtual void RequestInteractionAfterInterval()
        {
            bool hasIntervalExpired = _fixedUpdateTimer > DecisionRequestIntervalInSeconds + AdvanceNoticeInSeconds;

            if (SetConstantDecisionRequestInterval)
            {
                RequestInteractionAfterConstantInterval(hasIntervalExpired);
            }
            else
            {
                RequestInteractionAfterVariableInterval(hasIntervalExpired);
            }
        }

        protected virtual void RequestInteractionAfterConstantInterval(bool hasIntervalExpired)
        {
            if (hasIntervalExpired)
            {
                _advanceNoticeTimer = 0;
                RequestDecision();
                _fixedUpdateTimer = 0;
            }
        }

        protected virtual void RequestInteractionAfterVariableInterval(bool hasIntervalExpired)
        {
            //New decision is requested only after the number of seconds defined in DecisionRequestIntervalInSeconds after a task switch. 
            if (_taskSwitched)
            {
                _fixedUpdateTimer = 0;
            }

            if (hasIntervalExpired && !_taskSwitched && !_notificationActive)
            {
                _advanceNoticeTimer = 0;
                RequestSimpleDecision();
            }

            _taskSwitched = false;
        }

        protected virtual void PropagateTimeBetweenSwitches(int targetTask, Switcher switcher)
        {
            InvokeOnTaskSwitchCompleted(targetTask, switcher);
            TimeSinceLastSwitch = 0;
        }

        protected void InvokeOnTaskSwitchCompleted(int targetTask, Switcher switcher)
        {
            OnTaskSwitchCompleted?.Invoke(TimeSinceLastSwitch, targetTask, false, WasDecisionRequestedBySystem, switcher);
            WasDecisionRequestedBySystem = false;
        }

        protected override void Awake()
        {
            base.Awake();
            _controls = new SupervisorControls();
        }

        protected override void OnEnable()
        {
            foreach (ITask task in Tasks)
            {
                task.OnTermination += EndEpisodesForTerminatingTask;
            }

            _terminationCounter = 0;

            VisualTaskQueue.OnTaskSwitchTo += UpdateActiveTaskBasedOnTaskQueue;
            
            base.OnEnable();
            _controls.Heuristic.Enable();

            _taskRewards = new float[Tasks.Length];
            _taskRewardsText = new();

            //-1 => Managed by ml-agents
            if (TimeScale != -1)
            {
                Time.timeScale = TimeScale;
            }
        }

        protected override void OnDisable()
        {
            foreach (ITask task in Tasks)
            {
                task.OnTermination -= EndEpisodesForTerminatingTask;
            }

            EndEpisodeEvent?.Invoke(this, true);
            VisualTaskQueue.OnTaskSwitchTo -= UpdateActiveTaskBasedOnTaskQueue;
            base.OnDisable();
            _controls.Heuristic.Disable();
        }

        protected void EndEpisodesForTerminatingTask(ITask sender, int episodeId)
        {
            if (sender.IsTerminatingTask && episodeId >= _terminationCounter) //only the first task with IsTerminatingTask for a certain episode can enter
            {
                _terminationCounter++;
                AddReward(DequeueReward());

                Debug.Log("Final reward of Supervisor: " + GetCumulativeReward());

                RewardLastEpisode = GetCumulativeReward();

                foreach (ITask task in Tasks)
                {
                    if (task != sender)
                    {
                        task.EndEpisode();
                    }
                }

                EndEpisodeEvent?.Invoke(this, false);
                EndEpisode();
            }
        }

        protected void Act(int activeInstance)
        {
            switch (Mode)
            {
                case Mode.Force:
                    ForcedAction(activeInstance);
                    break;
                case Mode.Notification:
                    NotificationAction(activeInstance);
                    break;
                case Mode.Suggestion:
                    SuggestionAction(activeInstance);
                    break;
            }
        }

        protected void ResolveInteraction(int activeInstance)
        {
            _lastCollectedReward = PeekLastRewards();
            _collectedReward = DequeueReward();
            
            AddReward(_collectedReward);
            OnSetReward?.Invoke(_collectedReward);
        }

        protected void RequestInteraction()
        {
            if (GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType.Equals(Unity.MLAgents.Policies.BehaviorType.HeuristicOnly))
            {
                RequestDecision();
            }
            else
            {
                RequestInteractionAfterInterval();
            }
        }

        protected void RequestSimpleDecision()
        {
            if (_stepCounterDecisionRequester >= DecisionPeriod)
            {
                _stepCounterDecisionRequester = 0;
                RequestDecision();
            }
            else
            {
                RequestAction();
            }
            _stepCounterDecisionRequester++;
        }

        protected void UpdateDifficultyLevel()
        {
            if ((Convert.ToInt32(FixedEpisodeDuration) % DifficultyIncrementInterval) == 0)
            {
                if (!_isDifficultyUpdatedInCurrentInterval)
                {
                    foreach (ITask task in Tasks)
                    {
                        if (!task.IsIdle)
                        {
                            task.UpdateDifficultyLevel();
                        }
                    }

                    _isDifficultyUpdatedInCurrentInterval = true;
                }
            }
            else _isDifficultyUpdatedInCurrentInterval = false;
        }

        protected void UpdateAgentsActiveStatus(int activeInstance, Action<ITask> visualization)
        {
            Debug.Log($"Switch to task {activeInstance} with priority {Tasks[activeInstance].Priority}");

            if (Tasks[activeInstance].IsIdle)
            {
                Debug.LogWarning($"AMS switched to idle task {activeInstance}!");
                activeInstance = GetNextNonIdleTask();

                if (activeInstance == -1)
                {
                    return;
                }
            }

            OnTaskSwitchTo?.Invoke(Tasks[activeInstance]);
            OnTaskSwitchFrom?.Invoke(Tasks[_activeInstance]);
            _previousActiveInstance = _activeInstance;
            _activeInstance = activeInstance;
            _taskSwitched = true;

            foreach (ITask task in Tasks)
            {
                task.IsActive = false;
                visualization(task);
            }

            Tasks[activeInstance].IsActive = true;
            visualization(Tasks[activeInstance]);
        }

        protected void UpdateAgentsSuggestionStatus(int suggestedInstance, Action<ITask> visualization)
        {
            _previousActiveInstance = _activeInstance;
            _activeInstance = suggestedInstance;
            _taskSwitched = true;

            foreach (ITask task in Tasks)
            {
                task.IsSuggested = false;
                visualization(task);
            }

            Tasks[suggestedInstance].IsSuggested = true;
            visualization(Tasks[suggestedInstance]);
        }

        protected void HideInactiveInstance()
        {
            foreach (ITask task in Tasks)
            {
                foreach (Renderer renderer in task.GetGameObject().transform.parent.GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = task.IsActive;
                }
            }
        }

        protected IEnumerator Notification(ITask task)
        {
            Camera camera = task.GetGameObject().transform.parent.GetChildByName("Camera").GetComponent<Camera>();

            camera.clearFlags = CameraClearFlags.SolidColor;

            _notificationActive = true;

            yield return new WaitForSeconds(AdvanceNoticeInSeconds);

            _notificationActive = false;

            camera.clearFlags = CameraClearFlags.Skybox;
        }

        protected void VisualizeActiveStatusOfTasks(ITask task)
        {
            Camera camera = task.GetGameObject()
                .transform.parent
                .GetChildByName("Camera")
                .GetComponent<Camera>();

            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();

            cameraData.volumeLayerMask = task.IsActive
                ? (1 << 0)
                : (1 << 3);
        }


        private void InitMode()
        {
            if (VisualTaskQueue.enabled)
            {
                InitTaskQueueMode();
                return;
            }

            switch (Mode)
            {
                case Mode.Suggestion:
                    InitSuggestionMode();
                    break;
                default:
                    InitForceMode();
                    break;
            }
        }

        private int GetNextNonIdleTask()
        {
            try
            {
                ITask task = Tasks.First(x => !x.IsIdle);
                return GetTaskNumber(task);
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        private void InitSuggestionMode()
        {
            foreach (ITask task in Tasks)
            {
                task.IsAutonomous = true;
                task.GetGameObject().transform.parent.GetChildByName("Camera").GetChildByName("Frame").gameObject.SetActive(true);
                VisualizeSuggestionStatusOfTasks(task);
            }
        }

        private void InitTaskQueueMode()
        {
            FocusActiveTask = true;
            FocusActiveInstance();
        }

        private void InitForceMode()
        {
            foreach (ITask task in Tasks)
            {
                VisualizeActiveStatusOfTasks(task);
            }
        }

        /// <summary>
        ///_dragDifficulty is updated every 30 seconds. RequestDecision is handled manually here since DecisionRequestIntervalInSeconds is used (agent 
        /// does not use Decision Requester script).
        /// </summary>
        private void FixedUpdate()
        {
            _fixedUpdateTimer += Time.fixedDeltaTime;
            TimeSinceLastSwitch += Time.fixedDeltaTime;
            _fixedNotificationExecutionTimer += Time.fixedDeltaTime;
            FixedEpisodeDuration += Time.fixedDeltaTime;

            UpdateDifficultyLevel();
            RequestInteraction();
            ExecutePendingSwitchNotificationMode(_pendingInstance);
            CheckIdle();
        }

        /// <summary>
        /// Prints the current reward to Canvas
        /// </summary>
        private void Update()
        {
            if (ShowReward)
            {
                CumulativeRewardText.text = string.Format("Cumulative Reward:\t{0}", Math.Round(GetCumulativeReward(), 2).ToString());
                CurrentRewardText.text = string.Format("Current Reward:\t{0}", Math.Round(_lastCollectedReward, 2).ToString());
                RewardLastEpisodeText.text = string.Format("Reward Last Episode:\t{0}", Math.Round(RewardLastEpisode, 2).ToString());

                for (int i = 0; i < Tasks.Length; i++)
                {
                    _taskRewardsText[i].text = string.Format("Task {0} Reward:\t{1}", i, Math.Round(_taskRewards[i], 2).ToString());
                }
            }
        }

        private void CheckIdle()
        {
            if (GetActiveTask() != null && GetActiveTask().IsIdle)
            {
                throw new InvalidOperationException($"Idle task {GetActiveTask()} is active!");
            }
        }

        /// <summary>
        /// In case a visual task queue in combination with the suggestion mode  is used, the task selection logic must be decoupled from action 
        /// space of the supervisor. Otherwise, the user could not select a task from the visual task queue.
        /// </summary>
        private void UpdateActiveTaskBasedOnTaskQueue(ITask task, Switcher switcher)
        {
            if (VisualTaskQueue.enabled && (Mode == Mode.Suggestion || switcher == Switcher.System)) 
            {
                SwitchAgentTo(GetTaskNumber(task), switcher);
            }
        }

        private void ResetRewardSinceLastSwitch()
        {
            for (int i = 0; i < Tasks.Length; i++)
            {
                _rewardsSinceLastSwitch[i] = (0, 0);
            }
        }

        private void VisualizeSuggestionStatusOfTasks(ITask task)
        {
            GameObject imageGameObject = task.GetGameObject().transform.parent.GetChildByName("Camera").GetChildByName("Frame").GetChildByName("Image").gameObject;
            Image image = imageGameObject.GetComponent<Image>();
            BlinkingImageAnimation blinkingImageAnimation = imageGameObject.GetComponent<BlinkingImageAnimation>();

            if (task.IsSuggested)
            {
                image.color = new Color(0, 255, 0, 1);
                blinkingImageAnimation.FadeSpeed = 3;
            }
            else
            {
                image.color = new Color(255, 0, 0, 1);
                blinkingImageAnimation.FadeSpeed = 1;
            }
        }

        private void RunCountDown()
        {
            if (TimeScale == 1)
            {
                if (StartCountdownAt > 0)
                {
                    if (EpisodeCount == 1)
                    {
                        StartCoroutine(Countdown(StartCountdownAt * 4));
                    }
                    else
                    {
                        StartCoroutine(Countdown(StartCountdownAt));
                    }
                }
            }
        }

        private void Start()
        {
            EpisodeCount = 0;
            _isUserInput = false;
            _audioSource = GetComponent<AudioSource>();

            if (!ShowReward)
            {
                CumulativeRewardText.text = "";
                CurrentRewardText.text = "";
                RewardLastEpisodeText.text = "";
            }
            else
            {
                Debug.Log($"Tasks.Length: {Tasks.Length}");

                for (int i = 0; i < Tasks.Length; i++)
                {
                    GameObject textGameObject = Instantiate(_textPrefab, TaskRewardsCanvas.transform);
                    _taskRewardsText.Add(textGameObject.GetComponent<Text>());
                }
            }

            Debug.Log($"Execute environment with time scale {Time.timeScale}.");
        }

        private IEnumerator Countdown(float seconds)
        {
            CountdownTimer countdownTimer = TextMeshProUGUI.GetComponent<CountdownTimer>();
            countdownTimer.StartCountDown(seconds);

            Time.timeScale = 1 / (seconds * 2);

            yield return new WaitUntil(() => countdownTimer.CurrentTime == 0);

            Time.timeScale = TimeScale;
        }

        protected virtual void ForcedAction(int targetInstance)
        {
            if (targetInstance != GetActiveTaskNumber() && !_isForcedTaskSwitchPending)
            {
                PlayAudio();

                if (AdvanceNoticeInSeconds > 0 && !IsNextAdvanceNotificationIgnored)
                {
                    _isForcedTaskSwitchPending = true;
                    _advanceNoticeTimer = AdvanceNoticeInSeconds;
                    StartCoroutine(DelayedAgentSwitchTo(AdvanceNoticeInSeconds, targetInstance));
                }
                else
                {
                    SwitchAgentTo(targetInstance);
                }
            }

            IsNextAdvanceNotificationIgnored = false;
        }

        protected virtual void SuggestionAction(int targetInstance)
        {
            if (targetInstance != _suggestedInstance)
            {
                PlayAudio();

                if (_switchCount != 0)
                {
                    PropagateTimeBetweenSwitches(targetInstance, Switcher.Supervisor);
                }

                UpdateAgentsSuggestionStatus(targetInstance, VisualTaskQueue.enabled ? VisualTaskQueue.VisualizeTaskSuggestion : VisualizeSuggestionStatusOfTasks);
            }

            _suggestedInstance = targetInstance;
        }

        protected virtual void NotificationAction(int targetInstance)
        {
            if (_notifiedInstance != targetInstance)
            {
                if (!_isUserInput)
                {
                    GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
                    ITask task = VisualTaskQueue.enabled ? GetActiveTask() : Tasks[targetInstance];

                    StartCoroutine(Notification(task));

                    PlayAudio();

                    _isUserInput = true;
                    _fixedNotificationExecutionTimer = 0;
                    _pendingInstance = targetInstance;
                }
                else
                {
                    _isUserInput = false;
                    GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.InferenceOnly;
                    _notifiedInstance = targetInstance;
                    SwitchAgentTo(_notifiedInstance);
                }
            }
        }

        private void PlayAudio()
        {
            if (!(GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly))
            {
                if (_audioSource != null)
                {
                    _audioSource.Play();
                }
            }
        }

        private void ExecutePendingSwitchNotificationMode(int pendingInstance)
        {
            if (_isUserInput && _fixedNotificationExecutionTimer > 1 && Mode == Mode.Notification)
            {
                _isUserInput = false;
                GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly;
                SwitchAgentTo(pendingInstance);
                _notifiedInstance = pendingInstance;
            }
        }

        private IEnumerator Delay(float t, int activeInstance)
        {
            yield return new WaitForSeconds(t);

            _isForcedTaskSwitchPending = false;
            SwitchAgentTo(activeInstance);
        }

        private void ControlGamepad(ActionSegment<int> discreteActionsOut)
        {
            PerformInput(_controls.Heuristic.SwitchLeft, discreteActionsOut, false);
            PerformInput(_controls.Heuristic.SwitchRight, discreteActionsOut, true);
        }

        private void ControlKeyboard(ActionSegment<int> discreteActionsOut)
        {
            PerformInput(_controls.Heuristic.Switch, discreteActionsOut);
        }

        private void ControlTaskQueue(ActionSegment<int> discreteActionsOut)
        {
            ITask selection = VisualTaskQueue.SelectedTaskByUser;
            discreteActionsOut[0] = GetTaskNumber(selection);
        }

        private void PerformInput(InputAction inputAction, ActionSegment<int> discreteActionsOut, bool isSwitchedToRight = true)
        {
            if (inputAction.IsPressed() && _wasReleased[inputAction])
            {
                _wasReleased[inputAction] = false;

                if (isSwitchedToRight)
                {
                    discreteActionsOut[0] = _activeInstance < Tasks.Length - 1 ? _activeInstance + 1 : 0;
                }
                else
                {
                    discreteActionsOut[0] = _activeInstance > 0 ? _activeInstance - 1 : Tasks.Length - 1;
                }
            }
            else if (!inputAction.IsPressed())
            {
                _wasReleased[inputAction] = true;
            }
        }
    }
}