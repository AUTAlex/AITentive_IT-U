using Newtonsoft.Json;
using Supervisor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


public abstract class EngagementTimer : MonoBehaviour
{
    [field: SerializeField, Tooltip("The agent samples from the priovided distripution to simulate focus and interruptions. " +
    "The file must contain two lists on_screen_durations and off_screen_durations and must be located in StreamingAssets folder."), ProjectAssign]
    public string AttentionFileDataName { get; set; }

    [field: SerializeField, Tooltip("The agent samples from a random distribution instead."), ProjectAssign]
    public bool UseRandomTimes { get; set; }

    [field: SerializeField, Tooltip("Minimum on screen duration value used from the given distribution."), ProjectAssign]
    public float OnScreenDurationsMin { get; set; } = 0;

    [field: SerializeField, Tooltip("Maximum on screen duration value used from the given distribution."), ProjectAssign]
    public float OnScreenDurationsMax { get; set; } = 1000;

    [field: SerializeField, Tooltip("Minimum off screen duration value used from the given distribution."), ProjectAssign]
    public float OffScreenDurationsMin { get; set; } = 0;

    [field: SerializeField, Tooltip("Maximum off screen duration value used from the given distribution."), ProjectAssign]
    public float OffScreenDurationsMax { get; set; } = 1000;

    [field: SerializeField]
    public List<VisionAgent> VisionAgents;


    [field: SerializeField]
    private GameObject _snoozeGameObject;

    private SupervisorAgent _supervisorAgent;

    private float _attentionTime;

    private float _attentionTimer;

    private bool _isTaskInterrupted;

    private AttentionData _attentionData;


    protected abstract void SetTasksStatus(ITask[] tasks, bool pause);


    private void OnEnable()
    {
        InitializeAttentionTimes();
        _supervisorAgent = SupervisorAgent.GetSupervisor();
    }

    private void FixedUpdate()
    {
        UpdateAttentionTimeState();

        if (_attentionTimer < _attentionTime && _isTaskInterrupted)
        {
            SetTasksStatus(_supervisorAgent.Tasks, true);
            VisualizeSnooze(true);
        }
        else
        {
            SetTasksStatus(_supervisorAgent.Tasks, false);
            VisualizeSnooze(false);
        }
    }

    private void InitializeAttentionTimes()
    {
        if (!UseRandomTimes)
        {
            if (AttentionFileDataName == "") throw new IOException("AttentionFileDataName is not defined.");

            _attentionData = Util.ImportJsonNewtonsoft<AttentionData>(Path.Combine(Application.streamingAssetsPath, AttentionFileDataName));
        }

        _isTaskInterrupted = true;
        _attentionTime = 0;
        _attentionTimer = 0;
        UpdateAttentionTimeState();
    }

    private void UpdateAttentionTimeState()
    {
        _attentionTimer += Time.deltaTime;

        if (_attentionTime <= _attentionTimer)
        {
            _isTaskInterrupted = !_isTaskInterrupted;
            _attentionTime = GetRandomAttentionDuration();
            _attentionTimer = 0;

            string prefix = _isTaskInterrupted ? "Task interrupted for" : "Task active for";
            Debug.Log($"{prefix} {_attentionTime} seconds.");
        }
    }

    private float GetRandomAttentionDuration()
    {
        float min = _isTaskInterrupted ? OffScreenDurationsMin : OnScreenDurationsMin;
        float max = _isTaskInterrupted ? OffScreenDurationsMax : OnScreenDurationsMax;

        if (UseRandomTimes)
        {
            return Random.Range(min, max);
        }

        List<float> times = _isTaskInterrupted ? _attentionData.OffScreenDurations : _attentionData.OffScreenDurations;
        List<float> filteredList = times.Where(x => x > min && x < max).ToList();

        return filteredList[Random.Range(0, filteredList.Count)];
    }

    private void VisualizeSnooze(bool showSnooze)
    {
        _snoozeGameObject.SetActive(showSnooze);
    }
}



[System.Serializable]
public class AttentionData
{
    [JsonProperty("on_screen_durations")]
    public List<float> OnScreenDurations;

    [JsonProperty("off_screen_durations")]
    public List<float> OffScreenDurations;
}