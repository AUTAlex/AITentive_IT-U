using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using Utils;
using Random = UnityEngine.Random;
using Inputter;
using System.Linq;

public class GameManager : MonoBehaviour {
    
    [field: SerializeField, ProjectAssign, Header("Scenario")]
    public ScenarioSettings ActiveScenarioSettings { get; set; }

    [field: SerializeField]
    public List<ScenarioSettings> Scenarios { get; set; }

    [field: SerializeField]
    public RoadManager RoadManager { get; set; }

    [field: SerializeField]
    public BeliefUpdater BeliefUpdater { get; set; }

    [field: SerializeField, Tooltip("Enables the component responsible for generating 2D belief-state representations of scene traffic."), ProjectAssign]
    public bool UseBeliefStateForTraffic { get; set; }

    [field: SerializeField, Header("Player")]
    public GameObject Player { get; set; }

    public float CarSpeed { get; set; }

    [field: SerializeField, Header("Goal")]
    public float ScenarioDistance { get; set; } = 5000;

    [field: SerializeField]
    public GameObject FinishPrefab { get; set; }

    [field: SerializeField]
    public GameObject WrongDirectionPrefab { get; set; }

    [field: SerializeField, Header("Speed Sign Settings"), Tooltip("Min Speed on Sign")]
    public int MinSpeed { get; set; } = 100;
    
    [field: SerializeField, Tooltip("Max Speed on Sign")]
    public int MaxSpeed { get; set; } = 150;
    
    [field: SerializeField, Tooltip("Step size on sign")]
    public int Step { get; set; } = 10;

    [field: SerializeField, Tooltip("Stets a seed for the random generator to produce run independent deterministic Scenario generation.")]
    public bool IsDeterministicScenario { get; set; } = false;

    [field: SerializeField, Header("Overhead Sign Settings")]
    public int SignEveryXRoad { get; set; } = 1;

    [field: SerializeField, Header("Self driving Cars")]
    public bool SpawnCars { get; set; } = true;

    [field: SerializeField]
    public float TimeBetweenSpawns { get; set; } = 5f;

    [field: SerializeField]
    public float MaxCars { get; set; } = 10;
    
    [field: SerializeField, Tooltip("Speed in correlation to the current target speed of the player")]
    public float FasterCarsSpeed { get; set; } = 10f;
    
    [field: SerializeField, Tooltip("Speed in correlation to the current target speed of the player")]
    public float SlowerCarsSpeed { get; set; } = -10f;

    [field: SerializeField, ProjectAssign, Header("Performance Log Settings")]
    public bool WritePerformanceLog { get; set; } = false;

    [field: SerializeField]
    public PerformanceLogWriter PerfLogWriter { get; set; } = new();
    
    [field: SerializeField, Header("Logitech G29 Steering Wheel Settings")]
    public float SteeringSensitivity { get; set; } = 0.5f;

    [field: SerializeField]
    public int SaturationPercentage { get; set; } = 35;

    [field: SerializeField]
    public int Coefficient { get; set; } = 95;

    [field: SerializeField]
    public int SpringGain { get; set; } = 20;

    [field: SerializeField]
    public int DefaultSpringGain { get; set; } = 20;

    [field: SerializeField]
    public int OffsetPercentage { get; set; } = 0;

    [field: SerializeField]
    public GameObject[] SelfDrivingCars { get; set; }

    public float CurrentTargetSpeed { get; set; } = 100;

    public float CurrentObservedTargetSpeed { get; set; } = 100;

    public Lane CurrentObservedTargetLane { get; set; }

    public Vector3 CurrentObservedTargetPosition { get; set; }

    public int CurrentObservedDistanceToOverheadSign { get; set; }

    public int CurrentObservedDistanceToSpeedSign { get; set; }

    public bool LaneChangeInProgress { get; set; } = false;
    
    public Lane TargetLaneOfLaneChange { get; set; } = Lane.Center;

    public bool WasNextTargetSpeedObserved { get; set; } = false;

    public bool WasNextTargetLaneObserved { get; set; } = false;

    public bool WasCurrentTargetSpeedObserved { get; set; } = false;

    public bool WasCurrentTargetLaneObserved { get; set; } = false;

    public RCC_CarControllerV3 CarController { get; set; }

    public event Action<object, bool>? OnFinishReachedAction;


    private RCC_InputManager _rcc_InputManager;

    private List<SelfDrivingCar> _fasterCars = new List<SelfDrivingCar>();

    private List<SelfDrivingCar> _slowerCars = new List<SelfDrivingCar>();

    private LogitechGSDK.LogiControllerPropertiesData _logitechProperties;
    
    private GameObject _finish;

    private GameObject _wrongDirection;

    private Coroutine _trafficSpawnCoroutine;
    
    private Road _previousRoadSegment;
    
    private Road _currentRoadSegment;

    //private TerrainManager _terrainManager;

    private Transform _spawnContainer;

    private GameContext _gameContext;

    float _timeScale;


    public void StartSimulation() 
    {
        _gameContext = new GameContext(
            maxSpeed: MaxSpeed,
            minSpeed: MinSpeed,
            step: Step,
            signEveryXRoad: SignEveryXRoad
        );

        PerfLogWriter.Init("PerformanceLog_" + this.GetHashCode() + DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + (ActiveScenarioSettings ? "_" + ActiveScenarioSettings.name : "") + ".txt", this);
        CarController = Player.GetComponent<RCC_CarControllerV3>();

        if (_trafficSpawnCoroutine != null) {
            StopCoroutine(_trafficSpawnCoroutine);
        }
        _trafficSpawnCoroutine = StartCoroutine(SpawnSelfDrivers());

        RoadManager.InitializeRoad(_gameContext);
        
        PlaceStartFinish();
    }
    
    public void RestartSimulation() 
    {
        //_terrainManager.Reset();
        if (ActiveScenarioSettings.IsDeterministicScenario)
        {
            Random.InitState(42);
        }

        PerfLogWriter.Init("PerformanceLog_" + this.GetHashCode() + DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + (ActiveScenarioSettings ? "_" + ActiveScenarioSettings.name : "") + ".txt", this);

        if (_trafficSpawnCoroutine != null) {
            StopCoroutine(_trafficSpawnCoroutine);

            foreach (SelfDrivingCar car in _fasterCars) {
                if (car != null)
                {
                    Destroy(car.gameObject);
                }
            }

            foreach (SelfDrivingCar car in _slowerCars) {
                if (car != null)
                {
                    Destroy(car.gameObject);
                }
            }
        }

        _trafficSpawnCoroutine = StartCoroutine(SpawnSelfDrivers());

        RoadManager.InitializeRoad(_gameContext);

        PlaceAgentAtTargetPosition();
        CurrentTargetSpeed = 100;

        WasNextTargetSpeedObserved = false;
        WasNextTargetLaneObserved = false;
        WasCurrentTargetSpeedObserved = false;
        WasCurrentTargetLaneObserved = false;
        _fasterCars.Clear();
        _slowerCars.Clear();

        PlaceStartFinish();
    }

    public Vector3 GetClosestPointOnLineSegment(Vector3 positionOfPlayerOrAgent) 
    {
        if (positionOfPlayerOrAgent.z >= (_currentRoadSegment.transform.position.z - 5)) {
            return _currentRoadSegment.ClosestPointOnLineSegment(positionOfPlayerOrAgent);
        }

        if (_previousRoadSegment) {
            return _previousRoadSegment.ClosestPointOnLineSegment(positionOfPlayerOrAgent);
        }

        Debug.LogWarning("Failed to get Closest Point on Line Segment");
        return Vector3.zero;
    }

    public void SetCurrentRoadSegment(Road road) 
    {
        _previousRoadSegment = _currentRoadSegment;
        _currentRoadSegment = road;
    }

    public void SetCurrentTargetLane(Lane lane)
    {
        if (LaneChangeInProgress)
        {
            PerfLogWriter.WriteInfoLine(InfoLineType.LaneChangeFailed, (float)TargetLaneOfLaneChange);
        }

        PerfLogWriter.WriteInfoLine(InfoLineType.LaneChangeStart, (float)lane);
        PerfLogWriter.WriteInfoLine(InfoLineType.OptimalLaneChangeStart, (float)lane);
        LaneChangeInProgress = true;
        TargetLaneOfLaneChange = lane;
        UpdateNextTargetLaneState();
    }

    public void SetCurrentTargetSpeed(int signSpeed)
    {
        UpdateCurrentTargetSpeed(signSpeed);
        UpdateNextTargetSpeedState();
    }

    public void PlaceAgentAtTargetPosition() {
        float targetPosition = GetClosestPointOnLineSegment(Vector3.zero).x;

        StartCoroutine(Freeze(CarController));
        RCC.Transport(CarController, new Vector3(targetPosition, 0.75f, 10), Quaternion.identity);
    }

    /// <summary>
    /// Freezing the target vehicle until the RPM drops below 801.
    /// </summary>
    /// <param name="vehicle"></param>
    /// <returns></returns>
    private IEnumerator Freeze(RCC_CarControllerV3 vehicle)
    {
        while ((int)vehicle.engineRPMRaw > 800)
        {
            //Time.timeScale = 20f;
            vehicle.canControl = false;
            vehicle.Rigid.linearVelocity = new Vector3(0f, vehicle.Rigid.linearVelocity.y, 0f);
            vehicle.Rigid.angularVelocity = Vector3.zero;
            yield return null;
        }
        //Time.timeScale = _timeScale;

        vehicle.canControl = true;
    }

    public void UpdateNextTargetLaneState()
    {
        WasCurrentTargetLaneObserved = WasNextTargetLaneObserved;
        WasNextTargetLaneObserved = false;
    }

    public void UpdateNextTargetSpeedState()
    {
        WasCurrentTargetSpeedObserved = WasNextTargetSpeedObserved;
        WasNextTargetSpeedObserved = false;
    }

    public Vector3 GetCurrentLaneLocation(GameObject agent) 
    {
        Vector3 playerPos = agent.transform.position;
        var lane = _currentRoadSegment.IsInFrontOfOverheadSign(playerPos)
            ? _previousRoadSegment.GetActiveLine()
            : _currentRoadSegment.GetActiveLine();

        playerPos.x = DrivingUtil.GetXLocationForLane(lane, gameObject);

        return playerPos;
    }

    public Lane? GetTargetLane(GameObject agent)
    {
        OverheadSign overheadSign = (OverheadSign)RoadManager.GetNextObjectOnRoad<OverheadSign>(_currentRoadSegment.gameObject, agent);

        return overheadSign != null ? overheadSign.Lane : null;
    }

    public void UpdateCurrentTargetSpeed(int signSpeed) 
    {
        CurrentTargetSpeed = signSpeed;

        PerfLogWriter.WriteInfoLine(InfoLineType.TargetSpeedChange, signSpeed);

        foreach (SelfDrivingCar fasterCar in _fasterCars) {
            fasterCar.MaximumSpeed = CurrentTargetSpeed + FasterCarsSpeed;
        }

        foreach (SelfDrivingCar slowerCar in _slowerCars) {
            slowerCar.MaximumSpeed = CurrentTargetSpeed + SlowerCarsSpeed;
        }
    }

    public void UpdateCurrentObservedTargetSpeed(GameObject agent)
    {
        if (IsTargetSpeedObservable(agent))
        {
            WasNextTargetSpeedObserved = true;
            CurrentObservedTargetSpeed = RoadManager.GetNextSpeed(_currentRoadSegment.gameObject, agent);
        }
    }

    public void UpdateCurrentObservedTargetSpeed(GameObject agent, List<GameObject> observedObjects)
    {
        List<TextMeshPro> speedSignTexts = RoadManager.GetNextTargetSpeedTextMeshProOnRoad(_currentRoadSegment.gameObject, agent);

        if (speedSignTexts.IsNullOrEmpty())
        {
            return;
        }

        List<GameObject> speedSignTextGameObject = speedSignTexts.Select(s => s.gameObject).ToList();

        if (speedSignTextGameObject.Any(gameObject => observedObjects.Contains(gameObject)))
        {
            UpdateCurrentObservedTargetSpeed(agent);
        }
    }

    public void UpdateCurrentObservedTargetPosition(GameObject agent)
    {
        if (IsTargetPositionObservable(agent))
        {
            WasNextTargetLaneObserved = true;
            CurrentObservedTargetLane = (Lane)GetTargetLane(agent);
        }
    }

    public void UpdateCurrentObservedTargetPosition(GameObject agent, List<GameObject> observedObjects)
    {
        MonoBehaviour overheadSign = RoadManager.GetNextTargetOverheadSignSingleOnRoad(_currentRoadSegment.gameObject, agent);

        if (!overheadSign)
        {
            return;
        }

        GameObject overheadSignGameObject = overheadSign.gameObject;

        if (observedObjects.Contains(overheadSignGameObject))
        {
            UpdateCurrentObservedTargetPosition(agent);
        }
    }

    public void UpdateCurrentObservedDistanceToOverheadSign(GameObject agent)
    {
        if (IsTargetPositionObservable(agent))
        {
            CurrentObservedDistanceToOverheadSign = GetDistanceToNextObject<OverheadSign>(agent).GetValueOrDefault();
        }
    }

    public void UpdateCurrentObservedDistanceToOverheadSign(GameObject agent, List<GameObject> observedObjects)
    {
        MonoBehaviour overheadSign = RoadManager.GetNextObjectOnRoad<OverheadSign>(_currentRoadSegment.gameObject, agent);

        if (!overheadSign)
        {
            return;
        }

        GameObject overheadSignGameObject = overheadSign.gameObject;

        if (observedObjects.Contains(overheadSignGameObject))
        {
            UpdateCurrentObservedDistanceToOverheadSign(agent);
        }
    }

    public void UpdateCurrentObservedDistanceToSpeedSign(GameObject agent)
    {
        if (IsTargetSpeedObservable(agent))
        {
            CurrentObservedDistanceToSpeedSign = GetDistanceToNextObject<SpeedSign>(agent).GetValueOrDefault();
        }
    }

    public void UpdateCurrentObservedDistanceToSpeedSign(GameObject agent, List<GameObject> observedObjects)
    {
        MonoBehaviour speedSign = RoadManager.GetNextObjectOnRoad<SpeedSign>(_currentRoadSegment.gameObject, agent);

        if (!speedSign)
        {
            return;
        }

        GameObject speedSignGameObject = speedSign.gameObject;

        if (observedObjects.Contains(speedSignGameObject))
        {
            UpdateCurrentObservedDistanceToSpeedSign(agent);
        }
    }

    public bool IsTargetSpeedObservable(GameObject agent)
    {
        return GetDistanceToNextObject<SpeedSign>(agent) < 100;
    }

    public bool IsTargetPositionObservable(GameObject agent)
    {
        return GetDistanceToNextObject<OverheadSign>(agent) < 500;
    }

    public int? GetDistanceToNextObject<T>(GameObject agent) where T : MonoBehaviour
    {
        if(_currentRoadSegment == null)
        {
            return null;
        }

        return RoadManager.GetDistanceToNextObject<T>(_currentRoadSegment.gameObject, agent);
    }

    public void LoadScenarioSettings() 
    {
        ScenarioDistance = ActiveScenarioSettings.ScenarioDistance;
        MinSpeed = ActiveScenarioSettings.MinSpeed;
        MaxSpeed = ActiveScenarioSettings.MaxSpeed;
        Step = ActiveScenarioSettings.Step;
        SignEveryXRoad = ActiveScenarioSettings.SignEveryXRoad;
        SpawnCars = ActiveScenarioSettings.SpawnCars;
        TimeBetweenSpawns = ActiveScenarioSettings.TimeBetweenSpawns;
        MaxCars = ActiveScenarioSettings.MaxCars;
        FasterCarsSpeed = ActiveScenarioSettings.FasterCarsSpeed;
        SlowerCarsSpeed = ActiveScenarioSettings.SlowerCarsSpeed;
        //WritePerformanceLog = ActiveScenarioSettings.WritePerformanceLog;
        IsDeterministicScenario = ActiveScenarioSettings.IsDeterministicScenario;
    }


    private void Awake()
    {
        _spawnContainer = gameObject.GetSpawnContainer().transform;

        if (Scenarios.Count > 0)
        {
            if (!ActiveScenarioSettings)
            {
                ActiveScenarioSettings = Scenarios[0];
            }
            LoadScenarioSettings();
        }
    }

    private void FixedUpdate()
    {
        if (CarController)
        {
            CarSpeed = CarController.speed;

            if (WritePerformanceLog) 
            {
                LogDistance();
            }

            if (ScenarioDistance < CarController.transform.position.z)
            {
                //RestartSimulation();
                OnFinishReachedAction?.Invoke(this, false);
            }

            if (WasCurrentTargetLaneObserved)
            {
                CurrentObservedTargetPosition = GetClosestPointOnLineSegment(CarController.transform.position);
            }
        }
    }

    private void Update()
    {
        if (CarController)
        {
            if (LaneChangeInProgress && Mathf.Abs(CarController.transform.position.x - DrivingUtil.GetXLocationForLane(TargetLaneOfLaneChange, gameObject)) <= 1)
            {
                LaneChangeInProgress = false;
                PerfLogWriter.WriteInfoLine(InfoLineType.LaneChangeEnd, (float)TargetLaneOfLaneChange);
            }
        }
    }

    private void Start()
    {
        //_terrainManager = TerrainManager.Instance;
        _rcc_InputManager = RCC_InputManager.Instance;

        //rcc_InputManager.logitechSteeringSensitivity = steeringSensitivity;

        //LogitechGSDK.LogiSteeringInitialize(false);
        //InitializeLogitechSteeringWheel();
        _timeScale = Time.timeScale;

        StartSimulation();
    }

    private void OnEnable()
    {
        RoadManager.RoadSegmentHasChanged += SetCurrentRoadSegment;
        RoadManager.TargetLaneChanged += SetCurrentTargetLane;
        RoadManager.TargetSpeedChanged += SetCurrentTargetSpeed;
    }

    private void OnDisable()
    {
        Debug.Log("GameManager disabled, shutting down Logitech SDK");
        RoadManager.RoadSegmentHasChanged -= SetCurrentRoadSegment;
        RoadManager.TargetLaneChanged -= SetCurrentTargetLane;
        RoadManager.TargetSpeedChanged -= SetCurrentTargetSpeed;
        //LogitechGSDK.LogiSteeringShutdown();
    }


    private void PlaceStartFinish()
    {
        if (_finish != null)
        {
            _finish.transform.position = new Vector3(_spawnContainer.position.x, _spawnContainer.position.y, _spawnContainer.position.z + ScenarioDistance);
            _wrongDirection.transform.position = new Vector3(_spawnContainer.position.x, _spawnContainer.position.y, _spawnContainer.position.z - 5);
        }
        else
        {
            _finish = Instantiate(FinishPrefab, new Vector3(_spawnContainer.position.x, _spawnContainer.position.y, _spawnContainer.position.z + ScenarioDistance), Quaternion.identity, _spawnContainer);
            _wrongDirection = Instantiate(WrongDirectionPrefab, new Vector3(_spawnContainer.position.x, _spawnContainer.position.y, _spawnContainer.position.z - 5), Quaternion.identity, _spawnContainer);
        }
    }

    private IEnumerator SpawnSelfDrivers()
    {
        bool skippWait = false;
        WaitForSeconds waitAfterFailedSpawn = new WaitForSeconds(1f);

        while (SpawnCars)
        {
            if (!skippWait)
                yield return new WaitForSeconds(TimeBetweenSpawns);

            if (MaxCars > _fasterCars.Count + _slowerCars.Count)
            {
                int decider = Random.Range(0, 2);
                Lane deciderLane = EnumHelper.GetRandomEnumValue<Lane>();

                Vector3 spawnLocation = GetSpawnLocation(decider, deciderLane);

                // Size of the box to check for colliders
                Vector3 checkBoxSize = new Vector3(2, 1, 2);

                Collider[] hitColliders = Physics.OverlapBox(spawnLocation, checkBoxSize * 0.5f);
                if (hitColliders.Length > 0)
                {
                    skippWait = true;
                    yield return waitAfterFailedSpawn;
                    continue;
                }

                float checkDistance = 100f; // Distance to check behind the spawn location
                // Check for cars driving towards the spawn location using a raycast
                RaycastHit hit;
                if (Physics.Raycast(spawnLocation, -Vector3.forward, out hit, checkDistance))
                {
                    if (hit.collider.CompareTag("SelfDriving") || hit.collider.CompareTag("PlayerCar") || hit.collider.CompareTag("AgentCar"))
                    { // Assuming cars have a tag "Car"
                        // Debug.Log("Couldn't Spawn Car - Car detected within 50 units behind the spawn point");
                        skippWait = true;
                        yield return waitAfterFailedSpawn;
                        continue;
                    }
                }

                SpawnSelfDriver(decider, deciderLane, spawnLocation);

                skippWait = false;
            }
        }
    }

    private Vector3 GetSpawnLocation(int decider, Lane deciderLane)
    {
        float zLocation = decider switch
        {
            0 => Player.transform.position.z - 50f,
            1 => Player.transform.position.z + 150f,
            _ => 0
        };

        return new Vector3(DrivingUtil.GetXLocationForLane(deciderLane, gameObject), 0.75f, zLocation);
    }

    private void SpawnSelfDriver(int decider, Lane deciderLane, Vector3 spawnLocation)
    {
        GameObject car = Instantiate(SelfDrivingCars[Random.Range(0, SelfDrivingCars.Length)],
                    spawnLocation, Quaternion.identity, _spawnContainer);

        if (UseBeliefStateForTraffic)
        {
            BeliefUpdater.RegisterBelievableObject<RelativeBelievableObject2DSparse>(car);
        }

        // Debug.Log("Spawn: " + car.name);

        SelfDrivingCar selfDrivingCar = car.GetComponent<SelfDrivingCar>();
        selfDrivingCar.TargetLane = deciderLane;

        switch (decider)
        {
            case 0:
                _fasterCars.Add(selfDrivingCar);
                break;
            case 1:
                _slowerCars.Add(selfDrivingCar);
                break;
        }

        foreach (Collider collider in car.GetComponentsInHierarchy<Collider>())
        {
            Physics.IgnoreCollision(collider, _wrongDirection.GetComponentInChildren<Collider>());
        }
    }

    private void InitializeLogitechSteeringWheel() 
    {
        Debug.Log($"Logitech Steering Wheel: {LogitechGSDK.LogiUpdate()} | {LogitechGSDK.LogiIsConnected(0)}");

        if (!LogitechGSDK.LogiIsConnected(0))
            return;

        LogitechGSDK.LogiStopSpringForce(0);
        LogitechGSDK.LogiStopConstantForce(0);
        LogitechGSDK.LogiStopDamperForce(0);

        _logitechProperties.wheelRange = 90;
        _logitechProperties.forceEnable = true;
        _logitechProperties.overallGain = 80;
        _logitechProperties.springGain = SpringGain;
        _logitechProperties.damperGain = 80;
        _logitechProperties.allowGameSettings = false;
        _logitechProperties.combinePedals = false;
        _logitechProperties.defaultSpringEnabled = true;
        _logitechProperties.defaultSpringGain = DefaultSpringGain;
        LogitechGSDK.LogiSetPreferredControllerProperties(_logitechProperties);

        Debug.Log("Logitech Steering Wheel Initialized");
        LogitechGSDK.LogiPlaySpringForce(0, OffsetPercentage, SaturationPercentage, Coefficient);
        // LogitechGSDK.LogiPlayConstantForce(0, 50);
        // LogitechGSDK.LogiPlayDamperForce(0, 100);
    }

    private void LogDistance()
    {
        float distanceToCurrentLane = -1;

        if (CarController.transform.position.z >= (_currentRoadSegment.transform.position.z - 5))
        {
            distanceToCurrentLane = _currentRoadSegment.DistancePointToLineSegment(CarController.transform.position);
        }
        else
        {
            if (_previousRoadSegment)
            {
                distanceToCurrentLane =
                    _previousRoadSegment.DistancePointToLineSegment(CarController.transform.position);
            }
        }

        PerfLogWriter.WriteValueLine(CarController.transform.position.z, distanceToCurrentLane, CarSpeed,
            CarController.steerAngle * CarController.steerInput);
    }
}