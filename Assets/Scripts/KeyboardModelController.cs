// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class KeyboardModelController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotateSpeed = 120f;
    [SerializeField] private Color[] colors =
    {
        Color.white,
        new Color(0.45f, 0.85f, 1f, 1f),
        new Color(1f, 0.55f, 0.25f, 1f),
        new Color(0.65f, 1f, 0.45f, 1f)
    };

    private Renderer[] renderers;
    private int currentColorIndex;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        ApplyColor(colors[currentColorIndex]);
    }

    private void Update()
    {
        RotateModel();
        MoveModel();
        ChangeColor();
    }

    private void RotateModel()
    {
        var direction = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            direction -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            direction += 1f;
        }

        transform.Rotate(Vector3.up, direction * rotateSpeed * Time.deltaTime, Space.World);
    }

    private void MoveModel()
    {
        var direction = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            direction += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            direction -= 1f;
        }

        transform.position += transform.forward * direction * moveSpeed * Time.deltaTime;
    }

    private void ChangeColor()
    {
        if (!Input.GetKeyDown(KeyCode.C) || colors.Length == 0)
        {
            return;
        }

        currentColorIndex = (currentColorIndex + 1) % colors.Length;
        ApplyColor(colors[currentColorIndex]);
    }

    private void ApplyColor(Color color)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (var currentRenderer in renderers)
        {
            var materials = currentRenderer.materials;

            foreach (var material in materials)
            {
                material.color = color;
            }
        }
    }
}
