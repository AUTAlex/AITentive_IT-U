using UnityEngine;

/// <summary>
/// Members with this attribute will be manged, shown and assigned by the ProjectSettings component. Furthermore, Members with this attribute can be
/// configured with the help of a config file.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field)]
public class ProjectAssignAttribute : System.Attribute 
{
    [field: SerializeField]
    public string Header { get; set; }

    [field: SerializeField]
    public bool Hide { get; set; }
}