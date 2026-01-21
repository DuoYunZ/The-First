using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 如果你用的是 TextMeshPro

public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("UI 组件引用")]
    public GameObject panelRoot;      // 整个面板的父物体 (用于开关)
    public Slider healthSlider;       // 主血条 Slider
    public Image easeFillImage;       // 缓冲层 Image (如果是 Slider 的话就引用 Slider)
    public Slider easeSlider;         // 【推荐】缓冲层也用一个 Slider 组件，和主血条重叠
    public TextMeshProUGUI nameText;  // Boss 名字
    public CanvasGroup canvasGroup;   // 用于淡入淡出

    [Header("设置")]
    public float easeSpeed = 2f;      // 缓冲条追赶速度

    private Health targetBossHealth;  // 当前绑定的 Boss 血量组件

    private void Awake()
    {
        // 单例模式，方便 Boss 生成时直接调用
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始隐藏
        if (panelRoot != null) panelRoot.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0;
    }

    private void Update()
    {
        if (targetBossHealth == null) return;

        // 1. 更新主血条 (瞬间变化)
        // 假设 Health 组件有 currentHealth 和 maxHealth 字段
        float targetFill = (float)targetBossHealth.currentHealth / targetBossHealth.maxHealth;

        if (healthSlider.value != targetFill)
        {
            healthSlider.value = targetFill;
        }

        // 2. 更新缓冲条 (平滑追赶)
        // 如果 easeSlider 的值比主血条大，就慢慢减小
        if (easeSlider != null)
        {
            if (easeSlider.value > healthSlider.value)
            {
                easeSlider.value = Mathf.Lerp(easeSlider.value, healthSlider.value, Time.deltaTime * easeSpeed);
            }
            else
            {
                // 如果回血了，或者缓冲条追上了，就同步
                easeSlider.value = healthSlider.value;
            }
        }

        // 3. 检测 Boss 死亡
        if (targetBossHealth.IsDead)
        {
            HideBossBar();
        }
    }

    /// <summary>
    /// 当 Boss 生成时调用此方法
    /// </summary>
    public void InitializeBossBar(Health bossHealth, string bossName)
    {
        targetBossHealth = bossHealth;

        // 设置 UI
        if (nameText != null) nameText.text = bossName;

        // 重置血条状态
        healthSlider.value = 1f;
        if (easeSlider != null) easeSlider.value = 1f;

        // 显示面板
        if (panelRoot != null) panelRoot.SetActive(true);

        // 播放淡入动画
        StartCoroutine(FadeInRoutine());
    }

    public void HideBossBar()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            if (canvasGroup != null) canvasGroup.alpha = t;
            yield return null;
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        float t = canvasGroup.alpha;
        while (t > 0f)
        {
            t -= Time.deltaTime * 2f;
            if (canvasGroup != null) canvasGroup.alpha = t;
            yield return null;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
        targetBossHealth = null; // 解绑
    }
}