// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FinalPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.4f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float fallLoseHeight = -5f;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";
    [SerializeField] private string movingParameter = "IsMoving";

    private Rigidbody playerRigidbody;
    private Transform cameraTransform;
    private FinalGameController gameController;
    private Vector3 desiredVelocity;
    private bool controlEnabled = true;
    private bool fallReported;

    public float MoveSpeed => moveSpeed;
    public bool IsMoving => desiredVelocity.sqrMagnitude > 0.03f;

    public void Configure(FinalGameController owner, Transform cameraTarget, Animator characterAnimator)
    {
        gameController = owner;
        cameraTransform = cameraTarget;
        animator = characterAnimator;
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    public void SetControlEnabled(bool value)
    {
        controlEnabled = value;
        if (!value)
        {
            desiredVelocity = Vector3.zero;
            UpdateAnimation(0f);
        }
    }

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.useGravity = true;
        playerRigidbody.freezeRotation = true;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var capsule = GetComponent<CapsuleCollider>();
        capsule.radius = 0.36f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
    }

    private void Update()
    {
        ReadInput();
        CheckFall();
    }

    private void FixedUpdate()
    {
        var currentVelocity = playerRigidbody.velocity;
        var horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        var nextHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);
        playerRigidbody.velocity = new Vector3(nextHorizontalVelocity.x, currentVelocity.y, nextHorizontalVelocity.z);

        if (nextHorizontalVelocity.sqrMagnitude > 0.02f)
        {
            var lookRotation = Quaternion.LookRotation(nextHorizontalVelocity.normalized, Vector3.up);
            playerRigidbody.MoveRotation(Quaternion.Slerp(playerRigidbody.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime));
        }

        UpdateAnimation(nextHorizontalVelocity.magnitude);
    }

    private void ReadInput()
    {
        if (!controlEnabled || Time.timeScale <= 0f)
        {
            desiredVelocity = Vector3.zero;
            return;
        }

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);

        var forward = Vector3.forward;
        var right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        var direction = forward * input.y + right * input.x;
        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
        }

        desiredVelocity = direction * moveSpeed;
    }

    private void UpdateAnimation(float speed)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(speedParameter, speed);
        animator.SetBool(movingParameter, speed > 0.08f);
    }

    private void CheckFall()
    {
        if (fallReported || transform.position.y > fallLoseHeight)
        {
            return;
        }

        fallReported = true;
        SetControlEnabled(false);
        if (gameController != null)
        {
            gameController.LoseGame("Игрок упал за край арены");
        }
    }
}
