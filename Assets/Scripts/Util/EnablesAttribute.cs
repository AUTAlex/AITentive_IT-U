using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows to enable or disable a component based on the value of the field with this attribute. When the value of the field marked with this 
/// attribute is true (in case of a bool value) or has the value of EnabledForValue, the respective component will be enabled and therefore also 
/// shown in the inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class EnablesAttribute : System.Attribute
{
    [field: SerializeField]
    public Type ComponentType { get; set; }

    [field: SerializeField]
    public string EnabledForValue { get; set; }
}
