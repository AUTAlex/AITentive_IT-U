using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IPixelVisionAgent
{
    ITask FocusedTask { get; }
    GazeRecorder GazeRecorder { get; set; }
    List<GameObject> ObjectsAtGaze { get; }
    bool PixelOnlyMode { get; set; }
    bool RecordGaze { get; set; }
    int SampleCount { get; }
    bool ShowPerceivedObjects { get; set; }
    bool UseEmbeddings { get; set; }
    float VisionColliderRefreshInterval { get; set; }

    void CollectObservations(VectorSensor sensor);
    void DisposeSpawnedCameras();
    List<Vector2Int> GenerateFovealSamplePositions(Vector2 center, int sampleCount, float radius);
    List<GameObject> GetGameObjectsInFovealArea();
    void Heuristic(in ActionBuffers actionsOut);
    void InitCameras();
    void Initialize();
    void OnActionReceived(ActionBuffers actionBuffers);
    void OnMove(InputValue value);
}