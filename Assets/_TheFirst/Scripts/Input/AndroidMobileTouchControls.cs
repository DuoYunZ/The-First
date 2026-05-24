using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class AndroidMobileTouchControls : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const int SpriteSize = 128;

    private static AndroidMobileTouchControls instance;

    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private Sprite filledCircleSprite;
    private Sprite ringCircleSprite;
    private Font uiFont;
    private Rect lastSafeArea;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!ShouldRunOnThisPlatform() || instance != null)
        {
            return;
        }

        var go = new GameObject("[Android Mobile Touch Controls]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AndroidMobileTouchControls>();
    }

    private static bool ShouldRunOnThisPlatform()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!ShouldRunOnThisPlatform())
        {
            gameObject.SetActive(false);
            return;
        }

        EnsureEventSystem();
        BuildUi();
        UpdateVisibilityForActiveScene();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        DestroySpriteAndTexture(filledCircleSprite);
        DestroySpriteAndTexture(ringCircleSprite);
    }

    private void Update()
    {
        ApplySafeArea();
        UpdateVisibilityForActiveScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        UpdateVisibilityForActiveScene();
        ApplySafeArea();
    }

    private void BuildUi()
    {
        if (canvas != null)
        {
            return;
        }

        filledCircleSprite = CreateCircleSprite(false);
        ringCircleSprite = CreateCircleSprite(true);
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        var uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            uiLayer = 0;
        }

        var canvasObject = new GameObject("AndroidMobileTouchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = uiLayer;
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        safeAreaRoot = CreateRect("SafeArea", canvasObject.transform);
        safeAreaRoot.anchorMin = Vector2.zero;
        safeAreaRoot.anchorMax = Vector2.one;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;

        CreateStick("MoveStick", "<Gamepad>/leftStick", new Vector2(0f, 0f), new Vector2(180f, 165f));

        CreateButton("InteractButton", "Use", "<Gamepad>/buttonSouth", new Vector2(1f, 0f), new Vector2(-116f, 145f), 88f, new Color32(22, 24, 30, 145));
        CreateButton("DashButton", "Dash", "<Gamepad>/buttonEast", new Vector2(1f, 0f), new Vector2(-238f, 170f), 98f, new Color32(22, 24, 30, 145));
        CreateButton("UltimateButton", "Ult", "<Gamepad>/rightShoulder", new Vector2(1f, 0f), new Vector2(-128f, 270f), 104f, new Color32(22, 24, 30, 145));
        CreateButton("PauseButton", "II", "<Gamepad>/start", new Vector2(1f, 1f), new Vector2(-76f, -70f), 66f, new Color32(22, 24, 30, 135));
    }

    private void CreateStick(string name, string controlPath, Vector2 anchor, Vector2 anchoredPosition)
    {
        var zone = CreateRect(name, safeAreaRoot);
        zone.anchorMin = anchor;
        zone.anchorMax = anchor;
        zone.pivot = new Vector2(0.5f, 0.5f);
        zone.anchoredPosition = anchoredPosition;
        zone.sizeDelta = new Vector2(290f, 290f);

        var ring = CreateImage("Ring", zone, ringCircleSprite, new Color32(255, 255, 255, 86));
        ring.raycastTarget = false;
        SetRect(ring.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(190f, 190f));

        var controlObject = new GameObject("Control", typeof(RectTransform), typeof(Image), typeof(OnScreenStick));
        controlObject.layer = zone.gameObject.layer;
        controlObject.transform.SetParent(zone, false);

        var controlRect = controlObject.GetComponent<RectTransform>();
        SetRect(controlRect, Vector2.one * 0.5f, Vector2.zero, new Vector2(290f, 290f));

        var controlImage = controlObject.GetComponent<Image>();
        controlImage.sprite = filledCircleSprite;
        controlImage.color = new Color32(255, 255, 255, 1);

        var handle = CreateImage("Handle", controlRect, filledCircleSprite, new Color32(255, 255, 255, 72));
        handle.raycastTarget = false;
        SetRect(handle.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(112f, 112f));

        var stick = controlObject.GetComponent<OnScreenStick>();
        stick.controlPath = controlPath;
        stick.movementRange = 68f;
        stick.useIsolatedInputActions = false;
        stick.behaviour = OnScreenStick.Behaviour.ExactPositionWithStaticOrigin;
    }

    private void CreateButton(string name, string label, string controlPath, Vector2 anchor, Vector2 anchoredPosition, float size, Color color)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(OnScreenButton));
        buttonObject.layer = safeAreaRoot.gameObject.layer;
        buttonObject.transform.SetParent(safeAreaRoot, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);

        var image = buttonObject.GetComponent<Image>();
        image.sprite = filledCircleSprite;
        image.color = color;
        image.alphaHitTestMinimumThreshold = 0.2f;

        var ring = CreateImage("Ring", rect, ringCircleSprite, new Color32(255, 255, 255, 80));
        ring.raycastTarget = false;
        SetRect(ring.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(size, size));

        var button = buttonObject.GetComponent<OnScreenButton>();
        button.controlPath = controlPath;

        var text = CreateText("Label", label, rect, size >= 100f ? 22 : 20, new Color32(255, 255, 255, 210));
        SetRect(text.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(size * 0.95f, size * 0.55f));
    }

    private Text CreateText(string name, string value, Transform parent, int fontSize, Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.layer = ((Component)parent).gameObject.layer;
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = fontSize;
        if (uiFont != null)
        {
            text.font = uiFont;
        }

        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.layer = ((Component)parent).gameObject.layer;
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.layer = ((Component)parent).gameObject.layer;
        imageObject.transform.SetParent(parent, false);

        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static Sprite CreateCircleSprite(bool ringOnly)
    {
        var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var center = new Vector2((SpriteSize - 1) * 0.5f, (SpriteSize - 1) * 0.5f);
        var outerRadius = SpriteSize * 0.48f;
        var innerRadius = SpriteSize * 0.38f;

        for (var y = 0; y < SpriteSize; y++)
        {
            for (var x = 0; x < SpriteSize; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var alpha = 0f;
                if (ringOnly)
                {
                    alpha = Mathf.InverseLerp(outerRadius + 1.5f, outerRadius - 1.5f, distance) *
                            Mathf.InverseLerp(innerRadius - 1.5f, innerRadius + 1.5f, distance);
                }
                else
                {
                    alpha = Mathf.InverseLerp(outerRadius + 1.5f, outerRadius - 1.5f, distance);
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply(false, false);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), SpriteSize);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static void DestroySpriteAndTexture(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        var texture = sprite.texture;
        Destroy(sprite);
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private void ApplySafeArea()
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        var safeArea = Screen.safeArea;
        if (safeArea == lastSafeArea)
        {
            return;
        }

        lastSafeArea = safeArea;

        var anchorMin = safeArea.position;
        var anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        safeAreaRoot.anchorMin = anchorMin;
        safeAreaRoot.anchorMax = anchorMax;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private void UpdateVisibilityForActiveScene()
    {
        if (canvas == null)
        {
            return;
        }

        var shouldShow = ShouldShowInScene(SceneManager.GetActiveScene().name);
        if (canvas.gameObject.activeSelf != shouldShow)
        {
            canvas.gameObject.SetActive(shouldShow);
        }
    }

    private static bool ShouldShowInScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return true;
        }

        if (sceneName == "MainMenu" || sceneName == "CharacterSelectScene")
        {
            return false;
        }

        return sceneName == "Standard" ||
               sceneName.Contains("Hub") ||
               sceneName.Contains("Combat") ||
               sceneName.Contains("Arena") ||
               sceneName.StartsWith("Debug_");
    }
}
