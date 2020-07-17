using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
	[SerializeField] private GameObject carPrefab = null;
	[SerializeField] private GameObject destination = null; // unlike walkerSpawners, carSpawners have a unique destination, not a random one

	[SerializeField] private int nbCars = 1; // max number of cars that can be spawned by this spawner (serialized so that each spawner can have a different number)
	private List<GameObject> cars; // references to spawned cars to keep how many are currently spawned

	[SerializeField] private float spawnTimer = 3f; // frequency at which the cars are spawned, can also be modified for each spawner (the spawner will wait if there is something blocking the spawn area)
	private float timer;

	List<GameObject> colliders = null; // list of colliders currently blocking the spawn area, used to make sure we don't spawn a car into a collision

	void Start()
    {
		cars = new List<GameObject>();
		timer = 0f;
		colliders = new List<GameObject>();
	}

    void Update()
    {
		timer += Time.deltaTime;
        if (cars.Count == 0) // same as walkerSpawner, directly spawn a car if there is none already
		{ // could also be optimized, the same way as the walkerSpawner
			SpawnCar();
		}
		else if (cars.Count < nbCars && colliders.Count == 0 && timer > spawnTimer)
		{
			// if the max number is not reached, there is no collision blocking the spawn area
			// and the timer has finished : spawn a car and restart the timer
			SpawnCar();
			timer = 0f;
		}
    }

	// spawn a car, init its destination, reference this as its spawner and add it to the list to keep up the count of current cars
	private void SpawnCar()
	{
		GameObject go = Instantiate(carPrefab, transform.position, transform.rotation, GameObject.Find("Instances").transform);
		Car car = go.GetComponent<Car>();
		car.SetDestination(destination.transform.position);
		car.spawn = this;
		cars.Add(go);
	}

	// this will be called by the car once it has reached its destination
	// this will destroy the car
	public void RemoveCar(GameObject carToRemove)
	{
		cars.Remove(carToRemove);
		colliders.Remove(carToRemove.gameObject);
		Destroy(carToRemove);
	}

	// these 2 call-backs are simply here to update the list of blocking colliders
	private void OnTriggerEnter(Collider other)
	{
		colliders.Add(other.gameObject);
	}

	private void OnTriggerExit(Collider other)
	{
		colliders.Remove(other.gameObject);
	}
}
