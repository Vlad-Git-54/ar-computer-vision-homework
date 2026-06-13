// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class CameraLookAtObject : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.4f, -4.5f);
    [SerializeField] private float followSpeed = 8f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = Vector3.Lerp(transform.position, target.position + offset, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.1f);
    }
}
