// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RobotCoinPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private string gameplayFloorName = "Large Gameplay Floor";
    [SerializeField] private Vector2 referenceGameplaySize = new Vector2(28f, 28f);
    [SerializeField] private float minimumGameplaySpeedScale = 0.45f;
    [SerializeField] private float maximumGameplaySpeedScale = 1.5f;
    [SerializeField] private float fallLoseHeight = -3f;
    [SerializeField] private float edgeFallMargin = 0.25f;
    [SerializeField] private float edgeFallDownSpeed = 2.6f;
    [SerializeField] private float rotateSpeed = 130f;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField] private float movingThreshold = 0.08f;
    [SerializeField] private Color[] colors =
    {
        Color.white,
        new Color(0.45f, 0.85f, 1f, 1f),
        new Color(1f, 0.72f, 0.18f, 1f),
        new Color(0.55f, 1f, 0.5f, 1f)
    };

    private Rigidbody robotRigidbody;
    private Renderer[] renderers;
    private int colorIndex;
    private float moveInput;
    private float rotateInput;
    private bool fallLossTriggered;
    private Transform gameplayFloor;
    private CapsuleCollider playerCollider;
    private bool fallingOffField;

    public float MoveSpeed => moveSpeed;

    public Vector3 CreateScaledMovement(Vector3 worldDirection, float speedMultiplier, float deltaTime)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        var direction = Vector3.ClampMagnitude(worldDirection, 1f);
        var scale = GetGameplaySpeedScale();
        var speed = moveSpeed * speedMultiplier * deltaTime;
        return new Vector3(direction.x * scale.x, 0f, direction.z * scale.y) * speed;
    }

    public float GetScaledAnimationSpeed(float speedMultiplier)
    {
        var scale = GetGameplaySpeedScale();
        return moveSpeed * speedMultiplier * (scale.x + scale.y) * 0.5f;
    }

    private void Awake()
    {
        robotRigidbody = GetComponent<Rigidbody>();
        robotRigidbody.useGravity = true;
        robotRigidbody.freezeRotation = true;
        robotRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        playerCollider = GetComponent<CapsuleCollider>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        ApplyColor(colors[colorIndex]);
        FindGameplayFloor();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            colorIndex = (colorIndex + 1) % colors.Length;
            ApplyColor(colors[colorIndex]);
        }

        ReadMovementInput();
        CheckFallLoss();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (fallingOffField)
        {
            return;
        }

        var rotation = Quaternion.Euler(0f, rotateInput * rotateSpeed * Time.fixedDeltaTime, 0f);
        robotRigidbody.MoveRotation(robotRigidbody.rotation * rotation);

        var movement = CreateScaledMovement(transform.forward * moveInput, 1f, Time.fixedDeltaTime);
        if (ShouldFallFromGameplayFloor(robotRigidbody.position, movement, out var fallDirection))
        {
            StartFallingFromField(fallDirection);
            return;
        }

        var velocity = robotRigidbody.velocity;
        robotRigidbody.velocity = new Vector3(movement.x / Time.fixedDeltaTime, velocity.y, movement.z / Time.fixedDeltaTime);
    }

    private void ReadMovementInput()
    {
        moveInput = 0f;
        rotateInput = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            moveInput += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            moveInput -= 1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            rotateInput -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            rotateInput += 1f;
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        var movementSpeed = Mathf.Abs(moveInput) * GetScaledAnimationSpeed(1f);
        animator.SetFloat(speedParameter, movementSpeed);
        animator.SetBool(movingParameter, movementSpeed > movingThreshold);
    }

    private Vector2 GetGameplaySpeedScale()
    {
        if (gameplayFloor == null)
        {
            FindGameplayFloor();
        }

        if (gameplayFloor == null)
        {
            return Vector2.one;
        }

        var referenceWidth = Mathf.Max(0.01f, referenceGameplaySize.x);
        var referenceDepth = Mathf.Max(0.01f, referenceGameplaySize.y);
        var scaleX = Mathf.Abs(gameplayFloor.lossyScale.x) / referenceWidth;
        var scaleZ = Mathf.Abs(gameplayFloor.lossyScale.z) / referenceDepth;
        return new Vector2(
            Mathf.Clamp(scaleX, minimumGameplaySpeedScale, maximumGameplaySpeedScale),
            Mathf.Clamp(scaleZ, minimumGameplaySpeedScale, maximumGameplaySpeedScale));
    }

    private void FindGameplayFloor()
    {
        var floorObject = GameObject.Find(gameplayFloorName);
        gameplayFloor = floorObject != null ? floorObject.transform : null;
    }

    private bool ShouldFallFromGameplayFloor(Vector3 currentPosition, Vector3 movement, out Vector3 fallDirection)
    {
        fallDirection = Vector3.zero;
        if (gameplayFloor == null)
        {
            FindGameplayFloor();
        }

        if (gameplayFloor == null || movement.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        var nextPosition = currentPosition + movement;
        var currentLocal = gameplayFloor.InverseTransformPoint(currentPosition);
        var nextLocal = gameplayFloor.InverseTransformPoint(nextPosition);
        var localMovement = nextLocal - currentLocal;
        var marginX = edgeFallMargin / Mathf.Max(0.001f, Mathf.Abs(gameplayFloor.lossyScale.x));
        var marginZ = edgeFallMargin / Mathf.Max(0.001f, Mathf.Abs(gameplayFloor.lossyScale.z));
        var minX = -0.5f + marginX;
        var maxX = 0.5f - marginX;
        var minZ = -0.5f + marginZ;
        var maxZ = 0.5f - marginZ;
        var localFallDirection = Vector3.zero;

        if (nextLocal.x <= minX && localMovement.x < 0f)
        {
            localFallDirection.x = -1f;
        }
        else if (nextLocal.x >= maxX && localMovement.x > 0f)
        {
            localFallDirection.x = 1f;
        }

        if (nextLocal.z <= minZ && localMovement.z < 0f)
        {
            localFallDirection.z = -1f;
        }
        else if (nextLocal.z >= maxZ && localMovement.z > 0f)
        {
            localFallDirection.z = 1f;
        }

        if (localFallDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        fallDirection = Vector3.ProjectOnPlane(gameplayFloor.TransformDirection(localFallDirection), Vector3.up);
        if (fallDirection.sqrMagnitude <= 0.001f)
        {
            fallDirection = Vector3.ProjectOnPlane(movement, Vector3.up);
        }

        return fallDirection.sqrMagnitude > 0.001f;
    }

    private void StartFallingFromField(Vector3 fallDirection)
    {
        fallingOffField = true;
        moveInput = 0f;
        rotateInput = 0f;

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        robotRigidbody.detectCollisions = false;
        robotRigidbody.useGravity = true;
        var outwardVelocity = fallDirection.normalized * GetScaledAnimationSpeed(1f);
        robotRigidbody.velocity = outwardVelocity + Vector3.down * edgeFallDownSpeed;
    }

    private void CheckFallLoss()
    {
        if (fallLossTriggered || transform.position.y > fallLoseHeight)
        {
            return;
        }

        fallLossTriggered = true;
        moveInput = 0f;
        rotateInput = 0f;

        var scoreHud = ScoreHudController.Instance;
        if (scoreHud == null)
        {
            scoreHud = FindObjectOfType<ScoreHudController>();
        }

        if (scoreHud != null)
        {
            scoreHud.EndGame();
            return;
        }

        Debug.Log("Игрок упал за пределы карты");
    }

    private void ApplyColor(Color color)
    {
        foreach (var currentRenderer in renderers)
        {
            foreach (var material in currentRenderer.materials)
            {
                material.color = color;
            }
        }
    }
}
