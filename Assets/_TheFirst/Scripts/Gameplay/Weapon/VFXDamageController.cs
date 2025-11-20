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

        Health targetHealth = other.GetComponentInParent<Health>(); //

        // --- vvv [修改] vvv ---
        // (检查 Tag 和 hitTargets 列表)
        if (targetHealth != null && targetHealth.CompareTag("Enemy") && !hitTargets.Contains(targetHealth)) //
        {
            // (在造成伤害前，先在命中点生成特效 - 保持不变)
            if (hitEffectPrefab != null) //
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position); //
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward)); //
            }

            hitTargets.Add(targetHealth); //
            targetHealth.TakeDamage(finalDamage, other.transform.position, attacker, AttackType.Standard); //

            if (BattleStatisticsManager.Instance != null && !string.IsNullOrEmpty(weaponName))
            {
                BattleStatisticsManager.Instance.AddDamage(weaponName, finalDamage);
            }

            // --- vvv [新增] 能量石逻辑 (从 Projectile.cs 复制而来) vvv ---
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>(); //
            if (receiver != null && launcher != null && launcher.currentStone != null) //
            {
                EnergyStoneSO stone = launcher.currentStone; //

                // 1. 火焰石
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) //
                {
                    int fireStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyBurn); //
                    if (fireStoneCount >= 2 && receiver.IsBurning) //
                        receiver.Ignite(); //
                    else if (!receiver.IsBurning)
                        receiver.ApplyBurn(stone.burnDamage, stone.burnDuration, stone.burnTickInterval); //
                }

                // 2. 寒冰石
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow)) //
                {
                    receiver.ApplySlow(stone.slowPercentage, stone.slowDuration, stone.slowColor); //
                    int iceStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplySlow); //
                    if (iceStoneCount >= 2 && receiver.IsSlowed && !receiver.IsStunned && Random.value <= stone.freezeChance) //
                        receiver.ApplyStun(stone.freezeDuration, stone.freezeVfxPrefab); //
                }

                // 3. 雷电石
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) //
                {
                    int smiteDamage = Mathf.RoundToInt(stone.smiteDamage * (PlayerStats.Instance.damageMultiplier + stone.damageModifier) + PlayerStats.Instance.flatDamageBonus); //
                    targetHealth.TakeDamage(smiteDamage, targetHealth.transform.position, launcher.gameObject, AttackType.Standard); //
                    if (stone.smiteVfxPrefab != null) Instantiate(stone.smiteVfxPrefab, targetHealth.transform.position, Quaternion.identity); //

                    int lightningStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyChain); //
                    if (lightningStoneCount >= 2)
                    {
                        PlayerStats.Instance.lightningSmiteCounter++; //
                        if (PlayerStats.Instance.lightningSmiteCounter >= 5) //
                        {
                            PlayerStats.Instance.lightningSmiteCounter = 0; //
                            launcher.ChainLightningFromTarget(targetHealth.transform, stone.chainTargets, smiteDamage, stone.chainRange); //
                        }
                    }
                }

                // 4. 大地石 (眩晕)
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyStun)) //
                {
                    float finalStunChance = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyStun) >= 2) ? stone.stunChance_Stacked : stone.stunChance; //
                    if (Random.value <= finalStunChance && !receiver.IsStunned)
                        receiver.ApplyStun(stone.stunDuration); //
                }

                // 5. 风暴石 (击退)
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback)) //
                {
                    float finalKnockbackForce = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback) >= 2) ? stone.knockbackForce_Stacked : stone.knockbackForce; //
                    Vector3 pushDir = (targetHealth.transform.position - launcher.transform.position).normalized;
                    pushDir.y = 0;
                    receiver.ApplyKnockback(pushDir, finalKnockbackForce); //
                }

                // 6. 腐蚀石
                if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode)) //
                {
                    float finalCorrodeMultiplier = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode) >= 2) ? stone.corrodeMultiplier_Stacked : stone.corrodeMultiplier; //
                    receiver.ApplyCorrode(finalCorrodeMultiplier, 5f, stone.corrodeColor); // (5f 是示例持续时间)
                }
            }
            // --- ^^^ [新增] 能量石逻辑 ^^^ ---
        }
    }
}