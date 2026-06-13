// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RobotCoinPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float rotateSpeed = 130f;
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

    private void Awake()
    {
        robotRigidbody = GetComponent<Rigidbody>();
        robotRigidbody.useGravity = true;
        robotRigidbody.freezeRotation = true;
        robotRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

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
    }

    private void FixedUpdate()
    {
        var moveInput = 0f;
        var rotateInput = 0f;

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

        var rotation = Quaternion.Euler(0f, rotateInput * rotateSpeed * Time.fixedDeltaTime, 0f);
        robotRigidbody.MoveRotation(robotRigidbody.rotation * rotation);

        var movement = transform.forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        robotRigidbody.MovePosition(robotRigidbody.position + movement);
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
