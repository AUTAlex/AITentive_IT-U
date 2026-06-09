using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TaskQueueScenario", menuName = "AITentive/TaskQueueScenario")]
public class TaskQueueScenario : ScriptableObject
{
    public List<TaskSequence> TaskSchedule;

    public string TaskIconPathName;
}

