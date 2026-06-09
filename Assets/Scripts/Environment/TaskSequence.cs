using UnityEngine;

[System.Serializable]
public class TaskSequence
{
    public float SecondsToNextTask;

    public int Priority;

    public int MaxNumberActiveTasks = 6;
}