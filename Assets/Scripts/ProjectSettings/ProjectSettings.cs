using Supervisor;
using System;
using System.ArrayExtensions;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;


#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Search;
using UnityEngine.UI;


public enum AgentChoice
{
    Ball3DAgentOptimal,
    Ball3DAgentHumanCognition,
    Ball3DAgentHumanCognitionSingleProbabilityDistribution
}


public enum SupervisorChoice
{
    SupervisorAgent,
    SupervisorAgentV1,
    SupervisorAgentRandom,
    SupervisorAgentPriorityHeuristic,
    NoSupport
}


public enum VisionAgentChoice
{
    FocusAgent,
    PixelVisionAgent,
    PixelVisionAgentSequential
}


public enum VisualTaskQueueChoice
{
    None,
    Dynamic,
    Static
}


public class ProjectSettings : MonoBehaviour, IProjectSettings
{
    //Managed by Script and set by SupervisorChoice.Set
    [field: SerializeField, HideInInspector]
    public Supervisor.SupervisorAgent SupervisorAgent { get; private set; }

    //Managed by Script
    [field: SerializeField, HideInInspector]
    public List<VisionAgent> VisionAgents { get; set; }

    //Managed by Script
    [field: SerializeField, HideInInspector]
    public Text ProjectSettingsText { get; set; }

    //Managed by Script
    [field: SerializeField, HideInInspector]
    public GameObject VisionAgentPrefab { get; set; }

    //Could also be displayed via the respective property of the SupervisorAgent and the ProjectAssign attribute but is displayed here for better
    //overview.
    [field: SerializeField, Header("General Settings"), Tooltip("Mode of the supervisor: " +
    "\nForce -> automatic switch, the user cannot decide if the switch should be performed;\n " +
    "\nNotification -> the user will be notified about upcoming switch and can perform the switch during 1 second " +
    "If the switch was not performed by the user, the switch is performed after expiry of this 1 second;\n " +
    "\nSuggestion: the supervisor only suggestion a switch, the decision remains by the user;")]
    public Supervisor.Mode Mode { get; set; }

    public SupervisorChoice SupervisorChoice
    {
        get
        {
            return _supervisorChoice;
        }
        set
        {
            _supervisorChoice = value;
            UpdateSupervisorAgent();
        }
    }

    public VisionAgentChoice VisionAgentChoice
    {
        get
        {
            return _visionAgentChoice;
        }
        set
        {
            _visionAgentChoice = value;
            //UpdateVisionAgent();
        }
    }

    [SerializeField]
    private SupervisorChoice _supervisorChoice;

    [field: SerializeField, Tooltip("TaskQueue: only the active task and a queue of the inactive tasks is shown. Idle tasks are hidden."),
        Enables(ComponentType = typeof(VisualTaskQueue), EnabledForValue = "Dynamic"),
        Enables(ComponentType = typeof(StaticVisualTaskQueue), EnabledForValue = "Static")]
    public VisualTaskQueueChoice VisualTaskQueueChoice { get; set; }

    [SerializeField, ShowIf(ActionOnConditionFail.DontDraw, ConditionOperator.And, nameof(AtLeastOneTaskUsesVisionAgent))]
    private VisionAgentChoice _visionAgentChoice;

    [field: SerializeField, Tooltip("Marks tasks as inactive according to a specified file. This can be used to model inactivity caused by switching " +
        "to a task outside the scope of this environment, such as walking."),
        Enables(ComponentType = typeof(AttentionTimer))]
    public bool UseAttentionTimer { get; set; }

    [field: SerializeField, Tooltip("Marks tasks as invisible according to a specified file."),
    Enables(ComponentType = typeof(VisibilityTimer))]
    public bool UseVisibilityTimer { get; set; }

    [field: SerializeField, Tooltip("The user controls the task if true, otherwise the task-agent.")]
    public bool GameMode { get; set; }

    [field: SerializeField, Tooltip("The different tasks are shown on separate displays.")]
    public bool MultiScreen { get; set; }

    [field: SerializeField, Tooltip("List of models used in the experiment. The AITentiveModel allows to automatically assign the correct model to " +
        "a specific agent. Usually this list contains a supervisor model. In case of the training of the supervisor, also task agent models are " +
        "defined.")]
    public List<AITentiveModel> AITentiveModels { get; set; }

    [field: SerializeField, Tooltip("List of task prefabs that should be used for the experiment."), SearchContext("Tagstring:Task")]
    public GameObject[] TasksGameObjects { get; set; }

    [field: SerializeField, Tooltip("The index of the input list corresponds to task game object. The configured Input Action Asset is used for the" +
        "corresponding task and allows different input settings on task instance level.")]
    public List<InputActionAsset> Inputs { get; set; }

    [field: SerializeField, Header("General Vision Agent Settings"), Tooltip("If enabled, each task gets its own vision agent. If disabled, all tasks share a " +
    "single vision agent."), ShowIf(ActionOnConditionFail.DontDraw, ConditionOperator.And, nameof(AtLeastOneTaskUsesVisionAgent))]
    public bool UseVisionAgentPerTask { get; set; } = false;

    [field: SerializeField, Tooltip("Allows to test the vision agent implementation."),
        ShowIf(ActionOnConditionFail.DontDraw, ConditionOperator.And, nameof(AtLeastOneTaskUsesVisionAgent))]
    public bool HeuristicModeForVisionAgent { get; set; }

    [field: SerializeField, Tooltip("If true, the screen ratio defined for the vision agent is used."),
        ShowIf(ActionOnConditionFail.DontDraw, ConditionOperator.And, nameof(AtLeastOneTaskUsesVisionAgent))]
    public bool UseConstantAspectRatio { get; set; }

    public Agent[] Agents { get; private set; }

    public ITask[] Tasks
    {
        get => TasksGameObjects?.ToList().ConvertAll(x => x != null ? x.transform.GetChildByName("Agent").GetComponent<ITask>() : null).ToArray();
    }

    public MeasurementSettings MeasurementSettings => gameObject.GetComponent<MeasurementSettings>();

    private int _sampleSize;

    private int _simulationId;


    public bool AtLeastOneTaskUsesVisionAgent()
    {
        if (Tasks is not null)
        {
            foreach (ITask task in Tasks)
            {
                if (task != null && task.GetType().GetInterfaces().Contains(typeof(ICRTask)) && ((ICRTask)task).UseVisionAgent)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool AtLeastOneTaskIsAutonomous()
    {
        if (Tasks is not null)
        {
            foreach (ITask task in Tasks)
            {
                if (task != null && task.IsAutonomous)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool AllTasksAreAutonomous()
    {
        if (Tasks is not null)
        {
            foreach (ITask task in Tasks)
            {
                if (task != null && !task.IsAutonomous)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool SupervisorIsRandomSupervisor()
    {
        return SupervisorChoice == SupervisorChoice.SupervisorAgentRandom;
    }

    //the optional parameter t is needed for Build scripts since the UnityEditor library cannot be used for these scripts.
    public void UpdateSettings(bool isBuild = false)
    {
        RebuildEnvironment();
        UpdateSupervisorAgent();
        UpdateVisionAgents();
        UpdateMode(isBuild);
        UpdateAttentionTimer();

        ConfigurePerformanceMeasurment(GetTaskModels(), GetSupervisorModels());
    }

    public void GenerateFilename()
    {
        GetManagedComponentFor<PerformanceMeasurement>().FileNameForScores = Util.GenerateScoreFilename();
        GetManagedComponentFor<BehaviorMeasurementBehavior>().FileNameForBehavioralData = Util.GenerateBehavioralFilename();
    }

    public Dictionary<Type, List<AITentiveModel>> GetTaskModels()
    {
        Dictionary<Type, List<AITentiveModel>> models = new();

        foreach (AITentiveModel aITentiveModel in AITentiveModels)
        {
            if (aITentiveModel != null && aITentiveModel.GetType().GetInterface("ITask") != null)
            {
                if (!models.ContainsKey(aITentiveModel.GetType()))
                {
                    models[aITentiveModel.GetType()] = new();
                }

                models[aITentiveModel.GetType()].Add(aITentiveModel);
            }
        }

        return models;
    }

    public Dictionary<Type, List<AITentiveModel>> GetModels()
    {
        Dictionary<Type, List<AITentiveModel>> models = new();

        foreach (AITentiveModel aITentiveModel in AITentiveModels)
        {
            if (aITentiveModel != null)
            {
                if (!models.ContainsKey(aITentiveModel.GetType()))
                {
                    models[aITentiveModel.GetType()] = new();
                }

                models[aITentiveModel.GetType()].Add(aITentiveModel);
            }
        }

        return models;
    }

    public List<AITentiveModel> GetSupervisorModels()
    {
        return GetModels().GetValueOrDefault(typeof(Supervisor.SupervisorAgent), new());
    }

    public List<AITentiveModel> GetVisionModels()
    {
        Dictionary<Type, List<AITentiveModel>> models = GetModels();
        var model = models.FirstOrDefault(kv => typeof(VisionAgent).IsAssignableFrom(kv.Key)).Value ?? new();
        
        return model; 
    }

    public SupervisorAgent GetActiveSupervisor()
    {
        List<SupervisorAgent> supervisorAgents = SupervisorAgent.gameObject.GetComponents<SupervisorAgent>().ToList();

        foreach (SupervisorAgent supervisorAgent in supervisorAgents)
        {
            if (supervisorAgent.isActiveAndEnabled)
            {
                return supervisorAgent;
            }
        }

        return null;
    }

    public bool IsTrainingModeSupervisor(List<AITentiveModel> supervisorAgentModels)
    {
        bool isModelProvided = false;

        foreach (AITentiveModel aITentiveModel in supervisorAgentModels)
        {
            if (aITentiveModel.Model != null)
            {
                isModelProvided = true;
                break;
            }
        }

        return !isModelProvided && SupervisorChoice != SupervisorChoice.SupervisorAgentRandom && !GameMode;
    }

    public bool IsTrainingModeTasks(Dictionary<Type, List<AITentiveModel>> taskModels = null)
    {
        return (taskModels == null || GetNumberOfDifferentTasks() > taskModels.Count) && !GameMode && (!HeuristicModeForVisionAgent || !AtLeastOneTaskUsesVisionAgent());
    }

    public bool IsTrainingMode(Dictionary<Type, List<AITentiveModel>> taskModels, List<AITentiveModel> supervisorAgentModels)
    {
        return (IsTrainingModeSupervisor(supervisorAgentModels) || IsTrainingModeTasks(taskModels));
    }

    public SupervisorAgent GetSupervisorAgentForSupervisorChoice()
    {
        GameObject gameObject = SupervisorAgent.gameObject;

        if (_supervisorChoice == SupervisorChoice.NoSupport)
        {
            return SupervisorAgent;
        }
        else
        {
            string supervisorChoice = "Supervisor." + _supervisorChoice.ToString();
            SupervisorAgent supervisorAgent = (SupervisorAgent)gameObject.GetBaseComponent(Util.GetType(supervisorChoice));

            if (supervisorAgent is null)
            {
                throw new Exception($"Could not load type {Util.GetType(supervisorChoice)}. Have you added the script to the Supervisor GameObject?");
            }

            return supervisorAgent;
        }
    }

    public T GetManagedComponentFor<T>()
    {
        return (T)(object)GetManagedComponentFor(typeof(T));
    }

    public Component GetManagedComponentFor(Type t)
    {
        if (SupervisorAgent.gameObject.GetBaseComponent(t) != null)
        {
            return SupervisorAgent.gameObject.GetBaseComponent(t);
        }

        foreach (VisionAgent visonAgent in VisionAgents)
        {
            if (visonAgent.GetComponent(t) != null)
            {
                return visonAgent.GetComponent(t);
            }
        }

        foreach (GameObject task in TasksGameObjects)
        {
            if (task != null && task.GetComponentInChildren(t) != null)
            {
                return task.GetComponentInChildren(t);
            }
        }

        return (Component)GameObjectExtensions.FindFirstExactOrDerived(t);
    }

    public List<Component> GetManagedComponentsFor(Type t)
    {
        if (SupervisorAgent.gameObject.GetBaseComponent(t) != null)
        {
            return SupervisorAgent.gameObject.GetComponents(t).ToList();
        }

        if (VisionAgentPrefab.GetComponent(t) != null)
        {
            return VisionAgentPrefab.GetComponents(t).ToList();
        }

        foreach (GameObject task in TasksGameObjects)
        {
            if (task != null && task.GetComponentInChildren(t) != null)
            {
                return task.GetComponentsInChildren(t).ToList();
            }
        }

        return GameObject.FindObjectsByType(t, FindObjectsSortMode.None).ToList().Cast<Component>().ToList();
    }


    protected void Awake()
    {
        HandleSimulationCMDArgs(Util.GetArgs());
    }

    protected void Start()
    {
        LogPropertiesOfComponentsToFile();
    }

    private bool False()
    {
        return false;
    }

    private void HandleSimulationCMDArgs(Dictionary<string, string> args)
    {
        if (args.ContainsKey("-simulation"))
        {
            GetManagedComponentFor<PerformanceMeasurement>().IsAbcSimulation = GetManagedComponentFor<BehaviorMeasurementBehavior>().IsAbcSimulation = true;
            SupervisorChoice = SupervisorChoice.SupervisorAgentRandom;
            Core.ExitOnNumberOfCalls = 2;

            SetHumanCognitionParameters(args);
            UpdateSimulationSettings(int.Parse(args["-simulation"]));
            SetBehaviorTypeOfTaskAgents(GetTaskModels());

            ProjectManager.ProjectAssignValuesToFields();
        }
        else
        {
            GetManagedComponentFor<PerformanceMeasurement>().IsAbcSimulation = GetManagedComponentFor<BehaviorMeasurementBehavior>().IsAbcSimulation = false;
        }
    }

    private void SetHumanCognitionParameters(Dictionary<string, string> args)
    {
        Ball3DAgentHumanCognition ball3DAgentHumanCognition = GetManagedComponentFor<Ball3DAgentHumanCognition>();

        if (ball3DAgentHumanCognition != null)
        {
            ball3DAgentHumanCognition.Sigma = double.Parse(args["-sigma"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.SigmaMean = double.Parse(args["-sigmaMean"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.UpdatePeriod = float.Parse(args["-updatePeriode"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.ObservationProbability = double.Parse(args["-observationProbability"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.ConstantReactionTime = double.Parse(args["-constantReactionTime"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.OldDistributionPersistenceTime = float.Parse(args["-oldDistributionPersistenceTime"].Replace(',', '.'), CultureInfo.InvariantCulture);
            ball3DAgentHumanCognition.DecisionPeriod = int.Parse(args["-decisionPeriodBallAgent"], CultureInfo.InvariantCulture);
        }

        GetManagedComponentFor<PerformanceMeasurement>().SimulationId = int.Parse(args["-id"], CultureInfo.InvariantCulture);
        GetManagedComponentFor<BehaviorMeasurementBehavior>().SimulationId = int.Parse(args["-id"], CultureInfo.InvariantCulture);

        double ObservationProbability = double.Parse(args["-observationProbability"].Replace(',', '.'), CultureInfo.InvariantCulture);

        Assert.IsTrue(ObservationProbability >= 0 && ObservationProbability <= 1);
    }

    private void UpdateSimulationSettings(int sample)
    {
        InitBallAgents();

        PerformanceMeasurement performanceMeasurement = GetManagedComponentFor<PerformanceMeasurement>();
        BehaviorMeasurementBehavior behaviourMeasurementBehaviour = GetManagedComponentFor<BehaviorMeasurementBehavior>();

        behaviourMeasurementBehaviour.UpdateExistingModelBehavior = false;
        performanceMeasurement.FileNameForScores = "sim_scores.csv";
        behaviourMeasurementBehaviour.FileNameForBehavioralData = "sim.csv";
        performanceMeasurement.MaxNumberEpisodes = sample;
        behaviourMeasurementBehaviour.SampleSize = sample;
        performanceMeasurement.MinimumDurationForMeasurement = 0;
        GetManagedComponentFor<SupervisorAgent>().TimeScale = 20;
        SupervisorAgent.GetSupervisor().TimeScale = 20;
    }

    private void UpdateSupervisorAgent()
    {
        SupervisorAgent supervisorAgent = GetSupervisorAgentForSupervisorChoice();
        RestoreSupervisorConfiguration(SupervisorAgent);
        EnableAgent<SupervisorAgent>(supervisorAgent);

        SupervisorAgent = supervisorAgent;

        UpdateVisualTaskQueue();
    }

    private void UpdateVisionAgents()
    {
        foreach (VisionAgent visionAgent in VisionAgents)
        {
            EnableAgent<VisionAgent>(visionAgent);
            UpdateEmbeddingManager(visionAgent);
        }
    }

    private void UpdateAttentionTimer()
    {
        AttentionTimer attentionTimer = SupervisorAgent.GetComponent<AttentionTimer>();
        attentionTimer.VisionAgents = VisionAgents;
    }

    
    private void UpdateEmbeddingManager(VisionAgent visionAgent)
    {

        if (visionAgent.GetType() != typeof(IPixelVisionAgent) || !((IPixelVisionAgent)visionAgent).UseEmbeddings)
        {
            visionAgent.GetComponent<PythonEmbeddingManager>().enabled = false;
        }
        else
        {
            visionAgent.GetComponent<PythonEmbeddingManager>().enabled = true;
        }
    }
    

    private void EnableAgent<T>(Agent agent) where T : Agent
    {
        List<T> agents = new();
        agent.GetComponents(agents);
        agents.ForEach(i => i.enabled = false);

        agent.enabled = true;
    }

    private void UpdateVisualTaskQueue()
    {
        switch (VisualTaskQueueChoice)
        {
            case VisualTaskQueueChoice.Dynamic:
                SupervisorAgent.VisualTaskQueue = GetManagedComponentFor<VisualTaskQueue>();
                break;
            case VisualTaskQueueChoice.Static:
                SupervisorAgent.VisualTaskQueue = GetManagedComponentFor<StaticVisualTaskQueue>();
                break;
        }
    }

    private void RestoreSupervisorConfiguration(Supervisor.SupervisorAgent source)
    {
        GameObject gameObject = SupervisorAgent.gameObject;
        SupervisorAgent[] supervisorAgents = gameObject.GetComponents<SupervisorAgent>();

        foreach (SupervisorAgent supervisorAgent in supervisorAgents)
        {
            supervisorAgent.TaskGameObjects = source.TaskGameObjects;
            supervisorAgent.TaskGameObjectsProjectSettingsOrdering = source.TaskGameObjectsProjectSettingsOrdering;
            supervisorAgent.CumulativeRewardText = source.CumulativeRewardText;
            supervisorAgent.TextMeshProUGUI = source.TextMeshProUGUI;
        }
    }

    private void InitBallAgents()
    {
        Agents = SupervisorAgent.GetBallAgents();
    }

    private void RebuildEnvironment()
    {
        DestroyEnvironment();

        Agents = new Agent[TasksGameObjects.Length];

        bool useVisionAgent = CreateVisionAgents();
        GameObject[] taskGameObjects = CreateTasks(useVisionAgent);

        if (useVisionAgent) AssignTasksToVisionAgents(taskGameObjects);
        AssignTasksToSupervisor(taskGameObjects);
        AssignInputs(taskGameObjects);

        if (GameMode)
        {
            SupervisorAgent.TimeScale = 1;
        }
    }

    private void DestroyEnvironment()
    {
        DestroyGameObjects(SupervisorAgent.TaskGameObjects);
        VisionAgents.OfType<IPixelVisionAgent>().ToList().ForEach(x => x.DisposeSpawnedCameras());
        DestroyGameObjects(VisionAgents.Select(x => x.gameObject).ToArray());
        VisionAgents.Clear();
    }

    private bool CreateVisionAgents()
    {
        bool useVisionAgent = AtLeastOneTaskUsesVisionAgent();

        if (useVisionAgent)
        {
            if (UseVisionAgentPerTask)
            {
                for (int i = 0; i < TasksGameObjects.Length; i++)
                {
                    CreateVisionAgent();
                }
            }
            else
            {
                CreateVisionAgent();
            }
        }

        return useVisionAgent;
    }

    private void CreateVisionAgent()
    {
        GameObject visionAgentGameObject = Instantiate(VisionAgentPrefab, SupervisorAgent.transform);
        VisionAgent visionAgent = (VisionAgent)visionAgentGameObject.GetComponent(_visionAgentChoice.ToString());
        VisionAgents.Add(visionAgent);
        visionAgent.MultiScreen = MultiScreen;
        visionAgent.UseVisionAgentPerTask = UseVisionAgentPerTask;
        visionAgentGameObject.SetActive(true);
    }

    private GameObject[] CreateTasks(bool useVisionAgent)
    {
        Vector3[] coordinatesForTasks = GetCoordinatesForTasks(TasksGameObjects.Length);
        GameObject[] tasks = new GameObject[TasksGameObjects.Length];

        for (int i = 0; i < TasksGameObjects.Length; i++)
        {
            GameObject result;

            if (useVisionAgent)
            {
                if (UseVisionAgentPerTask)
                {
                    result = Instantiate(TasksGameObjects[i], coordinatesForTasks[i], Quaternion.identity, VisionAgents[i].transform);
                }
                else
                {
                    result = Instantiate(TasksGameObjects[i], coordinatesForTasks[i], Quaternion.identity, VisionAgents[0].transform);
                }
            }
            else
            {
                result = Instantiate(TasksGameObjects[i], coordinatesForTasks[i], Quaternion.identity, SupervisorAgent.transform);
            }

            if (MultiScreen)
            {
                ConfigCameraForMultiScreen(result.transform.GetChildByName("Camera").gameObject, i);
            }
            else
            {
                ConfigCameraForSplitScreen(result.transform.GetChildByName("Camera").gameObject, i);
            }

            Agents[i] = result.transform.GetChildByName("Agent").GetComponent<Agent>();
            tasks[i] = result;
        }

        return tasks;
    }

    private void AssignTasksToVisionAgents(GameObject[] taskGameObjects)
    {
        List<GameObject> sortetTaskGameObjects = new List<GameObject>(taskGameObjects);
        //The order of the state information is relevant. Therefore, a supervisor trained on TaskA, TaskB would not work for the order TaskB, TaskA
        sortetTaskGameObjects.Sort((x, y) => string.Compare(x.name, y.name));

        if (UseVisionAgentPerTask)
        {
            for (int i = 0; i < VisionAgents.Count; i++)
            {
                VisionAgents[i].TaskGameObjects = new GameObject[] { sortetTaskGameObjects[i] };
                VisionAgents[i].TaskGameObjectsProjectSettingsOrdering = new GameObject[] { taskGameObjects[i] };
                if (VisionAgents[i] is IPixelVisionAgent pixelVisionAgent)
                {
                    pixelVisionAgent.InitCameras();
                }
            }
        }
        else
        {
            VisionAgents[0].TaskGameObjects = sortetTaskGameObjects.ToArray();
            VisionAgents[0].TaskGameObjectsProjectSettingsOrdering = taskGameObjects.ToArray();
            if (VisionAgents[0] is IPixelVisionAgent pixelVisionAgent)
            {
                pixelVisionAgent.InitCameras();
            }
        }
    }

    private void AssignTasksToSupervisor(GameObject[] tasksGameObjects)
    {
        List<GameObject> sortetTaskGameObjects = new List<GameObject>(tasksGameObjects);
        //The order of the state information is relevant. Therefore, a supervisor trained on TaskA, TaskB would not work for the order TaskB, TaskA
        sortetTaskGameObjects.Sort((x, y) => string.Compare(x.name, y.name));

        SupervisorAgent.TaskGameObjects = sortetTaskGameObjects.ToArray();
        SupervisorAgent.TaskGameObjectsProjectSettingsOrdering = tasksGameObjects.ToArray();
    }

    private void AssignInputs(GameObject[] tasksGameObjects)
    {
        for (int i = 0; i < tasksGameObjects.Length; i++)
        {
            PlayerInput playerInput = tasksGameObjects[i].transform.GetChildByName("Agent").GetComponent<PlayerInput>();

            try
            {
                playerInput.actions = Inputs[i];
            }
            catch (ArgumentOutOfRangeException)
            {
                Inputs.Add(Inputs[0]);
                playerInput.actions = Inputs[i];
            }
        }
    }

    private void ConfigCameraForSplitScreen(GameObject cameraGameObject, int taskNumber)
    {
        Camera camera = cameraGameObject.GetComponent<Camera>();

        if (cameraGameObject.TryGetComponent<CinemachineBrain>(out var cinemachineBrain))
        {
            ConfigCinemachine(cinemachineBrain, taskNumber);
        }

        int taskCount = TasksGameObjects.Length;

        if (!UseConstantAspectRatio || !AtLeastOneTaskUsesVisionAgent())
        {
            float widths = 1f / taskCount;
            camera.rect = new Rect(widths * taskNumber, 0f, widths, 1f);
            return;
        }

        float aspect = GetAspectFromVisionAgent(taskNumber);

        // Find best grid
        GetBestGrid(taskCount, aspect, out int cols, out int rows);

        float cellWidth = 1f / cols;
        float cellHeight = 1f / rows;

        int col = taskNumber % cols;
        int row = taskNumber / cols;

        // Fit aspect INSIDE the cell
        float width = cellWidth;
        float height = width / aspect;

        if (height > cellHeight)
        {
            height = cellHeight;
            width = height * aspect;
        }

        float xOffset = (cellWidth - width) * 0.5f;
        float yOffset = (cellHeight - height) * 0.5f;

        camera.rect = new Rect(
            col * cellWidth + xOffset,
            1f - ((row + 1) * cellHeight) + yOffset, // Unity origin is bottom-left
            width,
            height
        );
    }

    private void ConfigCinemachine(CinemachineBrain cinemachineBrain, int taskNumber)
    {
        var channel = (OutputChannels)(1 << taskNumber);
        cinemachineBrain.ChannelMask = channel;

        Transform cameraTransform = cinemachineBrain.transform;
        Transform parent = cameraTransform.parent;

        foreach (Transform sibling in parent)
        {
            // Skip the Camera object itself
            if (sibling == cameraTransform)
                continue;

            // Find all virtual cameras under this sibling, including inactive ones
            var virtualCameras = sibling.GetComponentsInChildren<CinemachineVirtualCameraBase>(true);

            foreach (var virtualCamera in virtualCameras)
            {
                virtualCamera.OutputChannel = channel;
            }
        }
    }

    private void GetBestGrid(int count, float aspect, out int bestCols, out int bestRows)
    {
        float bestUsage = 0f;
        bestCols = count;
        bestRows = 1;

        for (int rows = 1; rows <= count; rows++)
        {
            int cols = Mathf.CeilToInt(count / (float)rows);

            float cellWidth = 1f / cols;
            float cellHeight = 1f / rows;

            float width = cellWidth;
            float height = width / aspect;

            if (height > cellHeight)
            {
                height = cellHeight;
                width = height * aspect;
            }

            float usage = (width * height) * count;

            if (usage > bestUsage)
            {
                bestUsage = usage;
                bestCols = cols;
                bestRows = rows;
            }
        }
    }

    private float GetAspectFromVisionAgent(int taskNumber)
    {
        DisplayConfiguration config = null;

        if (UseVisionAgentPerTask)
        {
            config = VisionAgents[taskNumber].DisplayConfigurations[0];
        }
        else
        {
            try
            {
                config = VisionAgents[0].DisplayConfigurations[taskNumber];
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogWarning($"Missing display config for task nr. {taskNumber}. Fallback to same config as for task nr. 0.");
                config = VisionAgents[0].DisplayConfigurations[0];
            }
        }

        return config.WidthPixel / config.HeightPixel;
    }


    private void ConfigCameraForMultiScreen(GameObject cameraGameObject, int taskNumber)
    {
        Camera camera = cameraGameObject.GetBaseComponent<Camera>();
        camera.targetDisplay = taskNumber;

        if (cameraGameObject.TryGetComponent<CinemachineBrain>(out var cinemachineBrain))
        {
            ConfigCinemachine(cinemachineBrain, taskNumber);
        }
    }

    private void DestroyGameObjects(GameObject[] gameObjects, int level = 0)
    {
        foreach (GameObject gameObject in gameObjects)
        {
            GameObject parent = gameObject;
            GameObject child = null;

            for (int i = 0; i <= level && parent != null; i++)
            {
                child = parent;
                parent = parent.transform.parent != null ? parent.transform.parent.gameObject : null;
            }

            DestroyImmediate(child);
        }
    }

    private Vector3[] GetCoordinatesForTasks(int numberOfTasks)
    {
        int distanceBetweenTasks = 1500;
        Vector3[] coordinates = new Vector3[numberOfTasks];
        int xStart = numberOfTasks % 2 == 0 ? (numberOfTasks / 2) * (-distanceBetweenTasks) + distanceBetweenTasks / 2 : (numberOfTasks / 2) * (-distanceBetweenTasks);
        coordinates[0] = new Vector3(xStart, 0, 5);

        for (int i = 1; i < numberOfTasks; i++)
        {
            coordinates[i] = new Vector3(coordinates[i - 1].x + distanceBetweenTasks, 0, 5);
        }

        return coordinates;
    }

    private void UpdateMode(bool isBuild = false)
    {
        Dictionary<Type, List<AITentiveModel>> taskModels = GetTaskModels();
        List<AITentiveModel> focusAgentModels = GetVisionModels();
        List<AITentiveModel> supervisorAgentModels = GetSupervisorModels();

        SetBehaviorTypeOfTaskAgents(taskModels);
        List<Unity.InferenceEngine.ModelAsset> visionAgentModels = SetBehaviorTypeOfVisionAgents(taskModels, focusAgentModels);
        Unity.InferenceEngine.ModelAsset visionAgentModel = visionAgentModels.IsNullOrEmpty() ? null : visionAgentModels[0];
        Unity.InferenceEngine.ModelAsset supervisorAgentModel = SetBehaviorTypeOfSupervisorAgent(supervisorAgentModels, isBuild);
        SetBehaviorTypeForAutonomousTaskAgentsTraining(taskModels, supervisorAgentModels);

        SetObservationShape(supervisorAgentModel, SupervisorAgent, SupervisorAgent.VectorObservationSize);
        SetActionSpec(SupervisorAgent, TasksGameObjects.Length);

        foreach (VisionAgent visionAgent in VisionAgents)
        {
            SetObservationShape(visionAgentModel, visionAgent, visionAgent.VectorObservationSize);
            SetActionSpec(visionAgent, visionAgent is IPixelVisionAgent ? (UseVisionAgentPerTask ? 1 : TasksGameObjects.Length) : CRUtil.GetNumberFocusableObjects(SupervisorAgent, Tasks), (visionAgent is IPixelVisionAgent ? 2 : 0));
        }

        foreach (Agent agent in Agents)
        {
            UpdateTaskAgentMode(agent);
        }
    }

    private void UpdateTaskAgentMode(Agent agent)
    {
        SetObservationShape(agent.GetComponent<BehaviorParameters>().Model, agent);

        if (agent is TypingAgent typingAgent)
        {
            typingAgent.NumberOfSplits = MultiScreen ? 1 : SupervisorAgent.TaskGameObjects.Length;
        }
    }

    private void SetBehaviorTypeForAutonomousTaskAgentsTraining(Dictionary<Type, List<AITentiveModel>> taskModels, List<AITentiveModel> supervisorAgentModels)
    {
        if (IsTrainingMode(taskModels, supervisorAgentModels))
        {
            SupervisorAgent.StartCountdownAt = 0;
            MultiScreen = false;
        }
    }

    private void SetBehaviorTypeOfTaskAgents(Dictionary<Type, List<AITentiveModel>> taskModels = null)
    {
        for (int i = 0; i < Agents.Length; i++)
        {
            if (GameMode)
            {
                Agents[i].GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
            }
            else
            {
                Type baseType = Agents[i].GetType().GetLowestBaseTypeInHierarchyOf(typeof(Agent)) == typeof(Task) ? Agents[i].GetType().GetLowestBaseTypeInHierarchyOf(typeof(Task)) : Agents[i].GetType().GetLowestBaseTypeInHierarchyOf(typeof(Agent));

                if (taskModels.ContainsKey(baseType))
                {
                    SetModelForAgent(Agents[i], taskModels[baseType], ((ITask)GetManagedComponentFor(baseType)).DecisionPeriod);
                    Agents[i].GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.InferenceOnly;
                }
                else
                {
                    Agents[i].GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.Default;
                }
            }
        }
    }

    private List<Unity.InferenceEngine.ModelAsset> SetBehaviorTypeOfVisionAgents(Dictionary<Type, List<AITentiveModel>> taskModels, List<AITentiveModel> visionAgentModels = null)
    {
        List<Unity.InferenceEngine.ModelAsset> visionAgentModelAsstes = new();

        foreach (VisionAgent visionAgent in VisionAgents)
        {
            visionAgentModelAsstes.Add(SetBehaviorTypeOfVisionAgent(visionAgent, taskModels, visionAgentModels));
        }

        return visionAgentModelAsstes;
    }

    private Unity.InferenceEngine.ModelAsset SetBehaviorTypeOfVisionAgent(VisionAgent visionAgent, Dictionary<Type, List<AITentiveModel>> taskModels, List<AITentiveModel> visionAgentModels = null)
    {
        Unity.InferenceEngine.ModelAsset visionAgentModel = null;

        if (AtLeastOneTaskUsesVisionAgent())
        {
            if (!visionAgentModels.IsNullOrEmpty())
            {
                visionAgentModel = SetModelForAgent(visionAgent, visionAgentModels);
            }

            if (IsTrainingModeTasks(taskModels))
            {
                visionAgent.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.Default;
            }
            else if (HeuristicModeForVisionAgent)
            {
                visionAgent.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
            }
            else
            {
                visionAgent.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.InferenceOnly;
            }
        }

        return visionAgentModel;
    }

    private Unity.InferenceEngine.ModelAsset SetBehaviorTypeOfSupervisorAgent(List<AITentiveModel> supervisorAgentModels = null, bool isBuild = false)
    {
        Unity.InferenceEngine.ModelAsset supervisorAgentModel = supervisorAgentModel = SetModelForAgent(SupervisorAgent, supervisorAgentModels);

        if (!isBuild)
        {
            SupervisorAgent.Mode = Mode;
        }

        if (SupervisorChoice == SupervisorChoice.NoSupport)
        {
            SupervisorAgent.AdvanceNoticeInSeconds = 0;
            GetManagedComponentFor<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
        }
        else if (IsTrainingModeSupervisor(supervisorAgentModels))
        {
            GetManagedComponentFor<BehaviorParameters>().BehaviorType = BehaviorType.Default;
        }
        else
        {
            GetManagedComponentFor<BehaviorParameters>().BehaviorType = BehaviorType.InferenceOnly;
        }

        return supervisorAgentModel;
    }

    private Unity.InferenceEngine.ModelAsset GetModelForDecisionPeriod(List<AITentiveModel> models, int decisionPeriod = 0)
    {
        string availableDecisionPeriods = "";

        foreach (AITentiveModel aITentiveModel in models)
        {
            if (aITentiveModel.DecisionPeriod == decisionPeriod || decisionPeriod == 0)
            {
                return aITentiveModel.Model;
            }

            availableDecisionPeriods += string.Format(" {0}: {1}", aITentiveModel.name, aITentiveModel.DecisionPeriod);
        }

        Debug.LogWarning(string.Format("Could not find model with decision period of {0}. Found models with the following decision periods: {1}.", decisionPeriod, availableDecisionPeriods));

        return null;
    }

    private Unity.InferenceEngine.ModelAsset SetModelForAgent(Agent agent, List<AITentiveModel> models, int decisionPeriod = 0)
    {
        Unity.InferenceEngine.ModelAsset model = null;

        if (!models.IsNullOrEmpty())
        {
            model = GetModelForDecisionPeriod(models, decisionPeriod);
        }

        agent.GetComponent<BehaviorParameters>().Model = model;

        return model;
    }

    private int CountUniqueTypes(object[] array)
    {
        HashSet<Type> uniqueTypes = new HashSet<Type>();

        foreach (var item in array)
        {
            uniqueTypes.Add(item.GetType());
        }

        return uniqueTypes.Count;
    }

    private int GetNumberOfDifferentTasks()
    {
        return CountUniqueTypes(Agents);
    }

    private void SetObservationShape(Unity.InferenceEngine.ModelAsset agentModel, Agent agent, int shape = -1)
    {
        if (agentModel != null)
        {
            int observationShape = GetObservationShape(Unity.InferenceEngine.ModelLoader.Load(agentModel));
            agent.gameObject.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = observationShape;
        }
        else
        {
            if (shape != -1)
            {
                agent.gameObject.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = shape;
            }
        }
    }

    private void SetActionSpec(Agent agent, int discreteBranchSizes = 0, int numContinuousActions = 0)
    {
        agent.GetComponent<BehaviorParameters>().BrainParameters.ActionSpec = new Unity.MLAgents.Actuators.ActionSpec(
            discreteBranchSizes: discreteBranchSizes != 0 ? new int[] { discreteBranchSizes == 0 ? TasksGameObjects.Length : discreteBranchSizes } : null,
            numContinuousActions: numContinuousActions
            );
    }

    private int GetObservationShape(Unity.InferenceEngine.Model model)
    {
        Unity.InferenceEngine.Model.Input input = GetHighestObsInput(model.inputs);

        int l = input.shape.rank;

        return input.shape.Get(l - 1);
    }

    private Unity.InferenceEngine.Model.Input GetHighestObsInput(List<Unity.InferenceEngine.Model.Input> inputList)
    {
        return inputList
            .Where(input => input.name.StartsWith("obs_"))
            .Select(input => new { input, number = int.TryParse(input.name.Split('_')[1], out int n) ? n : -1 })
            .OrderByDescending(x => x.number)
            .FirstOrDefault().input;
    }

    private void ConfigurePerformanceMeasurment(Dictionary<Type, List<AITentiveModel>> taskModels, List<AITentiveModel> supervisorAgentModels)
    {
        PerformanceMeasurement performanceMeasurement = GetManagedComponentFor<PerformanceMeasurement>();
        BehaviorMeasurementBehavior behaviourMeasurementBehaviour = GetManagedComponentFor<BehaviorMeasurementBehavior>();

        if (IsTrainingMode(taskModels, supervisorAgentModels))
        {
            performanceMeasurement.IsTrainingMode = true;
        }
        else
        {
            performanceMeasurement.IsTrainingMode = false;
        }

        performanceMeasurement.IsSupervised = true;
        if (SupervisorChoice == SupervisorChoice.NoSupport)
        {
            performanceMeasurement.IsSupervised = false;
        }

        if (behaviourMeasurementBehaviour.SaveBehavioralData && !performanceMeasurement.IsAbcSimulation && !performanceMeasurement.MeasurePerformance)
        {
            performanceMeasurement.MaxNumberEpisodes = 0;
            performanceMeasurement.MinimumDurationForMeasurement = 0;
            if (behaviourMeasurementBehaviour.FileNameForBehavioralData != null || behaviourMeasurementBehaviour.FileNameForBehavioralData == "")
            {
                performanceMeasurement.FileNameForScores = "scores_" + Util.GetCSVFilenameForBehavioralDataConfigString(behaviourMeasurementBehaviour.FileNameForBehavioralData);
            }
            else
            {
                Debug.LogWarning("Filename for behavioral data was not defined: New name generated.");
                GenerateFilename();
            }
        }

        if (behaviourMeasurementBehaviour.SaveBehavioralData)
        {

            if (!SupervisorIsRandomSupervisor())
            {
                behaviourMeasurementBehaviour.NumberOfTimeBins = 1;
            }
        }
    }

    private void LogPropertiesOfComponentsToFile()
    {
        SupervisorAgent supervisorAgent = SupervisorAgent.GetSupervisor();
        VisionAgent visionAgent = VisionAgents.IsNullOrEmpty() ? null : VisionAgents[0];

        //supervisorAgent
        LogToFile.LogPropertiesFieldsOfObject(supervisorAgent.GetComponent<BehaviorParameters>());

        supervisorAgent.TaskGameObjects.DistinctBy(x => x.transform.GetChildByName("Agent").GetComponent<ITask>().GetType()).ToList().ForEach(taskGameObject =>
        {
            GameObject agent = taskGameObject.transform.GetChildByName("Agent").gameObject;
            ITask task = agent.GetComponent<ITask>();

            LogToFile.LogPropertiesFieldsOfObject(task);
            LogToFile.LogPropertiesFieldsOfObject(task.StateInformation);
            LogToFile.LogPropertiesFieldsOfObject(agent.GetComponent<BehaviorParameters>());

            Transform ManagersTransform = taskGameObject.transform.GetChildByName("Managers");

            if (ManagersTransform != null)
            {
                Component[] components = ManagersTransform.GetComponents<Component>();

                foreach (Component comp in components)
                {
                    LogToFile.LogPropertiesFieldsOfObject(comp);
                }
            }
        });

        LogToFile.LogPropertiesFieldsOfObject(this);
        LogToFile.LogPropertiesFieldsOfObject(supervisorAgent.VisualTaskQueue);
        LogToFile.LogPropertiesFieldsOfObject(supervisorAgent);
        LogToFile.LogPropertiesFieldsOfObject(supervisorAgent.transform.GetComponent<AttentionTimer>());
        if (visionAgent != null)
        {
            LogToFile.LogPropertiesFieldsOfObject(visionAgent.gameObject.GetComponent<BehaviorParameters>());
            LogToFile.LogPropertiesFieldsOfObject(visionAgent);
        }
    }
}
