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
    public Color defaultEmissionColor = new Color(1.0f, 0.5f, 0.0f); // 默认颜色

    private Color currentEmissionColor;

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
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        currentEmissionColor = defaultEmissionColor;

        // Ensure propBlock is ready
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        SetChargedEffect();
    }
    public void SetEmissionColor(Color newColor)
    {
        currentEmissionColor = newColor;
        SetIntensity(maxIntensity); // Apply immediately
    }
    public void ResetEmissionColor()
    {
        currentEmissionColor = defaultEmissionColor;
        SetIntensity(maxIntensity);
    }

    /// <summary>
    /// 开始冷却视觉效果
    /// </summary>
    /// <param name="duration">冷却持续时间 (秒)</param>
    public void StartCooldown(float duration)
    {
        if (activeCooldownCoroutine != null) StopCoroutine(activeCooldownCoroutine);

        if (duration <= 0) { SetChargedEffect(); return; }

        // 【新增】双重保险：如果在非激活物体上启动，直接跳过并设置满状态
        if (!this.gameObject.activeInHierarchy)
        {
            // Debug.LogWarning($"[Cooldown] 试图在非激活物体 {name} 上启动协程，已忽略。");
            SetChargedEffect();
            return;
        }

        activeCooldownCoroutine = StartCoroutine(CooldownEffect(duration));
    }


    private IEnumerator CooldownEffect(float duration)
    {
        float elapsedTime = 0f;
        SetIntensity(minIntensity);
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            SetIntensity(Mathf.Lerp(minIntensity, maxIntensity, progress));
            yield return null;
        }
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

        // Lazy initialization to prevent crashes if called before Awake
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(propBlock);

        Color finalColor = currentEmissionColor * Mathf.Pow(2, intensity);

        propBlock.SetColor(EmissionColorID, finalColor);
        targetRenderer.SetPropertyBlock(propBlock);
    }
}