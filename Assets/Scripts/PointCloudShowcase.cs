// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class PointCloudShowcase : MonoBehaviour
{
    [SerializeField] private int pointCount = 260;
    [SerializeField] private float pointSize = 0.085f;
    [SerializeField] private Color pointColor = new Color(0.74f, 0.97f, 1f, 1f);
    [SerializeField] private Color emissionColor = new Color(0.24f, 0.8f, 1f, 1f);

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
        var lobeIndex = index % 7;
        var lobeCenter = GetLobeCenter(lobeIndex);
        var lobeScale = GetLobeScale(lobeIndex);
        var angle = Hash01(index, 11) * Mathf.PI * 2f;
        var height = Hash01(index, 23) * 2f - 1f;
        var radius = Mathf.Pow(Hash01(index, 37), 0.34f);
        var circleRadius = Mathf.Sqrt(1f - height * height);
        var softEdge = 0.82f + Hash01(index, 53) * 0.24f;

        return new Vector3(
            lobeCenter.x + Mathf.Cos(angle) * circleRadius * radius * lobeScale.x * softEdge,
            lobeCenter.y + height * radius * lobeScale.y * softEdge,
            lobeCenter.z + Mathf.Sin(angle) * circleRadius * radius * lobeScale.z * softEdge
        ) + CreateLoosePointOffset(index);
    }

    private float CreatePointSize(int index)
    {
        return pointSize * (0.7f + Hash01(index, 71) * 0.65f);
    }

    private Vector3 GetLobeCenter(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector3(0f, 0f, 0f);
            case 1:
                return new Vector3(-0.95f, -0.08f, 0.02f);
            case 2:
                return new Vector3(0.98f, -0.02f, 0.06f);
            case 3:
                return new Vector3(-0.34f, 0.42f, -0.05f);
            case 4:
                return new Vector3(0.48f, 0.36f, 0.12f);
            case 5:
                return new Vector3(-1.48f, 0.12f, 0.18f);
            default:
                return new Vector3(1.45f, 0.1f, -0.08f);
        }
    }

    private Vector3 GetLobeScale(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector3(1.08f, 0.52f, 0.46f);
            case 1:
                return new Vector3(0.86f, 0.43f, 0.42f);
            case 2:
                return new Vector3(0.9f, 0.48f, 0.42f);
            case 3:
                return new Vector3(0.72f, 0.44f, 0.36f);
            case 4:
                return new Vector3(0.72f, 0.42f, 0.36f);
            case 5:
                return new Vector3(0.58f, 0.34f, 0.3f);
            default:
                return new Vector3(0.6f, 0.34f, 0.3f);
        }
    }

    private Vector3 CreateLoosePointOffset(int index)
    {
        var hasLooseOffset = index % 9 == 0;
        if (!hasLooseOffset)
        {
            return Vector3.zero;
        }

        return new Vector3(
            (Hash01(index, 89) - 0.5f) * 0.22f,
            (Hash01(index, 97) - 0.5f) * 0.12f,
            (Hash01(index, 101) - 0.5f) * 0.18f
        );
    }

    private float Hash01(int index, int salt)
    {
        var value = Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private void DestroyGeneratedObject(Object target)
    {
        Destroy(target);
    }
}
