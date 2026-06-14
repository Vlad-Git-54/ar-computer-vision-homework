// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class ThirdPersonCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target = null;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5.4f, -8.4f);
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float lookHeight = 1f;
    [SerializeField] private bool followTargetRotation = false;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        var targetPosition = followTargetRotation ? target.position + target.rotation * offset : target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
