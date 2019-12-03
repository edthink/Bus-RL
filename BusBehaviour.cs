using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using MLAgents;

public class BusBehaviour : Agent
{
    // *** Bus data ***
    public int busIdx; // defined in Unity
    public int passengersBoarded = 0;
    public int loading = 0;

    // *** Physics ***
    Rigidbody rBody;
    // TODO: Scale up to n agents
    public GameObject otherBus; // defined in Unity
    private Rigidbody otherBusRB;
    public float moveSpeed; // defined in Unity
    private float maxSpeed = 2f;
    private float minSpeed = 0.5f;

    // *** Navigation ***    
    NavMeshAgent navAgent;
    private string[] stopOrder = new string[] { "Stop 1", "Stop 2", "Stop 3", "Stop 4"}; 
    public int nextStopIdx; // defined in Unity
    public string currentStop;
    private string previousStop;
    private float prevStopDist;

    // *** Rewards ***
    private float rwdPsxPickup = 0.05f; // Passenger pickup
    private float rwdPrevStop = -1.0f; // Returning to previous stop
    private float rwdCircuit = 0.25f; // Completing a circuit
    private float rwdDistance = 5.0f; // Maintaining distance
    private float rwdAction = -0.0001f; // Action penalty
    private float rwdEachStop = 0.1f; // Reaching each stop
    private float rwdAwayFromGoal = -1.0f; // Going away from goal


    public override void InitializeAgent()
    {
        base.InitializeAgent();
        rBody = GetComponent<Rigidbody>();
        otherBusRB = otherBus.GetComponent<Rigidbody>();
        
        // find stop by name based on currnet index, then update index
        navAgent = GetComponent<NavMeshAgent>();
        currentStop = stopOrder[nextStopIdx];
        navAgent.destination = GameObject.Find(currentStop).transform.position;
        navAgent.SetDestination(navAgent.destination);
        prevStopDist = Vector3.Distance(navAgent.destination, this.transform.position);
        nextStopIdx++;
	}

    public override void AgentReset()
    {
        this.rBody.velocity = Vector3.zero;
        this.rBody.angularVelocity = Vector3.zero;
    }

    // New required function for ML-Agents
    public override float[] Heuristic() {

        if (Input.GetKey(KeyCode.D))
        {
            return new float[] { 1 };
        }
        if (Input.GetKey(KeyCode.W))
        {
            return new float[] { 2 };
        }
        return new float[] { 0 };

    }

    // Unity function run on each iteration
    private void FixedUpdate() 
    {
        // WARNING: Rewarding here is very frequent so use with caution

        // Progression towards goal?
        float stopDist = Vector3.Distance(navAgent.destination, this.transform.position);
        if (stopDist > prevStopDist) { // Agent gone away from goal
            AddReward(rwdAwayFromGoal);
        }
        prevStopDist = stopDist;

        // TODO: Reward bus for being in correct index order

    }
	
	public override void CollectObservations()
	{

		// Agent positions - with y removed 
	    AddVectorObs(this.transform.position.x);
        AddVectorObs(this.transform.position.z);
        AddVectorObs(otherBus.transform.position.x);
        AddVectorObs(otherBus.transform.position.z);

        // Distance between agents
        // AddVectorObs(Vector3.Distance(otherBus.transform.position, this.transform.position));
        AddVectorObs(this.transform.position.x - otherBus.transform.position.x);
	    AddVectorObs(this.transform.position.z - otherBus.transform.position.z);

        // Current speeds
        AddVectorObs(this.rBody.velocity);
        AddVectorObs(otherBusRB.velocity);

        // Get current status of other bus variables
        BusBehaviour otherBusState = otherBus.GetComponent<BusBehaviour>();
        
        // Scheduled order
        AddVectorObs(this.busIdx);
        AddVectorObs(otherBusState.busIdx);

        // Bus stops
        AddVectorObs(this.nextStopIdx);
        AddVectorObs(otherBusState.nextStopIdx);

        // Loading behaviour
        AddVectorObs(this.loading);
        AddVectorObs(otherBusState.loading);

	}
	
	public override void AgentAction(float[] vectorAction, string textAction)
	{
        AddReward(rwdAction);

        // Determine move action
        float moveAmount = 0;
        if (vectorAction[0] == 1)
        {
            moveAmount = moveSpeed;
        }
        else if (vectorAction[0] == 2)
        {
            moveAmount = moveSpeed * 0.5f; // slower speed
        }
        else if (vectorAction[0] == 3)
        {
            moveAmount = 0f; // wait
        }
        
        print(busIdx.ToString() + " move amount " + moveAmount.ToString());
        
        // Apply the movement
        Vector3 moveVector = transform.forward * moveAmount;
        rBody.AddForce(moveVector * moveSpeed, ForceMode.VelocityChange);

	}

    void OnTriggerEnter(Collider collision) {
        
        // print (this.name + " collided with " + collision.gameObject.tag);

        // if at the next stop
        if (collision.gameObject.tag == currentStop) {
            
            // Reward reaching next stop
            // AddReward(rwdEachStop);

            // Reward maintaining distance
            float dist = Vector3.Distance(otherBus.transform.position, this.transform.position);
            // print(busIdx.ToString() + " distance to other: " + dist);         
            if (dist > 700) { AddReward(rwdDistance); }  
            
            // Get stop details
            StopCounts stopCount = collision.gameObject.GetComponent<StopCounts>();
            if (stopCount.waiting > 0) { StartCoroutine(WaitAndGo(stopCount)); }  // only stop if px waiting
            
            // Update stop 
            updateNextStop();

            // if full circle completed, mark as episode done
            if (currentStop == "Stop 1") {
                AddReward(rwdCircuit);
                Done();
            }

        // if returned to previous stop then penalise heavily
        } else if (collision.gameObject.tag == previousStop) {
            AddReward(rwdPrevStop);
        }
    }

    IEnumerator WaitAndGo(StopCounts stopCount)
    {
        // stop bus
        navAgent.Stop();
        print (this.name + " loading " + stopCount.waiting.ToString() + " passengers at " + stopOrder[nextStopIdx-1]);

        this.loading = stopCount.waiting;

        // start loading
        while (stopCount.waiting > 0) {
            stopCount.waiting--; // load passenger
            this.loading--; 
            passengersBoarded++; 
            yield return new WaitForSeconds(1f);   //Wait
            // print(this.name + " passengers waiting: " + stopCount.waiting);
            AddReward(rwdPsxPickup);
        }

        // confirm all passengers picked up (by this or other bus)
        this.loading = 0;

        // start bus
        navAgent.Resume();
    }

    void updateNextStop() {
        
        // Update next destination
        if (nextStopIdx > stopOrder.Length-1) { nextStopIdx = 0; }
        previousStop = currentStop;
        currentStop = stopOrder[nextStopIdx];
        navAgent.destination = GameObject.Find(currentStop).transform.position;
        navAgent.SetDestination(navAgent.destination);
        nextStopIdx++;

    }
}