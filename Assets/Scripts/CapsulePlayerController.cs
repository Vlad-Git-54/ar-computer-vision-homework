// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class CapsulePlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private Transform cameraTransform;

    private Rigidbody playerRigidbody;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.freezeRotation = true;
        playerRigidbody.useGravity = true;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void FixedUpdate()
    {
        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        var moveDirection = GetMoveDirection(input);
        var velocity = moveDirection * moveSpeed;
        velocity.y = playerRigidbody.velocity.y;
        playerRigidbody.velocity = velocity;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        }
    }

    private Vector3 GetMoveDirection(Vector3 input)
    {
        if (cameraTransform == null)
        {
            return input;
        }

        var cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        var cameraRight = cameraTransform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        return cameraForward * input.z + cameraRight * input.x;
    }
}
