using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.AI;

public class Walker : MonoBehaviour
{
	NavMeshAgent agent = null; // navmesh agent component reference

	// as we use intermediate destinations, we need to remember the final destination
	[SerializeField] private Vector3 destination;
	private bool destinationChanged = false;

	public void SetDestination(Vector3 newDestination)
	{
		destination = newDestination;
		destinationChanged = true;
	}

	// cost for every custom navigation layer, so we can have different costs for any unit
	[SerializeField] private float sidewalkCost = 1f;
	[SerializeField] private float roadCost = 10f;
	[SerializeField] private float crossWalkCost = 2f;

	static private GameObject[] possibleDestinations = null;

	// bool for every custom navigation layer, so we can decide which layers an agent can go through
	// we only have one kind of road so far
	[SerializeField] bool canWalkOtherThanSidewalk = true;

	List<GameObject> refCarsWaiting = null; // list of cars waiting at a crosswalk (is updated when cars leave)
	List<GameObject> carsWaiting = null; // list of cars that were waiting when agent arrived
	int refCount; // count of cars that were waiting the previous frame, used to know if a car left, so we don't check the whole lists every frame

	GameObject crossWalk; // the crosswalk the agent is crossing

	// we use a delegate for the update in order to not have any state enum and still be able to determine in which state we are (particularly useful for the CheckWalkers script)
	// it's also a good way to separate the updates of the different states so it's easier to read/modify
	public delegate void UpdateType();
	private UpdateType ApplyUpdate;

	public UpdateType GetUpdate() { return ApplyUpdate; }

	private delegate void ChangeState();
	private ChangeState StartGoto;
	private ChangeState StartWait;

	private bool changedState;

	private static float[] angles = null; // used to place agents while waiting
	private static int nbMaxLayers = 5; // how many angles we calculate

	public WalkerSpawner spawn = null; // ref to the spawner, used to call the RemoveWalker() function when the walker has arrived its destination

	void Start()
	{
		agent = GetComponent<NavMeshAgent>();
		agent.SetDestination(destination); // we set the start destination (SetRandomDestination() is called by the spawner before this Start())

		// set costs to know which kind of path they prefer (for instance if they try to avoid roads and minimize the number of times they cross it)
		agent.SetAreaCost(3, sidewalkCost);
		agent.SetAreaCost(4, roadCost);
		agent.SetAreaCost(5, crossWalkCost);

		// if they can walk only on the side walk, the area mask is set to 8 (so 1000 in binary) so that it walks only on the 4th area (which is sidewalk)
		if (canWalkOtherThanSidewalk == false)
		{
			agent.areaMask = 8;
		}

		carsWaiting = new List<GameObject>(); // init the list for later

		ApplyUpdate = UpdateGoto;

		changedState = false;

		StartGoto = () =>
		{
			ApplyUpdate = UpdateGoto;
			agent.SetDestination(destination);
			changedState = true;
		};

		StartWait = () =>
		{
			ApplyUpdate = UpdateWait;
			agent.SetDestination(transform.position);
			changedState = true;
		};

		if (angles == null) // precalculate angles
		{ //as this is static, it will be calculated once and this part won't play for any other walker
			angles = new float[nbMaxLayers];
			for (int i = 0; i < nbMaxLayers; i++)
			{
				angles[i] = Mathf.Acos(1f - 1f / (2 * (i + 1) * (i + 1))); // derived from Acos(a² + b² - c² / (2ab)) which is the formula to get an angle in a triangle when you know all lengths

				// to have a regular space between agents, we truncate to a value that allows a full half-circle
				int nbAgents = (int)(Mathf.PI / angles[i]);
				angles[i] = Mathf.PI / nbAgents;
			}
		}
	}

	public void SetRandomDestination()
	{
		// check all possible destinations
		if (possibleDestinations == null)
		{
			possibleDestinations = GameObject.FindGameObjectsWithTag("WalkerDestination");
		}
		// choose a random one
		SetDestination(possibleDestinations[Random.Range(0, possibleDestinations.Length)].transform.position);
	}

	void Update()
	{
		if (agent.enabled && destinationChanged)
		{
			agent.SetDestination(destination);
			destinationChanged = false;
		}

		if (!changedState)
		{
			ApplyUpdate();
		}
		else
		{
			changedState = false;
		}
	}

	private void UpdateGoto()
	{
		if (agent.enabled && agent.remainingDistance < agent.stoppingDistance) // when the walker arrives at its final destination
		{
			if (Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(destination.x, 0f, destination.z)) < agent.stoppingDistance) // double security, just to be sure
			{
				if (spawn)
				{
					spawn.RemoveWalker(gameObject); // if it is still linked to its spawner, remove it (the function will Destroy() the walker)
				}
				else
				{
					Destroy(gameObject); // else (which should not be activated, because the walker is supposed to stay linked to its spawner), simply Destroy() the walker
				}
			}
		}
	}

	private enum WaitType
	{
		Crosswalk,
		TrafficLight
	};

	private WaitType waitType; // used to know if the walker is waiting at a simple cross-walk or if there is a traffic light

	private TrafficLight trafficLightRef = null; // if there is a traffic light, stock its reference to check its color

	public void UpdateWait()
	{
		switch (waitType)
		{
			case WaitType.Crosswalk:
				UpdateWaitingCars();
				// if this is a simple cross-walk, the walker crosses it when all the cars that were waiting at its arrival are gone
				if (carsWaiting.Count == 0)
				{
					StartGoto();
				}
				break;
			case WaitType.TrafficLight:
				// else, simply wait for the traffic light to turn green
				if (trafficLightRef.GetColor() == TrafficLight.Color.Green)
				{
					StartGoto();
				}
				break;
			default:
				StartGoto();
				break;
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.name == "WalkerCheck") // when the agent arrives at a crosswalk, it checks if cars are waiting
		{
			crossWalk = other.gameObject.transform.parent.gameObject;
			refCarsWaiting = crossWalk.GetComponentInChildren<CheckCars>().GetCars();
			refCount = refCarsWaiting.Count;
			foreach (GameObject go in refCarsWaiting)
			{
				carsWaiting.Add(go);
			}
			if (carsWaiting.Count > 0)
			{
				if (HasToCross(crossWalk.transform)) // even if a car is waiting, the agent doesn't have to stop if he doesn't want to cross (should be checked first for improved performance !)
				{
					StartWait();
					waitType = WaitType.Crosswalk;

					ChooseWaitPos();
				}
			}
		}
	}

	private void OnTriggerStay(Collider other)
	{
		if (other.name == "Check2") // means the walker arrived at a traffic light
		{
			trafficLightRef = other.GetComponentInParent<TrafficLight>();
			if (trafficLightRef)
			{
				if (trafficLightRef.GetColor() == TrafficLight.Color.Red || trafficLightRef.GetColor() == TrafficLight.Color.Orange)
				{
					if (HasToCross(trafficLightRef.transform.parent)) // if the light is not green, the walker has to stop
					{
						StartWait();
						waitType = WaitType.TrafficLight;

						ChooseWaitPos();
					}
				}
			}
		}
	}

	public enum Side
	{
		Left,
		Right
	}

	// check on which side the agent is
	public Side CheckWhichSide()
	{
		if (waitType == WaitType.Crosswalk)
		{
			return Vector3.Distance(transform.position, crossWalk.transform.Find("LeftPoint").position)
				< Vector3.Distance(transform.position, crossWalk.transform.Find("RightPoint").position) ? Side.Left : Side.Right;
		}
		else
		{
			return Vector3.Distance(transform.position, trafficLightRef.transform.parent.Find("LeftPoint").position)
				< Vector3.Distance(transform.position, trafficLightRef.transform.parent.Find("RightPoint").position) ? Side.Left : Side.Right;
		}
	}

	// check on which side the agent is and give the corresponding waiting point
	private void CheckWhichSide(out Transform closest)
	{
		Transform right;
		Transform left;
		if (waitType == WaitType.Crosswalk)
		{
			left = crossWalk.transform.Find("LeftPoint");
			right = crossWalk.transform.Find("RightPoint");
		}
		else
		{
			left = trafficLightRef.transform.parent.Find("LeftPoint");
			right = trafficLightRef.transform.parent.Find("RightPoint");
		}
		closest = Vector3.Distance(transform.position, left.position)
			< Vector3.Distance(transform.position, right.position) ? left : right;
	}

	// check if the agent wants to cross the road
	private bool HasToCross(Transform roadCenter)
	{
		Vector3 toCrossWalk = roadCenter.position - transform.position;
		Vector3 toDestination = destination - roadCenter.position;

		toCrossWalk.y = 0f;
		toDestination.y = 0f;

		Vector3 right = waitType == WaitType.Crosswalk ? roadCenter.right : roadCenter.forward;

		return (Vector3.Dot(toCrossWalk, right) > 0f
			&& Vector3.Dot(toDestination, right) > 0f)
			|| (Vector3.Dot(toCrossWalk, right) < 0f
			&& Vector3.Dot(toDestination, right) < 0f);
	}

	// check if cars that were waiting when the walker arrived have left
	private void UpdateWaitingCars()
	{
		if (refCarsWaiting != null)
		{
			if (refCarsWaiting.Count < refCount)
			{
				bool present = false;
				do
				{
					foreach (GameObject go in carsWaiting)
					{
						present = false;
						foreach (GameObject refGO in refCarsWaiting)
						{
							if (refGO == go)
							{
								present = true;
								break;
							}
						}
						if (!present)
						{
							carsWaiting.Remove(go);
							break;
						}
					}
				} while (!present && carsWaiting.Count > 0);
			}
			refCount = refCarsWaiting.Count;
		}
	}

	private void ChooseWaitPos()
	{
		// look how many people are waiting on this side
		int nbWalkers = (waitType == WaitType.Crosswalk ?
			crossWalk.GetComponentInChildren<CheckWalkers>().GetWalkersWaiting(CheckWhichSide()).Count
			: trafficLightRef.GetComponentInParent<CheckWalkers>().GetWalkersWaiting(CheckWhichSide()).Count) - 1; // - 1 because it counts itself

		// get the reference point
		Transform closest;
		CheckWhichSide(out closest);

		if (nbWalkers == 0)
		{
			agent.SetDestination(closest.position); // if this is the first walker to arrive, he goes on the waiting point
		}
		else
		{
			// else we use the angles that were precalculated to determine the waiting pos (this sould form half-circles around the first waiting walker)
			for (int i = 0; i < angles.Length; i++)
			{
				int nbMaxWalkers = (int)(Mathf.PI / angles[i]);
				if (nbWalkers >= nbMaxWalkers)
				{
					nbWalkers -= nbMaxWalkers;
				}
				else
				{
					agent.SetDestination(closest.position + closest.right * Mathf.Cos(-angles[i] * nbWalkers) * 1.5f + closest.forward * Mathf.Sin(-angles[i] * nbWalkers) * 1.5f);
				}
			}
		}
	}
}