// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class PointCloudShowcase : MonoBehaviour
{
    [SerializeField] private int pointCount = 180;
    [SerializeField] private float pointSize = 0.1f;
    [SerializeField] private Color pointColor = new Color(0.1f, 0.95f, 1f, 1f);
    [SerializeField] private Color emissionColor = new Color(0.05f, 0.65f, 0.85f, 1f);

    private const string GeneratedPointPrefix = "Point Cloud Dot ";
    private Material pointMaterial;

    private void Start()
    {
        RebuildCloud();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            ClearGeneratedPoints();
        }

        if (pointMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(pointMaterial);
            }

            pointMaterial = null;
        }
    }

    private void OnValidate()
    {
        pointCount = Mathf.Clamp(pointCount, 20, 500);
        pointSize = Mathf.Clamp(pointSize, 0.02f, 0.35f);
    }

    private void RebuildCloud()
    {
        ClearGeneratedPoints();
        var material = GetPointMaterial();

        for (var i = 0; i < pointCount; i++)
        {
            var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = GeneratedPointPrefix + (i + 1).ToString("000");
            point.hideFlags = HideFlags.DontSave;
            point.transform.SetParent(transform, false);
            point.transform.localPosition = CreatePointPosition(i);
            point.transform.localScale = Vector3.one * CreatePointSize(i);

            var pointCollider = point.GetComponent<Collider>();
            if (pointCollider != null)
            {
                DestroyGeneratedObject(pointCollider);
            }

            var pointRenderer = point.GetComponent<MeshRenderer>();
            if (pointRenderer != null)
            {
                pointRenderer.sharedMaterial = material;
                pointRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pointRenderer.receiveShadows = false;
            }
        }
    }

    private void ClearGeneratedPoints()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith(GeneratedPointPrefix))
            {
                DestroyGeneratedObject(child.gameObject);
            }
        }
    }

    private Material GetPointMaterial()
    {
        if (pointMaterial != null)
        {
            pointMaterial.color = pointColor;
            pointMaterial.SetColor("_EmissionColor", emissionColor);
            return pointMaterial;
        }

        pointMaterial = new Material(Shader.Find("Standard"));
        pointMaterial.name = "Point Cloud Runtime Material";
        pointMaterial.hideFlags = HideFlags.DontSave;
        pointMaterial.color = pointColor;
        pointMaterial.SetColor("_EmissionColor", emissionColor);
        pointMaterial.EnableKeyword("_EMISSION");
        return pointMaterial;
    }

    private Vector3 CreatePointPosition(int index)
    {
        var progress = index / (float)(pointCount - 1);
        var angle = progress * Mathf.PI * 7.5f;
        var radius = 0.25f + progress * 1.65f;
        var wave = Mathf.Sin(index * 0.41f) * 0.45f;
        var jitterX = Mathf.Sin(index * 12.9898f) * 0.18f;
        var jitterY = Mathf.Cos(index * 7.233f) * 0.24f;
        var jitterZ = Mathf.Sin(index * 4.771f) * 0.18f;

        return new Vector3(
            Mathf.Cos(angle) * radius + jitterX,
            wave + jitterY,
            Mathf.Sin(angle) * radius * 0.72f + jitterZ
        );
    }

    private float CreatePointSize(int index)
    {
        return pointSize * (0.75f + Mathf.Abs(Mathf.Sin(index * 0.37f)) * 0.55f);
    }

    private void DestroyGeneratedObject(Object target)
    {
        Destroy(target);
    }
}
