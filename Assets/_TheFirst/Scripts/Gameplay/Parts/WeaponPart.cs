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
    }
    void Start()
    {
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
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
            float finalOrbitalSpeed = StatBlock.baseOrbitalSpeed;
            orbitalPivot.Rotate(Vector3.up, finalOrbitalSpeed * Time.deltaTime);
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Landmine) // Added null check
        {
            HandleLandminePlacement();
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            HandleBeamWeapon();
        }
    }

    private void OnDestroy()
    {
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        if (activeBeamInstance != null) { Destroy(activeBeamInstance.gameObject); }
    }
    #endregion

    #region Public Control Methods
    public void Activate()
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
    }

    public void Fire(Vector3 initialDirection)
    {
        if (StatBlock == null || !IsReadyToFire) return;
        // Specifically check boomerang state *before* checking IsReadyToFire for it
        if (StatBlock.behavior == WeaponBehaviorType.Boomerang && isBoomerangOut) return;

        StartCoroutine(FireRoutine(initialDirection));
    }

    private IEnumerator FireRoutine(Vector3 initialDirection)
    {
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;

        // --- Sound Delay ---
        if (fireSoundDelay < 0) { PlayFireSound(); yield return new WaitForSeconds(Mathf.Abs(fireSoundDelay)); }

        // --- Special Weapon Handling ---
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam) yield break;
        if (StatBlock.behavior == WeaponBehaviorType.Orbital) { if (!isOrbitalActive && orbitalCooldownTimer <= 0) { SetupOrbiters(); } yield break; }

        // --- Targeting Logic ---
        Vector3 finalTargetDirection = initialDirection;
        Transform firstTarget = null;
        if (StatBlock.autoAimAtNearestEnemy && GetComponentInParent<DroneAI>() == null)
        {
            Transform nearestEnemy = FindNearestEnemyTransform();
            // If nearest enemy found, use its direction
            if (nearestEnemy != null) { finalTargetDirection = (nearestEnemy.position - firePoint.position); finalTargetDirection.y = 0; finalTargetDirection.Normalize(); firstTarget = nearestEnemy; }
            // If no enemy found AND it's auto-aim, *don't* fire (unless it's boomerang, which uses player forward)
            else if (StatBlock.behavior != WeaponBehaviorType.Boomerang)
            {
                yield break;
            }
            // If it IS a boomerang and no enemy found, it will use initialDirection (player forward) below
        }
        if (finalTargetDirection.sqrMagnitude < 0.01f) { yield break; }

        // --- Firing based on Behavior ---
        switch (StatBlock.behavior)
        {
            case WeaponBehaviorType.Standard:
            case WeaponBehaviorType.Pierce:
                InstantiateAndFireProjectile(finalTargetDirection);
                break;
            case WeaponBehaviorType.ParabolicAOE:
                InstantiateAndFireParabolicProjectile(finalTargetDirection, StatBlock);
                break;
            case WeaponBehaviorType.Chain:
                if (firstTarget != null)
                {
                    int finalChainCount = StatBlock.baseChainCount; // Future upgrades
                    int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus); // Include flat bonus
                    StartCoroutine(ChainDamageRoutine(firstTarget, finalChainCount, finalDamage, StatBlock.chainRange));
                }
                break;
            case WeaponBehaviorType.SummonDrone:
                InstantiateAndInitializeDrones();
                if (StatBlock.isOneShot) { this.enabled = false; }
                break;
            case WeaponBehaviorType.PersistentAOE:
                Transform targetEnemy = FindNearestEnemyTransform();
                if (targetEnemy != null) { InstantiateAndFireAirdropDeployer(targetEnemy.position); }
                break;
            case WeaponBehaviorType.Boomerang:
                InstantiateAndFireBoomerang(finalTargetDirection);
                break;
                // Add cases for MeleeAOE, Landmine (handled in Update) if needed
        }

        // --- Muzzle Flash & Delayed Sound ---
        if (StatBlock.muzzleFlashPrefab != null) { Instantiate(StatBlock.muzzleFlashPrefab, firePoint.position, firePoint.rotation); }
        if (fireSoundDelay >= 0) { yield return new WaitForSeconds(fireSoundDelay); PlayFireSound(); }
    }

    public void OnBoomerangCaught(Vector3 catchPosition)
    {
        if (!isBoomerangOut) return; // Avoid processing if already caught/reset

        isBoomerangOut = false;
        ResetCooldown(); // Allow next shot immediately

        // Auto re-throw logic
        Transform nearestEnemy = FindNearestEnemyTransform(catchPosition, StatBlock.autoAimRange);
        Vector3 nextDirection;
        if (nearestEnemy != null)
        {
            nextDirection = (nearestEnemy.position - catchPosition).normalized;
            nextDirection.y = 0;
        }
        else
        {
            if (GameManager.Instance?.playerTransform != null)
                nextDirection = -GameManager.Instance.playerTransform.forward;
            else
                nextDirection = transform.forward;
        }
        StartCoroutine(FireRoutine(nextDirection.normalized)); // Immediately start the next fire sequence
    }

    public void StartCooldownIfNotCaught()
    {
        if (isBoomerangOut) // Only trigger if it was actually out and missed
        {
            isBoomerangOut = false;
            fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
            // Debug.Log($"Boomerang '{StatBlock.weaponName}' missed, starting cooldown: {fireCooldown}s.");
        }
    }

    public void ResetCooldown()
    {
        fireCooldown = 0.01f; // Set a very small cooldown to allow immediate firing
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

    private void InstantiateAndFireProjectile(Vector3 direction)
    {
        if (firePoint == null || StatBlock.projectilePrefab == null) return;

        GameObject bullet = Instantiate(StatBlock.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);
            float finalSpeed = StatBlock.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
            int finalPierceCount = StatBlock.basePierceCount + PlayerStats.Instance.bonusPierceCount;
            int finalDotDamage = Mathf.RoundToInt(StatBlock.baseDotDamage * PlayerStats.Instance.aoeDamageMultiplier); // Assuming DoT scales with AOE mult
            float finalDotDuration = StatBlock.baseDotDuration;
            float finalDotTickInterval = StatBlock.dotTickInterval;
            float finalSlowPercentage = StatBlock.baseSlowPercentage; // Add player stats if applicable
            float finalSlowDuration = StatBlock.baseSlowDuration; // Add player stats if applicable

            projectileScript.InitializeAsStraight(
              direction, finalSpeed, finalDamage, false, finalPierceCount,
              StatBlock.baseProjectileLifetime, StatBlock.shieldImpactEffectPrefab, StatBlock.defaultImpactEffectPrefab,
              finalDotDamage, finalDotDuration, finalDotTickInterval, finalSlowPercentage,
              finalSlowDuration, AttackType.Standard
            );
        }
    }

    private void InstantiateAndFireParabolicProjectile(Vector3 horizontalDir, WeaponStatBlock statsToUse)
    {
        if (firePoint == null || statsToUse.projectilePrefab == null) return;

        int finalDirectDamage = Mathf.RoundToInt(statsToUse.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);
        int finalAoeDamage = Mathf.RoundToInt(statsToUse.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
        float finalAoeRadius = statsToUse.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
        float finalLaunchForce = statsToUse.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
        int finalDotDamage = Mathf.RoundToInt(statsToUse.baseDotDamage * PlayerStats.Instance.aoeDamageMultiplier);
        float finalDotDuration = statsToUse.baseDotDuration;
        float finalDotTickInterval = statsToUse.dotTickInterval;

        GameObject bullet = Instantiate(statsToUse.projectilePrefab, firePoint.position, Quaternion.LookRotation(horizontalDir));
        Projectile projectileScript = bullet.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            float angleRad = statsToUse.launchAngle * Mathf.Deg2Rad;
            float hVel = finalLaunchForce * Mathf.Cos(angleRad);
            float vVel = finalLaunchForce * Mathf.Sin(angleRad);
            Vector3 initialVelocity = (horizontalDir * hVel) + (Vector3.up * vVel);

            projectileScript.InitializeAsParabolic(
                initialVelocity, finalDirectDamage, finalAoeDamage, statsToUse.baseProjectileLifetime,
                statsToUse.explosionEffectPrefab, finalAoeRadius, statsToUse.layersToDamageByAOE, statsToUse.layersToExplodeOn,
                finalDotDamage, finalDotDuration, finalDotTickInterval
            );
        }
    }

    private void InstantiateAndFireBoomerang(Vector3 direction)
    {
        if (firePoint == null || StatBlock.projectilePrefab == null)
        {
            // isBoomerangOut = false; // 发射失败时重置状态 (如果您有 isBoomerangOut 变量)
            Debug.LogError($"[WeaponPart] Boomerang fire failed: FirePoint or ProjectilePrefab missing for {StatBlock?.weaponName}");
            return;
        }

        GameObject bullet = Instantiate(StatBlock.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));

        int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);
        float finalSpeed = StatBlock.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
        float finalMaxDistance = StatBlock.maxDistance; // 暂时不用乘数
        float finalScale = PlayerStats.Instance.aoeRadiusMultiplier;
        bullet.transform.localScale = Vector3.one * finalScale;

        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // --- 调用新的初始化方法，并传递 overshoot ---
            projectileScript.InitializeAsBoomerang(
                direction,
                finalSpeed,
                finalDamage,
                finalMaxDistance,
                StatBlock.rotationSpeed,
                StatBlock.returnOvershootDistance // <-- 传递这个新值
            );
        }
        else
        {
            // isBoomerangOut = false; // 初始化失败时重置状态
            Debug.LogError($"[WeaponPart] Boomerang fire failed: Projectile script missing on prefab for {StatBlock?.weaponName}");
            Destroy(bullet);
        }
    }

    private IEnumerator ChainDamageRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange)
    {
        var hitEnemies = new List<Health>();
        Vector3 lastHitPosition = firePoint.position; // Start chain from fire point

        while (currentTarget != null && remainingChains >= 0)
        {
            Vector3 currentTargetPosition = currentTarget.position;
            Health targetHealth = currentTarget.GetComponent<Health>(); // Get Health component

            // Check if valid target and not already hit in this chain
            if (targetHealth != null && !hitEnemies.Contains(targetHealth) && !targetHealth.IsDead)
            {
                hitEnemies.Add(targetHealth);
                // Use targetHealth.TakeDamage directly
                targetHealth.TakeDamage(damage, currentTargetPosition, this.gameObject); // Pass damage source position and attacker

                // Instantiate VFX between last position and current target
                if (lightningChainPrefab != null)
                {
                    var chainVFX_GO = Instantiate(lightningChainPrefab, Vector3.zero, Quaternion.identity); // Instantiate at origin temporarily
                    chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, currentTargetPosition); // Setup positions
                }

                // Instantiate impact effect at the target
                if (StatBlock.impactEffectPrefab != null) // Use StatBlock impact effect
                {
                    Instantiate(StatBlock.impactEffectPrefab, currentTargetPosition, Quaternion.identity);
                }
            }

            yield return new WaitForSeconds(0.05f); // Small delay between chains

            // Find the next target
            Transform nextTarget = FindNextChainTarget(currentTargetPosition, chainRange, hitEnemies);
            remainingChains--;
            lastHitPosition = currentTargetPosition; // Update last hit position
            currentTarget = nextTarget;
        }
    }

    public void RefreshOrbiters()
    {
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital || !isOrbitalActive) return;
        // Debug.Log($"<color=orange>[WeaponPart] Refreshing orbiters for '{myStatBlock.weaponName}'...</color>");
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false;
        StopCoroutine(nameof(OrbitalLifetimeRoutine)); // Stop specific coroutine by name
        SetupOrbiters(); // Re-setup with current stats
    }

    private void SetupOrbiters()
    {
        Transform stableAnchor = FindStableAnchor();
        if (stableAnchor == null)
        {
            Debug.LogWarning($"StableAnchor not found! Using WeaponController transform as fallback.", this);
            stableAnchor = WeaponController.Instance.transform;
        }

        orbitalPivot = new GameObject($"{StatBlock.weaponName}_Pivot").transform;
        orbitalPivot.SetParent(stableAnchor);
        orbitalPivot.localPosition = Vector3.zero;
        orbitalPivot.localRotation = Quaternion.identity; // Ensure pivot starts unrotated

        isOrbitalActive = true;
        int finalOrbitalCount = StatBlock.baseOrbitalCount + PlayerStats.Instance.bonusOrbitalCount;
        float finalOrbitalRadius = StatBlock.baseOrbitalRadius * PlayerStats.Instance.aoeRadiusMultiplier; // Scale radius
        int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus); // Use direct damage stats

        for (int i = 0; i < finalOrbitalCount; i++)
        {
            if (StatBlock.orbitalPrefab == null) continue;
            float angle = i * (360f / finalOrbitalCount);
            // Spawn relative to pivot's *local* forward/right
            Vector3 spawnPos = Quaternion.Euler(0, angle, 0) * (Vector3.forward * finalOrbitalRadius);
            GameObject orbiterGO = Instantiate(StatBlock.orbitalPrefab, orbitalPivot);
            orbiterGO.transform.localPosition = spawnPos;
            orbiterGO.GetComponent<Orbiter>()?.Initialize(finalDamage);
        }

        float finalDuration = StatBlock.baseDuration; // Add upgrades later if needed
        if (finalDuration > 0)
        {
            StartCoroutine(OrbitalLifetimeRoutine(finalDuration));
        }
        // Debug.Log($"Setup Orbiters: Count={finalOrbitalCount}, Radius={finalOrbitalRadius}, Damage={finalDamage}, Duration={finalDuration}");
    }

    private IEnumerator OrbitalLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (orbitalPivot != null) Destroy(orbitalPivot.gameObject);
        isOrbitalActive = false;
        orbitalCooldownTimer = 1f / StatBlock.baseFireRate; // Use standard cooldown after duration ends
        // Debug.Log($"Orbital '{StatBlock.weaponName}' duration ended, starting cooldown: {orbitalCooldownTimer}s.");
    }

    private Transform FindNearestEnemyTransform() { return FindNearestEnemyTransform(transform.position, StatBlock.autoAimRange); }

    private Transform FindNearestEnemyTransform(Vector3 searchCenter, float searchRadius)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;
        if (StatBlock == null) return null; // Safety check

        // Ensure layersToDamageByAOE includes the "Enemies" layer or is set appropriately
        LayerMask layersToSearch = StatBlock.layersToDamageByAOE == 0 ? LayerMask.GetMask("Enemies") : StatBlock.layersToDamageByAOE;

        Collider[] colliders = Physics.OverlapSphere(searchCenter, searchRadius, layersToSearch);
        foreach (Collider hitCollider in colliders)
        {
            Health enemyHealth = hitCollider.GetComponentInParent<Health>();
            // Ensure it's an enemy, alive, and use CompareTag for reliability
            if (enemyHealth != null && !enemyHealth.IsDead && hitCollider.CompareTag("Enemy"))
            {
                float dSqrToTarget = (searchCenter - hitCollider.transform.position).sqrMagnitude;
                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    nearestEnemy = enemyHealth.transform; // Target the transform of the Health component
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
        if (beamCooldownTimer > 0f) return; // In cooldown

        ValidateOrFindTarget(); // Find/check target

        if (lockedBeamTarget != null) // Have a valid target
        {
            if (beamEnergyTimer > 0f && activeBeamInstance == null)
            {
                StartBeam(); // Start beam if energy available and not already active
            }
            if (activeBeamInstance != null) // If beam is active
            {
                beamEnergyTimer -= Time.deltaTime; // Consume energy
                if (beamEnergyTimer <= 0f)
                {
                    StopBeamAndStartCooldown(); // Energy depleted
                }
            }
        }
        else // No valid target
        {
            if (activeBeamInstance != null)
            {
                StopBeamForStandby(); // Stop beam if no target
            }
        }
    }

    private void ValidateOrFindTarget()
    {
        if (lockedBeamTarget != null)
        {
            // Check if target is still active and within range
            if (!lockedBeamTarget.gameObject.activeInHierarchy || Vector3.Distance(firePoint.position, lockedBeamTarget.position) > StatBlock.beamMaxDistance)
            {
                lockedBeamTarget = null; // Invalidate target
            }
        }
        if (lockedBeamTarget == null)
        {
            lockedBeamTarget = FindNearestEnemyTransform(firePoint.position, StatBlock.beamMaxDistance); // Find new target within range
        }
    }

    private void StartBeam()
    {
        if (StatBlock.beamVfxPrefab == null || firePoint == null) return;

        GameObject beamGO = Instantiate(StatBlock.beamVfxPrefab, firePoint.position, firePoint.rotation, firePoint); // Parent to firepoint
        activeBeamInstance = beamGO.GetComponent<PlayerBeamController>();

        if (activeBeamInstance != null)
        {
            activeBeamInstance.Initialize(StatBlock, this.gameObject, lockedBeamTarget);
            if (beamLoopSound != null && audioSource != null && !audioSource.isPlaying) // Play loop sound only if not already playing
            {
                audioSource.clip = beamLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            Debug.LogError($"Beam VFX prefab '{StatBlock.beamVfxPrefab.name}' is missing PlayerBeamController script!", this);
            Destroy(beamGO); // Clean up invalid instance
        }
    }

    private void StopBeamForStandby() // Target lost or out of range
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        lockedBeamTarget = null; // Lose lock
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); } // Stop loop sound
    }

    private void StopBeamAndStartCooldown() // Energy depleted
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        beamCooldownTimer = StatBlock.beamCooldown;
        beamEnergyTimer = 0; // Ensure energy is zero
        lockedBeamTarget = null; // Lose lock
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); } // Stop loop sound
        // Debug.Log($"Beam energy depleted, starting cooldown: {beamCooldownTimer}s.");
    }

    public void DeactivateBeam() // Called when weapon is unequipped
    {
        StopBeamForStandby(); // Just stop it, don't trigger cooldown
        beamEnergyTimer = 0; // Reset energy as well
    }

    private Transform FindStableAnchor()
    {
        if (WeaponController.Instance == null) return transform; // Fallback
        StableAnchorMarker marker = WeaponController.Instance.GetComponentInChildren<StableAnchorMarker>();
        return marker != null ? marker.transform : WeaponController.Instance.transform; // Return marker or controller transform
    }

    private void InstantiateAndInitializeDrones()
    {
        if (StatBlock == null || StatBlock.summonPrefab == null || StatBlock.summonWeaponStats == null) return;

        int finalSummonCount = StatBlock.summonCount; // Add upgrades later
        float finalSummonDuration = StatBlock.summonDuration; // Add upgrades later

        Transform playerRoot = WeaponController.Instance.transform; // Get player root

        for (int i = 0; i < finalSummonCount; i++)
        {
            // Spawn around the player root
            Vector3 spawnOffset = Random.insideUnitSphere * 2f; // Smaller radius around player
            spawnOffset.y = StatBlock.summonSpawnHeight; // Use defined height
            Vector3 spawnPosition = playerRoot.position + spawnOffset;

            GameObject droneGO = Instantiate(StatBlock.summonPrefab, spawnPosition, Quaternion.identity); // Instantiate at world position
            DroneAI droneAI = droneGO.GetComponent<DroneAI>();
            if (droneAI != null)
            {
                droneAI.Initialize(StatBlock.summonWeaponStats, finalSummonDuration, playerRoot); // Pass player root as master
            }
        }
    }

    private void InstantiateAndFireAirdropDeployer(Vector3 targetPosition)
    {
        if (StatBlock.deployerProjectilePrefab == null || firePoint == null) return;

        float spawnHeight = StatBlock.deployerSpawnHeight;
        float horizontalOffset = 5f; // Example offset

        // Calculate spawn position above and slightly offset from the target
        Vector3 directionFromPlayer = (targetPosition - firePoint.position).normalized;
        directionFromPlayer.y = 0; // Horizontal direction only
        Vector3 startPosition = targetPosition + (Vector3.up * spawnHeight) - (directionFromPlayer * horizontalOffset);
        Vector3 fallDirection = (targetPosition - startPosition).normalized; // Direction towards target

        GameObject deployerGO = Instantiate(StatBlock.deployerProjectilePrefab, startPosition, Quaternion.LookRotation(fallDirection));
        Projectile projectileScript = deployerGO.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            int finalDamage = Mathf.RoundToInt(StatBlock.baseAreaDamagePerTick * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus); // Include flat bonus
            float finalDuration = StatBlock.baseAreaDuration; // Add upgrades later
            float finalInterval = StatBlock.baseAreaTickInterval; // Add upgrades later (lower is faster ticks)

            projectileScript.InitializeAsAirdropDeployer(
                startPosition, fallDirection, StatBlock.deployerFallSpeed, StatBlock.areaPrefab,
                finalDamage, finalDuration, finalInterval, this.gameObject // Pass this WeaponPart's GameObject as attacker
            );
        }
    }


    private void HandleLandminePlacement()
    {
        if (!IsReadyToFire) return; // Check cooldown

        Vector2 randomCirclePoint = Random.insideUnitCircle * StatBlock.spawnRadius;
        // Place relative to the player's root position, not the weapon part itself
        Vector3 spawnPositionBase = WeaponController.Instance.transform.position + new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);

        RaycastHit hit;
        Vector3 spawnPosition = spawnPositionBase; // Default if raycast fails
        if (Physics.Raycast(spawnPositionBase + Vector3.up * 5f, Vector3.down, out hit, 10f, StatBlock.beamScorchMarkGroundLayer != 0 ? StatBlock.beamScorchMarkGroundLayer : LayerMask.GetMask("Ground"))) // Use beam ground layer or default "Ground"
        {
            spawnPosition = hit.point; // Place exactly on ground
        }

        if (StatBlock.minePrefab != null)
        {
            GameObject mineGO = Instantiate(StatBlock.minePrefab, spawnPosition, Quaternion.identity); // Default rotation
            Landmine mineScript = mineGO.GetComponent<Landmine>();
            if (mineScript != null)
            {
                int finalDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus); // Use AOE stats
                float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
                mineScript.Initialize(
                    finalDamage, finalRadius, StatBlock.armingTime, StatBlock.mineDuration,
                    WeaponController.Instance.gameObject, // Attacker is the player controller
                    StatBlock.explosionEffectPrefab, // Use explosion effect from StatBlock
                    StatBlock.layersToDamageByAOE // Use layers from StatBlock
                );
            }
            if (landminePlaceSound != null && audioSource != null)
            {
                AudioSource.PlayClipAtPoint(landminePlaceSound, spawnPosition); // Play sound at placement location
            }
        }

        // Reset cooldown after placing
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
    }
    #endregion
}