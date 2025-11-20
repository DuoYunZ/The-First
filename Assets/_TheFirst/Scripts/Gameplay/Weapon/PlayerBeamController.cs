// PlayerBeamController.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerBeamController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform target;
    private WeaponPart launcher;
    private WeaponStatBlock stats;

    private GameObject activeImpactVfxInstance;
    private float tickTimer;
    private int damagePerTick;

    // 由 WeaponPart 调用
    public void Initialize(WeaponStatBlock stats, WeaponPart launcher, Transform target) //
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.stats = stats;
        this.launcher = launcher; // [!] 存储 WeaponPart 引用
        this.target = target;

        // (伤害计算保持不变)
        this.damagePerTick = Mathf.CeilToInt((float)stats.beamDamagePerSecond / stats.beamDamageTickRate); //
    }

    void Update()
    {
        // 如果目标丢失，WeaponPart会销毁我们，所以这里只需要保证目标有效时才工作
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            // 安全起见，如果目标丢失也自我销毁
            Destroy(gameObject);
            return;
        }

        // 1. 更新视觉
        Vector3 startPoint = transform.position;
        Transform aimPoint = target.Find("AimTargetPoint");
        Vector3 endPoint = (aimPoint != null) ? aimPoint.position : target.position;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // 2. 更新命中特效
        if (stats.beamImpactVfxPrefab != null)
        {
            if (activeImpactVfxInstance == null)
                activeImpactVfxInstance = Instantiate(stats.beamImpactVfxPrefab, endPoint, Quaternion.identity);
            activeImpactVfxInstance.transform.position = endPoint;
        }

        // 3. 造成伤害
        tickTimer += Time.deltaTime;
        if (tickTimer >= (1f / stats.beamDamageTickRate))
        {
            tickTimer = 0f;
            Health enemyHealth = target.GetComponentInParent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damagePerTick, enemyHealth.transform.position, launcher.gameObject, AttackType.Standard); //

                if (BattleStatisticsManager.Instance != null && stats != null)
                {
                    BattleStatisticsManager.Instance.AddDamage(stats.weaponName, damagePerTick);
                }

                // --- vvv [新增] 能量石逻辑 vvv ---
                // 4. [!] 复制粘贴 `Projectile.cs` 中的 *所有* 能量石逻辑
                StatusEffectReceiver receiver = enemyHealth.GetComponent<StatusEffectReceiver>(); //
                if (receiver != null && launcher != null && launcher.currentStone != null) //
                {
                    EnergyStoneSO stone = launcher.currentStone;
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
    }

    void OnDestroy()
    {
        if (activeImpactVfxInstance != null) Destroy(activeImpactVfxInstance);
    }
}