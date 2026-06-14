// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScoreHudController : MonoBehaviour
{
    private const string HighScoreKey = "HomeworkCoinHighScore";

    [SerializeField] private int startingScore = 0;
    [SerializeField] private string menuSceneName = "MainMenuScene";

    private Text scoreText;
    private Text bestScoreText;
    private Text gameOverScoreText;
    private Text gameOverBestText;
    private GameObject gameOverPanel;
    private Sprite whiteSprite;
    private int currentScore;
    private int bestScore;
    private bool gameOver;

    public static ScoreHudController Instance { get; private set; }

    public int CurrentScore => currentScore;
    public int BestScore => bestScore;
    public bool IsGameOver => gameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        currentScore = Mathf.Max(0, startingScore);
        bestScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        CreateEventSystemIfNeeded();
        CreateInterface();
        UpdateScoreView();
        SetGameOverPanel(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        Time.timeScale = 1f;
    }

    public void AddScore(int amount)
    {
        if (gameOver || amount <= 0)
        {
            return;
        }

        currentScore += amount;
        SaveBestScoreIfNeeded();
        UpdateScoreView();
    }

    public void ResetCurrentScore()
    {
        currentScore = 0;
        UpdateScoreView();
    }

    public void EndGame()
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        SaveBestScoreIfNeeded();
        UpdateScoreView();
        UpdateGameOverView();
        SetGameOverPanel(true);

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    private void SaveBestScoreIfNeeded()
    {
        if (currentScore <= bestScore)
        {
            return;
        }

        bestScore = currentScore;
        PlayerPrefs.SetInt(HighScoreKey, bestScore);
        PlayerPrefs.Save();
    }

    private void CreateInterface()
    {
        whiteSprite = CreateWhiteSprite();

        var canvasObject = new GameObject("Homework 12 Score Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        CreateScoreHud(canvasObject.transform);
        CreateGameOverPanel(canvasObject.transform);
    }

    private void CreateScoreHud(Transform parent)
    {
        var panel = CreateRect("Score HUD", parent, new Vector2(-30f, -30f), new Vector2(330f, 96f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = whiteSprite;
        panelImage.color = new Color(0.04f, 0.06f, 0.08f, 0.82f);

        var title = CreateText("Score Title", panel, "Очки", 22, TextAnchor.MiddleLeft, Color.white);
        title.fontStyle = FontStyle.Bold;
        SetStretch(title.rectTransform, new Vector2(18f, 50f), new Vector2(18f, 8f));

        scoreText = CreateText("Score Value", panel, "0", 30, TextAnchor.MiddleRight, Color.white);
        scoreText.fontStyle = FontStyle.Bold;
        SetStretch(scoreText.rectTransform, new Vector2(18f, 36f), new Vector2(18f, 10f));

        bestScoreText = CreateText("Best Score Value", panel, "Рекорд: 0", 20, TextAnchor.MiddleLeft, new Color(0.82f, 0.9f, 1f, 1f));
        SetStretch(bestScoreText.rectTransform, new Vector2(18f, 10f), new Vector2(18f, 52f));
    }

    private void CreateGameOverPanel(Transform parent)
    {
        gameOverPanel = new GameObject("Game Over Panel");
        gameOverPanel.transform.SetParent(parent, false);

        var panelRect = gameOverPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var overlay = gameOverPanel.AddComponent<Image>();
        overlay.sprite = whiteSprite;
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        var box = CreateImage("Game Over Box", gameOverPanel.transform, new Color(0.08f, 0.1f, 0.14f, 0.97f));
        var boxRect = box.rectTransform;
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(520f, 360f);
        boxRect.anchoredPosition = Vector2.zero;

        var title = CreateText("Game Over Title", box.transform, "Игра окончена", 38, TextAnchor.MiddleCenter, Color.white);
        title.fontStyle = FontStyle.Bold;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 62f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -28f);

        gameOverScoreText = CreateText("Game Over Score", box.transform, "Счёт: 0", 26, TextAnchor.MiddleCenter, Color.white);
        gameOverScoreText.rectTransform.anchorMin = new Vector2(0f, 1f);
        gameOverScoreText.rectTransform.anchorMax = new Vector2(1f, 1f);
        gameOverScoreText.rectTransform.pivot = new Vector2(0.5f, 1f);
        gameOverScoreText.rectTransform.sizeDelta = new Vector2(0f, 42f);
        gameOverScoreText.rectTransform.anchoredPosition = new Vector2(0f, -110f);

        gameOverBestText = CreateText("Game Over Best Score", box.transform, "Рекорд: 0", 24, TextAnchor.MiddleCenter, new Color(0.82f, 0.9f, 1f, 1f));
        gameOverBestText.rectTransform.anchorMin = new Vector2(0f, 1f);
        gameOverBestText.rectTransform.anchorMax = new Vector2(1f, 1f);
        gameOverBestText.rectTransform.pivot = new Vector2(0.5f, 1f);
        gameOverBestText.rectTransform.sizeDelta = new Vector2(0f, 40f);
        gameOverBestText.rectTransform.anchoredPosition = new Vector2(0f, -154f);

        CreateButton("Restart Button", box.transform, "Заново", new Vector2(0f, -220f), RestartGame);
        CreateButton("Game Over Menu Button", box.transform, "В меню", new Vector2(0f, -292f), ExitToMenu);
    }

    private void UpdateScoreView()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }

        if (bestScoreText != null)
        {
            bestScoreText.text = "Рекорд: " + bestScore;
        }
    }

    private void UpdateGameOverView()
    {
        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = "Счёт: " + currentScore;
        }

        if (gameOverBestText != null)
        {
            gameOverBestText.text = "Рекорд: " + bestScore;
        }
    }

    private void SetGameOverPanel(bool active)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(active);
        }
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
