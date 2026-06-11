using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ball2DAgent : Task
{
    [field: SerializeField]
    private BallBehavior _ball;

    [field: SerializeField]
    private FloorBehavior _floor;

    [field: SerializeField]
    private GameObject _platformGameObject;


    public override IStateInformation StateInformation
    {
        get
        {
            _Ball2DAgentStateInformation ??= new Ball2DAgentStateInformation();

            return _Ball2DAgentStateInformation;
        }
        set
        {
            _Ball2DAgentStateInformation = value as Ball2DAgentStateInformation;
        }
    }


    private float _forceMultiplier = 1f;

    private Ball2DAgentStateInformation _Ball2DAgentStateInformation;


    public override void OnEpisodeBegin()
    {
        if (_ball != null)
        {
            _ball.GetComponent<Rigidbody2D>().linearVelocity = new Vector2();
            _ball.transform.position = new Vector3(Random.Range(-4f, 4f), 0.2f, 0f);
            _platformGameObject.transform.rotation = Quaternion.identity;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Target and Agent positions
        sensor.AddObservation(_platformGameObject.transform.position-_ball.transform.position);
        sensor.AddObservation(_ball.MyRb.linearVelocity);
        sensor.AddObservation(_platformGameObject.transform.rotation.z);
    }

    public override void OnActionReceivedInternal(ActionBuffers actionBuffers)
    {
        // Actions, size = 2
        int rotation = actionBuffers.DiscreteActions[0];
        if (rotation == 0)
        {
            _platformGameObject.transform.Rotate(Vector3.forward, 1f * _forceMultiplier);
        }
        else
        {
            _platformGameObject.transform.Rotate(Vector3.forward, -1f * _forceMultiplier);
        }

        // Rewards
        AddReward(0.1f);
    }

    public override void AddTrueObservationsToSensor(VectorSensor sensor) { }


    // Update is called once per frame
    protected override void OnFixedUpdate()
    {

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _platformGameObject.transform.Rotate(Vector3.forward, 1f);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            _platformGameObject.transform.Rotate(Vector3.forward, -1f);
        }
    }


    // Start is called before the first frame update
    private void Start()
    {
        if (_floor != null)
        {
            _floor.OnGameOver += FloorOnGameOver;
        }
    }

    private void FloorOnGameOver()
    {
        EndEpisode();
    }
}
