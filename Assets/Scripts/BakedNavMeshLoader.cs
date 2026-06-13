// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-10000)]
public class BakedNavMeshLoader : MonoBehaviour
{
    [SerializeField] private NavMeshData navMeshData;

    private NavMeshDataInstance navMeshDataInstance;

    private void Awake()
    {
        AddNavMeshIfNeeded();
    }

    private void OnEnable()
    {
        AddNavMeshIfNeeded();
    }

    private void OnDisable()
    {
        if (navMeshDataInstance.valid)
        {
            navMeshDataInstance.Remove();
        }
    }

    private void AddNavMeshIfNeeded()
    {
        if (navMeshData == null || navMeshDataInstance.valid)
        {
            return;
        }

        if (NavMesh.CalculateTriangulation().vertices.Length > 0)
        {
            return;
        }

        navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
    }
}
