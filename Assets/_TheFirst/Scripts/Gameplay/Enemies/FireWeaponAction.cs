// --- FireWeaponAction.cs (最终正确版) ---
using UnityEngine;
using System.Collections;

public class FireWeaponAction : Node
{
    [Header("武器设置")]
    [Tooltip("拖入一个WeaponStatBlock数据资产，定义本次攻击的类型")]
    public WeaponStatBlock weaponToFire;

    [Header("攻击参数")]
    [Tooltip("在一次行动中，连续射击的次数")]
    public int burstCount = 1;
    [Tooltip("每次连续射击之间的间隔时间")]
    public float timeBetweenBursts = 0.2f;

    [Header("发射点")]
    [Tooltip("（可选）指定一个发射点，如果不指定，则使用Boss自身位置")]
    public Transform firePoint;

    // 内部状态
    private int burstsFired = 0;
    private float timer;
    private bool isFiring = false;

    private Transform selfTransform;

    void Awake()
    {
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null)
        {
            selfTransform = bossRb.transform;
        }
    }

    public override NodeState Evaluate()
    {
        if (weaponToFire == null || selfTransform == null)
        {
            return NodeState.FAILURE;
        }

        if (!isFiring)
        {
            isFiring = true;
            burstsFired = 0;
            timer = 0;
            Fire();
            burstsFired++;
        }

        if (burstsFired < burstCount)
        {
            timer += Time.deltaTime;
            if (timer >= timeBetweenBursts)
            {
                timer = 0;
                Fire();
                burstsFired++;
            }
        }

        if (burstsFired >= burstCount)
        {
            isFiring = false;
            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }

    private void Fire()
    {
        if (weaponToFire.projectilePrefab == null) return;

        Transform spawnPoint = firePoint != null ? firePoint : selfTransform;
        GameObject projectileGO = Instantiate(weaponToFire.projectilePrefab, spawnPoint.position, spawnPoint.rotation);

        Projectile projectileScript = projectileGO.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // 【核心修正】根据你提供的 WeaponStatBlock.cs 和 Projectile.cs 的确切变量名和方法进行调用

            // 检查武器行为类型，我们先只处理最基础的直线弹(Standard/Pierce)
            if (weaponToFire.behavior == WeaponBehaviorType.Standard || weaponToFire.behavior == WeaponBehaviorType.Pierce)
            {
                projectileScript.InitializeAsStraight(
                    spawnPoint.forward,                         // dir: 发射方向
                    weaponToFire.baseLaunchForce,               // spd: 使用 baseLaunchForce 作为速度
                    weaponToFire.baseDirectDamage,              // directDmg: 使用 baseDirectDamage
                    true,                                       // isEnemyBullet: true
                    weaponToFire.basePierceCount,               // pierce: 使用 basePierceCount
                    weaponToFire.baseProjectileLifetime,        // life: 使用 baseProjectileLifetime
                    weaponToFire.shieldImpactEffectPrefab,      // shieldVfx: 使用 shieldImpactEffectPrefab
                    weaponToFire.defaultImpactEffectPrefab,     // defaultVfx: 使用 defaultImpactEffectPrefab
                    weaponToFire.baseDotDamage,                 // dotDmg: 使用 baseDotDamage
                    weaponToFire.baseDotDuration,               // dotDur: 使用 baseDotDuration
                    weaponToFire.dotTickInterval,               // dotTick: 使用 dotTickInterval
                    weaponToFire.baseSlowPercentage,            // slowPct: 使用 baseSlowPercentage
                    weaponToFire.baseSlowDuration,              // slowDur: 使用 baseSlowDuration
                    AttackType.Standard                         // type: 暂时使用默认的标准攻击类型
                );
            }
            // 后续可以在这里添加对其他行为类型 (如抛物线、追踪弹等) 的支持
            // else if (weaponToFire.behavior == WeaponBehaviorType.ParabolicAOE) { ... }
        }
        else
        {
            Debug.LogWarning("FireWeaponAction: 实例化的子弹上没有找到Projectile脚本！", projectileGO);
        }
    }
}