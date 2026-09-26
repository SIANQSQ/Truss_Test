using UnityEngine;
using UnityEngine.UI;

public class EscapeMenu : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField]
    private KeyCode toggleKey = KeyCode.Escape;

    [SerializeField]
    private bool pauseGameWhenOpen = true;

    private Canvas canvas;
    private GameObject menuPanel;
    private bool menuVisible;

    private void Start()
    {
        CreateMenu();
        CloseMenu();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }
    }

    private void CreateMenu()
    {
        CreateEventSystemIfNeeded();

        GameObject canvasObject =
            new GameObject("EscapeMenuCanvas");

        canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        menuPanel =
            CreateImage(
                "MenuPanel",
                canvas.transform,
                new Color(0f, 0f, 0f, 0.72f));

        SetFullScreen(
            menuPanel.GetComponent<RectTransform>());

        GameObject box =
            CreateImage(
                "MenuBox",
                menuPanel.transform,
                new Color(0.08f, 0.10f, 0.13f, 0.98f));

        RectTransform boxRect =
            box.GetComponent<RectTransform>();

        boxRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        boxRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        boxRect.pivot =
            new Vector2(0.5f, 0.5f);

        boxRect.sizeDelta =
            new Vector2(460f, 300f);

        boxRect.anchoredPosition =
            Vector2.zero;

        CreateText(
            "Title",
            box.transform,
            "桁架数字孪生",
            30,
            FontStyle.Bold,
            new Vector2(0f, 90f),
            new Vector2(400f, 50f));

        CreateText(
            "Subtitle",
            box.transform,
            "程序菜单",
            18,
            FontStyle.Normal,
            new Vector2(0f, 48f),
            new Vector2(400f, 35f));

        CreateButton(
            "ResumeButton",
            box.transform,
            "继续运行",
            new Vector2(0f, -20f),
            ResumeGame);

        CreateButton(
            "ExitButton",
            box.transform,
            "退出软件",
            new Vector2(0f, -95f),
            ExitApplication);

        CreateText(
            "Hint",
            box.transform,
            "按 Esc 关闭菜单",
            14,
            FontStyle.Normal,
            new Vector2(0f, -135f),
            new Vector2(400f, 30f));
    }

    private void ToggleMenu()
    {
        if (menuVisible)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    private void OpenMenu()
    {
        menuVisible = true;

        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
        }
    }

    private void CloseMenu()
    {
        menuVisible = false;

        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    private void ResumeGame()
    {
        CloseMenu();
    }

    private void ExitApplication()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private GameObject CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject imageObject =
            new GameObject(objectName);

        imageObject.transform.SetParent(
            parent,
            false);

        Image image =
            imageObject.AddComponent<Image>();

        image.color = color;

        return imageObject;
    }

    private void CreateText(
        string objectName,
        Transform parent,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Vector2 position,
        Vector2 size)
    {
        GameObject textObject =
            new GameObject(objectName);

        textObject.transform.SetParent(
            parent,
            false);

        Text text =
            textObject.AddComponent<Text>();

        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow =
            HorizontalWrapMode.Wrap;
        text.verticalOverflow =
            VerticalWrapMode.Overflow;

        Font defaultFont =
            Resources.GetBuiltinResource<Font>(
                "Arial.ttf");

        if (defaultFont != null)
        {
            text.font = defaultFont;
        }

        RectTransform rect =
            textObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject =
            CreateImage(
                objectName,
                parent,
                new Color(0.12f, 0.42f, 0.75f, 1f));

        Button button =
            buttonObject.AddComponent<Button>();

        button.onClick.AddListener(action);

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            new Color(0.12f, 0.42f, 0.75f, 1f);

        colors.highlightedColor =
            new Color(0.20f, 0.58f, 0.95f, 1f);

        colors.pressedColor =
            new Color(0.08f, 0.28f, 0.55f, 1f);

        colors.selectedColor =
            colors.highlightedColor;

        button.colors = colors;

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta =
            new Vector2(300f, 54f);

        rect.anchoredPosition =
            position;

        CreateText(
            objectName + "_Label",
            buttonObject.transform,
            label,
            20,
            FontStyle.Bold,
            Vector2.zero,
            new Vector2(280f, 45f));
    }

    private void SetFullScreen(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.localScale =
            Vector3.one;
    }

    private void CreateEventSystemIfNeeded()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>()
            != null)
        {
            return;
        }

        GameObject eventSystemObject =
            new GameObject("EventSystem");

        eventSystemObject.AddComponent<
            UnityEngine.EventSystems.EventSystem>();

        eventSystemObject.AddComponent<
            UnityEngine.EventSystems.StandaloneInputModule>();
    }
}