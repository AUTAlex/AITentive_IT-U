using Parlot.Fluent;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class BeliefUpdater : MonoBehaviour
{
    [field: SerializeField, ProjectAssign]
    public BelievableObjectConfig BelievableObjectConfig { get; set; }

    public List<IBelievableObject> BelievableObjects { private set; get; }

    public event Action<IBelievableObject> ObjectAdded;


    private float _updateTimer = 0.0f;

    private System.Random _rand;


    public TBelievableObject RegisterBelievableObject<TBelievableObject>(GameObject gameObject, BelievableObjectConfig believableObjectConfig = null) where TBelievableObject : MonoBehaviour, IBelievableObject
    {
        Initialize();
        TBelievableObject believableObject = gameObject.AddComponent<TBelievableObject>();
        BelievableObjects.Add(believableObject);
        believableObjectConfig = believableObjectConfig == null ? new(BelievableObjectConfig.UpdatePeriod, BelievableObjectConfig.NumberOfSamples, BelievableObjectConfig.Sigma, BelievableObjectConfig.SigmaMean, BelievableObjectConfig.UseEstimatedVelocity, BelievableObjectConfig.UseSampleVelocity, BelievableObjectConfig.ObservationProbability, BelievableObjectConfig.RangeMin, BelievableObjectConfig.RangeMax, BelievableObjectConfig.NumberOfBins) : believableObjectConfig;
        believableObject.Destructed += OnBelievableObjectDestructed;
        ObjectAdded?.Invoke(believableObject);

        believableObject.InitBeliefState(_rand, believableObjectConfig);

        return believableObject;
    }

    public void UnRegisterBelievableObject(GameObject gameObject)
    {
        IBelievableObject believableObject = gameObject.GetComponent<IBelievableObject>();
        BelievableObjects.Remove(believableObject);
    }

    public void UnRegisterBelievableObject(IBelievableObject believableObject)
    {
        BelievableObjects.Remove(believableObject);
    }


    private void UpdateBeliefState()
    {
        _updateTimer += Time.fixedDeltaTime;

        if (BelievableObjectConfig.UpdatePeriod < _updateTimer)
        {
            float updateTime = _updateTimer;
            _updateTimer = 0;

            foreach (IBelievableObject believableObject in BelievableObjects.ToArray())
            {
                believableObject.UpdateBeliefState(updateTime);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!BelievableObjects.IsNullOrEmpty()) 
        {
            UpdateBeliefState();
        }
    }

    private void Initialize()
    {
        _rand = _rand == null ? new System.Random() : _rand;
        BelievableObjects = BelievableObjects == null ? new() : BelievableObjects;
    }

    private void OnBelievableObjectDestructed(IBelievableObject believableObject)
    {
        UnRegisterBelievableObject(believableObject);
        believableObject.Destructed -= OnBelievableObjectDestructed;
    }
}
