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
    [SerializeField] private int brightThreshold = 128;
    [SerializeField] private int sampleStep = 6;
    [SerializeField] private float minMarkerSize = 0.1f;
    [SerializeField] private float maxMarkerSize = 0.92f;
    [SerializeField] private float minWhiteArea = 0.035f;
    [SerializeField] private float minMarkerDensity = 0.5f;
    [SerializeField] private float minCenterFill = 0.44f;
    [SerializeField] private float minCornerFill = 0.28f;
    [SerializeField] private float maxSheetLuminanceDeviation = 60f;
    [SerializeField] private int stableFramesToShow = 4;
    [SerializeField] private float markerLostDelay = 2f;
    [SerializeField] private bool keepGameVisibleAfterTrigger = false;
    [SerializeField] private string markerHelpText = "Маркер: белый лист А4";
    [SerializeField] private bool setupSceneAutomatically = true;
    [SerializeField] private Vector3 gameRootPosition = Vector3.zero;
    [SerializeField] private Vector3 gameRootRotation = Vector3.zero;
    [SerializeField] private Vector3 gameRootScale = Vector3.one;

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private byte[] whiteCells;
    private byte[] luminanceCells;
    private byte[] visitedCells;
    private int[] componentQueue;
    private GameObject backgroundObject;
    private Material backgroundMaterial;
    private Text statusText;
    private Text helpText;
    private bool gameVisible;
    private bool markerWasFound;
    private bool gameTriggered;
    private bool markerPauseActive;
    private float timeScaleBeforeMarkerPause = 1f;
    private int stableMarkerFrames;
    private float lastMarkerFoundTime = -10f;
    private float lastDetectionErrorLogTime = -10f;
    private string lastDetectionHint = "";
    private Sprite whiteSprite;

    private struct WhiteMarkerComponent
    {
        public bool Found;
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
        public int WhiteCount;
        public float Density;
        public float CenterFill;
        public float CornerFill;
        public float LuminanceDeviation;
    }

    private void Awake()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (setupSceneAutomatically)
        {
            CreateGameRootFromSceneIfNeeded();
            PlaceGameNormally();
        }
    }

    private void Start()
    {
        CreateInterface();
        StartWebcam();
        SetGameVisible(false, true);
        SetMarkerPause(true);
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

        if (!gameTriggered || !keepGameVisibleAfterTrigger)
        {
            UpdateMarkerTrigger();
        }

        var markerIsRecent = Time.unscaledTime - lastMarkerFoundTime <= markerLostDelay;
        var shouldShowGame = keepGameVisibleAfterTrigger ? gameTriggered : gameTriggered && markerIsRecent;

        if (!markerIsRecent && !keepGameVisibleAfterTrigger)
        {
            stableMarkerFrames = 0;
            gameTriggered = false;
        }

        SetGameVisible(shouldShowGame, false);
        SetMarkerPause(!shouldShowGame);

        if (shouldShowGame)
        {
            UpdateStatus(true, "Лист А4 найден, обычная игра запущена");
            return;
        }

        var waitingMessage = markerWasFound ? "Лист потерян, покажите его камере снова" : "Поднесите белый лист А4 к веб-камере";
        if (!string.IsNullOrEmpty(lastDetectionHint))
        {
            waitingMessage += ". " + lastDetectionHint;
        }

        UpdateStatus(false, waitingMessage);
    }

    private void UpdateMarkerTrigger()
    {
        var markerFound = DetectWhiteSheetSafely();
        if (markerFound)
        {
            markerWasFound = true;
            stableMarkerFrames++;
            lastMarkerFoundTime = Time.unscaledTime;
        }
        else if (Time.unscaledTime - lastMarkerFoundTime > markerLostDelay)
        {
            stableMarkerFrames = 0;
        }

        if (stableMarkerFrames >= stableFramesToShow)
        {
            gameTriggered = true;
            PlaceGameNormally();
        }
    }

    private bool DetectWhiteSheetSafely()
    {
        try
        {
            return DetectWhiteSheet();
        }
        catch (Exception exception)
        {
            if (Time.unscaledTime - lastDetectionErrorLogTime > 1f)
            {
                lastDetectionErrorLogTime = Time.unscaledTime;
                Debug.LogWarning("Кадр камеры пропущен из-за ошибки распознавания листа: " + exception.Message);
            }

            return false;
        }
    }

    private void SetMarkerPause(bool paused)
    {
        if (markerPauseActive == paused)
        {
            return;
        }

        markerPauseActive = paused;
        if (paused)
        {
            if (Time.timeScale > 0f)
            {
                timeScaleBeforeMarkerPause = Time.timeScale;
            }

            Time.timeScale = 0f;
            return;
        }

        Time.timeScale = Mathf.Approximately(timeScaleBeforeMarkerPause, 0f) ? 1f : timeScaleBeforeMarkerPause;
    }

    private void OnDestroy()
    {
        SetMarkerPause(false);

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

    private void CreateGameRootFromSceneIfNeeded()
    {
        if (gameRoot != null)
        {
            return;
        }

        var rootObject = new GameObject("AR Triggered Game Root");
        rootObject.transform.position = gameRootPosition;
        rootObject.transform.rotation = Quaternion.Euler(gameRootRotation);
        rootObject.transform.localScale = gameRootScale;

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

    private void PlaceGameNormally()
    {
        if (gameRoot == null)
        {
            return;
        }

        gameRoot.position = gameRootPosition;
        gameRoot.rotation = Quaternion.Euler(gameRootRotation);
        gameRoot.localScale = gameRootScale;
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
        backgroundObject.transform.localPosition = new Vector3(0f, 0f, 30f);
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

    private bool DetectWhiteSheet()
    {
        lastDetectionHint = "";
        if (webcamTexture == null || !webcamTexture.isPlaying || webcamTexture.width < 100 || webcamTexture.height < 100)
        {
            lastDetectionHint = "Камера ещё запускается";
            return false;
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
            for (var gridX = 0; gridX < gridWidth; gridX++)
            {
                var x = gridX * sampleStep;
                var color = ReadAverageCellColor(x, y, width, height);
                var luminance = (color.r * 30 + color.g * 59 + color.b * 11) / 100;
                var cellIndex = gridY * gridWidth + gridX;
                luminanceCells[cellIndex] = (byte)Mathf.Clamp(luminance, 0, 255);

                if (IsWhiteSheetPixel(color, luminance))
                {
                    whiteCells[cellIndex] = 1;
                }
            }
        }

        var markerFound = FindBestWhiteSheetComponent(gridWidth, gridHeight);
        if (!markerFound)
        {
            lastDetectionHint = "Белый лист пока не найден";
        }

        return markerFound;
    }

    private void EnsureMarkerGridSize(int gridLength)
    {
        if (whiteCells == null || whiteCells.Length != gridLength)
        {
            whiteCells = new byte[gridLength];
            luminanceCells = new byte[gridLength];
            visitedCells = new byte[gridLength];
            componentQueue = new int[gridLength];
            return;
        }

        Array.Clear(whiteCells, 0, gridLength);
        Array.Clear(luminanceCells, 0, gridLength);
        Array.Clear(visitedCells, 0, gridLength);
    }

    private bool IsWhiteSheetPixel(Color32 color, int luminance)
    {
        var maxChannel = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        var minChannel = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return luminance >= brightThreshold && minChannel >= 84 && maxChannel - minChannel <= 94;
    }

    private Color32 ReadAverageCellColor(int startX, int startY, int imageWidth, int imageHeight)
    {
        var endX = Mathf.Min(startX + sampleStep, imageWidth);
        var endY = Mathf.Min(startY + sampleStep, imageHeight);
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;

        for (var y = startY; y < endY; y++)
        {
            var lineStart = y * imageWidth;
            for (var x = startX; x < endX; x++)
            {
                var color = cameraPixels[lineStart + x];
                red += color.r;
                green += color.g;
                blue += color.b;
                count++;
            }
        }

        count = Mathf.Max(1, count);
        return new Color32((byte)(red / count), (byte)(green / count), (byte)(blue / count), 255);
    }

    private bool FindBestWhiteSheetComponent(int gridWidth, int gridHeight)
    {
        var bestScore = 0f;
        var totalCells = Mathf.Max(1, gridWidth * gridHeight);

        for (var startIndex = 0; startIndex < whiteCells.Length; startIndex++)
        {
            if (whiteCells[startIndex] == 0 || visitedCells[startIndex] != 0)
            {
                continue;
            }

            var component = ReadWhiteComponent(startIndex, gridWidth, gridHeight);
            if (!component.Found)
            {
                continue;
            }

            var componentWidth = component.MaxX - component.MinX + 1;
            var componentHeight = component.MaxY - component.MinY + 1;
            var markerSize = Mathf.Max(componentWidth / (float)gridWidth, componentHeight / (float)gridHeight);
            var whiteArea = component.WhiteCount / (float)totalCells;
            var aspect = componentWidth / (float)Mathf.Max(1, componentHeight);
            var aspectScore = GetA4AspectScore(aspect);

            if (markerSize < minMarkerSize
                || markerSize > maxMarkerSize
                || whiteArea < minWhiteArea
                || component.Density < minMarkerDensity
                || component.CenterFill < minCenterFill
                || component.CornerFill < minCornerFill
                || component.LuminanceDeviation > maxSheetLuminanceDeviation
                || aspectScore <= 0f)
            {
                continue;
            }

            var plainnessScore = 1f - Mathf.Clamp01(component.LuminanceDeviation / Mathf.Max(1f, maxSheetLuminanceDeviation));
            var score = component.WhiteCount * component.Density * (1f + aspectScore + plainnessScore);
            bestScore = Mathf.Max(bestScore, score);
        }

        return bestScore > 0f;
    }

    private WhiteMarkerComponent ReadWhiteComponent(int startIndex, int gridWidth, int gridHeight)
    {
        var result = new WhiteMarkerComponent();
        var head = 0;
        var tail = 0;
        var minX = gridWidth;
        var maxX = 0;
        var minY = gridHeight;
        var maxY = 0;
        var whiteCount = 0;

        componentQueue[tail++] = startIndex;
        visitedCells[startIndex] = 1;

        while (head < tail)
        {
            var index = componentQueue[head++];
            var x = index % gridWidth;
            var y = index / gridWidth;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
            whiteCount++;

            AddWhiteNeighbor(index - 1, x > 0, ref tail);
            AddWhiteNeighbor(index + 1, x < gridWidth - 1, ref tail);
            AddWhiteNeighbor(index - gridWidth, y > 0, ref tail);
            AddWhiteNeighbor(index + gridWidth, y < gridHeight - 1, ref tail);
        }

        var componentWidth = maxX - minX + 1;
        var componentHeight = maxY - minY + 1;
        var area = Mathf.Max(1, componentWidth * componentHeight);
        var averageLuminance = CountLuminanceCells(minX, maxX, minY, maxY, gridWidth) / (float)area;

        result.Found = whiteCount >= 45;
        result.MinX = minX;
        result.MaxX = maxX;
        result.MinY = minY;
        result.MaxY = maxY;
        result.WhiteCount = whiteCount;
        result.Density = whiteCount / (float)area;
        result.CenterFill = CalculateCenterFill(minX, maxX, minY, maxY, gridWidth);
        result.CornerFill = CalculateCornerFill(minX, maxX, minY, maxY, gridWidth);
        result.LuminanceDeviation = CountLuminanceDeviation(minX, maxX, minY, maxY, gridWidth, averageLuminance) / (float)area;
        return result;
    }

    private void AddWhiteNeighbor(int index, bool canUse, ref int tail)
    {
        if (!canUse || whiteCells[index] == 0 || visitedCells[index] != 0)
        {
            return;
        }

        visitedCells[index] = 1;
        componentQueue[tail++] = index;
    }

    private float CalculateCenterFill(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var insetX = Mathf.Max(1, Mathf.RoundToInt(width * 0.22f));
        var insetY = Mathf.Max(1, Mathf.RoundToInt(height * 0.22f));
        var centerMinX = Mathf.Min(maxX, minX + insetX);
        var centerMaxX = Mathf.Max(minX, maxX - insetX);
        var centerMinY = Mathf.Min(maxY, minY + insetY);
        var centerMaxY = Mathf.Max(minY, maxY - insetY);
        var area = Mathf.Max(1, (centerMaxX - centerMinX + 1) * (centerMaxY - centerMinY + 1));

        return CountWhiteCells(centerMinX, centerMaxX, centerMinY, centerMaxY, gridWidth) / (float)area;
    }

    private float CalculateCornerFill(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var cornerWidth = Mathf.Max(2, Mathf.RoundToInt(width * 0.2f));
        var cornerHeight = Mathf.Max(2, Mathf.RoundToInt(height * 0.2f));
        var whiteCount = CountWhiteCells(minX, minX + cornerWidth - 1, minY, minY + cornerHeight - 1, gridWidth)
            + CountWhiteCells(maxX - cornerWidth + 1, maxX, minY, minY + cornerHeight - 1, gridWidth)
            + CountWhiteCells(minX, minX + cornerWidth - 1, maxY - cornerHeight + 1, maxY, gridWidth)
            + CountWhiteCells(maxX - cornerWidth + 1, maxX, maxY - cornerHeight + 1, maxY, gridWidth);
        var area = Mathf.Max(1, cornerWidth * cornerHeight * 4);

        return whiteCount / (float)area;
    }

    private int CountWhiteCells(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        return CountCells(whiteCells, minX, maxX, minY, maxY, gridWidth);
    }

    private int CountLuminanceCells(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        return CountCells(luminanceCells, minX, maxX, minY, maxY, gridWidth);
    }

    private float CountLuminanceDeviation(int minX, int maxX, int minY, int maxY, int gridWidth, float averageLuminance)
    {
        if (luminanceCells == null || luminanceCells.Length == 0 || gridWidth <= 0)
        {
            return 0f;
        }

        var gridHeight = Mathf.Max(1, luminanceCells.Length / gridWidth);
        var safeMinX = Mathf.Clamp(Mathf.Min(minX, maxX), 0, gridWidth - 1);
        var safeMaxX = Mathf.Clamp(Mathf.Max(minX, maxX), 0, gridWidth - 1);
        var safeMinY = Mathf.Clamp(Mathf.Min(minY, maxY), 0, gridHeight - 1);
        var safeMaxY = Mathf.Clamp(Mathf.Max(minY, maxY), 0, gridHeight - 1);
        var deviation = 0f;

        for (var y = safeMinY; y <= safeMaxY; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = safeMinX; x <= safeMaxX; x++)
            {
                var index = rowStart + x;
                if (index >= 0 && index < luminanceCells.Length)
                {
                    deviation += Mathf.Abs(luminanceCells[index] - averageLuminance);
                }
            }
        }

        return deviation;
    }

    private int CountCells(byte[] cells, int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        if (cells == null || cells.Length == 0 || gridWidth <= 0)
        {
            return 0;
        }

        var gridHeight = Mathf.Max(1, cells.Length / gridWidth);
        var safeMinX = Mathf.Clamp(Mathf.Min(minX, maxX), 0, gridWidth - 1);
        var safeMaxX = Mathf.Clamp(Mathf.Max(minX, maxX), 0, gridWidth - 1);
        var safeMinY = Mathf.Clamp(Mathf.Min(minY, maxY), 0, gridHeight - 1);
        var safeMaxY = Mathf.Clamp(Mathf.Max(minY, maxY), 0, gridHeight - 1);
        var count = 0;

        for (var y = safeMinY; y <= safeMaxY; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = safeMinX; x <= safeMaxX; x++)
            {
                var index = rowStart + x;
                if (index >= 0 && index < cells.Length)
                {
                    count += cells[index];
                }
            }
        }

        return count;
    }

    private float GetA4AspectScore(float aspect)
    {
        const float landscapeA4 = 1.414f;
        const float portraitA4 = 0.707f;
        var landscapeScore = 1f - Mathf.Abs(aspect - landscapeA4) / 0.5f;
        var portraitScore = 1f - Mathf.Abs(aspect - portraitA4) / 0.34f;
        return Mathf.Clamp01(Mathf.Max(landscapeScore, portraitScore));
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
                currentRigidbody.useGravity = true;
                currentRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            else
            {
                currentRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                currentRigidbody.useGravity = false;
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
