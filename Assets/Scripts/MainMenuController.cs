// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";

    private Sprite whiteSprite;
    private readonly Color menuBackgroundColor = new Color(0.72f, 0.82f, 0.92f, 1f);
    private readonly Color menuPanelColor = new Color(0.94f, 0.97f, 1f, 0.96f);
    private readonly Color menuTextColor = new Color(0.07f, 0.1f, 0.15f, 1f);
    private readonly Color menuMutedTextColor = new Color(0.2f, 0.28f, 0.36f, 1f);
    private readonly Color buttonColor = new Color(0.1f, 0.42f, 0.88f, 1f);

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        whiteSprite = CreateWhiteSprite();
        PrepareCamera();
        CreateEventSystemIfNeeded();

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            CreateMenu();
            return;
        }

        StyleExistingMenu();
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void CreateMenu()
    {
        var canvasObject = new GameObject("Main Menu Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var background = CreateImage("Menu Background", canvasObject.transform, menuBackgroundColor);
        var backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var title = CreateText("Menu Title", background.transform, "Домашние задания AR", 48, TextAnchor.MiddleCenter, menuTextColor);
        title.fontStyle = FontStyle.Bold;
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(700f, 90f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -210f);

        CreateButton("Start Game Button", background.transform, "Начать", new Vector2(0f, -360f), StartGame);
        CreateButton("Quit Game Button", background.transform, "Выход", new Vector2(0f, -430f), QuitGame);
    }

    private void CreateButton(string objectName, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var buttonImage = CreateImage(objectName, parent, buttonColor);
        var rect = buttonImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(280f, 56f);
        rect.anchoredPosition = position;

        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        var text = CreateText(objectName + " Text", buttonImage.transform, label, 24, TextAnchor.MiddleCenter, Color.white);
        text.fontStyle = FontStyle.Bold;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
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

        return label;
    }

    private void StyleExistingMenu()
    {
        SetImageColor("Menu Background", menuBackgroundColor);
        SetImageColor("Menu Panel", menuPanelColor);
        SetImageColor("Start Game Button", buttonColor);
        SetImageColor("Quit Game Button", buttonColor);

        SetTextColor("Menu Title", menuTextColor);
        SetTextColor("Menu Subtitle", menuMutedTextColor);
        SetTextColor("Start Game Button Text", Color.white);
        SetTextColor("Quit Game Button Text", Color.white);
    }

    private void PrepareCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            camera = FindObjectOfType<Camera>();
        }

        if (camera == null)
        {
            return;
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = menuBackgroundColor;
    }

    private void SetImageColor(string objectName, Color color)
    {
        var target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        var image = target.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    private void SetTextColor(string objectName, Color color)
    {
        var target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        var text = target.GetComponent<Text>();
        if (text != null)
        {
            text.color = color;
        }
    }

    private Sprite CreateWhiteSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
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
