using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Runtime presentation layer for the truss digital-twin scene.
/// It owns the HUD and the Escape menu so the scene does not need a large
/// serialized UI hierarchy. All values are refreshed from the live model.
/// </summary>
public class EscapeMenu : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField]
    private KeyCode toggleKey = KeyCode.Escape;

    [SerializeField]
    private bool pauseGameWhenOpen = true;

    [Header("Startup")]
    [SerializeField]
    private bool showStartupAnimation = true;

    [SerializeField]
    private float startupAnimationDuration = 2.8f;

    [SerializeField]
    private Sprite startupLogo;

    [SerializeField]
    private string startupTitle = "桁架数字孪生系统";

    [SerializeField]
    private string startupSubtitle =
        "TRUSS DIGITAL TWIN  /  STRUCTURAL RESPONSE WORKSPACE";

    [Header("Presentation")]
    [SerializeField]
    private bool showHudOnStart = true;

    [SerializeField]
    private bool darkenWorldBackground = true;

    [SerializeField]
    private Color worldBackground = new Color(0.024f, 0.075f, 0.129f, 1f);

    private readonly Color ink = new Color(0.918f, 0.949f, 0.984f, 1f);
    private readonly Color mutedInk = new Color(0.569f, 0.651f, 0.741f, 1f);
    private readonly Color panelColor = new Color(0.047f, 0.125f, 0.208f, 0.985f);
    private readonly Color accent = new Color(0.220f, 0.741f, 0.973f, 1f);
    private readonly Color accentBlue = new Color(0.184f, 0.502f, 0.929f, 1f);
    private readonly Color neutral = new Color(0.82f, 0.89f, 0.97f, 1f);
    private readonly Color surfaceRaised = new Color(0.063f, 0.165f, 0.271f, 1f);
    private readonly Color surfaceControl = new Color(0.051f, 0.149f, 0.247f, 1f);
    private readonly Color toolbarColor = new Color(0.035f, 0.114f, 0.192f, 0.99f);
    private readonly Color borderBlue = new Color(0.149f, 0.341f, 0.490f, 0.72f);
    private readonly Color successColor = new Color(0.208f, 0.773f, 0.545f, 1f);
    private readonly Color dangerColor = new Color(0.851f, 0.294f, 0.357f, 1f);

    private Canvas canvas;
    private GameObject hudRoot;
    private GameObject menuPanel;
    private bool menuVisible;
    private bool hudVisible;
    private float lastStatusRefresh = -1f;
    private float lastVisualCache = -1f;
    private FourPointBeamElementVisual[] stressElements;
    private FourPointJointVisual[] stressJoints;
    private GameObject stageRoot;
    private Mesh stageFloorMesh;
    private Mesh stageGridMesh;
    private Material stageFloorMaterial;
    private Material stageGridMaterial;
    private GameObject topBarPanel;
    private GameObject analysisPanel;
    private GameObject legendPanel;
    private GameObject interactionPanel;
    private GameObject bottomBarPanel;
    private GameObject viewportHeaderRoot;
    private GameObject panelToggleDock;
    private GameObject startupOverlay;
    private CanvasGroup startupCanvasGroup;
    private RectTransform startupLogoRect;
    private Image startupProgressFill;
    private Text startupStatusText;
    private bool startupAnimationPlaying;
    private Texture2D roundedPanelTexture;
    private Texture2D roundedControlTexture;
    private Texture2D roundedPillTexture;
    private Texture2D strainGradientTexture;
    private Sprite roundedPanelSprite;
    private Sprite roundedControlSprite;
    private Sprite roundedPillSprite;
    private Sprite strainGradientSprite;
    private Font uiFont;

    private Text connectionText;
    private Text elementText;
    private Text jointText;
    private Text peakStrainText;
    private Text modeText;
    private Text timestampText;
    private Text bottomStatusText;
    private Text menuStatusText;
    private Image connectionDot;
    private Text selectedElementText;
    private Text selectedModeText;
    private Text selectedStrainText;
    private Text interactionStatusText;
    private Text directionButtonText;
    private InputField forceInput;
    private Button directionButton;
    private Button applyForceButton;
    private Button clearBeamButton;
    private Button resetResultsButton;
    private Button demoBendingButton;
    private Button menuResetResultsButton;

    private FourPointStressController stressController;
    private FourPointStressDemo stressDemo;
    private TrussCameraController cameraController;
    private WebSocketFourPointReceiver receiver;
    private TrussDualViewInteraction dualViewInteraction;

    private void Start()
    {
        CacheControllers();
        ConfigureWorldPresentation();
        CreateWorldStage();
        CacheStressVisuals();
        CreateEventSystemIfNeeded();
        CreateDualViewInteraction();
        CreateCanvas();
        CreateRuntimeUiAssets();
        CreateHud();
        CreateMenu();
        CreateStartupOverlay();

        hudVisible = showHudOnStart;
        if (startupOverlay != null)
        {
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
        }
        else
        {
            SetHudVisible(hudVisible);
        }
        CloseMenu();
        UpdateStatus(true);

        if (startupOverlay != null)
        {
            StartCoroutine(PlayStartupAnimation());
        }
    }

    private void Update()
    {
        if (startupAnimationPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            SetHudVisible(!hudVisible);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            FocusModel();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetResults();
        }

        if (Time.unscaledTime - lastVisualCache > 2f)
        {
            CacheStressVisuals();
        }

        if (Time.unscaledTime - lastStatusRefresh > 0.35f)
        {
            UpdateStatus(false);
        }
    }

    private void CacheControllers()
    {
        stressController = FindFirst<FourPointStressController>();
        stressDemo = FindFirst<FourPointStressDemo>();
        cameraController = FindFirst<TrussCameraController>();
        receiver = FindFirst<WebSocketFourPointReceiver>();
    }

    private void CreateDualViewInteraction()
    {
        GameObject originalModel = GameObject.Find("Truss_Root");
        GameObject stressModel = GameObject.Find("VirtualTruss_Root");
        Camera sourceCamera = Camera.main;

        if (originalModel == null || stressModel == null || sourceCamera == null)
        {
            UnityEngine.Debug.LogWarning(
                "Dual view requires Truss_Root, VirtualTruss_Root and Main Camera.");
            return;
        }

        dualViewInteraction = GetComponent<TrussDualViewInteraction>();
        if (dualViewInteraction == null)
        {
            dualViewInteraction =
                gameObject.AddComponent<TrussDualViewInteraction>();
        }

        dualViewInteraction.Initialize(
            sourceCamera,
            originalModel.transform,
            stressModel.transform,
            stressController,
            receiver);
    }

    private T FindFirst<T>() where T : UnityEngine.Object
    {
        T[] matches = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return matches != null && matches.Length > 0 ? matches[0] : null;
    }

    private void ConfigureWorldPresentation()
    {
        if (!darkenWorldBackground)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = worldBackground;
        }

        RenderSettings.ambientIntensity = 0.75f;
        RenderSettings.ambientLight = new Color(0.12f, 0.23f, 0.35f, 1f);
    }

    private void CacheStressVisuals()
    {
        lastVisualCache = Time.unscaledTime;
        stressElements = FindObjectsByType<FourPointBeamElementVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        stressJoints = FindObjectsByType<FourPointJointVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    private void CreateWorldStage()
    {
        GameObject originalModel = GameObject.Find("Truss_Root");
        Renderer[] renderers = originalModel != null
            ? originalModel.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(2f, 1f, 2f));

        foreach (Renderer currentRenderer in renderers)
        {
            if (currentRenderer == null ||
                currentRenderer.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = currentRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(currentRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Transform target = cameraController != null
                ? cameraController.transform
                : transform;
            bounds = new Bounds(target.position, new Vector3(2f, 1f, 2f));
        }

        float halfSize = Mathf.Max(
            Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.35f,
            0.85f);
        float halfX = halfSize;
        float halfZ = halfSize;
        float floorY = bounds.min.y - Mathf.Max(0.004f, bounds.size.y * 0.008f);
        float gridY = floorY + 0.001f;

        stageRoot = new GameObject("Truss_Engineering_Stage");
        stageRoot.transform.position =
            new Vector3(bounds.center.x, floorY, bounds.center.z);

        stageFloorMesh = new Mesh
        {
            name = "TrussStage_FloorMesh"
        };
        stageFloorMesh.vertices = new[]
        {
            new Vector3(-halfX, 0f, -halfZ),
            new Vector3(-halfX, 0f, halfZ),
            new Vector3(halfX, 0f, halfZ),
            new Vector3(halfX, 0f, -halfZ)
        };
        stageFloorMesh.normals = new[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };
        stageFloorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        stageFloorMesh.RecalculateBounds();

        GameObject floor = new GameObject("Stage_Floor");
        floor.transform.SetParent(stageRoot.transform, false);
        floor.AddComponent<MeshFilter>().sharedMesh = stageFloorMesh;
        MeshRenderer floorRenderer = floor.AddComponent<MeshRenderer>();
        stageFloorMaterial = CreateUnlitMaterial(
            "TrussStage_FloorMaterial",
            new Color(0.027f, 0.102f, 0.176f, 1f));
        floorRenderer.sharedMaterial = stageFloorMaterial;
        floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
        floorRenderer.receiveShadows = false;
        floorRenderer.lightProbeUsage = LightProbeUsage.Off;
        floorRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        float spacing = Mathf.Clamp(
            Mathf.Max(bounds.size.x, bounds.size.z) / 12f,
            0.08f,
            0.25f);
        spacing = Mathf.Max(spacing, Mathf.Max(halfX, halfZ) * 2f / 80f);
        // The grid is a child of the stage root, so its Y coordinate is local.
        stageGridMesh = BuildGridMesh(halfX, halfZ, 0.001f, spacing);

        GameObject grid = new GameObject("Stage_Grid");
        grid.transform.SetParent(stageRoot.transform, false);
        grid.AddComponent<MeshFilter>().sharedMesh = stageGridMesh;
        MeshRenderer gridRenderer = grid.AddComponent<MeshRenderer>();
        stageGridMaterial = CreateUnlitMaterial(
            "TrussStage_GridMaterial",
            new Color(0.090f, 0.290f, 0.451f, 1f));
        gridRenderer.sharedMaterial = stageGridMaterial;
        gridRenderer.shadowCastingMode = ShadowCastingMode.Off;
        gridRenderer.receiveShadows = false;
        gridRenderer.lightProbeUsage = LightProbeUsage.Off;
        gridRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private Mesh BuildGridMesh(
        float halfX,
        float halfZ,
        float gridY,
        float spacing)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        float width = halfX * 2f;
        float depth = halfZ * 2f;
        int xSteps = Mathf.Clamp(Mathf.CeilToInt(width / spacing), 4, 80);
        int zSteps = Mathf.Clamp(Mathf.CeilToInt(depth / spacing), 4, 80);

        for (int i = 0; i <= xSteps; i++)
        {
            float x = Mathf.Lerp(-halfX, halfX, i / (float)xSteps);
            AddGridLine(vertices, indices,
                new Vector3(x, gridY, -halfZ),
                new Vector3(x, gridY, halfZ));
        }

        for (int i = 0; i <= zSteps; i++)
        {
            float z = Mathf.Lerp(-halfZ, halfZ, i / (float)zSteps);
            AddGridLine(vertices, indices,
                new Vector3(-halfX, gridY, z),
                new Vector3(halfX, gridY, z));
        }

        Mesh mesh = new Mesh
        {
            name = "TrussStage_GridMesh"
        };
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddGridLine(
        List<Vector3> vertices,
        List<int> indices,
        Vector3 start,
        Vector3 end)
    {
        int index = vertices.Count;
        vertices.Add(start);
        vertices.Add(end);
        indices.Add(index);
        indices.Add(index + 1);
    }

    private Material CreateUnlitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            UnityEngine.Debug.LogWarning(
                "Could not find an unlit shader for the truss stage.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = materialName
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "TrussPresentationCanvas",
            typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void CreateRuntimeUiAssets()
    {
        roundedPanelSprite = CreateRoundedUiSprite(
            "Runtime_UI_Panel",
            64,
            13f,
            16f,
            out roundedPanelTexture);
        roundedControlSprite = CreateRoundedUiSprite(
            "Runtime_UI_Control",
            32,
            7f,
            9f,
            out roundedControlTexture);
        roundedPillSprite = CreateRoundedUiSprite(
            "Runtime_UI_Pill",
            32,
            15f,
            15f,
            out roundedPillTexture);
        CreateStrainGradientAsset();

        try
        {
            uiFont = Font.CreateDynamicFontFromOSFont(
                "Microsoft YaHei UI",
                16);
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont(
                    "Microsoft YaHei",
                    16);
            }
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 16);
            }
        }
        catch
        {
            uiFont = null;
        }
    }

    private Sprite CreateRoundedUiSprite(
        string assetName,
        int textureSize,
        float radius,
        float border,
        out Texture2D texture)
    {
        texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = assetName + "_Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        float half = textureSize * 0.5f;
        Vector2 inner = new Vector2(half - radius, half - radius);
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(
                    Mathf.Abs(x + 0.5f - half),
                    Mathf.Abs(y + 0.5f - half));
                Vector2 q = point - inner;
                Vector2 outside = new Vector2(
                    Mathf.Max(q.x, 0f),
                    Mathf.Max(q.y, 0f));
                float signedDistance = outside.magnitude +
                    Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                byte alphaValue = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(0.5f - signedDistance) * 255f);
                pixels[y * textureSize + x] =
                    new Color32(255, 255, 255, alphaValue);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        sprite.name = assetName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void CreateStrainGradientAsset()
    {
        const int width = 12;
        const int height = 256;
        strainGradientTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Runtime_Strain_Gradient_Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color compression = new Color(0.08f, 0.30f, 1f, 1f);
        Color cyan = new Color(0.08f, 0.78f, 0.94f, 1f);
        Color zero = new Color(0.91f, 0.94f, 0.97f, 1f);
        Color yellow = new Color(1f, 0.68f, 0.12f, 1f);
        Color tension = new Color(0.95f, 0.18f, 0.16f, 1f);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            Color color;
            if (t < 0.25f)
            {
                color = Color.Lerp(compression, cyan, t / 0.25f);
            }
            else if (t < 0.5f)
            {
                color = Color.Lerp(cyan, zero, (t - 0.25f) / 0.25f);
            }
            else if (t < 0.75f)
            {
                color = Color.Lerp(zero, yellow, (t - 0.5f) / 0.25f);
            }
            else
            {
                color = Color.Lerp(yellow, tension, (t - 0.75f) / 0.25f);
            }

            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = color;
            }
        }

        strainGradientTexture.SetPixels(pixels);
        strainGradientTexture.Apply(false, true);
        strainGradientSprite = Sprite.Create(
            strainGradientTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f);
        strainGradientSprite.name = "Runtime_Strain_Gradient";
        strainGradientSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void StyleRounded(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    private void AddSoftShadow(
        GameObject target,
        float alphaValue,
        Vector2 offset)
    {
        if (target == null)
        {
            return;
        }

        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alphaValue);
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }

    private void CreateStartupOverlay()
    {
        if (!showStartupAnimation || canvas == null)
        {
            return;
        }

        startupOverlay = CreateImage(
            "StartupOverlay",
            canvas.transform,
            worldBackground);
        Image overlayImage = startupOverlay.GetComponent<Image>();
        overlayImage.raycastTarget = true;
        SetFullScreen(startupOverlay.GetComponent<RectTransform>());
        startupOverlay.transform.SetAsLastSibling();

        startupCanvasGroup = startupOverlay.AddComponent<CanvasGroup>();
        startupCanvasGroup.alpha = 1f;
        startupCanvasGroup.interactable = true;
        startupCanvasGroup.blocksRaycasts = true;

        GameObject gridRoot = new GameObject(
            "StartupGrid",
            typeof(RectTransform));
        gridRoot.transform.SetParent(startupOverlay.transform, false);
        SetFullScreen(gridRoot.GetComponent<RectTransform>());
        Color gridColor = new Color(0.12f, 0.34f, 0.58f, 0.10f);
        for (int i = 1; i < 12; i++)
        {
            GameObject vertical = CreateImage(
                "StartupGridVertical" + i,
                gridRoot.transform,
                gridColor);
            RectTransform verticalRect = vertical.GetComponent<RectTransform>();
            float anchor = i / 12f;
            verticalRect.anchorMin = new Vector2(anchor, 0f);
            verticalRect.anchorMax = new Vector2(anchor, 1f);
            verticalRect.pivot = new Vector2(0.5f, 0.5f);
            verticalRect.sizeDelta = new Vector2(1f, 0f);
            verticalRect.anchoredPosition = Vector2.zero;
        }

        for (int i = 1; i < 8; i++)
        {
            GameObject horizontal = CreateImage(
                "StartupGridHorizontal" + i,
                gridRoot.transform,
                gridColor);
            RectTransform horizontalRect = horizontal.GetComponent<RectTransform>();
            float anchor = i / 8f;
            horizontalRect.anchorMin = new Vector2(0f, anchor);
            horizontalRect.anchorMax = new Vector2(1f, anchor);
            horizontalRect.pivot = new Vector2(0.5f, 0.5f);
            horizontalRect.sizeDelta = new Vector2(0f, 1f);
            horizontalRect.anchoredPosition = Vector2.zero;
        }

        GameObject startupCard = CreatePanel(
            "StartupCard",
            startupOverlay.transform,
            new Color(0.035f, 0.12f, 0.215f, 0.985f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(620f, 340f));
        AddAccentLine(
            startupCard.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f));

        CreateText(
            "StartupEyebrow",
            startupCard.transform,
            "SYSTEM INITIALIZATION",
            10,
            FontStyle.Bold,
            accent,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 134f),
            new Vector2(360f, 22f));

        GameObject logo = CreateImage(
            "StartupLogo",
            startupCard.transform,
            accentBlue);
        StyleRounded(logo.GetComponent<Image>(), roundedPanelSprite);
        startupLogoRect = logo.GetComponent<RectTransform>();
        startupLogoRect.anchorMin = new Vector2(0.5f, 0.5f);
        startupLogoRect.anchorMax = new Vector2(0.5f, 0.5f);
        startupLogoRect.pivot = new Vector2(0.5f, 0.5f);
        startupLogoRect.sizeDelta = new Vector2(72f, 72f);
        startupLogoRect.anchoredPosition = new Vector2(0f, 76f);
        AddSoftShadow(logo, 0.30f, new Vector2(0f, -4f));

        if (startupLogo != null)
        {
            GameObject customLogo = CreateImage(
                "StartupCustomLogo",
                logo.transform,
                Color.white);
            Image customLogoImage = customLogo.GetComponent<Image>();
            customLogoImage.sprite = startupLogo;
            customLogoImage.type = Image.Type.Simple;
            customLogoImage.preserveAspect = true;
            customLogoImage.raycastTarget = false;

            RectTransform customLogoRect = customLogo.GetComponent<RectTransform>();
            customLogoRect.anchorMin = Vector2.zero;
            customLogoRect.anchorMax = Vector2.one;
            customLogoRect.pivot = new Vector2(0.5f, 0.5f);
            customLogoRect.offsetMin = new Vector2(8f, 8f);
            customLogoRect.offsetMax = new Vector2(-8f, -8f);
        }
        else
        {
            Text logoText = CreateText(
                "StartupLogoText",
                logo.transform,
                "DT",
                21,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                new Vector2(64f, 64f));
            RectTransform logoTextRect = logoText.rectTransform;
            logoTextRect.anchorMin = Vector2.zero;
            logoTextRect.anchorMax = Vector2.one;
            logoTextRect.pivot = new Vector2(0.5f, 0.5f);
            logoTextRect.offsetMin = Vector2.zero;
            logoTextRect.offsetMax = Vector2.zero;
        }

        CreateText(
            "StartupTitle",
            startupCard.transform,
            startupTitle,
            28,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 14f),
            new Vector2(520f, 42f));
        CreateText(
            "StartupSubtitle",
            startupCard.transform,
            startupSubtitle,
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -20f),
            new Vector2(540f, 22f));

        GameObject progressTrack = CreateImage(
            "StartupProgressTrack",
            startupCard.transform,
            new Color(0.03f, 0.10f, 0.18f, 1f));
        StyleRounded(progressTrack.GetComponent<Image>(), roundedPillSprite);
        RectTransform trackRect = progressTrack.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(440f, 6f);
        trackRect.anchoredPosition = new Vector2(0f, -74f);

        GameObject progressFill = CreateImage(
            "StartupProgressFill",
            progressTrack.transform,
            accentBlue);
        startupProgressFill = progressFill.GetComponent<Image>();
        startupProgressFill.sprite = roundedPillSprite;
        startupProgressFill.type = Image.Type.Filled;
        startupProgressFill.fillMethod = Image.FillMethod.Horizontal;
        startupProgressFill.fillOrigin = 0;
        startupProgressFill.fillAmount = 0f;
        RectTransform fillRect = progressFill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        startupStatusText = CreateText(
            "StartupStatus",
            startupCard.transform,
            "正在初始化三维场景…",
            11,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -104f),
            new Vector2(440f, 24f));
        CreateText(
            "StartupFooter",
            startupCard.transform,
            "STRUCTURAL ANALYSIS WORKSPACE",
            9,
            FontStyle.Normal,
            new Color(0.42f, 0.58f, 0.74f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0f, -142f),
            new Vector2(440f, 20f));
    }

    private IEnumerator PlayStartupAnimation()
    {
        if (startupOverlay == null || startupCanvasGroup == null)
        {
            yield break;
        }

        startupAnimationPlaying = true;
        float duration = Mathf.Max(1.2f, startupAnimationDuration);
        float fadeOutDuration = Mathf.Min(0.50f, duration * 0.20f);
        float elapsed = 0f;
        bool interfaceRevealed = false;

        startupCanvasGroup.alpha = 1f;
        if (hudRoot != null)
        {
            hudRoot.SetActive(false);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (elapsed > duration - fadeOutDuration)
            {
                if (!interfaceRevealed)
                {
                    interfaceRevealed = true;
                    if (hudRoot != null)
                    {
                        hudRoot.SetActive(hudVisible);
                    }
                }

                startupCanvasGroup.alpha = Mathf.SmoothStep(
                    1f,
                    0f,
                    (elapsed - (duration - fadeOutDuration)) /
                    fadeOutDuration);
            }
            else
            {
                startupCanvasGroup.alpha = 1f;
            }

            if (startupProgressFill != null)
            {
                startupProgressFill.fillAmount = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);
            }

            if (startupLogoRect != null)
            {
                float logoProgress = Mathf.Clamp01(progress / 0.32f);
                float logoScale = Mathf.Lerp(
                    0.82f,
                    1f,
                    1f - Mathf.Pow(1f - logoProgress, 3f));
                startupLogoRect.localScale = Vector3.one * logoScale;
                startupLogoRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-5f, 0f, logoProgress));
            }

            if (startupStatusText != null)
            {
                if (progress < 0.34f)
                {
                    startupStatusText.text = "正在初始化三维场景…";
                }
                else if (progress < 0.67f)
                {
                    startupStatusText.text = "正在加载有限元可视化单元…";
                }
                else if (progress < 0.92f)
                {
                    startupStatusText.text = "正在准备数据通信接口…";
                }
                else
                {
                    startupStatusText.text = "系统准备完成";
                }
            }

            yield return null;
        }

        startupAnimationPlaying = false;
        if (hudRoot != null)
        {
            hudRoot.SetActive(hudVisible);
        }
        if (startupOverlay != null)
        {
            Destroy(startupOverlay);
            startupOverlay = null;
        }
    }

    private void CreateHud()
    {
        hudRoot = new GameObject("HUD", typeof(RectTransform));
        hudRoot.transform.SetParent(canvas.transform, false);
        SetFullScreen(hudRoot.GetComponent<RectTransform>());

        GameObject veil = CreateImage(
            "HUD_Veil",
            hudRoot.transform,
            new Color(0.005f, 0.025f, 0.055f, 0.024f));
        SetFullScreen(veil.GetComponent<RectTransform>());

        topBarPanel = CreatePanel(
            "TopBar",
            hudRoot.transform,
            panelColor,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(16f, -72f),
            new Vector2(-16f, -10f));
        GameObject topBar = topBarPanel;
        AddAccentLine(topBar.transform, new Vector2(0f, 0f), new Vector2(1f, 0f));

        GameObject logo = CreateImage(
            "ProductLogo",
            topBar.transform,
            accentBlue);
        Image logoImage = logo.GetComponent<Image>();
        StyleRounded(logoImage, roundedControlSprite);
        RectTransform logoRect = logo.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 0.5f);
        logoRect.anchorMax = new Vector2(0f, 0.5f);
        logoRect.pivot = new Vector2(0f, 0.5f);
        logoRect.sizeDelta = new Vector2(40f, 40f);
        logoRect.anchoredPosition = new Vector2(16f, 0f);
        Text logoText = CreateText(
            "ProductLogoText",
            logo.transform,
            "DT",
            14,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(36f, 36f));
        logoText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        logoText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        logoText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        logoText.rectTransform.anchoredPosition = Vector2.zero;

        CreateText(
            "ProductTitle",
            topBar.transform,
            "Truss Digital Twin",
            21,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(72f, 10f),
            new Vector2(420f, 30f));
        CreateText(
            "ProductSubtitle",
            topBar.transform,
            "Structural response workspace  /  Four-point strain",
            11,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(73f, -14f),
            new Vector2(520f, 20f));

        GameObject connectionBadge = CreateImage(
            "ConnectionBadge",
            topBar.transform,
            surfaceControl);
        StyleRounded(connectionBadge.GetComponent<Image>(), roundedPillSprite);
        RectTransform badgeRect = connectionBadge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0.5f);
        badgeRect.anchorMax = new Vector2(1f, 0.5f);
        badgeRect.pivot = new Vector2(1f, 0.5f);
        badgeRect.sizeDelta = new Vector2(214f, 36f);
        badgeRect.anchoredPosition = new Vector2(-24f, -2f);
        connectionDot = CreateImage(
            "ConnectionDot",
            connectionBadge.transform,
            accent).GetComponent<Image>();
        StyleRounded(connectionDot, roundedPillSprite);
        RectTransform dotRect = connectionDot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0f, 0.5f);
        dotRect.anchorMax = new Vector2(0f, 0.5f);
        dotRect.pivot = new Vector2(0f, 0.5f);
        dotRect.sizeDelta = new Vector2(10f, 10f);
        dotRect.anchoredPosition = new Vector2(14f, 0f);
        connectionText = CreateText(
            "ConnectionText",
            connectionBadge.transform,
            "LOCAL DEMO",
            12,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(32f, 0f),
            new Vector2(168f, 30f));

        CreateViewportHeaders(hudRoot.transform);

        analysisPanel = CreatePanel(
            "AnalysisPanel",
            hudRoot.transform,
            panelColor,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -128f),
            new Vector2(322f, 424f));
        GameObject leftPanel = analysisPanel;
        CreateText(
            "AnalysisHeading",
            leftPanel.transform,
            "Analysis",
            15,
            FontStyle.Bold,
            accent,
            TextAnchor.MiddleLeft,
            new Vector2(22f, 185f),
            new Vector2(250f, 26f));
        CreateText(
            "AnalysisSubheading",
            leftPanel.transform,
            "MODEL SUMMARY",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(22f, 158f),
            new Vector2(250f, 20f));
        AddDivider(leftPanel.transform, 132f);

        elementText = CreateStatRow(leftPanel.transform, "Elements", "--", 98f);
        jointText = CreateStatRow(leftPanel.transform, "Joint blocks", "--", 62f);
        peakStrainText = CreateStatRow(leftPanel.transform, "Peak |strain|", "--", 26f);
        modeText = CreateStatRow(leftPanel.transform, "Data source", "--", -10f);

        CreateButton(
            "FocusButton",
            leftPanel.transform,
            "聚焦模型",
            new Vector2(0f, -76f),
            accentBlue,
            FocusModel);
        resetResultsButton = CreateButton(
            "ResetButton",
            leftPanel.transform,
            "重置结果",
            new Vector2(0f, -132f),
            surfaceControl,
            ResetResults);
        demoBendingButton = CreateButton(
            "BendingButton",
            leftPanel.transform,
            "弯曲演示",
            new Vector2(0f, -188f),
            new Color(0.09f, 0.28f, 0.46f, 1f),
            RunBendingDemo);
        CreateText(
            "AnalysisHint",
            leftPanel.transform,
            "F  focus    R  reset    F1  toggle HUD",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(22f, -224f),
            new Vector2(270f, 22f));

        legendPanel = CreatePanel(
            "LegendPanel",
            hudRoot.transform,
            panelColor,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -128f),
            new Vector2(246f, 424f));
        GameObject currentLegendPanel = legendPanel;
        CreateText(
            "LegendHeading",
            currentLegendPanel.transform,
            "Result scale",
            15,
            FontStyle.Bold,
            accent,
            TextAnchor.MiddleLeft,
            new Vector2(20f, 185f),
            new Vector2(210f, 26f));
        CreateText(
            "LegendSubheading",
            currentLegendPanel.transform,
            "Engineering strain",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(20f, 158f),
            new Vector2(210f, 20f));
        AddDivider(currentLegendPanel.transform, 132f);
        CreateStrainLegend(currentLegendPanel.transform);

        CreateInteractionPanel(hudRoot.transform);

        bottomBarPanel = CreatePanel(
            "BottomBar",
            hudRoot.transform,
            toolbarColor,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 34f));
        GameObject bottomBar = bottomBarPanel;
        bottomStatusText = CreateText(
            "BottomStatus",
            bottomBar.transform,
            "Ready  /  Awaiting structural data",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(24f, 0f),
            new Vector2(760f, 28f));
        timestampText = CreateText(
            "Timestamp",
            bottomBar.transform,
            "T+0.0 s",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleRight,
            new Vector2(-24f, 0f),
            new Vector2(180f, 28f));

        CreatePanelToggleDock(hudRoot.transform);
    }

    private void CreateViewportHeaders(Transform parent)
    {
        viewportHeaderRoot = new GameObject(
            "ViewportHeaderGroup",
            typeof(RectTransform));
        viewportHeaderRoot.transform.SetParent(parent, false);
        SetFullScreen(viewportHeaderRoot.GetComponent<RectTransform>());
        Transform headerParent = viewportHeaderRoot.transform;

        GameObject divider = CreateImage(
            "ViewportDivider",
            headerParent,
            new Color(0.18f, 0.48f, 0.78f, 0.82f));
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 0f);
        dividerRect.anchorMax = new Vector2(0.5f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(2f, -114f);
        dividerRect.anchoredPosition = new Vector2(0f, -15f);

        CreateViewportBadge(
            headerParent,
            "OriginalViewBadge",
            "Reference model",
            0.25f,
            mutedInk);
        CreateViewportBadge(
            headerParent,
            "StressViewBadge",
            "Stress / strain result",
            0.75f,
            accent);
    }

    private void CreateViewportBadge(
        Transform parent,
        string objectName,
        string label,
        float anchorX,
        Color labelColor)
    {
        GameObject badge = CreateImage(
            objectName,
            parent,
            surfaceControl);
        StyleRounded(badge.GetComponent<Image>(), roundedPillSprite);
        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorX, 1f);
        rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(214f, 28f);
        rect.anchoredPosition = new Vector2(0f, -120f);

        Text badgeText = CreateText(
            objectName + "Text",
            badge.transform,
            label,
            11,
            FontStyle.Normal,
            labelColor,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(198f, 24f));
        RectTransform textRect = badgeText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
    }

    private void CreateInteractionPanel(Transform parent)
    {
        interactionPanel = CreatePanel(
            "ElementInteractionPanel",
            parent,
            panelColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 46f),
            new Vector2(820f, 214f));
        GameObject panel = interactionPanel;
        AddAccentLine(panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f));

        CreateText(
            "InteractionHeading",
            panel.transform,
            "Element inspector",
            15,
            FontStyle.Bold,
            accent,
            TextAnchor.MiddleLeft,
            new Vector2(22f, 93f),
            new Vector2(260f, 26f));
        selectedModeText = CreateText(
            "SelectedMode",
            panel.transform,
            "OFFLINE LOAD PREVIEW",
            11,
            FontStyle.Bold,
            mutedInk,
            TextAnchor.MiddleRight,
            new Vector2(500f, 93f),
            new Vector2(250f, 24f));

        GameObject divider = CreateImage(
            "InteractionDivider",
            panel.transform,
            borderBlue);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 0.5f);
        dividerRect.anchorMax = new Vector2(0f, 0.5f);
        dividerRect.pivot = new Vector2(0f, 0.5f);
        dividerRect.sizeDelta = new Vector2(736f, 1f);
        dividerRect.anchoredPosition = new Vector2(22f, 70f);

        selectedElementText = CreateText(
            "SelectedElement",
            panel.transform,
            "No element selected",
            13,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(22f, 43f),
            new Vector2(270f, 30f));
        selectedStrainText = CreateText(
            "SelectedStrain",
            panel.transform,
            "Select a virtual element in the right viewport",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.UpperLeft,
            new Vector2(300f, 55f),
            new Vector2(450f, 52f));

        CreateText(
            "ForceLabel",
            panel.transform,
            "FORCE",
            10,
            FontStyle.Bold,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(22f, -16f),
            new Vector2(60f, 24f));
        forceInput = CreateInputField(
            "ForceInput",
            panel.transform,
            "5",
            new Vector2(82f, -16f),
            new Vector2(112f, 34f));
        CreateText(
            "ForceUnit",
            panel.transform,
            "kN",
            10,
            FontStyle.Bold,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(202f, -16f),
            new Vector2(32f, 24f));

        directionButton = CreateCompactButton(
            "DirectionButton",
            panel.transform,
            dualViewInteraction != null
                ? dualViewInteraction.GetDirectionLabel()
                : "局部 -Z",
            new Vector2(-52f, -18f),
            new Vector2(176f, 36f),
            new Color(0.09f, 0.31f, 0.50f, 1f),
            CycleLoadDirection);
        directionButtonText =
            directionButton.GetComponentInChildren<Text>();

        applyForceButton = CreateCompactButton(
            "ApplyForceButton",
            panel.transform,
            "施加载荷",
            new Vector2(142f, -18f),
            new Vector2(160f, 36f),
            accentBlue,
            ApplySelectedForce);
        clearBeamButton = CreateCompactButton(
            "ClearBeamButton",
            panel.transform,
            "清除梁结果",
            new Vector2(314f, -18f),
            new Vector2(150f, 36f),
            dangerColor,
            ClearSelectedBeam);

        interactionStatusText = CreateText(
            "InteractionStatus",
            panel.transform,
            "SELECT AN ELEMENT IN THE STRESS VIEW",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(22f, -86f),
            new Vector2(730f, 24f));
    }

    private void CreatePanelToggleDock(Transform parent)
    {
        panelToggleDock = CreatePanel(
            "PanelToggleDock",
            parent,
            toolbarColor,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(16f, -112f),
            new Vector2(-16f, -76f));
        panelToggleDock.transform.SetAsLastSibling();

        CreateText(
            "PanelToggleDockLabel",
            panelToggleDock.transform,
            "WORKSPACE",
            10,
            FontStyle.Bold,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(18f, 0f),
            new Vector2(110f, 26f));

        CreatePanelToggleButton(
            panelToggleDock.transform,
            "HeaderPanelToggle",
            "标题栏",
            topBarPanel,
            new Vector2(-282f, 0f));
        CreatePanelToggleButton(
            panelToggleDock.transform,
            "AnalysisPanelToggle",
            "分析",
            analysisPanel,
            new Vector2(-169f, 0f));
        CreatePanelToggleButton(
            panelToggleDock.transform,
            "LegendPanelToggle",
            "图例",
            legendPanel,
            new Vector2(-56f, 0f));
        CreatePanelToggleButton(
            panelToggleDock.transform,
            "InspectorPanelToggle",
            "单元",
            interactionPanel,
            new Vector2(57f, 0f));
        CreatePanelToggleButton(
            panelToggleDock.transform,
            "StatusPanelToggle",
            "状态",
            bottomBarPanel,
            new Vector2(170f, 0f));
        CreatePanelToggleButton(
            panelToggleDock.transform,
            "ViewportLabelsToggle",
            "视图",
            viewportHeaderRoot,
            new Vector2(283f, 0f));
    }

    private void CreatePanelToggleButton(
        Transform parent,
        string objectName,
        string label,
        GameObject targetPanel,
        Vector2 position)
    {
        Button toggleButton = null;
        toggleButton = CreateCompactButton(
            objectName,
            parent,
            label,
            position,
            new Vector2(104f, 26f),
            surfaceControl,
            () => TogglePanelVisibility(
                targetPanel,
                toggleButton,
                label));
        UpdatePanelToggleButton(targetPanel, toggleButton, label);
    }

    private void TogglePanelVisibility(
        GameObject targetPanel,
        Button toggleButton,
        string label)
    {
        if (targetPanel == null)
        {
            return;
        }

        targetPanel.SetActive(!targetPanel.activeSelf);
        UpdatePanelToggleButton(targetPanel, toggleButton, label);
    }

    private void UpdatePanelToggleButton(
        GameObject targetPanel,
        Button toggleButton,
        string label)
    {
        if (targetPanel == null || toggleButton == null)
        {
            return;
        }

        bool visible = targetPanel.activeSelf;
        Text buttonText = toggleButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = label;
            buttonText.color = visible ? ink : mutedInk;
        }

        Color normalColor = visible
            ? new Color(0.09f, 0.31f, 0.50f, 1f)
            : surfaceControl;
        Image buttonImage = toggleButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = Color.white;
        }

        ColorBlock colors = toggleButton.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.24f);
        colors.selectedColor = colors.highlightedColor;
        toggleButton.colors = colors;
    }

    private void CreateMenu()
    {
        menuPanel = CreateImage(
            "EscapeMenuOverlay",
            canvas.transform,
            new Color(0.005f, 0.01f, 0.018f, 0.72f));
        menuPanel.GetComponent<Image>().raycastTarget = true;
        SetFullScreen(menuPanel.GetComponent<RectTransform>());

        GameObject menuBox = CreatePanel(
            "EscapeMenuBox",
            menuPanel.transform,
            surfaceRaised,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(500f, 520f));
        AddAccentLine(menuBox.transform, new Vector2(0f, 1f), new Vector2(1f, 1f));
        CreateText(
            "MenuTitle",
            menuBox.transform,
            "Session",
            23,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(34f, 205f),
            new Vector2(440f, 34f));
        CreateText(
            "MenuSubtitle",
            menuBox.transform,
            "Truss Digital Twin  /  Workspace controls",
            11,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(36f, 174f),
            new Vector2(420f, 22f));
        AddDivider(menuBox.transform, 142f);
        menuStatusText = CreateText(
            "MenuStatus",
            menuBox.transform,
            "LOCAL DEMO",
            12,
            FontStyle.Bold,
            accent,
            TextAnchor.MiddleLeft,
            new Vector2(36f, 112f),
            new Vector2(420f, 24f));

        CreateButton(
            "ResumeButton",
            menuBox.transform,
            "继续运行",
            new Vector2(0f, 62f),
            accent,
            ResumeGame);
        CreateButton(
            "MenuFocusButton",
            menuBox.transform,
            "重置视角",
            new Vector2(0f, 2f),
            accentBlue,
            FocusModel);
        menuResetResultsButton = CreateButton(
            "MenuResetButton",
            menuBox.transform,
            "重置应变结果",
            new Vector2(0f, -58f),
            surfaceControl,
            ResetResults);
        CreateButton(
            "MenuHudButton",
            menuBox.transform,
            "显示或隐藏界面",
            new Vector2(0f, -118f),
            new Color(0.09f, 0.28f, 0.46f, 1f),
            ToggleHudFromMenu);
        CreateButton(
            "ExitButton",
            menuBox.transform,
            "退出软件",
            new Vector2(0f, -178f),
            dangerColor,
            ExitApplication);
        CreateText(
            "MenuHint",
            menuBox.transform,
            "ESC  close menu      F1  HUD      F  focus      R  reset",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -230f),
            new Vector2(450f, 24f));
    }

    private Text CreateStatRow(
        Transform parent,
        string label,
        string value,
        float y)
    {
        CreateText(
            label + "Label",
            parent,
            label,
            11,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(22f, y),
            new Vector2(150f, 24f));
        Text valueText = CreateText(
            label + "Value",
            parent,
            value,
            13,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleRight,
            new Vector2(166f, y),
            new Vector2(130f, 24f));
        return valueText;
    }

    private void CreateStrainLegend(Transform parent)
    {
        GameObject frame = CreateImage(
            "StrainGradientFrame",
            parent,
            borderBlue);
        Image frameImage = frame.GetComponent<Image>();
        StyleRounded(frameImage, roundedControlSprite);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.sizeDelta = new Vector2(30f, 224f);
        frameRect.anchoredPosition = new Vector2(24f, -14f);

        GameObject gradient = CreateImage(
            "StrainGradient",
            frame.transform,
            Color.white);
        Image gradientImage = gradient.GetComponent<Image>();
        gradientImage.sprite = strainGradientSprite;
        gradientImage.type = Image.Type.Simple;
        RectTransform gradientRect = gradient.GetComponent<RectTransform>();
        gradientRect.anchorMin = Vector2.zero;
        gradientRect.anchorMax = Vector2.one;
        gradientRect.pivot = new Vector2(0.5f, 0.5f);
        gradientRect.offsetMin = new Vector2(5f, 5f);
        gradientRect.offsetMax = new Vector2(-5f, -5f);

        CreateText(
            "LegendTensionValue",
            parent,
            "+εmax",
            12,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(70f, 92f),
            new Vector2(140f, 22f));
        CreateText(
            "LegendTensionLabel",
            parent,
            "Tension",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(70f, 72f),
            new Vector2(140f, 18f));
        CreateText(
            "LegendZeroValue",
            parent,
            "0",
            12,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(70f, -14f),
            new Vector2(140f, 22f));
        CreateText(
            "LegendZeroLabel",
            parent,
            "Neutral",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(70f, -34f),
            new Vector2(140f, 18f));
        CreateText(
            "LegendCompressionValue",
            parent,
            "−εmax",
            12,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(70f, -118f),
            new Vector2(140f, 22f));
        CreateText(
            "LegendCompressionLabel",
            parent,
            "Compression",
            10,
            FontStyle.Normal,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(70f, -138f),
            new Vector2(140f, 18f));
        CreateText(
            "LegendRange",
            parent,
            "Normalized live range",
            10,
            FontStyle.Italic,
            mutedInk,
            TextAnchor.MiddleLeft,
            new Vector2(24f, -178f),
            new Vector2(196f, 20f));
    }

    private void CreateColorSwatch(
        Transform parent,
        string objectName,
        string label,
        Color color,
        float y)
    {
        GameObject swatch = CreateImage(
            objectName + "Swatch",
            parent,
            color);
        RectTransform rect = swatch.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(30f, 22f);
        rect.anchoredPosition = new Vector2(20f, y);
        CreateText(
            objectName + "Label",
            parent,
            label,
            11,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(62f, y),
            new Vector2(170f, 24f));
    }

    private GameObject CreatePanel(
        string objectName,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject panel = CreateImage(objectName, parent, color);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.raycastTarget = true;
        StyleRounded(panelImage, roundedPanelSprite);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        if (anchorMin == anchorMax)
        {
            rect.pivot = anchorMin;
            rect.sizeDelta = offsetMax;
            rect.anchoredPosition = offsetMin;
        }
        else
        {
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        AddSoftShadow(panel, 0.30f, new Vector2(0f, -5f));
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = borderBlue;
        outline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private GameObject CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.font = uiFont;
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        Color normalColor,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(
            objectName,
            parent,
            Color.white);
        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;
        StyleRounded(image, roundedControlSprite);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.20f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.16f, 0.24f, 0.34f, 0.52f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.10f;
        button.colors = colors;

        AddSoftShadow(buttonObject, 0.20f, new Vector2(0f, -2f));

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(286f, 36f);
        rect.anchoredPosition = position;

        Text buttonLabel = CreateText(
            objectName + "Label",
            buttonObject.transform,
            label,
            11,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(270f, 30f));
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;
        return button;
    }

    private Button CreateCompactButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        Vector2 size,
        Color normalColor,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(
            objectName,
            parent,
            label,
            position,
            normalColor,
            action);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Text labelText = button.GetComponentInChildren<Text>();
        if (labelText != null)
        {
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);
            labelRect.anchoredPosition = Vector2.zero;
        }

        return button;
    }

    private InputField CreateInputField(
        string objectName,
        Transform parent,
        string defaultValue,
        Vector2 position,
        Vector2 size)
    {
        GameObject inputObject = CreateImage(
            objectName,
            parent,
            Color.white);
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.raycastTarget = true;
        StyleRounded(inputImage, roundedControlSprite);

        InputField input = inputObject.AddComponent<InputField>();
        input.targetGraphic = inputImage;
        input.transition = Selectable.Transition.ColorTint;
        input.contentType = InputField.ContentType.DecimalNumber;
        input.lineType = InputField.LineType.SingleLine;
        input.caretColor = accent;
        input.caretWidth = 2;
        input.selectionColor = new Color(0.18f, 0.50f, 0.93f, 0.55f);
        ColorBlock inputColors = input.colors;
        inputColors.normalColor = surfaceControl;
        inputColors.highlightedColor = new Color(0.071f, 0.208f, 0.325f, 1f);
        inputColors.selectedColor = new Color(0.09f, 0.275f, 0.424f, 1f);
        inputColors.pressedColor = inputColors.selectedColor;
        inputColors.disabledColor = new Color(0.035f, 0.08f, 0.13f, 0.55f);
        inputColors.colorMultiplier = 1f;
        inputColors.fadeDuration = 0.10f;
        input.colors = inputColors;
        AddSoftShadow(inputObject, 0.16f, new Vector2(0f, -2f));

        Text valueText = CreateText(
            objectName + "Value",
            inputObject.transform,
            defaultValue,
            12,
            FontStyle.Bold,
            ink,
            TextAnchor.MiddleLeft,
            new Vector2(10f, 0f),
            new Vector2(size.x - 18f, size.y - 4f));
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.pivot = new Vector2(0.5f, 0.5f);
        valueRect.offsetMin = new Vector2(10f, 2f);
        valueRect.offsetMax = new Vector2(-8f, -2f);

        input.textComponent = valueText;
        input.text = defaultValue;
        return input;
    }

    private void AddAccentLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject line = CreateImage("AccentLine", parent, accent);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 2f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void AddDivider(Transform parent, float y)
    {
        GameObject divider = CreateImage(
            "Divider",
            parent,
            borderBlue);
        RectTransform rect = divider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(276f, 1f);
        rect.anchoredPosition = new Vector2(22f, y);
    }

    private void SetFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void UpdateStatus(bool force)
    {
        if (!force && Time.unscaledTime - lastStatusRefresh < 0.35f)
        {
            return;
        }

        lastStatusRefresh = Time.unscaledTime;
        if (stressElements == null)
        {
            CacheStressVisuals();
        }

        FourPointBeamElementVisual[] elements = stressElements;
        FourPointJointVisual[] joints = stressJoints;

        float peak = 0f;
        if (elements != null)
        {
            foreach (FourPointBeamElementVisual element in elements)
            {
                if (element == null)
                {
                    continue;
                }

                peak = Mathf.Max(
                    peak,
                    Mathf.Abs(element.strainP1),
                    Mathf.Abs(element.strainP2),
                    Mathf.Abs(element.strainP3),
                    Mathf.Abs(element.strainP4));
            }
        }

        bool isConnected = receiver != null && receiver.IsConnected;
        string source = isConnected ? "WebSocket live" :
            receiver != null ? "Local / offline" : "Local demo";
        string connection = isConnected ? "Live stream" : "Local demo";
        Color statusColor = isConnected ? successColor : mutedInk;

        if (connectionText != null)
        {
            connectionText.text = connection;
            connectionText.color = statusColor;
        }

        if (connectionDot != null)
        {
            connectionDot.color = isConnected ? accent : mutedInk;
        }

        if (elementText != null)
        {
            elementText.text = elements == null ? "0" : elements.Length.ToString();
        }

        if (jointText != null)
        {
            jointText.text = joints == null ? "0" : joints.Length.ToString();
        }

        if (peakStrainText != null)
        {
            peakStrainText.text = peak.ToString("0.000E+0");
        }

        if (modeText != null)
        {
            modeText.text = source;
        }

        if (bottomStatusText != null)
        {
            bottomStatusText.text = isConnected ?
                "Connected  /  Streaming four-point strain data" :
                "Ready  /  Local demo mode";
            bottomStatusText.color = statusColor;
        }

        if (menuStatusText != null)
        {
            menuStatusText.text = connection + "  /  " +
                                  (elements == null ? 0 : elements.Length) +
                                  " ELEMENTS";
            menuStatusText.color = statusColor;
        }

        if (timestampText != null)
        {
            timestampText.text = "T+" + Time.unscaledTime.ToString("0.0") + " s";
        }

        UpdateSelectionPanel();
    }

    private void UpdateSelectionPanel()
    {
        if (dualViewInteraction == null)
        {
            return;
        }

        FourPointBeamElementVisual selected =
            dualViewInteraction.SelectedElement;
        bool online = dualViewInteraction.IsOnline;

        if (selectedModeText != null)
        {
            selectedModeText.text = online
                ? "ONLINE / READ ONLY"
                : "OFFLINE / LOAD PREVIEW";
            selectedModeText.color = online ? accent : mutedInk;
        }

        if (selectedElementText != null)
        {
            selectedElementText.text = selected == null
                ? "No element selected"
                : selected.elementId + "  /  " + selected.beamId;
        }

        if (selectedStrainText != null)
        {
            if (selected == null)
            {
                selectedStrainText.text =
                    "Select a virtual element in the right viewport";
            }
            else
            {
                float stressFactor =
                    dualViewInteraction.ElasticModulusGPa * 1000f;
                string strainLine = string.Format(
                    CultureInfo.InvariantCulture,
                    "STRAIN  P1 {0:0.000E+0}   P2 {1:0.000E+0}   P3 {2:0.000E+0}   P4 {3:0.000E+0}",
                    selected.strainP1,
                    selected.strainP2,
                    selected.strainP3,
                    selected.strainP4);
                string stressLine = string.Format(
                    CultureInfo.InvariantCulture,
                    "STRESS* P1 {0:0.0}   P2 {1:0.0}   P3 {2:0.0}   P4 {3:0.0} MPa",
                    selected.strainP1 * stressFactor,
                    selected.strainP2 * stressFactor,
                    selected.strainP3 * stressFactor,
                    selected.strainP4 * stressFactor);
                selectedStrainText.text = strainLine + "\n" + stressLine;
            }
        }

        if (directionButtonText != null)
        {
            directionButtonText.text = dualViewInteraction.GetDirectionLabel();
        }

        if (forceInput != null)
        {
            forceInput.interactable = !online;
        }

        if (directionButton != null)
        {
            directionButton.interactable = !online;
        }

        if (applyForceButton != null)
        {
            applyForceButton.interactable = !online && selected != null;
        }

        if (clearBeamButton != null)
        {
            clearBeamButton.interactable = !online && selected != null;
        }

        if (resetResultsButton != null)
        {
            resetResultsButton.interactable = !online;
        }

        if (demoBendingButton != null)
        {
            demoBendingButton.interactable = !online;
        }

        if (menuResetResultsButton != null)
        {
            menuResetResultsButton.interactable = !online;
        }

        if (interactionStatusText != null)
        {
            string suffix = string.Empty;
            if (online && receiver != null && selected != null)
            {
                float selectedUpdate;
                suffix = receiver.TryGetElementLastUpdate(
                    selected.elementId,
                    out selectedUpdate)
                    ? "  /  SELECTED SAMPLE " +
                      Mathf.Max(
                          0f,
                          Time.realtimeSinceStartup - selectedUpdate)
                          .ToString("0.0") + " s AGO"
                    : "  /  NO LIVE SAMPLE FOR SELECTED ELEMENT";
            }
            interactionStatusText.text =
                dualViewInteraction.LastStatus + suffix;
            interactionStatusText.color = online ? accent : mutedInk;
        }
    }

    private void SetHudVisible(bool visible)
    {
        hudVisible = visible;
        if (hudRoot != null)
        {
            hudRoot.SetActive(visible);
        }
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

    private void ToggleHudFromMenu()
    {
        SetHudVisible(!hudVisible);
    }

    private void FocusModel()
    {
        CacheControllers();
        if (cameraController != null)
        {
            cameraController.FocusModel();
        }
    }

    private void ResetResults()
    {
        if (dualViewInteraction != null && dualViewInteraction.IsOnline)
        {
            UpdateSelectionPanel();
            return;
        }

        CacheControllers();
        if (stressController != null)
        {
            stressController.ResetAllElements();
        }

        if (dualViewInteraction != null)
        {
            dualViewInteraction.ClearLoadIndicator();
        }
    }

    private void RunBendingDemo()
    {
        if (dualViewInteraction != null && dualViewInteraction.IsOnline)
        {
            UpdateSelectionPanel();
            return;
        }

        CacheControllers();
        if (stressDemo != null)
        {
            stressDemo.ShowBendingExample();
        }
    }

    private void CycleLoadDirection()
    {
        if (dualViewInteraction == null || dualViewInteraction.IsOnline)
        {
            return;
        }

        dualViewInteraction.CycleDirection();
        if (directionButtonText != null)
        {
            directionButtonText.text = dualViewInteraction.GetDirectionLabel();
        }
    }

    private void ApplySelectedForce()
    {
        if (dualViewInteraction == null || forceInput == null)
        {
            return;
        }

        float forceKN;
        bool parsed = float.TryParse(
            forceInput.text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out forceKN);
        if (!parsed)
        {
            parsed = float.TryParse(forceInput.text, out forceKN);
        }

        if (!parsed)
        {
            interactionStatusText.text = "ENTER A VALID FORCE VALUE";
            interactionStatusText.color = dangerColor;
            return;
        }

        dualViewInteraction.ApplyOfflineForce(forceKN);
        UpdateSelectionPanel();
    }

    private void ClearSelectedBeam()
    {
        if (dualViewInteraction == null || dualViewInteraction.IsOnline)
        {
            return;
        }

        dualViewInteraction.ClearSelectedBeam();
        UpdateSelectionPanel();
    }

    private void ExitApplication()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        UnityEngine.Application.Quit();
#endif
    }

    private void CreateEventSystemIfNeeded()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            EventSystem[] systems = FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (systems != null && systems.Length > 0)
            {
                eventSystem = systems[0];
            }
        }

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
    }

    private void OnDestroy()
    {
        if (menuVisible)
        {
            Time.timeScale = 1f;
        }

        if (stageRoot != null)
        {
            Destroy(stageRoot);
        }

        if (stageFloorMesh != null)
        {
            Destroy(stageFloorMesh);
        }

        if (stageGridMesh != null)
        {
            Destroy(stageGridMesh);
        }

        if (stageFloorMaterial != null)
        {
            Destroy(stageFloorMaterial);
        }

        if (stageGridMaterial != null)
        {
            Destroy(stageGridMaterial);
        }

        if (roundedPanelSprite != null)
        {
            Destroy(roundedPanelSprite);
        }

        if (roundedControlSprite != null)
        {
            Destroy(roundedControlSprite);
        }

        if (roundedPillSprite != null)
        {
            Destroy(roundedPillSprite);
        }

        if (strainGradientSprite != null)
        {
            Destroy(strainGradientSprite);
        }

        if (roundedPanelTexture != null)
        {
            Destroy(roundedPanelTexture);
        }

        if (roundedControlTexture != null)
        {
            Destroy(roundedControlTexture);
        }

        if (roundedPillTexture != null)
        {
            Destroy(roundedPillTexture);
        }

        if (strainGradientTexture != null)
        {
            Destroy(strainGradientTexture);
        }

        if (uiFont != null)
        {
            Destroy(uiFont);
        }
    }
}
