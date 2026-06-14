// Автор: Марьяновский Владислав Андреевич

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
    [SerializeField] private float sampleInterval = 0.06f;
    [SerializeField] private float activationThreshold = 0.022f;
    [SerializeField] private float confidenceGap = 0.006f;
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 360;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private int pixelStep = 5;
    [SerializeField] private int backgroundDifferenceThreshold = 34;

    private const int BackgroundFramesNeeded = 8;
    private const int TemplateWidth = 24;
    private const int TemplateHeight = 16;

    private WebCamTexture webcamTexture;
    private Texture2D binaryPreviewTexture;
    private Color32[] currentPixels;
    private Color32[] backgroundPixels;
    private Color32[] binaryPixels;
    private int[] backgroundR;
    private int[] backgroundG;
    private int[] backgroundB;
    private byte[] leftTemplate;
    private byte[] centerTemplate;
    private byte[] rightTemplate;
    private int cachedWidth;
    private int cachedHeight;
    private int backgroundFramesCollected;
    private float nextSampleTime;
    private float webcamStartTime;
    private float leftScore;
    private float centerScore;
    private float rightScore;
    private FinalGestureZone activeZone;
    private FinalGestureZone heldZone;
    private float heldTime;
    private float lastConsumeTime;
    private bool backgroundReady;
    private bool capturingBackground;
    private bool triedDefaultCameraFallback;

    public FinalGestureZone ActiveZone => activeZone;
    public bool IsReady => webcamTexture != null && webcamTexture.isPlaying && webcamTexture.width > 32 && webcamTexture.height > 32;
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

    private void Start()
    {
        StartWebcam(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            RequestBackgroundCapture();
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            CaptureTemplate(FinalGestureZone.Left);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            CaptureTemplate(FinalGestureZone.Center);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            CaptureTemplate(FinalGestureZone.Right);
        }

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
        if (WebCamTexture.devices.Length == 0)
        {
            UpdateStatusText();
            return;
        }

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        var deviceName = WebCamTexture.devices[0].name;
        webcamTexture = useDefaultConstructor
            ? new WebCamTexture(deviceName)
            : new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);

        webcamTexture.Play();
        webcamStartTime = Time.unscaledTime;
        triedDefaultCameraFallback = useDefaultConstructor;
        ResetFrameCache();
        RequestBackgroundCapture();
        ApplyPreviewTexture();
    }

    private void RestartSlowCameraIfNeeded()
    {
        if (webcamTexture == null || IsReady || triedDefaultCameraFallback)
        {
            return;
        }

        if (Time.unscaledTime - webcamStartTime > 3.5f)
        {
            StartWebcam(true);
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
        backgroundPixels = null;
        binaryPixels = null;
        binaryPreviewTexture = null;
        leftScore = 0f;
        centerScore = 0f;
        rightScore = 0f;
        activeZone = FinalGestureZone.None;
        heldZone = FinalGestureZone.None;
        heldTime = 0f;
    }

    private void RequestBackgroundCapture()
    {
        backgroundReady = false;
        capturingBackground = true;
        backgroundFramesCollected = 0;
        backgroundR = null;
        backgroundG = null;
        backgroundB = null;
    }

    private void SampleWebcamIfNeeded()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
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

        if (capturingBackground)
        {
            CaptureBackgroundFrame();
            DrawBinaryPreview(false);
            activeZone = FinalGestureZone.None;
            return;
        }

        DrawBinaryPreview(true);
        var newLeftScore = CalculateZoneScore(0f, 0.34f);
        var newCenterScore = CalculateZoneScore(0.33f, 0.67f);
        var newRightScore = CalculateZoneScore(0.66f, 1f);
        AddTemplateScores(ref newLeftScore, ref newCenterScore, ref newRightScore);

        leftScore = Mathf.Lerp(leftScore, newLeftScore, 0.68f);
        centerScore = Mathf.Lerp(centerScore, newCenterScore, 0.68f);
        rightScore = Mathf.Lerp(rightScore, newRightScore, 0.68f);

        activeZone = ChooseActiveZone();
    }

    private void EnsureFrameBuffers()
    {
        if (cachedWidth == webcamTexture.width && cachedHeight == webcamTexture.height && currentPixels != null)
        {
            return;
        }

        cachedWidth = webcamTexture.width;
        cachedHeight = webcamTexture.height;
        currentPixels = new Color32[cachedWidth * cachedHeight];
        backgroundPixels = new Color32[cachedWidth * cachedHeight];
        binaryPixels = new Color32[cachedWidth * cachedHeight];
        binaryPreviewTexture = new Texture2D(cachedWidth, cachedHeight, TextureFormat.RGBA32, false);
        binaryPreviewTexture.wrapMode = TextureWrapMode.Clamp;
        binaryPreviewTexture.filterMode = FilterMode.Point;
        ApplyPreviewTexture();
        RequestBackgroundCapture();
    }

    private void CaptureBackgroundFrame()
    {
        if (backgroundR == null || backgroundR.Length != currentPixels.Length)
        {
            backgroundR = new int[currentPixels.Length];
            backgroundG = new int[currentPixels.Length];
            backgroundB = new int[currentPixels.Length];
        }

        for (var i = 0; i < currentPixels.Length; i++)
        {
            backgroundR[i] += currentPixels[i].r;
            backgroundG[i] += currentPixels[i].g;
            backgroundB[i] += currentPixels[i].b;
        }

        backgroundFramesCollected++;
        if (backgroundFramesCollected < BackgroundFramesNeeded)
        {
            return;
        }

        for (var i = 0; i < currentPixels.Length; i++)
        {
            backgroundPixels[i] = new Color32(
                (byte)(backgroundR[i] / BackgroundFramesNeeded),
                (byte)(backgroundG[i] / BackgroundFramesNeeded),
                (byte)(backgroundB[i] / BackgroundFramesNeeded),
                255);
        }

        backgroundReady = true;
        capturingBackground = false;
    }

    private void DrawBinaryPreview(bool useBackground)
    {
        for (var i = 0; i < currentPixels.Length; i++)
        {
            var foreground = useBackground && IsForeground(i);
            byte value;
            if (useBackground)
            {
                value = foreground ? (byte)255 : (byte)0;
            }
            else
            {
                var gray = GetGray(currentPixels[i]);
                value = gray > 118 ? (byte)255 : (byte)0;
            }

            binaryPixels[i] = new Color32(value, value, value, 255);
        }

        binaryPreviewTexture.SetPixels32(binaryPixels);
        binaryPreviewTexture.Apply(false);
    }

    private float CalculateZoneScore(float minNormalizedX, float maxNormalizedX)
    {
        if (!backgroundReady)
        {
            return 0f;
        }

        var minX = Mathf.Clamp(Mathf.FloorToInt(cachedWidth * minNormalizedX), 0, cachedWidth - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(cachedWidth * maxNormalizedX), minX + 1, cachedWidth);
        var minY = Mathf.Clamp(Mathf.FloorToInt(cachedHeight * 0.28f), 0, cachedHeight - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(cachedHeight * 0.96f), minY + 1, cachedHeight);

        var total = 0;
        var foreground = 0;
        var highForeground = 0;

        for (var y = minY; y < maxY; y += pixelStep)
        {
            var heightWeight = Mathf.InverseLerp(minY, maxY, y);
            for (var x = minX; x < maxX; x += pixelStep)
            {
                var sourceX = mirrorPreview ? cachedWidth - 1 - x : x;
                var index = y * cachedWidth + sourceX;
                total++;

                if (!IsForeground(index))
                {
                    continue;
                }

                foreground++;
                if (heightWeight > 0.46f)
                {
                    highForeground++;
                }
            }
        }

        if (total <= 0 || foreground < 14)
        {
            return 0f;
        }

        var foregroundRatio = (float)foreground / total;
        var highRatio = (float)highForeground / total;
        return foregroundRatio + highRatio * 0.85f;
    }

    private bool IsForeground(int index)
    {
        if (!backgroundReady || index < 0 || index >= currentPixels.Length)
        {
            return false;
        }

        var current = currentPixels[index];
        var background = backgroundPixels[index];
        var difference = Mathf.Abs(current.r - background.r) + Mathf.Abs(current.g - background.g) + Mathf.Abs(current.b - background.b);
        return difference >= backgroundDifferenceThreshold;
    }

    private byte GetGray(Color32 color)
    {
        return (byte)((color.r * 30 + color.g * 59 + color.b * 11) / 100);
    }

    private void CaptureTemplate(FinalGestureZone zone)
    {
        if (!backgroundReady || binaryPixels == null)
        {
            return;
        }

        var template = BuildReducedMask();
        if (zone == FinalGestureZone.Left)
        {
            leftTemplate = template;
        }
        else if (zone == FinalGestureZone.Center)
        {
            centerTemplate = template;
        }
        else if (zone == FinalGestureZone.Right)
        {
            rightTemplate = template;
        }
    }

    private byte[] BuildReducedMask()
    {
        var mask = new byte[TemplateWidth * TemplateHeight];
        for (var ty = 0; ty < TemplateHeight; ty++)
        {
            var y0 = Mathf.FloorToInt((float)ty / TemplateHeight * cachedHeight);
            var y1 = Mathf.FloorToInt((float)(ty + 1) / TemplateHeight * cachedHeight);
            for (var tx = 0; tx < TemplateWidth; tx++)
            {
                var x0 = Mathf.FloorToInt((float)tx / TemplateWidth * cachedWidth);
                var x1 = Mathf.FloorToInt((float)(tx + 1) / TemplateWidth * cachedWidth);
                var total = 0;
                var filled = 0;

                for (var y = y0; y < y1; y += 2)
                {
                    for (var x = x0; x < x1; x += 2)
                    {
                        var sourceX = mirrorPreview ? cachedWidth - 1 - x : x;
                        var index = y * cachedWidth + sourceX;
                        total++;
                        if (IsForeground(index))
                        {
                            filled++;
                        }
                    }
                }

                mask[ty * TemplateWidth + tx] = total == 0 ? (byte)0 : (byte)Mathf.Clamp(filled * 255 / total, 0, 255);
            }
        }

        return mask;
    }

    private void AddTemplateScores(ref float newLeftScore, ref float newCenterScore, ref float newRightScore)
    {
        var currentMask = BuildReducedMask();
        if (leftTemplate != null)
        {
            newLeftScore += CompareTemplate(currentMask, leftTemplate) * 0.025f;
        }

        if (centerTemplate != null)
        {
            newCenterScore += CompareTemplate(currentMask, centerTemplate) * 0.025f;
        }

        if (rightTemplate != null)
        {
            newRightScore += CompareTemplate(currentMask, rightTemplate) * 0.025f;
        }
    }

    private float CompareTemplate(byte[] currentMask, byte[] template)
    {
        if (currentMask == null || template == null || currentMask.Length != template.Length)
        {
            return 0f;
        }

        var difference = 0f;
        var templateMass = 0f;
        for (var i = 0; i < currentMask.Length; i++)
        {
            difference += Mathf.Abs(currentMask[i] - template[i]) / 255f;
            templateMass += template[i] / 255f;
        }

        if (templateMass < 2f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - difference / currentMask.Length);
    }

    private FinalGestureZone ChooseActiveZone()
    {
        var bestZone = FinalGestureZone.Left;
        var bestScore = leftScore;
        var secondScore = Mathf.Max(centerScore, rightScore);

        if (centerScore > bestScore)
        {
            bestZone = FinalGestureZone.Center;
            bestScore = centerScore;
            secondScore = Mathf.Max(leftScore, rightScore);
        }

        if (rightScore > bestScore)
        {
            bestZone = FinalGestureZone.Right;
            bestScore = rightScore;
            secondScore = Mathf.Max(leftScore, centerScore);
        }

        if (bestScore < activationThreshold || bestScore - secondScore < confidenceGap)
        {
            return FinalGestureZone.None;
        }

        return bestZone;
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

        if (WebCamTexture.devices.Length == 0)
        {
            statusText.text = "Вебкамера не найдена. Можно использовать клавиатуру.";
            return;
        }

        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            statusText.text = "Вебкамера запускается...";
            return;
        }

        if (!IsReady)
        {
            statusText.text = "Вебкамера запускается, ждём кадр...";
            return;
        }

        if (capturingBackground)
        {
            statusText.text = "Снимок фона: уберите руки из кадра на секунду";
            return;
        }

        var zone = activeZone == FinalGestureZone.Left ? "левая зона" :
            activeZone == FinalGestureZone.Center ? "центр" :
            activeZone == FinalGestureZone.Right ? "правая зона" : "рука не найдена";

        statusText.text = "Чёрно-белый AR режим: " + zone + ". B фон, F1/F2/F3 образцы.";
    }
}
