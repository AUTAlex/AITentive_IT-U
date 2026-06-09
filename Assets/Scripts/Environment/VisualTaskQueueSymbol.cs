using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VisualTaskQueueSymbol : MonoBehaviour
{
    public void SwitchToTask(GameObject gameObject)
    {
        VisualTaskQueue visualTaskQueue = gameObject.GetComponentsInParent<VisualTaskQueue>().Where(x => x.isActiveAndEnabled).First();
        visualTaskQueue.SwitchToTask(gameObject, Switcher.User);
    }
}
