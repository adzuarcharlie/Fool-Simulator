using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkerSpawner : MonoBehaviour
{
	[SerializeField] GameObject walkerPrefab = null;

	[SerializeField] private int nbWalkers = 20; // max number of walkers that can be spawned by this spawner (serialized so that each spawner can have a different number)
	private List<GameObject> walkers; // references to spawned walkers to keep how many are currently spawned

	[SerializeField] private float spawnTimer = 3f; // frequency at which the walkers are spawned, can also be modified for each spawner (the spawner will wait if there is something blocking the spawn area)
	private float timer;

	List<GameObject> colliders = null; // list of colliders currently blocking the spawn area, used to make sure we don't spawn a walker into a collision

	void Start()
    {
		walkers = new List<GameObject>();
		timer = 0f;
		colliders = new List<GameObject>();
	}

	void Update()
    {
		timer += Time.deltaTime;
		if (walkers.Count == 0) // if there is no walker, spawn it directly. As this is the first to be spawned, we don't wait for the timer to be finished
		{ // this may be optimized by putting this in the Start() and not having to test the condition, but I prefer to make sure that everything is correctly initialized
			// still may be possible by putting a coroutine in the start with a little delay, anyway this works fine for prototyping
			SpawnWalker();
		}
		else if (walkers.Count < nbWalkers && colliders.Count == 0 && timer > spawnTimer)
		{
			// if the max number is not reached, there is no collision blocking the spawn area
			// and the timer has finished : spawn a walker and restart the timer
			SpawnWalker();
			timer = 0f;
		}

	}

	// spawn a walker, init its destination, reference this as its spawner and add it to the list to keep up the count of current walkers
	private void SpawnWalker()
	{
		GameObject go = Instantiate(walkerPrefab, transform.position, transform.rotation, GameObject.Find("Instances").transform);
		Walker walker = go.GetComponent<Walker>();
		walker.SetRandomDestination();
		walker.spawn = this;
		walkers.Add(go);
	}

	// this will be called by the walker once it has reached its destination
	// this will destroy the walker
	public void RemoveWalker(GameObject walkerToRemove)
	{
		walkers.Remove(walkerToRemove);
		// make sure that we remove the walker's collider of the blocking list in case the walker is destroy on spawn
		// even though it should not be the case (better be careful)
		colliders.Remove(walkerToRemove.gameObject);
		Destroy(walkerToRemove);
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
