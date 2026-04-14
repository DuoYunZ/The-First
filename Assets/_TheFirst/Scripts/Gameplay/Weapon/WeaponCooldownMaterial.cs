// --- WeaponCooldownMaterial.cs ---
// 武器冷却视觉效果控制器
// 支持两种模式：整体发光渐变 / 从底部往上填充（需配合 Custom/WeaponFillCharge Shader）
using UnityEngine;
using System.Collections;

public class WeaponCooldownMaterial : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("需要控制材质的渲染器，通常是武器模型上的 MeshRenderer")]
    public Renderer targetRenderer;

    [Header("充能模式")]
    [Tooltip("true = 从底部往上填充（需要 WeaponFillCharge Shader）\nfalse = 整体渐变发光（兼容任何 Shader）")]
    public bool useFillMode = true;

    [Header("发光设置（整体模式）")]
    [Tooltip("自发光的基础颜色")]
    [ColorUsage(true, true)]
    public Color defaultEmissionColor = new Color(1.0f, 0.5f, 0.0f);

    private Color currentEmissionColor;

    [Tooltip("冷却开始时的最低发光强度")]
    public float minIntensity = -10f;

    [Tooltip("冷却完成时的最高发光强度")]
    public float maxIntensity = 5f;

    // --- 私有变量 ---
    private MaterialPropertyBlock propBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int FillProgressID = Shader.PropertyToID("_FillProgress");
    private Coroutine activeCooldownCoroutine;

    void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        currentEmissionColor = defaultEmissionColor;

        // 确保 propBlock 已初始化
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        SetChargedEffect();
    }

    public void SetEmissionColor(Color newColor)
    {
        currentEmissionColor = newColor;

        if (useFillMode)
        {
            // 填充模式：设置 Shader 的 _EmissionColor
            if (targetRenderer == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorID, newColor);
            targetRenderer.SetPropertyBlock(propBlock);
        }
        else
        {
            SetIntensity(maxIntensity);
        }
    }

    public void ResetEmissionColor()
    {
        currentEmissionColor = defaultEmissionColor;
        if (useFillMode)
            SetEmissionColor(defaultEmissionColor);
        else
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

        // 双重保险：如果在非激活物体上启动，直接跳过并设置满状态
        if (!this.gameObject.activeInHierarchy)
        {
            SetChargedEffect();
            return;
        }

        activeCooldownCoroutine = StartCoroutine(CooldownEffect(duration));
    }

    private IEnumerator CooldownEffect(float duration)
    {
        float elapsedTime = 0f;

        if (useFillMode)
        {
            // 填充模式：从 0 → 1 渐变 _FillProgress
            SetFillProgress(0f);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / duration);
                SetFillProgress(progress);
                yield return null;
            }
            SetFillProgress(1f);
        }
        else
        {
            // 整体发光模式：从暗 → 亮
            SetIntensity(minIntensity);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / duration);
                SetIntensity(Mathf.Lerp(minIntensity, maxIntensity, progress));
                yield return null;
            }
            SetIntensity(maxIntensity);
        }

        SetChargedEffect();
    }

    /// <summary>
    /// 直接将武器设置为满能量发光状态
    /// </summary>
    public void SetChargedEffect()
    {
        if (useFillMode)
            SetFillProgress(1f);
        else
            SetIntensity(maxIntensity);
    }

    /// <summary>
    /// 设置填充进度（0 = 完全未充能，1 = 完全充满）
    /// 配合 Custom/WeaponFillCharge Shader 使用
    /// </summary>
    private void SetFillProgress(float progress)
    {
        if (targetRenderer == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(FillProgressID, progress);
        targetRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// 设置发光强度（整体发光模式）
    /// </summary>
    private void SetIntensity(float intensity)
    {
        if (targetRenderer == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(propBlock);
        Color finalColor = currentEmissionColor * Mathf.Pow(2, intensity);
        propBlock.SetColor(EmissionColorID, finalColor);
        targetRenderer.SetPropertyBlock(propBlock);
    }
}