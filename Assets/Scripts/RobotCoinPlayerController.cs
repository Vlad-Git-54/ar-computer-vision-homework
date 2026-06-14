// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RobotCoinPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float fallLoseHeight = -3f;
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

    private void Awake()
    {
        robotRigidbody = GetComponent<Rigidbody>();
        robotRigidbody.useGravity = true;
        robotRigidbody.freezeRotation = true;
        robotRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

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
        var rotation = Quaternion.Euler(0f, rotateInput * rotateSpeed * Time.fixedDeltaTime, 0f);
        robotRigidbody.MoveRotation(robotRigidbody.rotation * rotation);

        var movement = transform.forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        robotRigidbody.MovePosition(robotRigidbody.position + movement);
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

        var movementSpeed = Mathf.Abs(moveInput) * moveSpeed;
        animator.SetFloat(speedParameter, movementSpeed);
        animator.SetBool(movingParameter, movementSpeed > movingThreshold);
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
