using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(Animator))]
public class SuperMechAI : MonoBehaviour
{
    private enum MechState { Following, Attacking }
    private MechState currentState = MechState.Following;

    [Header("机甲行动参数")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 5f;
    public float followDistance = 4.0f;
    public float attackRange = 20f;

    [Header("传送机制 (Teleport)")]
    public float teleportDistance = 25f; // 【新增】超过这个距离就传送
    public GameObject teleportVfxPrefab; // 【新增】传送时的特效

    [Header("机甲结构组件")]
    public Transform aimBone;
    public Transform leftShoulderMuzzle;
    public Transform rightShoulderMuzzle;
    public Transform chestMuzzle;

    [Header("骨骼修正")]
    public Vector3 boneCorrection = Vector3.zero;

    [Header("旋转限制")]
    public float maxTorsoAngle = 60f;

    [Header("攻击A：肩部导弹齐射")]
    public GameObject missilePrefab;
    public GameObject missileHitVfxPrefab;
    public GameObject muzzleFlashVfxPrefab; // 【新增】枪口开火特效
    public int missileVolleys = 6;
    public float volleyInterval = 0.2f;
    public float missileUpwardForce = 15f;

    [Header("导弹发射细节")]
    public float muzzlePosOffset = 0.3f;
    public float trajectorySpread = 1.5f;

    [Header("攻击B：胸部集束激光")]
    public float laserChargeTime = 1.0f;
    public float laserDuration = 2.5f;
    public float laserWidth = 2.0f;
    public Color laserColor = new Color(1f, 0.4f, 0f);
    public LayerMask enemyLayer;
    public GameObject chestChargeVfx;

    [Header("激光视觉优化")]
    public GameObject laserImpactVfxPrefab;
    public float minAttackDistance = 5.0f;

    [Header("循环设置")]
    public float attackCooldown = 3.0f;

    // 内部变量
    private Transform ownerPlayer;
    private WeaponPart ownerWeapon;
    private Transform currentTarget;
    private LineRenderer lineRenderer;
    private Animator animator;
    private bool isPerformingAttackRoutine = false;
    private GameObject currentLaserImpactVfx;

    private int missileDamage;
    private int laserDamage;
    private float laserTickRate;
    private float damageTickTimer;

    public void Initialize(WeaponPart weapon, WeaponStatBlock stats, Transform owner)
    {
        this.ownerWeapon = weapon;
        this.ownerPlayer = owner;
        float multiplier = PlayerStats.Instance.damageMultiplier;
        this.missileDamage = Mathf.RoundToInt(stats.baseDirectDamage * 0.4f * multiplier);
        this.laserDamage = Mathf.RoundToInt(stats.baseDirectDamage * 1.0f * multiplier);
        this.laserTickRate = stats.beamDamageTickRate > 0 ? stats.beamDamageTickRate : 5f;
        if (stats.beamDuration > 0) this.laserDuration = stats.beamDuration;
    }

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        animator = GetComponent<Animator>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
    }

    void Update()
    {
        if (ownerPlayer == null) return;

        switch (currentState)
        {
            case MechState.Following:
                HandleMovement();
                FindNewTarget();
                if (currentTarget != null && !isPerformingAttackRoutine)
                {
                    StartCoroutine(FullAttackRoutine());
                }
                break;
            case MechState.Attacking:
                if (animator != null) animator.SetBool("IsMoving", false);
                break;
        }
    }

    void LateUpdate()
    {
        if (currentTarget != null && aimBone != null)
        {
            if (Vector3.Distance(transform.position, currentTarget.position) <= attackRange + 5f)
                AimBoneLateUpdate();
        }
    }

    IEnumerator FullAttackRoutine()
    {
        isPerformingAttackRoutine = true;
        currentState = MechState.Attacking;
        if (animator != null) animator.SetBool("IsMoving", false);

        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < missileVolleys; i++)
        {
            // 发射前再次确认目标，防止目标死亡导致全部打空
            if (currentTarget == null) FindNewTarget();

            FireSingleMissile(leftShoulderMuzzle);
            yield return new WaitForSeconds(0.05f);
            FireSingleMissile(rightShoulderMuzzle);
            yield return new WaitForSeconds(volleyInterval);
        }

        yield return new WaitForSeconds(0.5f);

        if (currentTarget == null) FindNewTarget();
        GameObject vfxInstance = null;
        if (chestChargeVfx != null && chestMuzzle != null)
            vfxInstance = Instantiate(chestChargeVfx, chestMuzzle.position, chestMuzzle.rotation, chestMuzzle);

        yield return new WaitForSeconds(laserChargeTime);
        if (vfxInstance != null) Destroy(vfxInstance);

        currentState = MechState.Following;
        if (animator != null) animator.SetBool("IsMoving", true);

        float laserTimer = laserDuration;
        lineRenderer.enabled = true;
        if (laserImpactVfxPrefab != null)
        {
            if (currentLaserImpactVfx == null) currentLaserImpactVfx = Instantiate(laserImpactVfxPrefab);
            currentLaserImpactVfx.SetActive(true);
        }

        while (laserTimer > 0)
        {
            if (currentTarget == null) FindNewTarget();
            FireLaserTick(laserDuration - laserTimer);
            laserTimer -= Time.deltaTime;
            yield return null;
        }

        lineRenderer.enabled = false;
        if (currentLaserImpactVfx != null) currentLaserImpactVfx.SetActive(false);
        yield return new WaitForSeconds(attackCooldown);
        isPerformingAttackRoutine = false;
    }

    void HandleMovement()
    {
        float distToPlayer = Vector3.Distance(transform.position, ownerPlayer.position);

        // --- 【新增】传送逻辑 ---
        if (distToPlayer > teleportDistance)
        {
            TeleportToPlayer();
            return; // 传送完这一帧就结束，下一帧再处理移动
        }
        // -----------------------

        bool shouldMove = distToPlayer > followDistance;
        if (shouldMove)
        {
            Vector3 dir = (ownerPlayer.position - transform.position).normalized;
            dir.y = 0;
            transform.position += dir * moveSpeed * Time.deltaTime;

            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
            }
            if (aimBone != null && currentTarget == null)
                aimBone.localRotation = Quaternion.Slerp(aimBone.localRotation, Quaternion.identity, Time.deltaTime * 2f);
        }
        if (animator != null) animator.SetBool("IsMoving", shouldMove);
    }

    // 【新增】传送方法
    void TeleportToPlayer()
    {
        // 计算玩家背后的位置
        Vector3 spawnPos = ownerPlayer.position - ownerPlayer.forward * 3f;
        // 简单的地面校正 (防止卡在墙里或地下，简单处理保持原Y轴或玩家Y轴)
        spawnPos.y = ownerPlayer.position.y;

        // 播放消失特效 (在旧位置)
        if (teleportVfxPrefab != null) Instantiate(teleportVfxPrefab, transform.position, Quaternion.identity);

        // 瞬移
        transform.position = spawnPos;

        // 播放出现特效 (在新位置)
        if (teleportVfxPrefab != null) Instantiate(teleportVfxPrefab, transform.position, Quaternion.identity);

        // 重置状态
        isPerformingAttackRoutine = false;
        currentState = MechState.Following;
        lineRenderer.enabled = false;
        if (currentLaserImpactVfx != null) currentLaserImpactVfx.SetActive(false);
    }

    void AimBoneLateUpdate()
    {
        Vector3 directionToTarget = (currentTarget.position - transform.position).normalized;
        directionToTarget.y = 0;
        float angle = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        if (Mathf.Abs(angle) > maxTorsoAngle)
        {
            Quaternion targetBodyRot = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetBodyRot, rotationSpeed * Time.deltaTime * 5f);
        }
        Vector3 aimDir = (currentTarget.position - aimBone.position).normalized;
        aimDir.y = Mathf.Clamp(aimDir.y, -0.5f, 0.5f);
        Quaternion lookRot = Quaternion.LookRotation(aimDir);
        Quaternion finalRot = lookRot * Quaternion.Euler(boneCorrection);
        aimBone.rotation = Quaternion.Slerp(aimBone.rotation, finalRot, rotationSpeed * Time.deltaTime * 10f);
    }

    void FireSingleMissile(Transform muzzle)
    {
        if (muzzle == null) muzzle = transform;
        if (missilePrefab == null) return;

        // 1. 位置偏移
        Vector3 randomPosOffset = (muzzle.right * Random.Range(-1f, 1f) + muzzle.up * Random.Range(-1f, 1f)) * muzzlePosOffset;
        Vector3 spawnPos = muzzle.position + randomPosOffset;

        // 2. 【新增】生成枪口火焰
        if (muzzleFlashVfxPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashVfxPrefab, spawnPos, muzzle.rotation);
            Destroy(flash, 0.5f); // 自动销毁
        }

        GameObject missile = Instantiate(missilePrefab, spawnPos, muzzle.rotation);

        Projectile proj = missile.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.damage = missileDamage;
            proj.owner = ownerWeapon.gameObject;
            proj.MarkAsPlayerProjectile();

            // --- 【核心修复】防止导弹飞天 ---
            // 如果 currentTarget 丢失（比如跑太远了），我们临时用更大范围找一个替代目标
            // 这样导弹至少会飞向某个敌人，而不是飞向太空
            Transform missileTarget = currentTarget;
            if (missileTarget == null)
            {
                // 临时找一个 50米内的敌人给导弹
                missileTarget = FindEnemyForMissile(50f);
            }

            proj.InitializeAsHoming(
                missileTarget, // 传入修正后的目标
                20f,
                missileDamage,
                false,
                15f,
                5f,
                missileHitVfxPrefab,
                missileHitVfxPrefab
            );

            // 3. 初始轨迹
            Vector3 spreadDir = Random.insideUnitSphere * trajectorySpread;

            // 【核心修复】如果此时还是没有目标 (missileTarget == null)，
            // 说明周围真没怪了，那就不要往天上打，改为往“前方”抛射
            Vector3 baseDir = (missileTarget != null) ? (muzzle.up * 2.0f) : (muzzle.forward + muzzle.up * 0.5f);

            Vector3 launchDir = (baseDir + spreadDir).normalized;

            Rigidbody rb = missile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.velocity = launchDir * missileUpwardForce;
                missile.transform.forward = launchDir;
            }
        }
    }

    // 【新增】专门为导弹找目标的辅助方法
    Transform FindEnemyForMissile(float range)
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, range, enemyLayer);
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead) return col.transform; // 随便返回一个活着的就行
        }
        return null;
    }

    void FireLaserTick(float timeFactor = 0f)
    {
        Vector3 startPos = chestMuzzle != null ? chestMuzzle.position : transform.position;
        Vector3 dir = chestMuzzle != null ? chestMuzzle.forward : transform.forward;
        float maxDist = attackRange + 5f;
        Vector3 endPos = startPos + dir * maxDist;

        if (currentTarget != null)
        {
            Transform aimPoint = currentTarget.Find("AimTargetPoint");
            Vector3 baseTargetPos = (aimPoint != null) ? aimPoint.position : (currentTarget.position + Vector3.up * 1.5f);
            float sweepOffset = Mathf.Sin(timeFactor * 10f) * 1.5f;
            Vector3 rightDir = Vector3.Cross(dir, Vector3.up);
            endPos = baseTargetPos + rightDir * sweepOffset;
            endPos += (endPos - startPos).normalized * 3f;
        }

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);

        if (currentLaserImpactVfx != null)
        {
            RaycastHit wallHit;
            Vector3 impactPos = endPos;
            if (Physics.Raycast(startPos, (endPos - startPos).normalized, out wallHit, maxDist, enemyLayer)) impactPos = wallHit.point;
            currentLaserImpactVfx.transform.position = impactPos;
        }

        damageTickTimer += Time.deltaTime;
        if (damageTickTimer >= (1f / laserTickRate))
        {
            damageTickTimer = 0f;
            Vector3 sweepDir = (endPos - startPos).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(startPos, laserWidth * 0.5f, sweepDir, maxDist, enemyLayer);
            foreach (var hit in hits)
            {
                if (Vector3.Distance(transform.position, hit.point) < minAttackDistance) continue;
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead) h.TakeDamage(laserDamage, hit.point, ownerWeapon.gameObject, AttackType.Standard);
            }
        }
    }

    void FindNewTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        float maxHealth = -1f;
        Transform best = null;
        foreach (var col in enemies)
        {
            if (Vector3.Distance(transform.position, col.transform.position) < minAttackDistance) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                if (h.currentHealth > maxHealth)
                {
                    maxHealth = h.currentHealth;
                    best = col.transform;
                }
            }
        }
        currentTarget = best;
    }
}