// Автор: Марьяновский Владислав Андреевич

using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class FinalMenuEditorPreview : MonoBehaviour
{
    [SerializeField] private bool showPreviewInEditMode = true;

    private const string PreviewRootName = "Final Menu Editor Preview";
    private bool previewChecked;
    private Sprite whiteSprite;

    private void OnEnable()
    {
        previewChecked = false;
        if (Application.isPlaying)
        {
            SetPreviewActive(false);
        }
    }

    private void Update()
    {
        if (Application.isPlaying || previewChecked)
        {
            return;
        }

        previewChecked = true;
        EnsurePreview();
    }

    [ContextMenu("Rebuild Final Menu Preview")]
    private void RebuildPreview()
    {
        var existing = transform.Find(PreviewRootName);
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        previewChecked = false;
        EnsurePreview();
    }

    private void EnsurePreview()
    {
        if (!showPreviewInEditMode)
        {
            SetPreviewActive(false);
            return;
        }

        var existing = transform.Find(PreviewRootName);
        if (existing != null && existing.childCount > 0)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);

        var root = new GameObject(PreviewRootName);
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        var background = CreateImage("Preview Background", root.transform, new Color(0.02f, 0.04f, 0.07f, 1f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

        var title = CreateText("Preview Title", root.transform, "AR Coin Arena", 68, TextAnchor.MiddleCenter, Color.white);
        title.fontStyle = FontStyle.Bold;
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(900f, 90f), new Vector2(0.5f, 1f));

        var subtitle = CreateText("Preview Subtitle", root.transform, "Выберите робота через вебкамеру и запустите игру", 28, TextAnchor.MiddleCenter, new Color(0.86f, 0.94f, 1f, 1f));
        SetAnchored(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(920f, 44f), new Vector2(0.5f, 1f));

        CreatePanel(root.transform, "Синий робот", "Левая рука", new Vector2(0.18f, 0.48f), new Color(0.05f, 0.28f, 0.74f, 0.88f));
        CreatePanel(root.transform, "Play", "Рука в центре", new Vector2(0.5f, 0.47f), new Color(0.12f, 0.55f, 0.28f, 0.9f));
        CreatePanel(root.transform, "Красный робот", "Правая рука", new Vector2(0.82f, 0.48f), new Color(0.72f, 0.12f, 0.1f, 0.88f));

        var hint = CreateText("Preview Hint", root.transform, "Редакторский предпросмотр. Нажмите Play для запуска вебкамеры.", 22, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f, 1f));
        SetAnchored(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 42f), new Vector2(0.5f, 0f));
    }

    private void SetPreviewActive(bool active)
    {
        var existing = transform.Find(PreviewRootName);
        if (existing != null)
        {
            existing.gameObject.SetActive(active);
        }
    }

    private void CreatePanel(Transform parent, string title, string hint, Vector2 anchor, Color color)
    {
        var panel = CreateImage(title + " Preview Panel", parent, color);
        SetAnchored(panel.rectTransform, anchor, Vector2.zero, new Vector2(380f, 240f), new Vector2(0.5f, 0.5f));

        var titleText = CreateText(title + " Preview Title", panel.transform, title, title == "Play" ? 48 : 34, TextAnchor.MiddleCenter, Color.white);
        titleText.fontStyle = FontStyle.Bold;
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(330f, 72f), new Vector2(0.5f, 0.5f));

        var hintText = CreateText(title + " Preview Hint", panel.transform, hint, 22, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 1f, 1f));
        SetAnchored(hintText.rectTransform, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(330f, 44f), new Vector2(0.5f, 0.5f));

        var bar = CreateImage(title + " Preview Progress", panel.transform, new Color(0f, 0f, 0f, 0.28f));
        SetAnchored(bar.rectTransform, new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(300f, 16f), new Vector2(0.5f, 0.5f));
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
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    private void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
