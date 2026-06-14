// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class PointCloudShowcase : MonoBehaviour
{
    [SerializeField] private int pointCount = 420;
    [SerializeField] private float pointSize = 0.11f;
    [SerializeField] private Color pointColor = new Color(0.88f, 0.98f, 1f, 1f);
    [SerializeField] private Color emissionColor = new Color(0.42f, 0.86f, 1f, 1f);

    private const string GeneratedPointPrefix = "Point Cloud Dot ";
    private const string GeneratedPuffPrefix = "Cloud Puff ";
    private Material pointMaterial;
    private Material puffMaterial;

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

        if (puffMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(puffMaterial);
            }

            puffMaterial = null;
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
        var cloudBodyMaterial = GetPuffMaterial();

        for (var i = 0; i < GetPuffCount(); i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = GeneratedPuffPrefix + (i + 1).ToString("000");
            puff.hideFlags = HideFlags.DontSave;
            puff.transform.SetParent(transform, false);
            puff.transform.localPosition = GetPuffCenter(i);
            puff.transform.localScale = GetPuffScale(i);

            var puffCollider = puff.GetComponent<Collider>();
            if (puffCollider != null)
            {
                DestroyGeneratedObject(puffCollider);
            }

            var puffRenderer = puff.GetComponent<MeshRenderer>();
            if (puffRenderer != null)
            {
                puffRenderer.sharedMaterial = cloudBodyMaterial;
                puffRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puffRenderer.receiveShadows = false;
            }
        }

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
            if (child.name.StartsWith(GeneratedPointPrefix) || child.name.StartsWith(GeneratedPuffPrefix))
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

    private Material GetPuffMaterial()
    {
        if (puffMaterial != null)
        {
            return puffMaterial;
        }

        puffMaterial = new Material(Shader.Find("Standard"));
        puffMaterial.name = "Cloud Puff Runtime Material";
        puffMaterial.hideFlags = HideFlags.DontSave;
        puffMaterial.color = new Color(0.94f, 0.99f, 1f, 0.72f);
        puffMaterial.SetColor("_EmissionColor", new Color(0.18f, 0.34f, 0.42f, 1f));
        puffMaterial.EnableKeyword("_EMISSION");
        ConfigureTransparentMaterial(puffMaterial);
        return puffMaterial;
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private Vector3 CreatePointPosition(int index)
    {
        var lobeIndex = index % GetPuffCount();
        var lobeCenter = GetPuffCenter(lobeIndex);
        var lobeScale = GetPointLobeScale(lobeIndex);
        var angle = Hash01(index, 11) * Mathf.PI * 2f;
        var height = Hash01(index, 23) * 2f - 1f;
        var radius = Mathf.Pow(Hash01(index, 37), 0.24f);
        var circleRadius = Mathf.Sqrt(1f - height * height);
        var softEdge = 0.72f + Hash01(index, 53) * 0.42f;

        return new Vector3(
            lobeCenter.x + Mathf.Cos(angle) * circleRadius * radius * lobeScale.x * softEdge,
            lobeCenter.y + height * radius * lobeScale.y * softEdge,
            lobeCenter.z + Mathf.Sin(angle) * circleRadius * radius * lobeScale.z * softEdge
        ) + CreateLoosePointOffset(index);
    }

    private float CreatePointSize(int index)
    {
        return pointSize * (0.58f + Hash01(index, 71) * 0.75f);
    }

    private int GetPuffCount()
    {
        return 16;
    }

    private Vector3 GetPuffCenter(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector3(0f, 0f, 0f);
            case 1:
                return new Vector3(-0.82f, -0.12f, 0.03f);
            case 2:
                return new Vector3(0.84f, -0.08f, 0.02f);
            case 3:
                return new Vector3(-0.36f, 0.42f, -0.04f);
            case 4:
                return new Vector3(0.42f, 0.38f, 0.08f);
            case 5:
                return new Vector3(-1.46f, 0.04f, 0.13f);
            case 6:
                return new Vector3(1.42f, 0.02f, -0.08f);
            case 7:
                return new Vector3(-2.04f, -0.02f, -0.03f);
            case 8:
                return new Vector3(2.04f, -0.02f, 0.02f);
            case 9:
                return new Vector3(-1.16f, 0.34f, -0.15f);
            case 10:
                return new Vector3(1.1f, 0.32f, 0.13f);
            case 11:
                return new Vector3(-0.02f, 0.62f, 0.02f);
            case 12:
                return new Vector3(-0.58f, 0.08f, 0.34f);
            case 13:
                return new Vector3(0.62f, 0.1f, -0.3f);
            case 14:
                return new Vector3(-1.74f, 0.22f, 0.04f);
            default:
                return new Vector3(1.74f, 0.2f, 0.06f);
        }
    }

    private Vector3 GetPuffScale(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector3(1.42f, 0.7f, 0.74f);
            case 1:
                return new Vector3(1.28f, 0.6f, 0.66f);
            case 2:
                return new Vector3(1.34f, 0.62f, 0.64f);
            case 3:
                return new Vector3(1.04f, 0.7f, 0.58f);
            case 4:
                return new Vector3(1.06f, 0.66f, 0.58f);
            case 5:
                return new Vector3(0.9f, 0.5f, 0.52f);
            case 6:
                return new Vector3(0.92f, 0.5f, 0.5f);
            case 7:
                return new Vector3(0.72f, 0.38f, 0.42f);
            case 8:
                return new Vector3(0.76f, 0.38f, 0.42f);
            case 9:
                return new Vector3(0.9f, 0.52f, 0.48f);
            case 10:
                return new Vector3(0.88f, 0.52f, 0.48f);
            case 11:
                return new Vector3(0.96f, 0.52f, 0.5f);
            case 12:
                return new Vector3(0.78f, 0.42f, 0.4f);
            case 13:
                return new Vector3(0.82f, 0.44f, 0.4f);
            case 14:
                return new Vector3(0.62f, 0.36f, 0.36f);
            default:
                return new Vector3(0.66f, 0.36f, 0.36f);
        }
    }

    private Vector3 GetPointLobeScale(int index)
    {
        var puffScale = GetPuffScale(index);
        return new Vector3(puffScale.x * 0.56f, puffScale.y * 0.48f, puffScale.z * 0.56f);
    }

    private Vector3 CreateLoosePointOffset(int index)
    {
        var hasLooseOffset = index % 11 == 0;
        if (!hasLooseOffset)
        {
            return Vector3.zero;
        }

        return new Vector3(
            (Hash01(index, 89) - 0.5f) * 0.34f,
            (Hash01(index, 97) - 0.5f) * 0.18f,
            (Hash01(index, 101) - 0.5f) * 0.26f
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
