// --- VFXDamageController.cs (调试版) ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VFXDamageController : MonoBehaviour
{

    private int finalDamage;
    private GameObject attacker;
    private WeaponPart launcher;
    private List<Health> hitTargets = new List<Health>();
    private GameObject hitEffectPrefab; // <--- 新增：用于存储命中特效

    [Header("生命周期与伤害窗口")]
    [Tooltip("特效的总生命周期（秒），之后将销毁自身")]
    public float totalLifetime = 2f;
    [Tooltip("碰撞体保持有效的时间（秒），即伤害判定的窗口期")]
    public float damageActiveDuration = 0.2f; // 例如，只在前0.2秒造成伤害
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
        // 等待伤害窗口期结束
        yield return new WaitForSeconds(damageActiveDuration);

        // 时间一到，立即禁用碰撞体，停止伤害判定
        if (col != null)
        {
            col.enabled = false;
        }
    }
    // 修改 Initialize 方法，让它能接收 WeaponStatBlock
    public void Initialize(int calculatedDamage, GameObject hitVfx, GameObject attacker, WeaponPart launcher)
    {
        this.finalDamage = calculatedDamage;
        this.hitEffectPrefab = hitVfx;
        this.attacker = attacker;
        this.launcher = launcher;

        if (launcher != null && launcher.StatBlock != null)
        {
            this.weaponName = launcher.StatBlock.weaponName;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (finalDamage <= 0) return;

        Health targetHealth = other.GetComponentInParent<Health>();

        // 基础判定：是敌人且未受到过该特效伤害
        if (targetHealth != null && targetHealth.CompareTag("Enemy") && !hitTargets.Contains(targetHealth))
        {
            // 1. 命中特效
            if (hitEffectPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward));
            }

            // 2. 扣血
            hitTargets.Add(targetHealth);
            targetHealth.TakeDamage(finalDamage, other.transform.position, attacker, AttackType.Standard, null, null, weaponName);

            // 3. 统计
            if (BattleStatisticsManager.Instance != null && !string.IsNullOrEmpty(weaponName))
            {
                BattleStatisticsManager.Instance.AddDamage(weaponName, finalDamage);
            }

            // =========================================================
            //  核心逻辑：原生属性(Native) + 能量石(Stone) 混合判定
            // =========================================================
            if (launcher == null) return;
            EnergyStoneSO stone = launcher.currentStone;
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();
            WeaponStatBlock stats = launcher.StatBlock; // 获取数据蓝图

            if (receiver == null || stats == null) return;

            // ---------------------------------------------------------
            // 1. 雷电逻辑 (Chain / Smite)
            // ---------------------------------------------------------
            // 判定：(武器自带Chain > 0) OR (有雷石)
            bool hasChain = (stats.baseChainCount > 0) || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain));

            if (hasChain)
            {
                // 计算雷击伤害：优先用石头数据，没有则用基础伤害的一半
                int smiteDmg = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain))
                    ? Mathf.RoundToInt(stone.smiteDamage * (PlayerStats.Instance.damageMultiplier + stone.damageModifier))
                    : Mathf.RoundToInt(finalDamage * 0.5f);

                GameObject smiteVfx = (stone != null && stone.smiteVfxPrefab != null)
                      ? stone.smiteVfxPrefab
                      : stats.nativeSmiteVfxPrefab;

                // 造成雷击
                targetHealth.TakeDamage(smiteDmg, targetHealth.transform.position, launcher.gameObject, AttackType.Standard);
                if (smiteVfx != null) Instantiate(smiteVfx, targetHealth.transform.position, Quaternion.identity);

                // 计数器与连锁
                int chainStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyChain);
                // 只要是原生雷武，或者有雷石，就参与计数
                if (stats.baseChainCount > 0 || chainStoneCount >= 1)
                {
                    PlayerStats.Instance.lightningSmiteCounter++;
                    if (PlayerStats.Instance.lightningSmiteCounter >= 3)
                    {
                        PlayerStats.Instance.lightningSmiteCounter = 0;

                        // 确定连锁参数
                        int cCount = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) ? stone.chainTargets : stats.baseChainCount;
                        float cRange = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) ? stone.chainRange : stats.chainRange;

                        launcher.ChainLightningFromTarget(targetHealth.transform, cCount, smiteDmg, cRange);
                    }
                }
            }

            // ---------------------------------------------------------
            // 2. 火焰逻辑 (Burn)
            // ---------------------------------------------------------
            // 判定：(武器自带Burn) OR (有火石)
            bool hasBurn = stats.nativeBurn || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn));

            if (hasBurn)
            {
                int fireStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyBurn);
                // 确定燃烧参数：优先用石头，否则用 StatBlock 的 baseDotDamage
                int bDmg = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnDamage : stats.baseDotDamage;
                float bDur = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnDuration : stats.baseDotDuration;
                float bTick = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnTickInterval : stats.dotTickInterval;

                // 堆叠引爆逻辑 (仅当有火石堆叠时触发)
                if (fireStoneCount >= 2 && receiver.IsBurning)
                {
                    receiver.Ignite();
                }
                else if (!receiver.IsBurning)
                {
                    receiver.ApplyBurn(bDmg, bDur, bTick, weaponName);
                }
            }

            // ---------------------------------------------------------
            // 3. 击退逻辑 (Knockback)
            // ---------------------------------------------------------
            // 判定：(武器自带Knockback) OR (有风石)
            bool hasKnockback = stats.nativeKnockback || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback));

            if (hasKnockback)
            {
                float kForce = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback)) ? stone.knockbackForce : stats.nativeKnockbackForce;

                // 堆叠加成
                int windStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback);
                if (windStoneCount >= 2 && stone != null) kForce = stone.knockbackForce_Stacked;

                Vector3 pushDir = (targetHealth.transform.position - launcher.transform.position).normalized;
                pushDir.y = 0;
                receiver.ApplyKnockback(pushDir, kForce);
            }

            // ---------------------------------------------------------
            // 4. 寒冰/减速逻辑 (Slow/Ice)
            // ---------------------------------------------------------
            // 这里我们复用 baseSlowPercentage 作为原生减速
            bool hasSlow = (stats.baseSlowPercentage > 0) || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow));

            if (hasSlow)
            {
                float sPct = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow)) ? stone.slowPercentage : stats.baseSlowPercentage;
                float sDur = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow)) ? stone.slowDuration : stats.baseSlowDuration;
                Color sColor = (stone != null) ? stone.slowColor : Color.blue;

                receiver.ApplySlow(sPct, sDur, sColor);

                // 冰冻只在有石头堆叠时触发
                int iceStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplySlow);
                if (iceStoneCount >= 2 && receiver.IsSlowed && !receiver.IsStunned && stone != null)
                {
                    if (Random.value <= stone.freezeChance) receiver.ApplyStun(stone.freezeDuration, stone.freezeVfxPrefab);
                }
            }
        }
    }
}