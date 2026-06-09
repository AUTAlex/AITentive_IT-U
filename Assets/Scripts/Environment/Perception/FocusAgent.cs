using NUnit.Framework;
using Supervisor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.UI;

public class FocusAgent : VisionAgent
{
    private float[] _encodingTimer;

    private float _saccadeTimer = 0.0f;

    private float[] _encodingTime;

    private Dictionary<GameObject, GameObject> _eyeCanvases;

    private float[] _completedTime;

    private float _saccadeTime;

    private int _currentFixationIndex;


    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (actionBuffers.DiscreteActions[0] == -1)
        {
            Debug.Log("DiscreteActions equals to -1");
            return;
        }

        _currentFixationIndex = _currentTaskIndex;
        _currentTaskIndex = actionBuffers.DiscreteActions[0];

        (_encodingTime, _saccadeTime) = CalculateEMMAFixationTime(_currentTaskIndex); 
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddOneHotObservation(_supervisorAgent.GetActiveTaskNumber(), _supervisorAgent.TaskGameObjects.Length);

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

    public override void Initialize()
    {
        base.Initialize();
        if (ShowFocusedObject) InitVisualization();

        Encode(0);

        _encodingTimer = new float[_numberOfFocusableObjects];
        _encodingTime = new float[_numberOfFocusableObjects];
        _completedTime = new float[_numberOfFocusableObjects];

        Debug.Log("_encodingTimer: " + _encodingTimer);

        _saccadeTime = 0;
        _saccadeTimer = 0;
    }


    //TODO: Not tested for different agents
    protected override void WriteDiscreteActionMaskSingleView(IDiscreteActionMask actionMask)
    {
        for (int i = 0; i < _numberOfFocusableObjects; i++)
        {
            ITask task = CRUtil.GetTaskForIndexOfFocusableObjectSingleView(Tasks, i);
            actionMask.SetActionEnabled(branch: 0, actionIndex: i, isEnabled: Tasks.Where(x => x.GetType() == task.GetType()).Any(x => !x.IsIdle));
        }

        //At least one action must be enabled
        if (Tasks.All(x => x.IsIdle)) { actionMask.SetActionEnabled(branch: 0, actionIndex: 0, isEnabled: true); }
    }

    protected override void WriteDiscreteActionMaskMultiView(IDiscreteActionMask actionMask)
    {
        for (int i = 0; i < _numberOfFocusableObjects; i++)
        {
            (int taskIndex, _) = ActionTo2DIndex(i);
            bool isEnabled = IsSupervisorGuided ? Tasks[taskIndex].IsActive : !Tasks[taskIndex].IsIdle;
            actionMask.SetActionEnabled(branch: 0, actionIndex: i, isEnabled: isEnabled);
        }

        //At least one action must be enabled
        if (Tasks.All(x => x.IsIdle)) { actionMask.SetActionEnabled(branch: 0, actionIndex: 0, isEnabled: true); }
    }


    private void FixedUpdate()
    {
        if(_encodingTimer == null || IsPaused)
        {
            return;
        }

        for (int i = 0; i < _encodingTimer.Length; i++)
        {
            _encodingTimer[i] = IsTaskVisible(i) ? _encodingTimer[i] + Time.fixedDeltaTime : 0;

            if (_encodingTimer[i] > _encodingTime[i] && IsTaskVisible(i))
            {
                Encode(i);
            }
            else
            {
                Decode(i);
            }
        }

        _saccadeTimer += Time.fixedDeltaTime;

        if (_saccadeTimer > _saccadeTime)
        {
            RequestDecision();
            _saccadeTimer = 0;
        }

        SetReward(GetReward());
    }

    /// <summary>
    /// Calculates the needed time for the EMMA model to fixate on the target object.
    /// </summary>
    /// <param name="index"></param>
    /// <returns>(encodingTime, saccadeTime)</returns>
    private Tuple<float[], float> CalculateEMMAFixationTime(int targetIndex)
    {
        float[] newEncodingTimes = new float[_encodingTime.Length];

        float saccadeTime = 0;

        for (int i = 0; i < _encodingTime.Length; i++)
        {
            if(i == targetIndex)
            {
                (newEncodingTimes[i], _completedTime[i]) = CaclulateEncodingTimes(_currentFixationIndex, targetIndex);
                saccadeTime = _completedTime[i];
            }
            else
            {
                (newEncodingTimes[i], _completedTime[i]) = CaclulateEncodingTimes(_currentFixationIndex, i);
            }
        }

        if (newEncodingTimes[targetIndex] < PREPERATIONTIME)
        {
            //new decision after encoding is done
            return new Tuple<float[], float>(newEncodingTimes, newEncodingTimes[targetIndex]);
        }
        else
        {
            return new Tuple<float[], float>(newEncodingTimes, saccadeTime);
        }
    }

    private Tuple<float, float> CaclulateEncodingTimes(int indexSource, int indexTaget)
    {
        (int taskIndexSource, int indexObjectSource) = ActionTo2DIndex(indexSource);
        (int taskIndexTarget, int indexObjectTarget) = ActionTo2DIndex(indexTaget);

        float distance = Vector3.Distance(FocusableObjects[taskIndexSource].GetScreenCoordinatesForGameObjectIndex(indexObjectSource), FocusableObjects[taskIndexTarget].GetScreenCoordinatesForGameObjectIndex(indexObjectTarget));

        distance = CRUtil.PixelToCM(distance, GetDisplayConfigurationForCurrentState()) / 100;
        distance = taskIndexSource == taskIndexTarget ? distance : distance + DistanceBetweenTasksDisplays;

        float eccentricity = VisualDistance(distance);
        float encodingTime = CalculateEMMAEncodingTime(eccentricity);

        float executionTime = EXECUTIONTIMEEMMA + SACCADETIMEEMMA * eccentricity;
        float completedTime = PREPERATIONTIME + executionTime; //this time is completed when the function will be called again since RequestDecision is only called after "completedTime" seconds

        if (encodingTime < _encodingTime[indexTaget])
        {
            encodingTime = (1 - (_completedTime[indexTaget] / _encodingTime[indexTaget])) * encodingTime;
        }
        else if(encodingTime > _encodingTime[indexTaget])
        {
            //resets the timer if the encoding time increases --> the fixation moved away from the object
            _encodingTimer[indexTaget] = 0;
        }

        //Debug.Log("indexSource: " + indexSource + "; indexTaget: " + indexTaget + "; distance: " + distance + "encodingTime: " + encodingTime);

        return new Tuple<float, float>(encodingTime, completedTime);
    }

    private void Encode(int index)
    {
        (int targetTaskIndex, int targetIndex) = ActionTo2DIndex(index);
        FocusableObjects[targetTaskIndex].ActivateElement(targetIndex);

        if (ShowFocusedObject) VisualizeEncoding(index, FocusableObjects[targetTaskIndex].VisualElements[targetIndex]);
    }

    private void Decode(int index)
    {
        (int targetTaskIndex, int targetIndex) = ActionTo2DIndex(index);
        FocusableObjects[targetTaskIndex].DeactivateElement(targetIndex);

        if (ShowFocusedObject) VisualizeDecoding(FocusableObjects[targetTaskIndex].VisualElements[targetIndex]);
    }

    private void VisualizeEncoding(int index, GameObject gameObject)
    {
        Transform eyeImage = _eyeCanvases[gameObject].transform.GetChildByName("Image");

        if (index != _currentFixationIndex)
        {
            eyeImage.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            eyeImage.GetComponent<Image>().color = new Color(255 / 255f, 255 / 255f, 50 / 255f, 150 / 255f);
        }
        else
        {
            eyeImage.localScale = new Vector3(1f, 1f, 1f);
            eyeImage.GetComponent<Image>().color = new Color(0, 0, 0, 255 / 255f);
        }

        _eyeCanvases[gameObject].SetActive(true);
    }

    private void VisualizeDecoding(GameObject gameObject)
    {
        _eyeCanvases[gameObject].SetActive(false);
    }

    private Tuple<int, int> ActionTo2DIndex(int action)
    {
        if (CRUtil.IsSingleView(_supervisorAgent))
        {
            return ActionTo2DIndexSingleView(action);
        }
        else
        {
            return ActionTo2DIndexMultiView(action);
        }
    }

    private Tuple<int, int> ActionTo2DIndexMultiView(int action)
    {
        Tuple<int, int> result;

        int actionIndex = action;

        foreach (VisualStateSpace visualStateSpace in FocusableObjects)
        {
            if (action < visualStateSpace.VisualElements.Count)
            {
                result = new Tuple<int, int>(FocusableObjects.IndexOf(visualStateSpace), action);

                if (result.Item1 == -1)
                {
                    Debug.LogWarning($"Could not find index of {visualStateSpace}.");
                }

                return result;
            }

            action -= visualStateSpace.VisualElements.Count;
        }

        throw new ArgumentException($"Action {actionIndex + 1}/{_numberOfFocusableObjects} is out of range");
    }

    private Tuple<int, int> ActionTo2DIndexSingleView(int action)
    {
        Tuple<int, int> result;

        List<VisualStateSpace> focusableObjects = CRUtil.GetFocusableObjectsSingleView(Tasks);
        int actionIndex = action;

        foreach (VisualStateSpace visualStateSpace in focusableObjects)
        {
            if (action < visualStateSpace.VisualElements.Count)
            {
                result = new Tuple<int, int>(_supervisorAgent.GetActiveTaskNumber(), action);

                if (result.Item1 == -1)
                {
                    Debug.LogWarning($"Could not find index of {visualStateSpace}.");
                }

                return result;
            }

            action -= visualStateSpace.VisualElements.Count;
        }

        throw new ArgumentException($"Action {actionIndex + 1}/{_numberOfFocusableObjects} is out of range");
    }

    private bool IsTaskVisible(int actionIndex)
    {
        if (CRUtil.IsSingleView(_supervisorAgent))
        {
            (int taskIndex, int _) = ActionTo2DIndex(actionIndex);
            return Tasks[taskIndex].IsActive;
        }

        return true;
    }

    private void InitVisualization()
    {
        _eyeCanvases = new();
        int i = 0;

        foreach (ITask task in Tasks)
        {
            GameObject eyeCanvas = task.GetGameObject().transform.parent.transform.GetChildByName("Camera").GetChildByName("Eye_Canvas").gameObject;

            if (task.GetType().GetInterfaces().Contains(typeof(ICRTask)))
            {
                VisualStateSpace visualStateSpace = ((ICRTask)task).FocusStateSpace;

                if (visualStateSpace.VisualElements.Count > 1)
                {
                    foreach (GameObject visualElement in visualStateSpace.VisualElements)
                    {
                        _eyeCanvases[visualElement] = Instantiate(eyeCanvas, visualElement.transform.position, Quaternion.identity, visualElement.transform);
                        _eyeCanvases[visualElement].transform.localScale = new Vector3(1f, 1f, 1f);
                        _eyeCanvases[visualElement].SetActive(false);
                        _eyeCanvases[visualElement].transform.GetChildByName("Image").localPosition = new Vector3(0, 25, 0);

                        i++;
                    }
                }
                else
                {
                    _eyeCanvases[eyeCanvas] = eyeCanvas;

                    i++;
                }
            }
        }
    }
}
