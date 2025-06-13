using UnityEngine;
using UnityEngine.InputSystem;

public class Dino : MonoBehaviour
{
    private bool isJumping = false;
	private Vector3 defaultPosition;
	private float currentJumpTime;

	[SerializeField]
	private float maxHeight = 2f;

	[SerializeField]
	private float jumpSpeed = 2f;

	[SerializeField]
	private float maxJumpTime = .5f;

	private bool IsGrounded() => Mathf.Approximately(transform.position.y, defaultPosition.y);

	private bool IsMaxJumpHeight() => transform.position.y >= defaultPosition.y + maxHeight - Mathf.Epsilon;

	private void Start()
	{
		defaultPosition = transform.position;
	}

	void Update()
    {

		if (IsGrounded() && !isJumping)
		{
			isJumping = Keyboard.current.upArrowKey.isPressed;
			currentJumpTime = Time.time;
		}
		else if (isJumping)
		{
			isJumping = Keyboard.current.upArrowKey.isPressed && currentJumpTime < Time.time + maxJumpTime && !IsMaxJumpHeight();
		}

		var target = isJumping ? defaultPosition + Vector3.up * maxHeight : defaultPosition;
		transform.position = Vector3.MoveTowards(transform.position, target, jumpSpeed * Time.deltaTime);

	}
}
