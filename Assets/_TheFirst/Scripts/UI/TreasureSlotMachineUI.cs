using System.Collections;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime chest reward presentation. Chest rewards are automatic slot-machine payouts,
/// not another copy of the normal level-up card choice.
/// </summary>
public class TreasureSlotMachineUI : MonoBehaviour
{
    private static TreasureSlotMachineUI instance;

    private CanvasGroup canvasGroup;
    private RectTransform panel;
    private RectTransform leverKnob;
    private RectTransform tooltipPanel;
    private Image jackpotGlow;
    private Image resultPlate;
    private Image[] bulbImages;
    private RectTransform[] reelRollers;
    private RectTransform[] reelCenterCardRoots;
    private ReelCardView[][] reelCards;
    private int[] reelVisibleSlots;
    private Image[] reelBacks;
    private Image[] reelIcons;
    private TextMeshProUGUI[] reelCenterTexts;
    private TextMeshProUGUI[] reelLabels;
    private TextMeshProUGUI[] reelSubLabels;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI resultText;
    private TextMeshProUGUI footerText;
    private TextMeshProUGUI tooltipText;
    private TextMeshProUGUI confirmText;
    private TextMeshProUGUI rerollText;
    private Button confirmButton;
    private Button rerollButton;
    private Animator leverAnimator;
    private AudioSource slotLoopSource;
    private string[] reelHoverDetails = new string[3];
    private string defaultTitleText = "南瓜宝箱";
    private bool confirmRequested;
    private bool rerollRequested;
    private const string LeverPullTrigger = "Pull";

    private Sprite roundedSprite;
    private Sprite circleSprite;
    private Sprite chestMachineSprite;
    private Sprite chestCardSprite;
    private Sprite nothingSprite;

    [Header("Treasure Slot Prefabs")]
    public GameObject evolutionReelCardPrefab;

    [Header("Treasure Slot Audio")]
    public AudioClip leverPullSound;
    public AudioClip reelLoopSound;
    public AudioClip reelTickSound;
    public AudioClip reelStopSound;
    public AudioClip rewardRevealSound;
    public AudioClip jackpotSound;
    public AudioClip confirmSound;
    public AudioClip rerollSound;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    [Range(0f, 1f)] public float reelLoopVolume = 0.42f;

    private const string ChestMachineAssetPath = "Assets/_TheFirst/Art/Textures/UI/Chest.png";
    private const string ChestCardAssetPath = "Assets/_TheFirst/Art/Textures/UI/Chest_Card.png";
    private const string NothingAssetPath = "Assets/_TheFirst/Art/Textures/UI/Nothing.png";
    private const string DefaultSlotMachinePrefabPath = "Assets/_TheFirst/Prefabs/UI/TreasureSlotMachineUI.prefab";
    private const string DefaultSlotMachineResourcesPath = "UI/TreasureSlotMachineUI";
    private const string EvolutionReelCardResourcesPath = "UI/EvolutionReelCard";
    private const string ChestMachineResourcesPath = "UI/Chest";
    private const string ChestCardResourcesPath = "UI/Chest_Card";
    private const string NothingResourcesPath = "UI/Nothing";
    private const string TreasureSfxResourcesPath = "Audio/SFX/TreasureSlot/";
    private const float PanelWidth = 1500f;
    private const float PanelHeight = PanelWidth * 941f / 1672f;
    private const float LeverX = 620f;
    private const float LeverTopY = 150f;
    private const float LeverBottomY = -72f;
    private const int ReelCardSlotCount = 5;
    private const int ReelCardCenterSlot = 2;
    private const float ReelCardWidth = 190f;
    private const float ReelCardHeight = ReelCardWidth * 342f / 212f;
    private const float ReelCardStep = ReelCardHeight + 24f;

    public UpgradeManager.TreasureSlotReward CurrentReward { get; private set; }

    private readonly Color dimColor = new Color(0f, 0f, 0f, 0.64f);
    private readonly Color shadowColor = new Color(0f, 0f, 0f, 0.55f);
    private readonly Color goldColor = new Color(1f, 0.76f, 0.24f, 1f);
    private readonly Color goldHotColor = new Color(1f, 0.93f, 0.42f, 1f);
    private readonly Color creamColor = new Color(1f, 0.91f, 0.72f, 1f);
    private readonly Color mutedColor = new Color(0.78f, 0.62f, 0.44f, 1f);
    private readonly Color tealColor = new Color(0.18f, 0.75f, 0.70f, 1f);
    private readonly Color redColor = new Color(0.92f, 0.24f, 0.18f, 1f);
    private readonly Color reelCardBaseColor = Color.white;
    private readonly Color reelCardHotColor = new Color(1f, 0.91f, 0.62f, 1f);

    public static TreasureSlotMachineUI GetOrCreate()
    {
        if (instance != null) return instance;

        TreasureSlotMachineUI prefab = ResolvePrefab();
        if (prefab != null)
        {
            instance = Instantiate(prefab);
            instance.name = "TreasureSlotMachineUI_Runtime";
            DontDestroyOnLoad(instance.gameObject);
            if (instance.TryBindExistingLayout())
            {
                return instance;
            }

            Destroy(instance.gameObject);
            instance = null;
        }

        GameObject root = new GameObject("TreasureSlotMachineUI_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<TreasureSlotMachineUI>();
        instance.Build();
        return instance;
    }

    private static TreasureSlotMachineUI ResolvePrefab()
    {
        TreasureSlotMachineUI resourcesPrefab = Resources.Load<TreasureSlotMachineUI>(DefaultSlotMachineResourcesPath);
        if (resourcesPrefab != null)
        {
            return resourcesPrefab;
        }

#if UNITY_EDITOR
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSlotMachinePrefabPath);
        if (prefabObject != null && prefabObject.TryGetComponent(out TreasureSlotMachineUI prefab))
        {
            return prefab;
        }
#endif

        return null;
    }

    private void Build()
    {
        InitializeCoreComponents();

        Image dim = CreateImage("Dim", transform, Vector2.zero, Vector2.zero, dimColor, false);
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        panel = CreateRect("SlotMachinePanel", transform, Vector2.zero, new Vector2(PanelWidth, PanelHeight));
        CreateImage("Shadow", panel, new Vector2(8f, -24f), new Vector2(PanelWidth + 36f, PanelHeight + 28f), shadowColor, true);
        Image chestArt = CreateImage("ChestMachineArt", panel, Vector2.zero, new Vector2(PanelWidth, PanelHeight), Color.white, false, chestMachineSprite);
        chestArt.preserveAspect = true;

        jackpotGlow = null;
        titleText = CreateText("Title", panel, new Vector2(0f, 304f), new Vector2(640f, 52f), 46f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center);
        titleText.text = defaultTitleText;
        defaultTitleText = titleText.text;
        subtitleText = CreateText("Subtitle", panel, new Vector2(0f, 258f), new Vector2(640f, 30f), 21f, FontStyles.Bold, mutedColor, TextAlignmentOptions.Center);
        subtitleText.text = "拉杆启动 · 锁定奖励";

        BuildBulbs();
        CreateText("Hint", panel, new Vector2(0f, -362f), new Vector2(780f, 30f), 18f, FontStyles.Bold, mutedColor, TextAlignmentOptions.Center).text = "";
        BuildReels();
        BuildLever();
        BuildActionButtons();
        BuildResultPlate();
        BuildTooltip();

        HideImmediate();
    }

    private void InitializeCoreComponents()
    {
        roundedSprite = CreateRoundedSprite(64, 16f, new Vector4(18f, 18f, 18f, 18f));
        circleSprite = CreateRoundedSprite(64, 31f, Vector4.zero);
        chestMachineSprite = LoadUiSprite(ChestMachineResourcesPath, ChestMachineAssetPath);
        chestCardSprite = LoadUiSprite(ChestCardResourcesPath, ChestCardAssetPath);
        nothingSprite = LoadUiSprite(NothingResourcesPath, NothingAssetPath);
        if (evolutionReelCardPrefab == null)
        {
            evolutionReelCardPrefab = Resources.Load<GameObject>(EvolutionReelCardResourcesPath);
        }
        ResolveDefaultAudioClips();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        slotLoopSource = GetComponent<AudioSource>();
        if (slotLoopSource == null) slotLoopSource = gameObject.AddComponent<AudioSource>();
        slotLoopSource.playOnAwake = false;
        slotLoopSource.loop = true;
        slotLoopSource.spatialBlend = 0f;
    }

    private void BuildBulbs()
    {
        bulbImages = new Image[17];
        for (int i = 0; i < bulbImages.Length; i++)
        {
            float x = -430f + i * 54f;
            Color color = i % 2 == 0 ? goldHotColor : redColor;
            color.a = 0.45f;
            bulbImages[i] = CreateImage("Bulb_" + i, panel, new Vector2(x, 126f), new Vector2(20f, 20f), color, true, circleSprite);
        }
    }

    private float GetReelX(int index)
    {
        return -300f + index * 300f;
    }

    private void BuildReels()
    {
        reelRollers = new RectTransform[3];
        reelCenterCardRoots = new RectTransform[3];
        reelCards = new ReelCardView[3][];
        reelVisibleSlots = new int[3];
        reelBacks = new Image[3];
        reelIcons = new Image[3];
        reelCenterTexts = new TextMeshProUGUI[3];
        reelLabels = new TextMeshProUGUI[3];
        reelSubLabels = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            RectTransform reel = CreateRect("Reel_" + (i + 1), panel, new Vector2(GetReelX(i), -100f), new Vector2(ReelCardWidth + 18f, ReelCardHeight + 44f));
            reel.gameObject.AddComponent<RectMask2D>();

            RectTransform roller = CreateRect("CardRoller", reel, Vector2.zero, new Vector2(ReelCardWidth, ReelCardStep * ReelCardSlotCount));
            reelRollers[i] = roller;
            reelCards[i] = new ReelCardView[ReelCardSlotCount];

            for (int slot = 0; slot < ReelCardSlotCount; slot++)
            {
                float y = (slot - ReelCardCenterSlot) * ReelCardStep;
                reelCards[i][slot] = CreateReelCard(roller, "Card_" + slot, new Vector2(0f, y));
            }

            ReelCardView centerCard = reelCards[i][ReelCardCenterSlot];
            reelCenterCardRoots[i] = centerCard.root;
            reelBacks[i] = centerCard.back;
            reelIcons[i] = centerCard.icon;
            reelCenterTexts[i] = centerCard.centerText;
            reelLabels[i] = centerCard.label;
            reelSubLabels[i] = centerCard.subLabel;
            reelVisibleSlots[i] = ReelCardCenterSlot;

            Image hoverHit = CreateImage("HoverHit", reel, Vector2.zero, new Vector2(ReelCardWidth + 18f, ReelCardHeight + 44f), new Color(1f, 1f, 1f, 0f), false);
            hoverHit.raycastTarget = true;
            hoverHit.transform.SetAsLastSibling();
            hoverHit.gameObject.AddComponent<ReelHoverArea>().Initialize(this, i);
        }
    }

    private ReelCardView CreateReelCard(Transform parent, string objectName, Vector2 anchoredPosition)
    {
        RectTransform root = CreateRect(objectName, parent, anchoredPosition, new Vector2(ReelCardWidth, ReelCardHeight));
        Image back = CreateImage("CardArt", root, Vector2.zero, new Vector2(ReelCardWidth, ReelCardHeight), reelCardBaseColor, false, chestCardSprite);
        back.preserveAspect = false;

        Image icon = CreateImage("Icon", root, new Vector2(0f, 30f), new Vector2(76f, 76f), Color.white, false);
        icon.preserveAspect = true;

        TextMeshProUGUI centerText = CreateText("CenterText", root, new Vector2(0f, 30f), new Vector2(88f, 78f), 34f, FontStyles.Bold, creamColor, TextAlignmentOptions.Center);
        TextMeshProUGUI label = CreateText("Label", root, new Vector2(0f, -68f), new Vector2(142f, 32f), 25f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center);
        TextMeshProUGUI subLabel = CreateText("SubLabel", root, new Vector2(0f, -104f), new Vector2(154f, 26f), 16f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        return new ReelCardView
        {
            root = root,
            back = back,
            icon = icon,
            centerText = centerText,
            label = label,
            subLabel = subLabel
        };
    }

    private void BuildLever()
    {
        leverKnob = CreateImage("LeverKnobProxy", panel, new Vector2(LeverX, LeverTopY), new Vector2(72f, 72f), new Color(1f, 1f, 1f, 0f), true, circleSprite).rectTransform;

        Image hit = CreateImage("LeverHit", panel, new Vector2(612f, -4f), new Vector2(170f, 470f), new Color(1f, 1f, 1f, 0f), false);
        hit.raycastTarget = true;
    }

    private void BuildActionButtons()
    {
        confirmButton = CreateActionButton("ConfirmButton", panel, new Vector2(-120f, -378f), new Vector2(210f, 54f), "确认", out confirmText);
        rerollButton = CreateActionButton("RerollButton", panel, new Vector2(120f, -378f), new Vector2(210f, 54f), "金币重置", out rerollText);
        BindButtonEvents();
        SetConfirmEnabled(false);
        SetRerollEnabled(false, 0);
    }

    private Button CreateActionButton(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, out TextMeshProUGUI labelText)
    {
        Image background = CreateImage(objectName, parent, anchoredPosition, size, new Color(0.28f, 0.12f, 0.035f, 0.96f), true);
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.02f, 0.82f, 1f);
        colors.pressedColor = new Color(0.86f, 0.62f, 0.28f, 1f);
        colors.disabledColor = new Color(0.44f, 0.38f, 0.30f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        labelText = CreateText("Label", background.transform, Vector2.zero, new Vector2(size.x - 24f, size.y - 10f), 24f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center);
        labelText.text = label;
        return button;
    }

    private void BuildResultPlate()
    {
        resultPlate = CreateImage("ResultPlate", panel, new Vector2(0f, -292f), new Vector2(760f, 58f), new Color(0.16f, 0.04f, 0.035f, 0.16f), true);
        resultText = CreateText("ResultText", panel, new Vector2(0f, -292f), new Vector2(720f, 44f), 28f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center);
        footerText = CreateText("Footer", panel, new Vector2(0f, -344f), new Vector2(760f, 28f), 20f, FontStyles.Bold, mutedColor, TextAlignmentOptions.Center);
        footerText.text = "开箱  →  转轮加速  →  逐格停下  →  奖励爆发";
    }

    private void BuildTooltip()
    {
        tooltipPanel = CreateRect("HoverDetail", panel, new Vector2(0f, -330f), new Vector2(400f, 116f));
        CreateImage("TooltipShadow", tooltipPanel, new Vector2(5f, -7f), new Vector2(412f, 120f), shadowColor, true);
        CreateImage("TooltipFrame", tooltipPanel, Vector2.zero, new Vector2(400f, 116f), tealColor, true);
        CreateImage("TooltipPlate", tooltipPanel, Vector2.zero, new Vector2(386f, 102f), new Color(0.07f, 0.035f, 0.022f, 0.98f), true);
        tooltipText = CreateText("TooltipText", tooltipPanel, Vector2.zero, new Vector2(354f, 82f), 18f, FontStyles.Bold, creamColor, TextAlignmentOptions.Center);
        SetObjectActive(tooltipPanel, false);
    }

    private bool TryBindExistingLayout()
    {
        InitializeCoreComponents();

        panel = FindRect(transform, "SlotMachinePanel");
        if (panel == null)
        {
            Debug.LogWarning("TreasureSlotMachineUI prefab is missing SlotMachinePanel. Falling back to generated layout.");
            return false;
        }

        jackpotGlow = FindImage(panel, "JackpotGlow");
        SetObjectActive(jackpotGlow, false);
        jackpotGlow = null;
        resultPlate = FindImage(panel, "ResultPlate");
        titleText = FindText(panel, "Title");
        subtitleText = FindText(panel, "Subtitle");
        resultText = FindText(panel, "ResultText");
        footerText = FindText(panel, "Footer");
        defaultTitleText = titleText != null && !string.IsNullOrEmpty(titleText.text) ? titleText.text : "南瓜宝箱";
        tooltipPanel = FindRect(panel, "HoverDetail");
        tooltipText = tooltipPanel != null ? FindText(tooltipPanel, "TooltipText") : null;
        leverKnob = FindRect(panel, "LeverKnobProxy");
        leverAnimator = ResolveLeverAnimator();
        confirmButton = FindButton(panel, "ConfirmButton");
        rerollButton = FindButton(panel, "RerollButton");
        confirmText = confirmButton != null ? FindText(confirmButton.transform, "Label") : null;
        rerollText = rerollButton != null ? FindText(rerollButton.transform, "Label") : null;

        BindBulbsFromHierarchy();
        if (!BindReelsFromHierarchy())
        {
            Debug.LogWarning("TreasureSlotMachineUI prefab reel hierarchy is incomplete. Falling back to generated layout.");
            return false;
        }

        BindButtonEvents();
        HideImmediate();
        return true;
    }

    private void BindBulbsFromHierarchy()
    {
        bulbImages = new Image[17];
        for (int i = 0; i < bulbImages.Length; i++)
        {
            bulbImages[i] = FindImage(panel, "Bulb_" + i);
        }
    }

    private bool BindReelsFromHierarchy()
    {
        reelRollers = new RectTransform[3];
        reelCenterCardRoots = new RectTransform[3];
        reelCards = new ReelCardView[3][];
        reelVisibleSlots = new int[3];
        reelBacks = new Image[3];
        reelIcons = new Image[3];
        reelCenterTexts = new TextMeshProUGUI[3];
        reelLabels = new TextMeshProUGUI[3];
        reelSubLabels = new TextMeshProUGUI[3];

        for (int reelIndex = 0; reelIndex < 3; reelIndex++)
        {
            RectTransform reel = FindRect(panel, "Reel_" + (reelIndex + 1));
            if (reel == null) return false;

            RectTransform roller = FindRect(reel, "CardRoller");
            if (roller == null) return false;

            reelRollers[reelIndex] = roller;
            reelCards[reelIndex] = new ReelCardView[ReelCardSlotCount];

            for (int slot = 0; slot < ReelCardSlotCount; slot++)
            {
                RectTransform cardRoot = FindRect(roller, "Card_" + slot);
                if (cardRoot == null) return false;

                ReelCardView cardView = new ReelCardView
                {
                    root = cardRoot,
                    back = FindImage(cardRoot, "CardArt"),
                    icon = FindImage(cardRoot, "Icon"),
                    centerText = FindText(cardRoot, "CenterText"),
                    label = FindText(cardRoot, "Label"),
                    subLabel = FindText(cardRoot, "SubLabel")
                };

                if (cardView.back == null)
                {
                    return false;
                }

                reelCards[reelIndex][slot] = cardView;
            }

            ReelCardView centerCard = reelCards[reelIndex][ReelCardCenterSlot];
            reelCenterCardRoots[reelIndex] = centerCard.root;
            reelBacks[reelIndex] = centerCard.back;
            reelIcons[reelIndex] = centerCard.icon;
            reelCenterTexts[reelIndex] = centerCard.centerText;
            reelLabels[reelIndex] = centerCard.label;
            reelSubLabels[reelIndex] = centerCard.subLabel;
            reelVisibleSlots[reelIndex] = ReelCardCenterSlot;

            Image hoverHit = FindImage(reel, "HoverHit");
            if (hoverHit == null)
            {
                hoverHit = CreateImage("HoverHit", reel, Vector2.zero, reel.sizeDelta, new Color(1f, 1f, 1f, 0f), false);
            }

            hoverHit.raycastTarget = true;
            ReelHoverArea hoverArea = hoverHit.GetComponent<ReelHoverArea>();
            if (hoverArea == null) hoverArea = hoverHit.gameObject.AddComponent<ReelHoverArea>();
            hoverArea.Initialize(this, reelIndex);
            hoverHit.transform.SetAsLastSibling();
        }

        return true;
    }

    private Animator ResolveLeverAnimator()
    {
        RectTransform handle = FindRect(panel, "LeverHandle");
        if (handle != null && handle.TryGetComponent(out Animator handleAnimator))
        {
            return handleAnimator;
        }

        if (leverKnob != null && leverKnob.TryGetComponent(out Animator knobAnimator))
        {
            return knobAnimator;
        }

        return null;
    }

    private void BindButtonEvents()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                PlaySfx(confirmSound, 0.78f);
                confirmRequested = true;
            });
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() =>
            {
                PlaySfx(rerollSound, 0.88f);
                rerollRequested = true;
            });
        }
    }

    private RectTransform FindRect(Transform root, string objectName)
    {
        Transform child = FindDeepChild(root, objectName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private Image FindImage(Transform root, string objectName)
    {
        Transform child = FindDeepChild(root, objectName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TextMeshProUGUI FindText(Transform root, string objectName)
    {
        Transform child = FindDeepChild(root, objectName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(Transform root, string objectName)
    {
        Transform child = FindDeepChild(root, objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private Image CreateImage(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color, bool rounded, Sprite customSprite = null)
    {
        RectTransform rect = CreateRect(objectName, parent, anchoredPosition, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.sprite = customSprite != null ? customSprite : rounded ? roundedSprite : null;
        image.type = rounded && customSprite == null ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    private Sprite LoadUiSprite(string resourcesPath, string assetPath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D resourcesTexture = Resources.Load<Texture2D>(resourcesPath);
        if (resourcesTexture != null)
        {
            return CreateSpriteFromTexture(resourcesTexture);
        }

#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            Debug.LogWarning($"TreasureSlotMachineUI could not load UI texture at {assetPath}");
            return null;
        }

        return CreateSpriteFromTexture(texture);
#else
        return null;
#endif
    }

    private Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void ResolveDefaultAudioClips()
    {
        leverPullSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_lever_pull");
        reelLoopSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_reel_loop");
        reelTickSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_reel_tick");
        reelStopSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_reel_stop");
        rewardRevealSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_reward_reveal");
        jackpotSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_jackpot");
        confirmSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_button_confirm");
        rerollSound ??= Resources.Load<AudioClip>(TreasureSfxResourcesPath + "slot_button_reroll");
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, anchoredPosition, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(12f, fontSize * 0.62f);
        text.enableAutoSizing = true;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private Sprite CreateRoundedSprite(int size, float radius, Vector4 border)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Min(x, size - 1 - x);
                float py = Mathf.Min(y, size - 1 - y);
                bool inside = true;
                if (px < radius && py < radius)
                {
                    float dx = radius - px;
                    float dy = radius - py;
                    inside = dx * dx + dy * dy <= radius * radius;
                }
                texture.SetPixel(x, y, inside ? solid : clear);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public IEnumerator Play(
        UpgradeManager.TreasureSlotReward reward,
        Func<int, UpgradeManager.TreasureSlotReward> rollAgain,
        Func<int, bool> trySpendReroll,
        Func<int, int> getRerollCost)
    {
        CurrentReward = reward;
        gameObject.SetActive(true);
        confirmRequested = false;
        rerollRequested = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        canvasGroup.alpha = 0f;
        panel.localScale = Vector3.one * 0.55f;
        panel.localRotation = Quaternion.Euler(0f, 0f, -5f);
        SetObjectActive(jackpotGlow, false);
        SetObjectActive(tooltipPanel, false);
        SetImageColor(resultPlate, new Color(0.16f, 0.04f, 0.035f, 0.16f));
        SetText(titleText, defaultTitleText);
        SetText(subtitleText, "");
        SetText(resultText, "");
        SetTextColor(footerText, mutedColor);
        SetText(footerText, "");
        SetAnchoredPosition(leverKnob, new Vector2(LeverX, LeverTopY));
        PlayLeverPullAnimation();
        SetConfirmEnabled(false);
        SetRerollEnabled(false, 0);

        for (int i = 0; i < 3; i++)
        {
            SetReel(i, null, "?", "等待开奖", mutedColor, "?");
            SetReelDetail(i, "");
        }

        float pop = 0f;
        while (pop < 0.38f)
        {
            pop += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(pop / 0.38f);
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.4f));
            float scale = Mathf.LerpUnclamped(0.55f, 1.08f, EaseOutBack(t));
            float wobble = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t) * 5f;
            panel.localScale = Vector3.one * scale;
            panel.localRotation = Quaternion.Euler(0f, 0f, wobble);
            SetAnchoredPosition(leverKnob, new Vector2(LeverX, Mathf.Lerp(LeverTopY, LeverBottomY, Mathf.SmoothStep(0f, 1f, t))));
            UpdateBulbs(Mathf.FloorToInt(t * 22f));
            yield return null;
        }

        float settle = 0f;
        while (settle < 0.12f)
        {
            settle += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(settle / 0.12f);
            panel.localScale = Vector3.Lerp(Vector3.one * 1.08f, Vector3.one, Mathf.SmoothStep(0f, 1f, t));
            panel.localRotation = Quaternion.identity;
            yield return null;
        }

        yield return Spin(CurrentReward);
        yield return RevealReward(CurrentReward);

        int rerollCount = 0;
        while (true)
        {
            int nextRerollIndex = rerollCount + 1;
            int cost = getRerollCost != null ? getRerollCost(nextRerollIndex) : 0;
            SetConfirmEnabled(true);
            SetRerollEnabled(rollAgain != null && trySpendReroll != null, cost);

            yield return WaitForPlayerDecision();
            SetConfirmEnabled(false);
            bool reroll = rerollRequested;
            bool confirmed = confirmRequested;
            rerollRequested = false;
            confirmRequested = false;

            if (confirmed || !reroll)
            {
                SetRerollEnabled(false, 0);
                yield break;
            }

            if (trySpendReroll == null || !trySpendReroll(nextRerollIndex))
            {
                SetText(footerText, $"金币不足，无法重拉（需要 {cost}） · 点击空白处继续");
                SetTextColor(footerText, redColor);
                continue;
            }

            rerollCount++;
            SetObjectActive(tooltipPanel, false);
            SetRerollEnabled(false, 0);
            SetText(subtitleText, "");
            SetText(resultText, "");
            CurrentReward = rollAgain(rerollCount);
            yield return PullLeverAgain();
            yield return Spin(CurrentReward);
            yield return RevealReward(CurrentReward);
        }
    }

    private IEnumerator Spin(UpgradeManager.TreasureSlotReward reward)
    {
        FillSpinCards(reward);
        StartReelLoop();

        float elapsed = 0f;
        const float duration = 1.55f;
        float[] offsets = new float[3];
        int[] lastCycles = { -1, -1, -1 };
        while (elapsed < duration)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float speed = Mathf.Lerp(5600f, 720f, EaseOutQuint(t));

            for (int i = 0; i < 3; i++)
            {
                offsets[i] += speed * (1f + i * 0.08f) * deltaTime;
                int cycle = Mathf.FloorToInt(offsets[i] / ReelCardStep);
                if (cycle != lastCycles[i])
                {
                    RefreshSpinCards(i, reward);
                    lastCycles[i] = cycle;
                    if ((cycle + i) % 2 == 0)
                    {
                        PlaySfx(reelTickSound, 0.22f);
                    }
                }

                float offset = Mathf.Repeat(offsets[i], ReelCardStep);
                reelRollers[i].anchoredPosition = new Vector2(0f, -offset);
                SetReelTint(i, Color.Lerp(reelCardBaseColor, reelCardHotColor, Mathf.PingPong((elapsed * 5f) + i * 0.35f, 1f) * 0.22f));
            }

            SetAnchoredPosition(leverKnob, new Vector2(LeverX, Mathf.Lerp(LeverBottomY, LeverTopY, t)));
            SetReelLoopPitch(Mathf.Lerp(1.28f, 0.86f, t));
            UpdateBulbs(Mathf.FloorToInt(elapsed * 42f));
            yield return null;
        }

        yield return StopReelsOnFinalCards(reward);
        StopReelLoop();
    }

    private void FillSpinCards(UpgradeManager.TreasureSlotReward reward)
    {
        for (int reel = 0; reel < 3; reel++)
        {
            reelRollers[reel].anchoredPosition = Vector2.zero;
            SetVisibleReelSlot(reel, ReelCardCenterSlot);
            RefreshSpinCards(reel, reward);
            SetReelTint(reel, reelCardBaseColor);
            reelCenterCardRoots[reel].localScale = Vector3.one;
        }
    }

    private void RefreshSpinCards(int reel, UpgradeManager.TreasureSlotReward reward)
    {
        for (int slot = 0; slot < ReelCardSlotCount; slot++)
        {
            SetRandomSpinCard(reel, slot, reward);
        }
    }

    private void SetRandomSpinCard(int reel, int slot, UpgradeManager.TreasureSlotReward reward)
    {
        string[] labels = { "+1", "+2", "+3", "进化", "强化", "大奖" };
        string[] subs = { "", "", "", "", "", "" };
        string[] centers = { "+", "×2", "×3", "进", "强", "!" };

        int index = UnityEngine.Random.Range(0, labels.Length);
        Sprite spinIcon = GetRandomSpinIcon(reward);
        Color color = UnityEngine.Random.value > 0.4f ? goldHotColor : creamColor;
        SetReelCard(reel, slot, spinIcon, labels[index], subs[index], color, centers[index]);
    }

    private Sprite GetRandomSpinIcon(UpgradeManager.TreasureSlotReward reward)
    {
        if (reward.reelIcons != null)
        {
            Sprite[] icons = reward.reelIcons.Where(icon => icon != null).ToArray();
            if (icons.Length > 0) return icons[UnityEngine.Random.Range(0, icons.Length)];
        }

        if (reward.icon != null) return reward.icon;
        return nothingSprite;
    }

    private IEnumerator StopReelsOnFinalCards(UpgradeManager.TreasureSlotReward reward)
    {
        float[] delays = { 0f, 0.15f, 0.30f };
        float[] durations = { 0.88f, 0.94f, 1.00f };
        float[] offsets = new float[3];
        float[] startY = new float[3];
        float[] targetY = new float[3];
        int[] lastCycles = { -1, -1, -1 };
        bool[] finalStarted = new bool[3];
        bool[] finalStopped = new bool[3];

        for (int i = 0; i < 3; i++)
        {
            offsets[i] = Mathf.Abs(reelRollers[i].anchoredPosition.y);
        }

        float elapsed = 0f;
        float totalDuration = delays[2] + durations[2];
        while (elapsed < totalDuration)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;

            for (int i = 0; i < 3; i++)
            {
                if (!finalStarted[i])
                {
                    if (elapsed < delays[i])
                    {
                        offsets[i] += 900f * (1f + i * 0.06f) * deltaTime;
                        int cycle = Mathf.FloorToInt(offsets[i] / ReelCardStep);
                        if (cycle != lastCycles[i])
                        {
                            RefreshSpinCards(i, reward);
                            lastCycles[i] = cycle;
                            if ((cycle + i) % 2 == 0)
                            {
                                PlaySfx(reelTickSound, 0.18f);
                            }
                        }

                        reelRollers[i].anchoredPosition = new Vector2(0f, -Mathf.Repeat(offsets[i], ReelCardStep));
                        SetReelTint(i, Color.Lerp(reelCardBaseColor, reelCardHotColor, 0.16f));
                        continue;
                    }

                    RefreshSpinCards(i, reward);
                    startY[i] = reelRollers[i].anchoredPosition.y;
                    int targetSlot = startY[i] > -ReelCardStep * 0.58f ? ReelCardCenterSlot + 1 : ReelCardCenterSlot + 2;
                    targetY[i] = -(targetSlot - ReelCardCenterSlot) * ReelCardStep;
                    SetFinalReel(i, reward, targetSlot);
                    SetVisibleReelSlot(i, targetSlot);
                    finalStarted[i] = true;
                }

                if (finalStopped[i]) continue;

                float localT = Mathf.Clamp01((elapsed - delays[i]) / durations[i]);
                float y = Mathf.Lerp(startY[i], targetY[i], EaseOutQuad(localT));
                reelRollers[i].anchoredPosition = new Vector2(0f, y);
                SetReelTint(i, Color.Lerp(reelCardHotColor, reelCardBaseColor, localT * 0.65f));

                if (localT >= 1f)
                {
                    reelRollers[i].anchoredPosition = new Vector2(0f, targetY[i]);
                    SetReelTint(i, reelCardBaseColor);
                    finalStopped[i] = true;
                    PlaySfx(reelStopSound, 0.82f);
                }
            }

            UpdateBulbs(80 + Mathf.FloorToInt(elapsed * 26f));
            yield return null;
        }

        for (int i = 0; i < 3; i++)
        {
            reelRollers[i].anchoredPosition = new Vector2(0f, targetY[i]);
            SetReelTint(i, reelCardBaseColor);
            if (reelCenterCardRoots[i] != null)
            {
                reelCenterCardRoots[i].localScale = Vector3.one * 1.06f;
            }
        }

        yield return new WaitForSecondsRealtime(0.05f);

        for (int i = 0; i < 3; i++)
        {
            if (reelCenterCardRoots[i] != null)
            {
                reelCenterCardRoots[i].localScale = Vector3.one;
            }
        }
    }

    private void SetReelTint(int reel, Color color)
    {
        if (reelCards == null || reel < 0 || reel >= reelCards.Length || reelCards[reel] == null) return;

        for (int slot = 0; slot < reelCards[reel].Length; slot++)
        {
            reelCards[reel][slot].back.color = color;
        }
    }

    private void SetVisibleReelSlot(int reel, int slot)
    {
        if (reelVisibleSlots == null || reel < 0 || reel >= reelVisibleSlots.Length) return;

        int safeSlot = Mathf.Clamp(slot, 0, ReelCardSlotCount - 1);
        reelVisibleSlots[reel] = safeSlot;
        ReelCardView card = GetReelCard(reel, safeSlot);
        if (card == null) return;

        reelCenterCardRoots[reel] = card.root;
        reelBacks[reel] = card.back;
        reelIcons[reel] = card.icon;
        reelCenterTexts[reel] = card.centerText;
        reelLabels[reel] = card.label;
        reelSubLabels[reel] = card.subLabel;
    }

    private int GetVisibleReelSlot(int reel)
    {
        if (reelVisibleSlots == null || reel < 0 || reel >= reelVisibleSlots.Length)
        {
            return ReelCardCenterSlot;
        }

        return Mathf.Clamp(reelVisibleSlots[reel], 0, ReelCardSlotCount - 1);
    }

    private ReelCardView GetReelCard(int reel, int slot)
    {
        if (reelCards == null || reel < 0 || reel >= reelCards.Length || reelCards[reel] == null) return null;
        if (slot < 0 || slot >= reelCards[reel].Length) return null;
        return reelCards[reel][slot];
    }

    private IEnumerator RevealReward(UpgradeManager.TreasureSlotReward reward)
    {
        PlaySfx(reward.jackpot || reward.evolved ? jackpotSound : rewardRevealSound, reward.jackpot || reward.evolved ? 0.92f : 0.78f);

        for (int i = 0; i < 3; i++)
        {
            bool isEvolutionReel = reward.kind == UpgradeManager.TreasureRewardKind.Evolution
                && i == (reward.evolutionReelIndex >= 0 ? reward.evolutionReelIndex : 1);
            int visibleSlot = GetVisibleReelSlot(i);
            SetFinalReel(i, reward, visibleSlot);
            ReelCardView visibleCard = GetReelCard(i, visibleSlot);
            if (visibleCard != null && visibleCard.back != null)
            {
                visibleCard.back.color = isEvolutionReel ? new Color(1f, 0.86f, 0.45f, 1f) : (reward.jackpot || reward.evolved ? reelCardHotColor : reelCardBaseColor);
            }

            if (visibleCard != null && visibleCard.root != null)
            {
                visibleCard.root.localScale = Vector3.one * (isEvolutionReel ? 1.16f : 1.08f);
                visibleCard.root.localRotation = isEvolutionReel ? Quaternion.Euler(0f, 0f, -3f) : Quaternion.identity;
            }

            UpdateBulbs(100 + i);
            yield return new WaitForSecondsRealtime(0.16f);
            if (visibleCard != null && visibleCard.root != null)
            {
                visibleCard.root.localScale = Vector3.one;
                visibleCard.root.localRotation = Quaternion.identity;
            }
        }

        SetText(titleText, defaultTitleText);
        SetText(subtitleText, "");
        SetText(resultText, "");
        SetTextColor(resultText, reward.jackpot || reward.evolved ? goldHotColor : creamColor);
        SetImageColor(resultPlate, reward.jackpot || reward.evolved ? new Color(0.42f, 0.12f, 0.035f, 0.42f) : new Color(0.16f, 0.04f, 0.035f, 0.16f));
        SetTextColor(footerText, reward.jackpot || reward.evolved ? goldColor : mutedColor);
        SetText(footerText, "鼠标悬停卡片查看详情");

        if (reward.jackpot || reward.evolved)
        {
            for (int step = 0; step < 18; step++)
            {
                float pulse = 1f + Mathf.Sin(step * 0.72f) * 0.045f;
                panel.localScale = Vector3.one * pulse;
                UpdateBulbs(step);
                yield return new WaitForSecondsRealtime(0.035f);
            }
            panel.localScale = Vector3.one;
        }
    }

    private void SetFinalReel(int index, UpgradeManager.TreasureSlotReward reward, int slot = ReelCardCenterSlot)
    {
        string rewardName = GetRewardName(reward, index);
        Sprite reelIcon = GetRewardIcon(reward, index);

        if (IsBaseAttackReel(reward, index))
        {
            SetPumpkinBaseAttackReel(index, slot, reward);
            SetReelDetail(index, GetDetailForReel(reward, index));
            return;
        }

        if (reward.kind == UpgradeManager.TreasureRewardKind.Evolution)
        {
            int evolutionIndex = reward.evolutionReelIndex >= 0 ? reward.evolutionReelIndex : 1;
            bool hasSideReward = reward.awardedNodes != null
                && index >= 0
                && index < reward.awardedNodes.Length
                && reward.awardedNodes[index] != null;
            if (!hasSideReward && reward.reelLevelWeapons != null && index >= 0 && index < reward.reelLevelWeapons.Length)
            {
                hasSideReward = reward.reelLevelWeapons[index] != null;
            }

            if (index == evolutionIndex)
            {
                SetEvolutionReelCard(index, slot, reelIcon, rewardName);
            }
            else if (hasSideReward)
            {
                SetReelCard(index, slot, reelIcon, "+1", rewardName, goldHotColor, "+");
            }
            else
            {
                SetReelCard(index, slot, nothingSprite, "锁定", "", creamColor, "");
            }
            SetReelDetail(index, GetDetailForReel(reward, index));
            return;
        }

        if (reward.kind == UpgradeManager.TreasureRewardKind.Gold)
        {
            string label = index == 1 ? "空" : "无";
            SetReelCard(index, slot, nothingSprite, label, "无强化目标", mutedColor, "?");
            SetReelDetail(index, GetDetailForReel(reward, index));
            return;
        }

        int gain = Mathf.Clamp(reward.levelGain, 1, 3);
        if (index < gain)
        {
            SetReelCard(index, slot, reelIcon, "+1", rewardName, goldHotColor, "+");
            SetReelDetail(index, GetDetailForReel(reward, index));
            return;
        }

        if (reward.tryEvolutionAfterLevels && index == 2)
        {
            SetReelCard(index, slot, reelIcon, "MAX", "检查进化", goldHotColor, "进");
            SetReelDetail(index, GetDetailForReel(reward, index));
            return;
        }

        SetReelCard(index, slot, nothingSprite, "锁定", "", creamColor, "");
        SetReelDetail(index, GetDetailForReel(reward, index));
    }

    private bool IsBaseAttackReel(UpgradeManager.TreasureSlotReward reward, int index)
    {
        if (reward.kind == UpgradeManager.TreasureRewardKind.BaseAttack) return true;
        return reward.reelBaseAttackBonuses != null
            && index >= 0
            && index < reward.reelBaseAttackBonuses.Length
            && reward.reelBaseAttackBonuses[index];
    }

    private void SetPumpkinBaseAttackReel(int index, int slot, UpgradeManager.TreasureSlotReward reward)
    {
        SetReelCard(index, slot, nothingSprite, "\u653b\u51fb", "+" + FormatBaseAttackReelBonus(reward), goldHotColor, "+");
    }

    private string FormatBaseAttackReelBonus(UpgradeManager.TreasureSlotReward reward)
    {
        float perReel = reward.baseAttackBonusCount > 0
            ? reward.baseAttackBonus / reward.baseAttackBonusCount
            : reward.baseAttackBonus;
        if (perReel <= 0f) perReel = 0.02f;
        return Mathf.RoundToInt(perReel * 100f) + "%";
    }

    private string GetRewardName(UpgradeManager.TreasureSlotReward reward, int index)
    {
        if (reward.reelNames != null && index >= 0 && index < reward.reelNames.Length && !string.IsNullOrEmpty(reward.reelNames[index]))
        {
            return reward.reelNames[index];
        }

        if (reward.targetWeapon != null && !string.IsNullOrEmpty(reward.targetWeapon.weaponName))
        {
            return reward.targetWeapon.weaponName;
        }

        if (reward.evolved) return "武器进化";
        return "宝箱奖励";
    }

    private Sprite GetRewardIcon(UpgradeManager.TreasureSlotReward reward, int index)
    {
        if (reward.reelIcons != null && index >= 0 && index < reward.reelIcons.Length && reward.reelIcons[index] != null)
        {
            return reward.reelIcons[index];
        }

        return reward.icon;
    }

    private string GetDetailForReel(UpgradeManager.TreasureSlotReward reward, int index)
    {
        if (reward.reelDetails != null && index >= 0 && index < reward.reelDetails.Length && !string.IsNullOrEmpty(reward.reelDetails[index]))
        {
            return reward.reelDetails[index];
        }

        return reward.detailText;
    }

    private void SetReelDetail(int index, string detail)
    {
        if (index < 0 || index >= reelHoverDetails.Length) return;
        reelHoverDetails[index] = detail;
    }

    private void SetReel(int index, Sprite icon, string label, string subLabel, Color color, string centerFallback)
    {
        if (index < 0 || index >= reelIcons.Length) return;
        SetReelCard(index, ReelCardCenterSlot, icon, label, subLabel, color, centerFallback);
    }

    private void SetReelCard(int index, int slot, Sprite icon, string label, string subLabel, Color color, string centerFallback)
    {
        if (index < 0 || index >= reelCards.Length || reelCards[index] == null) return;
        if (slot < 0 || slot >= reelCards[index].Length) return;

        ReelCardView view = reelCards[index][slot];
        SetEvolutionOverlay(view, false, null, null, null);
        bool hasIcon = icon != null;
        if (view.icon != null)
        {
            view.icon.sprite = icon;
            view.icon.enabled = hasIcon;
        }

        if (view.centerText != null)
        {
            view.centerText.enabled = !hasIcon;
            view.centerText.text = centerFallback;
            view.centerText.color = color;
        }

        if (view.label != null)
        {
            view.label.text = label;
            view.label.color = color;
        }

        if (view.subLabel != null)
        {
            view.subLabel.text = subLabel;
        }
    }

    private void SetEvolutionReelCard(int index, int slot, Sprite icon, string rewardName)
    {
        SetReelCard(index, slot, icon, "\u8FDB\u5316", rewardName, goldHotColor, "\u2605");

        ReelCardView view = GetReelCard(index, slot);
        if (view == null) return;
        SetEvolutionOverlay(view, true, icon, "\u8FDB\u5316", rewardName);
    }

    private void SetEvolutionOverlay(ReelCardView view, bool active, Sprite icon, string label, string rewardName)
    {
        if (view == null || view.root == null) return;

        Transform existing = view.root.Find("EvolutionReelCard");
        if (!active)
        {
            SetBaseReelCardVisible(view, true);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
            return;
        }

        GameObject overlay = GetOrCreateEvolutionOverlay(view);
        if (overlay == null) return;

        overlay.SetActive(active);
        SetBaseReelCardVisible(view, false);

        Image overlayIcon = FindImage(overlay.transform, "Icon");
        if (overlayIcon != null)
        {
            overlayIcon.sprite = icon;
            overlayIcon.enabled = icon != null;
        }

        TextMeshProUGUI overlayLabel = FindText(overlay.transform, "Label");
        if (overlayLabel != null)
        {
            overlayLabel.text = label ?? string.Empty;
        }

        TextMeshProUGUI overlaySubLabel = FindText(overlay.transform, "SubLabel");
        if (overlaySubLabel != null)
        {
            overlaySubLabel.text = rewardName ?? string.Empty;
        }

        overlay.transform.SetAsLastSibling();
    }

    private void SetBaseReelCardVisible(ReelCardView view, bool visible)
    {
        if (view == null) return;
        if (view.back != null) view.back.enabled = visible;
        if (view.icon != null) view.icon.enabled = visible && view.icon.sprite != null;
        if (view.centerText != null) view.centerText.enabled = visible && !view.icon.enabled;
        if (view.label != null) view.label.enabled = visible;
        if (view.subLabel != null) view.subLabel.enabled = visible;
    }

    private GameObject GetOrCreateEvolutionOverlay(ReelCardView view)
    {
        Transform existing = view.root.Find("EvolutionReelCard");
        if (existing != null) return existing.gameObject;
        if (evolutionReelCardPrefab == null) return null;

        GameObject overlay = Instantiate(evolutionReelCardPrefab, view.root);
        overlay.name = "EvolutionReelCard";

        overlay.SetActive(false);
        return overlay;
    }

    public void ShowTooltip(int index)
    {
        if (index < 0 || index >= reelHoverDetails.Length) return;
        if (string.IsNullOrEmpty(reelHoverDetails[index])) return;
        if (tooltipPanel == null || tooltipText == null) return;

        tooltipText.text = reelHoverDetails[index];
        tooltipPanel.anchoredPosition = new Vector2(GetReelX(index), -330f);
        tooltipPanel.gameObject.SetActive(true);
        tooltipPanel.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.gameObject.SetActive(false);
        }
    }

    private IEnumerator PullLeverAgain()
    {
        PlayLeverPullAnimation();
        for (int i = 0; i < 3; i++)
        {
            SetReel(i, null, "?", "重新开奖", mutedColor, "?");
            SetReelDetail(i, "");
        }

        float time = 0f;
        while (time < 0.22f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.22f);
            SetAnchoredPosition(leverKnob, new Vector2(LeverX, Mathf.Lerp(LeverTopY, LeverBottomY, Mathf.Sin(t * Mathf.PI))));
            panel.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.045f);
            UpdateBulbs(Mathf.FloorToInt(t * 18f));
            yield return null;
        }

        SetAnchoredPosition(leverKnob, new Vector2(LeverX, LeverTopY));
        panel.localScale = Vector3.one;
    }

    private void SetRerollEnabled(bool enabled, int cost)
    {
        if (rerollButton != null)
        {
            rerollButton.interactable = enabled;
            rerollButton.gameObject.SetActive(true);
        }

        if (rerollText != null)
        {
            rerollText.text = enabled ? $"金币重置 {cost}" : "金币重置";
            rerollText.color = enabled ? goldColor : mutedColor;
        }
    }

    private void SetConfirmEnabled(bool enabled)
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = enabled;
            confirmButton.gameObject.SetActive(true);
        }

        if (confirmText != null)
        {
            confirmText.text = "确认";
            confirmText.color = enabled ? goldColor : mutedColor;
        }
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void SetTextColor(TextMeshProUGUI text, Color color)
    {
        if (text != null)
        {
            text.color = color;
        }
    }

    private void SetImageColor(Image image, Color color)
    {
        if (image != null)
        {
            image.color = color;
        }
    }

    private void SetObjectActive(Component component, bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(active);
        }
    }

    private void SetAnchoredPosition(RectTransform rect, Vector2 position)
    {
        if (rect != null)
        {
            rect.anchoredPosition = position;
        }
    }

    private void PlayLeverPullAnimation()
    {
        PlaySfx(leverPullSound, 0.9f);

        if (leverAnimator == null || !HasAnimatorTrigger(leverAnimator, LeverPullTrigger)) return;
        leverAnimator.ResetTrigger(LeverPullTrigger);
        leverAnimator.SetTrigger(LeverPullTrigger);
    }

    private void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;

        float volume = Mathf.Clamp01(sfxVolume * volumeScale);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySoundEffect(clip, volume);
            return;
        }

        if (slotLoopSource != null)
        {
            slotLoopSource.PlayOneShot(clip, volume);
        }
    }

    private void StartReelLoop()
    {
        if (reelLoopSound == null || slotLoopSource == null) return;

        slotLoopSource.clip = reelLoopSound;
        slotLoopSource.loop = true;
        slotLoopSource.spatialBlend = 0f;
        slotLoopSource.volume = Mathf.Clamp01(sfxVolume * reelLoopVolume);
        slotLoopSource.pitch = 1.22f;
        if (!slotLoopSource.isPlaying)
        {
            slotLoopSource.Play();
        }
    }

    private void SetReelLoopPitch(float pitch)
    {
        if (slotLoopSource != null && slotLoopSource.isPlaying && slotLoopSource.clip == reelLoopSound)
        {
            slotLoopSource.pitch = Mathf.Clamp(pitch, 0.65f, 1.35f);
        }
    }

    private void StopReelLoop()
    {
        if (slotLoopSource != null && slotLoopSource.clip == reelLoopSound)
        {
            slotLoopSource.Stop();
            slotLoopSource.clip = null;
            slotLoopSource.pitch = 1f;
        }
    }

    private bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForPlayerDecision()
    {
        yield return new WaitForSecondsRealtime(0.15f);

        while (true)
        {
            if (confirmRequested || rerollRequested)
            {
                yield break;
            }

            if (TryGetPointerDown(out Vector2 screenPosition)
                && !RectTransformUtility.RectangleContainsScreenPoint(panel, screenPosition, null))
            {
                HideTooltip();
                confirmRequested = true;
                yield break;
            }

            yield return null;
        }
    }

    private bool TryGetPointerDown(out Vector2 screenPosition)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private void UpdateBulbs(int step)
    {
        if (bulbImages == null) return;

        for (int i = 0; i < bulbImages.Length; i++)
        {
            Image bulb = bulbImages[i];
            if (bulb == null) continue;

            bool hot = (i + step) % 3 == 0;
            Color color = hot ? goldHotColor : (i + step) % 3 == 1 ? redColor : goldColor;
            color.a = hot ? 0.72f : 0.36f;
            bulb.color = color;
            bulb.transform.localScale = hot ? Vector3.one * 1.18f : Vector3.one;
        }
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutQuint(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t * t * t;
    }

    private float EaseOutQuad(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t;
    }

    public void Hide()
    {
        HideImmediate();
    }

    private void HideImmediate()
    {
        StopReelLoop();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private sealed class ReelHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TreasureSlotMachineUI owner;
        private int index;

        public void Initialize(TreasureSlotMachineUI owner, int index)
        {
            this.owner = owner;
            this.index = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.ShowTooltip(index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HideTooltip();
        }
    }

    private sealed class ReelCardView
    {
        public RectTransform root;
        public Image back;
        public Image icon;
        public TextMeshProUGUI centerText;
        public TextMeshProUGUI label;
        public TextMeshProUGUI subLabel;
    }
}
