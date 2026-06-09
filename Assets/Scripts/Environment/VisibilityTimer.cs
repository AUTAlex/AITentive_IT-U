using System.Linq;
using UnityEngine;

public class VisibilityTimer : EngagementTimer
{
    protected override void SetTasksStatus(ITask[] tasks, bool pause)
    {
        foreach (ICRTask task in tasks.OfType<ICRTask>())
        {
            if (task.IsVisible != !pause)
            {
                task.IsVisible = !pause;

                BeliefUpdater beliefUpdater = task.GetGameObject().GetComponent<BeliefUpdater>();

                if (beliefUpdater == null)
                {
                    continue;
                }

                foreach (IBelievableObject believableObject in beliefUpdater.BelievableObjects)
                {
                    believableObject.IsVisible = task.IsVisible;
                }
            }
        }
    }
}
