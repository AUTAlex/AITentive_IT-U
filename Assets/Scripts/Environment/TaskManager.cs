using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class TaskManager
{
    public static TaskManager Instance => _instance ??= new TaskManager();

    public int MaxTasks { get; set; } = 100; 



    private static TaskManager _instance;

    private static readonly string _filePath = Path.Combine(Application.streamingAssetsPath, "task_ids.csv");

    private Dictionary<string, int> _taskDictionary;


    public int GetTaskId(string taskName)
    {
        if (_taskDictionary.TryGetValue(taskName, out int existingId))
        {
            return existingId;
        }

        if (_taskDictionary.Count >= MaxTasks)
        {
            throw new InvalidOperationException("[TaskManager] Max number of task IDs reached.");
        }

        int newId = _taskDictionary.Count + 1;
        _taskDictionary[taskName] = newId;
        SaveTasksToCSV();
        return newId;
    }

    public int[] GetOneHotEncoding(string taskName)
    {
        int id = GetTaskId(taskName);
        var encoding = new int[MaxTasks];
        encoding[id - 1] = 1;
        return encoding;
    }

    public int GetTaskIndex(string taskName)
    {
        return GetTaskId(taskName);
    }

    // Optional: for debugging
    public void PrintRegisteredTasks()
    {
        foreach (var kvp in _taskDictionary)
        {
            Debug.Log($"[TaskManager] Task: {kvp.Key} => ID: {kvp.Value}");
        }
    }


    private TaskManager()
    {
        _taskDictionary = new Dictionary<string, int>();
        LoadTasksFromCSV();
    }

    private void LoadTasksFromCSV()
    {
        if (!File.Exists(_filePath))
        {
            Debug.Log($"[TaskManager] No task ID file found. Creating new one at {_filePath}");
            File.WriteAllText(_filePath, "task_name,id\n");
            return;
        }

        var lines = File.ReadAllLines(_filePath);
        foreach (var line in lines.Skip(1)) // skip header
        {
            var parts = line.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[1], out int id))
            {
                _taskDictionary[parts[0]] = id;
            }
        }
    }

    private void SaveTasksToCSV()
    {
        var lines = new List<string> { "task_name,id" };
        lines.AddRange(_taskDictionary.Select(kvp => $"{kvp.Key},{kvp.Value}"));
        File.WriteAllLines(_filePath, lines);
    }
}
