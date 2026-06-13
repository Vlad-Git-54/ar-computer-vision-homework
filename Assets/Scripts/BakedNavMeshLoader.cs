// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

public class BakedNavMeshLoader : MonoBehaviour
{
    [SerializeField] private NavMeshData navMeshData;

    private NavMeshDataInstance navMeshDataInstance;

    private void OnEnable()
    {
        if (navMeshData != null)
        {
            navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
        }
    }

    private void OnDisable()
    {
        if (navMeshDataInstance.valid)
        {
            navMeshDataInstance.Remove();
        }
    }
}
