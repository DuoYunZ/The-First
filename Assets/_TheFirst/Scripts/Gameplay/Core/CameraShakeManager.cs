using UnityEngine;
using Cinemachine;

/// <summary>
/// 相机震动管理器 — 通过 Cinemachine Impulse 系统实现震屏效果
/// 使用方法：CameraShakeManager.Instance.Shake(强度, 持续时间)
/// 需要在 CinemachineVirtualCamera 上添加 CinemachineImpulseListener 扩展
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance { get; private set; }

    [Header("默认震动参数")]
    [Tooltip("默认震动强度")]
    public float defaultIntensity = 0.5f;
    [Tooltip("默认震动持续时间（秒）")]
    public float defaultDuration = 0.2f;

    private CinemachineImpulseSource impulseSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>
    /// 触发一次震屏（使用默认参数）
    /// </summary>
    public void Shake()
    {
        Shake(defaultIntensity, defaultDuration);
    }

    /// <summary>
    /// 触发一次震屏
    /// </summary>
    /// <param name="intensity">震动强度</param>
    /// <param name="duration">持续时间（秒）</param>
    public void Shake(float intensity, float duration)
    {
        if (impulseSource == null) return;

        // 设置衰减时间
        impulseSource.m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime = duration * 0.3f;
        impulseSource.m_ImpulseDefinition.m_TimeEnvelope.m_DecayTime = duration * 0.7f;

        // 生成随机方向的冲击
        Vector3 velocity = Random.insideUnitSphere.normalized * intensity;
        impulseSource.GenerateImpulse(velocity);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
