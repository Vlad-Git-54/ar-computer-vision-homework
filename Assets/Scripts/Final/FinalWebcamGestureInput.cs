// Автор: Марьяновский Владислав Андреевич

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
    [SerializeField] private float sampleInterval = 0.05f;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 360;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private bool useSkinColor = true;
    [SerializeField] private bool useMotionFallback = true;
    [SerializeField] private int motionThreshold = 28;
    [SerializeField] private int blockSize = 8;
    [SerializeField] private int minChangedPixelsPerBlock = 3;
    [SerializeField] private int minBlobBlocks = 4;
    [SerializeField] private float zoneLatchSeconds = 1.1f;
    [SerializeField] private float cameraRestartDelay = 2.5f;

    private WebCamTexture webcamTexture;
    private Texture2D binaryPreviewTexture;
    private Color32[] currentPixels;
    private Color32[] previousPixels;
    private Color32[] binaryPixels;
    private bool[] movingBlocks;
    private bool[] largestBlobBlocks;
    private bool[] visitedBlocks;
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

    private IEnumerator Start()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }

        StartWebcam(false);
    }

    private void Update()
    {
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

    private void StartWebcam(bool useDefaultConstructor)
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            UpdateStatusText();
            return;
        }

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

        previewImage.texture = binaryPreviewTexture != null ? (Texture)binaryPreviewTexture : webcamTexture;
        previewImage.uvRect = mirrorPreview ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
    }

    private void ResetFrameCache()
    {
        cachedWidth = 0;
        cachedHeight = 0;
        currentPixels = null;
        previousPixels = null;
        binaryPixels = null;
        binaryPreviewTexture = null;
        movingBlocks = null;
        largestBlobBlocks = null;
        visitedBlocks = null;
        leftScore = 0f;
        centerScore = 0f;
        rightScore = 0f;
        activeZone = FinalGestureZone.None;
        heldZone = FinalGestureZone.None;
        lastDetectedZone = FinalGestureZone.None;
        heldTime = 0f;
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
            System.Array.Copy(currentPixels, previousPixels, currentPixels.Length);
        }

        var detectedZone = DetectHandContour();
        System.Array.Copy(currentPixels, previousPixels, currentPixels.Length);

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
        binaryPixels = new Color32[cachedWidth * cachedHeight];
        movingBlocks = new bool[gridWidth * gridHeight];
        largestBlobBlocks = new bool[gridWidth * gridHeight];
        visitedBlocks = new bool[gridWidth * gridHeight];
        binaryPreviewTexture = new Texture2D(cachedWidth, cachedHeight, TextureFormat.RGBA32, false);
        binaryPreviewTexture.wrapMode = TextureWrapMode.Clamp;
        binaryPreviewTexture.filterMode = FilterMode.Point;
        ApplyPreviewTexture();
        ClearPreview();
    }

    private FinalGestureZone DetectHandContour()
    {
        System.Array.Clear(movingBlocks, 0, movingBlocks.Length);
        System.Array.Clear(largestBlobBlocks, 0, largestBlobBlocks.Length);
        System.Array.Clear(visitedBlocks, 0, visitedBlocks.Length);

        BuildForegroundBlocks();
        var blob = FindLargestBlob();
        DrawMotionPreview(blob);

        leftScore = 0f;
        centerScore = 0f;
        rightScore = 0f;

        if (blob.Count < minBlobBlocks)
        {
            return FinalGestureZone.None;
        }

        var normalizedX = blob.CenterX / Mathf.Max(1f, gridWidth - 1f);
        var confidence = Mathf.Clamp01((float)blob.Count / 42f);
        if (normalizedX < 0.34f)
        {
            leftScore = confidence;
            return FinalGestureZone.Left;
        }

        if (normalizedX > 0.66f)
        {
            rightScore = confidence;
            return FinalGestureZone.Right;
        }

        centerScore = confidence;
        return FinalGestureZone.Center;
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

                movingBlocks[BlockIndex(bx, by)] = changed >= minChangedPixelsPerBlock;
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
                if (visitedBlocks[index] || !movingBlocks[index])
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

        if (largest.Count > 0)
        {
            foreach (var blockIndex in largest.BlockIndices)
            {
                largestBlobBlocks[blockIndex] = true;
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
                    if (visitedBlocks[nextIndex] || !movingBlocks[nextIndex])
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

    private void DrawMotionPreview(MotionBlob blob)
    {
        for (var i = 0; i < binaryPixels.Length; i++)
        {
            binaryPixels[i] = new Color32(0, 0, 0, 255);
        }

        for (var by = 0; by < gridHeight; by++)
        {
            for (var bx = 0; bx < gridWidth; bx++)
            {
                var blockIndex = BlockIndex(bx, by);
                if (!movingBlocks[blockIndex])
                {
                    continue;
                }

                var color = largestBlobBlocks[blockIndex]
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(70, 70, 70, 255);

                FillPreviewBlock(bx, by, color);
            }
        }

        if (blob.Count >= minBlobBlocks)
        {
            DrawBlobFrame(blob);
        }

        binaryPreviewTexture.SetPixels32(binaryPixels);
        binaryPreviewTexture.Apply(false);
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
                binaryPixels[y * cachedWidth + x] = color;
            }
        }
    }

    private void DrawBlobFrame(MotionBlob blob)
    {
        var minX = Mathf.Clamp(blob.MinX * blockSize, 0, cachedWidth - 1);
        var maxX = Mathf.Clamp((blob.MaxX + 1) * blockSize - 1, 0, cachedWidth - 1);
        var minY = Mathf.Clamp(blob.MinY * blockSize, 0, cachedHeight - 1);
        var maxY = Mathf.Clamp((blob.MaxY + 1) * blockSize - 1, 0, cachedHeight - 1);
        var frameColor = new Color32(255, 255, 255, 255);

        for (var x = minX; x <= maxX; x++)
        {
            binaryPixels[minY * cachedWidth + x] = frameColor;
            binaryPixels[maxY * cachedWidth + x] = frameColor;
        }

        for (var y = minY; y <= maxY; y++)
        {
            binaryPixels[y * cachedWidth + minX] = frameColor;
            binaryPixels[y * cachedWidth + maxX] = frameColor;
        }
    }

    private void ClearPreview()
    {
        if (binaryPreviewTexture == null || binaryPixels == null)
        {
            return;
        }

        for (var i = 0; i < binaryPixels.Length; i++)
        {
            binaryPixels[i] = new Color32(0, 0, 0, 255);
        }

        binaryPreviewTexture.SetPixels32(binaryPixels);
        binaryPreviewTexture.Apply(false);
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

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            statusText.text = "Нет доступа к вебкамере. Разрешите камеру для Unity.";
            return;
        }

        if (WebCamTexture.devices.Length == 0 && webcamTexture == null)
        {
            statusText.text = "Вебкамера не найдена. Можно использовать клавиатуру.";
            return;
        }

        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            statusText.text = "Вебкамера запускается или перезапускается...";
            return;
        }

        if (!IsReady)
        {
            statusText.text = "Вебкамера запускается, ждём кадр...";
            return;
        }

        var zone = activeZone == FinalGestureZone.Left ? "левая зона" :
            activeZone == FinalGestureZone.Center ? "центр" :
            activeZone == FinalGestureZone.Right ? "правая зона" : "движение руки не найдено";

        statusText.text = "AR контур руки: " + zone + ". Держите руку в нужной зоне.";
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
