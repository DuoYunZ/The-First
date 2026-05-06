using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火海区域 —— 在范围内每秒对敌人造成灼烧伤害
/// 由 PlayerMagicSystem 的"燃烧大地"技能生成
/// 自动注册/注销到 PlayerMagicSystem 的火海列表（供"炼狱之焰"计数使用）
/// </summary>
public class FirePoolZone : MonoBehaviour
{
    [Header("火海参数")]
    [Tooltip("区域半径")]
    public float radius = 2f;
    [Tooltip("持续时间")]
    public float duration = 3f;
    [Tooltip("每次伤害间隔")]
    public float tickInterval = 0.5f;
    [Tooltip("每次伤害值")]
    public int damagePerTick = 5;
    [Tooltip("燃烧DOT伤害（附加到敌人身上）")]
    public int burnDotDamage = 3;
    [Tooltip("燃烧DOT持续时间")]
    public float burnDotDuration = 2f;

    private LayerMask enemyLayer;
    private float tickTimer = 0f;
    private float elapsed = 0f;

    void Start()
    {
        enemyLayer = LayerMask.GetMask("Enemies") | LayerMask.GetMask("Enemy");

        // 注册到法师系统火海列表
        if (PlayerMagicSystem.Instance != null)
        {
            PlayerMagicSystem.Instance.RegisterFirePool(gameObject);
        }

        // 自动销毁
        Destroy(gameObject, duration);
    }

    void OnDestroy()
    {
        // 从法师系统注销
        if (PlayerMagicSystem.Instance != null)
        {
            PlayerMagicSystem.Instance.UnregisterFirePool(gameObject);
        }
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
    /// 对半径内所有敌人造成灼烧伤害
    /// </summary>
    private void DealDamageInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);

        // 获取炼狱伤害倍率（炼狱之焰激活时伤害暴涨）
        float damageMultiplier = 1f;
        if (PlayerMagicSystem.Instance != null)
        {
            damageMultiplier = PlayerMagicSystem.Instance.GetFirePoolDamageMultiplier();
        }
        // 玩家伤害加成
        if (PlayerStats.Instance != null)
        {
            damageMultiplier *= PlayerStats.Instance.damageMultiplier;
        }

        foreach (var col in hits)
        {
            Health enemyHealth = col.GetComponent<Health>();
            if (enemyHealth == null) enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                // 直接伤害
                int finalDamage = Mathf.RoundToInt(damagePerTick * damageMultiplier);
                if (finalDamage < 1) finalDamage = 1;

                enemyHealth.TakeDamage(
                    finalDamage,
                    transform.position,
                    gameObject,
                    AttackType.Standard
                );

                // 附加燃烧DOT
                if (burnDotDamage > 0)
                {
                    StatusEffectReceiver receiver = col.GetComponent<StatusEffectReceiver>();
                    if (receiver == null) receiver = col.GetComponentInParent<StatusEffectReceiver>();
                    if (receiver != null)
                    {
                        receiver.ApplyBurn(burnDotDamage, burnDotDuration, 0.5f, "燃烧大地");
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
