// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class ThirdPersonCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, -5f);
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float lookHeight = 1f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        var targetPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
