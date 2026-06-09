using Supervisor;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;


public enum Switcher
{
    None,
    User,
    Supervisor,
    System
}


public class VisualTaskQueue : MonoBehaviour
{
    [field: SerializeField, ProjectAssign]
    public TaskQueueScenario TaskQueueScenario { get; set; }

    [field: SerializeField, ProjectAssign]
    public bool AdaptivePriorityColor { get; set; }

    [field: SerializeField, ProjectAssign(Header = "Random Scenario Settings")]
    public bool UseRandomScenario { get; set; }

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public string TaskIconPathName { get; set; } = "profil_pictures";

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public int NumberOfTasks { get; set; } = 10;

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public int MaxSecondsToNextTask { get; set; } = 10;

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public int MinSecondsToNextTask { get; set; } = 1;

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public int MaxTaskPriority { get; set; } = 5;

    [field: SerializeField, ProjectAssign, Hidden(FieldName = "UseRandomScenario")]
    public int MinTaskPriority { get; set; } = 1;

    public ITask SelectedTaskByUser { get; set; }

    public float TimeSinceLastSwitch { get; set; }

    public int NumberOfTasksInSchedule => TaskQueueScenario.TaskSchedule.Count;

    public delegate void TaskSwitchTo(ITask task, Switcher switcher);
    public static event TaskSwitchTo OnTaskSwitchTo;

    public delegate void TaskSwitch(double timeBetweenSwitches, int targetTask, bool isNewEpisode, bool wasDecisionRequestedBySystem, Switcher switcher = default);
    public static event TaskSwitch OnTaskSwitch;


    [field: SerializeField]
    protected GameObject _taskQueueSymbolPrefab;

    protected SupervisorAgent _supervisorAgent;

    protected TaskQueueScenario _taskQueueScenario;

    protected Dictionary<ITask, GameObject> _taskIconGameObjectMap;

    protected List<ITask> _activeTasks;

    protected int _taskScheduleIndex = 0;

    protected ITask _suggestedTask;

    protected ITask _selectedTask;


    [field: SerializeField]
    private GameObject _supervisorGameObject;

    private Queue<ITask> _idleTaskQueue;

    private Dictionary<ITask, Sprite> _taskIconMap;

    private float _taskSpawnTimer = 0;

    private float _secondsToNextTaskBuffer;


    public void SwitchToTask(GameObject gameObject, Switcher switcher)
    {
        ITask task = _taskIconGameObjectMap.FirstOrDefault(x => x.Value == gameObject).Key;
        SwitchToTask(task, switcher);
    }

    public void SwitchToTask(ITask task, Switcher switcher)
    {
        if (task == null) //task can be null if a static visual task queue is used and the user selects a symbol with an unassigned task 
        {
            return;
        }

        SelectedTaskByUser = task;
        OnTaskSwitchTo.Invoke(task, switcher);
        OnTaskSwitch?.Invoke(TimeSinceLastSwitch, _supervisorAgent.GetTaskNumber(task), false, true, switcher);
        CheckIdle(task);

        TimeSinceLastSwitch = 0;
    }

    public void VisualizeTaskSuggestion(ITask selectedTask)
    {
        _suggestedTask = selectedTask;
        ChangeColorOfSelectedTask();
    }

    public void VisualizeTaskSelection(ITask selectedTask)
    {
        _selectedTask = selectedTask;
        ChangeColorOfSelectedTask();
    }

    public virtual void Restart()
    {
        if (_taskIconGameObjectMap.IsNullOrEmpty())
        {
            UpdateEnvironmentParameters();

            if (UseRandomScenario) 
            { 
                GenerateRandomScenario();
            }
            else
            {
                _taskQueueScenario = Instantiate(TaskQueueScenario);
            }

            _taskScheduleIndex = 0;
            TimeSinceLastSwitch = 0;
            InitIdleTaskQueue();
            _activeTasks = new();
            InitTaskIconMap();
            ResetTerminating();
            SwitchToTask(_supervisorAgent.Tasks[0], Switcher.System);
        }
    }


    protected List<Sprite> LoadAllImages(string path)
    {
        Object[] loadedResources = Resources.LoadAll(path, typeof(Sprite));

        List<Sprite> imagesList = new List<Sprite>();

        foreach (Object resource in loadedResources)
        {
            if (resource is Sprite texture)
            {
                imagesList.Add(texture);
            }
        }

        return imagesList;
    }

    protected virtual void AddTaskToVisualQueue(ITask task, Sprite sprite)
    {
        GameObject taskQueueSymbol = Instantiate(_taskQueueSymbolPrefab, gameObject.transform);
        taskQueueSymbol.transform.SetAsFirstSibling();
        taskQueueSymbol.transform.GetChildInHierarchyByName("Image").GetComponent<Image>().sprite = sprite;
        taskQueueSymbol.transform.GetChildInHierarchyByName("PriorityText").GetComponent<TextMeshProUGUI>().text = _taskQueueScenario.TaskSchedule[_taskScheduleIndex].Priority.ToString();

        _taskIconGameObjectMap.Add(task, taskQueueSymbol);
        _activeTasks.Add(task);
        task.IsIdle = false;
        task.Priority = _taskQueueScenario.TaskSchedule[_taskScheduleIndex].Priority;

        if (AdaptivePriorityColor)
        {
            SetColorOfPriorityCircle(task);
        }

        _taskScheduleIndex++;
    }

    protected virtual void RemoveTaskFromVisualQueue(ITask task)
    {
        if (_taskIconGameObjectMap.ContainsKey(task))
        {
            GameObject iconGameObject = _taskIconGameObjectMap[task];
            _taskIconGameObjectMap.Remove(task);
            Destroy(iconGameObject);
        }

        task.IsActive = false;
        task.IsIdle = true;
        task.Priority = 0;
    }

    protected virtual void ChangeColorOfSelectedTask()
    {
        foreach (KeyValuePair<ITask, GameObject> entry in _taskIconGameObjectMap)
        {
            ChangeColorOfTask(entry.Key, entry.Value);
        }
    }

    protected void ChangeColorOfTask(ITask task, GameObject gameObject)
    {
        Image image = gameObject.transform.GetChildInHierarchyByName("SelectedImage").GetComponent<Image>();

        if (task != null && task == _selectedTask && task == _suggestedTask)
        {
            image.color = new Color(0, 115, 0, 0.2f); //green
        }
        else if (task != null && task == _selectedTask && task != _suggestedTask)
        {
            image.color = new Color(115, 115, 0, 0.2f); //yellow
        }
        else if (task != null && task != _selectedTask && task == _suggestedTask)
        {
            image.color = new Color(115, 0, 0, 0.2f); //red
        }
        else
        {
            image.color = new Color(0, 0, 0, 0.2f);
        }
    }

    protected void SetColorOfPriorityCircle(ITask task)
    {
        GameObject taskQueueSymbol = _taskIconGameObjectMap[task];
        Image image = taskQueueSymbol.transform.GetChildInHierarchyByName("Circle").GetComponent<Image>();

        int minPriority = _taskQueueScenario.TaskSchedule.Min(x => x.Priority);
        int maxPriority = _taskQueueScenario.TaskSchedule.Max(x => x.Priority);

        image.color = Color.Lerp(Color.green, Color.red, (float)(task.Priority - minPriority) / (maxPriority - minPriority));
    }

    protected virtual void UpdateStateInformation()
    {
        if (!_taskIconGameObjectMap.IsNullOrEmpty())
        {
            foreach (KeyValuePair<ITask, GameObject> entry in _taskIconGameObjectMap)
            {
                entry.Value.transform.GetChildInHierarchyByName("TaskState").GetComponent<TextMeshProUGUI>().text = entry.Key.StateDescription;
            }
        }
    }


    private void OnEnable()
    {
        Task.OnEndEpisode += AddTaskToIdleQueue;
        Task.OnEndEpisode += ActivateFirstAddedTask;
        SupervisorAgent.OnStartEpisode += Restart;
        _supervisorAgent = SupervisorAgent.GetSupervisor();
    }

    private void CheckIdle(ITask task)
    {
        if (task != null && task.IsIdle)
        {
            throw new System.InvalidOperationException($"Idle task {task} is active!");
        }
    }

    private void OnDisable()
    {
        Task.OnEndEpisode -= AddTaskToIdleQueue;
        Task.OnEndEpisode -= ActivateFirstAddedTask;
        SupervisorAgent.OnStartEpisode -= Restart;
    }

    private void SetEnvironmentalNotes(ITask task)
    {
        string taskQueueScenario = UseRandomScenario ? "RandomScenario" : TaskQueueScenario.name;
        task.EnvironmentNotes["TaskQueueScenario"] = taskQueueScenario;
    }

    private void InitIdleTaskQueue()
    {
        _supervisorAgent.Tasks.ToList().ForEach(task => task.IsIdle = true);
        _idleTaskQueue = new Queue<ITask>(_supervisorAgent.Tasks);
    }

    private void InitTaskIconMap()
    {
        List<Sprite> sprites = LoadAllImages(_taskQueueScenario.TaskIconPathName);
        _taskIconMap = new();
        _taskIconGameObjectMap = new();

        for (int i = 0; i < _idleTaskQueue.Count; i++)
        {
            _taskIconMap.Add(_idleTaskQueue.ElementAt(i), sprites[i]);
        }

        ITask initSelection = _idleTaskQueue.Dequeue();
        AddTaskToVisualQueue(initSelection, sprites[0]);
        SelectedTaskByUser = initSelection;
        SetEnvironmentalNotes(initSelection);
    }

    private void FixedUpdate()
    {
        SetTerminatingTask();

        TimeSinceLastSwitch += Time.fixedDeltaTime;

        _taskSpawnTimer += Time.fixedDeltaTime;
        UpdateStateInformation();

        if(_taskQueueScenario != null && _taskScheduleIndex < _taskQueueScenario.TaskSchedule.Count)
        {
            if (IsTaskVisualQueueFull())
            {
                HoldSpawnCountdown();
            }

            if (_taskSpawnTimer >= _taskQueueScenario.TaskSchedule[_taskScheduleIndex].SecondsToNextTask &&
                !_idleTaskQueue.IsNullOrEmpty())
            {
                SpawnTask();
            }
        }
    }

    private bool IsTaskVisualQueueFull()
    {
        return !_taskIconGameObjectMap.IsNullOrEmpty() && (_taskIconGameObjectMap.Count >= 6 || (_taskQueueScenario.TaskSchedule[_taskScheduleIndex].MaxNumberActiveTasks != 0 && _taskIconGameObjectMap.Count > _taskQueueScenario.TaskSchedule[_taskScheduleIndex].MaxNumberActiveTasks));
    }

    private void HoldSpawnCountdown()
    {
        if (_secondsToNextTaskBuffer == 0)
        {
            _secondsToNextTaskBuffer = _taskQueueScenario.TaskSchedule[_taskScheduleIndex].SecondsToNextTask;
        }

        _taskQueueScenario.TaskSchedule[_taskScheduleIndex].SecondsToNextTask = _taskSpawnTimer + _secondsToNextTaskBuffer;
    }

    private void SpawnTask()
    {
        ITask task = _idleTaskQueue.Dequeue();
        Sprite sprite = _taskIconMap[task];

        SetEnvironmentalNotes(task);

        AddTaskToVisualQueue(task, sprite);

        _taskSpawnTimer = 0;
        _secondsToNextTaskBuffer = 0;

        if (_activeTasks.Count == 1)
        {
            Debug.Log($"Empty: switch to task {_supervisorAgent.GetTaskNumber(task)}");
            SwitchToTask(task, Switcher.System);
        }
    }

    private void ResetTerminating()
    {
        foreach(Task task in _supervisorAgent.Tasks)
        {
            task.IsTerminatingTask = false;
        }
    }

    private void AddTaskToIdleQueue(ITask task)
    {
        //prevents that there is no other task
        if (_taskScheduleIndex < _taskQueueScenario.TaskSchedule.Count && _taskIconGameObjectMap.Count == 1)
        {
            SpawnTask();
        }

        _idleTaskQueue.Enqueue(task);
        RemoveTaskFromVisualQueue(task);

        Debug.Log($"Set task {_supervisorAgent.GetTaskNumber(task)} to idle mode.");
    }

    private void ActivateFirstAddedTask(ITask task)
    {
        _activeTasks.Remove(task);
        _supervisorAgent.IsNextAdvanceNotificationIgnored = true;
        _supervisorAgent.WasDecisionRequestedBySystem = true;
        _supervisorAgent.RequestDecision();

        if (!_activeTasks.IsNullOrEmpty() && (_supervisorAgent.UsesHeuristic || _supervisorAgent.Mode == Mode.Suggestion))
        {
            ITask firstAddedTask = _activeTasks.First();
            SwitchToTask(firstAddedTask, Switcher.System);
        }
    }

    private void GenerateRandomScenario()
    {
        System.Random rnd = new();

        _taskQueueScenario = ScriptableObject.CreateInstance<TaskQueueScenario>();
        _taskQueueScenario.TaskIconPathName = TaskIconPathName;

        List<int> priorities = Util.GenerateRandomValues(MinTaskPriority, MaxTaskPriority, NumberOfTasks);
        List<int> seconds = Util.GenerateRandomValues(MinSecondsToNextTask, MaxSecondsToNextTask, NumberOfTasks);

        List<TaskSequence> taskSchedule = new List<TaskSequence>();

        for (int i = 0; i < NumberOfTasks; i++)
        {
            TaskSequence taskSequence = new();
            taskSequence.SecondsToNextTask = seconds[i];
            taskSequence.Priority = priorities[i];

            taskSchedule.Add(taskSequence);
        }

        _taskQueueScenario.TaskSchedule = taskSchedule;
    }

    private void UpdateEnvironmentParameters()
    {
        var envParams = Academy.Instance.EnvironmentParameters;

        NumberOfTasks = (int)envParams.GetWithDefault("NumberOfTasks", NumberOfTasks);
        MaxTaskPriority = (int)envParams.GetWithDefault("MaxTaskPriority", MaxTaskPriority);
        MinTaskPriority = (int)envParams.GetWithDefault("MinTaskPriority", MinTaskPriority);
        MaxSecondsToNextTask = (int)envParams.GetWithDefault("MaxSecondsToNextTask", MaxSecondsToNextTask);
        MinSecondsToNextTask = (int)envParams.GetWithDefault("MinSecondsToNextTask", MinSecondsToNextTask);
    }

    private void SetTerminatingTask()
    {
        if (_taskQueueScenario != null && _taskScheduleIndex == _taskQueueScenario.TaskSchedule.Count)
        {
            List<ITask> nonIdleTasks = _supervisorAgent.Tasks.ToList().FindAll(task => !task.IsIdle);

            if (nonIdleTasks.Count == 1)
            {
                nonIdleTasks.First().IsTerminatingTask = true;
            }
        }
    }
}

