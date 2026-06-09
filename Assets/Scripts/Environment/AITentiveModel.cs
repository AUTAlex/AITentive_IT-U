using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;


[CreateAssetMenu(fileName = "AITentiveModel", menuName = "AITentive/AITentiveModel")]
public class AITentiveModel : ScriptableObject
{
    public Unity.InferenceEngine.ModelAsset Model;

    public SupervisorSettings SupervisorSettings;

    public string Type;

    public int DecisionPeriod;


    public new Type GetType()
    {
        return Util.GetType(Type);
    }
}
