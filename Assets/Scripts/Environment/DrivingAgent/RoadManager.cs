using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.MLAgents;
using UnityEngine;
using Utils;
using static UnityEngine.GraphicsBuffer;


public class GameContext
{
    public readonly int MaxSpeed;
    public readonly int MinSpeed;
    public readonly int Step;
    public readonly int SignEveryXRoad;

    public GameContext(int maxSpeed, int minSpeed, int step, int signEveryXRoad)
    {
        MaxSpeed = maxSpeed;
        MinSpeed = minSpeed;
        Step = step;
        SignEveryXRoad = signEveryXRoad;
    }
}


public class RoadManager : MonoBehaviour 
{
    [field: SerializeField]
    public GameObject RoadPrefab { get; set; }
    
    public float RoadLength { get; set; } // The length of the road segment

    public event Action<Road> RoadSegmentHasChanged;

    public event Action<Lane> TargetLaneChanged;

    public event Action<int> TargetSpeedChanged;


    private Transform _spawnContainer;

    private GameObject[] _roadSegments;

    private int _overheadSignCounter = 0; //counter to check if overhead sign should be placed

    private int _lastIndex; // Index of the last segment

    private GameContext _gameContext;


    public void InitializeRoad(GameContext gameContext) 
    {
        _gameContext = gameContext;
        RoadPrefab.GetComponent<Road>().InitRoadLength(_gameContext);
        RoadLength = Road.RoadLength;
        
        _overheadSignCounter = -1;
        _lastIndex = 0;
        if (_roadSegments != null) {
            foreach (GameObject roadSegment in _roadSegments) {
                if (roadSegment) {
                    Destroy(roadSegment);
                }
            }
        }

        // Instantiate and position road segments
        _roadSegments = new GameObject[3];

        for (int i = 0; i < _roadSegments.Length; i++) {
            _roadSegments[i] = Instantiate(RoadPrefab, new Vector3(_spawnContainer.position.x, _spawnContainer.position.y, i * RoadLength - RoadLength),
                Quaternion.identity, _spawnContainer);

            Road road = _roadSegments[i].GetComponent<Road>();
            road.PlayerEntered += OnPlayerEntered;
            road.TargetLaneChanged += OnTargetLaneChanged;
            road.TargetSpeedChanged += OnTargetSpeedChanged;

            RoadContext roadContext = new RoadContext(
                previousSpeed: GetPreviousSpeed(_roadSegments[i]),
                previousLane: GetPreviousLane(_roadSegments[i]),
                isSignActiveOnRoad: _overheadSignCounter <= 0,
                gameContext: _gameContext
            );

            road.InitRoads(roadContext);
            
            _overheadSignCounter++;
            if (_overheadSignCounter.Equals(_gameContext.SignEveryXRoad))
                _overheadSignCounter = 0;
        }

        RoadSegmentHasChanged.Invoke(_roadSegments[1].GetComponent<Road>());
    }

    public void OnPlayerEntered(Road road)
    {
        RoadSegmentHasChanged.Invoke(road);
        MoveLastRoadToFront();
    }

    public void MoveLastRoadToFront() 
    {
        _overheadSignCounter++;
        if (_overheadSignCounter.Equals(_gameContext.SignEveryXRoad))
            _overheadSignCounter = 0;

        Road roadToMove = _roadSegments[_lastIndex].GetComponent<Road>();
        roadToMove.Reset();

        // Calculate the new position for the last segment to be moved to the front
        float newZPosition = _roadSegments[(_lastIndex + 1) % _roadSegments.Length].transform.localPosition.z + RoadLength * 2;
        roadToMove.gameObject.transform.localPosition = new Vector3(0, 0, newZPosition);

        Lane previousLane = GetPreviousLane(roadToMove.gameObject);
        roadToMove.PlaceRoadSign(previousLane);

        roadToMove.PlaceSpeedSign();

        // Update the index of the last segment
        _lastIndex = (_lastIndex + 1) % _roadSegments.Length;
    }

    public Lane GetPreviousLane(GameObject road) 
    {
        Lane previousFreeLaneIndex = Lane.Center;

        for (int currentIndex = 0; currentIndex < _roadSegments.Length; currentIndex++) {
            if (_roadSegments[currentIndex] == road) {
                int previousIndex = (currentIndex - 1 + _roadSegments.Length) % _roadSegments.Length;
                GameObject previousRoad = _roadSegments[previousIndex];
                if (previousRoad) {
                    previousFreeLaneIndex = previousRoad.GetComponent<Road>().GetActiveLine();
                }
            }
        }

        return previousFreeLaneIndex;
    }

    public int GetPreviousSpeed(GameObject road) 
    {
        int previousRoadSpeed = 100;

        for (int currentIndex = 0; currentIndex < _roadSegments.Length; currentIndex++) {
            if (_roadSegments[currentIndex] == road) 
            {    
                int previousIndex = (currentIndex - 1 + _roadSegments.Length) % _roadSegments.Length;
                GameObject previousRoad = _roadSegments[previousIndex];
                
                if (previousRoad) 
                {
                    previousRoadSpeed = previousRoad.GetComponent<Road>().SpeedSign.SignSpeed;
                }
            }
        }

        return previousRoadSpeed;
    }

    public MonoBehaviour GetNextObjectOnRoad<T>(GameObject targetRoad, GameObject agent) where T : MonoBehaviour
    {
        GameObject[] roadsInFrontOfCar = _roadSegments.Where(x => x.transform.position.z >= targetRoad.transform.position.z).OrderBy(x => x.transform.position.z).ToArray();

        foreach (GameObject road in roadsInFrontOfCar)
        {
            MonoBehaviour monoBehaviour = road.GetComponent<Road>().GetObjectOnRoad<T>();

            if (agent.transform.position.z < monoBehaviour.transform.position.z && monoBehaviour.gameObject.activeSelf)
            {
                return monoBehaviour;
            }
        }

        return null;
    }

    public OverheadSignSingle GetNextTargetOverheadSignSingleOnRoad(GameObject targetRoad, GameObject agent)
    {
        MonoBehaviour overheadSign = GetNextObjectOnRoad<OverheadSign>(targetRoad.gameObject, agent);

        if (!overheadSign)
        {
            return null;
        }

        foreach (Transform child in overheadSign.transform)
        {
            // Only check OverheadSign_Left / Center / Right
            if (!child.name.StartsWith("OverheadSign_"))
                continue;

            Transform symbolX = child.Find("symbol_x");

            if (symbolX != null && !symbolX.gameObject.activeSelf)
            {
                return child.gameObject.GetComponent<OverheadSignSingle>();
            }
        }

        return null;
    }

    public List<TextMeshPro> GetNextTargetSpeedTextMeshProOnRoad(GameObject targetRoad, GameObject agent)
    {
        SpeedSign speedSign = (SpeedSign)GetNextObjectOnRoad<SpeedSign>(targetRoad.gameObject, agent);

        if (!speedSign)
        {
            return null;
        }

        return new List<TextMeshPro>() { speedSign.SpeedSignText, speedSign.SpeedSignText2 };
    }

    public int GetNextSpeed(GameObject road, GameObject agent)
    {
        SpeedSign speedSign = GetNextObjectOnRoad<SpeedSign>(road, agent) as SpeedSign;

        return speedSign != null ? speedSign.SignSpeed : -1;
    }

    public int? GetDistanceToNextObject<T>(GameObject road, GameObject agent) where T : MonoBehaviour
    {
        MonoBehaviour objectOnRoad = GetNextObjectOnRoad<T>(road, agent) as T;

        return objectOnRoad != null ? (int)(objectOnRoad.transform.position.z - agent.transform.position.z) : null;
    }

    public Vector3 GetCurrentEndOfRoad() 
    {
        Vector3 endPos = _roadSegments[2].transform.position;
        endPos.z += RoadLength;
        return endPos;
    }

    private void OnTargetLaneChanged(Lane lane)
    {
        TargetLaneChanged.Invoke(lane);
    }

    private void OnTargetSpeedChanged(int signSpeed)
    {
        TargetSpeedChanged.Invoke(signSpeed);
    }

    private void Awake()
    {
        _spawnContainer = gameObject.GetSpawnContainer().transform;
    }

    private void OnDisable()
    {
        foreach (var roadSegment in _roadSegments) 
        {
            Road road = roadSegment.GetComponent<Road>();
            road.PlayerEntered -= OnPlayerEntered;
            road.TargetLaneChanged -= OnTargetLaneChanged;
            road.TargetSpeedChanged -= OnTargetSpeedChanged;
        } 
    }
}