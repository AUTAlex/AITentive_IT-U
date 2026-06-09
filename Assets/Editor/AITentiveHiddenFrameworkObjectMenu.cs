using UnityEditor;
using UnityEngine;

public static class AITentiveHiddenFrameworkObjectMenu
{
    [MenuItem("Tools/AITentive/Hide Framework Objects")]
    private static void HideFrameworkObjects()
    {
        SetFrameworkObjectsHidden(true);
    }

    [MenuItem("Tools/AITentive/Reveal Framework Objects")]
    private static void RevealFrameworkObjects()
    {
        SetFrameworkObjectsHidden(false);
    }

    private static void SetFrameworkObjectsHidden(bool hidden)
    {
        AITentiveHiddenFrameworkObject[] hiddenObjects =
            Resources.FindObjectsOfTypeAll<AITentiveHiddenFrameworkObject>();

        foreach (AITentiveHiddenFrameworkObject hiddenObject in hiddenObjects)
        {
            if (hiddenObject == null)
            {
                continue;
            }

            Undo.RecordObject(hiddenObject, hidden ? "Hide Framework Object" : "Reveal Framework Object");
            Undo.RecordObject(hiddenObject.gameObject, hidden ? "Hide Framework Object" : "Reveal Framework Object");

            hiddenObject.SetHiddenInHierarchy(hidden);

            EditorUtility.SetDirty(hiddenObject);
            EditorUtility.SetDirty(hiddenObject.gameObject);
        }

        EditorApplication.RepaintHierarchyWindow();
    }
}