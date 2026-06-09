using UnityEditor;
using UnityEngine;

public class AITentiveProjectSettingsWindow : EditorWindow
{
    private const float LeftIndent = 8f;
    private const float ScrollbarWidthAllowance = 18f;

    private ProjectSettings _projectSettings;
    private Editor _cachedEditor;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/AITentive/Project Settings")]
    public static void Open()
    {
        GetWindow<AITentiveProjectSettingsWindow>("AITentive Project Settings");
    }

    private void OnEnable()
    {
        FindProjectSettings();
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;

        if (_cachedEditor != null)
        {
            DestroyImmediate(_cachedEditor);
            _cachedEditor = null;
        }
    }

    private void OnHierarchyChanged()
    {
        if (_projectSettings == null)
        {
            FindProjectSettings();
        }

        Repaint();
    }

    private void FindProjectSettings()
    {
        _projectSettings =
            Object.FindFirstObjectByType<ProjectSettings>(FindObjectsInactive.Include);

        if (_cachedEditor != null)
        {
            DestroyImmediate(_cachedEditor);
            _cachedEditor = null;
        }
    }

    private void OnGUI()
    {
        if (_projectSettings == null)
        {
            EditorGUILayout.HelpBox(
                "No ProjectSettings component found in the loaded scene.",
                MessageType.Warning
            );

            if (GUILayout.Button("Search Again"))
            {
                FindProjectSettings();
            }

            return;
        }

        Editor.CreateCachedEditor(
            _projectSettings,
            typeof(ProjectSettingsInspector),
            ref _cachedEditor
        );

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(LeftIndent);
        EditorGUILayout.BeginVertical();

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        bool previousWideMode = EditorGUIUtility.wideMode;

        try
        {
            EditorGUIUtility.wideMode = true;

            float availableWidth =
                EditorGUIUtility.currentViewWidth
                - LeftIndent
                - ScrollbarWidthAllowance;

            EditorGUIUtility.labelWidth = Mathf.Clamp(
                availableWidth * 0.50f,
                220f,
                420f
            );

            _cachedEditor.OnInspectorGUI();
        }
        finally
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUIUtility.wideMode = previousWideMode;

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }
    }
}