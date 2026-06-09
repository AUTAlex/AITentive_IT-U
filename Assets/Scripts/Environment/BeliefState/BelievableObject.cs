using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BelievableObject<TBeliefState> : MonoBehaviour, IBelievableObject
{
    public bool IsVisible { get; set; }

    public float EstimatedVelocity { get; protected set; } = 0;

    public event Action<IBelievableObject> Destructed;

    public BelievableObjectConfig BelievableObjectConfig { get; protected set; }

    public abstract TBeliefState GetBeliefState();

    public abstract double GetProbabilityForTrueState();

    public abstract bool IsBeliefStateEqualToTrueState();

    public abstract float Entropy01 { get; }

    public abstract void UpdateBeliefState(float updateTime);

    public abstract void InitBeliefState(System.Random rand, BelievableObjectConfig believableObjectConfig);


    protected virtual void OnDestroy()
    {
        Destructed?.Invoke(this);
    }
}