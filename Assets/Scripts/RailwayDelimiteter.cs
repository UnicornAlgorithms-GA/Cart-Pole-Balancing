using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RailwayDelimiteter : MonoBehaviour
{
	public static RailwayDelimiteter instance;

	public Transform leftEndPoint;
	public Transform rightEndPoint;

	private void Awake()
	{
		instance = this;
	}

	public static bool IsInsideLimits(Transform target)
	{
		return target.position.x > instance.leftEndPoint.position.x &&
	           target.position.x < instance.rightEndPoint.position.x;
	}
}
