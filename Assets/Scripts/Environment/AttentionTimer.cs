using Newtonsoft.Json;
using Supervisor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


public class AttentionTimer : EngagementTimer
{
    [field: SerializeField, Tooltip("Pause only vision support instead of the whole task."), ProjectAssign]
    public bool PauseOnlyVision { get; set; }


    protected override void SetTasksStatus(ITask[] tasks, bool pause)
    {
        if (!PauseOnlyVision)
        {
            foreach (ITask task in tasks)
            {
                if ((task.IsPaused && !pause) || (!task.IsPaused && pause))
                {
                    task.IsPaused = pause;
                }
            }
        }

        VisionAgents.ForEach(x => x.IsPaused = pause);
    }
}