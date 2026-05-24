using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EditableRuntimeUIPrefabGenerator
{
    private const string UiPrefabFolder = "Assets/_TheFirst/Prefabs/UI";
    private const string ResourcesFolder = "Assets/_TheFirst/Resources";
    private const string ResourcesUiFolder = ResourcesFolder + "/UI";
    private const string UiTextureFolder = "Assets/_TheFirst/Art/Textures/UI";
    private const string RoleHudPrefabPath = UiPrefabFolder + "/RoleMechanicHUD.prefab";
    private const string DifficultyPrefabPath = UiPrefabFolder + "/LevelDifficultySelector.prefab";
    private const string TreasureSlotMachinePrefabPath = UiPrefabFolder + "/TreasureSlotMachineUI.prefab";
    private const string CodexBookPrefabPath = ResourcesUiFolder + "/CodexBook.prefab";
    private const string CodexAutoGenerateSessionKey = "TheFirst.EditableRuntimeUIPrefabGenerator.CodexBookChecked";
    private const string SwordBodyPath = UiTextureFolder + "/SwordFocusGauge_Body.png";
    private const string SwordFramePath = UiTextureFolder + "/SwordFocusGauge_Frame.png";

    static EditableRuntimeUIPrefabGenerator()
    {
        EditorApplication.delayCall += EnsureCodexBookPrefabExistsOnce;
    }

    [MenuItem("Tools/TheFirst/Regenerate Editable Runtime UI Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/_TheFirst", "Art");
        EnsureFolder("Assets/_TheFirst/Art", "Textures");
        EnsureFolder("Assets/_TheFirst/Art/Textures", "UI");
        EnsureFolder("Assets/_TheFirst", "Prefabs");
        EnsureFolder("Assets/_TheFirst/Prefabs", "UI");
        EnsureFolder("Assets/_TheFirst", "Resources");
        EnsureFolder("Assets/_TheFirst/Resources", "UI");

        Sprite bodySprite = GenerateSwordSprite(SwordBodyPath, false);
        Sprite frameSprite = GenerateSwordSprite(SwordFramePath, true);

        GenerateRoleMechanicHudPrefab(bodySprite, frameSprite);
        GenerateDifficultySelectorPrefab();
        GenerateTreasureSlotMachinePrefab();
        GenerateCodexBookPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EditableRuntimeUIPrefabGenerator] Generated editable runtime UI prefabs.");
    }

    [MenuItem("Tools/TheFirst/Regenerate CodexBook UI Prefab")]
    public static void GenerateCodexBookPrefab()
    {
        EnsureFolder("Assets/_TheFirst", "Resources");
        EnsureFolder("Assets/_TheFirst/Resources", "UI");

        GameObject root = BuildCodexBookPrefab();
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, CodexBookPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EditableRuntimeUIPrefabGenerator] Generated CodexBook UI prefab.");
    }

    [MenuItem("Tools/TheFirst/Regenerate Treasure Slot Machine UI Prefab")]
    public static void GenerateTreasureSlotMachinePrefab()
    {
        EnsureFolder("Assets/_TheFirst", "Prefabs");
        EnsureFolder("Assets/_TheFirst/Prefabs", "UI");

        GameObject root = new GameObject("TreasureSlotMachineUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(TreasureSlotMachineUI));
        try
        {
            TreasureSlotMachineUI slotMachine = root.GetComponent<TreasureSlotMachineUI>();
            MethodInfo buildMethod = typeof(TreasureSlotMachineUI).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic);
            buildMethod?.Invoke(slotMachine, null);

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "ReelHoverArea")
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            root.SetActive(true);
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, TreasureSlotMachinePrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EditableRuntimeUIPrefabGenerator] Generated treasure slot-machine UI prefab.");
    }

    private static void EnsureCodexBookPrefabExistsOnce()
    {
        if (SessionState.GetBool(CodexAutoGenerateSessionKey, false)) return;
        SessionState.SetBool(CodexAutoGenerateSessionKey, true);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CodexBookPrefabPath) != null) return;
        GenerateCodexBookPrefab();
    }

    private static GameObject BuildCodexBookPrefab()
    {
        GameObject root = CreateUiObject("Runtime_CodexBook", null, typeof(Image), typeof(Outline), typeof(Shadow));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(880f, 820f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.29f, 0.28f, 0.39f, 0.98f);

        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.68f, 0.22f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = root.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(0f, -8f);

        GameObject header = CreateUiObject("Runtime_CodexHeader", root.transform, typeof(Image));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 68f);
        header.GetComponent<Image>().color = new Color(0.50f, 0.06f, 0.12f, 1f);

        TextMeshProUGUI collectionText = AddText("Runtime_CollectionText", header.transform, 34f, TextAlignmentOptions.Center);
        collectionText.text = "Collected: 0 of 0";
        collectionText.color = new Color(0.94f, 0.91f, 0.86f, 1f);
        Stretch(collectionText.rectTransform, 0f, 8f, 110f, 0f);

        Button closeButton = CreateCloseButton(root.transform);
        closeButton.onClick.RemoveAllListeners();

        CreateCodexScroll(root.transform);
        CreateCodexSidebarItemPrefab(root.transform);
        CreateCodexDetailBar(root.transform);

        root.SetActive(true);
        return root;
    }

    private static void CreateCodexScroll(Transform parent)
    {
        GameObject scroll = CreateUiObject("Runtime_CodexScroll", parent, typeof(ScrollRect));
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(30f, 174f);
        scrollRect.offsetMax = new Vector2(-48f, -82f);

        ScrollRect scrollView = scroll.GetComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollView.scrollSensitivity = 36f;

        GameObject viewport = CreateUiObject("Viewport", scroll.transform, typeof(Image), typeof(Mask));
        Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0.12f, 0.11f, 0.17f, 0.58f);
        viewportImage.raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollView.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateUiObject("Content", viewport.transform, typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 560f);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.cellSize = new Vector2(72f, 72f);
        grid.spacing = new Vector2(18f, 16f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.content = contentRect;

        CreateCodexScrollbar(scroll.transform, scrollView);
    }

    private static void CreateCodexScrollbar(Transform parent, ScrollRect scrollView)
    {
        GameObject bar = CreateUiObject("Runtime_CodexScrollbar", parent, typeof(Image), typeof(Scrollbar));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = new Vector2(22f, 0f);
        barRect.sizeDelta = new Vector2(14f, 0f);
        bar.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.24f, 0.95f);

        GameObject slidingArea = CreateUiObject("Sliding Area", bar.transform);
        Stretch(slidingArea.GetComponent<RectTransform>(), 2f, 2f, 4f, 4f);

        GameObject handle = CreateUiObject("Handle", slidingArea.transform, typeof(Image));
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.96f, 0.70f, 0.24f, 1f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Stretch(handleRect, 0f, 0f, 0f, 0f);

        Scrollbar scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollView.verticalScrollbar = scrollbar;
        scrollView.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    private static void CreateCodexSidebarItemPrefab(Transform parent)
    {
        GameObject item = CreateUiObject("Runtime_SidebarItemPrefab", parent, typeof(Image), typeof(LayoutElement), typeof(Button), typeof(SkillTreeSidebarItem));
        item.SetActive(false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(72f, 72f);
        Image itemImage = item.GetComponent<Image>();
        itemImage.color = new Color(1f, 1f, 1f, 0f);

        LayoutElement layout = item.GetComponent<LayoutElement>();
        layout.preferredWidth = 72f;
        layout.preferredHeight = 72f;

        Button button = item.GetComponent<Button>();
        button.targetGraphic = itemImage;

        GameObject cardBg = CreateUiObject("CardBackground", item.transform, typeof(Image), typeof(Outline));
        Image cardImage = cardBg.GetComponent<Image>();
        cardImage.color = new Color(0.04f, 0.035f, 0.035f, 1f);
        cardImage.raycastTarget = false;
        Stretch(cardBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Outline cardOutline = cardBg.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.96f, 0.77f, 0.22f, 1f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        GameObject highlight = CreateUiObject("Highlight", item.transform, typeof(Image));
        Image highlightImage = highlight.GetComponent<Image>();
        highlightImage.color = new Color(1f, 0.78f, 0.18f, 0.34f);
        highlightImage.raycastTarget = false;
        Stretch(highlight.GetComponent<RectTransform>(), -4f, -4f, -4f, -4f);
        highlight.SetActive(false);

        Image icon = AddImage("Icon", item.transform, null, Color.white);
        icon.preserveAspect = true;
        Stretch(icon.rectTransform, 11f, 11f, 11f, 11f);

        GameObject lockOverlay = CreateUiObject("LockOverlay", item.transform, typeof(Image));
        Image lockImage = lockOverlay.GetComponent<Image>();
        lockImage.color = new Color(0f, 0f, 0f, 0.26f);
        lockImage.raycastTarget = false;
        Stretch(lockOverlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        lockOverlay.SetActive(false);

        SkillTreeSidebarItem sidebarItem = item.GetComponent<SkillTreeSidebarItem>();
        sidebarItem.iconImage = icon;
        sidebarItem.backgroundImage = cardImage;
        sidebarItem.highlightImage = highlightImage;
        sidebarItem.selectionHighlight = highlight;
        sidebarItem.lockOverlay = lockOverlay;
    }

    private static void CreateCodexDetailBar(Transform parent)
    {
        GameObject detail = CreateUiObject("Runtime_DetailRoot", parent, typeof(Image), typeof(Outline));
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0f);
        detailRect.pivot = new Vector2(0.5f, 0f);
        detailRect.anchoredPosition = new Vector2(0f, 20f);
        detailRect.sizeDelta = new Vector2(-36f, 136f);

        detail.GetComponent<Image>().color = new Color(0.55f, 0.52f, 0.49f, 1f);
        Outline outline = detail.GetComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.67f, 0.20f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject iconFrame = CreateUiObject("Runtime_DetailIconFrame", detail.transform, typeof(Image), typeof(Outline));
        RectTransform iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0.5f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(70f, 0f);
        iconFrameRect.sizeDelta = new Vector2(78f, 78f);
        iconFrame.GetComponent<Image>().color = new Color(0.04f, 0.035f, 0.035f, 1f);
        Outline iconOutline = iconFrame.GetComponent<Outline>();
        iconOutline.effectColor = new Color(0.96f, 0.78f, 0.22f, 1f);
        iconOutline.effectDistance = new Vector2(2f, -2f);

        Image icon = AddImage("Runtime_DetailIcon", iconFrame.transform, null, Color.white);
        icon.preserveAspect = true;
        Stretch(icon.rectTransform, 12f, 12f, 12f, 12f);

        TextMeshProUGUI title = AddText("Runtime_DetailTitle", detail.transform, "\u5357\u74dc\u56fe\u9274", 24f, TextAlignmentOptions.Left);
        title.color = new Color(1f, 0.83f, 0.28f, 1f);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -34f), new Vector2(-144f, 34f));

        TextMeshProUGUI body = AddText("Runtime_DetailBody", detail.transform, "", 21f, TextAlignmentOptions.Left);
        body.color = new Color(0.96f, 0.94f, 0.90f, 1f);
        SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -70f), new Vector2(-144f, 42f));

        TextMeshProUGUI footer = AddText("Runtime_DetailFooter", detail.transform, "", 18f, TextAlignmentOptions.Left);
        footer.color = new Color(1f, 0.58f, 0.12f, 1f);
        SetRect(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(96f, 28f), new Vector2(-144f, 28f));
    }

    private static Button CreateCloseButton(Transform parent)
    {
        GameObject close = CreateUiObject("Runtime_CloseButton", parent, typeof(Image), typeof(Button));
        RectTransform rect = close.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-34f, -34f);
        rect.sizeDelta = new Vector2(64f, 64f);

        Image image = close.GetComponent<Image>();
        image.color = new Color(0.78f, 0.16f, 0.12f, 1f);
        Button button = close.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = AddText("Label", close.transform, "X", 34f, TextAlignmentOptions.Center);
        label.color = Color.white;
        Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private static void GenerateRoleMechanicHudPrefab(Sprite bodySprite, Sprite frameSprite)
    {
        GameObject root = new GameObject("RoleMechanicHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RoleMechanicHudView));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject hudRoot = CreateUiObject("HUDRoot", root.transform, typeof(Image));
            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 0f);
            hudRect.anchorMax = new Vector2(0.5f, 0f);
            hudRect.pivot = new Vector2(0.5f, 0f);
            hudRect.anchoredPosition = new Vector2(0f, 28f);
            hudRect.sizeDelta = new Vector2(560f, 86f);
            Image hudImage = hudRoot.GetComponent<Image>();
            hudImage.color = new Color(0.08f, 0.045f, 0.025f, 0.58f);
            hudImage.raycastTarget = false;

            GameObject gauge = CreateUiObject("SwordGauge", hudRoot.transform);
            RectTransform gaugeRect = gauge.GetComponent<RectTransform>();
            gaugeRect.anchorMin = new Vector2(0.5f, 0.5f);
            gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
            gaugeRect.pivot = new Vector2(0.5f, 0.5f);
            gaugeRect.anchoredPosition = new Vector2(0f, 0f);
            gaugeRect.sizeDelta = new Vector2(500f, 74f);

            Image backgroundImage = AddImage("Image_Background", gauge.transform, bodySprite, new Color(0.16f, 0.09f, 0.035f, 0.92f));
            Stretch(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);

            Image fillGlowImage = AddImage("Image_FillGlow", gauge.transform, bodySprite, new Color(1f, 0.78f, 0.24f, 0.22f));
            Stretch(fillGlowImage.rectTransform, 3f, -3f, 3f, -3f);
            fillGlowImage.type = Image.Type.Filled;
            fillGlowImage.fillMethod = Image.FillMethod.Horizontal;
            fillGlowImage.fillAmount = 0.55f;

            Image fillImage = AddImage("Image_Fill", gauge.transform, bodySprite, new Color(1f, 0.55f, 0.18f, 1f));
            Stretch(fillImage.rectTransform, 8f, 8f, 8f, 8f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0.55f;

            Image frameImage = AddImage("Image2_Frame", gauge.transform, frameSprite, new Color(1f, 0.79f, 0.31f, 1f));
            Stretch(frameImage.rectTransform, 0f, 0f, 0f, 0f);

            TextMeshProUGUI label = AddText("Label", gauge.transform, 24f, TextAlignmentOptions.Center);
            label.text = "\u5251\u52bf 0/3  30s";
            label.color = new Color(1f, 0.95f, 0.78f, 1f);
            Stretch(label.rectTransform, 58f, 10f, 78f, 10f);

            GameObject pipsRoot = CreateUiObject("StackPips", gauge.transform, typeof(HorizontalLayoutGroup));
            RectTransform pipsRect = pipsRoot.GetComponent<RectTransform>();
            pipsRect.anchorMin = new Vector2(1f, 0.5f);
            pipsRect.anchorMax = new Vector2(1f, 0.5f);
            pipsRect.pivot = new Vector2(1f, 0.5f);
            pipsRect.anchoredPosition = new Vector2(-54f, 0f);
            pipsRect.sizeDelta = new Vector2(78f, 22f);

            HorizontalLayoutGroup pipsLayout = pipsRoot.GetComponent<HorizontalLayoutGroup>();
            pipsLayout.spacing = 8f;
            pipsLayout.childAlignment = TextAnchor.MiddleCenter;
            pipsLayout.childControlWidth = false;
            pipsLayout.childControlHeight = false;
            pipsLayout.childForceExpandWidth = false;
            pipsLayout.childForceExpandHeight = false;

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Image[] pips = new Image[3];
            for (int i = 0; i < pips.Length; i++)
            {
                Image pip = AddImage("Pip_" + (i + 1), pipsRoot.transform, uiSprite, new Color(0.18f, 0.12f, 0.07f, 0.82f));
                RectTransform pipRect = pip.rectTransform;
                pipRect.sizeDelta = new Vector2(18f, 18f);
                LayoutElement layout = pip.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 18f;
                layout.preferredHeight = 18f;
                pips[i] = pip;
            }

            RoleMechanicHudView view = root.GetComponent<RoleMechanicHudView>();
            view.labelText = label;
            view.fillImage = fillImage;
            view.fillGlowImage = fillGlowImage;
            view.stackPipRoot = pipsRoot;
            view.stackPips = pips;

            PrefabUtility.SaveAsPrefabAsset(root, RoleHudPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void GenerateDifficultySelectorPrefab()
    {
        GameObject root = new GameObject("LevelDifficultySelector", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LevelDifficultySelectorView));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -138f);
            rootRect.sizeDelta = new Vector2(430f, 118f);

            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.12f, 0.07f, 0.03f, 0.88f);

            VerticalLayoutGroup vertical = root.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(14, 14, 10, 10);
            vertical.spacing = 8f;
            vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            TextMeshProUGUI hint = AddText("DifficultyHint", root.transform, 18f, TextAlignmentOptions.Center);
            hint.text = "\u9009\u62e9\u96be\u5ea6";
            hint.color = new Color(1f, 0.91f, 0.68f, 1f);

            GameObject row = CreateUiObject("DifficultyButtons", root.transform, typeof(HorizontalLayoutGroup));
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 56f);
            HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;

            Button normalButton = CreateDifficultyButton("NormalDifficultyButton", row.transform, "\u666e\u901a", out TextMeshProUGUI normalText, out Image normalImage, out GameObject normalSelected, out _);
            Button hardButton = CreateDifficultyButton("HardDifficultyButton", row.transform, "\u56f0\u96be", out TextMeshProUGUI hardText, out Image hardImage, out GameObject hardSelected, out GameObject hardLocked);

            LevelDifficultySelectorView view = root.GetComponent<LevelDifficultySelectorView>();
            view.normalButton = normalButton;
            view.hardButton = hardButton;
            view.normalText = normalText;
            view.hardText = hardText;
            view.hintText = hint;
            view.normalBackground = normalImage;
            view.hardBackground = hardImage;
            view.normalSelectedRoot = normalSelected;
            view.hardSelectedRoot = hardSelected;
            view.hardLockedRoot = hardLocked;

            PrefabUtility.SaveAsPrefabAsset(root, DifficultyPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Button CreateDifficultyButton(string name, Transform parent, string labelText, out TextMeshProUGUI label, out Image background, out GameObject selectedRoot, out GameObject lockedRoot)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minHeight = 46f;
        layout.preferredHeight = 46f;

        background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.24f, 0.15f, 0.08f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.03f, 0.88f, 1f);
        colors.pressedColor = new Color(0.84f, 0.7f, 0.42f, 1f);
        colors.disabledColor = new Color(0.45f, 0.43f, 0.4f, 0.72f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        selectedRoot = CreateUiObject("SelectedFrame", buttonObject.transform, typeof(Image));
        Image selectedImage = selectedRoot.GetComponent<Image>();
        selectedImage.color = new Color(1f, 0.75f, 0.2f, 0.36f);
        Stretch(selectedRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        selectedRoot.SetActive(false);

        lockedRoot = CreateUiObject("LockedOverlay", buttonObject.transform, typeof(Image));
        Image lockedImage = lockedRoot.GetComponent<Image>();
        lockedImage.color = new Color(0f, 0f, 0f, 0.42f);
        Stretch(lockedRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        lockedRoot.SetActive(false);

        label = AddText("Label", buttonObject.transform, 22f, TextAlignmentOptions.Center);
        label.text = labelText;
        label.color = Color.white;
        Stretch(label.rectTransform, 8f, 4f, 8f, 4f);

        return button;
    }

    private static Image AddImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image));
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI AddText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI AddText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = AddText(name, parent, fontSize, alignment);
        text.text = value;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        Type[] finalComponents = new Type[components.Length + 1];
        finalComponents[0] = typeof(RectTransform);
        Array.Copy(components, 0, finalComponents, 1, components.Length);
        GameObject go = new GameObject(name, finalComponents);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static Sprite GenerateSwordSprite(string path, bool frameOnly)
    {
        const int width = 512;
        const int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsSwordShape(x, y, width, height);
                bool border = inside && IsSwordBorder(x, y, width, height);
                texture.SetPixel(x, y, inside && (!frameOnly || border) ? white : clear);
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static bool IsSwordBorder(int x, int y, int width, int height)
    {
        const int radius = 3;
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                if (!IsSwordShape(x + ox, y + oy, width, height)) return true;
            }
        }

        return false;
    }

    private static bool IsSwordShape(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;

        float nx = (float)x / (width - 1);
        float ny = (float)y / (height - 1);

        bool handle = nx >= 0.02f && nx <= 0.18f && ny >= 0.41f && ny <= 0.59f;
        bool guard = Mathf.Abs(nx - 0.205f) / 0.045f + Mathf.Abs(ny - 0.5f) / 0.36f <= 1f;
        bool blade = PointInPolygon(new Vector2(nx, ny), new[]
        {
            new Vector2(0.20f, 0.35f),
            new Vector2(0.86f, 0.24f),
            new Vector2(0.995f, 0.50f),
            new Vector2(0.86f, 0.76f),
            new Vector2(0.20f, 0.65f)
        });

        return handle || guard || blade;
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
