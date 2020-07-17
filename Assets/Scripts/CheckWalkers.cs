using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckWalkers : MonoBehaviour
{
	/*
	this scripts counts how many walkers are currently in its trigger (which takes both sides of the cross-walk)
	this is helpful for walkers who arrive later and want to wait in order to give them their waiting position
	(the formula is detailed in the Walker script)
	this is also helpful for cars arriving at the traffic light in order to know if walkers are waiting
	*/

	private List<GameObject> walkers = null;

	public List<GameObject> GetWalkers()
	{
		return walkers;
	}

	// get all the walkers who are currently waiting on one side of the cross-walk
	public List<GameObject> GetWalkersWaiting(Walker.Side side)
	{
		List<GameObject> walkersWaiting = new List<GameObject>();

		foreach (GameObject w in walkers)
			if (w.GetComponent<Walker>().GetUpdate() == w.GetComponent<Walker>().UpdateWait // check if they are waiting
				&& w.GetComponent<Walker>().CheckWhichSide() == side) // check if they are on the correct side
				walkersWaiting.Add(w);

		return walkersWaiting;
	}

    void Start()
    {
		walkers = new List<GameObject>();
    }

    void Update()
    {
		// remove walkers who reached their destination (and therefore were destroyed) from the list
		// could be put in a coroutine for optimization, but we would lose some precision regarding when they left
		// which could delay some cars ; anyway, we don't need optimization for now so I haven't chosen yet which is better
		walkers.RemoveAll((x) => { return x == null; });
    }

	// when a walker enters the trigger, add it to the list
	private void OnTriggerEnter(Collider other)
	{
		if (other.tag == "Walker")
		{
			// this is just a security to be sure we don't count twice the same walker
			foreach (GameObject go in walkers)
			{
				if (go == other.gameObject)
				{
					return;
				}
			}
			walkers.Add(other.gameObject);
		}
	}

	// when a walker leaves the trigger, remove it from the list
	private void OnTriggerExit(Collider other)
	{
		walkers.Remove(other.gameObject);
	}
}