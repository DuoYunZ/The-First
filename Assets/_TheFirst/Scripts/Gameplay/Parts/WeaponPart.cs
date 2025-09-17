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
    public float fireSoundDelay = -0.05f; // 默认设置为提前0.05秒


    private AudioSource audioSource;

    // 由 WeaponController 在运行时赋值
    public WeaponStatBlock StatBlock
    {
        get { return myStatBlock; } // 假设您有get
        set
        {
            myStatBlock = value;
            // 【新增日志】
            Debug.Log($"WeaponPart '{gameObject.name}' 已装备武器: '{myStatBlock.weaponName}', 行为类型: {myStatBlock.behavior}");
        }
    }

    // 内部计时器和状态
    private float fireCooldown = 0f;
    private float orbitalCooldownTimer = 0f;
    private bool isOrbitalActive = false;
    private Transform orbitalPivot;

    public bool IsReadyToFire => fireCooldown <= 0f;

    // --- 【新增】光束武器专用变量 ---
    private PlayerBeamController activeBeamInstance = null;
    private float beamEnergyTimer = 0f;  // 代表光束剩余的“能量”或“总持续时间”
    private float beamCooldownTimer = 0f; // 光束冷却计时器
    // --- 【新增】用于存储当前锁定的目标 ---
    private Transform lockedBeamTarget = null;



    #region Unity Lifecycle Methods
    void Awake() // 【修改】将 Start() 的内容移到 Awake()，确保 AudioSource 尽早被获取
    {
        // 确保 AudioSource 存在
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
        }
    }
    void Start()
    {
        // 在开始时，为光束武器充满能量
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
        }      
    }
    void Update()
    {
        // 更新所有冷却计时器
        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;
        if (orbitalCooldownTimer > 0f) orbitalCooldownTimer -= Time.deltaTime;
        if (beamCooldownTimer > 0f)
        {
            beamCooldownTimer -= Time.deltaTime;
            // 当冷却结束时，重新为光束充满能量
            if (beamCooldownTimer <= 0)
            {
                beamEnergyTimer = StatBlock.beamDuration;
                Debug.Log("光束冷却结束，能量已充满！");
            }
        }

        // 如果是轨道武器，则执行旋转
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital && orbitalPivot != null)
        {
            float finalOrbitalSpeed = StatBlock.baseOrbitalSpeed; //未來可擴充
            orbitalPivot.Rotate(Vector3.up, finalOrbitalSpeed * Time.deltaTime);
        }

        if (StatBlock.behavior == WeaponBehaviorType.Landmine)
        {
            HandleLandminePlacement();
        }
        // --- 自动发射的光束武器 ---
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            HandleBeamWeapon();
        }
    }

    private void OnDestroy()
    {
        // 确保在武器部件被销毁时，它创建的独立物件（如轨道轴心）也被一并销毁
        if (orbitalPivot != null)
        {
            Destroy(orbitalPivot.gameObject);
        }
        if (activeBeamInstance != null)
        {
            Destroy(activeBeamInstance.gameObject);
        }
    }

    #endregion

    #region Public Control Methods

    /// <summary>
    /// 启动武器。在 WeaponController 中赋值完 StatBlock 后呼叫。
    /// </summary>
    public void Activate()
    {
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
            beamCooldownTimer = 0; // 确保新装备的武器不在冷却中
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            // 如果是轨道武器，且不在冷却中，则立即启动
            if (!isOrbitalActive && orbitalCooldownTimer <= 0)
            {
                SetupOrbiters();
            }
        }
    }

    /// <summary>
    /// 开火指令，由 WeaponController 在每一帧调用
    /// </summary>
    public void Fire(Vector3 initialDirection)
    {
        if (StatBlock == null || !IsReadyToFire) return;
        StartCoroutine(FireRoutine(initialDirection));
               
    }
    private IEnumerator FireRoutine(Vector3 initialDirection)
    {
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
        if (fireSoundDelay < 0)
        {
            PlayFireSound(); // 先播放声音
            yield return new WaitForSeconds(Mathf.Abs(fireSoundDelay)); // 再等待
        }
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam) yield break;

        


        // --- 1. 处理轨道武器的特殊逻辑 ---
        if (StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            if (!isOrbitalActive && orbitalCooldownTimer <= 0)
            {
                SetupOrbiters();
            }
            yield break; // 轨道武器不执行后续的发射逻辑
        }

        // --- 2. 处理所有发射型武器的逻辑 ---
        Vector3 finalTargetDirection = initialDirection;
        bool isControlledByDrone = GetComponentInParent<DroneAI>() != null;
        Transform firstTarget = null; // 用于连锁闪电和自动瞄准

        if (StatBlock.autoAimAtNearestEnemy && GetComponentInParent<DroneAI>() == null)
        {
            Transform nearestEnemy = FindNearestEnemyTransform();
            if (nearestEnemy != null)
            {
                // 玩家的自动瞄准，我们依然希望它是水平的
                Vector3 directionToEnemy = (nearestEnemy.position - firePoint.position);
                directionToEnemy.y = 0;
                finalTargetDirection = directionToEnemy.normalized;
            }
            else
            {
                // 如果是自动瞄准武器但没找到敌人，则不开火
                yield break;
            }
        }

        if (finalTargetDirection.sqrMagnitude < 0.01f) yield break;

        // 3. 根据武器行为执行不同的发射方式
        switch (StatBlock.behavior)
        {
            case WeaponBehaviorType.Standard:
            case WeaponBehaviorType.Pierce:
                InstantiateAndFireProjectile(finalTargetDirection);
                break;

            case WeaponBehaviorType.ParabolicAOE:
                // 這裡應該呼叫一個專門發射拋物線炮彈的方法
                // 但如果這個方法不存在或被註解掉了，炮彈就發射不出去
                InstantiateAndFireParabolicProjectile(finalTargetDirection, StatBlock);
                break;
            case WeaponBehaviorType.Chain:
                if (firstTarget != null)
                {
                    int finalChainCount = StatBlock.baseChainCount; //未來可擴充
                    int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier);
                    StartCoroutine(ChainDamageRoutine(firstTarget, finalChainCount, finalDamage, StatBlock.chainRange));
                }
                break;

            case WeaponBehaviorType.SummonDrone:
                InstantiateAndInitializeDrones();

                if (StatBlock.isOneShot)
                {
                    this.enabled = false;
                    Debug.Log($"一次性召唤技能 '{StatBlock.weaponName}' 已执行完毕并禁用。");
                }
                break; // 结束

            case WeaponBehaviorType.PersistentAOE:
                // 1. 自動尋找最近的敵人作為目標
                Transform targetEnemy = FindNearestEnemyTransform();

                // 2. 如果找到了目標，則執行發射
                if (targetEnemy != null)
                {
                    // 3. 計算目標位置和 "天空" 中的起始位置
                    Vector3 targetPosition = targetEnemy.position;
                    float spawnHeight = StatBlock.deployerSpawnHeight;
                    float horizontalOffset = 5f; // 水平偏移量，製造傾斜感，可以設為 StatBlock 的屬性

                    // 從目標位置正上方，再稍微向玩家方向偏移一點，作為起始點
                    Vector3 directionFromPlayer = (targetPosition - transform.position).normalized;
                    directionFromPlayer.y = 0; // 只考慮水平方向

                    Vector3 startPosition = targetPosition + (Vector3.up * spawnHeight) - (directionFromPlayer * horizontalOffset);

                    // 4. 計算從起始位置指向目標位置的飛行方向
                    Vector3 fallDirection = (targetPosition - startPosition).normalized;

                    // 5. 實例化部署器
                    if (StatBlock.deployerProjectilePrefab != null)
                    {
                        // 在計算出的空中起始點創建部署器，並讓它朝向目標
                        GameObject deployerGO = Instantiate(StatBlock.deployerProjectilePrefab, startPosition, Quaternion.LookRotation(fallDirection));
                        Projectile projectileScript = deployerGO.GetComponent<Projectile>();

                        if (projectileScript != null)
                        {
                            // 6. 獲取傷害、持續時間等屬性
                            int finalDamage = Mathf.RoundToInt(StatBlock.baseAreaDamagePerTick * PlayerStats.Instance.aoeDamageMultiplier);
                            float finalDuration = StatBlock.baseAreaDuration;
                            float finalInterval = StatBlock.baseAreaTickInterval;

                            // 7. 呼叫我們修改後的初始化方法
                            projectileScript.InitializeAsAirdropDeployer(
                                startPosition,
                                fallDirection,
                                StatBlock.deployerFallSpeed,
                                StatBlock.areaPrefab,
                                finalDamage,
                                finalDuration,
                                finalInterval,
                                this.gameObject
                            );
                        }
                    }
                }
                // 如果沒找到敵人，則不發射，這符合自動攻擊武器的行為
                break;
        }

        if (StatBlock.muzzleFlashPrefab != null)
        {
            Instantiate(StatBlock.muzzleFlashPrefab, firePoint.position, firePoint.rotation);
        }
        if (fireSoundDelay >= 0)
        {
            yield return new WaitForSeconds(fireSoundDelay);
            PlayFireSound();
        }
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
    // --- 发射直线弹 (标准/穿透) ---
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

            // --- 新增：獲取燃燒屬性 ---
            int finalDotDamage = Mathf.RoundToInt(StatBlock.baseDotDamage * PlayerStats.Instance.aoeDamageMultiplier);
            float finalDotDuration = StatBlock.baseDotDuration;
            float finalDotTickInterval = StatBlock.dotTickInterval;
            float finalSlowPercentage = StatBlock.baseSlowPercentage; // + PlayerStats...
            float finalSlowDuration = StatBlock.baseSlowDuration; // + PlayerStats...

            projectileScript.InitializeAsStraight(
              direction, finalSpeed, finalDamage, false, finalPierceCount, 
              StatBlock.baseProjectileLifetime, StatBlock.shieldImpactEffectPrefab, StatBlock.defaultImpactEffectPrefab,
              finalDotDamage, finalDotDuration, finalDotTickInterval, finalSlowPercentage, // <--- 传入减速百分比
            finalSlowDuration, AttackType.Standard// <-- 傳入燃燒參數
            );
        }
    }

    // --- 发射抛物线弹 ---
    private void InstantiateAndFireParabolicProjectile(Vector3 horizontalDir, WeaponStatBlock statsToUse)
    {
        if (firePoint == null || statsToUse.projectilePrefab == null) return;

        // 1. 計算所有最終屬性
        int finalDirectDamage = Mathf.RoundToInt(statsToUse.baseDirectDamage * PlayerStats.Instance.damageMultiplier + PlayerStats.Instance.flatDamageBonus);
        int finalAoeDamage = Mathf.RoundToInt(statsToUse.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
        float finalAoeRadius = statsToUse.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
        float finalLaunchForce = statsToUse.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;

        // 燃燒相關屬性
        int finalDotDamage = Mathf.RoundToInt(statsToUse.baseDotDamage * PlayerStats.Instance.aoeDamageMultiplier);
        float finalDotDuration = statsToUse.baseDotDuration;
        float finalDotTickInterval = statsToUse.dotTickInterval;

        // 2. 實例化炮彈，初始旋轉可以朝向目標水平方向
        GameObject bullet = Instantiate(statsToUse.projectilePrefab, firePoint.position, Quaternion.LookRotation(horizontalDir));
        Projectile projectileScript = bullet.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            // 3. 計算拋物線的初始速度向量
            float angleRad = statsToUse.launchAngle * Mathf.Deg2Rad;
            float hVel = finalLaunchForce * Mathf.Cos(angleRad);
            float vVel = finalLaunchForce * Mathf.Sin(angleRad);
            Vector3 initialVelocity = (horizontalDir * hVel) + (Vector3.up * vVel);

            // 4. 呼叫炮彈的初始化方法，傳入所有計算好的參數
            projectileScript.InitializeAsParabolic(
                initialVelocity,
                finalDirectDamage,
                finalAoeDamage,
                statsToUse.baseProjectileLifetime,
                statsToUse.explosionEffectPrefab,
                finalAoeRadius,
                statsToUse.layersToDamageByAOE,
                statsToUse.layersToExplodeOn,
                finalDotDamage,
                finalDotDuration,
                finalDotTickInterval
            );
        }
    }

    // --- 连锁闪电协程 ---
    private IEnumerator ChainDamageRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange)
    {
        var hitEnemies = new List<Health>();
        Vector3 lastHitPosition = firePoint.position;

        while (currentTarget != null && remainingChains >= 0)
        {
            Vector3 currentTargetPosition = currentTarget.position;
            Health targetHealth = currentTarget.GetComponent<Health>();

            if (targetHealth != null && !hitEnemies.Contains(targetHealth))
            {
                hitEnemies.Add(targetHealth);
                targetHealth.TakeDamage(damage, transform.position, gameObject); // 修正：传入攻击者

                if (lightningChainPrefab != null)
                {
                    var chainVFX_GO = Instantiate(lightningChainPrefab, Vector3.zero, Quaternion.identity);
                    chainVFX_GO.GetComponent<ChainLightningVFX>().Setup(lastHitPosition, currentTargetPosition);
                }

                if (StatBlock.impactEffectPrefab != null)
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
    public void RefreshOrbiters()
    {
        // 检查1：这个方法只对已激活的轨道武器有效
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital || !isOrbitalActive)
        {
            return;
        }

        Debug.Log($"<color=orange>[WeaponPart] 接收到刷新指令，正在重新生成 '{myStatBlock.weaponName}'...</color>");

        // 步骤1：销毁旧的轨道轴心和所有轨道物
        if (orbitalPivot != null)
        {
            Destroy(orbitalPivot.gameObject);
        }

        // 清理旧的状态，防止意外的协程运行
        isOrbitalActive = false;
        StopAllCoroutines(); // 停止旧的生命周期计时器

        // 步骤2：立即使用最新的属性重新执行一次完整的设置流程
        SetupOrbiters();
    }
    // --- 轨道武器初始化 ---
    private void SetupOrbiters()
    {
        Transform stableAnchor = FindStableAnchor();
        if (stableAnchor == null)
        {
            Debug.LogError($"未能在玩家机甲上找到 'StableAnchor'！将使用根对象作为后备。");
            stableAnchor = WeaponController.Instance.transform;
        }

        orbitalPivot = new GameObject($"{StatBlock.weaponName}_Pivot").transform;
        orbitalPivot.SetParent(stableAnchor);
        orbitalPivot.localPosition = Vector3.zero;

        isOrbitalActive = true;
        int finalOrbitalCount = StatBlock.baseOrbitalCount + PlayerStats.Instance.bonusOrbitalCount;
        float finalOrbitalRadius = StatBlock.baseOrbitalRadius;
        int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier);

        for (int i = 0; i < finalOrbitalCount; i++)
        {
            if (StatBlock.orbitalPrefab == null) continue;
            float angle = i * (360f / finalOrbitalCount);
            Vector3 spawnPos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad)) * finalOrbitalRadius;
            GameObject orbiterGO = Instantiate(StatBlock.orbitalPrefab, orbitalPivot);
            orbiterGO.transform.localPosition = spawnPos;
            orbiterGO.GetComponent<Orbiter>()?.Initialize(finalDamage);
        }

        float finalDuration = StatBlock.baseDuration;
        if (finalDuration > 0)
        {
            StartCoroutine(OrbitalLifetimeRoutine(finalDuration));
        }
    }



    // --- 轨道武器生命周期协程 ---
    private IEnumerator OrbitalLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (orbitalPivot != null) Destroy(orbitalPivot.gameObject);
        isOrbitalActive = false;
        orbitalCooldownTimer = 1f / StatBlock.baseFireRate;
        Debug.Log($"轨道武器 '{StatBlock.weaponName}' 持续时间结束，进入冷却: {orbitalCooldownTimer} 秒。");
    }

    // --- 索敌方法 ---
    private Transform FindNearestEnemyTransform()
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;
        if (StatBlock == null) return null;

        Collider[] colliders = Physics.OverlapSphere(transform.position, StatBlock.autoAimRange, StatBlock.layersToDamageByAOE);
        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                float dSqrToTarget = (transform.position - hitCollider.transform.position).sqrMagnitude;
                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    Health enemyHealth = hitCollider.GetComponentInParent<Health>();
                    if (enemyHealth != null) nearestEnemy = enemyHealth.transform;
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
    // --- 【新增】光束武器的完整处理逻辑 ---
    private void HandleBeamWeapon()
    {
        // 1. 如果正在冷却，直接返回
        if (beamCooldownTimer > 0f) return;

        // 2. 验证当前目标或寻找新目标
        ValidateOrFindTarget();

        // 3. 根据目标和能量状态决定行为
        if (lockedBeamTarget != null)
        {
            // 有目标：尝试启动或维持光束
            if (beamEnergyTimer > 0f && activeBeamInstance == null)
            {
                StartBeam();
            }

            if (activeBeamInstance != null)
            {
                // 如果光束是激活的，就消耗能量
                beamEnergyTimer -= Time.deltaTime;
                if (beamEnergyTimer <= 0f)
                {
                    StopBeamAndStartCooldown();
                }
            }
        }
        else
        {
            // 没目标：如果光束还开着，就让它进入待机
            if (activeBeamInstance != null)
            {
                StopBeamForStandby();
            }
        }
    }
    private void ValidateOrFindTarget()
    {
        // 检查已锁定的目标是否仍然有效
        if (lockedBeamTarget != null)
        {
            if (!lockedBeamTarget.gameObject.activeInHierarchy || Vector3.Distance(transform.position, lockedBeamTarget.position) > StatBlock.beamMaxDistance)
            {
                lockedBeamTarget = null;
            }
        }

        // 如果没有锁定目标，则寻找一个新的
        if (lockedBeamTarget == null)
        {
            lockedBeamTarget = FindNearestEnemyTransform();
            // 再次验证新找到的目标是否在射程内
            if (lockedBeamTarget != null && Vector3.Distance(transform.position, lockedBeamTarget.position) > StatBlock.beamMaxDistance)
            {
                lockedBeamTarget = null;
            }
        }
    }
    private void StartBeam()
    {
        if (StatBlock.beamVfxPrefab == null) return;

        // 确保预制件上挂载的是 PlayerBeamController
        GameObject beamGO = Instantiate(StatBlock.beamVfxPrefab, firePoint.position, firePoint.rotation, firePoint);
        activeBeamInstance = beamGO.GetComponent<PlayerBeamController>();

        if (activeBeamInstance != null)
        {
            // 使用新脚本的初始化方法
            activeBeamInstance.Initialize(StatBlock, this.gameObject, lockedBeamTarget);
        }
        if (beamLoopSound != null && audioSource != null)
        {
            audioSource.clip = beamLoopSound;
            audioSource.loop = true; // 确保音效循环
            audioSource.Play();
        }
    }



    // 待机时，也应该解除锁定，以便下次能寻找新目标
    private void StopBeamForStandby()
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        lockedBeamTarget = null;

        if (audioSource != null) audioSource.Stop();
    }

    // 能量耗尽时，也要解除锁定
    private void StopBeamAndStartCooldown()
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;

        beamCooldownTimer = StatBlock.beamCooldown;
        beamEnergyTimer = 0;
        lockedBeamTarget = null;

        if (audioSource != null) audioSource.Stop();
    }



    // 当武器被卸下或切换时，确保光束停止
    public void DeactivateBeam()
    {
        {
            StopBeamForStandby(); // 切换武器时使用待机逻辑即可
        }
    }


    private Transform FindStableAnchor()
    {
        if (WeaponController.Instance == null) return null;

        // 在 WeaponController 所在的整個物件及其所有子物件中，尋找掛載了 StableAnchorMarker 的那個
        StableAnchorMarker marker = WeaponController.Instance.GetComponentInChildren<StableAnchorMarker>();

        if (marker != null)
        {
            return marker.transform;
        }

        // 如果找不到，再執行後備邏輯
        return null;
    }
    private void InstantiateAndInitializeDrones()
    {
        if (StatBlock == null || StatBlock.summonPrefab == null || StatBlock.summonWeaponStats == null)
        {
            Debug.LogError("召唤失败：WeaponStatBlock 中的 summonPrefab 或 summonWeaponStats 未设置！");
            return;
        }

        int finalSummonCount = StatBlock.summonCount;
        float finalSummonDuration = StatBlock.summonDuration;

        for (int i = 0; i < finalSummonCount; i++)
        {
            Vector3 spawnOffset = Random.insideUnitSphere * 3f;
            spawnOffset.y = StatBlock.summonSpawnHeight;
            Vector3 spawnPosition = transform.position + spawnOffset;

            // 【核心修改】Instantiate 时不再指定父级，让无人机生成在场景的根目录
            GameObject droneGO = Instantiate(StatBlock.summonPrefab, spawnPosition, Quaternion.identity);

            DroneAI droneAI = droneGO.GetComponent<DroneAI>();

            if (droneAI != null)
            {
                // 【重要】将 WeaponController.Instance.transform (即玩家的根对象) 作为主人传进去
                droneAI.Initialize(this.StatBlock.summonWeaponStats, finalSummonDuration, WeaponController.Instance.transform);
            }
        }
    }
    private void HandleLandminePlacement()
    {
        // 如果冷却未结束，则不执行
        if (!IsReadyToFire) return;

        // 1. 计算随机生成位置
        Vector2 randomCirclePoint = Random.insideUnitCircle * StatBlock.spawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);

        // (可选，但推荐) 增加一个射线检测，确保地雷生成在地面上
        RaycastHit hit;
        if (Physics.Raycast(spawnPosition + Vector3.up * 5f, Vector3.down, out hit, 10f, LayerMask.GetMask("Ground")))
        {
            spawnPosition = hit.point;
        }

        // 2. 实例化地雷
        if (StatBlock.minePrefab != null)
        {
            GameObject mineGO = Instantiate(StatBlock.minePrefab, spawnPosition, Quaternion.identity);
            Landmine mineScript = mineGO.GetComponent<Landmine>();

            if (mineScript != null)
            {
                // 3. 计算最终伤害和范围
                int finalDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier);
                float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;

                // 4. 初始化地雷
                mineScript.Initialize(
                    finalDamage,
                    finalRadius,
                    StatBlock.armingTime,
                    StatBlock.mineDuration,
                    WeaponController.Instance.gameObject, // 攻击者是玩家
                    StatBlock.explosionEffectPrefab,    // 使用抛物线弹的爆炸特效
                    StatBlock.layersToDamageByAOE
                );
            }
            if (landminePlaceSound != null && audioSource != null)
            {
                // 使用 PlayClipAtPoint 可以在地雷实际放置的位置播放音效，效果更佳
                AudioSource.PlayClipAtPoint(landminePlaceSound, spawnPosition);
            }
        }

        // 5. 重置冷却计时器
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
    }
    #endregion
}