// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAgentAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField] private float movingThreshold = 0.08f;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        var moveSpeed = agent.velocity.magnitude;
        animator.SetFloat(speedParameter, moveSpeed);
        animator.SetBool(movingParameter, moveSpeed > movingThreshold && !agent.pathPending);
    }
}
