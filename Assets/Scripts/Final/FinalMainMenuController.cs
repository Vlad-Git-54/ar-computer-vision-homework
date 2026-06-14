// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FinalMainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "FinalGameScene";
    [SerializeField] private float gestureHoldTime = 0.85f;

    private const string PlayerColorKey = "FinalProjectPlayerColor";

    private FinalWebcamGestureInput gestureInput;
    private Image bluePanel;
    private Image redPanel;
    private Image blueProgress;
    private Image redProgress;
    private Image playProgress;
    private Text selectedText;
    private int selectedColor;
    private Sprite whiteSprite;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        selectedColor = PlayerPrefs.GetInt(PlayerColorKey, 0);

        CreateCameraIfNeeded();
        CreateEventSystemIfNeeded();
        CreateInterface();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectColor(0);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectColor(1);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            Play();
        }

        if (gestureInput == null)
        {
            return;
        }

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Left, gestureHoldTime))
        {
            SelectColor(0);
        }

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Right, gestureHoldTime))
        {
            SelectColor(1);
        }

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Center, gestureHoldTime))
        {
            Play();
        }

        UpdateGestureProgress();
    }

    public void SelectBlue()
    {
        SelectColor(0);
    }

    public void SelectRed()
    {
        SelectColor(1);
    }

    public void Play()
    {
        PlayerPrefs.SetInt(PlayerColorKey, selectedColor);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    private void SelectColor(int value)
    {
        selectedColor = Mathf.Clamp(value, 0, 1);
        PlayerPrefs.SetInt(PlayerColorKey, selectedColor);
        PlayerPrefs.Save();
        UpdateSelectedView();
    }

    private void CreateInterface()
    {
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);

        var canvasObject = new GameObject("Final AR Main Menu Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var preview = CreateRawImage("Webcam Preview", canvasObject.transform);
        Stretch(preview.rectTransform, Vector2.zero, Vector2.zero);

        var dark = CreateImage("Dark Overlay", canvasObject.transform, new Color(0f, 0f, 0f, 0.48f));
        Stretch(dark.rectTransform, Vector2.zero, Vector2.zero);

        var title = CreateText("Title", canvasObject.transform, "AR Coin Arena", 68, TextAnchor.MiddleCenter, Color.white);
        title.fontStyle = FontStyle.Bold;
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(900f, 90f), new Vector2(0.5f, 1f));

        var subtitle = CreateText("Subtitle", canvasObject.transform, "Выберите робота через вебкамеру и запустите игру", 28, TextAnchor.MiddleCenter, new Color(0.86f, 0.94f, 1f, 1f));
        SetAnchored(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(920f, 44f), new Vector2(0.5f, 1f));

        bluePanel = CreateChoicePanel(canvasObject.transform, "Синий робот", "Левая рука", new Vector2(0.18f, 0.48f), new Color(0.05f, 0.28f, 0.74f, 0.88f), SelectBlue, out blueProgress);
        CreateChoicePanel(canvasObject.transform, "Play", "Рука в центре", new Vector2(0.5f, 0.47f), new Color(0.12f, 0.55f, 0.28f, 0.9f), Play, out playProgress);
        redPanel = CreateChoicePanel(canvasObject.transform, "Красный робот", "Правая рука", new Vector2(0.82f, 0.48f), new Color(0.72f, 0.12f, 0.1f, 0.88f), SelectRed, out redProgress);

        selectedText = CreateText("Selected Text", canvasObject.transform, "", 28, TextAnchor.MiddleCenter, new Color(0.8f, 1f, 0.86f, 1f));
        selectedText.fontStyle = FontStyle.Bold;
        SetAnchored(selectedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(740f, 52f), new Vector2(0.5f, 0f));

        var hint = CreateText("Hint", canvasObject.transform, "Клавиатура для проверки: 1 синий, 2 красный, Enter Play", 20, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f, 1f));
        SetAnchored(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(820f, 36f), new Vector2(0.5f, 0f));

        var status = CreateText("Webcam Status", canvasObject.transform, "", 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 1f));
        SetAnchored(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(920f, 32f), new Vector2(0.5f, 0f));

        gestureInput = gameObject.AddComponent<FinalWebcamGestureInput>();
        gestureInput.SetPreview(preview);
        gestureInput.SetStatus(status);

        UpdateSelectedView();
        UpdateGestureProgress();
    }

    private Image CreateChoicePanel(Transform parent, string title, string hint, Vector2 anchor, Color color, UnityEngine.Events.UnityAction clickAction, out Image progress)
    {
        var panel = CreateImage(title + " Panel", parent, color);
        var rect = panel.rectTransform;
        SetAnchored(rect, anchor, Vector2.zero, new Vector2(380f, 240f), new Vector2(0.5f, 0.5f));

        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        button.onClick.AddListener(clickAction);

        var titleText = CreateText(title + " Title", panel.transform, title, title == "Play" ? 48 : 34, TextAnchor.MiddleCenter, Color.white);
        titleText.fontStyle = FontStyle.Bold;
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(330f, 72f), new Vector2(0.5f, 0.5f));

        var hintText = CreateText(title + " Hint", panel.transform, hint, 22, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 1f, 1f));
        SetAnchored(hintText.rectTransform, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(330f, 44f), new Vector2(0.5f, 0.5f));

        var progressBack = CreateImage(title + " Progress Back", panel.transform, new Color(0f, 0f, 0f, 0.28f));
        SetAnchored(progressBack.rectTransform, new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(300f, 16f), new Vector2(0.5f, 0.5f));

        progress = CreateImage(title + " Progress", progressBack.transform, Color.white);
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        Stretch(progress.rectTransform, Vector2.zero, Vector2.zero);

        return panel;
    }

    private void UpdateSelectedView()
    {
        if (selectedText != null)
        {
            selectedText.text = selectedColor == 0 ? "Выбран синий робот" : "Выбран красный робот";
        }

        if (bluePanel != null)
        {
            bluePanel.color = selectedColor == 0 ? new Color(0.08f, 0.42f, 1f, 0.95f) : new Color(0.05f, 0.18f, 0.48f, 0.78f);
        }

        if (redPanel != null)
        {
            redPanel.color = selectedColor == 1 ? new Color(1f, 0.22f, 0.16f, 0.95f) : new Color(0.44f, 0.07f, 0.06f, 0.78f);
        }
    }

    private void UpdateGestureProgress()
    {
        SetProgress(blueProgress, gestureInput == null ? 0f : gestureInput.GetHoldProgress(FinalGestureZone.Left, gestureHoldTime), new Color(0.5f, 0.78f, 1f, 1f));
        SetProgress(redProgress, gestureInput == null ? 0f : gestureInput.GetHoldProgress(FinalGestureZone.Right, gestureHoldTime), new Color(1f, 0.64f, 0.58f, 1f));
        SetProgress(playProgress, gestureInput == null ? 0f : gestureInput.GetHoldProgress(FinalGestureZone.Center, gestureHoldTime), new Color(0.62f, 1f, 0.68f, 1f));
    }

    private void SetProgress(Image image, float value, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.fillAmount = value;
        image.color = color;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        return image;
    }

    private RawImage CreateRawImage(string objectName, Transform parent)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<RawImage>();
        image.color = Color.white;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        var label = textObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = -max;
    }

    private void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void CreateCameraIfNeeded()
    {
        if (Camera.main != null)
        {
            return;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.AddComponent<AudioListener>();
    }

    private void CreateEventSystemIfNeeded()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
