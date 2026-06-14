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
    [SerializeField] private int minCheckerContrast = 42;
    [SerializeField] private float minCheckerEdgeDensity = 0.045f;
    [SerializeField] private float minCheckerPatternScore = 0.48f;
    [SerializeField] private float minCheckerAxisAlternation = 0.68f;
    [SerializeField] private float minCheckerParityMatch = 0.72f;
    [SerializeField] private int maxCheckerDarkLuminance = 125;
    [SerializeField] private int minCheckerLightLuminance = 145;
    [SerializeField] private float minCheckerSize = 0.06f;
    [SerializeField] private float maxCheckerSize = 0.98f;
    [SerializeField] private float maxCheckerAspect = 4.2f;
    [SerializeField] private Vector2 detectedTabletContourPadding = new Vector2(0.04f, 0.04f);
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
    [SerializeField] private string markerHelpText = "Маркер: шахматная доска любого размера";
    [SerializeField] private bool useFixedTabletMarkerPlacement = false;
    [SerializeField] private Vector2 fixedTabletMarkerMin = new Vector2(0.35f, 0.38f);
    [SerializeField] private Vector2 fixedTabletMarkerMax = new Vector2(0.71f, 0.70f);
    [SerializeField] private bool alignSceneCameraWithWebcam = true;
    [SerializeField] private Vector3 webcamCameraPosition = new Vector3(0f, 9.5f, -6.2f);
    [SerializeField] private Vector3 webcamCameraRotation = new Vector3(61f, 0f, 0f);
    [SerializeField] private float webcamCameraFieldOfView = 62f;
    [SerializeField] private bool setupSceneAutomatically = true;
    [SerializeField] private Vector3 gameRootPosition = Vector3.zero;
    [SerializeField] private Vector3 gameRootRotation = Vector3.zero;
    [SerializeField] private Vector3 gameRootScale = Vector3.one;
    [SerializeField] private float fallbackGameplayWidth = 24f;
    [SerializeField] private float fallbackGameplayDepth = 18f;
    [SerializeField] private float markerPlacementSmoothness = 6f;
    [SerializeField] private float markerBoundsSmoothness = 8f;
    [SerializeField] private int gameRenderLayer = 30;
    [SerializeField] private int markerRenderTextureWidth = 1024;
    [SerializeField] private int markerRenderTextureHeight = 768;
    [SerializeField] private float markerOverlayDepth = 28f;
    [SerializeField] private float markerViewportPadding = 0.92f;
    [SerializeField] private float markerOverlayCameraPadding = 1.1f;
    [SerializeField] private Vector3 markerOverlayCameraRotation = new Vector3(90f, 0f, 0f);

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private byte[] whiteCells;
    private byte[] luminanceCells;
    private byte[] visitedCells;
    private int[] componentQueue;
    private GameObject backgroundObject;
    private Material backgroundMaterial;
    private Camera markerGameCamera;
    private RenderTexture markerGameTexture;
    private GameObject markerOverlayObject;
    private Mesh markerOverlayMesh;
    private Material markerOverlayMaterial;
    private Text statusText;
    private Text helpText;
    private bool gameVisible;
    private bool markerWasFound;
    private bool gameTriggered;
    private bool gamePlacedOnMarker;
    private bool markerPauseActive;
    private float timeScaleBeforeMarkerPause = 1f;
    private int stableMarkerFrames;
    private float lastMarkerFoundTime = -10f;
    private float lastDetectionErrorLogTime = -10f;
    private int currentMarkerGridWidth;
    private int currentMarkerGridHeight;
    private CheckerboardMarkerCandidate lastCheckerboardCandidate;
    private CheckerboardMarkerCandidate smoothedCheckerboardCandidate;
    private bool smoothedCheckerboardReady;
    private readonly Vector2[] currentOverlayCorners = new Vector2[4];
    private bool markerOverlayReady;
    private Bounds gameRootLocalBounds;
    private bool gameRootBoundsReady;
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

    private struct CheckerboardMarkerCandidate
    {
        public bool Found;
        public float Score;
        public float Contrast;
        public float MinGridX;
        public float MaxGridX;
        public float MinGridY;
        public float MaxGridY;
        public Vector2 Corner0Grid;
        public Vector2 Corner1Grid;
        public Vector2 Corner2Grid;
        public Vector2 Corner3Grid;
    }

    private struct CheckerContrastComponent
    {
        public bool Found;
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
        public int EdgeCount;
        public float Density;
        public float Score;
        public float PatternScore;
        public Vector2 Corner0Grid;
        public Vector2 Corner1Grid;
        public Vector2 Corner2Grid;
        public Vector2 Corner3Grid;
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
            AlignSceneCameraWithWebcam();
            SetupMarkerGameCamera();
            PlaceGameNormally();
        }
    }

    private void Start()
    {
        CreateInterface();
        StartWebcam();
        SetGameVisible(false, true);
        SetMarkerPause(true);
        UpdateStatus(false, "Поднесите шахматную доску к веб-камере");
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
            gamePlacedOnMarker = false;
            markerOverlayReady = false;
        }

        SetGameVisible(shouldShowGame, false);
        SetMarkerPause(!shouldShowGame);

        if (shouldShowGame)
        {
            PlaceGameOnMarker(lastCheckerboardCandidate, !gamePlacedOnMarker);
            gamePlacedOnMarker = true;

            UpdateStatus(true, "Шахматный маркер найден, игра размещена по его границам");
            return;
        }

        var waitingMessage = markerWasFound ? "Маркер потерян, покажите шахматную доску снова" : "Поднесите шахматную доску к веб-камере";
        if (!string.IsNullOrEmpty(lastDetectionHint))
        {
            waitingMessage += ". " + lastDetectionHint;
        }

        UpdateStatus(false, waitingMessage);
    }

    private void UpdateMarkerTrigger()
    {
        var markerFound = DetectCheckerboardMarkerSafely(out var markerCandidate);
        if (markerFound)
        {
            markerWasFound = true;
            stableMarkerFrames++;
            lastMarkerFoundTime = Time.unscaledTime;
            var placementCandidate = useFixedTabletMarkerPlacement ? CreateFixedTabletMarkerCandidate() : markerCandidate;
            lastCheckerboardCandidate = SmoothCheckerboardMarker(placementCandidate);
        }
        else if (Time.unscaledTime - lastMarkerFoundTime > markerLostDelay)
        {
            stableMarkerFrames = 0;
            smoothedCheckerboardReady = false;
        }

        if (stableMarkerFrames >= stableFramesToShow)
        {
            gameTriggered = true;
            if (!gamePlacedOnMarker)
            {
                PlaceGameOnMarker(lastCheckerboardCandidate, true);
                gamePlacedOnMarker = true;
            }
        }
    }

    private bool DetectCheckerboardMarkerSafely(out CheckerboardMarkerCandidate markerCandidate)
    {
        try
        {
            return DetectCheckerboardMarker(out markerCandidate);
        }
        catch (Exception exception)
        {
            markerCandidate = new CheckerboardMarkerCandidate();
            if (Time.unscaledTime - lastDetectionErrorLogTime > 1f)
            {
                lastDetectionErrorLogTime = Time.unscaledTime;
                Debug.LogWarning("Кадр камеры пропущен из-за ошибки распознавания шахматного маркера: " + exception.Message);
            }

            return false;
        }
    }

    private CheckerboardMarkerCandidate SmoothCheckerboardMarker(CheckerboardMarkerCandidate markerCandidate)
    {
        if (!smoothedCheckerboardReady || !smoothedCheckerboardCandidate.Found)
        {
            smoothedCheckerboardCandidate = markerCandidate;
            smoothedCheckerboardReady = true;
            return smoothedCheckerboardCandidate;
        }

        var t = Mathf.Clamp01(markerBoundsSmoothness * Time.unscaledDeltaTime);
        smoothedCheckerboardCandidate.Found = markerCandidate.Found;
        smoothedCheckerboardCandidate.Score = markerCandidate.Score;
        smoothedCheckerboardCandidate.Contrast = markerCandidate.Contrast;
        smoothedCheckerboardCandidate.MinGridX = Mathf.Lerp(smoothedCheckerboardCandidate.MinGridX, markerCandidate.MinGridX, t);
        smoothedCheckerboardCandidate.MaxGridX = Mathf.Lerp(smoothedCheckerboardCandidate.MaxGridX, markerCandidate.MaxGridX, t);
        smoothedCheckerboardCandidate.MinGridY = Mathf.Lerp(smoothedCheckerboardCandidate.MinGridY, markerCandidate.MinGridY, t);
        smoothedCheckerboardCandidate.MaxGridY = Mathf.Lerp(smoothedCheckerboardCandidate.MaxGridY, markerCandidate.MaxGridY, t);
        smoothedCheckerboardCandidate.Corner0Grid = Vector2.Lerp(smoothedCheckerboardCandidate.Corner0Grid, markerCandidate.Corner0Grid, t);
        smoothedCheckerboardCandidate.Corner1Grid = Vector2.Lerp(smoothedCheckerboardCandidate.Corner1Grid, markerCandidate.Corner1Grid, t);
        smoothedCheckerboardCandidate.Corner2Grid = Vector2.Lerp(smoothedCheckerboardCandidate.Corner2Grid, markerCandidate.Corner2Grid, t);
        smoothedCheckerboardCandidate.Corner3Grid = Vector2.Lerp(smoothedCheckerboardCandidate.Corner3Grid, markerCandidate.Corner3Grid, t);
        return smoothedCheckerboardCandidate;
    }

    private CheckerboardMarkerCandidate CreateFixedTabletMarkerCandidate()
    {
        var gridWidth = Mathf.Max(1, currentMarkerGridWidth);
        var gridHeight = Mathf.Max(1, currentMarkerGridHeight);
        var minX = Mathf.Clamp01(Mathf.Min(fixedTabletMarkerMin.x, fixedTabletMarkerMax.x));
        var maxX = Mathf.Clamp01(Mathf.Max(fixedTabletMarkerMin.x, fixedTabletMarkerMax.x));
        var minY = Mathf.Clamp01(Mathf.Min(fixedTabletMarkerMin.y, fixedTabletMarkerMax.y));
        var maxY = Mathf.Clamp01(Mathf.Max(fixedTabletMarkerMin.y, fixedTabletMarkerMax.y));

        return new CheckerboardMarkerCandidate
        {
            Found = true,
            Score = 1f,
            Contrast = minCheckerContrast,
            MinGridX = minX * gridWidth,
            MaxGridX = maxX * gridWidth,
            MinGridY = minY * gridHeight,
            MaxGridY = maxY * gridHeight,
            Corner0Grid = new Vector2(minX * gridWidth, minY * gridHeight),
            Corner1Grid = new Vector2(maxX * gridWidth, minY * gridHeight),
            Corner2Grid = new Vector2(maxX * gridWidth, maxY * gridHeight),
            Corner3Grid = new Vector2(minX * gridWidth, maxY * gridHeight)
        };
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

        if (markerOverlayMaterial != null)
        {
            Destroy(markerOverlayMaterial);
            markerOverlayMaterial = null;
        }

        if (markerGameTexture != null)
        {
            markerGameTexture.Release();
            Destroy(markerGameTexture);
            markerGameTexture = null;
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
        CacheGameRootBounds();
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

        CacheGameRootBounds();
        gameRoot.position = gameRootPosition;
        gameRoot.rotation = Quaternion.Euler(gameRootRotation);
        gameRoot.localScale = gameRootScale;
    }

    private void SetupMarkerGameCamera()
    {
        if (sceneCamera == null || gameRoot == null)
        {
            return;
        }

        AssignLayerRecursively(gameRoot.gameObject, gameRenderLayer);
        sceneCamera.cullingMask &= ~(1 << gameRenderLayer);

        markerGameTexture = new RenderTexture(
            Mathf.Max(256, markerRenderTextureWidth),
            Mathf.Max(256, markerRenderTextureHeight),
            16,
            RenderTextureFormat.ARGB32);
        markerGameTexture.name = "AR Marker Game Render Texture";
        markerGameTexture.Create();

        var cameraObject = new GameObject("AR Marker Game Camera");
        markerGameCamera = cameraObject.AddComponent<Camera>();
        markerGameCamera.enabled = false;
        markerGameCamera.clearFlags = CameraClearFlags.SolidColor;
        markerGameCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        markerGameCamera.cullingMask = 1 << gameRenderLayer;
        markerGameCamera.targetTexture = markerGameTexture;
        markerGameCamera.orthographic = true;
        markerGameCamera.nearClipPlane = 0.1f;
        markerGameCamera.farClipPlane = 100f;

        CreateMarkerOverlayObject();
        UpdateMarkerGameCameraView();
    }

    private void CreateMarkerOverlayObject()
    {
        if (sceneCamera == null || markerGameTexture == null)
        {
            return;
        }

        markerOverlayObject = new GameObject("AR Marker Game Overlay");
        markerOverlayObject.transform.SetParent(sceneCamera.transform, false);
        markerOverlayObject.transform.localPosition = Vector3.zero;
        markerOverlayObject.transform.localRotation = Quaternion.identity;
        markerOverlayObject.transform.localScale = Vector3.one;
        markerOverlayObject.SetActive(false);

        markerOverlayMesh = new Mesh();
        markerOverlayMesh.name = "AR Marker Game Overlay Mesh";

        var meshFilter = markerOverlayObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = markerOverlayMesh;

        markerOverlayMaterial = new Material(Shader.Find("Unlit/Texture"));
        markerOverlayMaterial.name = "AR Marker Game Overlay Material";
        markerOverlayMaterial.mainTexture = markerGameTexture;
        markerOverlayMaterial.renderQueue = 3000;

        var meshRenderer = markerOverlayObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = markerOverlayMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        UpdateMarkerOverlayMesh(new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        });
    }

    private void AssignLayerRecursively(GameObject currentObject, int layer)
    {
        if (currentObject == null)
        {
            return;
        }

        currentObject.layer = layer;
        foreach (Transform child in currentObject.transform)
        {
            AssignLayerRecursively(child.gameObject, layer);
        }
    }

    private void AlignSceneCameraWithWebcam()
    {
        if (!alignSceneCameraWithWebcam || sceneCamera == null)
        {
            return;
        }

        var follow = sceneCamera.GetComponent<ThirdPersonCameraFollow>();
        if (follow != null)
        {
            follow.enabled = false;
        }

        sceneCamera.transform.position = webcamCameraPosition;
        sceneCamera.transform.rotation = Quaternion.Euler(webcamCameraRotation);
        sceneCamera.fieldOfView = webcamCameraFieldOfView;
    }

    private void PlaceGameOnMarker(CheckerboardMarkerCandidate markerCandidate, bool immediate)
    {
        if (gameRoot == null || sceneCamera == null || markerOverlayMesh == null || !markerCandidate.Found)
        {
            return;
        }

        var targetCorners = new[]
        {
            GridToViewport(markerCandidate.Corner0Grid),
            GridToViewport(markerCandidate.Corner1Grid),
            GridToViewport(markerCandidate.Corner2Grid),
            GridToViewport(markerCandidate.Corner3Grid)
        };

        ApplyMarkerViewportPadding(targetCorners);

        if (immediate || !markerOverlayReady)
        {
            for (var index = 0; index < currentOverlayCorners.Length; index++)
            {
                currentOverlayCorners[index] = targetCorners[index];
            }

            markerOverlayReady = true;
        }
        else
        {
            var t = Mathf.Clamp01(markerPlacementSmoothness * Time.unscaledDeltaTime);
            for (var index = 0; index < currentOverlayCorners.Length; index++)
            {
                currentOverlayCorners[index] = Vector2.Lerp(currentOverlayCorners[index], targetCorners[index], t);
            }
        }

        UpdateMarkerOverlayMesh(currentOverlayCorners);
        UpdateMarkerGameCameraView();
    }

    private Vector2 GridToViewport(Vector2 gridPoint)
    {
        var gridWidth = Mathf.Max(1, currentMarkerGridWidth);
        var gridHeight = Mathf.Max(1, currentMarkerGridHeight);
        return new Vector2(
            Mathf.Clamp01(gridPoint.x / gridWidth),
            Mathf.Clamp01(gridPoint.y / gridHeight));
    }

    private void ApplyMarkerViewportPadding(Vector2[] corners)
    {
        var center = Vector2.zero;
        for (var index = 0; index < corners.Length; index++)
        {
            center += corners[index];
        }

        center /= Mathf.Max(1, corners.Length);
        for (var index = 0; index < corners.Length; index++)
        {
            corners[index] = center + (corners[index] - center) * markerViewportPadding;
            corners[index].x = Mathf.Clamp01(corners[index].x);
            corners[index].y = Mathf.Clamp01(corners[index].y);
        }
    }

    private void UpdateMarkerOverlayMesh(Vector2[] viewportCorners)
    {
        if (markerOverlayMesh == null || sceneCamera == null || viewportCorners == null || viewportCorners.Length < 4)
        {
            return;
        }

        var vertices = new[]
        {
            ViewportToCameraLocal(viewportCorners[0]),
            ViewportToCameraLocal(viewportCorners[1]),
            ViewportToCameraLocal(viewportCorners[2]),
            ViewportToCameraLocal(viewportCorners[3])
        };

        markerOverlayMesh.Clear();
        markerOverlayMesh.vertices = vertices;
        markerOverlayMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        markerOverlayMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        markerOverlayMesh.RecalculateBounds();
    }

    private Vector3 ViewportToCameraLocal(Vector2 viewportPoint)
    {
        var worldPoint = sceneCamera.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, markerOverlayDepth));
        return sceneCamera.transform.InverseTransformPoint(worldPoint);
    }

    private void UpdateMarkerGameCameraView()
    {
        if (markerGameCamera == null || gameRoot == null)
        {
            return;
        }

        CacheGameRootBounds();
        markerGameCamera.rect = new Rect(0f, 0f, 1f, 1f);

        var bounds = gameRootBoundsReady
            ? gameRootLocalBounds
            : new Bounds(Vector3.zero, new Vector3(fallbackGameplayWidth, 1f, fallbackGameplayDepth));
        var targetRotation = Quaternion.Euler(markerOverlayCameraRotation);
        var worldCenter = gameRoot.TransformPoint(bounds.center);
        var cameraHeight = Mathf.Max(18f, bounds.size.y + 12f);
        var targetPosition = worldCenter - targetRotation * Vector3.forward * cameraHeight;
        markerGameCamera.transform.position = targetPosition;
        markerGameCamera.transform.rotation = targetRotation;

        var viewportAspect = markerGameTexture == null
            ? 4f / 3f
            : markerGameTexture.width / (float)Mathf.Max(1, markerGameTexture.height);
        var widthSize = Mathf.Max(0.1f, bounds.size.x * Mathf.Abs(gameRoot.localScale.x));
        var depthSize = Mathf.Max(0.1f, bounds.size.z * Mathf.Abs(gameRoot.localScale.z));
        markerGameCamera.orthographicSize = Mathf.Max(depthSize * 0.5f, widthSize / (2f * viewportAspect)) * markerOverlayCameraPadding;
    }

    private void CacheGameRootBounds()
    {
        if (gameRoot == null)
        {
            return;
        }

        var renderers = gameRoot.GetComponentsInChildren<Renderer>(true);
        var foundRenderer = false;
        var localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var currentRenderer in renderers)
        {
            if (currentRenderer == null || !ShouldUseRendererForGameBounds(currentRenderer))
            {
                continue;
            }

            var rendererBounds = currentRenderer.bounds;
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var corner = new Vector3(
                    (cornerIndex & 1) == 0 ? rendererBounds.min.x : rendererBounds.max.x,
                    (cornerIndex & 2) == 0 ? rendererBounds.min.y : rendererBounds.max.y,
                    (cornerIndex & 4) == 0 ? rendererBounds.min.z : rendererBounds.max.z);
                var localCorner = gameRoot.InverseTransformPoint(corner);

                if (!foundRenderer)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    foundRenderer = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (!foundRenderer)
        {
            return;
        }

        gameRootLocalBounds = localBounds;
        gameRootBoundsReady = true;
    }

    private bool ShouldUseRendererForGameBounds(Renderer currentRenderer)
    {
        if (currentRenderer.GetComponentInParent<Canvas>() != null)
        {
            return false;
        }

        var currentTransform = currentRenderer.transform;
        while (currentTransform != null && currentTransform != gameRoot)
        {
            var objectName = currentTransform.name;
            if (objectName == "Point Cloud Homework" || objectName == "Scene Audio Controller")
            {
                return false;
            }

            currentTransform = currentTransform.parent;
        }

        return true;
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

    private bool DetectCheckerboardMarker(out CheckerboardMarkerCandidate markerCandidate)
    {
        markerCandidate = new CheckerboardMarkerCandidate();
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
        currentMarkerGridWidth = gridWidth;
        currentMarkerGridHeight = gridHeight;
        EnsureMarkerGridSize(gridLength);

        BuildLuminanceGrid(width, height, gridWidth, gridHeight);
        markerCandidate = FindBestCheckerboardMarker(gridWidth, gridHeight);
        var markerFound = markerCandidate.Found;
        if (!markerFound)
        {
            lastDetectionHint = "Шахматная доска пока не найдена";
        }

        return markerFound;
    }

    private void BuildLuminanceGrid(int width, int height, int gridWidth, int gridHeight)
    {
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
            }
        }
    }

    private CheckerboardMarkerCandidate FindBestCheckerboardMarker(int gridWidth, int gridHeight)
    {
        MarkCheckerContrastCells(gridWidth, gridHeight);
        var bestComponent = FindLargestCheckerContrastComponent(gridWidth, gridHeight);
        if (!bestComponent.Found)
        {
            return new CheckerboardMarkerCandidate();
        }

        var componentWidth = bestComponent.MaxX - bestComponent.MinX + 1;
        var componentHeight = bestComponent.MaxY - bestComponent.MinY + 1;
        var paddingX = Mathf.Max(3, Mathf.RoundToInt(componentWidth * detectedTabletContourPadding.x));
        var paddingY = Mathf.Max(3, Mathf.RoundToInt(componentHeight * detectedTabletContourPadding.y));

        return new CheckerboardMarkerCandidate
        {
            Found = true,
            Score = bestComponent.Score,
            Contrast = minCheckerContrast,
            MinGridX = Mathf.Clamp(bestComponent.MinX - paddingX, 0, gridWidth),
            MaxGridX = Mathf.Clamp(bestComponent.MaxX + paddingX + 1, 0, gridWidth),
            MinGridY = Mathf.Clamp(bestComponent.MinY - paddingY, 0, gridHeight),
            MaxGridY = Mathf.Clamp(bestComponent.MaxY + paddingY + 1, 0, gridHeight),
            Corner0Grid = ClampGridPoint(bestComponent.Corner0Grid, gridWidth, gridHeight),
            Corner1Grid = ClampGridPoint(bestComponent.Corner1Grid, gridWidth, gridHeight),
            Corner2Grid = ClampGridPoint(bestComponent.Corner2Grid, gridWidth, gridHeight),
            Corner3Grid = ClampGridPoint(bestComponent.Corner3Grid, gridWidth, gridHeight)
        };
    }

    private Vector2 ClampGridPoint(Vector2 point, int gridWidth, int gridHeight)
    {
        return new Vector2(
            Mathf.Clamp(point.x, 0f, gridWidth),
            Mathf.Clamp(point.y, 0f, gridHeight));
    }

    private void MarkCheckerContrastCells(int gridWidth, int gridHeight)
    {
        Array.Clear(whiteCells, 0, whiteCells.Length);
        Array.Clear(visitedCells, 0, visitedCells.Length);

        for (var y = 1; y < gridHeight - 1; y++)
        {
            var rowStart = y * gridWidth;
            for (var x = 1; x < gridWidth - 1; x++)
            {
                var luminance = ReadGridLuminance(x, y);
                var maxDelta = 0;
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(luminance - ReadGridLuminance(x - 1, y)));
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(luminance - ReadGridLuminance(x + 1, y)));
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(luminance - ReadGridLuminance(x, y - 1)));
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(luminance - ReadGridLuminance(x, y + 1)));

                if (maxDelta >= minCheckerContrast)
                {
                    whiteCells[rowStart + x] = 1;
                }
            }
        }
    }

    private CheckerContrastComponent FindLargestCheckerContrastComponent(int gridWidth, int gridHeight)
    {
        var bestComponent = new CheckerContrastComponent();
        var totalArea = Mathf.Max(1, gridWidth * gridHeight);

        for (var startIndex = 0; startIndex < whiteCells.Length; startIndex++)
        {
            if (whiteCells[startIndex] == 0 || visitedCells[startIndex] != 0)
            {
                continue;
            }

            var component = ReadCheckerContrastComponent(startIndex, gridWidth, gridHeight);
            if (!component.Found)
            {
                continue;
            }

            var componentWidth = component.MaxX - component.MinX + 1;
            var componentHeight = component.MaxY - component.MinY + 1;
            var componentArea = Mathf.Max(1, componentWidth * componentHeight);
            var frameCoverage = componentArea / (float)totalArea;
            var markerSize = Mathf.Max(componentWidth / (float)gridWidth, componentHeight / (float)gridHeight);
            var aspect = componentWidth / (float)Mathf.Max(1, componentHeight);
            var aspectLimit = Mathf.Max(1f, maxCheckerAspect);
            var aspectOk = aspect <= aspectLimit && aspect >= 1f / aspectLimit;
            var patternScore = CalculateCheckerPatternScore(component.MinX, component.MaxX, component.MinY, component.MaxY);

            if (markerSize < minCheckerSize
                || markerSize > maxCheckerSize
                || component.Density < minCheckerEdgeDensity
                || patternScore < minCheckerPatternScore
                || !aspectOk)
            {
                continue;
            }

            component.PatternScore = patternScore;
            component.Score = frameCoverage + component.Density * 0.6f + patternScore * 1.8f;
            if (!bestComponent.Found || component.Score > bestComponent.Score)
            {
                bestComponent = component;
            }
        }

        return bestComponent;
    }

    private float CalculateCheckerPatternScore(int minX, int maxX, int minY, int maxY)
    {
        var bestScore = CalculateCheckerTextureScore(minX, maxX, minY, maxY);
        for (var columns = 4; columns <= 14; columns++)
        {
            for (var rows = 4; rows <= 10; rows++)
            {
                bestScore = Mathf.Max(bestScore, CalculateExactCheckerPatternScore(minX, maxX, minY, maxY, columns, rows));
            }
        }

        return bestScore;
    }

    private float CalculateCheckerTextureScore(int minX, int maxX, int minY, int maxY)
    {
        var width = Mathf.Max(1, maxX - minX + 1);
        var height = Mathf.Max(1, maxY - minY + 1);
        if (width < 12 || height < 12)
        {
            return 0f;
        }

        var insetX = Mathf.Max(1, Mathf.RoundToInt(width * 0.03f));
        var insetY = Mathf.Max(1, Mathf.RoundToInt(height * 0.03f));
        minX += insetX;
        maxX -= insetX;
        minY += insetY;
        maxY -= insetY;
        width = Mathf.Max(1, maxX - minX + 1);
        height = Mathf.Max(1, maxY - minY + 1);

        var minLum = 255;
        var maxLum = 0;
        var sumLum = 0;
        var totalCells = 0;
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var luminance = ReadGridLuminance(x, y);
                minLum = Mathf.Min(minLum, luminance);
                maxLum = Mathf.Max(maxLum, luminance);
                sumLum += luminance;
                totalCells++;
            }
        }

        if (maxLum - minLum < minCheckerContrast * 2
            || minLum > maxCheckerDarkLuminance
            || maxLum < minCheckerLightLuminance)
        {
            return 0f;
        }

        var threshold = sumLum / Mathf.Max(1, totalCells);
        var darkCells = 0;
        var lightCells = 0;
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var luminance = ReadGridLuminance(x, y);
                if (luminance < threshold - 8)
                {
                    darkCells++;
                }
                else if (luminance > threshold + 8)
                {
                    lightCells++;
                }
            }
        }

        var darkBalance = darkCells / (float)Mathf.Max(1, totalCells);
        var lightBalance = lightCells / (float)Mathf.Max(1, totalCells);
        if (darkBalance < 0.25f || darkBalance > 0.75f || lightBalance < 0.25f || lightBalance > 0.75f)
        {
            return 0f;
        }

        var verticalLineCount = 0;
        var horizontalLineCount = 0;
        var verticalTransitions = 0;
        var horizontalTransitions = 0;
        var verticalPairs = 0;
        var horizontalPairs = 0;

        for (var x = minX + 1; x <= maxX; x++)
        {
            var edgeHits = 0;
            for (var y = minY; y <= maxY; y++)
            {
                var left = ReadGridLuminance(x - 1, y);
                var right = ReadGridLuminance(x, y);
                if (CrossesCheckerThreshold(left, right, threshold))
                {
                    edgeHits++;
                }
            }

            verticalTransitions += edgeHits;
            verticalPairs += height;
            if (edgeHits / (float)Mathf.Max(1, height) >= 0.16f)
            {
                verticalLineCount++;
            }
        }

        for (var y = minY + 1; y <= maxY; y++)
        {
            var edgeHits = 0;
            for (var x = minX; x <= maxX; x++)
            {
                var bottom = ReadGridLuminance(x, y - 1);
                var top = ReadGridLuminance(x, y);
                if (CrossesCheckerThreshold(bottom, top, threshold))
                {
                    edgeHits++;
                }
            }

            horizontalTransitions += edgeHits;
            horizontalPairs += width;
            if (edgeHits / (float)Mathf.Max(1, width) >= 0.16f)
            {
                horizontalLineCount++;
            }
        }

        if (verticalLineCount < 4 || horizontalLineCount < 4)
        {
            return 0f;
        }

        var verticalTransitionRatio = verticalTransitions / (float)Mathf.Max(1, verticalPairs);
        var horizontalTransitionRatio = horizontalTransitions / (float)Mathf.Max(1, horizontalPairs);
        if (verticalTransitionRatio < 0.045f || horizontalTransitionRatio < 0.045f)
        {
            return 0f;
        }

        var transitionScore = Mathf.Clamp01((verticalTransitionRatio + horizontalTransitionRatio) / 0.18f);
        var lineScore = Mathf.Min(Mathf.Clamp01(verticalLineCount / 6f), Mathf.Clamp01(horizontalLineCount / 5f));
        var balanceScore = 1f - Mathf.Clamp01(Mathf.Abs(darkBalance - 0.5f) / 0.25f);
        var contrastScore = Mathf.Clamp01((maxLum - minLum) / 170f);
        return transitionScore * 0.35f + lineScore * 0.35f + balanceScore * 0.15f + contrastScore * 0.15f;
    }

    private bool CrossesCheckerThreshold(int first, int second, int threshold)
    {
        return Mathf.Abs(first - second) >= minCheckerContrast
            && ((first < threshold && second > threshold) || (first > threshold && second < threshold));
    }

    private float CalculateExactCheckerPatternScore(int minX, int maxX, int minY, int maxY, int columns, int rows)
    {
        if (columns * rows < 24)
        {
            return 0f;
        }

        var width = Mathf.Max(1, maxX - minX + 1);
        var height = Mathf.Max(1, maxY - minY + 1);
        if (width < columns || height < rows)
        {
            return 0f;
        }

        var values = new float[columns, rows];
        var darkSum = 0f;
        var lightSum = 0f;
        var darkCount = 0;
        var lightCount = 0;

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var sampleX = Mathf.Clamp(minX + Mathf.FloorToInt((x + 0.5f) * width / columns), minX, maxX);
                var sampleY = Mathf.Clamp(minY + Mathf.FloorToInt((y + 0.5f) * height / rows), minY, maxY);
                var luminance = ReadGridLuminance(sampleX, sampleY);
                values[x, y] = luminance;

                if (((x + y) & 1) == 0)
                {
                    darkSum += luminance;
                    darkCount++;
                }
                else
                {
                    lightSum += luminance;
                    lightCount++;
                }
            }
        }

        var firstAverage = darkSum / Mathf.Max(1, darkCount);
        var secondAverage = lightSum / Mathf.Max(1, lightCount);
        var contrast = Mathf.Abs(firstAverage - secondAverage);
        var darkerAverage = Mathf.Min(firstAverage, secondAverage);
        var lighterAverage = Mathf.Max(firstAverage, secondAverage);
        if (contrast < minCheckerContrast
            || darkerAverage > maxCheckerDarkLuminance
            || lighterAverage < minCheckerLightLuminance)
        {
            return 0f;
        }

        var horizontalAlternations = 0;
        var verticalAlternations = 0;
        var horizontalPairs = 0;
        var verticalPairs = 0;
        var patternMatches = 0;
        var darkCells = 0;
        var totalCells = Mathf.Max(1, columns * rows);
        var threshold = (firstAverage + secondAverage) * 0.5f;
        var evenCellsAreDark = firstAverage < secondAverage;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var shouldBeDark = (((x + y) & 1) == 0) == evenCellsAreDark;
                var isDark = values[x, y] < threshold;
                if (isDark)
                {
                    darkCells++;
                }

                if (shouldBeDark == isDark)
                {
                    patternMatches++;
                }

                if (x + 1 < columns)
                {
                    horizontalPairs++;
                    var nextIsDark = values[x + 1, y] < threshold;
                    if (isDark != nextIsDark && Mathf.Abs(values[x, y] - values[x + 1, y]) >= minCheckerContrast)
                    {
                        horizontalAlternations++;
                    }
                }

                if (y + 1 < rows)
                {
                    verticalPairs++;
                    var nextIsDark = values[x, y + 1] < threshold;
                    if (isDark != nextIsDark && Mathf.Abs(values[x, y] - values[x, y + 1]) >= minCheckerContrast)
                    {
                        verticalAlternations++;
                    }
                }
            }
        }

        var horizontalScore = horizontalAlternations / (float)Mathf.Max(1, horizontalPairs);
        var verticalScore = verticalAlternations / (float)Mathf.Max(1, verticalPairs);
        var alternationScore = (horizontalScore + verticalScore) * 0.5f;
        var patternMatchScore = patternMatches / (float)totalCells;
        var darkBalance = darkCells / (float)totalCells;
        if (horizontalScore < minCheckerAxisAlternation
            || verticalScore < minCheckerAxisAlternation
            || patternMatchScore < minCheckerParityMatch
            || darkBalance < 0.35f
            || darkBalance > 0.65f)
        {
            return 0f;
        }

        var contrastScore = Mathf.Clamp01(contrast / 110f);
        return alternationScore * 0.38f + patternMatchScore * 0.42f + contrastScore * 0.2f;
    }

    private CheckerContrastComponent ReadCheckerContrastComponent(int startIndex, int gridWidth, int gridHeight)
    {
        var result = new CheckerContrastComponent();
        var head = 0;
        var tail = 0;
        var minX = gridWidth;
        var maxX = 0;
        var minY = gridHeight;
        var maxY = 0;
        var edgeCount = 0;
        var sumX = 0f;
        var sumY = 0f;
        var sumXX = 0f;
        var sumYY = 0f;
        var sumXY = 0f;

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
            edgeCount++;
            sumX += x;
            sumY += y;
            sumXX += x * x;
            sumYY += y * y;
            sumXY += x * y;

            AddWhiteNeighbor(index - 1, x > 0, ref tail);
            AddWhiteNeighbor(index + 1, x < gridWidth - 1, ref tail);
            AddWhiteNeighbor(index - gridWidth, y > 0, ref tail);
            AddWhiteNeighbor(index + gridWidth, y < gridHeight - 1, ref tail);
        }

        var componentWidth = maxX - minX + 1;
        var componentHeight = maxY - minY + 1;
        var area = Mathf.Max(1, componentWidth * componentHeight);

        result.Found = edgeCount >= 24;
        result.MinX = minX;
        result.MaxX = maxX;
        result.MinY = minY;
        result.MaxY = maxY;
        result.EdgeCount = edgeCount;
        result.Density = edgeCount / (float)area;
        ApplyOrientedComponentCorners(ref result, gridWidth, gridHeight, edgeCount, sumX, sumY, sumXX, sumYY, sumXY, tail);
        return result;
    }

    private void ApplyOrientedComponentCorners(
        ref CheckerContrastComponent component,
        int gridWidth,
        int gridHeight,
        int edgeCount,
        float sumX,
        float sumY,
        float sumXX,
        float sumYY,
        float sumXY,
        int queueLength)
    {
        if (edgeCount <= 0)
        {
            component.Corner0Grid = new Vector2(component.MinX, component.MinY);
            component.Corner1Grid = new Vector2(component.MaxX, component.MinY);
            component.Corner2Grid = new Vector2(component.MaxX, component.MaxY);
            component.Corner3Grid = new Vector2(component.MinX, component.MaxY);
            return;
        }

        var invCount = 1f / edgeCount;
        var center = new Vector2(sumX * invCount, sumY * invCount);
        var covarianceXX = sumXX * invCount - center.x * center.x;
        var covarianceYY = sumYY * invCount - center.y * center.y;
        var covarianceXY = sumXY * invCount - center.x * center.y;
        var angle = 0.5f * Mathf.Atan2(2f * covarianceXY, covarianceXX - covarianceYY);
        var axisX = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var axisY = new Vector2(-axisX.y, axisX.x);

        var minProjectionX = float.PositiveInfinity;
        var maxProjectionX = float.NegativeInfinity;
        var minProjectionY = float.PositiveInfinity;
        var maxProjectionY = float.NegativeInfinity;

        for (var index = 0; index < queueLength; index++)
        {
            var cellIndex = componentQueue[index];
            var x = cellIndex % gridWidth;
            var y = cellIndex / gridWidth;
            var offset = new Vector2(x, y) - center;
            var projectionX = Vector2.Dot(offset, axisX);
            var projectionY = Vector2.Dot(offset, axisY);
            minProjectionX = Mathf.Min(minProjectionX, projectionX);
            maxProjectionX = Mathf.Max(maxProjectionX, projectionX);
            minProjectionY = Mathf.Min(minProjectionY, projectionY);
            maxProjectionY = Mathf.Max(maxProjectionY, projectionY);
        }

        var widthPadding = Mathf.Max(1f, (maxProjectionX - minProjectionX) * detectedTabletContourPadding.x);
        var heightPadding = Mathf.Max(1f, (maxProjectionY - minProjectionY) * detectedTabletContourPadding.y);
        minProjectionX -= widthPadding;
        maxProjectionX += widthPadding;
        minProjectionY -= heightPadding;
        maxProjectionY += heightPadding;

        component.Corner0Grid = ClampGridPoint(center + axisX * minProjectionX + axisY * minProjectionY, gridWidth, gridHeight);
        component.Corner1Grid = ClampGridPoint(center + axisX * maxProjectionX + axisY * minProjectionY, gridWidth, gridHeight);
        component.Corner2Grid = ClampGridPoint(center + axisX * maxProjectionX + axisY * maxProjectionY, gridWidth, gridHeight);
        component.Corner3Grid = ClampGridPoint(center + axisX * minProjectionX + axisY * maxProjectionY, gridWidth, gridHeight);
    }

    private int ReadGridLuminance(int gridX, int gridY)
    {
        if (luminanceCells == null || luminanceCells.Length == 0)
        {
            return 0;
        }

        var gridWidth = Mathf.Max(1, currentMarkerGridWidth);
        var gridHeight = Mathf.Max(1, currentMarkerGridHeight);
        var safeX = Mathf.Clamp(gridX, 0, gridWidth - 1);
        var safeY = Mathf.Clamp(gridY, 0, gridHeight - 1);
        var index = Mathf.Clamp(safeY * gridWidth + safeX, 0, luminanceCells.Length - 1);
        return luminanceCells[index];
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

        if (markerGameCamera != null)
        {
            markerGameCamera.enabled = visible;
        }

        if (markerOverlayObject != null)
        {
            markerOverlayObject.SetActive(visible);
        }

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
        statusText = CreateText("Marker Status", panel.transform, "Поднесите шахматную доску к веб-камере", 24, FontStyle.Bold, Color.white);
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
