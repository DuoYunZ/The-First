using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 地图关卡选择界面 — 手动切图排列方案
///
/// 设计思路：
/// - 底图为灰色版完整地图（代表所有区域锁定的默认状态）
/// - 每个区域单独切成彩色图片，由美术手动摆放在灰色底图上方
/// - 已解锁区域：显示彩色切图 → 遮住灰色底图 → 可点击
/// - 未解锁区域：彩色切图透明 → 露出灰色底图 → 不可点击
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    public static LevelSelectUI Instance { get; private set; }

    [Header("面板引用")]
    [Tooltip("整个关卡选择面板的根对象")]
    public GameObject levelSelectPanel;

    [Tooltip("面板的 CanvasGroup（用于渐变动画）")]
    public CanvasGroup panelCanvasGroup;

    [Header("确认弹窗")]
    [Tooltip("确认面板根对象")]
    public GameObject confirmPanel;
    [Tooltip("确认面板 - 关卡名称")]
    public TextMeshProUGUI confirmLevelName;
    [Tooltip("确认面板 - 关卡描述")]
    public TextMeshProUGUI confirmDescription;
    [Tooltip("确认面板 - 推荐等级")]
    public TextMeshProUGUI confirmRecommendedLevel;
    [Tooltip("确认面板 - 关卡图标")]
    public Image confirmIcon;
    [Tooltip("确认面板 - 出发按钮")]
    public Button confirmStartButton;
    [Tooltip("确认面板 - 取消按钮")]
    public Button confirmCancelButton;

    [Header("难度选择")]
    [Tooltip("难度选择根对象。为空时会在运行时生成一个原型控件。")]
    public GameObject difficultyPanel;
    public Button normalDifficultyButton;
    public Button hardDifficultyButton;
    public TextMeshProUGUI normalDifficultyText;
    public TextMeshProUGUI hardDifficultyText;
    public TextMeshProUGUI difficultyHintText;
    [Tooltip("Editable difficulty selector prefab. Used when the scene has no bound difficultyPanel.")]
    public LevelDifficultySelectorView difficultyPanelPrefab;
    [Tooltip("当场景中未绑定难度控件时，运行时自动生成一套临时美术原型。")]
    public bool buildRuntimeDifficultyPrototype = true;
    [Tooltip("When enabled, this script overwrites confirm panel and button colors at runtime. Keep disabled for hand-authored visuals.")]
    public bool applyRuntimeConfirmStyling = false;

    [Header("其他UI")]
    [Tooltip("关闭/返回按钮")]
    public Button closeButton;
    [Tooltip("面板标题文本")]
    public TextMeshProUGUI titleText;

    [Header("动画设置")]
    [Tooltip("面板淡入/淡出时间")]
    public float fadeDuration = 0.3f;

    // 内部状态
    private LevelData selectedLevel;
    private bool selectedHardDifficulty;
    private bool difficultyListenersBound;
    private System.Action<string> onLevelConfirmed;
    private LevelDifficultySelectorView difficultySelectorView;

    private const string DefaultDifficultyPanelPrefabPath = "Assets/_TheFirst/Prefabs/UI/LevelDifficultySelector.prefab";

    // 缓存场景中所有的区域按钮
    private LevelRegionButton[] regionButtons;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 【关键】先收集所有区域按钮（此时面板还是活跃的，子对象的 Awake 已执行）
        if (levelSelectPanel != null)
        {
            regionButtons = levelSelectPanel.GetComponentsInChildren<LevelRegionButton>(true);
        }

        // 初始隐藏确认弹窗
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        // 【关键】通过 CanvasGroup 隐藏面板（不使用 SetActive(false)）
        // 这样子对象保持活跃状态，Awake/Start 正常运行，事件监听正常注册
        SetPanelVisible(false);
    }

    void Start()
    {
        EnsureDifficultyControls();
        BindDifficultyButtons();
        RefreshMapSelectChrome();

        // 绑定按钮事件
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        if (confirmStartButton != null)
            confirmStartButton.onClick.AddListener(OnConfirmStart);
        if (confirmCancelButton != null)
            confirmCancelButton.onClick.AddListener(OnConfirmCancel);
    }

    #region 面板显示/隐藏核心方法

    /// <summary>
    /// 设置面板的可见性（通过 CanvasGroup 控制，不使用 SetActive）
    /// 这样子对象保持活跃，事件监听正常工作
    /// </summary>
    private void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
        else if (levelSelectPanel != null)
        {
            // 没有 CanvasGroup 时降级为 SetActive
            levelSelectPanel.SetActive(visible);
        }
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 打开关卡选择界面
    /// </summary>
    /// <param name="callback">玩家确认选择后的回调，传入目标场景名</param>
    public void Show(System.Action<string> callback = null)
    {
        onLevelConfirmed = callback;
        selectedLevel = null;
        selectedHardDifficulty = DataManager.Instance != null
            && DataManager.Instance.selectedDemoDifficulty == DemoDifficultySelection.Hard;

        // 隐藏确认弹窗
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        EnsureDifficultyControls();
        RefreshDifficultyControls();
        RefreshMapSelectChrome();

        // 刷新所有区域的解锁状态
        RefreshAllRegions();

        // 显示面板（带淡入动画）
        if (panelCanvasGroup != null)
        {
            StartCoroutine(FadePanelIn());
        }
        else
        {
            SetPanelVisible(true);
        }

        // 暂停游戏
        Time.timeScale = 0f;

        // 启用鼠标光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 关闭关卡选择界面
    /// </summary>
    public void Hide()
    {
        if (panelCanvasGroup != null)
        {
            StartCoroutine(FadePanelOut());
        }
        else
        {
            ClosePanel();
        }
    }

    /// <summary>
    /// 由 LevelRegionButton 调用 — 点击已解锁的区域
    /// </summary>
    public void OnRegionClicked(LevelData level)
    {
        if (level == null) return;
        selectedLevel = level;
        EnsureDifficultyControls();

        if (selectedHardDifficulty && !IsHardDifficultyUnlocked())
        {
            selectedHardDifficulty = false;
        }

        // 显示确认弹窗
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            RefreshDifficultyControls();

            if (confirmLevelName != null)
                confirmLevelName.text = level.LocalizedName;

            if (confirmDescription != null)
            {
                string desc = level.LocalizedDescription;
                confirmDescription.text = string.IsNullOrEmpty(desc) ? "" : desc;
                confirmDescription.gameObject.SetActive(!string.IsNullOrEmpty(desc));
            }

            if (confirmRecommendedLevel != null)
            {
                if (level.recommendedLevel > 0)
                {
                    confirmRecommendedLevel.gameObject.SetActive(true);
                    string label = LocalizationManager.CurrentLanguage == SystemLanguage.English
                        ? "Recommended Lv." : "推荐等级";
                    confirmRecommendedLevel.text = $"{label} {level.recommendedLevel}";
                }
                else
                {
                    confirmRecommendedLevel.gameObject.SetActive(false);
                }
            }

            if (confirmIcon != null)
            {
                if (level.levelIcon != null)
                {
                    confirmIcon.sprite = level.levelIcon;
                    confirmIcon.gameObject.SetActive(true);
                }
                else
                {
                    confirmIcon.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // 没有确认弹窗，直接出发
            ConfirmAndDepart(level);
        }
    }

    private void EnsureDifficultyControls()
    {
        if (difficultyPanel != null)
        {
            BindDifficultySelectorView(difficultyPanel.GetComponentInChildren<LevelDifficultySelectorView>(true));
            return;
        }

        if (confirmPanel == null)
        {
            return;
        }

        Transform parent = confirmStartButton != null && confirmStartButton.transform.parent != null
            ? confirmStartButton.transform.parent
            : confirmPanel.transform;

        LevelDifficultySelectorView prefab = ResolveDifficultyPanelPrefab();
        if (prefab != null)
        {
            LevelDifficultySelectorView view = Instantiate(prefab, parent, false);
            view.name = "LevelDifficultySelector";
            BindDifficultySelectorView(view);
            BindDifficultyButtons();
            return;
        }

        if (!buildRuntimeDifficultyPrototype)
        {
            return;
        }

        GameObject root = new GameObject("Runtime_DifficultySelector", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);
        difficultyPanel = root;
        difficultySelectorView = root.AddComponent<LevelDifficultySelectorView>();

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

        difficultyHintText = CreateRuntimeText("DifficultyHint", root.transform, 18f, FontStyles.Bold, TextAlignmentOptions.Center);

        GameObject row = new GameObject("DifficultyButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(root.transform, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 56f);

        HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 10f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;

        normalDifficultyButton = CreateDifficultyButton("NormalDifficultyButton", row.transform, out normalDifficultyText);
        hardDifficultyButton = CreateDifficultyButton("HardDifficultyButton", row.transform, out hardDifficultyText);
        difficultySelectorView.normalButton = normalDifficultyButton;
        difficultySelectorView.hardButton = hardDifficultyButton;
        difficultySelectorView.normalText = normalDifficultyText;
        difficultySelectorView.hardText = hardDifficultyText;
        difficultySelectorView.hintText = difficultyHintText;
        difficultySelectorView.normalBackground = normalDifficultyButton != null ? normalDifficultyButton.GetComponent<Image>() : null;
        difficultySelectorView.hardBackground = hardDifficultyButton != null ? hardDifficultyButton.GetComponent<Image>() : null;

        BindDifficultyButtons();
    }

    private LevelDifficultySelectorView ResolveDifficultyPanelPrefab()
    {
        if (difficultyPanelPrefab != null) return difficultyPanelPrefab;

#if UNITY_EDITOR
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultDifficultyPanelPrefabPath);
        if (prefabObject != null)
        {
            difficultyPanelPrefab = prefabObject.GetComponent<LevelDifficultySelectorView>();
        }
#endif

        return difficultyPanelPrefab;
    }

    private void BindDifficultySelectorView(LevelDifficultySelectorView view)
    {
        if (view == null || difficultySelectorView == view) return;

        difficultySelectorView = view;
        difficultyPanel = view.gameObject;
        normalDifficultyButton = view.normalButton != null ? view.normalButton : normalDifficultyButton;
        hardDifficultyButton = view.hardButton != null ? view.hardButton : hardDifficultyButton;
        normalDifficultyText = view.normalText != null ? view.normalText : normalDifficultyText;
        hardDifficultyText = view.hardText != null ? view.hardText : hardDifficultyText;
        difficultyHintText = view.hintText != null ? view.hintText : difficultyHintText;
        difficultyListenersBound = false;
    }

    private TextMeshProUGUI CreateRuntimeText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(1f, 0.91f, 0.68f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private Button CreateDifficultyButton(string name, Transform parent, out TextMeshProUGUI label)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minHeight = 46f;
        layout.preferredHeight = 46f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.23f, 0.16f, 0.09f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.03f, 0.88f, 1f);
        colors.pressedColor = new Color(0.84f, 0.7f, 0.42f, 1f);
        colors.disabledColor = new Color(0.45f, 0.43f, 0.4f, 0.72f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        label = CreateRuntimeText("Label", buttonObject.transform, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        return button;
    }

    private void BindDifficultyButtons()
    {
        if (difficultyListenersBound)
        {
            return;
        }

        if (normalDifficultyButton != null)
        {
            normalDifficultyButton.onClick.AddListener(() => SetSelectedDifficulty(false));
        }

        if (hardDifficultyButton != null)
        {
            hardDifficultyButton.onClick.AddListener(() => SetSelectedDifficulty(true));
        }

        difficultyListenersBound = true;
    }

    private void SetSelectedDifficulty(bool hard)
    {
        selectedHardDifficulty = hard && IsHardDifficultyUnlocked();
        RefreshDifficultyControls();
    }

    private void RefreshDifficultyControls()
    {
        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(selectedLevel != null);
        }

        bool hardUnlocked = IsHardDifficultyUnlocked();
        if (!hardUnlocked)
        {
            selectedHardDifficulty = false;
        }

        if (normalDifficultyText != null)
        {
            normalDifficultyText.text = LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Normal" : "普通";
        }

        if (hardDifficultyText != null)
        {
            hardDifficultyText.text = hardUnlocked
                ? (LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Hard" : "困难")
                : (LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Hard Locked" : "困难 未解锁");
        }

        if (difficultyHintText != null)
        {
            difficultyHintText.text = hardUnlocked
                ? (LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Select difficulty" : "选择难度")
                : (LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Clear Normal to unlock Hard" : "通关普通后解锁困难");
        }

        if (hardDifficultyButton != null)
        {
            hardDifficultyButton.interactable = hardUnlocked;
        }

        if (difficultySelectorView != null)
        {
            difficultySelectorView.ApplyVisualState(selectedHardDifficulty, hardUnlocked);
        }
        else
        {
            SetDifficultyButtonVisual(normalDifficultyButton, !selectedHardDifficulty, false);
            SetDifficultyButtonVisual(hardDifficultyButton, selectedHardDifficulty, !hardUnlocked);
        }
    }

    private void SetDifficultyButtonVisual(Button button, bool selected, bool locked)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        if (locked)
        {
            image.color = new Color(0.19f, 0.18f, 0.17f, 0.9f);
        }
        else if (selected)
        {
            image.color = new Color(0.93f, 0.55f, 0.16f, 1f);
        }
        else
        {
            image.color = new Color(0.24f, 0.15f, 0.08f, 0.95f);
        }
    }

    private bool IsHardDifficultyUnlocked()
    {
        if (!DemoContentGate.DemoModeEnabled)
        {
            return true;
        }

        return PlayerProgressManager.Instance != null
            && PlayerProgressManager.Instance.IsItemUnlocked(DemoContentGate.HardUnlockItemId);
    }

    private void ApplySelectedDifficulty()
    {
        if (DataManager.Instance == null)
        {
            return;
        }

        bool canUseHard = selectedHardDifficulty && IsHardDifficultyUnlocked();
        DataManager.Instance.selectedDemoDifficulty = canUseHard
            ? DemoDifficultySelection.Hard
            : DemoDifficultySelection.Normal;
    }

    private void RefreshMapSelectChrome()
    {
        if (titleText != null)
        {
            titleText.text = LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Select Stage" : "选择关卡";
            if (applyRuntimeConfirmStyling)
            {
                titleText.color = new Color(1f, 0.92f, 0.72f, 1f);
                titleText.fontStyle = FontStyles.Bold;
            }
        }

        if (!applyRuntimeConfirmStyling)
        {
            return;
        }

        if (confirmPanel != null)
        {
            Image confirmImage = confirmPanel.GetComponent<Image>();
            if (confirmImage != null)
            {
                confirmImage.color = new Color(0.05f, 0.035f, 0.02f, 0.2f);
            }
        }

        StyleButton(confirmStartButton, new Color(0.92f, 0.55f, 0.16f, 1f));
        StyleButton(confirmCancelButton, new Color(0.14f, 0.10f, 0.07f, 0.94f));
    }

    private void StyleButton(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    #endregion

    #region 内部逻辑

    /// <summary>
    /// 刷新所有区域的解锁状态
    /// </summary>
    private void RefreshAllRegions()
    {
        if (regionButtons == null || regionButtons.Length == 0)
        {
            // 重新查找
            if (levelSelectPanel != null)
                regionButtons = levelSelectPanel.GetComponentsInChildren<LevelRegionButton>(true);
        }

        if (regionButtons == null) return;

        foreach (var region in regionButtons)
        {
            if (region != null)
            {
                region.RefreshUnlockState();
            }
        }
    }

    /// <summary>
    /// 确认弹窗 - 点击「出发」
    /// </summary>
    private void OnConfirmStart()
    {
        if (selectedLevel == null) return;
        ConfirmAndDepart(selectedLevel);
    }

    /// <summary>
    /// 确认弹窗 - 点击「取消」
    /// </summary>
    private void OnConfirmCancel()
    {
        selectedLevel = null;
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    /// <summary>
    /// 确认选择关卡并开始传送
    /// </summary>
    private void ConfirmAndDepart(LevelData level)
    {
        ApplySelectedDifficulty();

        string targetScene = level.sceneName;
        if (!DemoContentGate.IsSceneAllowed(targetScene))
        {
            string fallbackScene = DemoContentGate.GetDemoFallbackScene();
            Debug.LogWarning($"[LevelSelectUI] Demo build blocked scene '{targetScene}', fallback='{fallbackScene}'");
            targetScene = fallbackScene;
        }

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError($"[LevelSelectUI] 关卡 '{level.levelName}' 的 sceneName 为空！");
            return;
        }

        // 隐藏面板
        SetPanelVisible(false);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        // 恢复时间缩放
        Time.timeScale = 1f;

        // 通过回调通知 PipeTeleporter 执行传送动画
        if (onLevelConfirmed != null)
        {
            onLevelConfirmed.Invoke(targetScene);
        }
        else
        {
            // 没有回调时直接加载场景
            LoadScene(targetScene);
        }
    }

    /// <summary>
    /// 直接加载场景（带过渡效果）
    /// </summary>
    private void LoadScene(string sceneName)
    {
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition())
        {
            transitioner.TransitionToScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    #endregion

    #region 面板动画

    /// <summary>
    /// 淡入面板
    /// </summary>
    private IEnumerator FadePanelIn()
    {
        if (panelCanvasGroup == null) yield break;

        // 先设置可交互和射线检测（让按钮能立即响应）
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        // 执行淡入
        yield return FadeAlpha(0f, 1f);
    }

    /// <summary>
    /// 淡出面板并关闭
    /// </summary>
    private IEnumerator FadePanelOut()
    {
        if (panelCanvasGroup == null)
        {
            ClosePanel();
            yield break;
        }

        // 先禁用交互（防止淡出过程中误点击）
        panelCanvasGroup.interactable = false;

        // 执行淡出
        yield return FadeAlpha(panelCanvasGroup.alpha, 0f);

        // 淡出完成后完全关闭
        ClosePanel();
    }

    /// <summary>
    /// CanvasGroup alpha 渐变
    /// </summary>
    private IEnumerator FadeAlpha(float from, float to)
    {
        panelCanvasGroup.alpha = from;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float smoothT = t * t * (3f - 2f * t);
            panelCanvasGroup.alpha = Mathf.Lerp(from, to, smoothT);
            yield return null;
        }

        panelCanvasGroup.alpha = to;
    }

    private void ClosePanel()
    {
        // 通过 CanvasGroup 隐藏
        SetPanelVisible(false);

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        // 恢复时间
        Time.timeScale = 1f;

        // 通知 PipeTeleporter 重置状态（允许玩家再次触发传送）
        var teleporter = Object.FindFirstObjectByType<PipeTeleporter>();
        if (teleporter != null)
        {
            teleporter.ResetTeleporter();
        }

        // 清空状态
        onLevelConfirmed = null;
        selectedLevel = null;
    }

    #endregion

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
