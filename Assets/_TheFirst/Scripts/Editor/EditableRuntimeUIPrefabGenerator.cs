using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EditableRuntimeUIPrefabGenerator
{
    private const string UiPrefabFolder = "Assets/_TheFirst/Prefabs/UI";
    private const string UiTextureFolder = "Assets/_TheFirst/Art/Textures/UI";
    private const string RoleHudPrefabPath = UiPrefabFolder + "/RoleMechanicHUD.prefab";
    private const string DifficultyPrefabPath = UiPrefabFolder + "/LevelDifficultySelector.prefab";
    private const string TreasureSlotMachinePrefabPath = UiPrefabFolder + "/TreasureSlotMachineUI.prefab";
    private const string SwordBodyPath = UiTextureFolder + "/SwordFocusGauge_Body.png";
    private const string SwordFramePath = UiTextureFolder + "/SwordFocusGauge_Frame.png";

    [MenuItem("Tools/TheFirst/Regenerate Editable Runtime UI Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/_TheFirst", "Art");
        EnsureFolder("Assets/_TheFirst/Art", "Textures");
        EnsureFolder("Assets/_TheFirst/Art/Textures", "UI");
        EnsureFolder("Assets/_TheFirst", "Prefabs");
        EnsureFolder("Assets/_TheFirst/Prefabs", "UI");

        Sprite bodySprite = GenerateSwordSprite(SwordBodyPath, false);
        Sprite frameSprite = GenerateSwordSprite(SwordFramePath, true);

        GenerateRoleMechanicHudPrefab(bodySprite, frameSprite);
        GenerateDifficultySelectorPrefab();
        GenerateTreasureSlotMachinePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EditableRuntimeUIPrefabGenerator] Generated editable runtime UI prefabs.");
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
