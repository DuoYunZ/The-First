using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冰雹风暴区域 —— 在范围内每秒对敌人造成伤害
/// 由 PlayerMagicSystem 在冰锥大招释放时生成
/// </summary>
public class HailStormZone : MonoBehaviour
{
    [Header("冰雹风暴参数")]
    [Tooltip("区域半径")]
    public float radius = 8f;
    [Tooltip("持续时间")]
    public float duration = 5f;
    [Tooltip("每次伤害间隔")]
    public float tickInterval = 1f;
    [Tooltip("每次伤害值")]
    public int damagePerTick = 20;
    [Tooltip("敌人层级")]
    public LayerMask enemyLayer;

    private float elapsed = 0f;
    private float tickTimer = 0f;

    void Start()
    {
        // 自动设置敌人层级
        if (enemyLayer == 0)
        {
            enemyLayer = LayerMask.GetMask("Enemies") | LayerMask.GetMask("Enemy");
        }

        // 自动销毁
        Destroy(gameObject, duration);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            DealDamageInRadius();
        }
    }

    /// <summary>
    /// 对半径内所有敌人造成伤害
    /// </summary>
    private void DealDamageInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);

        foreach (var col in hits)
        {
            Health enemyHealth = col.GetComponent<Health>();
            if (enemyHealth == null) enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                // 计算最终伤害（含玩家伤害加成）
                float finalDamage = damagePerTick;
                if (PlayerStats.Instance != null)
                {
                    finalDamage *= PlayerStats.Instance.damageMultiplier;
                }

                enemyHealth.TakeDamage(
                    Mathf.RoundToInt(finalDamage),
                    transform.position,
                    gameObject,              // 攻击者
                    AttackType.Standard      // 攻击类型
                );
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
