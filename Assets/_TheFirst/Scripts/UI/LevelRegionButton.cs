using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

/// <summary>
/// 关卡区域按钮 — 挂在每个区域切图的 Image 对象上
/// 
/// 使用方式：
/// 1. 将区域彩色切图作为 Image 放在地图容器下
/// 2. 手动调整位置和大小，对齐灰色底图
/// 3. 挂载此脚本，拖入对应的 LevelData
/// 4. 脚本会自动处理：解锁/锁定视觉、悬停高亮、点击事件
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class LevelRegionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("关卡数据")]
    [Tooltip("此区域对应的关卡数据")]
    public LevelData levelData;

    [Header("区域名称标签（可选）")]
    [Tooltip("区域名称文本（子对象中的 TMP_Text）")]
    public TextMeshProUGUI regionNameText;

    [Header("锁定图标（可选）")]
    [Tooltip("锁定时显示的图标 GameObject")]
    public GameObject lockIcon;

    [Header("悬停效果设置")]
    [Tooltip("悬停时的缩放倍数")]
    public float hoverScale = 1.05f;
    [Tooltip("悬停缩放动画时长")]
    public float hoverAnimDuration = 0.15f;
    [Tooltip("悬停时的亮度提升")]
    public float hoverBrightness = 1.2f;

    // 内部引用
    private Image regionImage;
    private Button regionButton;
    private Color originalColor;
    private Vector3 originalScale;
    private Coroutine hoverCoroutine;
    private bool isUnlocked = false;
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 确保组件引用被正确初始化（可多次调用，安全幂等）
    /// </summary>
    private void Initialize()
    {
        if (isInitialized) return;

        regionImage = GetComponent<Image>();
        regionButton = GetComponent<Button>();

        if (regionImage != null)
            originalColor = regionImage.color;

        originalScale = transform.localScale;
        isInitialized = true;
    }

    void Start()
    {
        // 确保初始化（以防 Awake 未被调用）
        Initialize();

        // 刷新解锁状态
        RefreshUnlockState();

        // 绑定点击事件
        if (regionButton != null)
            regionButton.onClick.AddListener(OnClicked);
    }

    /// <summary>
    /// 刷新解锁状态和视觉效果
    /// 每次打开面板时由 LevelSelectUI 调用
    /// </summary>
    public void RefreshUnlockState()
    {
        if (!DemoContentGate.IsLevelAllowed(levelData))
        {
            gameObject.SetActive(false);
            return;
        }

        // 确保初始化
        Initialize();

        if (levelData == null)
        {
            Debug.LogWarning($"[LevelRegionButton] {gameObject.name} 未设置 LevelData！", this);
            return;
        }

        if (regionImage == null || regionButton == null)
        {
            Debug.LogWarning($"[LevelRegionButton] {gameObject.name} 组件引用丢失！", this);
            return;
        }

        isUnlocked = IsLevelUnlocked(levelData);

        if (isUnlocked)
        {
            // 已解锁：显示彩色原图
            regionImage.color = originalColor;
            regionButton.interactable = true;

            // 隐藏锁定图标
            if (lockIcon != null)
                lockIcon.SetActive(false);

            // 显示区域名称
            if (regionNameText != null)
            {
                regionNameText.text = levelData.LocalizedName;
                regionNameText.color = Color.white;
            }
        }
        else
        {
            // 未解锁：隐藏彩色图（露出灰色底图）
            regionImage.color = new Color(
                originalColor.r, originalColor.g, originalColor.b, 0f
            );
            regionButton.interactable = false;

            // 显示锁定图标
            if (lockIcon != null)
                lockIcon.SetActive(true);

            // 区域名称灰色
            if (regionNameText != null)
            {
                regionNameText.text = levelData.LocalizedName;
                regionNameText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
        }
    }

    /// <summary>
    /// 判断关卡是否已解锁
    /// </summary>
    private bool IsLevelUnlocked(LevelData level)
    {
        if (level.isUnlockedByDefault) return true;

        if (!string.IsNullOrEmpty(level.requiredClearedLevelID))
        {
            if (PlayerProgressManager.Instance != null)
            {
                return PlayerProgressManager.Instance.HasMechanic(level.requiredClearedLevelID);
            }
        }

        return false;
    }

    /// <summary>
    /// 点击事件 — 通知 LevelSelectUI 显示确认弹窗
    /// </summary>
    private void OnClicked()
    {
        if (!isUnlocked || levelData == null) return;

        if (LevelSelectUI.Instance != null)
        {
            LevelSelectUI.Instance.OnRegionClicked(levelData);
        }
        else
        {
            Debug.LogError("[LevelRegionButton] LevelSelectUI.Instance 为空！无法打开确认弹窗。");
        }
    }

    #region 悬停效果

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isUnlocked) return;

        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateHover(true));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isUnlocked) return;

        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateHover(false));
    }

    /// <summary>
    /// 悬停动画：缩放 + 亮度变化
    /// </summary>
    private IEnumerator AnimateHover(bool entering)
    {
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = entering ? originalScale * hoverScale : originalScale;

        Color startColor = regionImage.color;
        Color targetColor = entering
            ? new Color(
                Mathf.Min(originalColor.r * hoverBrightness, 1f),
                Mathf.Min(originalColor.g * hoverBrightness, 1f),
                Mathf.Min(originalColor.b * hoverBrightness, 1f),
                originalColor.a)
            : originalColor;

        float elapsed = 0f;
        while (elapsed < hoverAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hoverAnimDuration);
            // SmoothStep 缓动
            float smooth = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, smooth);
            regionImage.color = Color.Lerp(startColor, targetColor, smooth);

            yield return null;
        }

        transform.localScale = targetScale;
        regionImage.color = targetColor;
    }

    #endregion
}
