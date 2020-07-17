using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckCars : MonoBehaviour
{
	/*
	this scripts counts how many cars are currently in its trigger (which is the same as the CheckCars trigger)
	this is helpful for walkers who arrive in order to know if cars are waiting
	*/

	private List<GameObject> cars = null;

	public List<GameObject> GetCars()
	{
		return cars;
	}

	void Start()
	{
		cars = new List<GameObject>();
	}

	// unlike CheckWalkers, we don't need an update to remove null elements
	// because there isn't any destination near a CheckCars trigger
	// therefore there won't be any car destroyed inside the trigger

	// when a car enters the trigger, add it to the list
	private void OnTriggerEnter(Collider other)
	{
		if (other.tag == "Car")
		{
			// this is just a security to be sure we don't count twice the same car
			foreach (GameObject go in cars)
			{
				if (go == other.gameObject)
				{
					return;
				}
			}
			cars.Add(other.gameObject);
		}
	}

	// when a car leaves the trigger, remove it from the list
	private void OnTriggerExit(Collider other)
	{
		cars.Remove(other.gameObject);
	}
}