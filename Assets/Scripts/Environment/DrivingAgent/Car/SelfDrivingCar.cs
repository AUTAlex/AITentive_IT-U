using UnityEngine;
using Utils;

public class SelfDrivingCar : MonoBehaviour
{
    [field: SerializeField, Header("Settings")]
    public Lane TargetLane { get; set; } = Lane.Center;

    [field: SerializeField]
    public float MaximumSpeed { get; set; } = 100f;


    [Header("Current Inputs")]
    [SerializeField] private float _steerInput = 0f;
    [SerializeField] private float _throttleInput = 0f;
    [SerializeField] private float _brakeInput = 0f;
    [SerializeField] private float _handbrakeInput = 0f;

    [Header("Speed Limit")]
    [SerializeField] private bool _limitSpeed = true;

    [Header("Steering")]
    [SerializeField] private bool _smoothedSteer = true;

    [Header("Detection")]
    [SerializeField] private LayerMask _layerMask;

    [Header("Realistic Car Controller")]
    private RCC_CarControllerV3 _carController;

    private const float LookAheadDistance = 40f;
    private const float FullSteerAngle = 45f;

    private const float VehicleDetectionDistance = 50f;
    private const float EmergencyBrakeDistance = 20f;
    private const float SideRayOffset = 1.5f;

    private const float StopSpeed = 5f;

    private void OnEnable()
    {
        _carController = GetComponent<RCC_CarControllerV3>();

        if (_carController != null)
            _carController.externalController = true;
    }

    private void Update()
    {
        if (transform.position.y < -5f)
            Destroy(gameObject);

        if (_carController == null)
            return;

        if (!_limitSpeed)
            MaximumSpeed = _carController.maxspeed;
    }

    private void FixedUpdate()
    {
        if (_carController == null || !_carController.canControl)
            return;

        ResetInputs();

        _throttleInput = CalculateThrottle();
        _steerInput = CalculateSteering() * _carController.direction;

        CheckForVehicleInFront();

        ClampInputs();
        FeedRCC();
    }

    private void ResetInputs()
    {
        _steerInput = 0f;
        _throttleInput = 0f;
        _brakeInput = 0f;
        _handbrakeInput = 0f;
    }

    private float CalculateThrottle()
    {
        if (MaximumSpeed <= 0f)
            return 0f;

        float speedRatio = Mathf.Clamp01(_carController.speed / MaximumSpeed);

        // At 0% speed: full throttle.
        // At 100% speed: no throttle.
        return 1f - speedRatio;
    }

    private float CalculateSteering()
    {
        float targetLaneX = DrivingUtil.GetXLocationForLane(TargetLane, gameObject);

        Vector3 carPosition = transform.position;

        // Road is assumed to be straight along world Z.
        Vector3 targetPosition = new Vector3(
            targetLaneX,
            carPosition.y,
            carPosition.z + LookAheadDistance
        );

        Vector3 directionToTarget = targetPosition - carPosition;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude < 0.001f)
            return 0f;

        directionToTarget.Normalize();

        Debug.DrawRay(carPosition, transform.forward * LookAheadDistance, Color.yellow);
        Debug.DrawRay(carPosition, directionToTarget * LookAheadDistance, Color.blue);

        float signedAngle = Vector3.SignedAngle(
            transform.forward,
            directionToTarget,
            Vector3.up
        );

        float steer = signedAngle / FullSteerAngle;

        return Mathf.Clamp(steer, -1f, 1f);
    }

    private void CheckForVehicleInFront()
    {
        Vector3[] raycastStarts = GetRaycastStartPositions();

        foreach (Vector3 raycastStart in raycastStarts)
        {
            Debug.DrawRay(
                raycastStart,
                transform.forward * VehicleDetectionDistance,
                Color.red
            );

            if (!Physics.Raycast(
                    raycastStart,
                    transform.forward,
                    out RaycastHit hit,
                    VehicleDetectionDistance,
                    _layerMask))
            {
                continue;
            }

            if (!IsVehicle(hit.collider))
                continue;

            RCC_CarControllerV3 targetCarController =
                hit.collider.GetComponentInParent<RCC_CarControllerV3>();

            if (targetCarController == null)
                continue;

            if (_carController.speed <= targetCarController.speed)
                continue;

            Brake();

            if (hit.distance < EmergencyBrakeDistance)
                EmergencyBrake();
        }
    }

    private Vector3[] GetRaycastStartPositions()
    {
        Vector3 center = transform.position;
        Vector3 left = center - transform.right * SideRayOffset;
        Vector3 right = center + transform.right * SideRayOffset;

        return new Vector3[]
        {
            center,
            left,
            right
        };
    }

    private bool IsVehicle(Collider collider)
    {
        return collider.CompareTag("PlayerCar")
            || collider.CompareTag("AgentCar")
            || collider.CompareTag("SelfDriving");
    }

    private void Brake()
    {
        _throttleInput = 0f;
        _brakeInput = 1f;
    }

    private void EmergencyBrake()
    {
        _throttleInput = 0f;
        _steerInput = 0f;
        _brakeInput = 1f;

        // Only use the handbrake when the car is already nearly stopped.
        if (_carController.speed < StopSpeed)
        {
            _brakeInput = 0f;
            _handbrakeInput = 1f;
        }
    }

    private void ClampInputs()
    {
        _steerInput = Mathf.Clamp(_steerInput, -1f, 1f);
        _throttleInput = Mathf.Clamp01(_throttleInput);
        _brakeInput = Mathf.Clamp01(_brakeInput);
        _handbrakeInput = Mathf.Clamp01(_handbrakeInput);
    }

    private void FeedRCC()
    {
        if (!_carController.changingGear && !_carController.cutGas)
        {
            _carController.throttleInput =
                _carController.direction == 1
                    ? _throttleInput
                    : _brakeInput;

            _carController.brakeInput =
                _carController.direction == 1
                    ? _brakeInput
                    : _throttleInput;
        }
        else
        {
            _carController.throttleInput = 0f;
            _carController.brakeInput = 0f;
        }

        if (_smoothedSteer)
        {
            _carController.steerInput = Mathf.Lerp(
                _carController.steerInput,
                _steerInput,
                Time.fixedDeltaTime * 5f
            );
        }
        else
        {
            _carController.steerInput = _steerInput;
        }

        _carController.handbrakeInput = _handbrakeInput;
    }

    private void OnDisable()
    {
        if (_carController != null)
            _carController.externalController = false;
    }
}