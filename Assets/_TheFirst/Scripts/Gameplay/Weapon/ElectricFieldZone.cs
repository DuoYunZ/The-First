using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 电磁场区域：磁暴后生成的持续特效区域
/// 对区域内的敌人造成低伤害，并提升玩家对场内敌人的暴击率
/// </summary>
public class ElectricFieldZone : MonoBehaviour
{
    [Header("电磁场配置")]
    private float duration = 3f;         // 持续时间
    private float damagePerTick;         // 每跳伤害
    private float radius = 2f;           // 半径
    private LayerMask enemyLayer;        // 敌人层
    private float tickInterval = 0.5f;   // 伤害间隔
    private float tickTimer = 0f;

    [Header("暴击率提升")]
    [Tooltip("电磁场内敌人受到攻击时额外暴击率")]
    public float critRateBonus = 0.2f;   // +20% 暴击率

    // 当前在场内的敌人
    private HashSet<StatusEffectReceiver> affectedEnemies = new HashSet<StatusEffectReceiver>();

    /// <summary>
    /// 初始化电磁场参数
    /// </summary>
    public void Initialize(float duration, float damage, float radius, LayerMask layer)
    {
        this.duration = duration;
        this.damagePerTick = damage;
        this.radius = radius;
        this.enemyLayer = layer;
    }

    void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            ApplyFieldEffects();
        }
    }

    // 对场内敌人施加伤害和暴击率Debuff
    void ApplyFieldEffects()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);
        foreach (var col in hits)
        {
            if (col == null) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                // 造成低伤害
                if (damagePerTick > 0)
                {
                    h.TakeDamage(Mathf.RoundToInt(damagePerTick), transform.position, gameObject, AttackType.Standard);
                }

                // 施加感电效果（提升被暴击率）
                StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                if (receiver != null)
                {
                    receiver.ApplyShock(tickInterval + 0.1f, null);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
