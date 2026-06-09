using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows to hide a field based on the value of another field.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class HiddenAttribute : System.Attribute
{
    /// <summary>
    /// When the field of the FieldName is false or has the value of HiddenForValue, the field with this attribute will not be shown in the 
    /// drop-down-menu in the project settings.
    /// </summary>
    [field: SerializeField]
    public string FieldName { get; set; }

    [field: SerializeField]
    public string HiddenForValue { get; set; }
}
