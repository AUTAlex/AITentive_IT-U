using System;
using UnityEngine;

public class QuitDiagnostics : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        Application.wantsToQuit += OnWantsToQuit;
        Application.quitting += OnQuitting;

        Debug.LogError("QuitDiagnostics registered");
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        Application.quitting -= OnQuitting;

        Debug.LogError("QuitDiagnostics destroyed\n" + Environment.StackTrace);
    }

    private bool OnWantsToQuit()
    {
        Debug.LogError(
            "Application.wantsToQuit fired\n" +
            "time=" + Time.realtimeSinceStartup + "\n" +
            "isEditor=" + Application.isEditor + "\n" +
            Environment.StackTrace
        );

        return true;
    }

    private void OnQuitting()
    {
        Debug.LogError(
            "Application.quitting fired\n" +
            "time=" + Time.realtimeSinceStartup + "\n" +
            Environment.StackTrace
        );
    }
}