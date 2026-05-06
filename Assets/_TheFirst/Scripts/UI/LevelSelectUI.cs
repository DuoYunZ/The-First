using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

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
    private System.Action<string> onLevelConfirmed;

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

        // 隐藏确认弹窗
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

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

        // 显示确认弹窗
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);

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
        string targetScene = level.sceneName;
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
