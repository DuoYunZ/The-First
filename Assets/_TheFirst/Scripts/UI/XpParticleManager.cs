using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 管理从伤害位置飞向武器图标的XP粒子效果
/// 使用单例模式，挂载在Canvas上
/// </summary>
public class XpParticleManager : MonoBehaviour
{
    public static XpParticleManager Instance { get; private set; }

    [Header("粒子设置")]
    [Tooltip("XP粒子预制件 (UI Image)")]
    public GameObject xpParticlePrefab;
    
    [Tooltip("每次伤害生成的粒子数量")]
    public int particlesPerHit = 3;
    
    [Tooltip("粒子飞行时间")]
    public float flyDuration = 0.5f;
    
    [Tooltip("粒子初始扩散半径")]
    public float spreadRadius = 50f;
    
    [Tooltip("粒子大小")]
    public float particleScale = 1f;

    [Header("视觉效果")]
    [Tooltip("粒子颜色")]
    public Color particleColor = new Color(1f, 0.8f, 0.3f, 1f);
    
    [Tooltip("到达时的缩放动画")]
    public float arrivalPunchScale = 1.3f;

    private Camera mainCamera;
    private RectTransform canvasRect;
    private Canvas parentCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        mainCamera = Camera.main;
        
        // 尝试多种方式获取Canvas
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = GetComponent<Canvas>();
        }
        if (parentCanvas == null)
        {
            // 尝试从场景中找到主Canvas
            parentCanvas = FindFirstObjectByType<Canvas>();
        }
        
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogWarning("[XpParticleManager] 未找到Canvas，粒子效果将被禁用");
        }
    }

    /// <summary>
    /// 从世界位置发射粒子飞向指定的UI目标
    /// </summary>
    public void SpawnXpParticles(Vector3 worldPosition, RectTransform targetUI, int count = -1)
    {
        // 安全检查
        if (xpParticlePrefab == null || targetUI == null) return;
        
        // 延迟初始化
        if (mainCamera == null) mainCamera = Camera.main;
        if (canvasRect == null) InitializeReferences();
        
        // 如果仍然无法获取必要引用，直接返回
        if (mainCamera == null || canvasRect == null || parentCanvas == null) 
        {
            Debug.LogWarning("[XpParticleManager] 缺少必要引用，跳过粒子生成");
            return;
        }

        int particleCount = count > 0 ? count : particlesPerHit;
        
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPosition);
        
        // 转换为Canvas坐标
        Vector2 canvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, parentCanvas.worldCamera, out canvasPos);

        for (int i = 0; i < particleCount; i++)
        {
            StartCoroutine(SpawnAndFlyParticle(canvasPos, targetUI));
        }
    }

    private IEnumerator SpawnAndFlyParticle(Vector2 startPos, RectTransform target)
    {
        // 创建粒子
        GameObject particle = Instantiate(xpParticlePrefab, transform);
        RectTransform rect = particle.GetComponent<RectTransform>();
        Image img = particle.GetComponent<Image>();
        
        if (rect == null || img == null)
        {
            Destroy(particle);
            yield break;
        }

        // 设置初始位置（带随机扩散）
        Vector2 randomOffset = Random.insideUnitCircle * spreadRadius;
        Vector2 actualStart = startPos + randomOffset;
        rect.anchoredPosition = actualStart;
        rect.localScale = Vector3.one * particleScale;
        img.color = particleColor;

        // 飞行动画
        float elapsed = 0f;
        float randomDelay = Random.Range(0f, 0.1f);
        yield return new WaitForSecondsRealtime(randomDelay);

        Vector2 startPosition = rect.anchoredPosition;
        
        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flyDuration;
            
            // 使用缓动曲线 (先慢后快)
            float easedT = t * t * t;

            // 【修复】实时计算目标的Canvas坐标
            Vector2 targetPos = GetTargetCanvasPosition(target);
            
            // 插值移动
            rect.anchoredPosition = Vector2.Lerp(startPosition, targetPos, easedT);
            
            // 逐渐缩小
            float scale = Mathf.Lerp(particleScale, particleScale * 0.5f, easedT);
            rect.localScale = Vector3.one * scale;
            
            yield return null;
        }

        // 到达目标时的反馈（不触发脉冲，由WeaponUI处理）
        // 销毁粒子
        Destroy(particle);
    }

    /// <summary>
    /// 获取目标UI元素在当前Canvas下的正确坐标
    /// </summary>
    private Vector2 GetTargetCanvasPosition(RectTransform target)
    {
        if (target == null) return Vector2.zero;
        
        // 将目标的世界坐标转为屏幕坐标，再转为当前Canvas的本地坐标
        Vector3 worldPos = target.position;
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldPos);
        
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, parentCanvas.worldCamera, out localPos);
        
        return localPos;
    }
}
