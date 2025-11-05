using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponPart : MonoBehaviour
{
    [Header("武器数据蓝图 (在预制件中设置)")]
    public WeaponStatBlock myStatBlock;

    [Header("组件引用 (在预制件中设置)")]
    public Transform firePoint;

    [Header("特效预制件")]
    [Tooltip("连锁闪电的视觉特效预制件")]
    public GameObject lightningChainPrefab;

    [Header("音效设置")]
    [Tooltip("标准、穿透、抛物线等发射型武器的开火音效")]
    public AudioClip[] fireSounds;
    [Tooltip("放置地雷的音效")]
    public AudioClip landminePlaceSound;
    [Tooltip("光束武器持续发出的循环音效")]
    public AudioClip beamLoopSound;
    [Tooltip("开火音效相对于子弹发射的延迟（秒）。负数为提前播放。")]
    public float fireSoundDelay = -0.05f;

    [Header("光环状态 (运行时)")]
    private List<Health> targetsInAura = new List<Health>();
    private float auraTickTimer = 0f;
    private SphereCollider auraCollider;
    private GameObject auraVfxInstance;

    private AudioSource audioSource;

    // 由 WeaponController 在运行时赋值
    public WeaponStatBlock StatBlock
    {
        get { return myStatBlock; }
        set { myStatBlock = value; }
    }   

    private bool isBoomerangOut = false;
    private float fireCooldown = 0f;
    private float orbitalCooldownTimer = 0f;
    private bool isOrbitalActive = false;
    private Transform orbitalPivot;

    public bool IsReadyToFire => fireCooldown <= 0f;

    private PlayerBeamController activeBeamInstance = null;
    private float beamEnergyTimer = 0f;
    private float beamCooldownTimer = 0f;
    private Transform lockedBeamTarget = null;

    #region Unity Lifecycle Methods
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
        }

        auraCollider = GetComponent<SphereCollider>();
    }
    void Start()
    {
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Aura)
        {
            SetupAura();
        }
    }
    void Update()
    {
        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;
        if (orbitalCooldownTimer > 0f) orbitalCooldownTimer -= Time.deltaTime;
        if (beamCooldownTimer > 0f)
        {
            beamCooldownTimer -= Time.deltaTime;
            if (beamCooldownTimer <= 0)
            {
                beamEnergyTimer = StatBlock.beamDuration;
            }
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital && orbitalPivot != null)
        {
            float finalOrbitalSpeed = StatBlock.baseOrbitalSpeed; // Add player stats later if needed
            orbitalPivot.Rotate(Vector3.up, finalOrbitalSpeed * Time.deltaTime);
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Landmine)
        {
            HandleLandminePlacement();
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            HandleBeamWeapon();
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Aura)
        {
            HandleAuraDamageTick();
        }
    }

    private void SetupAura()
    {
        if (StatBlock == null) return;

        // 计算最终半径 (计入玩家加成)
        float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier; //

        if (auraCollider != null)
        {
            auraCollider.radius = finalRadius;
        }
        else { Debug.LogError("Aura WeaponPart 预制件上缺少 SphereCollider!", this); }

        // 实例化视觉特效 (如果提供了)
        if (StatBlock.auraVfxPrefab != null)
        {
            // 作为子物体实例化，确保它跟随 WeaponPart (即玩家)
            auraVfxInstance = Instantiate(StatBlock.auraVfxPrefab, transform.position, Quaternion.identity, transform);
            // 调整VFX的缩放以匹配碰撞器半径 (这里的 '2' 是一个通用值，你可能需要微调)
            auraVfxInstance.transform.localScale = Vector3.one * finalRadius * StatBlock.vfxBaseScaleMultiplier;
        }

        auraTickTimer = 0; // 立即触发第一次伤害
        targetsInAura.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        // 确保此逻辑只在光环武器上运行
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.Aura) return;

        Health enemyHealth = other.GetComponentInParent<Health>();
        // 检查是否是敌人、未死亡、且不在列表中
        if (enemyHealth != null && !enemyHealth.IsDead && other.CompareTag("Enemy")) //
        {
            if (!targetsInAura.Contains(enemyHealth))
            {
                targetsInAura.Add(enemyHealth);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // 确保此逻辑只在光环武器上运行
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.Aura) return;

        Health enemyHealth = other.GetComponentInParent<Health>();
        if (enemyHealth != null)
        {
            if (targetsInAura.Contains(enemyHealth))
            {
                targetsInAura.Remove(enemyHealth);
            }
        }
    }

    public void RefreshAura()
    {
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.Aura) return;

        float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier; //

        if (auraCollider != null) { auraCollider.radius = finalRadius; }

        if (auraVfxInstance != null)
        {
            auraVfxInstance.transform.localScale = Vector3.one * finalRadius * StatBlock.vfxBaseScaleMultiplier;
        }
    }
    private void HandleAuraDamageTick()
    {
        auraTickTimer -= Time.deltaTime;
        if (auraTickTimer <= 0f)
        {
            if (StatBlock == null) return;
            auraTickTimer = StatBlock.baseAreaTickInterval; // 重置计时器

            // 计算最终伤害
            int finalDamage = Mathf.RoundToInt(StatBlock.baseAreaDamagePerTick * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus); //

            // 从后往前遍历列表，防止在移除时出错
            for (int i = targetsInAura.Count - 1; i >= 0; i--)
            {
                Health target = targetsInAura[i];
                if (target == null || target.IsDead) //
                {
                    targetsInAura.RemoveAt(i);
                    continue;
                }

                // 对范围内的每个目标造成伤害
                target.TakeDamage(finalDamage, target.transform.position, this.gameObject, AttackType.Standard); //
            }
        }
    }

    private void OnDestroy()
    {
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        if (activeBeamInstance != null) { Destroy(activeBeamInstance.gameObject); }
    }
    #endregion

    #region Public Control Methods
    public void Activate() // Called when weapon is equipped/activated
    {
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
            beamCooldownTimer = 0;
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            if (!isOrbitalActive && orbitalCooldownTimer <= 0)
            {
                SetupOrbiters();
            }
        }
        // 添加：激活时重置回旋镖状态（如果它是回旋镖）
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Boomerang)
        {
            isBoomerangOut = false;
            // 注意：catchStacks 现在由 PlayerStats 管理，这里不需要重置
        }
    }

    public void Fire(Vector3 initialDirection)
    {
        // Check generic cooldown first
        if (!IsReadyToFire) return;
        // Then check specific boomerang state
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Boomerang && isBoomerangOut) return;

        StartCoroutine(FireRoutine(initialDirection));
    }   
    

    private IEnumerator FireRoutine(Vector3 initialDirection)
    {
        // --- 冷却和状态检查 (保持不变) ---
        if (StatBlock.behavior == WeaponBehaviorType.Boomerang)
        {
            if (isBoomerangOut) yield break;
            isBoomerangOut = true; // 发射时不冷却，只标记
        }
        else { fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier; }
        // --- 冷却结束 ---

        // 音效延迟...
        if (fireSoundDelay < 0) { PlayFireSound(); yield return new WaitForSeconds(Mathf.Abs(fireSoundDelay)); }
        // 特殊武器...
        if (StatBlock?.behavior == WeaponBehaviorType.Beam) yield break;
        if (StatBlock?.behavior == WeaponBehaviorType.Orbital) { if (!isOrbitalActive && orbitalCooldownTimer <= 0) { SetupOrbiters(); } yield break; }

        // 目标和方向...
        Vector3 finalTargetDirection = initialDirection;
        Transform firstTarget = null;
        if (StatBlock.autoAimAtNearestEnemy && GetComponentInParent<DroneAI>() == null)
        {
            Transform nearestEnemy = FindNearestEnemyTransform();
            if (nearestEnemy != null) { finalTargetDirection = (nearestEnemy.position - firePoint.position); finalTargetDirection.y = 0; finalTargetDirection.Normalize(); firstTarget = nearestEnemy; }
            else if (StatBlock.behavior != WeaponBehaviorType.Boomerang) { yield break; }
        }
        if (finalTargetDirection.sqrMagnitude < 0.01f) { if (StatBlock?.behavior == WeaponBehaviorType.Boomerang) isBoomerangOut = false; yield break; }

        // --- Calculate Final Damage and Scale ---
        int baseDamage = 0;
        float baseScale = 1f; // 基础体积乘数总是 1

        if (StatBlock != null && PlayerStats.Instance != null)
        {
            // 基础伤害考虑玩家全局加成
            baseDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);
            // 基础体积考虑玩家全局加成
            baseScale = PlayerStats.Instance.aoeRadiusMultiplier;
        }

        // 2. 如果是回旋镖，计算【加法】叠加乘数
        float totalDamageMultiplier = 1f;
        float totalScaleMultiplier = 1f; // 这个乘数应用在 PlayerStats 的基础体积上
        if (StatBlock?.behavior == WeaponBehaviorType.Boomerang && PlayerStats.Instance != null && PlayerStats.Instance.boomerangMaxCatchStacks > 0)
        {
            // 【核心修改】计算总加成百分比
            float totalDamageBonusPercent = PlayerStats.Instance.boomerangCatchStacks * PlayerStats.Instance.boomerangStackDamageBonusPercent;
            float totalScaleBonusPercent = PlayerStats.Instance.boomerangCatchStacks * PlayerStats.Instance.boomerangStackScaleBonusPercent;

            // 应用加法叠加
            totalDamageMultiplier = 1f + totalDamageBonusPercent;
            totalScaleMultiplier = 1f + totalScaleBonusPercent; // 这个乘数是相对于【原始模型】的
            // Debug.Log($"[FireRoutine] Stacks={PlayerStats.Instance.boomerangCatchStacks}, DmgBonus={totalDamageBonusPercent*100}%, ScaleBonus={totalScaleBonusPercent*100}% => TotalDmgMult={totalDamageMultiplier}, TotalScaleMult={totalScaleMultiplier}");
        }
        // 3. 计算最终伤害和体积
        // 最终伤害 = 基础伤害 * 叠加乘数
        int finalDamage = Mathf.RoundToInt(baseDamage * totalDamageMultiplier);
        // 最终体积 = 玩家基础体积 * 叠加乘数
        float finalScale = baseScale * totalScaleMultiplier; // <-- 体积乘数应用在这里

        // --- Firing based on Behavior ---
        switch (StatBlock?.behavior)
        {
            case WeaponBehaviorType.Standard:
            case WeaponBehaviorType.Pierce:
                InstantiateAndFireProjectile(finalTargetDirection, finalDamage); break; // 确保接收 finalDamage
            case WeaponBehaviorType.ParabolicAOE:
                int finalAoeDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
                InstantiateAndFireParabolicProjectile(finalTargetDirection, StatBlock, finalDamage, finalAoeDamage); break; // 确保接收 finalDamage(s)
            case WeaponBehaviorType.Chain:
                if (firstTarget != null) { StartCoroutine(ChainDamageRoutine(firstTarget, StatBlock.baseChainCount, finalDamage, StatBlock.chainRange)); }
                break; // 确保使用 finalDamage
            case WeaponBehaviorType.SummonDrone:
                InstantiateAndInitializeDrones(); if (StatBlock.isOneShot) { this.enabled = false; }
                break;
            case WeaponBehaviorType.PersistentAOE:
                Transform targetEnemy = FindNearestEnemyTransform();
                if (targetEnemy != null) { InstantiateAndFireAirdropDeployer(targetEnemy.position); }
                break; // 确保传递伤害
            case WeaponBehaviorType.Boomerang:
                InstantiateAndFireBoomerang(finalTargetDirection, finalDamage, finalScale); break; // 传递最终伤害 & 最终体积
            case null: Debug.LogError("StatBlock is null!"); break;
        }

        // --- Muzzle Flash & Delayed Sound ---
        if (StatBlock != null && StatBlock.muzzleFlashPrefab != null) { Instantiate(StatBlock.muzzleFlashPrefab, firePoint.position, firePoint.rotation); }
        if (fireSoundDelay >= 0) { yield return new WaitForSeconds(fireSoundDelay); PlayFireSound(); }
    }

    public void OnBoomerangCaught(Vector3 catchPosition)
    {
        if (!isBoomerangOut || StatBlock == null || PlayerStats.Instance == null)
        {
            // Debug.LogWarning($"[OnBoomerangCaught] Ignored. isBoomerangOut={isBoomerangOut}, StatBlock Null? {StatBlock == null}, PlayerStats Null? {PlayerStats.Instance == null}");
            return;
        }

        isBoomerangOut = false; // Mark as returned

        // --- 核心修改：增加 PlayerStats 中的叠加层数 ---
        if (PlayerStats.Instance.boomerangMaxCatchStacks > 0)
        {
            PlayerStats.Instance.boomerangCatchStacks = Mathf.Min(PlayerStats.Instance.boomerangCatchStacks + 1, PlayerStats.Instance.boomerangMaxCatchStacks);
            Debug.Log($"Boomerang caught! PlayerStats Stacks increased to {PlayerStats.Instance.boomerangCatchStacks}/{PlayerStats.Instance.boomerangMaxCatchStacks}");
        }

        // --- 核心修改结束 ---

        ResetCooldown(); // 重置冷却

        // --- 自动再次丢出逻辑 ---
        Transform nearestEnemy = FindNearestEnemyTransform(catchPosition, StatBlock.autoAimRange);
        Vector3 nextDirection;
        if (nearestEnemy != null)
        {
            nextDirection = (nearestEnemy.position - catchPosition).normalized;
            nextDirection.y = 0; // Keep horizontal
        }
        else // No enemy? Throw behind player
        {
            if (GameManager.Instance?.playerTransform != null)
                nextDirection = -GameManager.Instance.playerTransform.forward;
            else
                nextDirection = transform.forward; // Fallback
        }
        Debug.Log($"[OnBoomerangCaught] Triggering re-throw towards {nextDirection}");
        StartCoroutine(FireRoutine(nextDirection.normalized));
    }

    public void StartCooldownIfNotCaught()
    {
        if (isBoomerangOut)
        {
            isBoomerangOut = false;
            fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
            // Debug.Log($"Boomerang '{StatBlock?.weaponName}' missed, starting cooldown: {fireCooldown}s.");

            // --- 【核心修改】重置 PlayerStats 中的叠加层数 ---
            if (PlayerStats.Instance != null && PlayerStats.Instance.boomerangCatchStacks > 0)
            {
                PlayerStats.Instance.boomerangCatchStacks = 0;
                Debug.Log("PlayerStats catch stacks reset to 0.");
            }
            // --- 【核心修改结束】 ---
        }
    }

    public void ResetCooldown()
    {
        fireCooldown = 0.01f; // Set a very small cooldown to allow immediate firing after catch
    }
    #endregion

    #region Private Helper Methods
    private void PlayFireSound()
    {
        if (fireSounds != null && fireSounds.Length > 0 && audioSource != null)
        {
            AudioClip clipToPlay = fireSounds[Random.Range(0, fireSounds.Length)];
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    // Updated to accept finalDamage
    private void InstantiateAndFireProjectile(Vector3 direction, int finalDamage)
    {
        if (firePoint == null || StatBlock?.projectilePrefab == null) return;
        GameObject bullet = Instantiate(StatBlock.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            float finalSpeed = StatBlock.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
            int finalPierceCount = StatBlock.basePierceCount + PlayerStats.Instance.bonusPierceCount;
            // Get DoT/Slow stats (assuming they scale appropriately or have their own logic)
            int finalDotDamage = Mathf.RoundToInt(StatBlock.baseDotDamage /* * ScalingFactorIfNeeded */);
            float finalDotDuration = StatBlock.baseDotDuration;
            float finalDotTickInterval = StatBlock.dotTickInterval;
            float finalSlowPercentage = StatBlock.baseSlowPercentage;
            float finalSlowDuration = StatBlock.baseSlowDuration;

            projectileScript.InitializeAsStraight(
              direction, finalSpeed, finalDamage, // Use passed finalDamage
              false, finalPierceCount, StatBlock.baseProjectileLifetime,
              StatBlock.shieldImpactEffectPrefab, StatBlock.defaultImpactEffectPrefab,
              finalDotDamage, finalDotDuration, finalDotTickInterval, finalSlowPercentage, finalSlowDuration,
              AttackType.Standard
            );
        }
        else { Destroy(bullet); } // Clean up if script missing
    }

    // Updated to accept finalDirectDamage and finalAoeDamage
    private void InstantiateAndFireParabolicProjectile(Vector3 horizontalDir, WeaponStatBlock statsToUse, int finalDirectDamage, int finalAoeDamage)
    {
        if (firePoint == null || statsToUse?.projectilePrefab == null) return;

        // Calculate other final stats as needed
        float finalAoeRadius = statsToUse.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
        float finalLaunchForce = statsToUse.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
        int finalDotDamage = Mathf.RoundToInt(statsToUse.baseDotDamage /* * ScalingFactorIfNeeded */);
        float finalDotDuration = statsToUse.baseDotDuration;
        float finalDotTickInterval = statsToUse.dotTickInterval;

        float finalStunChance = statsToUse.baseStunChance + PlayerStats.Instance.parabolicAoeStunChance;
        // 最终时长 = 武器基础时长 (未来也可以让 PlayerStats 强化这个)
        float finalStunDuration = statsToUse.baseStunDuration;

        GameObject bullet = Instantiate(statsToUse.projectilePrefab, firePoint.position, Quaternion.LookRotation(horizontalDir));
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            float angleRad = statsToUse.launchAngle * Mathf.Deg2Rad;
            float hVel = finalLaunchForce * Mathf.Cos(angleRad);
            float vVel = finalLaunchForce * Mathf.Sin(angleRad);
            Vector3 initialVelocity = (horizontalDir * hVel) + (Vector3.up * vVel);

            projectileScript.InitializeAsParabolic(
                 initialVelocity, finalDirectDamage, finalAoeDamage,
                 statsToUse.baseProjectileLifetime, statsToUse.explosionEffectPrefab, finalAoeRadius,
                 statsToUse.layersToDamageByAOE, statsToUse.layersToExplodeOn,
                 finalDotDamage, finalDotDuration, finalDotTickInterval,
                 finalStunChance, finalStunDuration
             );
        }
        else { Destroy(bullet); } // Clean up
    }

    // Updated to accept finalDamage and finalScale
    private void InstantiateAndFireBoomerang(Vector3 direction, int finalDamage, float finalScale, Vector3? launchPosition = null) // <-- 添加可选参数
    {
        // 如果没有提供发射位置，则使用默认的 firePoint
        Vector3 spawnPos = launchPosition ?? firePoint.position;

        if (StatBlock?.projectilePrefab == null) // 简化 null 检查
        {
            isBoomerangOut = false;
            Debug.LogError($"[WeaponPart] Boomerang fire failed: ProjectilePrefab missing for {StatBlock?.weaponName}");
            return;
        }

        GameObject bullet = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
        bullet.transform.localScale = Vector3.one * finalScale;

        float finalSpeed = StatBlock.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
        float finalMaxDistance = StatBlock.maxDistance;
        float finalCatchRadius = StatBlock.catchRadius * finalScale;

        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // --- 调用最终的 InitializeAsBoomerang ---
            projectileScript.InitializeAsBoomerang(
                direction,
                finalSpeed,
                finalDamage,
                finalMaxDistance,
                finalCatchRadius,
                StatBlock.baseProjectileLifetime,
                StatBlock.shieldImpactEffectPrefab,
                StatBlock.defaultImpactEffectPrefab,
                this,
                StatBlock.rotationSpeed,
                StatBlock.returnOvershootDistance // <-- 传递这个值
            );
        }
        else
        {
            isBoomerangOut = false;
            Debug.LogError($"[WeaponPart] Boomerang fire failed: Projectile script missing on prefab for {StatBlock?.weaponName}");
            Destroy(bullet);
        }
    }

    private IEnumerator ChainDamageRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange)
    {
        var hitEnemies = new List<Health>();
        Vector3 lastHitPosition = firePoint.position;

        while (currentTarget != null && remainingChains >= 0)
        {
            Vector3 currentTargetPosition = currentTarget.position;
            Health targetHealth = currentTarget.GetComponent<Health>();

            if (targetHealth != null && !hitEnemies.Contains(targetHealth) && !targetHealth.IsDead)
            {
                hitEnemies.Add(targetHealth);
                targetHealth.TakeDamage(damage, currentTargetPosition, this.gameObject); // Use calculated damage

                if (lightningChainPrefab != null)
                {
                    var chainVFX_GO = Instantiate(lightningChainPrefab, Vector3.zero, Quaternion.identity);
                    chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, currentTargetPosition);
                }
                if (StatBlock?.impactEffectPrefab != null) // Use StatBlock impact
                {
                    Instantiate(StatBlock.impactEffectPrefab, currentTargetPosition, Quaternion.identity);
                }
            }

            yield return new WaitForSeconds(0.05f);

            Transform nextTarget = FindNextChainTarget(currentTargetPosition, chainRange, hitEnemies);
            remainingChains--;
            lastHitPosition = currentTargetPosition;
            currentTarget = nextTarget;
        }
    }

    public void RefreshOrbiters() // Re-instantiate orbiters with current stats
    {
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital || !isOrbitalActive) return;
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false; // Mark inactive before setup
        StopCoroutine(nameof(OrbitalLifetimeRoutine)); // Stop potential old lifetime timer
        SetupOrbiters(); // Call setup again
    }

    private void SetupOrbiters() // Creates the orbital system
    {
        if (StatBlock == null || StatBlock.orbitalPrefab == null) return; // Need data

        Transform stableAnchor = FindStableAnchor(); // Find player's stable anchor point

        orbitalPivot = new GameObject($"{StatBlock.weaponName}_Pivot").transform;
        orbitalPivot.SetParent(stableAnchor);
        orbitalPivot.localPosition = Vector3.zero;
        orbitalPivot.localRotation = Quaternion.identity;

        isOrbitalActive = true; // Mark as active *after* pivot creation
        int finalOrbitalCount = StatBlock.baseOrbitalCount + PlayerStats.Instance.bonusOrbitalCount;
        float finalOrbitalRadius = StatBlock.baseOrbitalRadius * PlayerStats.Instance.aoeRadiusMultiplier;
        // Orbital damage scales like direct damage
        int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);

        for (int i = 0; i < finalOrbitalCount; i++)
        {
            float angle = i * (360f / finalOrbitalCount);
            Vector3 spawnPos = Quaternion.Euler(0, angle, 0) * (Vector3.forward * finalOrbitalRadius);
            GameObject orbiterGO = Instantiate(StatBlock.orbitalPrefab, orbitalPivot); // Parent to pivot
            orbiterGO.transform.localPosition = spawnPos; // Position relative to pivot
            orbiterGO.GetComponent<Orbiter>()?.Initialize(finalDamage);
        }

        float finalDuration = StatBlock.baseDuration; // Apply player bonuses if any
        if (finalDuration > 0)
        {
            StartCoroutine(OrbitalLifetimeRoutine(finalDuration));
        }
    }

    private IEnumerator OrbitalLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); } // Destroy pivot and children
        isOrbitalActive = false; // Mark inactive
        orbitalCooldownTimer = (1f / StatBlock.baseFireRate); // Start cooldown based on fire rate after duration ends
    }

    private Transform FindNearestEnemyTransform() { return FindNearestEnemyTransform(transform.position, StatBlock?.autoAimRange ?? 0f); }

    // Overload for searching from a specific point
    private Transform FindNearestEnemyTransform(Vector3 searchCenter, float searchRadius)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;
        if (StatBlock == null || searchRadius <= 0) return null;

        LayerMask layersToSearch = StatBlock.layersToDamageByAOE == 0 ? LayerMask.GetMask("Enemies") : StatBlock.layersToDamageByAOE;

        Collider[] colliders = Physics.OverlapSphere(searchCenter, searchRadius, layersToSearch);
        foreach (Collider hitCollider in colliders)
        {
            Health enemyHealth = hitCollider.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead && hitCollider.CompareTag("Enemy")) // Check tag too
            {
                float dSqrToTarget = (searchCenter - hitCollider.transform.position).sqrMagnitude;
                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    nearestEnemy = enemyHealth.transform;
                }
            }
        }
        return nearestEnemy;
    }

    private Transform FindNextChainTarget(Vector3 currentPosition, float range, List<Health> alreadyHit)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(currentPosition, range, StatBlock.layersToDamageByAOE);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;
        foreach (var col in nearbyColliders)
        {
            Health potentialTargetHealth = col.GetComponentInParent<Health>();
            if (potentialTargetHealth != null && !potentialTargetHealth.IsDead && !alreadyHit.Contains(potentialTargetHealth))
            {
                float distSqr = (currentPosition - col.transform.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestTarget = potentialTargetHealth.transform;
                }
            }
        }
        return closestTarget;
    }

    private void HandleBeamWeapon()
    {
        if (StatBlock == null) return;
        if (beamCooldownTimer > 0f) return;

        ValidateOrFindTarget();

        if (lockedBeamTarget != null)
        {
            if (beamEnergyTimer > 0f && activeBeamInstance == null) { StartBeam(); }
            if (activeBeamInstance != null)
            {
                beamEnergyTimer -= Time.deltaTime;
                if (beamEnergyTimer <= 0f) { StopBeamAndStartCooldown(); }
            }
        }
        else { if (activeBeamInstance != null) { StopBeamForStandby(); } }
    }

    private void ValidateOrFindTarget()
    {
        if (StatBlock == null) return;
        if (lockedBeamTarget != null)
        {
            if (!lockedBeamTarget.gameObject.activeInHierarchy || Vector3.Distance(firePoint.position, lockedBeamTarget.position) > StatBlock.beamMaxDistance)
            { lockedBeamTarget = null; }
        }
        if (lockedBeamTarget == null)
        { lockedBeamTarget = FindNearestEnemyTransform(firePoint.position, StatBlock.beamMaxDistance); }
    }

    private void StartBeam()
    {
        if (StatBlock?.beamVfxPrefab == null || firePoint == null) return;
        GameObject beamGO = Instantiate(StatBlock.beamVfxPrefab, firePoint.position, firePoint.rotation, firePoint);
        activeBeamInstance = beamGO.GetComponent<PlayerBeamController>();
        if (activeBeamInstance != null)
        {
            activeBeamInstance.Initialize(StatBlock, this.gameObject, lockedBeamTarget);
            if (beamLoopSound != null && audioSource != null && !audioSource.isPlaying)
            { audioSource.clip = beamLoopSound; audioSource.loop = true; audioSource.Play(); }
        }
        else { Debug.LogError($"Beam VFX missing PlayerBeamController script!", this); Destroy(beamGO); }
    }

    private void StopBeamForStandby()
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        lockedBeamTarget = null;
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); }
    }

    private void StopBeamAndStartCooldown()
    {
        if (activeBeamInstance == null || StatBlock == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        beamCooldownTimer = StatBlock.beamCooldown;
        beamEnergyTimer = 0;
        lockedBeamTarget = null;
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); }
    }

    public void DeactivateBeam() // When weapon is unequipped
    {
        StopBeamForStandby();
        beamEnergyTimer = 0;
    }

    private Transform FindStableAnchor() // Finds a stable point on the player rig for parenting orbitals/pivots
    {
        // Try finding a specific marker component first
        if (WeaponController.Instance != null)
        {
            StableAnchorMarker marker = WeaponController.Instance.GetComponentInChildren<StableAnchorMarker>();
            if (marker != null) return marker.transform;
        }
        // Fallback to the WeaponController transform itself (player root)
        return WeaponController.Instance?.transform ?? transform; // Use own transform if controller missing
    }

    private void InstantiateAndInitializeDrones()
    {
        if (StatBlock == null || StatBlock.summonPrefab == null || StatBlock.summonWeaponStats == null || WeaponController.Instance == null) return;

        int finalSummonCount = StatBlock.summonCount;
        float finalSummonDuration = StatBlock.summonDuration;
        Transform playerRoot = WeaponController.Instance.transform;

        for (int i = 0; i < finalSummonCount; i++)
        {
            Vector3 spawnOffset = Random.insideUnitSphere * 2f;
            spawnOffset.y = StatBlock.summonSpawnHeight;
            Vector3 spawnPosition = playerRoot.position + spawnOffset;

            GameObject droneGO = Instantiate(StatBlock.summonPrefab, spawnPosition, Quaternion.identity);
            DroneAI droneAI = droneGO.GetComponent<DroneAI>();
            if (droneAI != null) { droneAI.Initialize(StatBlock.summonWeaponStats, finalSummonDuration, playerRoot); }
            else { Debug.LogWarning($"Summon prefab '{StatBlock.summonPrefab.name}' is missing DroneAI script.", droneGO); }
        }
    }

    private void InstantiateAndFireAirdropDeployer(Vector3 targetPosition)
    {
        if (StatBlock?.deployerProjectilePrefab == null || firePoint == null || WeaponController.Instance == null) return;

        float spawnHeight = StatBlock.deployerSpawnHeight;
        float horizontalOffset = 5f; // Offset from target

        Vector3 directionFromPlayer = (targetPosition - WeaponController.Instance.transform.position).normalized; // Use player position
        directionFromPlayer.y = 0;
        Vector3 startPosition = targetPosition + (Vector3.up * spawnHeight) - (directionFromPlayer * horizontalOffset);
        Vector3 fallDirection = (targetPosition - startPosition).normalized; // Direction towards target on ground

        GameObject deployerGO = Instantiate(StatBlock.deployerProjectilePrefab, startPosition, Quaternion.LookRotation(fallDirection));
        Projectile projectileScript = deployerGO.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            int finalDamage = Mathf.RoundToInt(StatBlock.baseAreaDamagePerTick * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
            float finalDuration = StatBlock.baseAreaDuration;
            float finalInterval = StatBlock.baseAreaTickInterval;

            projectileScript.InitializeAsAirdropDeployer(
                startPosition, fallDirection, StatBlock.deployerFallSpeed, StatBlock.areaPrefab,
                finalDamage, finalDuration, finalInterval, WeaponController.Instance.gameObject // Attacker is the Player object
            );
        }
        else { Destroy(deployerGO); } // Clean up
    }

    private void HandleLandminePlacement()
    {
        if (StatBlock == null || !IsReadyToFire || WeaponController.Instance == null) return;

        Vector2 randomCirclePoint = Random.insideUnitCircle * StatBlock.spawnRadius;
        Vector3 spawnPositionBase = WeaponController.Instance.transform.position + new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);

        RaycastHit hit;
        Vector3 spawnPosition = spawnPositionBase;
        LayerMask groundMask = StatBlock.beamScorchMarkGroundLayer != 0 ? StatBlock.beamScorchMarkGroundLayer : LayerMask.GetMask("Ground");
        if (Physics.Raycast(spawnPositionBase + Vector3.up * 5f, Vector3.down, out hit, 10f, groundMask))
        { spawnPosition = hit.point; } // Place on ground if found

        if (StatBlock.minePrefab != null)
        {
            GameObject mineGO = Instantiate(StatBlock.minePrefab, spawnPosition, Quaternion.identity);
            Landmine mineScript = mineGO.GetComponent<Landmine>();
            if (mineScript != null)
            {
                int finalDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
                float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
                mineScript.Initialize(
                    finalDamage, finalRadius, StatBlock.armingTime, StatBlock.mineDuration,
                    WeaponController.Instance.gameObject, // Player is attacker
                    StatBlock.explosionEffectPrefab, StatBlock.layersToDamageByAOE
                );
            }
            else { Debug.LogWarning($"Mine prefab '{StatBlock.minePrefab.name}' is missing Landmine script.", mineGO); }

            if (landminePlaceSound != null && audioSource != null)
            { AudioSource.PlayClipAtPoint(landminePlaceSound, spawnPosition); } // Play sound at location
        }

        // Reset cooldown
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
    }
    #endregion
}

