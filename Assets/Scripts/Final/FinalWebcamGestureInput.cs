// Автор: Марьяновский Владислав Андреевич

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum FinalGestureZone
{
    None,
    Left,
    Center,
    Right
}

public class FinalWebcamGestureInput : MonoBehaviour
{
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Text statusText;
    [SerializeField] private bool mirrorPreview = true;
    [SerializeField] private bool requestWebcamPermission = false;
    [SerializeField] private float sampleInterval = 0.05f;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 360;
    [SerializeField] private int requestedFps = 30;

    [Header("Contour")]
    [SerializeField] private bool useSkinColor = true;
    [SerializeField] private bool useMotionFallback = true;
    [SerializeField] private int motionThreshold = 28;
    [SerializeField] private int blockSize = 8;
    [SerializeField] private int minChangedPixelsPerBlock = 3;
    [SerializeField] private int minBlobBlocks = 4;
    [SerializeField] private float zoneLatchSeconds = 1.1f;
    [SerializeField] private float cameraRestartDelay = 2.5f;

    [Header("Template calibration")]
    [SerializeField] private int templateWidth = 24;
    [SerializeField] private int templateHeight = 16;
    [SerializeField] private float templateConfidenceThreshold = 0.28f;
    [SerializeField] private float templateConfidenceGap = 0.03f;
    [SerializeField] private KeyCode captureLeftKey = KeyCode.F1;
    [SerializeField] private KeyCode captureCenterKey = KeyCode.F2;
    [SerializeField] private KeyCode captureRightKey = KeyCode.F3;
    [SerializeField] private KeyCode clearTemplatesKey = KeyCode.F4;

    private const string TemplateKeyPrefix = "FinalProjectHandTemplate_";

    private WebCamTexture webcamTexture;
    private Texture2D previewTexture;
    private Color32[] currentPixels;
    private Color32[] previousPixels;
    private Color32[] previewPixels;
    private bool[] foregroundBlocks;
    private bool[] largestBlobBlocks;
    private bool[] visitedBlocks;
    private byte[] lastReducedMask;
    private byte[] leftTemplate;
    private byte[] centerTemplate;
    private byte[] rightTemplate;
    private int cachedWidth;
    private int cachedHeight;
    private int gridWidth;
    private int gridHeight;
    private float nextSampleTime;
    private float webcamStartTime;
    private float leftScore;
    private float centerScore;
    private float rightScore;
    private float lastDetectionTime;
    private float statusOverrideUntil;
    private string statusOverride;
    private FinalGestureZone activeZone;
    private FinalGestureZone heldZone;
    private FinalGestureZone lastDetectedZone;
    private float heldTime;
    private float lastConsumeTime;
    private bool triedDefaultCameraFallback;

    public FinalGestureZone ActiveZone => activeZone;
    public bool IsReady => webcamTexture != null && webcamTexture.width > 32 && webcamTexture.height > 32;
    public float LeftScore => leftScore;
    public float CenterScore => centerScore;
    public float RightScore => rightScore;

    public void SetPreview(RawImage image)
    {
        previewImage = image;
        ApplyPreviewTexture();
    }

    public void SetStatus(Text label)
    {
        statusText = label;
        UpdateStatusText();
    }

    private void Awake()
    {
        LoadTemplates();
    }

    private void Start()
    {
        StartCoroutine(StartWebcamRoutine(false));
    }

    private void Update()
    {
        HandleTemplateKeys();
        RestartSlowCameraIfNeeded();
        SampleWebcamIfNeeded();
        UpdateHeldZone();
        UpdateStatusText();
    }

    private void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }

    public float GetHoldProgress(FinalGestureZone zone, float requiredTime)
    {
        if (heldZone != zone || requiredTime <= 0.001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(heldTime / requiredTime);
    }

    public bool ConsumeHeldGesture(FinalGestureZone zone, float requiredTime)
    {
        if (heldZone != zone || heldTime < requiredTime)
        {
            return false;
        }

        if (Time.unscaledTime - lastConsumeTime < 0.45f)
        {
            return false;
        }

        lastConsumeTime = Time.unscaledTime;
        heldTime = 0f;
        return true;
    }

    private IEnumerator StartWebcamRoutine(bool useDefaultConstructor)
    {
#if !UNITY_EDITOR
        if (requestWebcamPermission && !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#else
        yield return null;
#endif

        StartWebcam(useDefaultConstructor);
    }

    private void StartWebcam(bool useDefaultConstructor)
    {
#if !UNITY_EDITOR
        if (requestWebcamPermission && !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            SetStatusOverride("Нет доступа к вебкамере. Разрешите камеру для Unity.", 3f);
            return;
        }
#endif

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        var devices = WebCamTexture.devices;
        if (devices.Length > 0 && !useDefaultConstructor)
        {
            webcamTexture = new WebCamTexture(devices[0].name, requestedWidth, requestedHeight, requestedFps);
        }
        else
        {
            webcamTexture = new WebCamTexture();
        }

        webcamTexture.Play();
        webcamStartTime = Time.unscaledTime;
        triedDefaultCameraFallback = useDefaultConstructor;
        ResetFrameCache();
        ApplyPreviewTexture();
    }

    private void RestartSlowCameraIfNeeded()
    {
        if (webcamTexture == null || IsReady)
        {
            return;
        }

        if (!triedDefaultCameraFallback && Time.unscaledTime - webcamStartTime > cameraRestartDelay)
        {
            StartWebcam(true);
            return;
        }

        if (triedDefaultCameraFallback && Time.unscaledTime - webcamStartTime > cameraRestartDelay * 2f)
        {
            StartWebcam(false);
        }
    }

    private void ApplyPreviewTexture()
    {
        if (previewImage == null)
        {
            return;
        }

        previewImage.texture = previewTexture != null ? (Texture)previewTexture : webcamTexture;
        previewImage.uvRect = mirrorPreview ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
    }

    private void ResetFrameCache()
    {
        cachedWidth = 0;
        cachedHeight = 0;
        currentPixels = null;
        previousPixels = null;
        previewPixels = null;
        previewTexture = null;
        foregroundBlocks = null;
        largestBlobBlocks = null;
        visitedBlocks = null;
        lastReducedMask = null;
        leftScore = 0f;
        centerScore = 0f;
        rightScore = 0f;
        activeZone = FinalGestureZone.None;
        heldZone = FinalGestureZone.None;
        lastDetectedZone = FinalGestureZone.None;
        heldTime = 0f;
    }

    private void HandleTemplateKeys()
    {
        if (Input.GetKeyDown(captureLeftKey))
        {
            CaptureTemplate(FinalGestureZone.Left);
        }

        if (Input.GetKeyDown(captureCenterKey))
        {
            CaptureTemplate(FinalGestureZone.Center);
        }

        if (Input.GetKeyDown(captureRightKey))
        {
            CaptureTemplate(FinalGestureZone.Right);
        }

        if (Input.GetKeyDown(clearTemplatesKey))
        {
            ClearTemplates();
        }
    }

    private void SampleWebcamIfNeeded()
    {
        if (webcamTexture == null)
        {
            activeZone = FinalGestureZone.None;
            return;
        }

        if (!webcamTexture.isPlaying)
        {
            webcamTexture.Play();
            activeZone = FinalGestureZone.None;
            return;
        }

        if (Time.unscaledTime < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.unscaledTime + sampleInterval;
        if (!IsReady)
        {
            activeZone = FinalGestureZone.None;
            return;
        }

        EnsureFrameBuffers();
        webcamTexture.GetPixels32(currentPixels);

        if (previousPixels == null)
        {
            previousPixels = new Color32[currentPixels.Length];
            Array.Copy(currentPixels, previousPixels, currentPixels.Length);
        }

        var detectedZone = DetectHandContour();
        Array.Copy(currentPixels, previousPixels, currentPixels.Length);

        if (detectedZone != FinalGestureZone.None)
        {
            activeZone = detectedZone;
            lastDetectedZone = detectedZone;
            lastDetectionTime = Time.unscaledTime;
        }
        else if (lastDetectedZone != FinalGestureZone.None && Time.unscaledTime - lastDetectionTime <= zoneLatchSeconds)
        {
            activeZone = lastDetectedZone;
        }
        else
        {
            activeZone = FinalGestureZone.None;
            lastDetectedZone = FinalGestureZone.None;
        }
    }

    private void EnsureFrameBuffers()
    {
        if (cachedWidth == webcamTexture.width && cachedHeight == webcamTexture.height && currentPixels != null)
        {
            return;
        }

        cachedWidth = webcamTexture.width;
        cachedHeight = webcamTexture.height;
        gridWidth = Mathf.CeilToInt((float)cachedWidth / blockSize);
        gridHeight = Mathf.CeilToInt((float)cachedHeight / blockSize);
        currentPixels = new Color32[cachedWidth * cachedHeight];
        previousPixels = null;
        previewPixels = new Color32[cachedWidth * cachedHeight];
        foregroundBlocks = new bool[gridWidth * gridHeight];
        largestBlobBlocks = new bool[gridWidth * gridHeight];
        visitedBlocks = new bool[gridWidth * gridHeight];
        previewTexture = new Texture2D(cachedWidth, cachedHeight, TextureFormat.RGBA32, false);
        previewTexture.wrapMode = TextureWrapMode.Clamp;
        previewTexture.filterMode = FilterMode.Point;
        ApplyPreviewTexture();
        ClearPreview();
    }

    private FinalGestureZone DetectHandContour()
    {
        Array.Clear(foregroundBlocks, 0, foregroundBlocks.Length);
        Array.Clear(largestBlobBlocks, 0, largestBlobBlocks.Length);
        Array.Clear(visitedBlocks, 0, visitedBlocks.Length);

        BuildForegroundBlocks();
        var blob = FindLargestBlob();
        MarkLargestBlob(blob);
        DrawPreview(blob);

        leftScore = 0f;
        centerScore = 0f;
        rightScore = 0f;
        lastReducedMask = null;

        if (blob.Count < minBlobBlocks)
        {
            return FinalGestureZone.None;
        }

        lastReducedMask = BuildReducedMask(largestBlobBlocks);
        return ChooseZone(blob, lastReducedMask);
    }

    private void BuildForegroundBlocks()
    {
        for (var by = 0; by < gridHeight; by++)
        {
            for (var bx = 0; bx < gridWidth; bx++)
            {
                var changed = 0;
                var startX = bx * blockSize;
                var startY = by * blockSize;
                var endX = Mathf.Min(startX + blockSize, cachedWidth);
                var endY = Mathf.Min(startY + blockSize, cachedHeight);

                for (var y = startY; y < endY; y += 2)
                {
                    for (var x = startX; x < endX; x += 2)
                    {
                        var sourceX = mirrorPreview ? cachedWidth - 1 - x : x;
                        var index = y * cachedWidth + sourceX;
                        if (IsForegroundPixel(index))
                        {
                            changed++;
                        }
                    }
                }

                foregroundBlocks[BlockIndex(bx, by)] = changed >= minChangedPixelsPerBlock;
            }
        }
    }

    private MotionBlob FindLargestBlob()
    {
        var largest = new MotionBlob();
        var queue = new Queue<int>();

        for (var by = 0; by < gridHeight; by++)
        {
            for (var bx = 0; bx < gridWidth; bx++)
            {
                var index = BlockIndex(bx, by);
                if (visitedBlocks[index] || !foregroundBlocks[index])
                {
                    continue;
                }

                var current = FloodFillBlob(bx, by, queue);
                if (current.Count > largest.Count)
                {
                    largest = current;
                }
            }
        }

        return largest;
    }

    private MotionBlob FloodFillBlob(int startX, int startY, Queue<int> queue)
    {
        var blob = new MotionBlob();
        queue.Clear();
        queue.Enqueue(BlockIndex(startX, startY));
        visitedBlocks[BlockIndex(startX, startY)] = true;

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var bx = index % gridWidth;
            var by = index / gridWidth;

            blob.Add(index, bx, by);

            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    var nx = bx + ox;
                    var ny = by + oy;
                    if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight)
                    {
                        continue;
                    }

                    var nextIndex = BlockIndex(nx, ny);
                    if (visitedBlocks[nextIndex] || !foregroundBlocks[nextIndex])
                    {
                        continue;
                    }

                    visitedBlocks[nextIndex] = true;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        return blob;
    }

    private void MarkLargestBlob(MotionBlob blob)
    {
        if (blob.Count <= 0)
        {
            return;
        }

        foreach (var blockIndex in blob.BlockIndices)
        {
            largestBlobBlocks[blockIndex] = true;
        }
    }

    private FinalGestureZone ChooseZone(MotionBlob blob, byte[] currentMask)
    {
        var fallbackZone = GetZoneByBlobPosition(blob);
        var fallbackConfidence = Mathf.Clamp01((float)blob.Count / 42f);
        SetZoneScore(fallbackZone, fallbackConfidence * 0.45f);

        if (!HasAnyTemplate() || currentMask == null)
        {
            return fallbackZone;
        }

        var bestZone = FinalGestureZone.None;
        var bestScore = -1f;
        var secondScore = -1f;

        CheckTemplate(currentMask, leftTemplate, FinalGestureZone.Left, ref bestZone, ref bestScore, ref secondScore);
        CheckTemplate(currentMask, centerTemplate, FinalGestureZone.Center, ref bestZone, ref bestScore, ref secondScore);
        CheckTemplate(currentMask, rightTemplate, FinalGestureZone.Right, ref bestZone, ref bestScore, ref secondScore);

        if (bestZone != FinalGestureZone.None)
        {
            SetZoneScore(bestZone, bestScore);
        }

        if (bestZone != FinalGestureZone.None && bestScore >= templateConfidenceThreshold && bestScore - secondScore >= templateConfidenceGap)
        {
            return bestZone;
        }

        return FinalGestureZone.None;
    }

    private void CheckTemplate(byte[] currentMask, byte[] template, FinalGestureZone zone, ref FinalGestureZone bestZone, ref float bestScore, ref float secondScore)
    {
        if (template == null)
        {
            return;
        }

        var score = CompareMasks(currentMask, template);
        SetZoneScore(zone, score);

        if (score > bestScore)
        {
            secondScore = bestScore;
            bestScore = score;
            bestZone = zone;
        }
        else if (score > secondScore)
        {
            secondScore = score;
        }
    }

    private FinalGestureZone GetZoneByBlobPosition(MotionBlob blob)
    {
        var normalizedX = blob.CenterX / Mathf.Max(1f, gridWidth - 1f);
        if (normalizedX < 0.34f)
        {
            return FinalGestureZone.Left;
        }

        if (normalizedX > 0.66f)
        {
            return FinalGestureZone.Right;
        }

        return FinalGestureZone.Center;
    }

    private void SetZoneScore(FinalGestureZone zone, float score)
    {
        if (zone == FinalGestureZone.Left)
        {
            leftScore = Mathf.Max(leftScore, score);
        }
        else if (zone == FinalGestureZone.Center)
        {
            centerScore = Mathf.Max(centerScore, score);
        }
        else if (zone == FinalGestureZone.Right)
        {
            rightScore = Mathf.Max(rightScore, score);
        }
    }

    private bool IsForegroundPixel(int index)
    {
        var current = currentPixels[index];
        if (useSkinColor && IsSkinPixel(current))
        {
            return true;
        }

        return useMotionFallback && previousPixels != null && IsMovingPixel(index);
    }

    private bool IsMovingPixel(int index)
    {
        var current = currentPixels[index];
        var previous = previousPixels[index];
        var difference = Mathf.Abs(current.r - previous.r) + Mathf.Abs(current.g - previous.g) + Mathf.Abs(current.b - previous.b);
        return difference >= motionThreshold;
    }

    private bool IsSkinPixel(Color32 color)
    {
        var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        var min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));

        if (color.r < 70 || color.g < 35 || color.b < 18)
        {
            return false;
        }

        if (max - min < 16)
        {
            return false;
        }

        return color.r > color.b + 8 && color.r >= color.g - 8 && color.g > color.b - 4;
    }

    private byte[] BuildReducedMask(bool[] sourceBlocks)
    {
        if (sourceBlocks == null || templateWidth <= 0 || templateHeight <= 0)
        {
            return null;
        }

        var mask = new byte[templateWidth * templateHeight];
        for (var ty = 0; ty < templateHeight; ty++)
        {
            var startY = Mathf.FloorToInt((float)ty / templateHeight * gridHeight);
            var endY = Mathf.Max(startY + 1, Mathf.FloorToInt((float)(ty + 1) / templateHeight * gridHeight));

            for (var tx = 0; tx < templateWidth; tx++)
            {
                var startX = Mathf.FloorToInt((float)tx / templateWidth * gridWidth);
                var endX = Mathf.Max(startX + 1, Mathf.FloorToInt((float)(tx + 1) / templateWidth * gridWidth));
                var total = 0;
                var filled = 0;

                for (var y = startY; y < endY && y < gridHeight; y++)
                {
                    for (var x = startX; x < endX && x < gridWidth; x++)
                    {
                        total++;
                        if (sourceBlocks[BlockIndex(x, y)])
                        {
                            filled++;
                        }
                    }
                }

                mask[ty * templateWidth + tx] = total > 0 && filled * 2 >= total ? (byte)1 : (byte)0;
            }
        }

        return mask;
    }

    private float CompareMasks(byte[] currentMask, byte[] template)
    {
        if (currentMask == null || template == null || currentMask.Length != template.Length)
        {
            return 0f;
        }

        var intersection = 0;
        var union = 0;

        for (var i = 0; i < currentMask.Length; i++)
        {
            var current = currentMask[i] > 0;
            var saved = template[i] > 0;
            if (current && saved)
            {
                intersection++;
            }

            if (current || saved)
            {
                union++;
            }
        }

        return union == 0 ? 0f : (float)intersection / union;
    }

    private void CaptureTemplate(FinalGestureZone zone)
    {
        if (lastReducedMask == null)
        {
            SetStatusOverride("Образец не сохранен: поднимите руку так, чтобы появился белый контур.", 3f);
            return;
        }

        var copy = new byte[lastReducedMask.Length];
        Array.Copy(lastReducedMask, copy, lastReducedMask.Length);

        if (zone == FinalGestureZone.Left)
        {
            leftTemplate = copy;
        }
        else if (zone == FinalGestureZone.Center)
        {
            centerTemplate = copy;
        }
        else if (zone == FinalGestureZone.Right)
        {
            rightTemplate = copy;
        }

        SaveTemplate(zone, copy);
        SetStatusOverride("Образец сохранен: " + GetZoneLabel(zone) + ".", 3f);
    }

    private void ClearTemplates()
    {
        leftTemplate = null;
        centerTemplate = null;
        rightTemplate = null;
        PlayerPrefs.DeleteKey(TemplateKeyPrefix + FinalGestureZone.Left);
        PlayerPrefs.DeleteKey(TemplateKeyPrefix + FinalGestureZone.Center);
        PlayerPrefs.DeleteKey(TemplateKeyPrefix + FinalGestureZone.Right);
        PlayerPrefs.Save();
        SetStatusOverride("Образцы рук сброшены. Сохраните новые через F1, F2, F3.", 3f);
    }

    private void LoadTemplates()
    {
        leftTemplate = LoadTemplate(FinalGestureZone.Left);
        centerTemplate = LoadTemplate(FinalGestureZone.Center);
        rightTemplate = LoadTemplate(FinalGestureZone.Right);
    }

    private void SaveTemplate(FinalGestureZone zone, byte[] template)
    {
        PlayerPrefs.SetString(TemplateKeyPrefix + zone, Convert.ToBase64String(template));
        PlayerPrefs.Save();
    }

    private byte[] LoadTemplate(FinalGestureZone zone)
    {
        var encoded = PlayerPrefs.GetString(TemplateKeyPrefix + zone, "");
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            var template = Convert.FromBase64String(encoded);
            return template.Length == templateWidth * templateHeight ? template : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private bool HasAnyTemplate()
    {
        return leftTemplate != null || centerTemplate != null || rightTemplate != null;
    }

    private int TemplateCount()
    {
        var count = 0;
        if (leftTemplate != null)
        {
            count++;
        }

        if (centerTemplate != null)
        {
            count++;
        }

        if (rightTemplate != null)
        {
            count++;
        }

        return count;
    }

    private string GetZoneLabel(FinalGestureZone zone)
    {
        if (zone == FinalGestureZone.Left)
        {
            return "левая рука";
        }

        if (zone == FinalGestureZone.Center)
        {
            return "рука по центру";
        }

        if (zone == FinalGestureZone.Right)
        {
            return "правая рука";
        }

        return "нет зоны";
    }

    private void SetStatusOverride(string message, float seconds)
    {
        statusOverride = message;
        statusOverrideUntil = Time.unscaledTime + seconds;
    }

    private void DrawPreview(MotionBlob blob)
    {
        for (var i = 0; i < previewPixels.Length; i++)
        {
            var gray = (byte)(GetGray(currentPixels[i]) * 0.35f);
            previewPixels[i] = new Color32(gray, gray, gray, 255);
        }

        for (var by = 0; by < gridHeight; by++)
        {
            for (var bx = 0; bx < gridWidth; bx++)
            {
                var blockIndex = BlockIndex(bx, by);
                if (!foregroundBlocks[blockIndex])
                {
                    continue;
                }

                var color = largestBlobBlocks[blockIndex]
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(85, 85, 85, 255);

                FillPreviewBlock(bx, by, color);
            }
        }

        if (blob.Count >= minBlobBlocks)
        {
            DrawBlobFrame(blob);
        }

        previewTexture.SetPixels32(previewPixels);
        previewTexture.Apply(false);
    }

    private byte GetGray(Color32 color)
    {
        return (byte)((color.r * 30 + color.g * 59 + color.b * 11) / 100);
    }

    private void FillPreviewBlock(int bx, int by, Color32 color)
    {
        var startX = bx * blockSize;
        var startY = by * blockSize;
        var endX = Mathf.Min(startX + blockSize, cachedWidth);
        var endY = Mathf.Min(startY + blockSize, cachedHeight);

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                previewPixels[y * cachedWidth + x] = color;
            }
        }
    }

    private void DrawBlobFrame(MotionBlob blob)
    {
        var minX = Mathf.Clamp(blob.MinX * blockSize, 0, cachedWidth - 1);
        var maxX = Mathf.Clamp((blob.MaxX + 1) * blockSize - 1, 0, cachedWidth - 1);
        var minY = Mathf.Clamp(blob.MinY * blockSize, 0, cachedHeight - 1);
        var maxY = Mathf.Clamp((blob.MaxY + 1) * blockSize - 1, 0, cachedHeight - 1);
        var frameColor = new Color32(120, 255, 150, 255);

        for (var x = minX; x <= maxX; x++)
        {
            previewPixels[minY * cachedWidth + x] = frameColor;
            previewPixels[maxY * cachedWidth + x] = frameColor;
        }

        for (var y = minY; y <= maxY; y++)
        {
            previewPixels[y * cachedWidth + minX] = frameColor;
            previewPixels[y * cachedWidth + maxX] = frameColor;
        }
    }

    private void ClearPreview()
    {
        if (previewTexture == null || previewPixels == null)
        {
            return;
        }

        for (var i = 0; i < previewPixels.Length; i++)
        {
            previewPixels[i] = new Color32(0, 0, 0, 255);
        }

        previewTexture.SetPixels32(previewPixels);
        previewTexture.Apply(false);
    }

    private int BlockIndex(int x, int y)
    {
        return y * gridWidth + x;
    }

    private void UpdateHeldZone()
    {
        if (activeZone == FinalGestureZone.None)
        {
            heldZone = FinalGestureZone.None;
            heldTime = 0f;
            return;
        }

        if (heldZone != activeZone)
        {
            heldZone = activeZone;
            heldTime = 0f;
        }

        heldTime += Time.unscaledDeltaTime;
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(statusOverride) && Time.unscaledTime < statusOverrideUntil)
        {
            statusText.text = statusOverride;
            return;
        }

        if (webcamTexture == null)
        {
            statusText.text = "Вебкамера запускается. Если кадра нет, проверьте доступ к камере.";
            return;
        }

        if (!webcamTexture.isPlaying)
        {
            statusText.text = "Вебкамера перезапускается...";
            return;
        }

        if (!IsReady)
        {
            statusText.text = "Вебкамера запускается, ждем первый кадр...";
            return;
        }

        var zone = activeZone == FinalGestureZone.None ? "рука не распознана" : GetZoneLabel(activeZone);
        statusText.text = "AR образцы: " + zone + ". F1 левая, F2 центр, F3 правая, F4 сброс. Сохранено: " + TemplateCount() + "/3.";
    }

    private struct MotionBlob
    {
        public int Count;
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
        public float CenterX => Count <= 0 ? 0f : sumX / Count;

        public List<int> BlockIndices => blockIndices ?? (blockIndices = new List<int>());

        private int sumX;
        private List<int> blockIndices;

        public void Add(int index, int x, int y)
        {
            if (Count == 0)
            {
                MinX = x;
                MaxX = x;
                MinY = y;
                MaxY = y;
            }
            else
            {
                MinX = Mathf.Min(MinX, x);
                MaxX = Mathf.Max(MaxX, x);
                MinY = Mathf.Min(MinY, y);
                MaxY = Mathf.Max(MaxY, y);
            }

            Count++;
            sumX += x;
            BlockIndices.Add(index);
        }
    }
}
