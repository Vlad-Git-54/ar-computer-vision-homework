// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FixedJointTrainController : MonoBehaviour
{
    [SerializeField] private KeyCode forceKey = KeyCode.F;
    [SerializeField] private Vector3 forceDirection = Vector3.right;
    [SerializeField] private float forcePower = 14f;
    [SerializeField] private bool pushOnStart = true;

    private Rigidbody wagonRigidbody;

    private void Awake()
    {
        wagonRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (pushOnStart)
        {
            PushFirstWagon();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(forceKey))
        {
            PushFirstWagon();
        }
    }

    private void PushFirstWagon()
    {
        wagonRigidbody.AddForce(forceDirection.normalized * forcePower, ForceMode.Impulse);
    }
}
