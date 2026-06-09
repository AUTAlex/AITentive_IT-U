using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Project/Default Scene Settings")]
public class DefaultSceneSettings : ScriptableObject
{
    public SceneAsset scene;
}