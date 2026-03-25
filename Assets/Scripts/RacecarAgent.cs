
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
//using System.Collections;

public class RacecarAgent : Agent
{
    [Header("Scene References")]
    [SerializeField] private PrometeoCarController _carController;
    [SerializeField] private Rigidbody _carRb;
    [SerializeField] private Transform _goal;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _carStartPosition = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private Vector2 _goalDistanceRange = new Vector2(5f, 30f);

    [Header("Reward Settings")]
    //[SerializeField] private float goalReward = 1.0f;
    //[SerializeField] private float wallHitPenalty = -0.05f;
    //[SerializeField] private float wallStayPenaltyPerSecond = -0.01f;
    [SerializeField] private float progressRewardScale = 0.01f;
    // [SerializeField] private float stepPenalty = -0.001f;
    private float _previousDistanceToGoal;


    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    public override void Initialize()
    {
        Debug.Log("Race Car Initializing..... VROOM VROOOOOOM");

        if (_carController == null)
            _carController = GetComponent<PrometeoCarController>();

        if (_carRb == null)
            _carRb = GetComponent<Rigidbody>();

        CurrentEpisode = 0;
        CumulativeReward = 0f;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode has began.. type shi"); 

        CurrentEpisode++;
        CumulativeReward = 0f;

        ResetCar();
        SpawnGoal();

        _previousDistanceToGoal = Vector3.Distance(transform.position, _goal.position);

    }

    private void ResetCar()
    {
        // Reset transform
        transform.SetLocalPositionAndRotation(_carStartPosition, Quaternion.identity);

        // Reset rigidbody
        if (_carRb != null)
        {
            _carRb.linearVelocity = Vector3.zero;
            _carRb.angularVelocity = Vector3.zero;
        }

        // Reset car controller internals if method exists
        if (_carController != null)
        {
            _carController.ClearInputs();
            _carController.ResetCarState();
        }
    }

    private void SpawnGoal()
    {
        float randomAngle = Random.Range(0f, 360f);
        Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;

        float randomDistance = Random.Range(_goalDistanceRange.x, _goalDistanceRange.y);

        Vector3 goalPosition = _carStartPosition + randomDirection * randomDistance;
        _goal.localPosition = new Vector3(goalPosition.x, 1f, goalPosition.z);
    }

    // New version
    public override void CollectObservations(VectorSensor sensor)
    {
        // Goal relative to car, in local space
        Vector3 localGoal = transform.InverseTransformPoint(_goal.position);

        // Local velocity of the car
        Vector3 localVelocity = transform.InverseTransformDirection(_carRb.linearVelocity);

        // Distance to goal
        float distanceToGoal = localGoal.magnitude;

        // Speed normalized
        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_carController.carSpeed) / Mathf.Max(1f, _carController.maxSpeed));

        // Steering normalized from current wheel steer angle
        float normalizedSteer = 0f;
        if (_carController.frontLeftCollider != null && _carController.maxSteeringAngle > 0)
        {
            normalizedSteer = _carController.frontLeftCollider.steerAngle / _carController.maxSteeringAngle;
        }

        // The Turtle's direction (on the Y-axis)
        float normalizedRotation = (transform.localRotation.eulerAngles.y / 360f) * 2f - 1f;

        // Observations
        sensor.AddObservation(localGoal.x / 20f);           // 1
        sensor.AddObservation(localGoal.z / 20f);           // 2
        sensor.AddObservation(Mathf.Clamp01(distanceToGoal / 20f)); // 3
        sensor.AddObservation(localVelocity.x / 20f);       // 4
        sensor.AddObservation(localVelocity.z / 20f);       // 5
        sensor.AddObservation(normalizedSpeed);             // 6
        sensor.AddObservation(normalizedSteer);             // 7
        sensor.AddObservation(normalizedRotation);          // 8
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

        int throttleAction = discreteActions[0];
        int steeringAction = discreteActions[1];

        ApplyActions(throttleAction, steeringAction);

        // Small step penalty
        AddReward(-2f / MaxStep);

        // Reward progress toward the goal
        float currentDistance = Vector3.Distance(transform.position, _goal.position);
        float distanceDelta = _previousDistanceToGoal - currentDistance;
        AddReward(distanceDelta * progressRewardScale);
        _previousDistanceToGoal = currentDistance;


        // Update the cumulative reward after adding the step penalty
        CumulativeReward = GetCumulativeReward();
    }

    private void ApplyActions(int throttleAction, int steeringAction)
    {
        _carController.ClearInputs();

        // Throttle branch
        switch (throttleAction)
        {
            case 1:
                _carController.inputForward = true;
                break;
            case 2:
                _carController.inputReverse = true;
                break;
        }

        // Steering branch
        switch (steeringAction)
        {
            case 1:
                _carController.inputLeft = true;
                break;
            case 2:
                _carController.inputRight = true;
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // If found goal - Give Reward and End Episode
        if (other.gameObject.CompareTag("Goal"))
        {
            GoalReached();
        }
    }

    private void GoalReached()
    {
        AddReward(1.0f); // Large reward for reaching the goal
        CumulativeReward = GetCumulativeReward();

        EndEpisode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Apply a small negative reward when the collision starts
            AddReward(-0.05f);
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
