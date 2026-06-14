// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameplayHudPauseController : MonoBehaviour
{
    [SerializeField] private string playerObjectName = "Robot Player";
    [SerializeField] private string menuSceneName = "MainMenuScene";

    private PlayerHealth playerHealth;
    private Image healthFill;
    private Text healthText;
    private GameObject pausePanel;
    private bool isPaused;
    private Sprite whiteSprite;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        playerHealth = FindPlayerHealth();
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += UpdateHealthView;
        }

        CreateEventSystemIfNeeded();
        CreateInterface();
        UpdateHealthView();
        SetPaused(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetPaused(!isPaused);
        }

        UpdateHealthView();
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealthView;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(menuSceneName);
    }

    private PlayerHealth FindPlayerHealth()
    {
        var playerObject = GameObject.Find(playerObjectName);
        if (playerObject == null)
        {
            var robotPlayer = FindObjectOfType<RobotCoinPlayerController>();
            playerObject = robotPlayer == null ? null : robotPlayer.gameObject;
        }

        if (playerObject == null)
        {
            return null;
        }

        var health = playerObject.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = playerObject.AddComponent<PlayerHealth>();
        }

        return health;
    }

    private void CreateInterface()
    {
        whiteSprite = CreateWhiteSprite();

        var canvasObject = new GameObject("Homework 10 HUD Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        CreateHealthHud(canvasObject.transform);
        CreatePauseMenu(canvasObject.transform);
    }

    private void CreateHealthHud(Transform parent)
    {
        var panel = CreateRect("Health HUD", parent, new Vector2(24f, -24f), new Vector2(320f, 76f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        var title = CreateText("Health Title", panel, "Здоровье", 22, TextAnchor.UpperLeft, Color.white);
        SetStretch(title.rectTransform, new Vector2(0f, 38f), new Vector2(0f, 0f));

        var barBack = CreateImage("Health Bar Background", panel, new Color(0.08f, 0.09f, 0.12f, 0.9f));
        SetStretch(barBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 34f));

        healthFill = CreateImage("Health Bar Fill", barBack.transform, new Color(0.18f, 0.85f, 0.42f, 1f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        SetStretch(healthFill.rectTransform, new Vector2(4f, 4f), new Vector2(4f, 4f));

        healthText = CreateText("Health Value", barBack.transform, "100 / 100", 20, TextAnchor.MiddleCenter, Color.white);
        SetStretch(healthText.rectTransform, Vector2.zero, Vector2.zero);
    }

    private void CreatePauseMenu(Transform parent)
    {
        pausePanel = new GameObject("Pause Menu");
        pausePanel.transform.SetParent(parent, false);

        var panelRect = pausePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var overlay = pausePanel.AddComponent<Image>();
        overlay.sprite = whiteSprite;
        overlay.color = new Color(0f, 0f, 0f, 0.62f);

        var box = CreateImage("Pause Menu Box", pausePanel.transform, new Color(0.09f, 0.11f, 0.15f, 0.96f));
        var boxRect = box.rectTransform;
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(420f, 280f);
        boxRect.anchoredPosition = Vector2.zero;

        var title = CreateText("Pause Title", box.transform, "Пауза", 38, TextAnchor.MiddleCenter, Color.white);
        title.fontStyle = FontStyle.Bold;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 72f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -28f);

        CreateButton("Resume Button", box.transform, "Продолжить", new Vector2(0f, -62f), ResumeGame);
        CreateButton("Exit To Menu Button", box.transform, "Выйти в меню", new Vector2(0f, -136f), ExitToMenu);
    }

    private void SetPaused(bool value)
    {
        isPaused = value;
        Time.timeScale = isPaused ? 0f : 1f;
        AudioListener.pause = isPaused;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }

        Cursor.visible = isPaused;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UpdateHealthView(int currentHealth, int maxHealth)
    {
        if (healthFill != null)
        {
            healthFill.fillAmount = maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth;
        }

        if (healthText != null)
        {
            healthText.text = currentHealth + " / " + maxHealth;
        }
    }

    private void UpdateHealthView()
    {
        if (playerHealth == null)
        {
            UpdateHealthView(0, 100);
            return;
        }

        UpdateHealthView(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var buttonImage = CreateImage(objectName, parent, new Color(0.16f, 0.48f, 0.92f, 1f));
        var rect = buttonImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(280f, 54f);
        rect.anchoredPosition = position;

        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.48f, 0.92f, 1f);
        colors.highlightedColor = new Color(0.22f, 0.58f, 1f, 1f);
        colors.pressedColor = new Color(0.1f, 0.34f, 0.7f, 1f);
        button.colors = colors;

        var text = CreateText(objectName + " Text", buttonImage.transform, label, 24, TextAnchor.MiddleCenter, Color.white);
        text.fontStyle = FontStyle.Bold;
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);

        return button;
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

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        var rectObject = new GameObject(objectName);
        rectObject.transform.SetParent(parent, false);

        var rect = rectObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return rect;
    }

    private void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
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
