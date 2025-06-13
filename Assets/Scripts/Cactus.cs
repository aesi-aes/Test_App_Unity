using System;
using UnityEngine;

public class Cactus : MonoBehaviour
{
	private Transform endTransform;

	[SerializeField]
	private float speed = 1f;

	public bool IsFinished() => Helper.CompareVector3(transform.position, endTransform.position);

	public void Move()
	{
		transform.position = Vector3.MoveTowards(transform.position, endTransform.position, Time.deltaTime * speed);
	}

	public void Setup(Transform endPosition)
	{
		endTransform = endPosition; 
	}
}
