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

    private float critRate = 0f; // 暴击率
    private float critMultiplier = 1.5f; // 暴击伤害倍率
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

    // === 技能树扩展属性 ===
    private int lightningRepeatCount = 0;    // 连续雷击次数（每次落雷后0.3秒再落一道）
    private bool magneticStormEnabled = false; // 磁暴开关
    private float magneticStormDamageBonus = 0f; // 磁暴伤害/范围加成
    private float magneticStormAreaBonus = 0f;   // 磁暴范围加成
    private bool electricFieldEnabled = false;   // 电磁场开关
    private float electricFieldDamageBonus = 0f; // 电磁场伤害加成
    private float electricFieldDurationBonus = 0f; // 电磁场持续时间加成

    [Header("技能树特效预制件")]
    [Tooltip("磁暴爆炸特效")]
    public GameObject magneticStormVfxPrefab;
    [Tooltip("电磁场持续特效")]
    public GameObject electricFieldVfxPrefab;

    public void Initialize(int baseWeaponDamage, float rangeMult, WeaponPart weapon, int count, float critRateParam)
    {
        this.ownerWeapon = weapon;
        this.lightningCount = count;
        this.critRate = critRateParam;

        this.finalDotDamage = Mathf.RoundToInt(baseWeaponDamage * dotDamageMultiplier);
        this.finalLightningDamage = Mathf.RoundToInt(baseWeaponDamage * lightningDamageMultiplier);

        this.radius *= rangeMult;
        transform.localScale = Vector3.one * (this.radius * 0.7f);

        // 从 WeaponPart 读取技能树升级属性
        if (weapon != null)
        {
            this.lightningRepeatCount = weapon.localLightningRepeatCount;
            this.stunDuration += weapon.localStunDurationBonus;
            this.magneticStormEnabled = weapon.isMagneticStormEnabled;
            this.magneticStormDamageBonus = weapon.localDamageBonus;
            this.magneticStormAreaBonus = weapon.localAreaBonus;
            this.electricFieldEnabled = weapon.isElectricFieldEnabled;
            this.electricFieldDamageBonus = weapon.localElectricFieldDamageBonus;
            this.electricFieldDurationBonus = weapon.localElectricFieldDurationBonus;
        }

        Debug.Log($"[光环初始化] 暴击率:{this.critRate:P0}, 连续雷击:{lightningRepeatCount}, 磁暴:{magneticStormEnabled}, 电磁场:{electricFieldEnabled}");
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
        
        // 【调试】每秒输出一次计时状态
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"<color=magenta>[雷击Update] strikeTimer={strikeTimer:F1}/{lightningInterval}, ownerWeapon={(ownerWeapon != null ? ownerWeapon.name : "NULL")}</color>");
        }
        
        if (strikeTimer >= lightningInterval)
        {
            strikeTimer = 0f;
            Debug.Log("<color=lime>[雷击] 触发落雷协程!</color>");
            // 【核心修改】启动协程，而不是直接调用函数
            StartCoroutine(TriggerLightningStrikeRoutine());
        }
    }

    // --- 协程：带间隔的连环落雷 ---
    IEnumerator TriggerLightningStrikeRoutine()
    {
        // 1. 获取范围内所有敌人碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);
        
        // 【调试】输出检测结果
        Debug.Log($"<color=orange>[雷击检测] 位置:{transform.position}, 半径:{radius}, Layer:{enemyLayer.value}, 检测到碰撞体数量:{hits.Length}</color>");

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

            // 使用 AimTargetPoint 作为落雷位置（而非脚底）
            Vector3 strikePos = (target.AimTargetPoint != null) ? target.AimTargetPoint.position : target.transform.position;

            // A. 特效挂到目标身上
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
                    // 感电状态下暴击率提升
                    float effectiveCrit = this.critRate + (receiver != null && receiver.IsShocked ? 0.2f : 0f);

                    bool isCrit = Random.value <= effectiveCrit;
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

            // === 连续雷击：每次落雷后0.3秒再落一道 ===
            for (int r = 0; r < lightningRepeatCount; r++)
            {
                yield return new WaitForSeconds(0.3f);
                // 随机选新目标，没有就打同一个
                Health repeatTarget = GetRandomAliveEnemy(validEnemies);
                if (repeatTarget == null) repeatTarget = target;
                if (repeatTarget == null || repeatTarget.IsDead) continue;

                Vector3 repeatPos = repeatTarget.transform.position;
                if (thunderStrikeVfxPrefab != null) Instantiate(thunderStrikeVfxPrefab, repeatPos, Quaternion.identity);
                DealLightningDamage(repeatPos);
            }

            // === 磁暴：落雷后触发一次性范围爆炸 ===
            if (magneticStormEnabled)
            {
                float stormRadius = lightningSplashRadius * (1f + magneticStormAreaBonus);
                int stormDamage = Mathf.RoundToInt(finalLightningDamage * 0.5f * (1f + magneticStormDamageBonus));
                if (magneticStormVfxPrefab != null)
                {
                    GameObject stormVfx = Instantiate(magneticStormVfxPrefab, strikePos, Quaternion.identity);
                    // 磁暴特效按范围缩放
                    stormVfx.transform.localScale = Vector3.one * (stormRadius / lightningSplashRadius);
                }

                Collider[] stormHits = Physics.OverlapSphere(strikePos, stormRadius, enemyLayer);
                foreach (var sh in stormHits)
                {
                    Health shHealth = sh.GetComponentInParent<Health>();
                    if (shHealth != null && !shHealth.IsDead)
                    {
                        shHealth.TakeDamage(stormDamage, strikePos, ownerWeapon.gameObject, AttackType.Standard);
                    }
                }

                // === 电磁场：磁暴后生成持续区域，提升暴击率 ===
                if (electricFieldEnabled && electricFieldVfxPrefab != null)
                {
                    float fieldDuration = 3f + electricFieldDurationBonus;
                    GameObject field = Instantiate(electricFieldVfxPrefab, strikePos, Quaternion.identity);
                    var fieldScript = field.GetComponent<ElectricFieldZone>();
                    if (fieldScript != null)
                    {
                        float fieldDamage = stormDamage * 0.2f * (1f + electricFieldDamageBonus);
                        fieldScript.Initialize(fieldDuration, fieldDamage, stormRadius, enemyLayer);
                    }
                    Destroy(field, fieldDuration);
                }
            }
        }
    }

    // === 大招 BUFF：雷霆之力（临时增加暴击率）===
    private float thunderBuffCritBonus = 0f;

    public void ApplyThunderBuff(float critBonus, float duration)
    {
        thunderBuffCritBonus = critBonus;
        critRate += critBonus;
        Debug.Log($"<color=yellow>[雷霆之力] 暴击率临时 +{critBonus:P0}，持续 {duration} 秒，当前暴击率: {critRate:P0}</color>");
        StartCoroutine(ThunderBuffRoutine(critBonus, duration));
    }

    IEnumerator ThunderBuffRoutine(float critBonus, float duration)
    {
        yield return new WaitForSeconds(duration);
        critRate -= critBonus;
        thunderBuffCritBonus = 0f;
        Debug.Log($"<color=yellow>[雷霆之力] BUFF 结束，暴击率恢复为: {critRate:P0}</color>");
    }

    // 辅助：随机获取一个存活敌人
    Health GetRandomAliveEnemy(List<Health> enemies)
    {
        var alive = enemies.FindAll(e => e != null && !e.IsDead);
        if (alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }

    // 辅助：对落雷点造成溅射伤害
    void DealLightningDamage(Vector3 pos)
    {
        Collider[] nearby = Physics.OverlapSphere(pos, lightningSplashRadius, enemyLayer);
        foreach (var hit in nearby)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                float effectiveCrit = critRate + (receiver != null && receiver.IsShocked ? 0.2f : 0f);
                bool isCrit = Random.value <= effectiveCrit;
                int dmg = isCrit ? Mathf.RoundToInt(finalLightningDamage * critMultiplier) : finalLightningDamage;
                if (isCrit && receiver != null)
                {
                    receiver.ApplyStun(stunDuration, electricSparkVfxPrefab);
                }
                h.TakeDamage(dmg, pos, ownerWeapon.gameObject, AttackType.Standard,
                    null, null, ownerWeapon.StatBlock.weaponName, isCrit);
            }
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