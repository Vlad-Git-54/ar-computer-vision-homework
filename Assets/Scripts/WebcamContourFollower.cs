// Автор: Марьяновский Владислав Андреевич

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class WebcamContourFollower : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Transform followerObject;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private int sampleStep = 5;
    [SerializeField] private int edgeThreshold = 72;
    [SerializeField] private int darkObjectThreshold = 125;
    [SerializeField] private int minimumEdgeCells = 32;
    [SerializeField] private float minimumContourSize = 0.07f;
    [SerializeField] private float maximumContourSize = 0.78f;
    [SerializeField] private float minimumEdgeDensity = 0.035f;
    [SerializeField] private float minimumObjectFill = 0.18f;
    [SerializeField] private float maximumObjectFill = 0.86f;
    [SerializeField] private float movementSpeed = 3.8f;
    [SerializeField] private Vector2 worldXRange = new Vector2(-4.6f, 4.6f);
    [SerializeField] private Vector2 worldZRange = new Vector2(-2.8f, 2.8f);
    [SerializeField] private float followerHeight = 0.55f;
    [SerializeField] private bool buildDemoScene = true;

    private const float BackgroundDistance = 26f;
    private const float ContourLineWidth = 5f;

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private byte[] rawEdgeCells;
    private byte[] edgeCells;
    private byte[] visitedCells;
    private int[] componentQueue;
    private GameObject backgroundObject;
    private Material backgroundMaterial;
    private Material followerMaterial;
    private Material floorMaterial;
    private Material targetMaterial;
    private Transform targetMarker;
    private Text statusText;
    private Image[] contourLines;
    private Sprite whiteSprite;
    private Vector3 currentTarget;
    private string currentHint = "";

    private struct ContourResult
    {
        public bool Found;
        public int MinCellX;
        public int MaxCellX;
        public int MinCellY;
        public int MaxCellY;
        public int EdgeCount;
        public float NormalizedMinX;
        public float NormalizedMaxX;
        public float NormalizedMinY;
        public float NormalizedMaxY;
    }

    private void Awake()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        ConfigureCamera();
    }

    private void Start()
    {
        if (buildDemoScene)
        {
            CreateDemoScene();
        }

        CreateInterface();
        StartWebcam();
        currentTarget = followerObject != null ? followerObject.position : Vector3.zero;
        UpdateStatus("Поднесите темный объект на светлом фоне к веб-камере");
    }

    private void Update()
    {
        UpdateBackgroundScale();

        if (TryFindContourSafely(out var contour))
        {
            currentTarget = ConvertContourToWorld(contour);
            MoveFollowerToTarget();
            UpdateContourBox(contour, true);
            UpdateStatus("Контур найден. 3D-объект движется к найденному объекту");
            return;
        }

        UpdateContourBox(contour, false);
        UpdateStatus(string.IsNullOrEmpty(currentHint) ? "Контур не найден" : currentHint);
    }

    private void OnDestroy()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }

        DestroyRuntimeObject(backgroundMaterial);
        DestroyRuntimeObject(followerMaterial);
        DestroyRuntimeObject(floorMaterial);
        DestroyRuntimeObject(targetMaterial);
    }

    private void ConfigureCamera()
    {
        sceneCamera.transform.position = new Vector3(0f, 5.5f, -7.5f);
        sceneCamera.transform.rotation = Quaternion.Euler(36f, 0f, 0f);
        sceneCamera.fieldOfView = 58f;
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 1000f;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
    }

    private void CreateDemoScene()
    {
        if (FindObjectOfType<Light>() == null)
        {
            var lightObject = new GameObject("Directional Light");
            var sceneLight = lightObject.AddComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.15f;
            sceneLight.color = new Color(1f, 0.96f, 0.86f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 18f);
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Contour Tracking Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(1.15f, 1f, 0.75f);

        floorMaterial = new Material(Shader.Find("Standard"));
        floorMaterial.name = "Contour Floor Runtime Material";
        floorMaterial.color = new Color(0.38f, 0.55f, 0.46f, 1f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        if (followerObject == null)
        {
            var follower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            follower.name = "3D Object Following Contour";
            follower.transform.position = new Vector3(0f, followerHeight, 0f);
            follower.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            followerObject = follower.transform;

            followerMaterial = new Material(Shader.Find("Standard"));
            followerMaterial.name = "Contour Follower Runtime Material";
            followerMaterial.color = new Color(1f, 0.66f, 0.12f, 1f);
            followerMaterial.SetColor("_EmissionColor", new Color(0.25f, 0.12f, 0.02f, 1f));
            followerMaterial.EnableKeyword("_EMISSION");
            follower.GetComponent<Renderer>().sharedMaterial = followerMaterial;
        }

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Contour Target Marker";
        marker.transform.position = new Vector3(0f, 0.03f, 0f);
        marker.transform.localScale = new Vector3(0.55f, 0.02f, 0.55f);
        targetMarker = marker.transform;

        var markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            DestroyRuntimeObject(markerCollider);
        }

        targetMaterial = new Material(Shader.Find("Standard"));
        targetMaterial.name = "Contour Target Runtime Material";
        targetMaterial.color = new Color(0.12f, 0.9f, 1f, 0.65f);
        targetMaterial.SetColor("_EmissionColor", new Color(0.03f, 0.5f, 0.7f, 1f));
        targetMaterial.EnableKeyword("_EMISSION");
        ConfigureTransparentMaterial(targetMaterial);
        marker.GetComponent<Renderer>().sharedMaterial = targetMaterial;
    }

    private void StartWebcam()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            currentHint = "Веб-камера не найдена";
            return;
        }

        webcamTexture = new WebCamTexture(devices[0].name, requestedWidth, requestedHeight, requestedFps);
        webcamTexture.Play();
        CreateWebcamBackground();
    }

    private void CreateWebcamBackground()
    {
        if (sceneCamera == null || webcamTexture == null)
        {
            return;
        }

        backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundObject.name = "Webcam Contour Background";
        backgroundObject.transform.SetParent(sceneCamera.transform, false);
        backgroundObject.transform.localPosition = new Vector3(0f, 0f, BackgroundDistance);
        backgroundObject.transform.localRotation = Quaternion.identity;

        var backgroundCollider = backgroundObject.GetComponent<Collider>();
        if (backgroundCollider != null)
        {
            DestroyRuntimeObject(backgroundCollider);
        }

        var backgroundRenderer = backgroundObject.GetComponent<MeshRenderer>();
        backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        backgroundRenderer.receiveShadows = false;

        backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        backgroundMaterial.name = "Webcam Contour Runtime Material";
        backgroundMaterial.mainTexture = webcamTexture;
        backgroundRenderer.sharedMaterial = backgroundMaterial;
        UpdateBackgroundScale();
    }

    private void UpdateBackgroundScale()
    {
        if (sceneCamera == null || backgroundObject == null)
        {
            return;
        }

        var height = 2f * BackgroundDistance * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var width = height * sceneCamera.aspect;
        backgroundObject.transform.localScale = new Vector3(width, height, 1f);
    }

    private bool TryFindContourSafely(out ContourResult contour)
    {
        try
        {
            return TryFindContour(out contour);
        }
        catch (Exception exception)
        {
            contour = new ContourResult();
            currentHint = "Кадр камеры пропущен: " + exception.Message;
            return false;
        }
    }

    private bool TryFindContour(out ContourResult contour)
    {
        contour = new ContourResult();

        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            currentHint = "Камера еще запускается";
            return false;
        }

        var textureWidth = webcamTexture.width;
        var textureHeight = webcamTexture.height;
        if (textureWidth < 64 || textureHeight < 64)
        {
            currentHint = "Камера еще запускается";
            return false;
        }

        cameraPixels = webcamTexture.GetPixels32();
        var gridWidth = Mathf.Max(3, textureWidth / sampleStep);
        var gridHeight = Mathf.Max(3, textureHeight / sampleStep);
        var totalCells = gridWidth * gridHeight;
        EnsureDetectionBuffers(totalCells);
        Array.Clear(rawEdgeCells, 0, totalCells);
        Array.Clear(edgeCells, 0, totalCells);
        Array.Clear(visitedCells, 0, totalCells);

        var foregroundCells = BuildForegroundMap(gridWidth, gridHeight, textureWidth, textureHeight);
        if (foregroundCells < minimumEdgeCells)
        {
            currentHint = "Покажите темный предмет на белом листе или светлой стене";
            return false;
        }

        ExpandEdgeMap(gridWidth, gridHeight);

        var bestScore = 0f;
        var bestContour = new ContourResult();
        for (var y = 1; y < gridHeight - 1; y++)
        {
            for (var x = 1; x < gridWidth - 1; x++)
            {
                var index = y * gridWidth + x;
                if (edgeCells[index] == 0 || visitedCells[index] != 0)
                {
                    continue;
                }

                var candidate = SearchConnectedContour(x, y, gridWidth, gridHeight, textureWidth, textureHeight);
                if (!IsUsableContour(candidate, gridWidth, gridHeight, out var score))
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestContour = candidate;
                }
            }
        }

        if (!bestContour.Found)
        {
            currentHint = "Покажите один крупный темный объект на светлом фоне";
            return false;
        }

        contour = bestContour;
        currentHint = "";
        return true;
    }

    private int BuildForegroundMap(int gridWidth, int gridHeight, int textureWidth, int textureHeight)
    {
        var luminanceSum = 0f;
        var sampleCount = 0;

        for (var y = 1; y < gridHeight - 1; y++)
        {
            for (var x = 1; x < gridWidth - 1; x++)
            {
                var pixelX = Mathf.Clamp(x * sampleStep, 1, textureWidth - 2);
                var pixelY = Mathf.Clamp(y * sampleStep, 1, textureHeight - 2);
                luminanceSum += GetLuminance(pixelX, pixelY, textureWidth, textureHeight);
                sampleCount++;
            }
        }

        var averageLuminance = sampleCount > 0 ? luminanceSum / sampleCount : darkObjectThreshold;
        var adaptiveThreshold = Mathf.RoundToInt(averageLuminance - edgeThreshold * 0.45f);
        var threshold = Mathf.Clamp(Mathf.Min(darkObjectThreshold, adaptiveThreshold), 0, 255);
        var foregroundCells = 0;

        for (var y = 1; y < gridHeight - 1; y++)
        {
            for (var x = 1; x < gridWidth - 1; x++)
            {
                var pixelX = Mathf.Clamp(x * sampleStep, 1, textureWidth - 2);
                var pixelY = Mathf.Clamp(y * sampleStep, 1, textureHeight - 2);
                var luminance = GetLuminance(pixelX, pixelY, textureWidth, textureHeight);

                if (luminance <= threshold)
                {
                    rawEdgeCells[y * gridWidth + x] = 1;
                    foregroundCells++;
                }
            }
        }

        return foregroundCells;
    }

    private void ExpandEdgeMap(int gridWidth, int gridHeight)
    {
        for (var y = 1; y < gridHeight - 1; y++)
        {
            for (var x = 1; x < gridWidth - 1; x++)
            {
                var hasEdgeNearCell = false;
                for (var offsetY = -1; offsetY <= 1 && !hasEdgeNearCell; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (rawEdgeCells[(y + offsetY) * gridWidth + x + offsetX] != 0)
                        {
                            hasEdgeNearCell = true;
                            break;
                        }
                    }
                }

                if (hasEdgeNearCell)
                {
                    edgeCells[y * gridWidth + x] = 1;
                }
            }
        }
    }

    private ContourResult SearchConnectedContour(int startX, int startY, int gridWidth, int gridHeight, int textureWidth, int textureHeight)
    {
        var head = 0;
        var tail = 0;
        var startIndex = startY * gridWidth + startX;
        componentQueue[tail++] = startIndex;
        visitedCells[startIndex] = 1;

        var minX = startX;
        var maxX = startX;
        var minY = startY;
        var maxY = startY;
        var edgeCount = 0;

        while (head < tail)
        {
            var currentIndex = componentQueue[head++];
            var currentX = currentIndex % gridWidth;
            var currentY = currentIndex / gridWidth;
            edgeCount++;

            minX = Mathf.Min(minX, currentX);
            maxX = Mathf.Max(maxX, currentX);
            minY = Mathf.Min(minY, currentY);
            maxY = Mathf.Max(maxY, currentY);

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var nextX = currentX + offsetX;
                    var nextY = currentY + offsetY;
                    if (nextX <= 0 || nextX >= gridWidth - 1 || nextY <= 0 || nextY >= gridHeight - 1)
                    {
                        continue;
                    }

                    var nextIndex = nextY * gridWidth + nextX;
                    if (visitedCells[nextIndex] != 0 || edgeCells[nextIndex] == 0)
                    {
                        continue;
                    }

                    visitedCells[nextIndex] = 1;
                    componentQueue[tail++] = nextIndex;
                }
            }
        }

        var result = new ContourResult
        {
            Found = true,
            MinCellX = minX,
            MaxCellX = maxX,
            MinCellY = minY,
            MaxCellY = maxY,
            EdgeCount = edgeCount,
            NormalizedMinX = Mathf.Clamp01((minX * sampleStep) / (float)textureWidth),
            NormalizedMaxX = Mathf.Clamp01(((maxX + 1) * sampleStep) / (float)textureWidth),
            NormalizedMinY = Mathf.Clamp01((minY * sampleStep) / (float)textureHeight),
            NormalizedMaxY = Mathf.Clamp01(((maxY + 1) * sampleStep) / (float)textureHeight)
        };

        return result;
    }

    private bool IsUsableContour(ContourResult contour, int gridWidth, int gridHeight, out float score)
    {
        score = 0f;
        if (!contour.Found || contour.EdgeCount < minimumEdgeCells)
        {
            return false;
        }

        var cellWidth = contour.MaxCellX - contour.MinCellX + 1;
        var cellHeight = contour.MaxCellY - contour.MinCellY + 1;
        var normalizedWidth = cellWidth / (float)gridWidth;
        var normalizedHeight = cellHeight / (float)gridHeight;
        var area = cellWidth * cellHeight;
        var fill = contour.EdgeCount / Mathf.Max(1f, area);
        var minimumFill = Mathf.Max(minimumEdgeDensity, minimumObjectFill);

        if (normalizedWidth < minimumContourSize || normalizedHeight < minimumContourSize)
        {
            return false;
        }

        if (normalizedWidth > maximumContourSize || normalizedHeight > maximumContourSize)
        {
            return false;
        }

        if (fill < minimumFill || fill > maximumObjectFill)
        {
            return false;
        }

        score = contour.EdgeCount + area * 0.12f;
        return true;
    }

    private int GetLuminance(int x, int y, int width, int height)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        var pixel = cameraPixels[y * width + x];
        return (pixel.r * 30 + pixel.g * 59 + pixel.b * 11) / 100;
    }

    private void EnsureDetectionBuffers(int totalCells)
    {
        if (rawEdgeCells == null || rawEdgeCells.Length != totalCells)
        {
            rawEdgeCells = new byte[totalCells];
            edgeCells = new byte[totalCells];
            visitedCells = new byte[totalCells];
            componentQueue = new int[totalCells];
        }
    }

    private Vector3 ConvertContourToWorld(ContourResult contour)
    {
        var normalizedX = (contour.NormalizedMinX + contour.NormalizedMaxX) * 0.5f;
        var normalizedY = (contour.NormalizedMinY + contour.NormalizedMaxY) * 0.5f;
        var x = Mathf.Lerp(worldXRange.x, worldXRange.y, normalizedX);
        var z = Mathf.Lerp(worldZRange.x, worldZRange.y, normalizedY);
        return new Vector3(x, followerHeight, z);
    }

    private void MoveFollowerToTarget()
    {
        if (followerObject == null)
        {
            return;
        }

        followerObject.position = Vector3.MoveTowards(followerObject.position, currentTarget, movementSpeed * Time.deltaTime);
        var flatDirection = currentTarget - followerObject.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude > 0.001f)
        {
            followerObject.rotation = Quaternion.Slerp(followerObject.rotation, Quaternion.LookRotation(flatDirection), 12f * Time.deltaTime);
        }

        if (targetMarker != null)
        {
            targetMarker.position = new Vector3(currentTarget.x, 0.03f, currentTarget.z);
        }
    }

    private void CreateInterface()
    {
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));

        var canvasObject = new GameObject("Contour Tracking HUD Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var panel = CreateUiObject("Contour Status Panel", canvasObject.transform);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 42f);
        panel.sizeDelta = new Vector2(660f, 86f);

        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = whiteSprite;
        panelImage.color = new Color(0.05f, 0.08f, 0.1f, 0.88f);

        var title = CreateText("Contour Status Text", panel);
        title.anchorMin = new Vector2(0f, 0f);
        title.anchorMax = new Vector2(1f, 1f);
        title.offsetMin = new Vector2(24f, 12f);
        title.offsetMax = new Vector2(-24f, -12f);
        statusText = title.GetComponent<Text>();
        statusText.fontSize = 28;
        statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.MiddleLeft;

        CreateContourLines(canvasObject.transform);
    }

    private RectTransform CreateUiObject(string objectName, Transform parent)
    {
        var uiObject = new GameObject(objectName);
        uiObject.transform.SetParent(parent, false);
        return uiObject.AddComponent<RectTransform>();
    }

    private RectTransform CreateText(string objectName, RectTransform parent)
    {
        var textRect = CreateUiObject(objectName, parent);
        var text = textRect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "";
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return textRect;
    }

    private void CreateContourLines(Transform parent)
    {
        contourLines = new Image[4];
        for (var i = 0; i < contourLines.Length; i++)
        {
            var lineRect = CreateUiObject("Detected Contour Line " + (i + 1), parent);
            var lineImage = lineRect.gameObject.AddComponent<Image>();
            lineImage.sprite = whiteSprite;
            lineImage.color = new Color(0.2f, 1f, 0.36f, 0.95f);
            lineImage.raycastTarget = false;
            lineRect.gameObject.SetActive(false);
            contourLines[i] = lineImage;
        }
    }

    private void UpdateContourBox(ContourResult contour, bool visible)
    {
        if (contourLines == null)
        {
            return;
        }

        foreach (var line in contourLines)
        {
            line.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        var minX = Mathf.Clamp01(contour.NormalizedMinX);
        var maxX = Mathf.Clamp01(contour.NormalizedMaxX);
        var minY = Mathf.Clamp01(contour.NormalizedMinY);
        var maxY = Mathf.Clamp01(contour.NormalizedMaxY);

        SetHorizontalLine(contourLines[0].rectTransform, minX, maxX, maxY);
        SetHorizontalLine(contourLines[1].rectTransform, minX, maxX, minY);
        SetVerticalLine(contourLines[2].rectTransform, minY, maxY, minX);
        SetVerticalLine(contourLines[3].rectTransform, minY, maxY, maxX);
    }

    private void SetHorizontalLine(RectTransform rectTransform, float minX, float maxX, float y)
    {
        rectTransform.anchorMin = new Vector2(minX, y);
        rectTransform.anchorMax = new Vector2(maxX, y);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(0f, ContourLineWidth);
    }

    private void SetVerticalLine(RectTransform rectTransform, float minY, float maxY, float x)
    {
        rectTransform.anchorMin = new Vector2(x, minY);
        rectTransform.anchorMax = new Vector2(x, maxY);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(ContourLineWidth, 0f);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }
}
