// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class HingeDoorController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private float motorSpeed = 90f;
    [SerializeField] private float motorForce = 250f;
    [SerializeField] private float openedAngle = 90f;
    [SerializeField] private float closedAngle = 0f;

    private HingeJoint hingeJoint;
    private bool shouldOpen;

    private void Awake()
    {
        hingeJoint = GetComponent<HingeJoint>();
        ConfigureJoint();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            shouldOpen = !shouldOpen;
        }
    }

    private void FixedUpdate()
    {
        var targetAngle = shouldOpen ? openedAngle : closedAngle;
        var angleDelta = Mathf.DeltaAngle(hingeJoint.angle, targetAngle);

        var motor = hingeJoint.motor;
        motor.force = motorForce;
        motor.targetVelocity = Mathf.Abs(angleDelta) < 2f ? 0f : Mathf.Sign(angleDelta) * motorSpeed;
        hingeJoint.motor = motor;
    }

    private void ConfigureJoint()
    {
        hingeJoint.useMotor = true;
        hingeJoint.useLimits = true;

        var limits = hingeJoint.limits;
        limits.min = closedAngle;
        limits.max = openedAngle;
        hingeJoint.limits = limits;
    }
}
