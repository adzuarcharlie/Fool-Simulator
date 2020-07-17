using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficLight : MonoBehaviour
{
	// all materials used for lights
	private static Material green;
	private static Material orange;
	private static Material red;
	private static Material black; // this one is for unlit lights

	// the master is another traffic light to synchronise to
	// if one has no master, it will simply use the green, orange and red timers to change its color
	[SerializeField] private TrafficLight master = null;
	// if isSynchronised is set to true, this traffic light will always be in the same state as its master
	// if it is set to false, it will have the opposite state :
	// green if the master is orange or red
	// then will change to orange once the green timer has finished
	// then will change to red once the orange timer has finished. Will change the master to green at the same time
	[SerializeField] private bool isSynchronised = false;

	// timers used to change color (in case they are used)
	[SerializeField] private float greenTime = 5f;
	[SerializeField] private float orangeTime = 2f;
	[SerializeField] private float redTime = 10f;
	[SerializeField] private bool useRedTime = false; // if this is not the master, we might want not to use the red timer and check the master's color instead
	[SerializeField] private bool stopCars = true; // true = this is a traffic light for cars ; false = this is for walkers

	private float timer;

	public enum Color
	{
		Green,
		Orange,
		Red
	}

	private Color color;

	public Color GetColor()
	{
		return color;
	}

	private delegate void ChangeState();
	private ChangeState changeState; // delegate to check if the color has to be changed
	// allows a separation between the Update() and the CheckGreen()/CheckOrange() functions to make the code easier to read/modify

	void Start()
	{
		if (!red)
		{
			green = Resources.Load<Material>("Green");
			orange = Resources.Load<Material>("Orange");
			red = Resources.Load<Material>("Red");
			black = Resources.Load<Material>("Black");
		}

		// a master traffic light starts green, so the traffic lights that depend on it will start red
		// synchronised ones will be updated to the master's state anyway
		if (master)
		{
			SetRed();
		}
		else
		{
			SetGreen();
		}
		timer = 0f;
		changeState = CheckGreen;
	}

	void Update()
	{
		if (master) // the traffic light has a master
		{
			if (isSynchronised)
			{ // if it is synchronised and the master has changed color, update this traffic light to its color
				if (master.color != color)
				{
					switch (master.color)
					{
						case Color.Green:
							SetGreen();
							break;
						case Color.Orange:
							SetOrange();
							break;
						case Color.Red:
							SetRed();
							break;
						default:
							break;
					}
				}
			}
			else // if it is not synchronised
			{
				if (master.color == Color.Red)
				{
					if (color == Color.Red)
					{ // if master and this are red, it means the master has changed to red during this frame
														// (because this is red while master is green or orange)
						// so this traffic light changes to green
						SetGreen();
					}

					timer += Time.deltaTime;
					changeState(); // while master is red, we use the timers to change this to orange then red

					if (color == Color.Red) // when this changes to red, set the master to green, then this simply has to wait for it to change to red again
					{
						master.SetGreen();
					}
				}
			}
		}
		else // this is the master
		{
			if (color != Color.Red)
			{
				// always use the timers to update green and orange states
				timer += Time.deltaTime;
				changeState();
			}
			else
			{
				if (useRedTime) // when it is red, use the timer only if useRedTime is set to true
				{
					timer += Time.deltaTime;
					if (timer > redTime)
					{
						SetGreen();
					}
				}
			}
		}
	}

	// update green state by checking the timer and changing the color to orange once it is over
	private void CheckGreen()
	{
		if (timer > greenTime)
		{
			timer = 0f;
			SetOrange();
		}
	}

	// update orange state by checking the timer and changing the color to red once it is over
	private void CheckOrange()
	{
		if (timer > orangeTime)
		{
			timer = 0f;
			SetRed();
		}
	}

	// when the color is set to green, set the right material to the corresponding light and the unlit material to others
	private void SetGreen()
	{
		timer = 0f;
		color = Color.Green;
		foreach (Transform tr in GetComponentsInChildren<Transform>(false))
		{
			if (tr.name == "Green")
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(green);
			}
			else if (tr.name != "Check" && tr.name != "Check2" && !tr.name.Contains("TrafficLight"))
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(black);
			}
		}
		changeState = CheckGreen;
	}

	// when the color is set to orange, set the right material to the corresponding light and the unlit material to others
	private void SetOrange()
	{
		timer = 0f;
		color = Color.Orange;
		foreach (Transform tr in GetComponentsInChildren<Transform>(false))
		{
			if (tr.name == "Orange")
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(orange);
			}
			else if (!stopCars && tr.name == "Red") // if stopCars is set to false, it means this traffic light is for walkers and therefore has no orange light ; the orange state corresponds to red
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(red);
			}
			else if (tr.name != "Check" && tr.name != "Check2" && !tr.name.Contains("TrafficLight"))
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(black);
			}
		}
		changeState = CheckOrange;
	}

	// when the color is set to red, set the right material to the corresponding light and the unlit material to others
	private void SetRed()
	{
		timer = 0f;
		color = Color.Red;
		foreach (Transform tr in GetComponentsInChildren<Transform>(false))
		{
			if (tr.name == "Red")
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(red);
			}
			else if (tr.name != "Check" && tr.name != "Check2" && !tr.name.Contains("TrafficLight"))
			{
				tr.GetComponent<MeshRenderer>().material.CopyPropertiesFromMaterial(black);
			}
		}
		changeState = CheckGreen;
	}
}
