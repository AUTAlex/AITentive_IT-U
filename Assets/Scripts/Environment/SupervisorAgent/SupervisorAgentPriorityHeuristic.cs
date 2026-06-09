using Supervisor;
using Unity.MLAgents.Actuators;
using UnityEngine;

namespace Supervisor
{
    public class SupervisorAgentPriorityHeuristic : SupervisorAgent
    {
        [field: SerializeField, Tooltip("Minimum required difference in priority for a new task to preempt the current one. If the current task has priority p, the agent will only switch to a new task with priority >= p + prioritySwitchThreshold"), ProjectAssign]
        public int PrioritySwitchThreshold { get; set; } = 2;


        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            int action = UsesHeuristic ? actionBuffers.DiscreteActions[0] : GetTaskForPriority();

            Act(action);
            ResolveInteraction(action);
        }


        private int GetTaskForPriority()
        {
            return GetActiveTask() == null || GetActiveTask().Priority + PrioritySwitchThreshold <= Tasks[GetTaskNumberWithHighestPriority()].Priority ? GetTaskNumberWithHighestPriority() : GetActiveTaskNumber();
        }

        private int GetTaskNumberWithHighestPriority()
        {
            int priority = 0;
            ITask result = null;

            foreach (ITask task in Tasks)
            {
                if (task.Priority > priority)
                {
                    priority = task.Priority;
                    result = task;
                }
            }

            return GetTaskNumber(result);
        }

    }
}
