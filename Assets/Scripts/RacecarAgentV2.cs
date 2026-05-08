using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RacecarAgentV2 : Agent
{
    [Header("Scene References")]
    [SerializeField] private PrometeoCarController _carController;
    [SerializeField] private Rigidbody _rb;

    [Header("Track")]
    [SerializeField] private Vector3 _carStartPosition = new Vector3(9.22f, 10.1f, 11.5f);
    [SerializeField] private Transform checkpointParent;
    private Transform[] checkpoints;

    [Header("Stuck Detection")]
    [SerializeField] private float minSpeedForStuckCheck = 2f;
    [SerializeField] private float stuckTimeThreshold = 3f;
    private float _stuckTimer = 0f;

    [Header("Reward Settings")]
    [SerializeField] private float checkpointReward = 1.0f;
    [SerializeField] private float progressRewardScale = 0.02f;
    [SerializeField] private float wallPenalty = -0.05f;

    private int _nextCheckpointIndex = 0;
    private float _prevDistance;

    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    public override void Initialize()
    {
        Debug.Log("Race Car Initializing..... VROOM VROOOOOOM");

        if (_carController == null)
            _carController = GetComponent<PrometeoCarController>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        CurrentEpisode = 0;
        CumulativeReward = 0f;

        // AUTO GET CHECKPOINTS
        checkpoints = new Transform[checkpointParent.childCount];

        for (int i = 0; i < checkpointParent.childCount; i++)
        {
            checkpoints[i] = checkpointParent.GetChild(i);
        }
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode has began.. type shi"); 
        CurrentEpisode++;
        CumulativeReward = 0f;

        ResetCar();

        _nextCheckpointIndex = 0;
        _prevDistance = DistanceToNextCheckpoint();
    }

    void ResetCar()
    {
        // Reset transform
        transform.SetLocalPositionAndRotation(_carStartPosition, Quaternion.Euler(0f, 40f, 0f));

        // Reset Rigidbody
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Reset Car Controller Internals
        _carController.ClearInputs();
        _carController.ResetCarState();
    }

    // =========================
    // OBSERVATIONS
    // =========================
    public override void CollectObservations(VectorSensor sensor)
    {
        Transform target = checkpoints[_nextCheckpointIndex];

        Vector3 localTarget = transform.InverseTransformPoint(target.position);
        Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);

        // Target direction - 1, 2
        sensor.AddObservation(localTarget.x / 20f);
        sensor.AddObservation(localTarget.z / 20f);

        // Velocity - 3, 4
        sensor.AddObservation(localVel.x / 20f);
        sensor.AddObservation(localVel.z / 20f);

        // Speed - 5
        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_carController.carSpeed) / Mathf.Max(1f, _carController.maxSpeed));
        sensor.AddObservation(normalizedSpeed);

        // Steering - 6
        float normalizedSteer = _carController.frontLeftCollider.steerAngle / _carController.maxSteeringAngle;
        sensor.AddObservation(normalizedSteer);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        // Branch 0: throttle
        // 0 = none, 1 = forward, 2 = reverse
        discreteActions[0] = 0;

        // Branch 1: steering
        // 0 = neutral, 1 = left, 2 = right
        discreteActions[1] = 0;

        if (Input.GetKey(KeyCode.W))
            discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.S))
            discreteActions[0] = 2;

        if (Input.GetKey(KeyCode.A))
            discreteActions[1] = 1;
        else if (Input.GetKey(KeyCode.D))
            discreteActions[1] = 2;
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        var discreteActions = actions.DiscreteActions;
        
        int throttle = actions.DiscreteActions[0];
        int steer = actions.DiscreteActions[1];

        ApplyActions(throttle, steer);

        AddReward(-1f / MaxStep); // step penalty

        // PROGRESS REWARD
        float currentDistance = DistanceToNextCheckpoint();
        float delta = _prevDistance - currentDistance;
        AddReward(delta * progressRewardScale);
        _prevDistance = currentDistance;

        // Direction toward next checkpoint
        Vector3 toCheckpoint =
            (checkpoints[_nextCheckpointIndex].position - transform.position).normalized;

        // Alignment between car forward and checkpoint direction
        float alignment = Vector3.Dot(transform.forward, toCheckpoint);

        // Reward facing correct direction
        AddReward(alignment * 0.001f);
        
        // STUCK DETECTION
        if (Mathf.Abs(_carController.carSpeed) < minSpeedForStuckCheck)
        {
            _stuckTimer += Time.fixedDeltaTime;

            if (_stuckTimer >= stuckTimeThreshold)
            {
                AddReward(-1f);
                EndEpisode();
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        // FLIP DETECTION
        float uprightDot = Vector3.Dot(transform.up, Vector3.up);

        if (uprightDot < 0.3f)
        {
            AddReward(-1f);
            EndEpisode();
        }

        // Update the cumulative reward after adding the step penalty
        CumulativeReward = GetCumulativeReward();
    }

    void ApplyActions(int throttle, int steer)
    {
        _carController.ClearInputs();

        // Throttle branch
        switch (throttle)
        {
            case 1:
                _carController.inputForward = true;
                break;
            case 2:
                _carController.inputReverse = true;
                break;
        }
        
        // Steering branch
        switch (steer)
        {
            case 1:
                _carController.inputLeft = true;
                break;
            case 2:
                _carController.inputRight = true;
                break;
        }
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

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Apply a small negative reward when the collision starts
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }
}