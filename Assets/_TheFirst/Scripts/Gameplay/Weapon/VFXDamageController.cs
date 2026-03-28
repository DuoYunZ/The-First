// --- VFXDamageController.cs (调试版) ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VFXDamageController : MonoBehaviour
{

    private int baseDamage; // 【改名】这里存的是基础面板伤害
    private GameObject attacker;
    public WeaponPart sourceWeapon;
    private List<Health> hitTargets = new List<Health>();
    private GameObject hitEffectPrefab;

    // 烈焰模式：强制点燃
    [HideInInspector] public bool forceBurn = false;
    [HideInInspector] public float forceBurnDamage = 5f;
    [HideInInspector] public float forceBurnDuration = 3f;

    [Header("生命周期与伤害窗口")]
    public float totalLifetime = 2f;
    public float damageActiveDuration = 0.2f;
    private Collider col;

    private string weaponName;

    

    void Awake()
    {
        col = GetComponent<Collider>();
    }

    void Start()
    {
        // 预定在总生命周期结束后销毁整个GameObject
        Destroy(gameObject, totalLifetime);

        // 启动一个协程，在指定的伤害窗口期后，禁用碰撞体
        StartCoroutine(DeactivateColliderRoutine());
    }

    private IEnumerator DeactivateColliderRoutine()
    {
        yield return new WaitForSeconds(damageActiveDuration);
        if (col != null) col.enabled = false;
    }
    // 修改 Initialize 方法，让它能接收 WeaponStatBlock
    public void Initialize(int damageInput, GameObject hitVfx, GameObject attacker, WeaponPart weapon)
    {
        this.baseDamage = damageInput;
        this.hitEffectPrefab = hitVfx;
        this.attacker = attacker;

        // 保存引用
        this.sourceWeapon = weapon;

        // 使用 sourceWeapon 访问
        if (this.sourceWeapon != null && this.sourceWeapon.StatBlock != null)
        {
            this.weaponName = this.sourceWeapon.StatBlock.weaponName;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (baseDamage <= 0) return;

        Health targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth != null && targetHealth.CompareTag("Enemy") && !hitTargets.Contains(targetHealth))
        {
            hitTargets.Add(targetHealth);

            // =========================================================
            //  【计算暴击：全局 + 局部】
            // =========================================================
            bool isCurrentHitCrit = false;
            float totalCritRate = 0f;
            float totalCritDmgMult = 1.5f; // 基础爆伤倍率

            // 1. 获取全局属性
            if (PlayerStats.Instance != null)
            {
                totalCritRate += PlayerStats.Instance.critRate;
                totalCritDmgMult = PlayerStats.Instance.critDamage;
            }

            // 2. 获取局部属性 (直接从 launcher 读取)
            if (sourceWeapon != null)
            {
                totalCritRate += sourceWeapon.localCritRateBonus;
                totalCritDmgMult += sourceWeapon.localCritDamageBonus;
            }

            int finalRealDamage = baseDamage;

            // 3. 随机判定
            if (Random.value <= totalCritRate)
            {
                isCurrentHitCrit = true;
                finalRealDamage = Mathf.RoundToInt(baseDamage * totalCritDmgMult);
                Debug.Log($"[VFX] 暴击! {weaponName} -> {targetHealth.name}, 伤害: {finalRealDamage}");
            }
            // =========================================================

            // 产生命中特效
            if (hitEffectPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward));
            }

            // 扣血
            targetHealth.TakeDamage(
                finalRealDamage,
                other.transform.position,
                attacker,
                AttackType.Standard,
                null,
                null,
                weaponName,
                isCurrentHitCrit
            );

            // 统计
            if (BattleStatisticsManager.Instance != null && !string.IsNullOrEmpty(weaponName))
            {
                BattleStatisticsManager.Instance.AddDamage(weaponName, finalRealDamage);
            }

            // =========================================================
            //  特效与元素逻辑 (Smite, Burn, etc.)
            // =========================================================
            if (sourceWeapon == null || sourceWeapon.StatBlock == null) return;

            WeaponStatBlock stats = sourceWeapon.StatBlock; // 替换 launcher
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();

            // 暴击触发落雷
            if (isCurrentHitCrit)
            {
                if (stats.nativeSmiteVfxPrefab != null)
                {
                    Instantiate(stats.nativeSmiteVfxPrefab, targetHealth.transform.position + Vector3.up * 1.5f, Quaternion.identity);
                }
                int lightningDamage = Mathf.RoundToInt(finalRealDamage * 0.5f);
                if (lightningDamage < 1) lightningDamage = 1;
                targetHealth.TakeDamage(lightningDamage, targetHealth.transform.position, attacker, AttackType.Standard);

                // 暴击强制触发连锁(可选)
                int chainCount = stats.baseChainCount > 0 ? stats.baseChainCount : 3;
                sourceWeapon.ChainLightningFromTarget(targetHealth.transform, chainCount, lightningDamage, stats.chainRange);
            }
            // 非暴击但也配置了连锁
            else if (stats.baseChainCount > 0)
            {
                int chainDmg = Mathf.RoundToInt(finalRealDamage * 0.8f);
                sourceWeapon.ChainLightningFromTarget(targetHealth.transform, stats.baseChainCount, chainDmg, stats.chainRange);
            }

            if (receiver == null) return;

            // 击退
            if (stats.nativeKnockback)
            {
                Vector3 pushDir = (targetHealth.transform.position - sourceWeapon.transform.position).normalized;
                pushDir.y = 0;
                receiver.ApplyKnockback(pushDir, stats.nativeKnockbackForce);
            }
            // 燃烧
            if (stats.nativeBurn && !receiver.IsBurning)
            {
                receiver.ApplyBurn(stats.baseDotDamage, stats.baseDotDuration, stats.dotTickInterval, weaponName);
            }
            // 减速
            if (stats.baseSlowPercentage > 0)
            {
                receiver.ApplySlow(stats.baseSlowPercentage, stats.baseSlowDuration, Color.blue);
            }
            // 腐蚀
            if (stats.nativeCorrode)
            {
                receiver.ApplyCorrode(stats.nativeCorrodeMultiplier, 5f, stats.nativeCorrodeColor, weaponName);
            }
            // 烈焰模式强制点燃（由 PlayerBladeAttack 设置）
            if (forceBurn && !receiver.IsBurning)
            {
                int burnDmg = Mathf.RoundToInt(forceBurnDamage);
                if (burnDmg < 1) burnDmg = 1;
                receiver.ApplyBurn(burnDmg, forceBurnDuration, 1f, weaponName);
            }
        }
    }
}