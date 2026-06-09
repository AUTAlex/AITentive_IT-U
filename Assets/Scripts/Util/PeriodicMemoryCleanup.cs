using System.Collections;
using UnityEngine;

public class PeriodicMemoryCleanup : MonoBehaviour
{
    [SerializeField] private float intervalSeconds = 120f; // every 2 minutes

    void Start()
    {
        StartCoroutine(CleanupLoop());
    }

    IEnumerator CleanupLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(intervalSeconds);

            Debug.Log("[MemoryCleanup] Running GC + UnloadUnusedAssets");

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }
}