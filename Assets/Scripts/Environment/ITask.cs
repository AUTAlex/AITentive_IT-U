using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public interface ITask
{
    /// <summary>
    /// Used by the supervisor to indicate that the task is active. The task should only be controllable if this value is true.
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>
    /// Used by the supervisor to indicate that the user should switch to this task.
    /// </summary>
    bool IsSuggested { get; set; }

    /// <summary>
    /// Used by the supervisor to determine when its episode should end. The supervisor ends its episode if the task is terminal. Otherwise only the
    /// cumulated reward is reset. In case the task is terminating, InvokeTermination must be called, otherwise EndEpisode() s.t. only the episode
    /// of the task will be ended.
    /// episode 
    /// </summary>
    bool IsTerminatingTask { get; set; }

    /// <summary>
    /// The supervisor will ignore this task if this value is true. This value indicates that the task runs autonomously and will not be affected by
    /// the `IsActive` value controlled by the supervisor. This variable could be used for training purpose i.e. when the general task agent should
    /// be trained without the involvement of the supervisor
    /// </summary>.
    bool IsAutonomous { get; set; }

    /// <summary>
    /// Only used if TaskVisualQueue is used. If the value is true, the task is in a idle state and the supervisor will not consider the task.
    /// </summary>
    bool IsIdle { get; set; }

    /// <summary>
    /// Defines if Update and FixedUpdate are called
    /// </summary>
    bool IsPaused { get; set; }

    /// <summary>
    /// Only used if TaskVisualQueue is used. Priority of the task shown to the user. The higher the value, the higher the priority.
    /// </summary>
    int Priority { get; set; }

    /// <summary>
    /// Includes the timestamp of the last performed action of the task.
    /// </summary>
    float TimeLastPerformedAction { get; set; }

    /// <summary>
    /// Reward of the last episode. If this value was already polled or no episode was completed, 0 is returned.
    /// </summary>
    float RewardOfLastEpisode { get; set; }

    /// <summary>
    /// Priority of the last episode. If this value was already polled or no episode was completed, 0 is returned.
    /// </summary>
    public int PriorityLastEpisode { get; set; }

    /// <summary>
    /// Is only relevant if the Task implements concepts of CR. Specifies the elements of a task that the focus agent can concentrate on. These 
    /// elements should be defined within the editor. If there are no visual elements and the task's general focus needs to be determined, add the 
    /// agent game object as a single entry to the FocusStateSpace. The task's belief state should then be updated accordingly.
    /// </summary>
    VisualStateSpace FocusStateSpace { get; set; }

    /// <summary>
    /// The PixelVisionAgent ignores all objects with the tags defined in this list.
    /// </summary>
    List<string> IgnoredTagsForVision { get; }

    /// <summary>
    /// The PixelVisionAgent also returns objects located behind GameObjects whose tags are included in TransparentTagsForVision.
    /// </summary>
    List<string> TransparentTagsForVision { get; }

    /// <summary>
    /// Is only relevant if the Task implements concepts of CR. If true, the task does consider the focus agent to update its visibility state and 
    /// effect therefore how the belief state is updated.
    /// </summary>
    bool UseVisionAgent { get; set; }

    /// <summary>
    /// Specifies the data that is used to analyze the switching behavior of the user/agent. The ISwitchingData should be extended with state 
    /// information of the task to see e.g. in which condition the user/agent has left the task before switching to another task.
    /// </summary>
    IStateInformation StateInformation { get; set; }

    /// <summary>
    /// StateInformation at the end of the last episode.
    /// </summary>
    IStateInformation StateInformationOnEndEpisode { get; set; }

    /// <summary>
    /// The frequency with which the agent requests a decision. A DecisionPeriod of 5 means that the Agent will request a decision every 5 Academy
    /// steps. In contrast to other values related to CR, this value cannot be changed during runtime and stays fixed per training. Therefore, if 
    /// a agent was trained with a specific DecisionPeriod, the value cannot be changed afterwards.
    /// </summary>
    int DecisionPeriod { get; set; }

    /// <summary>
    /// Returns the reward of the task that should be used to calculate the reward of the supervisor agent and its associated priority.
    /// </summary>
    /// <returns> Reward </returns>
    Queue<(float, int)> TaskRewardForSupervisorAgent { get; }

    /// <summary>
    /// Returns the reward of the task that should be used to calculate the reward of the focus agent and its associated priority.
    /// </summary>
    /// <returns> Reward </returns>
    Queue<(float, int)> TaskRewardForFocusAgent { get; }

    /// <summary>
    /// Describes the current state of the environment. This information is shown in the VisualTaskQueue.
    /// </summary>
    string StateDescription { get; }

    /// <summary>
    /// Describes if the task was processed by the user. This information is used by the StaticVisualTaskQueue to decide if the task queue symbol
    /// containing this task can be used.
    /// </summary>
    bool IsTaskProcessed { get; set; }

    /// <summary>
    /// Is invoked if an episode of a task has ended. 
    /// </summary>
    public static event Action<ITask> OnEndEpisode;

    /// <summary>
    /// Will be invoked when the task is completed. This triggers the end of the episode of the supervisor agent if the calling Task 
    /// `IsTerminatingTask` and therefore of all other tasks. If the task is not a terminal task, the accumulated reward of the task
    /// is reset but the episode of the supervisor agent continues.
    /// </summary>
    public event Action<ITask, int> OnTermination;

    /// <summary>
    /// Must be invoked on action received. This information is used to analyze the switching behavior of the user/agent.
    /// </summary>
    delegate void OnActionReceivedAction(List<dynamic> performedActions, ITask task, double timeSinceLastSwitch = -1);
    static event OnActionReceivedAction OnAction;
    static void InvokeOnAction(List<dynamic> performedActions, ITask task, double timeSinceLastSwitch = -1) => OnAction?.Invoke(performedActions, task, timeSinceLastSwitch);

    /// <summary>
    /// Adds true state information to the sensor of the supervisor agent. Therefore, the sum over all states of the tasks will be the state space 
    ///  of the supervisor agent.
    /// </summary>
    /// <param name="sensor"> Sensor of the supervisor or any other agent working with the true space of the tasks</param>
    void AddTrueObservationsToSensor(VectorSensor sensor);

    /// <summary>
    /// The supervisor agent will call this method to update the difficulty level of the task. How the difficulty level is updated is task specific.
    /// </summary>
    void UpdateDifficultyLevel();

    /// <summary>
    /// Returns the Agent GameObject of the task.
    /// </summary>
    /// <returns> Agent GameObject </returns>
    GameObject GetGameObject();

    void EndEpisode();

    /// <summary>
    /// Returns the agent script implementing the ITask interface for the given GameObjects.
    /// </summary>
    /// <param name="TaskGameObjects"> Task prefabs</param>
    /// <returns> The agent script implementing the ITask interface</returns>
    static ITask[] GetTasksFromGameObjects(GameObject[] TaskGameObjects)
    {
        ITask[] tasks = new ITask[TaskGameObjects.Length];

        for (int i = 0; i < TaskGameObjects.Length; i++)
        {
            if (TaskGameObjects[i] != null)
            {
                tasks[i] = TaskGameObjects[i].transform.GetChildByName("Agent").GetComponent<ITask>();
            }
        }

        return tasks;
    }

    /// <summary>
    /// Dictionary of the task specific performance values of the current episode. This function is called by the PerformanceMeasurement class at the
    /// end of an episode of the supervisor and appends the values of the Dictionary as columns to the performance file for the current episode of 
    /// the supervisor. The task is responsible for continuously aggregating the values for the current episode of the supervisor. The key value is 
    /// used as the column name in the performance file.
    /// </summary>.
    Dictionary<string, double> AccumulatedPerformance { get; }

    /// <summary>
    /// Dictionary of the task specific performance values of the current episode. This function is called by the PerformanceMeasurement class at the
    /// end of an episode of task agent and appends the values of the Dictionary as columns to the performance file for the current episode of the
    /// task agent. The task is responsible for continuously aggregating the values for the current episode. The key value is used as the column name
    /// in the performance file. The values are reseted by the PerformanceMeasurement class at the end of the episode.
    /// </summary>.
    Dictionary<string, double> Performance { get; }

    /// <summary>
    /// Dictionary of the task specific notes of the current episode. This function is called by the PerformanceMeasurement class at the
    /// end of an episode of task agent and appends the values of the Dictionary as columns to the performance file for the current episode of the
    /// task agent. The key value is used as the column name in the performance file.
    /// </summary>.
    Dictionary<string, string> PerformanceNotes { get; }

    /// <summary>
    /// Dictionary of the environment specific notes of the current episode, e.g. information related to the visual task queue. The variable is 
    /// directly manipulated by the environmental components and is called by the PerformanceMeasurement class at the end of an episode of task 
    /// agent and appends the values of the Dictionary as columns to the performance file for the current episode of the task agent. The key value
    /// is used as the column name in the performance file.
    /// </summary>.
    Dictionary<string, string> EnvironmentNotes { get; set; }

    /// <summary>
    /// Resets the accumulated performance values of the task for the current episode. This function is called by the PerformanceMeasurement class at the end of
    /// an episode of the supervisor.
    /// </summary>
    void ResetAccumulatedPerformance();

    /// <summary>
    /// Provides the current input value like configured in the ProjectSetting component and should be implemented if the tasks need to be
    /// controllable on instance level.
    /// </summary>
    /// <param name="value"></param>
    public void OnMove(InputValue value);

    /// <summary>
    /// Returns the total reward of the last episode of the task. If this value was already polled or no episode was completed, 0 is returned.
    /// </summary>
    /// <returns> Total reward of last completed episode. </returns>
    public (float, int) PollRewardOfLastEpisode();
}

