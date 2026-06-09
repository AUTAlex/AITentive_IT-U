using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

public static class AgentExtensions
{
    /// <summary>
    /// Returns the BehaviorType of the agent.
    /// </summary>
    public static BehaviorType GetBehaviorType(this Agent agent)
    {
        var behavior = agent.GetComponent<BehaviorParameters>();

        if (behavior == null)
        {
            Debug.LogError($"No BehaviorParameters found on {agent.name}.");
            return BehaviorType.Default;
        }

        return behavior.BehaviorType;
    }
}
