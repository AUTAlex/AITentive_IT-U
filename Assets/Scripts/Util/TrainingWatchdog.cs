using UnityEngine;
using Unity.MLAgents;
using System.Threading;
using System.Diagnostics;

public class TrainingWatchdog : MonoBehaviour
{
    [Tooltip("How many seconds of 'frozen' main thread before we kill the process?")]
    [SerializeField] private float timeoutSeconds = 600f;

    private long lastPulseTicks;
    private bool isRunning = false;
    private Thread watchdogThread;

    void Start()
    {
        // We don't start the thread immediately in Start() 
        // because the handshake with Python might take a few frames.
    }

    void Update()
    {
        // 1. Check if training is active
        if (!isRunning && Academy.Instance.IsCommunicatorOn)
        {
            UnityEngine.Debug.Log("ML-Agents Trainer detected. Starting Watchdog Thread...");
            StartWatchdog();
        }

        // 2. If running, update the heartbeat pulse
        if (isRunning)
        {
            Interlocked.Exchange(ref lastPulseTicks, System.DateTime.UtcNow.Ticks);
        }
    }

    private void StartWatchdog()
    {
        isRunning = true;

        // Set initial pulse
        Interlocked.Exchange(ref lastPulseTicks, System.DateTime.UtcNow.Ticks);

        watchdogThread = new Thread(WatchdogLoop);
        watchdogThread.IsBackground = true;
        watchdogThread.Start();
    }

    private void WatchdogLoop()
    {
        while (isRunning)
        {
            Thread.Sleep(5000); // Check every 5 seconds

            long currentPulse = Interlocked.Read(ref lastPulseTicks);
            System.TimeSpan timeSinceLastPulse = System.DateTime.UtcNow - new System.DateTime(currentPulse);

            if (timeSinceLastPulse.TotalSeconds > timeoutSeconds)
            {
                // MAIN THREAD IS HUNG
                UnityEngine.Debug.LogError($"WATCHDOG: Main thread hung for {timeSinceLastPulse.TotalSeconds}s. Killing process.");
                Process.GetCurrentProcess().Kill();
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
    }
}