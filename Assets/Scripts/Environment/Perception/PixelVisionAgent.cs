using Supervisor;
using System;
using System.ArrayExtensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;


[System.Serializable]
public class CameraCaptureInfo
{
    public Camera DisplayCamera;
    public Camera RenderCamera;
    public RenderTexture FoveatedRenderTexture;
    public RenderTexture RawRenderTexture;
    public FoveatedVisionEffect FoveatedVisionEffect;
}


[DefaultExecutionOrder(-10000)]
public class PixelVisionAgent : VisionAgent, IPixelVisionAgent
{
    //[field: SerializeField]
    //public bool TrainingMode { get; set; } = false;

    [field: SerializeField]
    public GazeRecorder GazeRecorder { get; set; }

    [field: SerializeField, ProjectAssign]
    public bool UseMouseHeuristic { get; set; } = false;

    [field: SerializeField, Tooltip("Number of samples taken for detecting objects in the foveal area."), ProjectAssign]
    public int SampleCount { get; private set; } = 32;

    [field: SerializeField, Tooltip("Adds embeddings of the observed objects to observation space if true."), ProjectAssign]
    public bool UseEmbeddings { get; set; } = false;

    [field: SerializeField, Tooltip("Adds embeddings of the observed objects to observation space if true."), ProjectAssign]
    public bool PixelOnlyMode { get; set; } = false;

    [field: SerializeField, Tooltip("When set to true, objects detected within the fovea will appear in yellow."), ProjectAssign]
    public bool ShowPerceivedObjects { get; set; } = false;

    [field: SerializeField, Tooltip("When set to true, gaze movement including task StateInformation will be saved to file."), ProjectAssign]
    public bool RecordGaze { get; set; } = false;

    [field: SerializeField, Tooltip("How often AddVisionColliders is executed in seconds."), ProjectAssign]
    public float VisionColliderRefreshInterval { get; set; } = 1f;

    [field: SerializeField, Tooltip("Determines if UI elements should be detected by the gaze."), ProjectAssign]
    public bool DetectUIObjectsInFovea { get; set; } = false;

    private float _visionColliderRefreshTimer = 0f;

    public List<GameObject> ObjectsAtGaze { get; private set; }

    public ITask FocusedTask
    {
        get
        {
            return TasksProjectSettingsOrdering[_currentTaskIndex];
        }
    }

    [field: SerializeField]
    private RenderTextureSensorComponent[] _renderTextureSensors;

    [field: SerializeField]
    private Shader _foveatedShader;

    [field: SerializeField]
    private CameraCaptureInfo[] _cameraInfos;

    [field: SerializeField]
    private List<RenderTexture> _foveatedRT;

    [field: SerializeField]
    private List<RenderTexture> _rawRT;

    [SerializeField]
    private int MaxPhysicsHitsPerGazeRay = 8;

    [SerializeField]
    private int MinRaycastsPerJob = 4;

    private NativeArray<RaycastCommand> _gazeRayCommands;
    private NativeArray<RaycastHit> _gazeRayResults;

    private readonly List<Vector2Int> _gazeSamplePositions = new();

    private PythonEmbeddingManager _pythonEmbeddingManager;

    private Material _foveatedMaterial;

    private Vector2 _gazeUV;

    private Vector2Int _gazePixelPosition;

    private Vector2Int _previousGazePixelPosition;

    private Vector2 _currentInput;

    private GameObject _dot;

    private Canvas _overlayCanvas;

    private List<GameObject> _targetMarkers;

    private GameObject _canvasGO;

    private PointerEventData _pointerEventData;

    private EventSystem _eventSystem;

    private float _decisionRequestTime = 0.0f;

    private float _decisionRequestTimer = 0.0f;

    private const int VISIONLAYER = 31;


    public void OnMove(InputValue value)
    {
        _currentInput = value.Get<Vector2>();
    }

    public override void Initialize()
    {
        ObjectsAtGaze = new();

        //InitCameras();
        base.Initialize(); // let ML-Agents discover them
        FocusableObjects = CRUtil.GetFocusableGameObjectsOfTasks(TasksProjectSettingsOrdering.ToList()); //override task ordering of VisionAgent
        _pythonEmbeddingManager = GetComponent<PythonEmbeddingManager>();
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        int index = TaskManager.Instance.GetTaskIndex(FocusedTask.GetType().Name);
        int maxTasks = TaskManager.Instance.MaxTasks;

        sensor.AddOneHotObservation(index, maxTasks);

        if (!PixelOnlyMode)
        {
            foreach (ITask task in Tasks)
            {
                try
                {
                    if (task.GetType().GetInterfaces().Contains(typeof(ICRTask)))
                    {
                        ((ICRTask)task).AddBeliefObservationsToSensor(sensor);
                    }
                    else
                    {
                        task.AddTrueObservationsToSensor(sensor);
                    }
                }
                catch (NullReferenceException e)
                {
                    Debug.Log($"Task {task}: {e.Message}");
                }
            }
        }

        if (UseEmbeddings)
        {
            CollectEmbeddings(sensor);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        float rawX = actionBuffers.ContinuousActions[0];
        float rawY = actionBuffers.ContinuousActions[1];

        int targetTextureIndex = actionBuffers.DiscreteActions[0];
        Vector2 absoluteGaze = IsHeuristicMode() ? new(rawX, rawY) : new(Mathf.Clamp01((rawX + 1f) / 2f), Mathf.Clamp01((rawY + 1f) / 2f));
        (float encodingTime, float saccadeTime) = CaclulateEncodingTimes(targetTextureIndex, absoluteGaze);
        _decisionRequestTime = Mathf.Max(encodingTime, saccadeTime);

        _currentTaskIndex = actionBuffers.DiscreteActions[0];
        _gazePixelPosition = GetGazePixelOffset(absoluteGaze);
        //Debug.Log("_gazePixelPosition: " + _gazePixelPosition + "; _previousGazePixelPosition" + _previousGazePixelPosition + "; absoluteGaze: " + absoluteGaze);
        _gazeUV = absoluteGaze;

        if (_previousGazePixelPosition == _gazePixelPosition)
        {
            return;
        }

        _previousGazePixelPosition = _gazePixelPosition;
        //Debug.Log($"GameObject at gaze position {_gazePixelPosition} ({absoluteGaze}) after moving for {_decisionRequestTime} s: {string.Join(", ", GetGameObjectsInFovealArea().Select(go => go.name))}");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (UseMouseHeuristic)
        {
            MouseHeuristic(actionsOut);
        }
        else
        {
            KeyboardHeuristic(actionsOut);
        }
    }

    public List<GameObject> GetGameObjectsInFovealArea()
    {
        HashSet<GameObject> objectsInFovea = new();

        List<GraphicRaycaster> raycasters = GameObject
            .FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None)
            .Where(x =>
                x.isActiveAndEnabled &&
                x.GetComponent<Canvas>() != null)
            .ToList();

        BuildGazeSamplePositions();

        AddPhysicsObjectsInFoveaBatched(objectsInFovea);

        // UI raycasts cannot use RaycastCommand, so keep them on the main thread.
        if (DetectUIObjectsInFovea)
        {
            AddUIObjectsInFovea(objectsInFovea, raycasters);
        }

        return objectsInFovea.ToList();
    }

    public List<Vector2Int> GenerateFovealSamplePositions(Vector2 center, int sampleCount, float radius)
    {
        List<Vector2Int> samples = new();

        float goldenAngle = Mathf.PI * (3 - Mathf.Sqrt(5)); // ~2.39996

        for (int i = 0; i < sampleCount; i++)
        {
            float r = radius * Mathf.Sqrt((float)i / sampleCount);
            float theta = i * goldenAngle;

            float x = center.x + r * Mathf.Cos(theta);
            float y = center.y + r * Mathf.Sin(theta);

            samples.Add(Vector2Int.RoundToInt(new Vector2(x, y)));
        }

        return samples;
    }

    public void InitCameras()
    {
        _cameraInfos = new CameraCaptureInfo[transform.childCount];

        for (int i = 0; i < Tasks.Length; i++)
        {
            int RenderTextureIndex = UseVisionAgentPerTask ? transform.GetSiblingIndex() : i;

            Camera originalCamera = transform.GetChild(i).GetChildInHierarchyByName($"Camera").GetComponent<Camera>();

            //The cloned camera renders its output to _rawRT, which is then processed with the foveated effect. The resulting image is stored in
            //_foveatedRT.
            RenderTexture foveatedRT = CreateRenderTexture(RenderTextureIndex, _foveatedRT, "FoveatedRT");
            RenderTexture rawRT = CreateRenderTexture(RenderTextureIndex, _rawRT, "RawRT");
            Camera clonedCamera = CloneCamera(originalCamera, TasksProjectSettingsOrdering[i], rawRT);
            CreateRenderTextureSensor(foveatedRT, i);
            CreatePreview(foveatedRT, _renderTextureSensors[i].SensorName);

            _cameraInfos[i] = new()
            {
                DisplayCamera = originalCamera,
                RenderCamera = clonedCamera,
                FoveatedRenderTexture = foveatedRT,
                RawRenderTexture = rawRT,
                FoveatedVisionEffect = clonedCamera.GetComponent<FoveatedVisionEffect>()
            };

            if (_cameraInfos[i] == null)
            {
                Debug.LogError($"Child {i} does not have a Camera component.");
            }
        }
    }

    public void DisposeSpawnedCameras()
    {
        if (_cameraInfos.IsNullOrEmpty()) { return; }

        // Destroy cloned cameras
        foreach (var info in _cameraInfos)
        {
            if (info?.RenderCamera != null)
            {
                if (info.RenderCamera.targetTexture != null)
                {
                    info.RenderCamera.targetTexture.Release();
                    DestroyImmediate(info.RenderCamera.targetTexture);
                }

                DestroyImmediate(info.RenderCamera.gameObject);
            }
        }

        // Destroy dynamically added sensor components
        var sensors = GetComponents<RenderTextureSensorComponent>();
        foreach (var sensor in sensors)
        {
            DestroyImmediate(sensor);
        }

        // Destroy preview objects you created
        for (int i = transform.parent.childCount - 1; i >= 0; i--)
        {
            Transform t = transform.parent.GetChild(i);
            if (t.name.Contains("Vision Agent Observation Space"))
            {
                DestroyImmediate(t.gameObject);
            }
        }
    }


    //TODO: Not tested for different agents
    protected override void WriteDiscreteActionMaskSingleView(IDiscreteActionMask actionMask)
    {
        for (int i = 0; i < TasksProjectSettingsOrdering.Count(); i++)
        {
            ITask task = TasksProjectSettingsOrdering[i];
            actionMask.SetActionEnabled(branch: 0, actionIndex: i, isEnabled: Tasks.Where(x => x.GetType() == task.GetType()).Any(x => !x.IsIdle));
        }

        //At least one action must be enabled
        if (Tasks.All(x => x.IsIdle)) { actionMask.SetActionEnabled(branch: 0, actionIndex: 0, isEnabled: true); }
    }

    protected override void WriteDiscreteActionMaskMultiView(IDiscreteActionMask actionMask)
    {
        for (int i = 0; i < TasksProjectSettingsOrdering.Count(); i++)
        {
            bool isEnabled = IsSupervisorGuided ? TasksProjectSettingsOrdering[i].IsActive : !TasksProjectSettingsOrdering[i].IsIdle;
            actionMask.SetActionEnabled(branch: 0, actionIndex: i, isEnabled: isEnabled);
        }

        //At least one action must be enabled
        if (Tasks.All(x => x.IsIdle)) { actionMask.SetActionEnabled(branch: 0, actionIndex: 0, isEnabled: true); }
    }

    protected virtual bool IsHeuristicMode()
    {
        return gameObject.GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly;
    }

    protected override float GetReward()
    {
        float reward = base.GetReward();
        reward -= ObjectsAtGaze.IsNullOrEmpty() ? NullObservationPenalty : 0;

        return reward;
    }


    private RenderTexture CreateRenderTexture(int index, List<RenderTexture> renderTextures, string name)
    {
        if (index < renderTextures.Count)
        {
            return renderTextures[index];
        }

        string rtName = $"{name}_{index}";
        string assetPath = $"Assets/Resources/{rtName}.renderTexture";

        // 1. Try loading from Resources
        RenderTexture existingRT = Resources.Load<RenderTexture>(rtName);
        if (existingRT != null)
        {
            renderTextures.Add(existingRT);
            return existingRT;
        }

#if UNITY_EDITOR

        // 2. Clone base RenderTexture
        RenderTexture baseRT = renderTextures[0];
        RenderTexture newRT = new RenderTexture(baseRT)
        {
            name = rtName
        };

        // 3. Save as asset
        AssetDatabase.CreateAsset(newRT, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 4. Load it properly from Resources and add it
        RenderTexture loadedRT = Resources.Load<RenderTexture>(rtName);
        renderTextures.Add(loadedRT);

        return loadedRT;
#else
        return null;
#endif
    }

    private Tuple<float, float> CaclulateEncodingTimes(int targetTextureIndex, Vector2 absoluteGaze)
    {
        float distance = CalculateDistanceBetweenPositions(targetTextureIndex, _gazePixelPosition, GetGazePixelOffset(absoluteGaze));

        float eccentricity = VisualDistance(distance);
        float encodingTime = CalculateEMMAEncodingTime(eccentricity);

        float executionTime = EXECUTIONTIMEEMMA + SACCADETIMEEMMA * eccentricity;
        float saccadeTime = PREPERATIONTIME + executionTime;

        if (encodingTime < PREPERATIONTIME)
        {
            return new Tuple<float, float>(encodingTime, 0);
        }

        if (encodingTime > saccadeTime)
        {
            float remainingEncodingTime = CalculateEMMAEncodingTime(0);
            encodingTime = (1 - (saccadeTime / encodingTime)) * remainingEncodingTime;
        }

        //Debug.Log("indexSource: " + indexSource + "; indexTaget: " + indexTaget + "; distance: " + distance + "encodingTime: " + encodingTime);

        return new Tuple<float, float>(encodingTime, saccadeTime);
    }

    private Vector2 GetGazeViewport(Vector2Int gazePixelPosition)
    {
        DisplayConfiguration config = GetDisplayConfigurationForCurrentState();

        Vector2 viewport = new Vector2(
            (config.WidthPixel / 2 + (float)gazePixelPosition.x) / config.WidthPixel,
            (config.HeightPixel / 2 + (float)gazePixelPosition.y) / config.HeightPixel
        );

        return viewport;
    }

    private Vector2Int GetGazePixelOffset(Vector2 viewport)
    {
        DisplayConfiguration config = GetDisplayConfigurationForCurrentState();

        float pixelX = (viewport.x * config.WidthPixel) - (config.WidthPixel / 2f);
        float pixelY = (viewport.y * config.HeightPixel) - (config.HeightPixel / 2f);

        return Vector2Int.RoundToInt(new Vector2(pixelX, pixelY));
    }

    private void BuildGazeSamplePositions()
    {
        _gazeSamplePositions.Clear();

        // Center point first.
        _gazeSamplePositions.Add(_gazePixelPosition);

        int fovealRadiusPixels = (int)GetFovealRadiusPixel(1);
        List<Vector2Int> spiralSamples = GenerateFovealSamplePositions(
            _gazePixelPosition,
            SampleCount,
            fovealRadiusPixels
        );

        _gazeSamplePositions.AddRange(spiralSamples);
    }

    private void AddUIObjectsInFovea(HashSet<GameObject> objectsInFovea, List<GraphicRaycaster> raycasters)
    {
        Camera displayCam = _cameraInfos[_currentTaskIndex].DisplayCamera;

        for (int i = 0; i < _gazeSamplePositions.Count; i++)
        {
            Vector2 viewport = GetGazeViewport(_gazeSamplePositions[i]);
            Vector3 screenPos = displayCam.ViewportToScreenPoint(viewport);

            objectsInFovea.UnionWith(
                GetUIElementsAtGaze(raycasters, screenPos, displayCam)
            );
        }
    }

    private void AddPhysicsObjectsInFoveaBatched(HashSet<GameObject> objectsInFovea)
    {
        int rayCount = _gazeSamplePositions.Count;
        if (rayCount == 0)
        {
            return;
        }

        EnsureRaycastBuffers(rayCount);

        Camera displayCam = _cameraInfos[_currentTaskIndex].DisplayCamera;

        int mask = 1 << VISIONLAYER;

        QueryParameters queryParameters = new QueryParameters(
            layerMask: mask,
            hitMultipleFaces: false,
            hitTriggers: QueryTriggerInteraction.Collide,
            hitBackfaces: true
        );

        for (int i = 0; i < rayCount; i++)
        {
            Vector2 viewport = GetGazeViewport(_gazeSamplePositions[i]);
            Ray ray = displayCam.ViewportPointToRay(viewport);

            //Debug.DrawRay(ray.origin, ray.direction * 10f, Color.green, 1f);

            //Debug.Log($"backoff: {backoff}");

            _gazeRayCommands[i] = new RaycastCommand(
                from: ray.origin,
                direction: ray.direction,
                queryParameters: queryParameters,
                distance: 1000f
            );
        }

        JobHandle handle = RaycastCommand.ScheduleBatch(
            _gazeRayCommands,
            _gazeRayResults,
            MinRaycastsPerJob,
            MaxPhysicsHitsPerGazeRay,
            default
        );

        handle.Complete();

        List<string> transparentTags = Tasks
            .DistinctBy(x => x.GetType())
            .SelectMany(x => x.TransparentTagsForVision)
            .ToList();

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            int resultStart = rayIndex * MaxPhysicsHitsPerGazeRay;

            RaycastHit closestHit = default;
            float closestDistance = float.MaxValue;
            GameObject go;

            for (int hitIndex = 0; hitIndex < MaxPhysicsHitsPerGazeRay; hitIndex++)
            {
                RaycastHit hit = _gazeRayResults[resultStart + hitIndex];

                // Unity docs: stop at the first invalid result for this ray.
                if (hit.collider == null)
                {
                    break;
                }

                if (transparentTags.Contains(hit.collider.tag))
                {
                    go = hit.collider.gameObject;

                    objectsInFovea.Add(go);
                    objectsInFovea.UnionWith(go.GetAllParents("SpawnContainer"));
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }
            }

            if (closestHit.collider == null)
            {
                continue;
            }

            go = closestHit.collider.gameObject;

            objectsInFovea.Add(go);
            objectsInFovea.UnionWith(go.GetAllParents("SpawnContainer"));
        }
    }

    private void EnsureRaycastBuffers(int rayCount)
    {
        int resultCount = rayCount * MaxPhysicsHitsPerGazeRay;

        if (!_gazeRayCommands.IsCreated || _gazeRayCommands.Length != rayCount)
        {
            if (_gazeRayCommands.IsCreated)
            {
                _gazeRayCommands.Dispose();
            }

            _gazeRayCommands = new NativeArray<RaycastCommand>(
                rayCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
        }

        if (!_gazeRayResults.IsCreated || _gazeRayResults.Length != resultCount)
        {
            if (_gazeRayResults.IsCreated)
            {
                _gazeRayResults.Dispose();
            }

            _gazeRayResults = new NativeArray<RaycastHit>(
                resultCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory
            );
        }
    }

    private void Start()
    {
        //base.Start();

        for (int i = 0; i < 32; i++)
        {
            Physics.IgnoreLayerCollision(VISIONLAYER, i, true);
        }

        RenderTexture current = _cameraInfos[0].FoveatedRenderTexture;
        _gazePixelPosition = _previousGazePixelPosition = new Vector2Int(0, 0);
        //CreateGazeCanvas();
        CreateGazeOverlay();
        _targetMarkers = new();
        _supervisorAgent = SupervisorAgent.GetSupervisor();
    }

    private void FixedUpdate()
    {
        if (ShowFocusedObject)
        {
            if (ShowPerceivedObjects)
            {
                UpdateTargetMarkers();
            }

            UpdateDotPosition();
        }

        if (IsPaused)
        {
            ObjectsAtGaze.Clear();
            return;
        }

        if (RecordGaze) GazeRecorder.RecordGaze(_gazeUV, TasksProjectSettingsOrdering.Select(x => x.StateInformation).ToList());

        _visionColliderRefreshTimer += Time.fixedDeltaTime;
        if (_visionColliderRefreshTimer >= VisionColliderRefreshInterval)
        {
            AddVisionColliders();
            _visionColliderRefreshTimer = 0f;
        }

        _decisionRequestTimer = IsTaskVisible() ? _decisionRequestTimer + Time.fixedDeltaTime : 0;

        if (_decisionRequestTimer > _decisionRequestTime)
        {
            ObjectsAtGaze = GetGameObjectsInFovealArea();
            //Debug.Log(string.Join(", ", ObjectsAtGaze.Where(obj => obj != null).Select(obj => obj.name)));
            EncodeLegacy();

            SetReward(GetReward());
            UpdateFoveatedBlur();

            RequestDecision();
            _decisionRequestTimer = IsHeuristicMode() ? _decisionRequestTimer : 0;
        }
    }

    private void UpdateFoveatedBlur()
    {
        for (int i = 0; i < _cameraInfos.Count(); i++)
        {
            var info = _cameraInfos[i];

            if (i == _currentTaskIndex)
            {
                ApplyFoveatedVisionEffect(info, GetFovealRadiusViewPort(2.5f));
            }
            else
            {
                ApplyFoveatedVisionEffect(info, 0);
            }
        }
    }

    private void ApplyFoveatedVisionEffect(CameraCaptureInfo info, float radius)
    {
        info.FoveatedVisionEffect.GazeUV = GetGazeViewport(_gazePixelPosition);
        info.FoveatedVisionEffect.FovealRadius = radius;

        RenderTexture rawRT = info.RawRenderTexture;
        RenderTexture foveatedRT = info.FoveatedRenderTexture;

        info.FoveatedVisionEffect.Apply(rawRT, foveatedRT);
    }

    private void UpdateDotPosition()
    {
        // Ensure overlay uses current display camera
        _overlayCanvas.worldCamera = _cameraInfos[_currentTaskIndex].DisplayCamera;

        DisplayConfiguration config = GetDisplayConfigurationForCurrentState();

        float x = (float)_gazePixelPosition.x / config.WidthPixel * _overlayCanvas.pixelRect.width;
        float y = (float)_gazePixelPosition.y / config.HeightPixel * _overlayCanvas.pixelRect.height;

        _dot.transform.localPosition = new Vector3(x, y, 0f);
        //_dot.GetComponent<RectTransform>().anchoredPosition = new Vector3(x, y, 0);
    }

    private void UpdateTargetMarkers()
    {
        if (ObjectsAtGaze == null)
        {
            return;
        }

        _targetMarkers.ForEach(x => Destroy(x));
        _targetMarkers.Clear();

        for (int i = 0; i < ObjectsAtGaze.Count; i++)
        {
            GameObject target = ObjectsAtGaze[i];
            if (!target)
            {
                continue;
            }

            Camera displayCam = _cameraInfos[_currentTaskIndex].DisplayCamera;

            Bounds bounds;

            // Try Renderer
            Renderer rend = target.GetComponentInParent<Renderer>();
            if (rend != null)
            {
                bounds = rend.bounds;
            }
            else
            {
                // Try Collider
                Collider coll = target.GetComponentInParent<Collider>();
                if (coll != null)
                {
                    bounds = coll.bounds;
                }
                else
                {
                    // Fallback: create small bounds around transform
                    bounds = new Bounds(target.transform.position, Vector3.one * 0.1f);
                }
            }

            // Project world bounds to screen corners
            Vector3[] worldCorners = new Vector3[8];
            Vector3 extents = bounds.extents;
            Vector3 center = bounds.center;

            worldCorners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            worldCorners[1] = center + new Vector3(-extents.x, -extents.y, extents.z);
            worldCorners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            worldCorners[3] = center + new Vector3(-extents.x, extents.y, extents.z);
            worldCorners[4] = center + new Vector3(extents.x, -extents.y, -extents.z);
            worldCorners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            worldCorners[6] = center + new Vector3(extents.x, extents.y, -extents.z);
            worldCorners[7] = center + new Vector3(extents.x, extents.y, extents.z);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (var corner in worldCorners)
            {
                Vector3 screenCorner = displayCam.WorldToScreenPoint(corner);
                if (screenCorner.z < 0) continue; // Skip corners behind camera

                min = Vector2.Min(min, screenCorner);
                max = Vector2.Max(max, screenCorner);
            }

            if (min.x == float.MaxValue) continue; // All corners behind camera

            Vector2 screenCenter = (min + max) / 2f;
            Vector2 size = max - min;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayCanvas.GetComponent<RectTransform>(),
                screenCenter,
                displayCam,
                out Vector2 localPoint
            );

            GameObject targetMarker = CreateTargetMarker(localPoint, size);
            _targetMarkers.Add(targetMarker);
        }
    }

    private void AddVisionColliders()
    {
        // 1) figure out which tags to skip
        List<string> ignoreTags = Tasks
            .DistinctBy(x => x.GetType())
            .SelectMany(x => x.IgnoredTagsForVision)
            .ToList();

        // 2) gather all renderers you care about
        var renderers = GameObject
            .FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r =>
                r.enabled &&
                r.gameObject.activeInHierarchy &&
                r.isVisible &&
                !ignoreTags.Contains(r.tag) &&
                r.sharedMaterial != null &&
                r.transform.Find("VisionCollider") == null &&
                (!r.sharedMaterial.HasProperty("_Color") || r.sharedMaterial.color.a > 0 || r.CompareTag("Observable")))
            .ToList();

        int created = 0;

        foreach (var rend in renderers)
        {
            Transform oldVisionCollider = rend.transform.Find("VisionCollider");
            if (oldVisionCollider != null)
            {
                Destroy(oldVisionCollider.gameObject);
            }

            var visionGO = new GameObject("VisionCollider");
            visionGO.transform.SetParent(rend.transform, worldPositionStays: false);
            visionGO.layer = VISIONLAYER;
            visionGO.tag = rend.tag;

            MeshFilter mf = rend.GetComponent<MeshFilter>();

            if (mf == null || mf.sharedMesh == null)
            {
                Destroy(visionGO);
                continue;
            }

            Rigidbody rb = visionGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (visionGO.CompareTag("DetailedVisionMesh"))
            {
                var mc = visionGO.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;

            }
            else
            {
                var bc = visionGO.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center;
                bc.size = mf.sharedMesh.bounds.size;
            }
        }
    }

    private void CollectEmbeddings(VectorSensor sensor)
    {
        int count = Mathf.Min(SampleCount / 2, ObjectsAtGaze.Count);

        // Extract names to embed
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = ObjectsAtGaze[i].name.ToLower();
        }

        names = names.Distinct().ToArray();

        // Get batched embeddings
        float[][] embeddings = _pythonEmbeddingManager.GetEmbeddings(names);

        // Add to sensor
        for (int i = 0; i < SampleCount / 2; i++)
        {
            if (i < names.Length)
            {
                float[] emb = embeddings[i];
                //Debug.Log("emb[0]: " + emb[0]);

                sensor.AddObservation(emb);
            }
            else
            {
                sensor.AddObservation(new float[PythonEmbeddingManager.EmbeddingSize]);
            }
        }
    }

    private List<GameObject> GetUIElementsAtGaze(List<GraphicRaycaster> raycasters, Vector2 screenPosition, Camera camera)
    {
        _pointerEventData = new PointerEventData(_eventSystem);
        _pointerEventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        List<GraphicRaycaster> rayCasters = GetRaycastersAtScreenPosition(raycasters, screenPosition, camera);

        foreach (GraphicRaycaster rayCaster in rayCasters)
        {
            rayCaster.Raycast(_pointerEventData, results);
        }

        return results.Select(x => x.gameObject).ToList();
    }

    private void CreateGazeOverlay()
    {
        _canvasGO = new GameObject("GazeOverlayCanvas");
        _overlayCanvas = _canvasGO.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _overlayCanvas.worldCamera = _cameraInfos[_currentTaskIndex].DisplayCamera;
        _overlayCanvas.planeDistance = 0.1f;
        _overlayCanvas.sortingOrder = 1;

        //_canvasGO.AddComponent<GraphicRaycaster>();

        _dot = new GameObject("GazeDot");
        _dot.transform.SetParent(_canvasGO.transform);
        Image redImage = _dot.AddComponent<Image>();
        redImage.color = Color.red;
        redImage.raycastTarget = false;
        _dot.transform.localScale = new Vector3(3f, 3f, 1f);


        RectTransform rt = redImage.rectTransform;
        rt.sizeDelta = new Vector2(5, 5);

        AssignEventSystem();
    }

    private void AssignEventSystem()
    {
        _eventSystem = EventSystem.current;
        if (_eventSystem == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            _eventSystem = esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    List<GraphicRaycaster> GetRaycastersAtScreenPosition(List<GraphicRaycaster> rayCasters, Vector2 screenPos, Camera cam)
    {
        List<GraphicRaycaster> result = new();

        foreach (var rayCaster in rayCasters)
        {
            if (rayCaster == null || !rayCaster.isActiveAndEnabled)
                continue;

            Canvas canvas = rayCaster.GetComponent<Canvas>();

            if (canvas == null || canvas.GetComponent<RectTransform>() == null)
                continue;

            // Check if the screen position overlaps this canvas
            if (RectTransformUtility.RectangleContainsScreenPoint(canvas.GetComponent<RectTransform>(), screenPos, cam))
            {
                result.Add(rayCaster);
            }
        }

        return result;
    }

    private GameObject CreateTargetMarker(Vector2 localPoint, Vector2 size)
    {
        GameObject targetMarker = new GameObject("GazeTargetMarker");
        targetMarker.transform.SetParent(_canvasGO.transform);
        Image markerImage = targetMarker.AddComponent<Image>();
        markerImage.color = new Color(1f, 1f, 0f, 0.3f);

        RectTransform markerRT = markerImage.rectTransform;
        markerRT.localPosition = new Vector3(0, 0, 0); // Set z to 0 explicitly
        markerRT.anchoredPosition = localPoint;
        markerRT.sizeDelta = size;
        markerRT.localScale = Vector3.one;

        return targetMarker;
    }

    private Vector3 PixelToWorld(Vector2Int pixel, int camIndex)
    {
        Camera cam = _cameraInfos[camIndex].RenderCamera;
        RenderTexture rt = _cameraInfos[camIndex].FoveatedRenderTexture;

        Vector2 viewport = new Vector2(
            (float)pixel.x / rt.width,
            (float)pixel.y / rt.height
        );

        return cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, cam.nearClipPlane + 11f)); // just in front of camera
    }

    private Vector2 ConvertPixelRadiusToViewportRadius(float pixelRadius, Camera camera)
    {
        RenderTexture rt = camera.targetTexture;

        // Viewport is normalized: (0..1) range
        float viewportRadiusX = pixelRadius / rt.width;
        float viewportRadiusY = pixelRadius / rt.height;

        return new Vector2(viewportRadiusX, viewportRadiusY);
    }

    private float ConvertPixelRadiusToViewport(float pixelRadius)
    {
        DisplayConfiguration config = GetDisplayConfigurationForCurrentState();

        // Normalize using the smaller of width or height to maintain circular shape
        float minDimension = Mathf.Min(config.WidthPixel, config.HeightPixel);
        return pixelRadius / minDimension;
    }

    private Tuple<int, Vector2> ApplyGazeAction(Vector2Int delta)
    {
        if (DisplayAlignment == DisplayAlignment.Horizontal)
        {
            return ApplyHorizontalGazeAction(delta);
        }
        else
        {
            return ApplyVerticalGazeAction(delta);
        }
    }

    private Tuple<int, Vector2> ApplyHorizontalGazeAction(Vector2Int delta)
    {
        DisplayConfiguration config = GetDisplayConfigurationForCurrentState();
        Vector2Int newGaze = _gazePixelPosition + delta;
        int index = _currentTaskIndex;

        if (newGaze.x >= config.WidthPixel / 2 && _currentTaskIndex < _cameraInfos.Length - 1)
        {
            index++;
            newGaze.x = newGaze.x - config.WidthPixel;
        }
        else if (newGaze.x < -config.WidthPixel / 2 && _currentTaskIndex > 0)
        {
            index--;
            newGaze.x = config.WidthPixel + newGaze.x;
        }

        //Debug.Log($"_currentTextureIndex: {_currentTextureIndex}; _gazePixelPosition: {_gazePixelPosition}: currentTex.width: {currentTex.width}");

        return new(index, GetGazeViewport(newGaze));
    }

    private Tuple<int, Vector2> ApplyVerticalGazeAction(Vector2Int delta)
    {
        RenderTexture currentTex = _cameraInfos[_currentTaskIndex].FoveatedRenderTexture;
        Vector2Int newGaze = _gazePixelPosition + delta;
        int index = _currentTaskIndex;

        if (newGaze.y >= currentTex.height / 2 && _currentTaskIndex < _cameraInfos.Length - 1)
        {
            index++;
            newGaze.y = newGaze.y - currentTex.height;
        }
        else if (newGaze.y < -currentTex.height / 2 && _currentTaskIndex > 0)
        {
            index--;
            RenderTexture prev = _cameraInfos[_currentTaskIndex].FoveatedRenderTexture;
            newGaze.y = prev.height + newGaze.y;
        }

        return new(index, GetGazeViewport(newGaze));
    }

    private void CreatePreview(RenderTexture renderTexture, string name)
    {
        GameObject preview = new GameObject($"Vision Agent Observation Space {name} Preview");
        preview.transform.SetParent(gameObject.transform.parent);
        preview.AddComponent<RawImage>().texture = renderTexture;
    }

    private void CreateRenderTextureSensor(RenderTexture renderTexture, int index)
    {
        if (_renderTextureSensors == null || _renderTextureSensors.Length != Tasks.Length)
        {
            _renderTextureSensors = new RenderTextureSensorComponent[Tasks.Length];
        }

        _renderTextureSensors[index] = gameObject.AddComponent<RenderTextureSensorComponent>();
        _renderTextureSensors[index].RenderTexture = renderTexture;
        _renderTextureSensors[index].SensorName = $"{TasksProjectSettingsOrdering[index].GetType().Name}_{index}";
        //_renderTextureSensors[index].CompressionType = SensorCompressionType.None;
    }

    private Camera CloneCamera(Camera originalCamera, ITask task, RenderTexture renderTexture)
    {
        //Camera targetCamera = TrainingMode ? originalCamera : Instantiate(originalCamera, task.GetGameObject().transform.parent.GetChildInHierarchyByName("SpawnContainer"));
        Camera targetCamera = Instantiate(originalCamera, task.GetGameObject().transform.parent.GetChildInHierarchyByName("SpawnContainer"));

        targetCamera.rect = MultiScreen ? targetCamera.rect : new Rect(0, 0, 1, 1);
        targetCamera.targetTexture = renderTexture;
        FoveatedVisionEffect foveatedVisionEffect = targetCamera.gameObject.AddComponent<FoveatedVisionEffect>();
        foveatedVisionEffect.FovealRadius = ConvertPixelRadiusToViewport(GetFovealRadiusPixel()); ;
        foveatedVisionEffect.FoveatedShader = _foveatedShader;

        return targetCamera;
    }

    private void SaveTextureToPNG(Texture2D tex, string filename)
    {
        byte[] bytes = tex.EncodeToPNG();
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"Saved PNG to: {path}");
    }

    private bool IsTaskVisible()
    {
        if (CRUtil.IsSingleView(_supervisorAgent))
        {
            return Tasks[_currentTaskIndex].IsActive;
        }

        return true;
    }

    protected RenderTexture GetRenderTextureForCurrentState()
    {
        return UseVisionAgentPerTask ? _foveatedRT[1] : _foveatedRT[_currentTaskIndex];
    }

    private float GetFovealRadiusViewPort(float visualAngleDegrees = 2.5f)
    {
        return ConvertPixelRadiusToViewport(GetFovealRadiusPixel(visualAngleDegrees));
    }

    private float GetFovealRadiusPixel(float visualAngleDegrees = 2.5f)
    {
        return CRUtil.CMToPixel(CRUtil.GetFovealRadiusInCM(GetDisplayConfigurationForCurrentState().DistanceToUserMeter, visualAngleDegrees), GetDisplayConfigurationForCurrentState()); // / GetDisplayRenderTextureRatioForCurrentState();
    }

    /// <summary>
    /// Legacy function to encode the visual state space for the old version of the agent.
    /// </summary>
    private void EncodeLegacy()
    {
        FocusableObjects.ForEach(x => x.DeactivateAllElements());

        foreach (GameObject gameObject in ObjectsAtGaze)
        {
            FocusableObjects[_currentTaskIndex].ActivateElement(gameObject);
        }
    }

    private void OnDestroy()
    {
        DisposeRaycastBuffers();
    }

    private void DisposeRaycastBuffers()
    {
        if (_gazeRayCommands.IsCreated)
        {
            _gazeRayCommands.Dispose();
        }

        if (_gazeRayResults.IsCreated)
        {
            _gazeRayResults.Dispose();
        }
    }

    private void KeyboardHeuristic(in ActionBuffers actionsOut)
    {
        Tuple<int, Vector2> result = ApplyGazeAction(new Vector2Int((int)_currentInput.x * GetDisplayConfigurationForCurrentState().WidthPixel / 50, (int)_currentInput.y * GetDisplayConfigurationForCurrentState().HeightPixel / 50));

        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        continuousActionsOut[0] = result.Item2.x;
        continuousActionsOut[1] = result.Item2.y;
        discreteActionsOut[0] = result.Item1;
    }

    //Currently only works for single task
    private void MouseHeuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2Int mousePositionNorm = new Vector2Int((int)((mousePosition.x / Screen.width) * GetDisplayConfigurationForCurrentState().WidthPixel), (int)((mousePosition.y / Screen.height) * GetDisplayConfigurationForCurrentState().HeightPixel));
        Vector2 viewportPosition = GetGazeViewport(mousePositionNorm);

        continuousActionsOut[0] = viewportPosition.x - 0.5f;
        continuousActionsOut[1] = viewportPosition.y - 0.5f;
        discreteActionsOut[0] = 0; //change logic
    }
}
