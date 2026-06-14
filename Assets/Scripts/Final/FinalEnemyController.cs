// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public class FinalEnemyController : MonoBehaviour
{
    [SerializeField] private float destinationUpdateInterval = 0.12f;
    [SerializeField] private float touchDistance = 0.9f;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";
    [SerializeField] private string movingParameter = "IsMoving";

    private NavMeshAgent agent;
    private Transform target;
    private FinalGameController gameController;
    private float nextDestinationTime;
    private Vector3 previousPosition;
    private float stuckTimer;

    public void Configure(FinalGameController owner, Transform player, Animator characterAnimator, float speed)
    {
        gameController = owner;
        target = player;
        animator = characterAnimator;

        agent.speed = speed;
        agent.acceleration = 18f;
        agent.angularSpeed = 540f;
        agent.stoppingDistance = 0.05f;
        agent.autoBraking = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.radius = 0.38f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;

        var capsule = GetComponent<CapsuleCollider>();
        capsule.radius = 0.42f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.isTrigger = true;

        previousPosition = transform.position;
    }

    private void Update()
    {
        if (target == null || gameController == null || gameController.IsGameEnded)
        {
            SetAnimation(0f);
            return;
        }

        if (Time.time >= nextDestinationTime)
        {
            nextDestinationTime = Time.time + destinationUpdateInterval;
            agent.SetDestination(target.position);
        }

        RecoverIfStuck();
        SetAnimation(agent.velocity.magnitude);

        if (Vector3.Distance(transform.position, target.position) <= touchDistance)
        {
            gameController.LoseGame("Противник догнал игрока");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (target == null || gameController == null)
        {
            return;
        }

        if (other.transform == target || other.GetComponentInParent<FinalPlayerController>() != null)
        {
            gameController.LoseGame("Противник догнал игрока");
        }
    }

    private void RecoverIfStuck()
    {
        if (!agent.hasPath)
        {
            stuckTimer = 0f;
            previousPosition = transform.position;
            return;
        }

        var moved = Vector3.Distance(transform.position, previousPosition);
        previousPosition = transform.position;

        if (moved > 0.015f)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.4f)
        {
            return;
        }

        stuckTimer = 0f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * 2.2f, out hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    private void SetAnimation(float speed)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(speedParameter, speed);
        animator.SetBool(movingParameter, speed > 0.08f);
    }
}
