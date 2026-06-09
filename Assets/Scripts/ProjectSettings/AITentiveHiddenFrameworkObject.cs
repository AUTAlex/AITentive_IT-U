using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class AITentiveHiddenFrameworkObject : MonoBehaviour
{
    [SerializeField]
    private bool _hideInHierarchy = true;

    public bool HideInHierarchy => _hideInHierarchy;

    private void OnEnable()
    {
        ApplyHideState();
    }

    private void OnValidate()
    {
        ApplyHideState();
    }

    public void SetHiddenInHierarchy(bool hidden)
    {
        _hideInHierarchy = hidden;
        ApplyHideState();
    }

    private void ApplyHideState()
    {
        // Cleanup from the older version that used HideFlags.NotEditable.
        gameObject.hideFlags &= ~HideFlags.NotEditable;

        if (_hideInHierarchy)
        {
            gameObject.hideFlags |= HideFlags.HideInHierarchy;
        }
        else
        {
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorApplication.RepaintHierarchyWindow();
#endif
    }
}