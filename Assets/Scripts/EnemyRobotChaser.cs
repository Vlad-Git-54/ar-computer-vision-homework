// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class EnemyRobotChaser : MonoBehaviour
{
    [SerializeField] private string targetObjectName = "Robot Player";
    [SerializeField] private float moveSpeed = 3.25f;
    [SerializeField] private float rotationSpeed = 9f;
    [SerializeField] private float stopDistance = 0.08f;
    [SerializeField] private bool scaleMovementWithParent = true;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField] private Color enemyColor = new Color(1f, 0.28f, 0.22f, 1f);

    private Rigidbody enemyRigidbody;
    private Transform target;
    private Renderer[] renderers;

    private void Awake()
    {
        enemyRigidbody = GetComponent<Rigidbody>();
        enemyRigidbody.useGravity = true;
        enemyRigidbody.freezeRotation = true;
        enemyRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        ApplyColor(enemyColor);
        FindTarget();
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            FindTarget();
        }

        if (target == null)
        {
            SetAnimationSpeed(0f);
            return;
        }

        var direction = target.position - transform.position;
        direction.y = 0f;

        var distance = direction.magnitude;
        if (distance <= stopDistance || distance <= 0.01f)
        {
            SetAnimationSpeed(0f);
            return;
        }

        var moveDirection = direction.normalized;
        var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        enemyRigidbody.MoveRotation(Quaternion.Slerp(enemyRigidbody.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));

        var nextPosition = enemyRigidbody.position + moveDirection * GetScaledMoveSpeed() * Time.fixedDeltaTime;
        enemyRigidbody.MovePosition(nextPosition);
        SetAnimationSpeed(moveSpeed);
    }

    private void FindTarget()
    {
        var playerObject = GameObject.Find(targetObjectName);
        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }

    private void SetAnimationSpeed(float speed)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(speedParameter, speed);
        animator.SetBool(movingParameter, speed > 0.08f);
    }

    private float GetScaledMoveSpeed()
    {
        if (!scaleMovementWithParent || transform.parent == null)
        {
            return moveSpeed;
        }

        var parentScale = transform.parent.lossyScale;
        var horizontalScale = (Mathf.Abs(parentScale.x) + Mathf.Abs(parentScale.z)) * 0.5f;
        return moveSpeed * Mathf.Max(0.01f, horizontalScale);
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
