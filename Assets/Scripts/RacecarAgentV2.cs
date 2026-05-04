using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RacecarAgentV2 : Agent
{
    [Header("References")]
    [SerializeField] private PrometeoCarController _car;
    [SerializeField] private Rigidbody _rb;

    [Header("Track")]
    [SerializeField] private Transform[] checkpoints;

    [Header("Ray Perception")]
    [SerializeField] private int rayCount = 7;
    [SerializeField] private float rayAngle = 90f; // total spread
    [SerializeField] private float rayDistance = 20f;
    [SerializeField] private LayerMask rayMask;

    [Header("Rewards")]
    [SerializeField] private float checkpointReward = 1.0f;
    [SerializeField] private float progressRewardScale = 0.02f;
    [SerializeField] private float wallPenalty = -0.05f;

    private int _nextCheckpointIndex = 0;
    private float _prevDistance;

    public override void Initialize()
    {
        if (_car == null) _car = GetComponent<PrometeoCarController>();
        if (_rb == null) _rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        ResetCar();

        _nextCheckpointIndex = 0;
        _prevDistance = DistanceToNextCheckpoint();
    }

    void ResetCar()
    {
        transform.position = checkpoints[0].position + Vector3.up * 0.5f;
        transform.rotation = checkpoints[0].rotation;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        _car.ClearInputs();
        _car.ResetCarState();
    }

    // =========================
    // OBSERVATIONS
    // =========================
    public override void CollectObservations(VectorSensor sensor)
    {
        Transform target = checkpoints[_nextCheckpointIndex];

        Vector3 localTarget = transform.InverseTransformPoint(target.position);
        Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);

        // Target direction
        sensor.AddObservation(localTarget.x / 20f);
        sensor.AddObservation(localTarget.z / 20f);

        // Distance
        sensor.AddObservation(localTarget.magnitude / 20f);

        // Velocity
        sensor.AddObservation(localVel.x / 20f);
        sensor.AddObservation(localVel.z / 20f);

        // Speed
        float speed = Mathf.Abs(_car.carSpeed) / Mathf.Max(1f, _car.maxSpeed);
        sensor.AddObservation(speed);

        // Steering
        float steer = _car.frontLeftCollider.steerAngle / _car.maxSteeringAngle;
        sensor.AddObservation(steer);

        // Rotation
        float rot = (transform.eulerAngles.y / 360f) * 2f - 1f;
        sensor.AddObservation(rot);

        // 🔥 RAYS (this is huge)
        CastRays(sensor);
    }

    void CastRays(VectorSensor sensor)
    {
        float step = rayAngle / (rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -rayAngle / 2f + step * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, rayDistance, rayMask))
            {
                sensor.AddObservation(hit.distance / rayDistance);
            }
            else
            {
                sensor.AddObservation(1f);
            }
        }
    }

    // =========================
    // ACTIONS
    // =========================
    public override void OnActionReceived(ActionBuffers actions)
    {
        int throttle = actions.DiscreteActions[0];
        int steer = actions.DiscreteActions[1];

        ApplyActions(throttle, steer);

        AddReward(-1f / MaxStep); // step penalty

        // 🔥 PROGRESS REWARD
        float dist = DistanceToNextCheckpoint();
        float delta = _prevDistance - dist;
        AddReward(delta * progressRewardScale);
        _prevDistance = dist;
    }

    void ApplyActions(int throttle, int steer)
    {
        _car.ClearInputs();

        if (throttle == 1) _car.inputForward = true;
        if (throttle == 2) _car.inputReverse = true;

        if (steer == 1) _car.inputLeft = true;
        if (steer == 2) _car.inputRight = true;
    }

    // =========================
    // CHECKPOINT LOGIC
    // =========================
    float DistanceToNextCheckpoint()
    {
        return Vector3.Distance(transform.position, checkpoints[_nextCheckpointIndex].position);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == checkpoints[_nextCheckpointIndex])
        {
            AddReward(checkpointReward);

            _nextCheckpointIndex = (_nextCheckpointIndex + 1) % checkpoints.Length;
        }
    }

    // =========================
    // COLLISIONS
    // =========================
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(wallPenalty);
        }
    }
}