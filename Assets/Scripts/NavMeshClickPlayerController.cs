// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshClickPlayerController : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private float sampleDistance = 2f;
    [SerializeField] private Color markerColor = new Color(0.25f, 0.9f, 0.35f, 1f);

    private NavMeshAgent agent;
    private GameObject destinationMarker;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        CreateDestinationMarker();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0) || sceneCamera == null)
        {
            return;
        }

        var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 200f))
        {
            return;
        }

        if (NavMesh.SamplePosition(hit.point, out var navMeshHit, sampleDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(navMeshHit.position);
            destinationMarker.transform.position = navMeshHit.position + Vector3.up * 0.04f;
            destinationMarker.SetActive(true);
        }
    }

    private void CreateDestinationMarker()
    {
        destinationMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        destinationMarker.name = "Player Click Destination Marker";
        destinationMarker.transform.localScale = new Vector3(0.4f, 0.03f, 0.4f);
        destinationMarker.SetActive(false);

        var markerRenderer = destinationMarker.GetComponent<Renderer>();
        markerRenderer.material.color = markerColor;

        var markerCollider = destinationMarker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }
    }
}
