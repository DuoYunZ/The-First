using UnityEngine;
using System.Collections; // 必须引用，用于协程
using System.Collections.Generic;
using System.Linq;

public class MagneticStormAura : MonoBehaviour
{
    [Header("领域配置")]
    public float radius = 5f;
    public float pullSpeed = 0f;

    [Header("持续感电 (DOT)")]
    [Tooltip("光环造成伤害的系数")]
    public float dotDamageMultiplier = 0f;
    public float dotInterval = 0.5f;

    [Header("间歇性落雷 (Strike)")]
    [Tooltip("落雷造成伤害的系数")]
    public float lightningDamageMultiplier = 2.0f;
    public float lightningInterval = 2.0f;
    public float lightningSplashRadius = 2.0f;

    [Header("视觉特效")]
    [Tooltip("每道雷电之间的发射间隔 (秒)")]
    public float lightningStrikeDelay = 0.2f; // 【新增】雷电间隔
    public GameObject thunderStrikeVfxPrefab;
    public GameObject electricSparkVfxPrefab;

    private float critRate = 0f; // 【修改】以前是 stunChance，现在存暴击率
    private float critMultiplier = 1.5f; // 暴击伤害倍率 (可配置)
    private float stunDuration = 1.0f;   // 眩晕时间

    [Header("目标层级")]
    public LayerMask enemyLayer;

    // 内部变量
    private int finalDotDamage;
    private int finalLightningDamage;
    private int lightningCount = 1;

    private float dotTimer;
    private float strikeTimer;
    private WeaponPart ownerWeapon;

    public void Initialize(int baseWeaponDamage, float rangeMult, WeaponPart weapon, int count, float critRateParam)
    {
        this.ownerWeapon = weapon;
        this.lightningCount = count;
        this.critRate = critRateParam; // 【修改】保存暴击率

        this.finalDotDamage = Mathf.RoundToInt(baseWeaponDamage * dotDamageMultiplier);
        this.finalLightningDamage = Mathf.RoundToInt(baseWeaponDamage * lightningDamageMultiplier);

        this.radius *= rangeMult;
        transform.localScale = Vector3.one * (this.radius * 0.7f);
        Debug.Log($"[光环初始化] 暴击率: {this.critRate:P0}");
    }

    void Update()
    {
        if (pullSpeed > 0) PullEnemies();

        dotTimer += Time.deltaTime;
        if (dotTimer >= dotInterval)
        {
            dotTimer = 0f;
            ApplyDotDamage();
        }

        strikeTimer += Time.deltaTime;
        if (strikeTimer >= lightningInterval)
        {
            strikeTimer = 0f;
            // 【核心修改】启动协程，而不是直接调用函数
            StartCoroutine(TriggerLightningStrikeRoutine());
        }
    }

    // --- 协程：带间隔的连环落雷 ---
    IEnumerator TriggerLightningStrikeRoutine()
    {
        // 1. 获取范围内所有敌人碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);

        if (hits.Length == 0) yield break;

        // 2. 整理为唯一的 Health 列表 (防止一个敌人有多个碰撞体被算作多人)
        List<Health> validEnemies = new List<Health>();
        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            // 确保活着，且去重
            if (h != null && !h.IsDead && !validEnemies.Contains(h))
            {
                validEnemies.Add(h);
            }
        }

        if (validEnemies.Count == 0) yield break;

        // 3. 【核心修改】构建攻击目标队列
        // 逻辑：如果落雷数是 5，敌人是 2 -> [A, B] + [B, A] + [A] (随机顺序)
        List<Health> strikeQueue = new List<Health>();
        int strikesLeft = lightningCount;

        while (strikesLeft > 0)
        {
            // 打乱当前活着的敌人列表
            var shuffledBatch = validEnemies.OrderBy(x => Random.value).ToList();

            // 这一轮能取多少个？(取 "剩余次数" 和 "敌人总数" 的较小值)
            // 这样保证了每一轮循环都会优先打一圈不同的敌人
            int countToTake = Mathf.Min(strikesLeft, shuffledBatch.Count);

            strikeQueue.AddRange(shuffledBatch.Take(countToTake));
            strikesLeft -= countToTake;
        }

        // 4. 执行连环落雷
        foreach (var target in strikeQueue)
        {
            if (target == null || target.IsDead) continue;

            Vector3 strikePos = target.transform.position;

            // A. 特效 (不变)
            if (thunderStrikeVfxPrefab != null) Instantiate(thunderStrikeVfxPrefab, strikePos, Quaternion.identity);

            // B. 造成溅射伤害 (AOE)
            Collider[] nearby = Physics.OverlapSphere(strikePos, lightningSplashRadius, enemyLayer);
            foreach (var hit in nearby)
            {
                if (hit == null) continue;
                Health h = hit.GetComponentInParent<Health>();
                StatusEffectReceiver receiver = h.GetComponentInParent<StatusEffectReceiver>();

                if (h != null && !h.IsDead)
                {
                    // ==========================================
                    // 【核心修改】暴击判定逻辑
                    // ==========================================

                    // 1. 获取当前的目标的实际暴击率
                    // (如果你的感电 Debuff 会增加怪物被暴击的概率，在这里加上逻辑)
                    // 比如: float effectiveCrit = this.critRate + (receiver.HasShock ? 0.1f : 0f);
                    float effectiveCrit = this.critRate;

                    bool isCrit = Random.value <= this.critRate;
                    int actualDamage = finalLightningDamage;

                    if (isCrit)
                    {
                        // 暴击：伤害增加 + 必定眩晕
                        actualDamage = Mathf.RoundToInt(finalLightningDamage * critMultiplier);

                        // 触发眩晕
                        if (receiver != null)
                        {
                            // 假设你有 ApplyStun，如果没有就用 ApplySlow 替代
                            receiver.ApplyStun(stunDuration, electricSparkVfxPrefab);
                            // Debug.Log("雷击暴击！触发眩晕！");
                        }
                    }

                    // 造成伤害
                    // 注意：如果你的 TakeDamage 支持传入 isCrit 参数来飘黄字，记得传进去
                    h.TakeDamage(actualDamage,                       // 1. 伤害
        strikePos,                          // 2. 位置
        ownerWeapon.gameObject,             // 3. 攻击者
        AttackType.Standard,                // 4. 类型
        projectile: null,                   // 5. Projectile (光环没有子弹脚本)
        beamController: null,               // 6. BeamController (光环没有射线脚本)
        sourceWeaponName: ownerWeapon.StatBlock.weaponName, // 7. 武器名
        isCritical: isCrit                  // 8. 暴击状态 (传进去!)
    );
                }
            }
            yield return new WaitForSeconds(lightningStrikeDelay);
        }
    }

    void PullEnemies()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, radius, enemyLayer);
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                Vector3 targetPos = transform.position;
                Vector3 enemyPos = col.transform.position;
                targetPos.y = enemyPos.y;

                if (Vector3.Distance(enemyPos, targetPos) > 1.5f)
                {
                    col.transform.position = Vector3.MoveTowards(enemyPos, targetPos, pullSpeed * Time.deltaTime);
                }
            }
        }
    }

    void ApplyDotDamage()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, radius, enemyLayer);
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            StatusEffectReceiver status = col.GetComponentInParent<StatusEffectReceiver>();

            if (h != null && !h.IsDead)
            {
                // 【修复】只有当伤害大于 0 (即开启了 DOT 模式) 时，才造成伤害并施加感电
                if (finalDotDamage > 0)
                {
                    h.TakeDamage(finalDotDamage, col.transform.position, ownerWeapon.gameObject, AttackType.Standard);

                    // 将 ApplyShock 移到这里面
                    if (status != null)
                    {
                        status.ApplyShock(1.0f, electricSparkVfxPrefab);
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}