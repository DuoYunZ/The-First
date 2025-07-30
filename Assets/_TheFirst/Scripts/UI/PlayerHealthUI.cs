// PlayerHealthUI.cs (最终修正版 - 修复隐藏父对象的问题)
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI组件引用")]
    public Image healthFillImage;       // 顶层，橙色
    public Image shieldFillImage;       // 中层，蓝色
    public Image pendingDamageFillImage;  // 底层，暗红色

    [Header("宽度设置")]
    [Tooltip("代表100点生命/护盾值的UI基础宽度")]
    public float baseWidthPer100Points = 300f;

    [Header("动画设置")]
    [Tooltip("追赶动画开始前的延迟")]
    public float pendingDamageDelay = 0.5f;
    [Tooltip("追赶动画的持续时间")]
    public float pendingDamageDuration = 0.4f;

    private Health playerHealth;
    private PlayerShield playerShield;
    private RectTransform selfRectTransform;

    private Coroutine pendingDamageCoroutine;

    void Awake()
    {
        selfRectTransform = GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged.RemoveListener(OnDataChanged);
        if (playerShield != null) playerShield.OnShieldChanged.RemoveListener(OnDataChanged);
    }

    public void Initialize(Health health, PlayerShield shield)
    {
        playerHealth = health;
        playerShield = shield;

        if (playerHealth == null || playerShield == null) return;

        playerHealth.OnHealthChanged.AddListener(OnDataChanged);
        playerShield.OnShieldChanged.AddListener(OnDataChanged);

        StartCoroutine(InitialUpdate());
    }

    private IEnumerator InitialUpdate()
    {
        yield return null;
        UpdateHealthDisplay(true);
    }

    private void OnDataChanged(int current, int max)
    {
        UpdateHealthDisplay(false);
    }

    private void UpdateHealthDisplay(bool instant = false)
    {
        if (playerHealth == null || playerShield == null) return;

        int currentHealth = playerHealth.GetCurrentHealth();
        int maxHealth = playerHealth.GetMaxHealth();
        int currentShield = playerShield.GetCurrentShield();

        // --- 【核心修正】---
        // 只控制 shieldFillImage 自身的显隐，不再影响父对象
        shieldFillImage.gameObject.SetActive(currentShield > 0);

        float displayMaxWidth = Mathf.Max(maxHealth, currentHealth + currentShield);

        float newPhysicalWidth = displayMaxWidth / 100f * baseWidthPer100Points;
        selfRectTransform.sizeDelta = new Vector2(newPhysicalWidth, selfRectTransform.sizeDelta.y);

        float healthTargetRatio = (currentHealth > 0) ? (float)currentHealth / displayMaxWidth : 0;
        float shieldTargetRatio = (currentShield > 0) ? (float)(currentHealth + currentShield) / displayMaxWidth : healthTargetRatio;

        if (pendingDamageCoroutine != null)
        {
            StopCoroutine(pendingDamageCoroutine);
        }

        if (instant || healthTargetRatio > healthFillImage.fillAmount)
        {
            healthFillImage.fillAmount = healthTargetRatio;
            shieldFillImage.fillAmount = shieldTargetRatio;
            pendingDamageCoroutine = StartCoroutine(AnimatePendingDamage(shieldTargetRatio, 0f)); // 立即追赶
        }
        else
        {
            healthFillImage.fillAmount = healthTargetRatio;
            shieldFillImage.fillAmount = shieldTargetRatio;

            pendingDamageCoroutine = StartCoroutine(AnimatePendingDamage(shieldTargetRatio, pendingDamageDelay)); // 延迟追赶
        }
    }

    private IEnumerator AnimatePendingDamage(float targetRatio, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        float timer = 0f;
        float startFill = pendingDamageFillImage.fillAmount;

        while (timer < pendingDamageDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / pendingDamageDuration);
            pendingDamageFillImage.fillAmount = Mathf.Lerp(startFill, targetRatio, progress);
            yield return null;
        }

        pendingDamageFillImage.fillAmount = targetRatio;
    }
}