// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshEnemyChaser : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float updateInterval = 0.15f;

    private NavMeshAgent agent;
    private float nextUpdateTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (target == null || Time.time < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.time + updateInterval;
        agent.SetDestination(target.position);
    }
}
