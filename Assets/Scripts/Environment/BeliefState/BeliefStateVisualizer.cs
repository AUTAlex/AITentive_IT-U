using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using UnityEngine;

public class BeliefStateVisualizer : MonoBehaviour
{
    [field: SerializeField, Tooltip("Visualizes the current belief position of all relevant objects."), ProjectAssign]
    public bool ShowBeliefState { get; set; }

    [SerializeField]
    private Material GhostMaterial;


    [SerializeField]
    private float maxSmoothSpeed = Mathf.Infinity;

    private readonly float beliefStateSmoothTime = 0.01f;

    private Dictionary<IBelievableObject, Vector3> _beliefStateVelocities;

    private BeliefUpdater _beliefUpdater;

    private Dictionary<IBelievableObject, GameObject> _believableObjectVisualizations;

    private Transform _spawnContainer;


    private void Start()
    {
        _spawnContainer = gameObject.GetSpawnContainer().transform;
        _beliefUpdater = GetComponent<BeliefUpdater>();
        _believableObjectVisualizations = new();
        _beliefStateVelocities = new();
    }

    private void Update()
    {
        if (!ShowBeliefState)
        {
            return;
        }

        RemoveOutdatedBeliefStateVisualizations();
        UpdatenBeliefStateVisualizationPositions();
    }

    private void UpdatenBeliefStateVisualizationPositions()
    {
        foreach (RelativeBelievableObject2DSparse believableObject in _beliefUpdater.BelievableObjects.OfType<RelativeBelievableObject2DSparse>())
        {
            GameObject beliefStateVisualization = GetBeliefStateVisualization(believableObject);

            Vector2 beliefState = believableObject.GetBeliefState();

            Vector3 currentPosition = beliefStateVisualization.transform.localPosition;
            Vector3 targetPosition = new(beliefState.x, beliefStateVisualization.transform.localPosition.y, beliefState.y);

            Vector3 velocity = _beliefStateVelocities.GetValueOrDefault(believableObject);

            Vector3 smoothedPosition = Vector3.SmoothDamp(
                currentPosition,
                targetPosition,
                ref velocity,
                beliefStateSmoothTime,
                maxSmoothSpeed,
                Time.deltaTime
            );

            _beliefStateVelocities[believableObject] = velocity;
            beliefStateVisualization.transform.localPosition = smoothedPosition;
        }
    }

    private void RemoveOutdatedBeliefStateVisualizations()
    {
        List<IBelievableObject> removedBelievableObjects =
            _believableObjectVisualizations
                .Where(x => !_beliefUpdater.BelievableObjects.Contains(x.Key))
                .Select(x => x.Key)
                .ToList();

        foreach (RelativeBelievableObject2DSparse believableObject in removedBelievableObjects)
        {
            Destroy(_believableObjectVisualizations[believableObject]);

            _believableObjectVisualizations.Remove(believableObject);
            _beliefStateVelocities.Remove(believableObject);
        }
    }

    private GameObject GetBeliefStateVisualization(RelativeBelievableObject2DSparse believableObject)
    {
        GameObject beliefStateVisualization = _believableObjectVisualizations.GetValueOrDefault(believableObject);

        if (beliefStateVisualization == default)
        {
            beliefStateVisualization = GhostVisualizationUtil.InstantiateBeliefStateVisualization(believableObject.gameObject, GhostMaterial, _spawnContainer);
            _believableObjectVisualizations[believableObject] = beliefStateVisualization;

            return beliefStateVisualization;
        }

        return beliefStateVisualization;
    }
}
