using System.Collections.Generic;
using System.Linq;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// The StaticVisualTaskQueue contains always all symbols. In case a symbol is inactive, its priority is equal to 0. If a new task is added, it 
/// is either set to the top position of the queue or it randomly changes the position with a already spawned task and moves it instead to the top
/// position of the queue. Therefore, this queue allows to simulate a changing priority while not skipping any TaskSequence.
/// </summary>
public class StaticVisualTaskQueue : VisualTaskQueue
{
    private Dictionary<Sprite, bool> _personaAvailable;

    private protected Dictionary<GameObject, ITask> _iconGameObjectTaskMap;


    public override void Restart()
    {
        SpawnAllPersonas();
        base.Restart();
    }


    protected override void AddTaskToVisualQueue(ITask task, Sprite sprite)
    {
        GameObject taskQueueSymbol = GetAvailableTaskQueueSymbol();

        _taskIconGameObjectMap.Add(task, taskQueueSymbol);

        if (_iconGameObjectTaskMap[taskQueueSymbol] == null)
        {
            taskQueueSymbol.transform.SetAsFirstSibling();
        }
        else
        {
            ITask replacedTask = _iconGameObjectTaskMap[taskQueueSymbol];
            GameObject emptyTaskQueueSymbol = GetEmptyTaskQueueSymbol();
            emptyTaskQueueSymbol.transform.SetAsFirstSibling();
            VisualizePriority(emptyTaskQueueSymbol, replacedTask.Priority);
            _iconGameObjectTaskMap[emptyTaskQueueSymbol] = replacedTask;
            _taskIconGameObjectMap[replacedTask] = emptyTaskQueueSymbol;
        }

        _iconGameObjectTaskMap[taskQueueSymbol] = task;

        _activeTasks.Add(task);
        task.IsIdle = false;
        task.Priority = _taskQueueScenario.TaskSchedule[_taskScheduleIndex].Priority;
        VisualizePriority(taskQueueSymbol, _taskQueueScenario.TaskSchedule[_taskScheduleIndex].Priority);

        if (AdaptivePriorityColor)
        {
            SetColorOfPriorityCircle(task);
        }

        _taskScheduleIndex++;
    }

    protected override void RemoveTaskFromVisualQueue(ITask task)
    {
        GameObject taskQueueSymbol = _taskIconGameObjectMap[task];
        _taskIconGameObjectMap.Remove(task);
        _iconGameObjectTaskMap[taskQueueSymbol] = null;

        task.IsActive = false;
        task.IsIdle = true;
        task.Priority = 0;
        VisualizePriority(taskQueueSymbol, 0);
    }

    protected override void ChangeColorOfSelectedTask()
    {
        foreach (KeyValuePair<GameObject, ITask> entry in _iconGameObjectTaskMap)
        {
            ChangeColorOfTask(entry.Value, entry.Key);
        }
    }

    protected override void UpdateStateInformation()
    {
        if (!_iconGameObjectTaskMap.IsNullOrEmpty())
        {
            foreach (KeyValuePair<GameObject, ITask> entry in _iconGameObjectTaskMap)
            {
                if (entry.Value != null && !entry.Value.IsIdle) 
                {
                    entry.Key.transform.GetChildInHierarchyByName("TaskState").GetComponent<TextMeshProUGUI>().text = entry.Value.StateDescription;
                }
                else
                {
                    entry.Key.transform.GetChildInHierarchyByName("TaskState").GetComponent<TextMeshProUGUI>().text = GetDefaultStateDescriptionForConfiguration();
                }
            }
        }
    }

    /**
     * The following code leads to a warning
    private string GetDefaultStateDescriptionForConfiguration()
    {
        Type type = _supervisorAgent.Tasks.FirstOrDefault()?.GetType();
        ITask instance = (ITask)Activator.CreateInstance(type);

        return instance.StateDescription;
    }
    **/

    private string GetDefaultStateDescriptionForConfiguration()
    {
        return "New Conversation...";
    }

    private void VisualizePriority(GameObject taskQueueSymbol, int priority)
    {
        taskQueueSymbol.transform.GetChildInHierarchyByName("PriorityText").GetComponent<TextMeshProUGUI>().text = priority.ToString();
    }

    private void SpawnAllPersonas()
    {
        if (_iconGameObjectTaskMap != null)
        {
            return;
        }

        string taskIconPathName = UseRandomScenario ? TaskIconPathName : TaskQueueScenario.TaskIconPathName;

        List<Sprite> sprites = LoadAllImages(taskIconPathName);
        _iconGameObjectTaskMap = new();

        for (int i = 0; i < _supervisorAgent.Tasks.Length; i++)
        {
            GameObject taskQueueSymbol = Instantiate(_taskQueueSymbolPrefab, gameObject.transform);
            taskQueueSymbol.transform.SetAsFirstSibling();
            taskQueueSymbol.transform.GetChildInHierarchyByName("Image").GetComponent<Image>().sprite = sprites[i];
            taskQueueSymbol.transform.GetChildInHierarchyByName("PriorityText").GetComponent<TextMeshProUGUI>().text = 0.ToString();

            _iconGameObjectTaskMap.Add(taskQueueSymbol, null);
        }
    }

    private GameObject GetAvailableTaskQueueSymbol()
    {
        List<GameObject> availableTaskQueueSymbols = new List<GameObject>();

        foreach (KeyValuePair<GameObject, ITask> entry in _iconGameObjectTaskMap)
        {
            if ((_suggestedTask == null || entry.Value != _suggestedTask) && (_selectedTask == null || entry.Value != _selectedTask) && (entry.Value == null || !entry.Value.IsTaskProcessed))
            {
                availableTaskQueueSymbols.Add(entry.Key);
            }
        }

        System.Random random = new System.Random();
        int index = random.Next(availableTaskQueueSymbols.Count); 

        return availableTaskQueueSymbols[index];
    }

    private GameObject GetEmptyTaskQueueSymbol() 
    {
        foreach (KeyValuePair<GameObject, ITask> entry in _iconGameObjectTaskMap)
        {
            if (entry.Value == null)
            {
                return entry.Key;
            }
        }

        return null;
    }
}
