using Supervisor;
using System;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;

public interface IProjectSettings
{
    Agent[] Agents { get; }
    Mode Mode { get; set; }
    Text ProjectSettingsText { get; set; }
    SupervisorAgent SupervisorAgent { get; }
    List<VisionAgent> VisionAgents { get; }
    GameObject[] TasksGameObjects { get; set; }
    ITask[] Tasks { get; }
    List<AITentiveModel> AITentiveModels { get; set; }
    SupervisorChoice SupervisorChoice { get; set; }
    VisionAgentChoice VisionAgentChoice { get; set; }
    bool GameMode { get; set; }
    bool MultiScreen { get; set; }
    bool UseVisionAgentPerTask { get; set; }
    bool UseConstantAspectRatio { get; set; }
    bool HeuristicModeForVisionAgent { get; set; }
    VisualTaskQueueChoice VisualTaskQueueChoice { get; set; }
    bool UseAttentionTimer { get; set; }
    bool UseVisibilityTimer { get; set; }
    GameObject VisionAgentPrefab { get; set; }
    MeasurementSettings MeasurementSettings { get; }
    bool AtLeastOneTaskUsesVisionAgent();
    void GenerateFilename();
    void UpdateSettings(bool isBuild = false);
    T GetManagedComponentFor<T>();
    SupervisorAgent GetActiveSupervisor();
    Component GetManagedComponentFor(Type t);
    List<Component> GetManagedComponentsFor(Type t);
    SupervisorAgent GetSupervisorAgentForSupervisorChoice();
}