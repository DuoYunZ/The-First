using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 环绕+闪电链融合大招：雷暴漩涡
/// 高速旋转的电弧环绕体，吸附周围敌人并释放闪电链
/// </summary>
public class StormOrbiterUltimate : MonoBehaviour
{
    [Header("基础设置")]
    [Tooltip("持续时间")]
    public float duration = 12f;
    [Tooltip("每次伤害")]
    public int damagePerHit = 30;
    [Tooltip("伤害间隔")]
    public float damageInterval = 0.3f;

    [Header("旋转设置")]
    [Tooltip("旋转速度（度/秒）")]
    public float rotationSpeed = 720f;
    [Tooltip("环绕半径")]
    public float orbitRadius = 3f;

    [Header("吸附设置")]
    [Tooltip("吸附范围")]
    public float pullRadius = 10f;
    [Tooltip("吸附速度")]
    public float pullSpeed = 8f;

    [Header("闪电链设置")]
    [Tooltip("链式传导目标数")]
    public int chainTargets = 5;
    [Tooltip("链式传导范围")]
    public float chainRange = 8f;
    [Tooltip("闪电链释放间隔")]
    public float chainInterval = 0.5f;
    [Tooltip("闪电链伤害系数")]
    public float chainDamageMultiplier = 0.5f;

    [Header("特效")]
    [Tooltip("闪电链特效预制件")]
    public GameObject chainLightningVfxPrefab;
    [Tooltip("命中特效预制件")]
    public GameObject impactVfxPrefab;

    // 内部变量
    private Transform ownerTransform;
    private WeaponPart chainLightningWeapon; // 闪电链武器引用，用于调用 ChainLightningFromTarget
    private LayerMask enemyLayer;
    private float damageTimer;
    private float chainTimer;
    private float angle;
    private HashSet<int> damageCooldowns = new HashSet<int>();

    /// <summary>
    /// 初始化融合大招
    /// </summary>
    public void Initialize(WeaponPart orbitalWeapon, WeaponPart lightningWeapon, int damage, float dur)
    {
        ownerTransform = GameManager.Instance?.playerTransform;
        chainLightningWeapon = lightningWeapon;
        damagePerHit = damage;
        duration = dur;

        // 从闪电链武器获取伤害特效
        if (lightningWeapon != null && lightningWeapon.StatBlock != null)
        {
            chainLightningVfxPrefab = lightningWeapon.StatBlock.hitEffectPrefab;
        }

        enemyLayer = LayerMask.GetMask("Enemy") | LayerMask.GetMask("Enemies");

        // 生命周期由 orbitalPivot 管理，枢轴销毁时本组件一起销毁

    }

    void Update()
    {
        // 吸附敌人
        PullEnemies();

        // 持续伤害
        damageTimer += Time.deltaTime;
        if (damageTimer >= damageInterval)
        {
            damageTimer = 0f;
            DealDamageInRange();
        }

        // 释放闪电链
        chainTimer += Time.deltaTime;
        if (chainTimer >= chainInterval)
        {
            chainTimer = 0f;
            ReleaseChainLightning();
        }
    }

    /// <summary>
    /// 吸附范围内敌人向中心靠拢
    /// </summary>
    void PullEnemies()
    {
        Vector3 center = transform.position;
        Collider[] enemies = Physics.OverlapSphere(center, pullRadius, enemyLayer);

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            // 检查是否有 NavMeshAgent（优先控制寻路吸附）
            NavMeshAgent agent = h.GetComponent<NavMeshAgent>();
            Vector3 enemyPos = h.transform.position;
            Vector3 targetPos = center;
            targetPos.y = enemyPos.y; // 保持敌人高度不变

            float dist = Vector3.Distance(enemyPos, targetPos);
            if (dist > 1.5f)
            {
                if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
                {
                    // 有 NavMeshAgent：直接设置目标为漩涡中心
                    agent.SetDestination(targetPos);
                }
                else
                {
                    // 无 NavMeshAgent：直接移动位置
                    h.transform.position = Vector3.MoveTowards(enemyPos, targetPos, pullSpeed * Time.deltaTime);
                }
            }
        }
    }

    /// <summary>
    /// 对范围内敌人造成伤害
    /// </summary>
    void DealDamageInRange()
    {
        Vector3 center = transform.position;
        Collider[] enemies = Physics.OverlapSphere(center, orbitRadius + 2f, enemyLayer);

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            int id = h.gameObject.GetInstanceID();
            if (damageCooldowns.Contains(id)) continue;

            Vector3 hitPoint = (h.AimTargetPoint != null) ? h.AimTargetPoint.position : h.transform.position;
            h.TakeDamage(damagePerHit, hitPoint, gameObject, AttackType.Standard);

            // 命中特效
            if (impactVfxPrefab != null)
            {
                GameObject vfx = Instantiate(impactVfxPrefab, hitPoint, Quaternion.identity);
                Destroy(vfx, 1f);
            }
        }
    }

    /// <summary>
    /// 释放闪电链
    /// </summary>
    void ReleaseChainLightning()
    {
        Vector3 center = transform.position;
        Collider[] enemies = Physics.OverlapSphere(center, pullRadius, enemyLayer);
        if (enemies.Length == 0) return;

        // 找最近的敌人作为闪电链起点
        Transform bestTarget = null;
        float minDist = float.MaxValue;

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            float d = Vector3.Distance(center, col.transform.position);
            if (d < minDist)
            {
                minDist = d;
                bestTarget = h.transform;
            }
        }

        if (bestTarget != null && chainLightningWeapon != null)
        {
            int chainDmg = Mathf.RoundToInt(damagePerHit * chainDamageMultiplier);
            chainLightningWeapon.ChainLightningFromTarget(
                bestTarget,
                chainTargets,
                chainDmg,
                chainRange,
                chainLightningVfxPrefab,
                impactVfxPrefab
            );
        }
    }

    void OnDrawGizmosSelected()
    {
        // 吸附范围
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pullRadius);
        // 伤害范围
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, orbitRadius + 2f);
    }
}
