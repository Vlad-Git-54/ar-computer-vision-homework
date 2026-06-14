// Автор: Марьяновский Владислав Андреевич

using System;
using UnityEngine;
using UnityEngine.UI;

public class WebcamMarkerGameController : MonoBehaviour
{
    [SerializeField] private Transform gameRoot;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private int brightThreshold = 178;
    [SerializeField] private int sampleStep = 4;
    [SerializeField] private float minMarkerSize = 0.18f;
    [SerializeField] private float maxMarkerSize = 0.95f;
    [SerializeField] private float minMarkerDensity = 0.68f;
    [SerializeField] private float maxDarkMarkerDensity = 0.12f;
    [SerializeField] private float markerDistanceFromCamera = 7.2f;
    [SerializeField] private float gameScaleAtMarker = 0.22f;
    [SerializeField] private float followSharpness = 8f;
    [SerializeField] private float markerLostDelay = 0.45f;
    [SerializeField] private string markerHelpText = "Маркер: чистый белый лист А4";
    [SerializeField] private bool setupSceneAutomatically = true;
    [SerializeField] private Vector3 arCameraPosition = new Vector3(0f, 2.8f, -2.5f);
    [SerializeField] private Vector3 arCameraRotation = new Vector3(14f, 0f, 0f);

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private byte[] brightMarkerCells;
    private byte[] darkMarkerCells;
    private byte[] visitedMarkerCells;
    private int[] componentQueue;
    private GameObject backgroundObject;
    private Material backgroundMaterial;
    private Text statusText;
    private Text helpText;
    private bool gameVisible;
    private bool markerWasFound;
    private float lastMarkerFoundTime = -10f;
    private Sprite whiteSprite;

    private struct MarkerObservation
    {
        public bool Found;
        public Vector2 Center;
        public float Size;
        public float Density;
    }

    private struct BrightMarkerComponent
    {
        public bool Found;
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
        public int BrightCount;
        public float BrightDensity;
        public float DarkDensity;
    }

    private void Awake()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (setupSceneAutomatically)
        {
            SetupCameraForMarkerScene();
            CreateGameRootFromSceneIfNeeded();
        }
    }

    private void Start()
    {
        CreateInterface();
        StartWebcam();
        SetGameVisible(false, true);
        UpdateStatus(false, "Поднесите белый лист А4 к веб-камере");
    }

    private void Update()
    {
        if (sceneCamera == null || gameRoot == null)
        {
            UpdateStatus(false, "Не назначены камера или игровой корень");
            return;
        }

        UpdateBackgroundScale();

        var marker = DetectMarker();
        if (marker.Found)
        {
            lastMarkerFoundTime = Time.unscaledTime;
            markerWasFound = true;
            PlaceGameOnMarker(marker);
        }

        var markerIsVisible = Time.unscaledTime - lastMarkerFoundTime <= markerLostDelay;
        SetGameVisible(markerIsVisible, !markerIsVisible && Time.frameCount % 15 == 0);

        if (markerIsVisible)
        {
            UpdateStatus(true, "Лист А4 найден, игра размещена на нём");
            return;
        }

        UpdateStatus(false, markerWasFound ? "Лист потерян, покажите его камере снова" : "Поднесите белый лист А4 к веб-камере");
    }

    private void OnDestroy()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }

        if (backgroundMaterial != null)
        {
            Destroy(backgroundMaterial);
            backgroundMaterial = null;
        }
    }

    private void SetupCameraForMarkerScene()
    {
        if (sceneCamera == null)
        {
            return;
        }

        var cameraFollow = sceneCamera.GetComponent<ThirdPersonCameraFollow>();
        if (cameraFollow != null)
        {
            Destroy(cameraFollow);
        }

        sceneCamera.transform.position = arCameraPosition;
        sceneCamera.transform.rotation = Quaternion.Euler(arCameraRotation);
        sceneCamera.fieldOfView = 55f;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = new Color(0.05f, 0.07f, 0.09f, 1f);
    }

    private void CreateGameRootFromSceneIfNeeded()
    {
        if (gameRoot != null)
        {
            return;
        }

        var rootObject = new GameObject("AR Marker Game Root");
        rootObject.transform.position = new Vector3(0f, 0f, markerDistanceFromCamera);
        rootObject.transform.rotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one * gameScaleAtMarker;

        var sceneObjects = FindObjectsOfType<GameObject>();
        foreach (var sceneObject in sceneObjects)
        {
            if (sceneObject == rootObject || sceneObject.transform.parent != null || !ShouldMoveToGameRoot(sceneObject.name))
            {
                continue;
            }

            sceneObject.transform.SetParent(rootObject.transform, true);
        }

        gameRoot = rootObject.transform;
    }

    private bool ShouldMoveToGameRoot(string objectName)
    {
        if (objectName == "Main Camera" || objectName == "Directional Light" || objectName == "AR Marker Webcam Controller")
        {
            return false;
        }

        return objectName == "Robot Player"
            || objectName == "Enemy Robot"
            || objectName == "Large Gameplay Floor"
            || objectName == "Back Wall"
            || objectName == "Left Wall"
            || objectName == "Right Wall"
            || objectName == "Middle Wall Left"
            || objectName == "Middle Wall Right"
            || objectName == "Short Wall"
            || objectName == "Static Capsule Obstacle"
            || objectName == "Point Cloud Homework"
            || objectName == "Gameplay HUD and Pause"
            || objectName == "Score HUD and Game Over"
            || objectName == "Scene Audio Controller"
            || objectName.StartsWith("Coin");
    }

    private void StartWebcam()
    {
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            UpdateStatus(false, "Веб-камера не найдена");
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
        backgroundObject.name = "Webcam Background";
        backgroundObject.transform.SetParent(sceneCamera.transform, false);
        backgroundObject.transform.localPosition = new Vector3(0f, 0f, markerDistanceFromCamera + 15f);
        backgroundObject.transform.localRotation = Quaternion.identity;

        var colliderComponent = backgroundObject.GetComponent<Collider>();
        if (colliderComponent != null)
        {
            Destroy(colliderComponent);
        }

        backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        backgroundMaterial.name = "Webcam Background Material";
        backgroundMaterial.mainTexture = webcamTexture;

        var backgroundRenderer = backgroundObject.GetComponent<MeshRenderer>();
        if (backgroundRenderer != null)
        {
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            backgroundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backgroundRenderer.receiveShadows = false;
        }

        UpdateBackgroundScale();
    }

    private void UpdateBackgroundScale()
    {
        if (sceneCamera == null || backgroundObject == null)
        {
            return;
        }

        var distance = Mathf.Abs(backgroundObject.transform.localPosition.z);
        var height = 2f * distance * Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        var width = height * sceneCamera.aspect;
        backgroundObject.transform.localScale = new Vector3(width, height, 1f);
    }

    private MarkerObservation DetectMarker()
    {
        var observation = new MarkerObservation();
        if (webcamTexture == null || !webcamTexture.isPlaying || webcamTexture.width < 100 || webcamTexture.height < 100)
        {
            return observation;
        }

        var width = webcamTexture.width;
        var height = webcamTexture.height;
        var pixelCount = width * height;
        if (cameraPixels == null || cameraPixels.Length != pixelCount)
        {
            cameraPixels = new Color32[pixelCount];
        }

        webcamTexture.GetPixels32(cameraPixels);

        var gridWidth = Mathf.Max(1, width / sampleStep);
        var gridHeight = Mathf.Max(1, height / sampleStep);
        var gridLength = gridWidth * gridHeight;
        EnsureMarkerGridSize(gridLength);

        for (var gridY = 0; gridY < gridHeight; gridY++)
        {
            var y = gridY * sampleStep;
            var lineStart = y * width;
            for (var gridX = 0; gridX < gridWidth; gridX++)
            {
                var x = gridX * sampleStep;
                var color = cameraPixels[lineStart + x];
                var luminance = (color.r * 30 + color.g * 59 + color.b * 11) / 100;
                var cellIndex = gridY * gridWidth + gridX;

                if (IsBrightMarkerPixel(color, luminance))
                {
                    brightMarkerCells[cellIndex] = 1;
                }

                if (luminance <= 72)
                {
                    darkMarkerCells[cellIndex] = 1;
                }
            }
        }

        var bestObservation = FindBestBrightMarkerComponent(gridWidth, gridHeight, width, height);

        if (!bestObservation.Found)
        {
            return observation;
        }

        return bestObservation;
    }

    private void EnsureMarkerGridSize(int gridLength)
    {
        if (brightMarkerCells == null || brightMarkerCells.Length != gridLength)
        {
            brightMarkerCells = new byte[gridLength];
            darkMarkerCells = new byte[gridLength];
            visitedMarkerCells = new byte[gridLength];
            componentQueue = new int[gridLength];
            return;
        }

        Array.Clear(brightMarkerCells, 0, gridLength);
        Array.Clear(darkMarkerCells, 0, gridLength);
        Array.Clear(visitedMarkerCells, 0, gridLength);
    }

    private bool IsBrightMarkerPixel(Color32 color, int luminance)
    {
        return luminance >= brightThreshold && color.r >= 118 && color.g >= 118 && color.b >= 118;
    }

    private MarkerObservation FindBestBrightMarkerComponent(int gridWidth, int gridHeight, int imageWidth, int imageHeight)
    {
        var bestObservation = new MarkerObservation();
        var bestScore = 0f;

        for (var startIndex = 0; startIndex < brightMarkerCells.Length; startIndex++)
        {
            if (brightMarkerCells[startIndex] == 0 || visitedMarkerCells[startIndex] != 0)
            {
                continue;
            }

            var component = ReadBrightComponent(startIndex, gridWidth, gridHeight);
            if (!component.Found)
            {
                continue;
            }

            var marker = CreateObservationFromComponent(component, gridWidth, imageWidth, imageHeight);
            if (!marker.Found)
            {
                continue;
            }

            var componentWidth = component.MaxX - component.MinX + 1;
            var componentHeight = component.MaxY - component.MinY + 1;
            var aspect = componentWidth / (float)Mathf.Max(1, componentHeight);
            var aspectBonus = GetA4AspectScore(aspect);
            var score = component.BrightCount * component.BrightDensity * (1f + aspectBonus);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestObservation = marker;
        }

        return bestObservation;
    }

    private BrightMarkerComponent ReadBrightComponent(int startIndex, int gridWidth, int gridHeight)
    {
        var result = new BrightMarkerComponent();
        var head = 0;
        var tail = 0;
        var minX = gridWidth;
        var maxX = 0;
        var minY = gridHeight;
        var maxY = 0;
        var brightCount = 0;

        componentQueue[tail++] = startIndex;
        visitedMarkerCells[startIndex] = 1;

        while (head < tail)
        {
            var index = componentQueue[head++];
            var x = index % gridWidth;
            var y = index / gridWidth;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
            brightCount++;

            AddBrightNeighbor(index - 1, x > 0, ref tail);
            AddBrightNeighbor(index + 1, x < gridWidth - 1, ref tail);
            AddBrightNeighbor(index - gridWidth, y > 0, ref tail);
            AddBrightNeighbor(index + gridWidth, y < gridHeight - 1, ref tail);
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var area = Mathf.Max(1, width * height);
        var brightDensity = brightCount / (float)area;
        var darkDensity = CountDarkCells(minX, maxX, minY, maxY, gridWidth) / (float)area;

        result.Found = brightCount >= 160 && brightDensity >= minMarkerDensity && darkDensity <= maxDarkMarkerDensity;
        result.MinX = minX;
        result.MaxX = maxX;
        result.MinY = minY;
        result.MaxY = maxY;
        result.BrightCount = brightCount;
        result.BrightDensity = brightDensity;
        result.DarkDensity = darkDensity;
        return result;
    }

    private void AddBrightNeighbor(int index, bool canUse, ref int tail)
    {
        if (!canUse || brightMarkerCells[index] == 0 || visitedMarkerCells[index] != 0)
        {
            return;
        }

        visitedMarkerCells[index] = 1;
        componentQueue[tail++] = index;
    }

    private int CountDarkCells(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        var count = 0;
        for (var y = minY; y <= maxY; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = minX; x <= maxX; x++)
            {
                count += darkMarkerCells[rowStart + x];
            }
        }

        return count;
    }

    private MarkerObservation CreateObservationFromComponent(BrightMarkerComponent component, int gridWidth, int imageWidth, int imageHeight)
    {
        var observation = new MarkerObservation();
        var width = Mathf.Max(1, component.MaxX - component.MinX + 1);
        var height = Mathf.Max(1, component.MaxY - component.MinY + 1);
        var aspect = width / (float)height;
        var markerWidth = width * sampleStep;
        var markerHeight = height * sampleStep;
        var markerSize = Mathf.Max(markerWidth / (float)imageWidth, markerHeight / (float)imageHeight);

        if (markerSize < minMarkerSize || markerSize > maxMarkerSize || !IsA4AspectAccepted(aspect))
        {
            return observation;
        }

        observation.Found = true;
        observation.Center = new Vector2((component.MinX + component.MaxX + 1f) * sampleStep * 0.5f / imageWidth, (component.MinY + component.MaxY + 1f) * sampleStep * 0.5f / imageHeight);
        observation.Size = markerSize;
        observation.Density = component.BrightDensity + component.DarkDensity;
        return observation;
    }

    private bool IsA4AspectAccepted(float aspect)
    {
        const float landscapeA4 = 1.414f;
        const float portraitA4 = 0.707f;
        return Mathf.Abs(aspect - landscapeA4) <= 0.55f || Mathf.Abs(aspect - portraitA4) <= 0.3f;
    }

    private float GetA4AspectScore(float aspect)
    {
        const float landscapeA4 = 1.414f;
        const float portraitA4 = 0.707f;
        var landscapeScore = 1f - Mathf.Abs(aspect - landscapeA4) / 0.55f;
        var portraitScore = 1f - Mathf.Abs(aspect - portraitA4) / 0.3f;
        return Mathf.Clamp01(Mathf.Max(landscapeScore, portraitScore));
    }

    private void PlaceGameOnMarker(MarkerObservation marker)
    {
        var targetPosition = sceneCamera.ViewportToWorldPoint(new Vector3(marker.Center.x, marker.Center.y, markerDistanceFromCamera));
        var targetRotation = Quaternion.Euler(0f, sceneCamera.transform.eulerAngles.y, 0f);
        var markerScale = Mathf.InverseLerp(minMarkerSize, maxMarkerSize, marker.Size);
        var targetScale = gameScaleAtMarker * Mathf.Lerp(0.82f, 1.35f, markerScale);
        var blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);

        gameRoot.position = Vector3.Lerp(gameRoot.position, targetPosition, blend);
        gameRoot.rotation = Quaternion.Slerp(gameRoot.rotation, targetRotation, blend);
        gameRoot.localScale = Vector3.Lerp(gameRoot.localScale, Vector3.one * targetScale, blend);
    }

    private void SetGameVisible(bool visible, bool force)
    {
        if (gameRoot == null)
        {
            return;
        }

        if (!force && gameVisible == visible)
        {
            return;
        }

        gameVisible = visible;

        foreach (var currentRenderer in gameRoot.GetComponentsInChildren<Renderer>(true))
        {
            currentRenderer.enabled = visible;
        }

        foreach (var currentCollider in gameRoot.GetComponentsInChildren<Collider>(true))
        {
            currentCollider.enabled = visible;
        }

        foreach (var currentRigidbody in gameRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            currentRigidbody.velocity = Vector3.zero;
            currentRigidbody.angularVelocity = Vector3.zero;
            if (visible)
            {
                currentRigidbody.isKinematic = false;
                currentRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            else
            {
                currentRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                currentRigidbody.isKinematic = true;
            }
        }

        SetRuntimeGameCanvasVisible("Homework 10 HUD Canvas", visible);
        SetRuntimeGameCanvasVisible("Homework 12 Score Canvas", visible);
    }

    private void SetRuntimeGameCanvasVisible(string canvasName, bool visible)
    {
        var canvasObject = GameObject.Find(canvasName);
        if (canvasObject == null)
        {
            return;
        }

        var canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = visible;
        }
    }

    private void CreateInterface()
    {
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

        var canvasObject = new GameObject("AR Marker HUD Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 180;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel(canvasObject.transform);
        statusText = CreateText("Marker Status", panel.transform, "Поднесите белый лист А4 к веб-камере", 24, FontStyle.Bold, Color.white);
        SetStretch(statusText.rectTransform, new Vector2(22f, 38f), new Vector2(22f, 12f));

        helpText = CreateText("Marker Help", panel.transform, markerHelpText, 18, FontStyle.Normal, new Color(0.82f, 0.9f, 1f, 1f));
        SetStretch(helpText.rectTransform, new Vector2(22f, 12f), new Vector2(22f, 52f));
    }

    private Image CreatePanel(Transform parent)
    {
        var panelObject = new GameObject("AR Marker Status Panel");
        panelObject.transform.SetParent(parent, false);

        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(720f, 94f);
        rect.anchoredPosition = new Vector2(0f, 26f);

        var image = panelObject.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = new Color(0.04f, 0.06f, 0.08f, 0.82f);
        return image;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle style, Color color)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        var label = textObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    private void UpdateStatus(bool markerFound, string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = markerFound ? new Color(0.4f, 1f, 0.62f, 1f) : Color.white;
        }

        if (helpText != null)
        {
            helpText.text = markerHelpText;
        }
    }
}
