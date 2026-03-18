
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;

public class RacecarAgent : Agent
{
    [Header("Scene References")]
    [SerializeField] private PrometeoCarController _carController;
    [SerializeField] private Rigidbody _carRb;
    [SerializeField] private Transform _goal;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _carStartPosition = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector2 _goalDistanceRange = new Vector2(5f, 30f);

    [Header("Reward Settings")]
    //[SerializeField] private float goalReward = 1.0f;
    //[SerializeField] private float wallHitPenalty = -0.05f;
    //[SerializeField] private float wallStayPenaltyPerSecond = -0.01f;
    //[SerializeField] private float progressRewardScale = 0.01f;
    // [SerializeField] private float stepPenalty = -0.001f;
    /*
    [SerializeField] private float stuckSpeedThreshold = 0.75f;
    [SerializeField] private float stuckTimeLimit = 3f;
    [SerializeField] private float stuckPenalty = -0.25f;
    */

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
        //SpawnObjects();
    }

    /* Change this maybe, to spawn within a specific box, right now it randomly gets spawned in a random direction with a random length,  
    private void SpawnObjects()
    {
        transform.SetLocalPositionAndRotation(new Vector3(0f, 0.15f, 0f), Quaternion.identity);

        //Randomize the direction on the Y-axis (angle in degrees)
        float randomAngle = Random.Range(0f, 360f);
        Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;

        // Randomize the distance within the range [1, 2.5]
        float randomDistance = Random.Range(1f, 2.5f);

        // Calculate the goal's position
        Vector3 goalPosition = transform.localPosition + randomDirection * randomDistance;

        // Apply the calculated position to the goal
        _goal.localPosition = new Vector3(goalPosition.x, 0.3f, goalPosition.z);
    }
    */

    private void ResetCar()
    {
        // Reset transform
        transform.SetPositionAndRotation(_carStartPosition, Quaternion.identity);

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
        _goal.position = new Vector3(goalPosition.x, 0.3f, goalPosition.z);
    }



    //OLD Version
    /* 
    public override void CollectObservations(VectorSensor sensor)
    {
        // The goal's position
        float goalPosX_normalized = _goal.localPosition.x / 5f;
        float goalPosZ_normalized = _goal.localPosition.z / 5f;

        // The Racecar's Position
        float racecarPosX_normalized = transform.localPosition.x / 5f;
        float racecarPosZ_normalized = transform.localPosition.z / 5f;

        // The Racecar's direction (on the Y-axis)
        float racecarRotation_normalized = (transform.localRotation.eulerAngles.y / 360f) * 2f - 1f;

        sensor.AddObservation(goalPosX_normalized);
        sensor.AddObservation(goalPosZ_normalized);
        sensor.AddObservation(racecarPosX_normalized);
        sensor.AddObservation(racecarPosZ_normalized);
        sensor.AddObservation(racecarRotation_normalized);
    }
    */

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

        // Observations
        sensor.AddObservation(localGoal.x / 20f);           // 1
        sensor.AddObservation(localGoal.z / 20f);           // 2
        sensor.AddObservation(Mathf.Clamp01(distanceToGoal / 20f)); // 3
        sensor.AddObservation(localVelocity.x / 20f);       // 4
        sensor.AddObservation(localVelocity.z / 20f);       // 5
        sensor.AddObservation(normalizedSpeed);             // 6
        sensor.AddObservation(normalizedSteer);             // 7
    }

    // Old Heuristic
    /*
    // This needs to be changed and altered to allow for multiple discrete action branches lowkey
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Is this correct? I doubt
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        // Throttle
        discreteActionsOut[0] = 0; // No Gas/Brake
        
        // Steering
        discreteActionsOut[1] = 0; // No Steering

        // Throttle
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
        
        // Steering
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[1] = 1;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[1] = 2;
        }
    }
    */

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

    // Old
    /*
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Old layover code from turtle... One discrete action per step 
        // Move the agent using the action
        MoveAgent(actions.DiscreteActions);

        // Penalty given each step to encourage agent to finish the task quickly
        AddReward(-2f / MaxStep);

        // Update the cumulative reward after adding the step penalty
        CumulativeReward = GetCumulativeReward();
    }
    */


    public override void OnActionReceived(ActionBuffers actions)
    {
        var discreteActions = actions.DiscreteActions;

        int throttleAction = discreteActions[0];
        int steeringAction = discreteActions[1];

        ApplyActions(throttleAction, steeringAction);

        // Small step penalty
        AddReward(-2f / MaxStep);

        // I dont like any of this
        /*
        // Reward progress toward the goal
        float currentDistance = Vector3.Distance(transform.position, _goal.position);
        float distanceDelta = previousDistanceToGoal - currentDistance;
        AddReward(distanceDelta * progressRewardScale);
        previousDistanceToGoal = currentDistance;

        // Stuck penalty
        float speed = carRb.linearVelocity.magnitude;
        if (speed < stuckSpeedThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeLimit)
            {
                AddReward(stuckPenalty);
                EndEpisode();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        */

        // Update the cumulative reward after adding the step penalty
        CumulativeReward = GetCumulativeReward();
    }

    // Move Agent (New)
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

    /* OLD MOVE AGENT
    // This needs to be changed and altered to allow for multiple discrete action branches lowkey
    public void MoveAgent(ActionSegment<int> act)
    {
        var action = act[0];

        switch (action)
        {
            case 1: // move forward
                transform.position += transform.forward * _moveSpeed * Time.deltaTime;
                break;
            case 2: // Rotate Left
                transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f);
                break;
            case 3: // Rotate Right
                transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f);
                break;
        }
    }
    */

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
