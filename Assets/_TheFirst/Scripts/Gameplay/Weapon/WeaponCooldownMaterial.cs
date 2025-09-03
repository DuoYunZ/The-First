// --- WeaponCooldownMaterial.cs ---
using UnityEngine;
using System.Collections;

public class WeaponCooldownMaterial : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("需要控制材质的渲染器，通常是武器模型上的 MeshRenderer")]
    public Renderer targetRenderer;

    [Header("发光设置")]
    [Tooltip("自发光的基础颜色")]
    [ColorUsage(true, true)] // 允许在颜色拾取器中使用HDR
    public Color emissionColor = new Color(1.0f, 0.5f, 0.0f);

    [Tooltip("冷却开始时的最低发光强度")]
    public float minIntensity = -10f;

    [Tooltip("冷却完成时的最高发光强度")]
    public float maxIntensity = 5f;

    // --- 私有变量 ---
    private MaterialPropertyBlock propBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private Coroutine activeCooldownCoroutine;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        // 如果没有手动指定渲染器，就自动在子物体中查找第一个
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        // 初始状态设置为满能量
        SetChargedEffect();
    }

    /// <summary>
    /// 开始冷却视觉效果
    /// </summary>
    /// <param name="duration">冷却持续时间 (秒)</param>
    public void StartCooldown(float duration)
    {
        // 【调试日志 1】检查方法是否被调用，以及冷却时间是否正确
        Debug.Log($"[WeaponMaterial] StartCooldown called with duration: {duration}");

        if (activeCooldownCoroutine != null)
        {
            StopCoroutine(activeCooldownCoroutine);
        }

        // 【调试日志 2】检查传入的 duration 是否有效
        if (duration <= 0)
        {
            Debug.LogWarning("[WeaponMaterial] Cooldown duration is 0 or negative. Animation skipped. Setting to charged.");
            SetChargedEffect();
            return; // 如果时间无效，则直接设置为满能量状态并返回
        }

        activeCooldownCoroutine = StartCoroutine(CooldownEffect(duration));
    }

    private IEnumerator CooldownEffect(float duration)
    {
        float elapsedTime = 0f;

        // 【调试日志 3】确认强度是否被设置为最小值
        Debug.Log("[WeaponMaterial] Coroutine started. Setting intensity to min: " + minIntensity);
        SetIntensity(minIntensity);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float currentIntensity = Mathf.Lerp(minIntensity, maxIntensity, progress);
            SetIntensity(currentIntensity);

            // 【调试日志 4】（可选，如果需要可以取消注释）在循环中持续打印进度
            // Debug.Log($"[WeaponMaterial] Cooldown progress: {progress * 100:F1}%, Current Intensity: {currentIntensity:F2}");

            yield return null;
        }

        // 【调试日志 5】确认协程是否正常结束
        Debug.Log("[WeaponMaterial] Cooldown finished. Setting intensity to max: " + maxIntensity);
        SetChargedEffect();
    }

    /// <summary>
    /// 直接将武器设置为满能量发光状态
    /// </summary>
    public void SetChargedEffect()
    {
        SetIntensity(maxIntensity);
    }

    /// <summary>
    /// 设置发光强度
    /// </summary>
    private void SetIntensity(float intensity)
    {
        if (targetRenderer == null) return;

        // 获取当前的属性块，以防其他脚本也在修改它
        targetRenderer.GetPropertyBlock(propBlock);

        // URP Lit Shader 使用颜色乘以 2^强度 来计算HDR颜色
        Color finalColor = emissionColor * Mathf.Pow(2, intensity);

        // 设置颜色并应用到渲染器
        propBlock.SetColor(EmissionColorID, finalColor);
        targetRenderer.SetPropertyBlock(propBlock);
    }
}