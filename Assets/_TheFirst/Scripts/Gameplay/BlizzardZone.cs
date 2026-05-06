using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 永冻领域 —— 范围内敌人减速70%，每秒冰冻判定，冰锥穿透不消耗
/// 由 PlayerMagicSystem 在 Mage_Talent_Blizzard 激活时自动生成
/// </summary>
public class BlizzardZone : MonoBehaviour
{
    [Header("永冻领域参数")]
    [Tooltip("区域半径")]
    public float radius = 8f;
    [Tooltip("持续时间")]
    public float duration = 6f;
    [Tooltip("减速百分比 (0.7 = 70%减速)")]
    public float slowPercent = 0.7f;
    [Tooltip("冰冻判定间隔")]
    public float freezeTickInterval = 1f;
    [Tooltip("每次冰冻判定的概率 (0~1)")]
    public float freezeChance = 0.3f;
    [Tooltip("冰冻持续时间")]
    public float freezeDuration = 1.5f;

    private LayerMask enemyLayer;
    private float freezeTickTimer = 0f;

    // 追踪在区域内的敌人，用于持续减速的添加/移除
    private HashSet<StatusEffectReceiver> affectedEnemies = new HashSet<StatusEffectReceiver>();

    void Start()
    {
        enemyLayer = LayerMask.GetMask("Enemies") | LayerMask.GetMask("Enemy");

        // 通知法师系统：永冻领域激活（冰锥穿透不消耗）
        if (PlayerMagicSystem.Instance != null)
        {
            PlayerMagicSystem.Instance.isBlizzardActive = true;
        }

        // 自动销毁
        Destroy(gameObject, duration);
    }

    void Update()
    {
        freezeTickTimer += Time.deltaTime;

        // 每帧更新减速效果（进入/离开区域）
        UpdateSlowEffects();

        // 定时冰冻判定
        if (freezeTickTimer >= freezeTickInterval)
        {
            freezeTickTimer -= freezeTickInterval;
            FreezeCheck();
        }
    }

    void OnDestroy()
    {
        // 移除所有持续减速
        foreach (var receiver in affectedEnemies)
        {
            if (receiver != null)
            {
                receiver.RemovePersistentSlow(this);
            }
        }
        affectedEnemies.Clear();

        // 通知法师系统：永冻领域结束
        if (PlayerMagicSystem.Instance != null)
        {
            PlayerMagicSystem.Instance.isBlizzardActive = false;
        }
    }

    /// <summary>
    /// 更新范围内敌人的持续减速效果
    /// </summary>
    private void UpdateSlowEffects()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);

        // 当前帧在范围内的敌人
        HashSet<StatusEffectReceiver> currentFrame = new HashSet<StatusEffectReceiver>();

        foreach (var col in hits)
        {
            StatusEffectReceiver receiver = col.GetComponent<StatusEffectReceiver>();
            if (receiver == null) receiver = col.GetComponentInParent<StatusEffectReceiver>();
            if (receiver == null) continue;

            currentFrame.Add(receiver);

            // 新进入的敌人：添加持续减速
            if (!affectedEnemies.Contains(receiver))
            {
                receiver.ApplyPersistentSlow(this, slowPercent, new Color(0.4f, 0.7f, 1f));
            }
        }

        // 离开区域的敌人：移除持续减速
        var toRemove = new List<StatusEffectReceiver>();
        foreach (var receiver in affectedEnemies)
        {
            if (receiver == null || !currentFrame.Contains(receiver))
            {
                if (receiver != null) receiver.RemovePersistentSlow(this);
                toRemove.Add(receiver);
            }
        }
        foreach (var r in toRemove) affectedEnemies.Remove(r);

        // 更新追踪列表
        affectedEnemies = currentFrame;
    }

    /// <summary>
    /// 每秒冰冻判定：对范围内敌人进行概率冰冻
    /// </summary>
    private void FreezeCheck()
    {
        foreach (var receiver in affectedEnemies)
        {
            if (receiver == null) continue;
            if (receiver.IsFrozen) continue; // 已冰冻的不重复

            if (Random.value < freezeChance)
            {
                receiver.ApplyFreeze(freezeDuration);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
