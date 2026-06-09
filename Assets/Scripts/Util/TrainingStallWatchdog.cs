using System;
using System.Collections;
using System.IO;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Profiling;

public class TrainingStallWatchdog : MonoBehaviour
{
    [Header("Log cadence")]
    [SerializeField] private float logEverySeconds = 10f;

    [Header("Stall detection")]
    [SerializeField] private float stallIfNoStepAdvanceForSeconds = 60f;

    [Header("Optional action")]
    [SerializeField] private bool writeReportToFile = true;

    long _lastStep;
    float _lastAdvanceTime;
    float _updateTimer;

    void OnEnable()
    {
        _lastStep = GetStepCountSafe();
        _lastAdvanceTime = Time.realtimeSinceStartup;
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return null;

            long step = GetStepCountSafe();
            float now = Time.realtimeSinceStartup;

            if (_updateTimer > logEverySeconds)
            {
                _updateTimer = 0;
                // Lightweight periodic telemetry
                Debug.Log(
                    $"[StallWatchdog] step={step} " +
                    $"sinceAdvance={(now - _lastAdvanceTime):F1}s " +
                    $"fps={(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-6f)):F1} " +
                    $"monoUsedMB={Profiler.GetMonoUsedSizeLong() / (1024f * 1024f):F1} " +
                    $"monoHeapMB={Profiler.GetMonoHeapSizeLong() / (1024f * 1024f):F1} " +
                    $"reservedMB={Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f):F1} " +
                    $"allocMB={Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1}"
                );
            }

            // Stall condition
            if (now - _lastAdvanceTime >= stallIfNoStepAdvanceForSeconds)
            {
                var report = BuildReport(step, now);
                Debug.LogError(report);

                if (writeReportToFile)
                    TryWriteReport(report);

                // NOTE: don’t auto-quit by default; you want evidence first.
                // If you *do* want auto-restart behavior, do it from the Python
                // launcher by killing & restarting env processes when no steps arrive.
            }

            _lastStep = step;
            _lastAdvanceTime = now;
        }
    }

    static long GetStepCountSafe()
    {
        try
        {
            return Academy.Instance != null ? Academy.Instance.StepCount : -1;
        }
        catch { return -2; }
    }

    string BuildReport(long step, float now)
    {
        return
            "========== ML-Agents Stall Report ==========\n" +
            $"timeRealtime={now:F2}\n" +
            $"academyStep={step}\n" +
            $"noAdvanceSeconds={(now - _lastAdvanceTime):F2}\n" +
            $"unityTimeScale={Time.timeScale}\n" +
            $"targetFrameRate={Application.targetFrameRate}\n" +
            $"graphicsDevice={SystemInfo.graphicsDeviceName}\n" +
            $"graphicsMemMB={SystemInfo.graphicsMemorySize}\n" +
            $"monoUsedMB={Profiler.GetMonoUsedSizeLong() / (1024f * 1024f):F1}\n" +
            $"monoHeapMB={Profiler.GetMonoHeapSizeLong() / (1024f * 1024f):F1}\n" +
            $"reservedMB={Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f):F1}\n" +
            $"allocMB={Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1}\n" +
            $"stack(main)={Environment.StackTrace}\n" +
            "===========================================\n";
    }

    private void FixedUpdate()
    {
        _updateTimer += Time.fixedDeltaTime;
    }

    void TryWriteReport(string report)
    {
        try
        {
            var dir = Path.Combine(Application.persistentDataPath, "stall-reports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir,
                $"stall_{DateTime.Now:yyyyMMdd_HHmmss}_{System.Diagnostics.Process.GetCurrentProcess().Id}.txt");
            File.WriteAllText(path, report);
        }
        catch (Exception e)
        {
            Debug.LogError($"[StallWatchdog] Failed to write report: {e}");
        }
    }
}