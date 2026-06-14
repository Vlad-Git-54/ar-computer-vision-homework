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
    [SerializeField] private int brightThreshold = 140;
    [SerializeField] private int sampleStep = 4;
    [SerializeField] private float minMarkerSize = 0.12f;
    [SerializeField] private float maxMarkerSize = 0.9f;
    [SerializeField] private float minMarkerDensity = 0.42f;
    [SerializeField] private float maxDarkMarkerDensity = 0.18f;
    [SerializeField] private float markerDistanceFromCamera = 7.2f;
    [SerializeField] private float gameScaleAtMarker = 0.055f;
    [SerializeField] private float gameplayWorldSize = 28f;
    [SerializeField] private float gameplayWorldWidth = 28f;
    [SerializeField] private float gameplayWorldDepth = 28f;
    [SerializeField] private float paperCoverage = 0.72f;
    [SerializeField] private float minGameScale = 0.006f;
    [SerializeField] private float maxGameScale = 0.07f;
    [SerializeField] private float maxSurroundingBrightDensity = 0.42f;
    [SerializeField] private float minBorderContrast = 14f;
    [SerializeField] private float minCornerFill = 0.34f;
    [SerializeField] private float markerPlaneLift = 0.04f;
    [SerializeField] private float followSharpness = 8f;
    [SerializeField] private float markerLostDelay = 1.25f;
    [SerializeField] private string markerHelpText = "Маркер: чистый белый лист А4";
    [SerializeField] private bool setupSceneAutomatically = true;
    [SerializeField] private bool placeGameOnCameraFacingPaper = true;
    [SerializeField] private bool freezePhysicsInMarkerMode = false;
    [SerializeField] private bool useGravityInMarkerMode = false;
    [SerializeField] private bool fitGameplayToPaperAspect = true;
    [SerializeField] private bool lockAnchorAfterPlacement = true;
    [SerializeField] private int stableMarkerFramesBeforePlacement = 4;
    [SerializeField] private float markerSmoothingSharpness = 4f;
    [SerializeField] private Vector3 arCameraPosition = new Vector3(0f, 2.8f, -2.5f);
    [SerializeField] private Vector3 arCameraRotation = new Vector3(14f, 0f, 0f);

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private byte[] brightMarkerCells;
    private byte[] smoothedBrightMarkerCells;
    private byte[] darkMarkerCells;
    private byte[] luminanceMarkerCells;
    private byte[] visitedMarkerCells;
    private int[] componentQueue;
    private GameObject backgroundObject;
    private Material backgroundMaterial;
    private Text statusText;
    private Text helpText;
    private bool gameVisible;
    private bool markerWasFound;
    private MarkerObservation lastStableMarker;
    private bool stableMarkerInitialized;
    private bool markerAnchorLocked;
    private int stableMarkerFrameCount;
    private float lastMarkerFoundTime = -10f;
    private float lastDetectionErrorLogTime = -10f;
    private string lastDetectionHint = "";
    private Sprite whiteSprite;

    private struct MarkerObservation
    {
        public bool Found;
        public Vector2 Center;
        public float Size;
        public float Density;
        public float ViewportWidth;
        public float ViewportHeight;
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
        public float SurroundingBrightDensity;
        public float BorderContrast;
        public float CornerFill;
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

        var marker = DetectMarkerSafely();
        var markerWasVisibleBeforeUpdate = Time.unscaledTime - lastMarkerFoundTime <= markerLostDelay;
        if (marker.Found)
        {
            lastMarkerFoundTime = Time.unscaledTime;
            markerWasFound = true;
            AcceptMarkerObservation(marker, markerWasVisibleBeforeUpdate);
        }

        var markerIsVisible = Time.unscaledTime - lastMarkerFoundTime <= markerLostDelay;
        var markerReady = markerIsVisible && lastStableMarker.Found && stableMarkerFrameCount >= stableMarkerFramesBeforePlacement;
        if (markerReady && (!markerAnchorLocked || !lockAnchorAfterPlacement))
        {
            PlaceGameOnMarker(lastStableMarker, !markerAnchorLocked);
            markerAnchorLocked = true;
        }
        else if (!markerIsVisible)
        {
            stableMarkerInitialized = false;
            markerAnchorLocked = false;
            stableMarkerFrameCount = 0;
        }

        SetGameVisible(markerReady, !markerReady && Time.frameCount % 15 == 0);

        if (markerReady)
        {
            UpdateStatus(true, "Лист А4 найден, игра закреплена на нём");
            return;
        }

        var waitingMessage = markerWasFound ? "Лист потерян, покажите его камере снова" : "Поднесите белый лист А4 к веб-камере";
        if (!string.IsNullOrEmpty(lastDetectionHint))
        {
            waitingMessage += ". " + lastDetectionHint;
        }

        UpdateStatus(false, waitingMessage);
    }

    private void AcceptMarkerObservation(MarkerObservation marker, bool hadRecentMarker)
    {
        if (!stableMarkerInitialized || !hadRecentMarker)
        {
            lastStableMarker = marker;
            stableMarkerInitialized = true;
            markerAnchorLocked = false;
            stableMarkerFrameCount = 1;
            return;
        }

        if (markerAnchorLocked && lockAnchorAfterPlacement)
        {
            return;
        }

        var blend = 1f - Mathf.Exp(-markerSmoothingSharpness * Time.unscaledDeltaTime);
        lastStableMarker.Found = true;
        lastStableMarker.Center = Vector2.Lerp(lastStableMarker.Center, marker.Center, blend);
        lastStableMarker.Size = Mathf.Lerp(lastStableMarker.Size, marker.Size, blend);
        lastStableMarker.Density = Mathf.Lerp(lastStableMarker.Density, marker.Density, blend);
        lastStableMarker.ViewportWidth = Mathf.Lerp(lastStableMarker.ViewportWidth, marker.ViewportWidth, blend);
        lastStableMarker.ViewportHeight = Mathf.Lerp(lastStableMarker.ViewportHeight, marker.ViewportHeight, blend);
        stableMarkerFrameCount = Mathf.Min(stableMarkerFrameCount + 1, stableMarkerFramesBeforePlacement);
    }

    private MarkerObservation DetectMarkerSafely()
    {
        try
        {
            return DetectMarker();
        }
        catch (Exception exception)
        {
            if (Time.unscaledTime - lastDetectionErrorLogTime > 1f)
            {
                lastDetectionErrorLogTime = Time.unscaledTime;
                Debug.LogWarning("Кадр камеры пропущен из-за ошибки распознавания листа: " + exception.Message);
            }

            return new MarkerObservation();
        }
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
            for (var gridX = 0; gridX < gridWidth; gridX++)
            {
                var x = gridX * sampleStep;
                var color = ReadAverageCellColor(x, y, width, height);
                var luminance = (color.r * 30 + color.g * 59 + color.b * 11) / 100;
                var cellIndex = gridY * gridWidth + gridX;
                luminanceMarkerCells[cellIndex] = (byte)Mathf.Clamp(luminance, 0, 255);

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

        SmoothBrightMarkerGrid(gridWidth, gridHeight);
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
            smoothedBrightMarkerCells = new byte[gridLength];
            darkMarkerCells = new byte[gridLength];
            luminanceMarkerCells = new byte[gridLength];
            visitedMarkerCells = new byte[gridLength];
            componentQueue = new int[gridLength];
            return;
        }

        Array.Clear(brightMarkerCells, 0, gridLength);
        Array.Clear(smoothedBrightMarkerCells, 0, gridLength);
        Array.Clear(darkMarkerCells, 0, gridLength);
        Array.Clear(luminanceMarkerCells, 0, gridLength);
        Array.Clear(visitedMarkerCells, 0, gridLength);
        lastDetectionHint = "";
    }

    private bool IsBrightMarkerPixel(Color32 color, int luminance)
    {
        var maxChannel = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        var minChannel = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return luminance >= brightThreshold && minChannel >= 92 && maxChannel - minChannel <= 104;
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

    private void SmoothBrightMarkerGrid(int gridWidth, int gridHeight)
    {
        for (var y = 0; y < gridHeight; y++)
        {
            for (var x = 0; x < gridWidth; x++)
            {
                var index = y * gridWidth + x;
                if (brightMarkerCells[index] != 0)
                {
                    smoothedBrightMarkerCells[index] = 1;
                    continue;
                }

                smoothedBrightMarkerCells[index] = CountBrightNeighbors(x, y, gridWidth, gridHeight) >= 3 ? (byte)1 : (byte)0;
            }
        }

        Array.Copy(smoothedBrightMarkerCells, brightMarkerCells, brightMarkerCells.Length);
    }

    private int CountBrightNeighbors(int x, int y, int gridWidth, int gridHeight)
    {
        var count = 0;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var currentY = y + offsetY;
            if (currentY < 0 || currentY >= gridHeight)
            {
                continue;
            }

            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var currentX = x + offsetX;
                if (currentX < 0 || currentX >= gridWidth)
                {
                    continue;
                }

                count += brightMarkerCells[currentY * gridWidth + currentX];
            }
        }

        return count;
    }

    private MarkerObservation FindBestBrightMarkerComponent(int gridWidth, int gridHeight, int imageWidth, int imageHeight)
    {
        var bestObservation = new MarkerObservation();
        var bestScore = 0f;
        var componentCount = 0;
        var shapeCandidateCount = 0;

        for (var startIndex = 0; startIndex < brightMarkerCells.Length; startIndex++)
        {
            if (brightMarkerCells[startIndex] == 0 || visitedMarkerCells[startIndex] != 0)
            {
                continue;
            }

            var component = ReadBrightComponent(startIndex, gridWidth, gridHeight);
            componentCount++;
            if (!component.Found)
            {
                continue;
            }

            var marker = CreateObservationFromComponent(component, gridWidth, imageWidth, imageHeight);
            if (!marker.Found)
            {
                continue;
            }

            shapeCandidateCount++;

            var componentWidth = component.MaxX - component.MinX + 1;
            var componentHeight = component.MaxY - component.MinY + 1;
            var aspect = componentWidth / (float)Mathf.Max(1, componentHeight);
            var aspectBonus = GetA4AspectScore(aspect);
            var borderBonus = Mathf.Clamp01(component.BorderContrast / Mathf.Max(1f, minBorderContrast));
            var score = component.BrightCount * component.BrightDensity * (1f + aspectBonus + borderBonus);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestObservation = marker;
        }

        if (!bestObservation.Found)
        {
            lastDetectionHint = componentCount == 0
                ? "Белая область пока не найдена"
                : shapeCandidateCount == 0
                    ? "Белая область есть, но она не похожа на лист А4"
                    : "";
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
        var ringCells = Mathf.Max(1, CountRingCells(minX, maxX, minY, maxY, gridWidth, gridHeight));
        var surroundingBrightDensity = CountBrightCellsAround(minX, maxX, minY, maxY, gridWidth, gridHeight) / (float)ringCells;
        var innerLuminance = CountLuminanceCells(minX, maxX, minY, maxY, gridWidth) / (float)area;
        var ringLuminance = CountLuminanceCellsAround(minX, maxX, minY, maxY, gridWidth, gridHeight) / (float)ringCells;
        var borderContrast = innerLuminance - ringLuminance;
        var cornerFill = CountCornerBrightFill(minX, maxX, minY, maxY, gridWidth) / (float)Mathf.Max(1, CountCornerCells(minX, maxX, minY, maxY));

        result.Found = brightCount >= 80
            && brightDensity >= minMarkerDensity
            && darkDensity <= maxDarkMarkerDensity
            && (surroundingBrightDensity <= maxSurroundingBrightDensity || borderContrast >= minBorderContrast)
            && borderContrast >= minBorderContrast * 0.5f
            && cornerFill >= minCornerFill;
        result.MinX = minX;
        result.MaxX = maxX;
        result.MinY = minY;
        result.MaxY = maxY;
        result.BrightCount = brightCount;
        result.BrightDensity = brightDensity;
        result.DarkDensity = darkDensity;
        result.SurroundingBrightDensity = surroundingBrightDensity;
        result.BorderContrast = borderContrast;
        result.CornerFill = cornerFill;
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
        return CountCells(darkMarkerCells, minX, maxX, minY, maxY, gridWidth);
    }

    private int CountLuminanceCells(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        return CountCells(luminanceMarkerCells, minX, maxX, minY, maxY, gridWidth);
    }

    private int CountBrightCellsAround(int minX, int maxX, int minY, int maxY, int gridWidth, int gridHeight)
    {
        var count = 0;
        var ringMinX = Mathf.Max(0, minX - 5);
        var ringMaxX = Mathf.Min(gridWidth - 1, maxX + 5);
        var ringMinY = Mathf.Max(0, minY - 5);
        var ringMaxY = Mathf.Min(gridHeight - 1, maxY + 5);

        for (var y = ringMinY; y <= ringMaxY; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = ringMinX; x <= ringMaxX; x++)
            {
                if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                {
                    continue;
                }

                var index = rowStart + x;
                if (index >= 0 && index < brightMarkerCells.Length)
                {
                    count += brightMarkerCells[index];
                }
            }
        }

        return count;
    }

    private int CountLuminanceCellsAround(int minX, int maxX, int minY, int maxY, int gridWidth, int gridHeight)
    {
        var count = 0;
        var ringMinX = Mathf.Max(0, minX - 5);
        var ringMaxX = Mathf.Min(gridWidth - 1, maxX + 5);
        var ringMinY = Mathf.Max(0, minY - 5);
        var ringMaxY = Mathf.Min(gridHeight - 1, maxY + 5);

        for (var y = ringMinY; y <= ringMaxY; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = ringMinX; x <= ringMaxX; x++)
            {
                if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                {
                    continue;
                }

                var index = rowStart + x;
                if (index >= 0 && index < luminanceMarkerCells.Length)
                {
                    count += luminanceMarkerCells[index];
                }
            }
        }

        return count;
    }

    private int CountRingCells(int minX, int maxX, int minY, int maxY, int gridWidth, int gridHeight)
    {
        var ringMinX = Mathf.Max(0, minX - 5);
        var ringMaxX = Mathf.Min(gridWidth - 1, maxX + 5);
        var ringMinY = Mathf.Max(0, minY - 5);
        var ringMaxY = Mathf.Min(gridHeight - 1, maxY + 5);
        var fullArea = (ringMaxX - ringMinX + 1) * (ringMaxY - ringMinY + 1);
        var innerArea = (maxX - minX + 1) * (maxY - minY + 1);

        return Mathf.Max(1, fullArea - innerArea);
    }

    private int CountCornerBrightFill(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var cornerWidth = Mathf.Max(2, Mathf.RoundToInt(width * 0.2f));
        var cornerHeight = Mathf.Max(2, Mathf.RoundToInt(height * 0.2f));

        return CountBrightCells(minX, minX + cornerWidth - 1, minY, minY + cornerHeight - 1, gridWidth)
            + CountBrightCells(maxX - cornerWidth + 1, maxX, minY, minY + cornerHeight - 1, gridWidth)
            + CountBrightCells(minX, minX + cornerWidth - 1, maxY - cornerHeight + 1, maxY, gridWidth)
            + CountBrightCells(maxX - cornerWidth + 1, maxX, maxY - cornerHeight + 1, maxY, gridWidth);
    }

    private int CountCornerCells(int minX, int maxX, int minY, int maxY)
    {
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var cornerWidth = Mathf.Max(2, Mathf.RoundToInt(width * 0.2f));
        var cornerHeight = Mathf.Max(2, Mathf.RoundToInt(height * 0.2f));
        return cornerWidth * cornerHeight * 4;
    }

    private int CountBrightCells(int minX, int maxX, int minY, int maxY, int gridWidth)
    {
        return CountCells(brightMarkerCells, minX, maxX, minY, maxY, gridWidth);
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

    private MarkerObservation CreateObservationFromComponent(BrightMarkerComponent component, int gridWidth, int imageWidth, int imageHeight)
    {
        var observation = new MarkerObservation();
        var gridHeight = Mathf.Max(1, imageHeight / sampleStep);
        var width = Mathf.Max(1, component.MaxX - component.MinX + 1);
        var height = Mathf.Max(1, component.MaxY - component.MinY + 1);
        var aspect = width / (float)height;
        var markerWidth = width * sampleStep;
        var markerHeight = height * sampleStep;
        var markerSize = Mathf.Max(markerWidth / (float)imageWidth, markerHeight / (float)imageHeight);
        var edgeMargin = 1;

        if (component.MinX <= edgeMargin
            || component.MinY <= edgeMargin
            || component.MaxX >= gridWidth - edgeMargin
            || component.MaxY >= gridHeight - edgeMargin
            || markerSize < minMarkerSize
            || markerSize > maxMarkerSize
            || !IsA4AspectAccepted(aspect))
        {
            return observation;
        }

        observation.Found = true;
        observation.Center = new Vector2((component.MinX + component.MaxX + 1f) * sampleStep * 0.5f / imageWidth, (component.MinY + component.MaxY + 1f) * sampleStep * 0.5f / imageHeight);
        observation.Size = markerSize;
        observation.ViewportWidth = markerWidth / (float)imageWidth;
        observation.ViewportHeight = markerHeight / (float)imageHeight;
        observation.Density = component.BrightDensity + component.DarkDensity;
        return observation;
    }

    private bool IsA4AspectAccepted(float aspect)
    {
        const float landscapeA4 = 1.414f;
        const float portraitA4 = 0.707f;
        return Mathf.Abs(aspect - landscapeA4) <= 0.34f || Mathf.Abs(aspect - portraitA4) <= 0.22f;
    }

    private float GetA4AspectScore(float aspect)
    {
        const float landscapeA4 = 1.414f;
        const float portraitA4 = 0.707f;
        var landscapeScore = 1f - Mathf.Abs(aspect - landscapeA4) / 0.34f;
        var portraitScore = 1f - Mathf.Abs(aspect - portraitA4) / 0.22f;
        return Mathf.Clamp01(Mathf.Max(landscapeScore, portraitScore));
    }

    private void PlaceGameOnMarker(MarkerObservation marker, bool instant)
    {
        var targetPosition = sceneCamera.ViewportToWorldPoint(new Vector3(marker.Center.x, marker.Center.y, markerDistanceFromCamera));
        var targetRotation = Quaternion.Euler(0f, sceneCamera.transform.eulerAngles.y, 0f);
        if (placeGameOnCameraFacingPaper)
        {
            targetPosition += sceneCamera.transform.forward * markerPlaneLift;
            targetRotation = Quaternion.LookRotation(sceneCamera.transform.up, -sceneCamera.transform.forward);
        }

        var targetScale = CalculateGameScale(marker);
        var blend = instant ? 1f : 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);

        gameRoot.position = Vector3.Lerp(gameRoot.position, targetPosition, blend);
        gameRoot.rotation = Quaternion.Slerp(gameRoot.rotation, targetRotation, blend);
        gameRoot.localScale = Vector3.Lerp(gameRoot.localScale, targetScale, blend);
    }

    private Vector3 CalculateGameScale(MarkerObservation marker)
    {
        var viewportWorldHeight = 2f * markerDistanceFromCamera * Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        var viewportWorldWidth = viewportWorldHeight * sceneCamera.aspect;
        var markerWorldWidth = Mathf.Max(0.01f, marker.ViewportWidth * viewportWorldWidth);
        var markerWorldHeight = Mathf.Max(0.01f, marker.ViewportHeight * viewportWorldHeight);
        var sourceWidth = gameplayWorldWidth > 0.01f ? gameplayWorldWidth : gameplayWorldSize;
        var sourceDepth = gameplayWorldDepth > 0.01f ? gameplayWorldDepth : gameplayWorldSize;
        var scaleX = markerWorldWidth * paperCoverage / Mathf.Max(0.01f, sourceWidth);
        var scaleZ = markerWorldHeight * paperCoverage / Mathf.Max(0.01f, sourceDepth);

        scaleX = Mathf.Clamp(scaleX, minGameScale, maxGameScale);
        scaleZ = Mathf.Clamp(scaleZ, minGameScale, maxGameScale);

        if (!fitGameplayToPaperAspect)
        {
            var uniformScale = Mathf.Min(scaleX, scaleZ);
            return Vector3.one * uniformScale;
        }

        var heightScale = Mathf.Min(scaleX, scaleZ);
        return new Vector3(scaleX, heightScale, scaleZ);
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
                currentRigidbody.isKinematic = freezePhysicsInMarkerMode;
                currentRigidbody.useGravity = useGravityInMarkerMode;
                currentRigidbody.collisionDetectionMode = freezePhysicsInMarkerMode ? CollisionDetectionMode.Discrete : CollisionDetectionMode.Continuous;
            }
            else
            {
                currentRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
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
