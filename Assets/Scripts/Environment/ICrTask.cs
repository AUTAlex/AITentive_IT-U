using System;
using Unity.MLAgents.Sensors;


public interface ICRTask : ITask
{
    /// <summary>
    /// Adds observations that are perceived by the agent (e.g. the belief state) to the sensor of the focus agent. This should reflect the agent's
    /// uncertainty about the environment and the task.
    /// </summary>
    /// <param name="sensor"> Sensor of the focus agent or any other agent working with the belief state of the tasks </param>
    void AddBeliefObservationsToSensor(VectorSensor sensor)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Indicates whether the task agent perceives the environment, resulting in a corresponding update to its belief state.
    /// </summary>
    public bool IsVisible { get; set; }
}
