#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Diagnostics;
#endif

using System.Diagnostics;
using System;
using UnityEngine;
using System.Collections;

public class WindowTitleFromArgs : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool SetWindowText(IntPtr hWnd, string lpString);
#endif

    IEnumerator Start()
    {
        // wait until window is fully created
        yield return new WaitForSeconds(0.5f);

        string trialId = GetArg("--hpo_trial_id") ?? "unknown";
        int pid = Process.GetCurrentProcess().Id;

        string title = $"ML-Agents | Trial {trialId} | PID {pid}";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        SetTitleForThisProcess(title);
#endif
    }

    string GetArg(string key)
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var a in args)
            if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return a.Substring(key.Length + 1);
        return null;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    void SetTitleForThisProcess(string title)
    {
        uint myPid = (uint)Process.GetCurrentProcess().Id;

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == myPid)
            {
                SetWindowText(hWnd, title);
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);
    }
#endif
}
