using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 辅助型光环脚本，支持低伤害DOT、减速、易伤标记、回血脉动。
/// 所有数值由 SO Inspector 的 value 字段直接控制，无需改代码。
/// 生命汲取为大招技能，由 UltimateManager 控制。
/// </summary>
public class SupportAura : MonoBehaviour
{
    [Header("光环配置")]
    public float radius = 5f;
    public float dotInterval = 1.0f;
    private int damagePerTick;

    [Header("特效")]
    public GameObject auraVfxPrefab;
    private GameObject currentVfx;

    [Header("目标层级")]
    public LayerMask enemyLayer;

    // 内部状态
    private float dotTimer;
    private float healTimer;
    private WeaponPart ownerWeapon;
    private float baseScale;         // 初始化时的基础缩放值

    // 从 WeaponPart 读取的实际数值（由 SO value 控制）
    private float healAmount;         // 每60秒回血量（0=未开启）
    private float slowPercent;        // 减速百分比（0=未开启，如25=25%减速）
    private float fragilePercent;     // 增伤百分比（0=未开启，如8=8%增伤）

    // 大招状态（由 UltimateManager 控制）
    [HideInInspector] public bool isLifeSiphonActive = false;
    [HideInInspector] public bool isRadiusBoostActive = false;  // 大招范围增大
    [HideInInspector] public bool isPushActive = false;         // 连携大招：推开敌人
    [HideInInspector] public float pushForce = 3f;              // 推开力度

    /// <summary>
    /// 初始化辅助光环
    /// </summary>
    public void Initialize(int baseDamage, float rangeMult, WeaponPart weapon)
    {
        this.ownerWeapon = weapon;
        // baseDamage 已经由 SetupAura 计算好（含 damageMultiplier + localDamageBonus）
        this.damagePerTick = baseDamage;
        this.radius *= rangeMult;

        // 从 WeaponPart 读取 SO 设置的实际数值
        if (weapon != null)
        {
            this.healAmount = weapon.auraHealAmount;
            this.slowPercent = weapon.auraSlowPercent;
            this.fragilePercent = weapon.auraFragilePercent;

            // 脆弱印记副作用：触发间隔增加（增伤越高惩罚越低）
            if (this.fragilePercent > 0)
            {
                // 基础惩罚15%，每多1%增伤减少0.5%惩罚，最低5%
                float penalty = Mathf.Max(0.05f, 0.15f - (this.fragilePercent - 8f) * 0.005f);
                this.dotInterval *= (1f + penalty);
            }
        }

        // 设置范围缩放
        this.baseScale = this.radius * 2f;
        transform.localScale = Vector3.one * baseScale;

        // 生成 VFX
        if (auraVfxPrefab != null && currentVfx == null)
        {
            currentVfx = Instantiate(auraVfxPrefab, transform);
            currentVfx.transform.localPosition = Vector3.zero;
            currentVfx.transform.localScale = Vector3.one;
        }

        // 确保所有粒子系统使用 Hierarchy 缩放模式（这样 transform.localScale 能正确传播）
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    void Update()
    {
        // 0. 实时更新缩放（大招范围增大时 VFX 跟着放大）
        float scaleMult = isRadiusBoostActive ? 1.5f : 1f;
        float targetScale = baseScale * scaleMult;
        float currentScale = transform.localScale.x;
        float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 5f);
        transform.localScale = Vector3.one * newScale;


        // 1. 伤害与 Debuff 逻辑 (DOT)
        dotTimer += Time.deltaTime;
        if (dotTimer >= dotInterval)
        {
            dotTimer = 0f;
            ApplyAuraEffects();
        }

        // 2. 生命脉动回血逻辑（每60秒触发一次）
        if (healAmount > 0)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= 60f)
            {
                healTimer = 0f;
                TriggerHealingPulse();
            }
        }
    }

    /// <summary>
    /// 对光环范围内的敌人施加效果
    /// </summary>
    private void ApplyAuraEffects()
    {
        // 大招范围增大（1.5倍）
        float effectiveRadius = isRadiusBoostActive ? radius * 1.5f : radius;
        Collider[] enemies = Physics.OverlapSphere(transform.position, effectiveRadius, enemyLayer);
        foreach (var col in enemies)
        {
            if (col == null) continue;

            Health h = col.GetComponent<Health>();
            if (h == null) h = col.GetComponentInParent<Health>();

            StatusEffectReceiver status = col.GetComponent<StatusEffectReceiver>();
            if (status == null) status = col.GetComponentInParent<StatusEffectReceiver>();

            if (h != null && !h.IsDead)
            {
                // 造成基础伤害
                if (damagePerTick > 0)
                {
                    h.TakeDamage(damagePerTick, col.transform.position, ownerWeapon.gameObject, AttackType.Standard);
                }

                // 连携大招：推开敌人（平滑过渡）
                if (isPushActive)
                {
                    Vector3 pushDir = (h.transform.position - transform.position);
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.01f) pushDir = Random.insideUnitSphere;
                    pushDir.Normalize();
                    StartCoroutine(SmoothPushEnemy(h.transform, pushDir, pushForce, 0.3f));
                }

                if (status != null)
                {
                    // 迟缓力场（使用独立方法，不覆盖冰系减速）
                    if (slowPercent > 0)
                    {
                        float slowRate = 1f - (slowPercent / 100f); // 25% → 0.75倍速
                        status.ApplyAuraSlow(slowRate, dotInterval * 1.5f);
                    }

                    // 脆弱印记（提升受到的伤害）
                    if (fragilePercent > 0)
                    {
                        float fragileBonus = fragilePercent / 100f; // 8% → 0.08
                        status.ApplyFragileMark(fragileBonus, dotInterval * 1.5f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 触发生命脉动回血
    /// </summary>
    private void TriggerHealingPulse()
    {
        if (PlayerStats.Instance == null) return;
        Health playerHealth = PlayerStats.Instance.GetComponent<Health>();
        if (playerHealth == null) return;

        int heal = Mathf.RoundToInt(healAmount);
        playerHealth.Heal(heal);
    }

    // === 生命汲取大招：敌人在光环内死亡时回血（由 UltimateManager 开启） ===

    private void OnEnable()
    {
        Health.OnEnemyDied += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        Health.OnEnemyDied -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(Health deadEnemy)
    {
        // 只在大招激活期间生效
        if (!isLifeSiphonActive || deadEnemy == null) return;

        // 大招期间使用增大后的范围判定
        float effectiveRadius = isRadiusBoostActive ? radius * 1.5f : radius;
        float distSq = (deadEnemy.transform.position - transform.position).sqrMagnitude;
        if (distSq <= effectiveRadius * effectiveRadius)
        {
            if (PlayerStats.Instance == null) return;
            Health playerHealth = PlayerStats.Instance.GetComponent<Health>();
            if (playerHealth == null) return;

            int maxHp = playerHealth.GetMaxHealth();
            int heal = Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.01f));
            playerHealth.Heal(heal);
        }
    }
    /// <summary>
    /// 平滑推开敌人协程
    /// </summary>
    private IEnumerator SmoothPushEnemy(Transform target, Vector3 direction, float distance, float duration)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        float moved = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float step = (distance / duration) * Time.deltaTime;
            moved += step;
            if (moved > distance) step -= (moved - distance);
            target.position += direction * step;
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
