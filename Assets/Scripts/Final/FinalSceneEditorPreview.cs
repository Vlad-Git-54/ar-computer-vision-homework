// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[ExecuteAlways]
public class FinalSceneEditorPreview : MonoBehaviour
{
    [SerializeField] private bool showPreviewInEditMode = true;
    [SerializeField] private Vector2 arenaSize = new Vector2(42f, 24f);

    private const string PreviewRootName = "Final Editable Arena Preview";
    private bool previewChecked;

    private void OnEnable()
    {
        previewChecked = false;
        if (Application.isPlaying)
        {
            SetPreviewActive(false);
        }
    }

    private void Update()
    {
        if (Application.isPlaying || previewChecked)
        {
            return;
        }

        previewChecked = true;
        EnsurePreview();
    }

    [ContextMenu("Rebuild Final Arena Preview")]
    private void RebuildPreview()
    {
        var existing = transform.Find(PreviewRootName);
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        previewChecked = false;
        EnsurePreview();
    }

    private void EnsurePreview()
    {
        if (!showPreviewInEditMode)
        {
            SetPreviewActive(false);
            return;
        }

        var existing = transform.Find(PreviewRootName);
        if (existing != null && existing.childCount > 0)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        var root = new GameObject(PreviewRootName).transform;
        root.SetParent(transform, false);

        CreateCube(root, "Preview Floor", new Vector3(0f, -0.12f, 0f), new Vector3(arenaSize.x, 0.24f, arenaSize.y), new Color(0.56f, 0.82f, 0.75f, 1f));
        CreateCube(root, "Preview North Wall", new Vector3(-7f, 1.2f, 9.6f), new Vector3(20f, 2.4f, 0.48f), new Color(0.55f, 0.63f, 0.76f, 1f));
        CreateCube(root, "Preview West Wall", new Vector3(-10.5f, 1.1f, -2.5f), new Vector3(0.5f, 2.2f, 11f), new Color(0.55f, 0.63f, 0.76f, 1f));
        CreateCube(root, "Preview Center Wall", new Vector3(0f, 1.1f, 0f), new Vector3(11f, 2.2f, 0.5f), new Color(0.55f, 0.63f, 0.76f, 1f));
        CreateCube(root, "Preview East Wall", new Vector3(10.5f, 1.1f, 2.5f), new Vector3(0.5f, 2.2f, 10f), new Color(0.55f, 0.63f, 0.76f, 1f));
        CreateCube(root, "Preview Left Cover", new Vector3(-3.5f, 0.8f, -7f), new Vector3(5.4f, 1.6f, 0.55f), new Color(0.55f, 0.63f, 0.76f, 1f));
        CreateCube(root, "Preview Right Cover", new Vector3(6f, 0.8f, -5.5f), new Vector3(5.4f, 1.6f, 0.55f), new Color(0.55f, 0.63f, 0.76f, 1f));

        CreateCapsule(root, "Preview Player", new Vector3(0f, 0.9f, -8.5f), new Color(0.18f, 0.66f, 1f, 1f));
        CreateCapsule(root, "Preview Enemy 1", new Vector3(-17f, 0.9f, 8f), new Color(0.95f, 0.18f, 0.12f, 1f));
        CreateCapsule(root, "Preview Enemy 2", new Vector3(17f, 0.9f, 8f), new Color(0.95f, 0.18f, 0.12f, 1f));
        CreateCapsule(root, "Preview Enemy 3", new Vector3(17f, 0.9f, -8f), new Color(0.95f, 0.18f, 0.12f, 1f));

        var coinPositions = new[]
        {
            new Vector3(-17f, 0.55f, -8f), new Vector3(-12f, 0.55f, -4f), new Vector3(-7f, 0.55f, 6f),
            new Vector3(-2f, 0.55f, -8f), new Vector3(3f, 0.55f, 5f), new Vector3(8f, 0.55f, -7f),
            new Vector3(13f, 0.55f, 7f), new Vector3(17f, 0.55f, -2f)
        };

        for (var i = 0; i < coinPositions.Length; i++)
        {
            CreateSphere(root, "Preview Coin " + (i + 1), coinPositions[i], new Color(1f, 0.78f, 0.12f, 1f));
        }
    }

    private void SetPreviewActive(bool active)
    {
        var existing = transform.Find(PreviewRootName);
        if (existing != null)
        {
            existing.gameObject.SetActive(active);
        }
    }

    private void CreateCube(Transform parent, string objectName, Vector3 position, Vector3 scale, Color color)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        ApplyMaterial(item, color);
    }

    private void CreateCapsule(Transform parent, string objectName, Vector3 position, Color color)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
        ApplyMaterial(item, color);
    }

    private void CreateSphere(Transform parent, string objectName, Vector3 position, Color color)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = Vector3.one * 0.52f;
        ApplyMaterial(item, color);
    }

    private void ApplyMaterial(GameObject item, Color color)
    {
        var renderer = item.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        var shader = Shader.Find("Standard");
        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = color;
    }
}
