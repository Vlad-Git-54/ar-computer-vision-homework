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
    [SerializeField] private float sampleInterval = 0.08f;
    [SerializeField] private float activationThreshold = 0.035f;
    [SerializeField] private float confidenceGap = 0.012f;
    [SerializeField] private int pixelStep = 7;
    [SerializeField] private int motionThreshold = 38;

    private WebCamTexture webcamTexture;
    private Color32[] currentPixels;
    private Color32[] previousPixels;
    private int cachedWidth;
    private int cachedHeight;
    private float nextSampleTime;
    private float leftScore;
    private float centerScore;
    private float rightScore;
    private FinalGestureZone activeZone;
    private FinalGestureZone heldZone;
    private float heldTime;
    private float lastConsumeTime;

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

    private void Start()
    {
        StartWebcam();
    }

    private void Update()
    {
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

    private void StartWebcam()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            UpdateStatusText();
            return;
        }

        webcamTexture = new WebCamTexture(null, 960, 540, 30);
        webcamTexture.Play();
        ApplyPreviewTexture();
    }

    private void ApplyPreviewTexture()
    {
        if (previewImage == null || webcamTexture == null)
        {
            return;
        }

        previewImage.texture = webcamTexture;
        previewImage.uvRect = mirrorPreview ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
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

        if (cachedWidth != webcamTexture.width || cachedHeight != webcamTexture.height || currentPixels == null)
        {
            cachedWidth = webcamTexture.width;
            cachedHeight = webcamTexture.height;
            currentPixels = new Color32[cachedWidth * cachedHeight];
            previousPixels = null;
        }

        webcamTexture.GetPixels32(currentPixels);

        var newLeftScore = CalculateZoneScore(0f, 0.34f);
        var newCenterScore = CalculateZoneScore(0.33f, 0.67f);
        var newRightScore = CalculateZoneScore(0.66f, 1f);

        leftScore = Mathf.Lerp(leftScore, newLeftScore, 0.55f);
        centerScore = Mathf.Lerp(centerScore, newCenterScore, 0.55f);
        rightScore = Mathf.Lerp(rightScore, newRightScore, 0.55f);
        activeZone = ChooseActiveZone();

        if (previousPixels == null || previousPixels.Length != currentPixels.Length)
        {
            previousPixels = new Color32[currentPixels.Length];
        }

        var temp = previousPixels;
        previousPixels = currentPixels;
        currentPixels = temp;
    }

    private float CalculateZoneScore(float minNormalizedX, float maxNormalizedX)
    {
        if (previousPixels == null)
        {
            return 0f;
        }

        var minX = Mathf.Clamp(Mathf.FloorToInt(cachedWidth * minNormalizedX), 0, cachedWidth - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(cachedWidth * maxNormalizedX), minX + 1, cachedWidth);
        var minY = Mathf.Clamp(Mathf.FloorToInt(cachedHeight * 0.42f), 0, cachedHeight - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(cachedHeight * 0.96f), minY + 1, cachedHeight);

        var total = 0;
        var motionPixels = 0;
        var skinPixels = 0;
        var skinMotionPixels = 0;

        for (var y = minY; y < maxY; y += pixelStep)
        {
            for (var x = minX; x < maxX; x += pixelStep)
            {
                var sourceX = mirrorPreview ? cachedWidth - 1 - x : x;
                var index = y * cachedWidth + sourceX;
                var current = currentPixels[index];
                var previous = previousPixels[index];
                var difference = Mathf.Abs(current.r - previous.r) + Mathf.Abs(current.g - previous.g) + Mathf.Abs(current.b - previous.b);
                var isMoving = difference >= motionThreshold;
                var looksLikeSkin = LooksLikeSkin(current);

                total++;
                if (isMoving)
                {
                    motionPixels++;
                }

                if (looksLikeSkin)
                {
                    skinPixels++;
                }

                if (isMoving && looksLikeSkin)
                {
                    skinMotionPixels++;
                }
            }
        }

        if (total <= 0)
        {
            return 0f;
        }

        var motionRatio = (float)motionPixels / total;
        var skinRatio = (float)skinPixels / total;
        var skinMotionRatio = (float)skinMotionPixels / total;
        return motionRatio + skinMotionRatio * 1.35f + skinRatio * 0.12f;
    }

    private bool LooksLikeSkin(Color32 color)
    {
        var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        var min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));

        return color.r > 92 &&
            color.g > 38 &&
            color.b > 18 &&
            color.r > color.g * 1.08f &&
            color.r > color.b * 1.22f &&
            max - min > 18;
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

        if (webcamTexture == null)
        {
            statusText.text = "Вебкамера не найдена. Можно использовать клавиатуру.";
            return;
        }

        if (!IsReady)
        {
            statusText.text = "Вебкамера запускается...";
            return;
        }

        statusText.text = "AR выбор активен: поднимите руку в нужной зоне";
    }
}
