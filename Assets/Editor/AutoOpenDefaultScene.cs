#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AutoOpenDefaultScene
{
    static AutoOpenDefaultScene()
    {
        // run once after domain reload
        EditorApplication.update += TryOpen;
    }

    static void TryOpen()
    {
        EditorApplication.update -= TryOpen;

        if (Application.isBatchMode) return; // don’t interfere with CI
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var active = EditorSceneManager.GetActiveScene();
        // Only intervene if we're on an untitled/new scene
        if (!active.IsValid() || string.IsNullOrEmpty(active.path))
        {
            // find the settings asset
            var guids = AssetDatabase.FindAssets("t:DefaultSceneSettings");
            if (guids.Length == 0) return;

            var settings = AssetDatabase.LoadAssetAtPath<DefaultSceneSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0])
            );

            if (settings != null && settings.scene != null)
            {
                var path = AssetDatabase.GetAssetPath(settings.scene);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }
            }
        }
    }
}
#endif
