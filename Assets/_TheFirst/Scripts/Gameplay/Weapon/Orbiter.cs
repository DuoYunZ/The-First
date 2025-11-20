// 创建新脚本 Orbiter.cs
using System.Collections.Generic;
using UnityEngine;
public class Orbiter : MonoBehaviour
{
    [Header("自转设置")]
    [Tooltip("轨道物体自身的旋转速度（度/秒）")]
    public float selfRotationSpeed = 1440f; // 在这里设置一个默认的旋转速度

    private int damage = 10;
    private WeaponPart launcher;

    // 如果需要，还可以有独立的冷却计时器，防止它对同一个敌人造成过于频繁的伤害
    private float hitCooldown = 0.5f;
    private float lastHitTime = -1f;
    private Dictionary<Health, float> hitTargetsCooldown = new Dictionary<Health, float>();


    public void Initialize(int damage, WeaponPart part) //
    {
        this.damage = damage;
        this.launcher = part; // <--- [新增]
    }

    void Update()
    {
        // 让轨道物体围绕自己的Y轴（Vector3.up）进行旋转
        // Time.deltaTime 确保旋转是平滑且独立于帧率的
        transform.Rotate(Vector3.up, selfRotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        Health enemyHealth = other.GetComponentInParent<Health>();

        // 检查是否获取到有效的Health组件，以及敌人是否已死亡
        if (enemyHealth == null || enemyHealth.IsDead) return;

        // 【修改后】的冷却判断逻辑
        // 检查1: 字典里是否已经有这个敌人了？
        if (hitTargetsCooldown.ContainsKey(enemyHealth))
        {
            // 如果有，再检查它的独立冷却时间是否已过
            if (Time.time > hitTargetsCooldown[enemyHealth] + hitCooldown)
            {
                // 冷却已过，可以再次造成伤害
                ApplyDamage(enemyHealth);
            }
            // 如果冷却没过，则什么都不做
        }
        else
        {
            // 如果字典里没有这个敌人，说明是第一次命中，直接造成伤害
            ApplyDamage(enemyHealth);
        }
    }
    private void ApplyDamage(Health enemyHealth)
    {
        // 1. 造成伤害
        enemyHealth.TakeDamage(damage, transform.position, this.gameObject);

        // 2. 更新或添加该敌人的命中时间到字典中
        hitTargetsCooldown[enemyHealth] = Time.time;

        // --- [新增] 3. 统计伤害 ---
        if (BattleStatisticsManager.Instance != null && launcher != null && launcher.StatBlock != null)
        {
            BattleStatisticsManager.Instance.AddDamage(launcher.StatBlock.weaponName, damage);
        }
        // ------------------------

        StatusEffectReceiver receiver = enemyHealth.GetComponent<StatusEffectReceiver>();
        if (receiver != null && launcher != null && launcher.currentStone != null) //
        {
            EnergyStoneSO stone = launcher.currentStone; //

            // [!] 在这里，你需要从 Projectile.cs 复制粘贴 *所有* 的
            // 'if (stone.stoneEffects.Contains(...))' 逻辑块

            // 例如：
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
                receiver.GetComponent<Health>()?.TakeDamage(smiteDamage, receiver.transform.position, launcher.gameObject, AttackType.Standard); //
                if (stone.smiteVfxPrefab != null) Instantiate(stone.smiteVfxPrefab, receiver.transform.position, Quaternion.identity); //

                int lightningStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyChain); //
                if (lightningStoneCount >= 2)
                {
                    PlayerStats.Instance.lightningSmiteCounter++; //
                    if (PlayerStats.Instance.lightningSmiteCounter >= 5) //
                    {
                        PlayerStats.Instance.lightningSmiteCounter = 0; //
                        launcher.ChainLightningFromTarget(receiver.transform, stone.chainTargets, smiteDamage, stone.chainRange); //
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
                Vector3 pushDir = (receiver.transform.position - launcher.transform.position).normalized;
                pushDir.y = 0;
                receiver.ApplyKnockback(pushDir, finalKnockbackForce); //
            }

            // 6. 腐蚀石
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode)) //
            {
                float finalCorrodeMultiplier = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode) >= 2) ? stone.corrodeMultiplier_Stacked : stone.corrodeMultiplier; //
                receiver.ApplyCorrode(finalCorrodeMultiplier, 5f, stone.corrodeColor); // (5f 是瞬时debuff的示例持续时间)
            }
        }
    }
}