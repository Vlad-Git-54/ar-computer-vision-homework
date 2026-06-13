// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpringBallStarter : MonoBehaviour
{
    [SerializeField] private Vector3 startVelocity = new Vector3(2.5f, 0f, 0f);

    private void Start()
    {
        var ballRigidbody = GetComponent<Rigidbody>();
        ballRigidbody.velocity = startVelocity;
    }
}
