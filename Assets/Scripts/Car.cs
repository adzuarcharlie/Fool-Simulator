using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.AI;

public class Car : MonoBehaviour
{
	NavMeshAgent agent = null; // navmesh agent component reference

	// I implemented the same "intermediate destinations" system as the walker's, even though it is not useful for now
	// because the cars go straight forward, but it will find itself useful if we want to give them more complex paths
	private Vector3 destination;
	private bool destinationChanged = false;
	private bool destinationValid = false; // use to be sure the destination is correctly initialized and not the default value

	public CarSpawner spawn = null; // ref to the spawner, used to call the RemoveCar() function when the car has arrived its destination

	List<GameObject> refWalkersWaiting = null; // list of walkers waiting at a crosswalk (is updated when walkers leave)
	List<GameObject> walkersWaiting = null; // list of walkers that were waiting when agent arrived
	int refCount; // count of walkers that were waiting the previous frame, used to know if a walker left, so we don't check the whole lists every frame

	float currentSpeed = 0f; // speed is manually regulated in the update
	// modifiable values
	[SerializeField] float maxSpeed = 40f;
	[SerializeField] float acceleration = 20f;
	[SerializeField] float stopMultiplier = 10f; // we multiply the acceleration by this value to simulate a break
	[SerializeField] float stopStartDistance = 15f; // this is the distance from the car ahead at which we start reducing the speed

	// we check if there is a car ahead
	// if there is one, we regulate the speed according to the distance to it, else we accelerate until full speed
	[SerializeField] private GameObject carAhead = null;
	[SerializeField] float maxDetectionDistance = 15f; // distance at which the car ahead is detected
	public void SetDestination(Vector3 newDestination)
	{
		destination = newDestination;
		destinationChanged = true;
		destinationValid = true;
	}

	private TrafficLight trafficLight = null; // when the car is at a traffic light, we get a reference to in order to check its color

	private float lifeTimer = 0f; // timer to make sure that the car isn't destroyed just after it's spawned

	void Start()
	{
		agent = GetComponent<NavMeshAgent>();

		// these values aren't serialized because there are only 2 for now, it would need to be if we add more complexity
		agent.SetAreaCost(4, 1);
		agent.SetAreaCost(5, 10);

		// 32 + 16, so 110000 in binary, which corresponds to the 5th and 6th areas (so road and cross-walk)
		agent.areaMask = 48; // as 4th area is set to 0, the cars won't be able to navigate on the side-walk

		walkersWaiting = new List<GameObject>();
	}

	void Update()
	{
		lifeTimer += Time.deltaTime;
		RaycastHit hit;
		// the traffic lights work with triggers, so we need to ignore them to be sure the car is sensed even if it is inside one
		if (Physics.Raycast(transform.position, transform.forward, out hit, maxDetectionDistance, int.MaxValue, QueryTriggerInteraction.Ignore))
		{
			// we only detect cars for now, we could also detect walkers
			// this part should also be improved using a collider to detect everything that is around the car and not only straight ahead
			// we could also implement a prediction algorithm to prevent collisions with cars coming from the sides
			if (hit.transform.gameObject.GetComponent<Car>())
			{
				carAhead = hit.transform.gameObject; // check if there is a car not far ahead
			}
		}

		if (!destinationValid) // if this bool is set to true, it means the destination has not been set
		{// so we don't do anything (the destination should be correct for the next frame)
			return;
		}
		if (destinationChanged)
		{
			agent.SetDestination(destination);
			destinationChanged = false;
		} // when the destination changes, we let a frame to update all intern path-finding values, just in case
		else if (agent.remainingDistance < agent.stoppingDistance && lifeTimer > 0.1f) // lifeTimer is used to avoid cars being destroyed on their first frame
		{
			// once the car has reached its destination
			if (spawn)
			{
				// call the spawner's remove function that takes care of everything
				spawn.RemoveCar(gameObject);
			}
			else
			{
				// if the spanwer isn't referenced, simply destroy it (which shouldn't happen, but I'd rather have this security)
				Destroy(gameObject);
			}
		}
		else // this part is the main behaviour, where we manage speed
		{
			if (walkersWaiting.Count != 0) // this list is only updated when the car is waiting at a traffic light
			{ // so if the count is != from 0, this means there are walkers crossing the road or willing to
				// therefore we stop the car
				currentSpeed -= acceleration * Time.deltaTime * stopMultiplier;
				if (currentSpeed < 0f)
				{
					currentSpeed = 0f;
				}
			}
			else if (trafficLight != null) // if there is nobody waiting to cross the road, we look at the traffic light
			{ // it doesn't count the walkers who arrived after the car, it's a "first arrived -> first gone"
				if (trafficLight.GetColor() == TrafficLight.Color.Green)
				{
					trafficLight = null; // if it is green, the car passes and has no longer need of looking at this traffic light
				}
				else if (trafficLight.GetColor() == TrafficLight.Color.Red)
				{
					// if it is red, we stop the car
					currentSpeed -= acceleration * Time.deltaTime * stopMultiplier;
					if (currentSpeed < 0f)
					{
						currentSpeed = 0f;
					}
				}
				// if it is orange, we keep the previous behaviour
				// if it turns to red before the car has passed, it will stop
				// when the car passes the cross-walk, it will stop looking at the traffic light (this is coded in the OnTriggerExit at the end of this script)
			}
			else if (carAhead != null) // if there is a car ahead, we regulate the speed according to the distance
			{
				if (Vector3.Distance(transform.position, carAhead.transform.position) < agent.radius * 2.5f) // if it is too close, we break instantly
				{ // we know all cars have the same radius here, else we would have to add both agents' radius, here we can simply double it
				  // we also add an arbitrary .5 to let a little space between the cars
					currentSpeed = 0f; // this is an emergency case, normally the car reduces its speed slowly and stops before to come this close (at least if the stopMultiplier is high enough)
				}
				else if (Vector3.Distance(transform.position, carAhead.transform.position) < stopStartDistance)
				{
					// if the car is within the specified range, gently stop the car
					currentSpeed -= acceleration * Time.deltaTime * stopMultiplier;
					if (currentSpeed < 0f)
					{
						currentSpeed = 0f;
					}
				}
				else // if the car is too far away, apply normal behaviour
				{ // which is to accelerate until max speed
					currentSpeed += acceleration * Time.deltaTime;
					if (currentSpeed > maxSpeed)
					{
						currentSpeed = maxSpeed;
					}
				}
			}
			else // if the car is not at a traffic light and there is no car ahead, simply accelerate until full speed
			{
				currentSpeed += acceleration * Time.deltaTime;
				if (currentSpeed > maxSpeed)
				{
					currentSpeed = maxSpeed;
				}
			}

			// apply speed to the NavMeshAgent
			transform.forward = (destination - transform.position).normalized;
			agent.velocity = transform.forward * currentSpeed;
		}
		// no matter the behaviour
		// check the reference walkers list and the current walkers list to know if a walker is gone
		// if a walker is gone, remove it so that the car will be able to move when there is none left
		if (refWalkersWaiting != null)
		{
			if (refWalkersWaiting.Count < refCount)
			{
				bool present = false;
				do
				{
					foreach (GameObject go in walkersWaiting)
					{
						present = false;
						foreach (GameObject refGO in refWalkersWaiting)
						{
							if (refGO == go)
							{
								present = true;
								break;
							}
						}
						if (!present)
						{
							walkersWaiting.Remove(go);
							break;
						}
					}
				} while (!present && walkersWaiting.Count > 0);
			}
			refCount = refWalkersWaiting.Count;
		}
	}

	// when the car enters a trigger, check which kind it is and apply the corresponding behaviour
	private void OnTriggerEnter(Collider other)
	{
		if (other.name == "CarCheck")
		{
			// if this is a traffic light with a cross-walk, we have to register the walkers currently waiting to know when all are gone
			// all walkers who arrive later won't be considered by this car
			refWalkersWaiting = other.gameObject.transform.parent.GetComponentInChildren<CheckWalkers>().GetWalkers();
			refCount = refWalkersWaiting.Count;
			foreach (GameObject go in refWalkersWaiting)
			{
				walkersWaiting.Add(go);
			}
		}
		else if (other.name == "Check") // if this is a simple traffic light with no cross-walk, we only need to register the traffic light as no walker can cross the road
		{
			trafficLight = other.GetComponentInParent<TrafficLight>();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.name == "Check") // when the car leaves the traffic light trigger, it stops looking at it
		{
			trafficLight = null;
		}
	}
}