using UnityEngine;

/// <summary>
/// 飓风子弹辅助控制器，配合 Projectile 使用。
/// 负责击退、风力回旋、乱流效果。
/// </summary>
public class HurricaneProjectile : MonoBehaviour
{
    [Header("击退效果")]
    public float knockbackForce = 5f;
    public float knockbackRadius = 2f;
    public float knockbackInterval = 0.3f;

    private float knockbackTimer = 0f;
    private WeaponPart sourceWeapon;
    private bool hasTurbulenceTriggered = false; // 乱流等级1时只触发一次
    private bool hasWindReturnTriggered = false; // 风力回旋只触发一次

    /// <summary>
    /// 由武器发射逻辑调用，初始化飓风参数
    /// </summary>
    public void Setup(WeaponPart weapon)
    {
        this.sourceWeapon = weapon;
    }

    void Update()
    {
        knockbackTimer += Time.deltaTime;
        if (knockbackTimer >= knockbackInterval)
        {
            knockbackTimer = 0f;
            ApplyKnockback();
        }
    }

    /// <summary>
    /// 对周围敌人施加击退
    /// </summary>
    private void ApplyKnockback()
    {
        if (knockbackForce <= 0f) return;
        Collider[] cols = Physics.OverlapSphere(transform.position, knockbackRadius, LayerMask.GetMask("Enemies"));
        foreach (var col in cols)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            Vector3 pushDir = (h.transform.position - transform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) continue;
            pushDir.Normalize();
            h.transform.position += pushDir * knockbackForce * Time.deltaTime;
        }
    }

    /// <summary>
    /// 【乱流】命中敌人时由 Projectile.HandleHit 调用
    /// </summary>
    public void TrySpawnTurbulence(Vector3 hitPoint, int currentPierceCount)
    {
        if (sourceWeapon == null) return;
        int turbLevel = sourceWeapon.localTurbulenceLevel;
        if (turbLevel <= 0) return;

        // 等级1：仅首次命中触发
        if (turbLevel == 1 && hasTurbulenceTriggered) return;
        hasTurbulenceTriggered = true;

        SpawnSubHurricane(hitPoint, true);
        Debug.Log($"<color=green>[乱流] 在命中点生成小飓风 (等级:{turbLevel})</color>");
    }

    /// <summary>
    /// 【风力回旋】穿透耗尽时由 Projectile.HandleHit 调用
    /// </summary>
    public void TryWindReturn()
    {
        if (hasWindReturnTriggered) return;
        if (sourceWeapon == null) return;
        if (!sourceWeapon.isWindReturnEnabled) return;

        hasWindReturnTriggered = true;
        SpawnSubHurricane(transform.position, false);
        Debug.Log($"<color=green>[风力回旋] 向随机方向再发一道飓风</color>");
    }

    /// <summary>
    /// 飓风销毁时（包括 lifetime 到期），如果风力回旋还没触发过则触发
    /// </summary>
    void OnDestroy()
    {
        // 子飓风不触发
        Projectile proj = GetComponent<Projectile>();
        if (proj != null && proj.isSubHurricane) return;

        if (!hasWindReturnTriggered)
        {
            TryWindReturn();
        }
    }

    /// <summary>
    /// 生成一个子飓风（不会再次触发风力回旋/乱流）
    /// </summary>
    private void SpawnSubHurricane(Vector3 spawnPos, bool isTurbulence)
    {
        if (sourceWeapon == null || sourceWeapon.StatBlock == null) return;
        // 乱流用小飓风预制件，风力回旋用主飓风预制件
        GameObject prefab = isTurbulence
            ? (sourceWeapon.StatBlock.subHurricanePrefab != null ? sourceWeapon.StatBlock.subHurricanePrefab : sourceWeapon.StatBlock.projectilePrefab)
            : sourceWeapon.StatBlock.projectilePrefab;
        if (prefab == null) return;

        // 随机水平方向
        float angle = Random.Range(0f, 360f);
        Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

        GameObject subGo = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));

        // 标记为子飓风，防止无限递归
        Projectile subProj = subGo.GetComponent<Projectile>();
        if (subProj != null)
        {
            // 继承母飓风的实际穿透（基础 + 升级加成）
            int fullPierce = sourceWeapon.StatBlock.basePierceCount + sourceWeapon.localPierceCountBonus;

            // 乱流小飓风：伤害减半，穿透保持
            // 风力回旋飓风：基础伤害，穿透保持
            int subDamage = isTurbulence
                ? Mathf.Max(1, sourceWeapon.StatBlock.baseDirectDamage / 2)
                : sourceWeapon.StatBlock.baseDirectDamage;
            float subSpeed = sourceWeapon.StatBlock.baseLaunchForce;
            float subLife = sourceWeapon.StatBlock.baseProjectileLifetime * 0.5f;

            subProj.InitializeAsStraight(
                dir, subSpeed, subDamage,
                false, fullPierce,
                subLife,
                null, null,
                0, 0, 0, 0, 0,
                AttackType.Standard,
                sourceWeapon
            );
            subProj.isSubHurricane = true; // 关键：防止递归
        }

        // 子飓风的 HurricaneProjectile 也初始化
        HurricaneProjectile subHc = subGo.GetComponent<HurricaneProjectile>();
        if (subHc != null)
        {
            subHc.Setup(sourceWeapon);
        }
    }
}
