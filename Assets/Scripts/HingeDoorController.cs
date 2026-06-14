// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class HingeDoorController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private float motorSpeed = 90f;
    [SerializeField] private float motorForce = 250f;
    [SerializeField] private float openedAngle = 90f;
    [SerializeField] private float closedAngle = 0f;

    private HingeJoint doorHingeJoint;
    private bool shouldOpen;

    private void Awake()
    {
        doorHingeJoint = GetComponent<HingeJoint>();
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
        var angleDelta = Mathf.DeltaAngle(doorHingeJoint.angle, targetAngle);

        var motor = doorHingeJoint.motor;
        motor.force = motorForce;
        motor.targetVelocity = Mathf.Abs(angleDelta) < 2f ? 0f : Mathf.Sign(angleDelta) * motorSpeed;
        doorHingeJoint.motor = motor;
    }

    private void ConfigureJoint()
    {
        doorHingeJoint.useMotor = true;
        doorHingeJoint.useLimits = true;

        var limits = doorHingeJoint.limits;
        limits.min = closedAngle;
        limits.max = openedAngle;
        doorHingeJoint.limits = limits;
    }
}
