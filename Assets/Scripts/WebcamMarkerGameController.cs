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
    [SerializeField] private int darkThreshold = 72;
    [SerializeField] private int sampleStep = 4;
    [SerializeField] private float minMarkerSize = 0.08f;
    [SerializeField] private float maxMarkerSize = 0.68f;
    [SerializeField] private float minMarkerDensity = 0.18f;
    [SerializeField] private float markerDistanceFromCamera = 7.2f;
    [SerializeField] private float gameScaleAtMarker = 0.18f;
    [SerializeField] private float followSharpness = 8f;
    [SerializeField] private float markerLostDelay = 0.45f;
    [SerializeField] private string markerAssetPath = "Assets/Textures/ARHomeworkQrMarker.png";
    [SerializeField] private bool setupSceneAutomatically = true;
    [SerializeField] private Vector3 arCameraPosition = new Vector3(0f, 2.8f, -2.5f);
    [SerializeField] private Vector3 arCameraRotation = new Vector3(14f, 0f, 0f);

    private WebCamTexture webcamTexture;
    private Color32[] cameraPixels;
    private int[] darkColumnCounts;
    private int[] darkRowCounts;
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
        UpdateStatus(false, "Поднесите маркер к веб-камере");
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
            UpdateStatus(true, "Маркер найден, игра размещена на нём");
            return;
        }

        UpdateStatus(false, markerWasFound ? "Маркер потерян, покажите его камере снова" : "Поднесите маркер к веб-камере");
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

        if (darkColumnCounts == null || darkColumnCounts.Length != width)
        {
            darkColumnCounts = new int[width];
        }
        else
        {
            Array.Clear(darkColumnCounts, 0, darkColumnCounts.Length);
        }

        if (darkRowCounts == null || darkRowCounts.Length != height)
        {
            darkRowCounts = new int[height];
        }
        else
        {
            Array.Clear(darkRowCounts, 0, darkRowCounts.Length);
        }

        webcamTexture.GetPixels32(cameraPixels);

        var darkPixels = 0;

        for (var y = 0; y < height; y += sampleStep)
        {
            var lineStart = y * width;
            for (var x = 0; x < width; x += sampleStep)
            {
                var color = cameraPixels[lineStart + x];
                var luminance = (color.r * 30 + color.g * 59 + color.b * 11) / 100;
                if (luminance > darkThreshold)
                {
                    continue;
                }

                darkPixels++;
                darkColumnCounts[x]++;
                darkRowCounts[y]++;
            }
        }

        if (darkPixels < 18)
        {
            return observation;
        }

        var minX = FindHistogramPosition(darkColumnCounts, darkPixels, 0.04f);
        var maxX = FindHistogramPosition(darkColumnCounts, darkPixels, 0.96f);
        var minY = FindHistogramPosition(darkRowCounts, darkPixels, 0.04f);
        var maxY = FindHistogramPosition(darkRowCounts, darkPixels, 0.96f);

        if (minX >= maxX || minY >= maxY)
        {
            return observation;
        }

        var markerWidth = maxX - minX + 1;
        var markerHeight = maxY - minY + 1;
        var markerSize = Mathf.Max(markerWidth / (float)width, markerHeight / (float)height);
        var aspect = markerWidth / (float)markerHeight;
        var sampledArea = Mathf.Max(1f, markerWidth * markerHeight / (float)(sampleStep * sampleStep));
        var density = darkPixels / sampledArea;

        if (markerSize < minMarkerSize || markerSize > maxMarkerSize || aspect < 0.55f || aspect > 1.8f || density < minMarkerDensity)
        {
            return observation;
        }

        observation.Found = true;
        observation.Center = new Vector2((minX + maxX) * 0.5f / width, (minY + maxY) * 0.5f / height);
        observation.Size = markerSize;
        observation.Density = density;
        return observation;
    }

    private int FindHistogramPosition(int[] histogram, int totalCount, float fraction)
    {
        var targetCount = Mathf.Clamp(Mathf.RoundToInt(totalCount * fraction), 1, totalCount);
        var accumulated = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            accumulated += histogram[i];
            if (accumulated >= targetCount)
            {
                return i;
            }
        }

        return histogram.Length - 1;
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
            currentRigidbody.isKinematic = !visible;
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
        statusText = CreateText("Marker Status", panel.transform, "Поднесите маркер к веб-камере", 24, FontStyle.Bold, Color.white);
        SetStretch(statusText.rectTransform, new Vector2(22f, 38f), new Vector2(22f, 12f));

        helpText = CreateText("Marker Help", panel.transform, "Маркер для проверки: " + markerAssetPath, 18, FontStyle.Normal, new Color(0.82f, 0.9f, 1f, 1f));
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
            helpText.text = "Маркер для проверки: " + markerAssetPath;
        }
    }
}
