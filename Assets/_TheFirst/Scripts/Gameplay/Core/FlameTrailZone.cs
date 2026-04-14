using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 燃烧区域 — 由 FlameTrailController 在地面生成
/// 对进入范围的敌人施加燃烧效果，在持续时间结束后自动销毁
/// </summary>
public class FlameTrailZone : MonoBehaviour
{
    private int damagePerTick;
    private float duration;
    private float radius;
    private float tickInterval;

    private float lifeTimer;
    private float tickTimer;

    // 缓存已处理的敌人，避免同一帧重复伤害
    private HashSet<Collider> enemiesInZone = new HashSet<Collider>();

    private SphereCollider zoneCollider;
    private bool isInitialized = false;

    /// <summary>
    /// 由 FlameTrailController 调用初始化参数
    /// </summary>
    public void Initialize(int damage, float dur, float rad, float tick)
    {
        damagePerTick = damage;
        duration = dur;
        radius = rad;
        tickInterval = tick;

        lifeTimer = duration;
        tickTimer = 0f;

        // 设置触发器
        zoneCollider = GetComponent<SphereCollider>();
        if (zoneCollider == null)
        {
            zoneCollider = gameObject.AddComponent<SphereCollider>();
        }
        zoneCollider.isTrigger = true;
        zoneCollider.radius = radius;

        // 确保在敌人层上
        gameObject.layer = LayerMask.NameToLayer("Default");

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 生命周期倒计时
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 伤害跳间隔
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            DealDamageToEnemiesInZone();
        }
    }

    /// <summary>
    /// 对区域内所有敌人施加燃烧伤害
    /// </summary>
    private void DealDamageToEnemiesInZone()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, LayerMask.GetMask("Enemies"));
        foreach (var hit in hits)
        {
            Health health = hit.GetComponent<Health>();
            if (health != null && !health.IsDead)
            {
                // 直接造成伤害
                health.TakeDamage(damagePerTick, transform.position, gameObject, AttackType.Standard, null, null, "燃烧轨迹");

                // 同时施加短暂燃烧状态（视觉效果）
                StatusEffectReceiver receiver = hit.GetComponent<StatusEffectReceiver>();
                if (receiver != null && !receiver.IsBurning)
                {
                    receiver.ApplyBurn(damagePerTick, 1.5f, tickInterval, "燃烧轨迹");
                }
            }
        }
    }

    void OnDestroy()
    {
        enemiesInZone.Clear();
    }
}
