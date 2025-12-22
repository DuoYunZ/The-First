using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponPart : MonoBehaviour
{
    [Header("武器数据蓝图 (在预制件中设置)")]
    public WeaponStatBlock myStatBlock;

    public int currentLevel = 1;
    public int maxLevel = 10;

    [Header("组件引用 (在预制件中设置)")]
    public Transform firePoint;

    [Header("视觉表现组件 (可选)")]
    [Tooltip("控制背后悬浮武器动画的脚本")]
    public FloatingWeaponController floatingVisual;
    [Tooltip("控制材质发光冷却的脚本")]
    public WeaponCooldownMaterial cooldownMaterial;
    [Tooltip("开火时隐藏武器模型的时间 (模拟挥砍/发射动作)")]
    public float hideVisualDuration = 0.15f; // 远程通常比近战快，0.15秒比较合适

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
    private float auraDebuffRefreshTimer = 0f; //
    private HashSet<StatusEffectReceiver> aura_ActiveSlows = new HashSet<StatusEffectReceiver>(); //
    private HashSet<StatusEffectReceiver> aura_ActiveWeaKens = new HashSet<StatusEffectReceiver>(); //
    private HashSet<StatusEffectReceiver> aura_ActiveCorrodes = new HashSet<StatusEffectReceiver>(); // <--- [新增]
    private float auraTickTimer = 0f;
    private SphereCollider auraCollider;
    private GameObject auraVfxInstance;
    [Tooltip("光环只应检测这些层上的敌人")]
    public LayerMask enemyLayerMask; // <--- vvv 新增

    [Tooltip("光环磁铁应检测的掉落物层")]
    public LayerMask pickupLayerMask;

    private AudioSource audioSource;

    [Header("能量石 (运行时)")]
    [Tooltip("此武器当前镶嵌的能量石")]
    public EnergyStoneSO currentStone { get; private set; }

    private float auraKnockbackTimer = 0f;

    private float auraMagnetTimer = 0f;


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
        if (StatBlock != null) maxLevel = StatBlock.maxLevel;
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam) beamEnergyTimer = StatBlock.beamDuration;
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Aura) SetupAura();

        // Robust Component Finding
        if (floatingVisual == null) floatingVisual = GetComponentInChildren<FloatingWeaponController>();
        if (cooldownMaterial == null) cooldownMaterial = GetComponentInChildren<WeaponCooldownMaterial>();

        // Initialize Color from Weapon Data
        if (cooldownMaterial != null && StatBlock != null)
        {
            // Default to StatBlock color
            if (StatBlock.weaponGlowColor.maxColorComponent > 0)
                cooldownMaterial.SetEmissionColor(StatBlock.weaponGlowColor);
        }
        UpdateVisualModel();
    }
    void Update()
    {
        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;
        if (orbitalCooldownTimer > 0f) orbitalCooldownTimer -= Time.deltaTime;
        if (auraKnockbackTimer > 0f) auraKnockbackTimer -= Time.deltaTime; //
        if (auraDebuffRefreshTimer > 0f) auraDebuffRefreshTimer -= Time.deltaTime;
        // --- ^^^ [新增] ^^^ ---

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
            HandleAuraKnockback();
            HandleAuraPersistentDebuffs();
            HandleAuraMagnet();
        }
    }

    private void SetupAura()
    {
        if (StatBlock == null) return;

        if (auraCollider == null)
        {
            Debug.LogError("Aura WeaponPart 预制件上缺少 SphereCollider!", this);
        }

        // 立即刷新状态来设置半径和VFX
        RefreshAura();

        auraTickTimer = 0; // 立即触发第一次伤害
        
    }


   

    public void RefreshAura()
    {
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.Aura) return; //

        // 1. 计算最终半径 (计入玩家和石头加成)
        float stoneScaleBonus = (currentStone != null) ? currentStone.scaleModifier : 0f;
        float finalRadius = StatBlock.baseAoeRadius * (PlayerStats.Instance.aoeRadiusMultiplier + stoneScaleBonus); //

        // 2. 更新碰撞器
        if (auraCollider != null) { auraCollider.radius = finalRadius; } //

        // 3. 确定使用哪个VFX Prefab和缩放乘数
        GameObject prefabToUse = StatBlock.auraVfxPrefab; //
        float scaleMultiplier = StatBlock.vfxBaseScaleMultiplier; //

        if (currentStone != null)
        {
            Debug.Log($"<color=cyan>[RefreshAura] 检查石头: {currentStone.stoneName}</color>"); //

            if (currentStone.auraVfxOverride != null) //
            {
                Debug.Log($"<color=green>[RefreshAura] 成功！找到覆盖VFX: {currentStone.auraVfxOverride.name}</color>"); //
                prefabToUse = currentStone.auraVfxOverride; //
                scaleMultiplier = currentStone.overrideVfxScaleMultiplier; //
            }
            else
            {
                // [!] 这就是 Bug 所在
                Debug.LogWarning($"<color=yellow>[RefreshAura] 失败! 石头 '{currentStone.stoneName}' 的 AuraVfxOverride 字段是 NULL (None)。</color>");
            }
        }
        else
        {
            Debug.Log("[RefreshAura] 检查: currentStone 为 null，使用默认VFX。");
        }

        // 4. 检查是否需要更换VFX
        // (我们使用 .name 比较，因为实例化的 '(Clone)' 后缀会被我们移除)
        bool isWrongVfx = (auraVfxInstance != null && (auraVfxInstance.name.StartsWith(prefabToUse.name) == false));

        if (auraVfxInstance == null || (isWrongVfx && prefabToUse != null))
        {
            if (auraVfxInstance != null) Destroy(auraVfxInstance); // 销毁旧的

            if (prefabToUse != null)
            {
                auraVfxInstance = Instantiate(prefabToUse, transform.position, Quaternion.identity, transform); //
                auraVfxInstance.name = prefabToUse.name; // 存储预制件名称以供比较
            }
        }

        // 5. 更新VFX缩放
        if (auraVfxInstance != null) //
        {
            auraVfxInstance.transform.localScale = Vector3.one * finalRadius * scaleMultiplier;
        }
    }
    private void HandleAuraDamageTick()
    {
        auraTickTimer -= Time.deltaTime;
        if (auraTickTimer <= 0f)
        {
            if (StatBlock == null) return; //

            // --- vvv [ 核心修复 3 ] vvv ---
            // (重新添加 finalDamage 和 chainedTargets 的声明)

            float stoneFireRateBonus = (currentStone != null) ? currentStone.fireRateModifier : 0f; //
            float finalTickInterval = StatBlock.baseAreaTickInterval * (1f - stoneFireRateBonus); //
            auraTickTimer = finalTickInterval;

            // [!] 'finalDamage' 的声明在这里
            float stoneDamageBonus = (currentStone != null) ? currentStone.damageModifier : 0f; //
            int finalDamage = Mathf.RoundToInt( //
                (StatBlock.baseAreaDamagePerTick * (PlayerStats.Instance.aoeDamageMultiplier + stoneDamageBonus)) + //
                PlayerStats.Instance.flatAoeDamageBonus //
            );

            // [!] 'chainedTargets' 的声明在这里
            List<Health> chainedTargets = null; //
            if (currentStone != null && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) //
            {
                chainedTargets = ApplyAuraChainDamage(finalDamage); //
            }
            // --- ^^^ [ 核心修复 3 ] ^^^ ---

            if (auraCollider == null) return; //
            if (enemyLayerMask == 0) return; //

            Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue; //

                Health target = hit.GetComponentInParent<Health>(); //
                if (target == null || target.IsDead) //
                {
                    continue;
                }

                // (使用 'finalDamage' 和 'chainedTargets')
                if (chainedTargets == null || !chainedTargets.Contains(target))
                {
                    // [修复 3] 传入 StatBlock.weaponName
                    target.TakeDamage(finalDamage, target.transform.position, this.gameObject, AttackType.Standard, null, null, StatBlock.weaponName);
                }

                if (currentStone != null)
                {
                    StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>();
                    if (receiver != null)
                    {
                        if (currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn) && Random.value <= currentStone.burnChance)
                        {
                            // [修复 4] 传入 StatBlock.weaponName 给 ApplyBurn
                            receiver.ApplyBurn(currentStone.burnDamage, currentStone.burnDuration, currentStone.burnTickInterval, StatBlock.weaponName);
                        }
                    }
                }
            }
        }
    }

    public void ChainLightningFromTarget(Transform startTarget, int maxChains, int damage, float range)
    {
        // 1. 基础安全检查 (移除对 currentStone 的检查！)
        if (startTarget == null || maxChains <= 0)
        {
            return;
        }

        // 2. 决定使用哪个特效
        // 优先使用石头特效；如果没有石头，使用 WeaponStatBlock 里的原生特效
        GameObject chainVfxToUse = null;
        GameObject impactVfxToUse = null;

        if (currentStone != null && currentStone.chainVfxPrefab != null)
        {
            // 有石头，用石头的
            chainVfxToUse = currentStone.chainVfxPrefab;
            impactVfxToUse = currentStone.chainImpactVfxPrefab;
        }
        else
        {
            // 没石头，用 StatBlock 原生的
            // 注意：StatBlock 可能会空，所以加个 ?. 检查
            chainVfxToUse = StatBlock?.nativeChainVfxPrefab;
            impactVfxToUse = StatBlock?.defaultImpactEffectPrefab; // 或者是 nativeChainImpact，暂时用通用的
        }

        // 3. 如果连原生的都没配，尝试用 WeaponPart 自身 Inspector 里的备份 (你现有的 lightningChainPrefab)
        if (chainVfxToUse == null)
        {
            chainVfxToUse = this.lightningChainPrefab;
        }
        if (impactVfxToUse == null) impactVfxToUse = StatBlock?.defaultImpactEffectPrefab;
        // 4. 启动协程
        StartCoroutine(ChainLightningRoutine(startTarget, maxChains, damage, range, chainVfxToUse, impactVfxToUse));
    }
    private IEnumerator ChainLightningRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange, GameObject chainVfx, GameObject impactVfx)
    {
        var hitEnemies = new List<Health>();
        Vector3 lastHitPosition = currentTarget.position; // [!] 从第一个目标开始

        while (currentTarget != null && remainingChains >= 0)
        {
            Vector3 currentTargetHitPoint = currentTarget.GetComponent<Health>()?.AimTargetPoint?.position ?? currentTarget.position; //
            Health targetHealth = currentTarget.GetComponent<Health>(); //

            if (targetHealth != null && !hitEnemies.Contains(targetHealth) && !targetHealth.IsDead) //
            {
                hitEnemies.Add(targetHealth);

                // (我们假设第一个目标已经在 Projectile.cs 中受到了伤害，
                //  这个协程只伤害 *后续* 目标)
                if (hitEnemies.Count > 1)
                {
                    targetHealth.TakeDamage(damage, currentTargetHitPoint, this.gameObject, AttackType.Standard); //
                }

                // 播放连锁VFX (从上一个点到这个点)
                if (chainVfx != null)
                {
                    var chainVFX_GO = Instantiate(chainVfx, Vector3.zero, Quaternion.identity); //
                    chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, currentTargetHitPoint); //
                }
                // 播放受击VFX
                if (impactVfx != null)
                {
                    Instantiate(impactVfx, currentTargetHitPoint, Quaternion.identity); //
                }
            }

            yield return new WaitForSeconds(0.05f); // 连锁之间的微小延迟

            // (查找下一个目标... 逻辑保持不变)
            Transform nextTarget = FindNextChainTarget(currentTargetHitPoint, chainRange, hitEnemies);
            remainingChains--;
            lastHitPosition = currentTargetHitPoint;
            currentTarget = nextTarget;
        }
    }
    private void HandleAuraPersistentDebuffs()
    {
        auraDebuffRefreshTimer -= Time.deltaTime;
        if (auraDebuffRefreshTimer > 0f) return;
        auraDebuffRefreshTimer = 0.25f; // 优化：每秒检查4次

        if (currentStone == null || auraCollider == null || enemyLayerMask == 0)
        {
            ClearAllAuraDebuffs(); // 如果没有石头或碰撞器，清除所有
            return;
        }

        bool stoneHasSlow = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow); //
        bool stoneHasWeaken = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyWeaken); //
        bool stoneHasCorrode = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode); //

        // 1. 获取半径内的所有敌人
        HashSet<StatusEffectReceiver> enemiesInRadius = new HashSet<StatusEffectReceiver>();
        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

        // --- vvv [ 核心修复 ] vvv ---
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue; //

            // 1. 先获取 Health 组件并检查 IsDead
            Health targetHealth = hit.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead) //
            {
                continue; // 跳过死亡或没有 Health 的物体
            }

            // 2. 只有在存活时，才获取 StatusEffectReceiver
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>(); //
            if (receiver != null)
            {
                enemiesInRadius.Add(receiver);
            }
        }
        // --- ^^^ [ 核心修复 ] ^^^ ---

        // --- 2. 处理减速 (Slow) ---
        ProcessDebuffList(enemiesInRadius, aura_ActiveSlows, stoneHasSlow,
            (receiver) => { receiver.ApplyPersistentSlow(this, currentStone.slowPercentage, currentStone.slowColor); }, //
            (receiver) => { receiver.RemovePersistentSlow(this); }
        );

        // --- 3. 处理弱化 (Weaken) ---
        ProcessDebuffList(enemiesInRadius, aura_ActiveWeaKens, stoneHasWeaken,
            (receiver) => { receiver.ApplyPersistentWeaken(this, currentStone.weakenPercentage); }, //
            (receiver) => { receiver.RemovePersistentWeaken(this); }
        );

        float finalCorrodeMultiplier = 1.0f;
        if (stoneHasCorrode && currentStone != null)
        {
            int corrodeStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode); //
            if (corrodeStoneCount >= 2) //
            {
                finalCorrodeMultiplier = currentStone.corrodeMultiplier_Stacked; // [!] 使用堆叠后的乘数
            }
            else
            {
                finalCorrodeMultiplier = currentStone.corrodeMultiplier; // [!] 使用基础乘数
            }
        }
        // 4. 处理腐蚀 (Corrode)
        ProcessDebuffList(enemiesInRadius, aura_ActiveCorrodes, stoneHasCorrode,
            (receiver) => { receiver.ApplyPersistentCorrode(this, finalCorrodeMultiplier, currentStone.corrodeColor); }, //
            (receiver) => { receiver.RemovePersistentCorrode(this); }
        );
    }

    private void HandleAuraMagnet()
    {
        auraMagnetTimer -= Time.deltaTime;
        if (auraMagnetTimer > 0f) return;
        auraMagnetTimer = 0.5f; // 优化：每秒检查2次

        if (currentStone == null || !currentStone.applyMagnet || auraCollider == null || pickupLayerMask == 0)
        {
            return;
        }

        // 1. 获取玩家 Transform (用于传递给掉落物)
        Transform playerTransform = GameManager.Instance?.playerTransform;
        if (playerTransform == null) return;

        // 2. 计算磁力半径
        // (光环的基础半径 + 能量石的额外百分比加成)
        float finalRadius = auraCollider.radius * (1f + currentStone.magnetRadiusBonusPercent);

        // 3. 扫描掉落物
        Collider[] hits = Physics.OverlapSphere(transform.position, finalRadius, pickupLayerMask);

        foreach (var hit in hits)
        {
            // 尝试获取经验球
            ExperienceGem gem = hit.GetComponent<ExperienceGem>(); //
            if (gem != null)
            {
                gem.TriggerMagnet(playerTransform);
                continue; // 下一个
            }

            // 尝试获取金币
            GoldPickup gold = hit.GetComponent<GoldPickup>(); //
            if (gold != null)
            {
                gold.TriggerMagnet(playerTransform);
            }
        }
    }

    /// <summary>
    /// (新增) 比较新旧列表并应用/移除debuff的通用帮助方法
    /// </summary>
    private void ProcessDebuffList(
        HashSet<StatusEffectReceiver> enemiesInRadius,
        HashSet<StatusEffectReceiver> activeList,
        bool stoneHasEffect,
        System.Action<StatusEffectReceiver> OnApply,
        System.Action<StatusEffectReceiver> OnRemove)
    {
        if (!stoneHasEffect)
        {
            // 如果石头没有这个效果，移除所有
            foreach (var receiver in activeList) { OnRemove(receiver); }
            activeList.Clear();
            return;
        }

        // 1. 应用新debuff (在范围内，但不在旧列表里)
        foreach (var receiver in enemiesInRadius)
        {
            if (activeList.Add(receiver)) // Add() 只有在元素 *不* 存在时才返回 true
            {
                OnApply(receiver);
            }
        }

        // 2. 移除旧debuff (在旧列表里，但不在范围内)
        activeList.RemoveWhere(receiver =>
        {
            if (receiver == null || !enemiesInRadius.Contains(receiver))
            {
                OnRemove(receiver);
                return true; // 从 activeList 中移除
            }
            return false;
        });
    }
    private void ClearAllAuraDebuffs()
    {
        foreach (var receiver in aura_ActiveSlows) { receiver?.RemovePersistentSlow(this); }
        aura_ActiveSlows.Clear();

        foreach (var receiver in aura_ActiveWeaKens) { receiver?.RemovePersistentWeaken(this); }
        aura_ActiveWeaKens.Clear();

        foreach (var receiver in aura_ActiveCorrodes) { receiver?.RemovePersistentCorrode(this); } //
        aura_ActiveCorrodes.Clear();
    }
    private List<Health> ApplyAuraChainDamage(int baseDamage)
    {
        // --- vvv [ 核心修复 ] vvv ---
        // 1. 不再读取列表，而是立即扫描
        if (currentStone == null || auraCollider == null) return null;
        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //
                                                                                                          // --- ^^^ [ 核心修复 ] ^^^ ---


        // 计算连锁伤害
        int chainDamage = Mathf.RoundToInt(baseDamage * currentStone.chainDamageMultiplier); //
        if (chainDamage <= 0) chainDamage = 1;

        List<Health> hitTargets = new List<Health>();

        // --- vvv [ 核心修复 ] vvv ---
        // 2. 从扫描结果 (hits) 中构建潜在目标列表
        List<Health> potentialTargets = new List<Health>();
        foreach (Collider hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && !target.IsDead) //
            {
                potentialTargets.Add(target);
            }
        }
        // --- ^^^ [ 核心修复 ] ^^^ ---

        if (potentialTargets.Count == 0) return null;

        // ... (确定 firstTarget 的逻辑保持不变) ...
        Health firstTarget = potentialTargets
            .OrderBy(t => (t.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();

        if (firstTarget == null) return null;
        potentialTargets.Remove(firstTarget);

        Health currentTarget = firstTarget;
        Vector3 lastVFXOriginPoint = transform.position;

        // (连锁循环逻辑保持不变)
        for (int i = 0; i <= currentStone.chainTargets; i++) //
        {
            if (currentTarget == null) break;

            Vector3 currentTargetHitPoint = currentTarget.AimTargetPoint != null ? currentTarget.AimTargetPoint.position : currentTarget.transform.position; //

            currentTarget.TakeDamage(chainDamage, currentTargetHitPoint, this.gameObject, AttackType.Standard); //
            hitTargets.Add(currentTarget);

            if (currentStone.chainImpactVfxPrefab != null) //
            {
                Instantiate(currentStone.chainImpactVfxPrefab, currentTargetHitPoint, Quaternion.identity); //
            }
            else if (StatBlock.defaultImpactEffectPrefab != null)
            {
                Instantiate(StatBlock.defaultImpactEffectPrefab, currentTargetHitPoint, Quaternion.identity); //
            }

            if (currentStone.chainVfxPrefab != null) //
            {
                Vector3 vfxOrigin = (i == 0) ? transform.position : lastVFXOriginPoint;
                var chainVFX_GO = Instantiate(currentStone.chainVfxPrefab, Vector3.zero, Quaternion.identity); //
                chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(vfxOrigin, currentTargetHitPoint); //
            }

            lastVFXOriginPoint = currentTargetHitPoint;

            Health nextTarget = null;
            float minSqrDist = currentStone.chainRange * currentStone.chainRange; //

            foreach (Health potential in potentialTargets)
            {
                if (potential == null || hitTargets.Contains(potential)) continue;
                float sqrDist = (potential.transform.position - lastVFXOriginPoint).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nextTarget = potential;
                }
            }

            if (nextTarget != null)
            {
                potentialTargets.Remove(nextTarget);
                currentTarget = nextTarget;
            }
            else
            {
                break;
            }
        }

        return hitTargets;
    }

    private void HandleAuraKnockback()
    {
        if (currentStone == null || !currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback) || auraKnockbackTimer > 0f) //
        {
            return;
        }

        // (检查通过，重置计时器)
        auraKnockbackTimer = currentStone.knockbackInterval; //

        // --- vvv [ 调试日志 ] vvv ---
        if (auraCollider == null) //
        {
            Debug.LogError("Knockback FAILED: auraCollider is NULL!");
            return;
        }
        if (enemyLayerMask == 0) //
        {
            Debug.LogWarning("Knockback Check: 'Enemy Layer Mask' (in Inspector) is not set!");
            return;
        }
        // --- ^^^ [ 调试日志 ] ^^^ ---

        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

        if (hits.Length == 0)
        {
            // 这是你之前看到的日志的新版本
            Debug.LogWarning("Knockback Fire: OverlapSphere found 0 targets. (Check LayerMask?)");
            return;
        }

        Debug.Log($"<color=green>Knockback Fire: OverlapSphere found {hits.Length} colliders. Checking them...</color>");
        int knockbackStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback); //

        float finalKnockbackForce;

        if (knockbackStoneCount >= 2) //
        {
            // 使用堆叠后的力度
            finalKnockbackForce = currentStone.knockbackForce_Stacked; //
        }
        else
        {
            // 使用基础力度
            finalKnockbackForce = currentStone.knockbackForce; //
        }


        foreach (Collider hit in hits)
        {
            // --- vvv [ 核心修复 ] vvv ---
            // 强制检查 Tag
            if (!hit.CompareTag("Enemy"))
            {
                Debug.Log($"Knockback Check: Ignored collider '{hit.name}' (Tag is not 'Enemy').");
                continue;
            }
            // --- ^^^ [ 核心修复 ] ^^^ ---

            Health target = hit.GetComponentInParent<Health>(); //
            if (target == null || target.IsDead) continue; //

            StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>(); //
            if (receiver != null)
            {
                Vector3 pushDir = (target.transform.position - transform.position).normalized; //
                pushDir.y = 0; //

                // [!] 使用 finalKnockbackForce
                receiver.ApplyKnockback(pushDir, finalKnockbackForce); //
            }
            else
            {
                Debug.LogWarning($"Knockback Check: Target {target.name} IS 'Enemy' but is missing StatusEffectReceiver!");
            }
        }
    }
    private void OnDestroy()
    {
        if (currentStone != null)
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.RegisterStone(null, currentStone);
            }
        }
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
        Debug.Log($"[WeaponPart] Fire 被调用. IsReady: {IsReadyToFire}");
        // Check generic cooldown first
        if (!IsReadyToFire) return;
        // Then check specific boomerang state
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Boomerang && isBoomerangOut) return;

        StartCoroutine(FireRoutine(initialDirection));
    }   
    

    private IEnumerator FireRoutine(Vector3 initialDirection)
    {
        float stoneDmgMod = (currentStone != null) ? currentStone.damageModifier : 0f;
        float stoneScaleMod = (currentStone != null) ? currentStone.scaleModifier : 0f;
        float stoneFireRateMod = (currentStone != null) ? currentStone.fireRateModifier : 0f;
        // --- 冷却和状态检查 (保持不变) ---
        if (StatBlock.behavior == WeaponBehaviorType.Boomerang)
        {
            if (isBoomerangOut) yield break;
            isBoomerangOut = true; // 发射时不冷却，只标记
        }
        else
        {
            // 冷却 = 基础 / (玩家乘数 * (1 + 能量石乘数))
            float finalFireRateMultiplier = PlayerStats.Instance.fireRateMultiplier * (1f + stoneFireRateMod); //
            // 防止除以零
            if (finalFireRateMultiplier <= 0) finalFireRateMultiplier = PlayerStats.Instance.fireRateMultiplier; //

            fireCooldown = (1f / StatBlock.baseFireRate) / finalFireRateMultiplier; //
        }
        // --- 冷却结束 ---

        if (cooldownMaterial != null) cooldownMaterial.StartCooldown(fireCooldown);

        if (floatingVisual != null) floatingVisual.HideWeapon();

       

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
            // 基础伤害 = (武器基础 * (玩家乘数 + 能量石乘数)) + 玩家固定值
            baseDamage = Mathf.RoundToInt(
                StatBlock.baseDirectDamage * (PlayerStats.Instance.damageMultiplier + stoneDmgMod) + //
                PlayerStats.Instance.flatDamageBonus //
            );

            // 基础体积 = 玩家乘数 + 能量石乘数 (注意：基础是1，不是0)
            baseScale = PlayerStats.Instance.aoeRadiusMultiplier + stoneScaleMod; //
        }

        // 2. 如果是回旋镖，计算【加法】叠加乘数
        float totalDamageMultiplier = 1f;
        float totalScaleMultiplier = 1f; // 这个乘数应用在 PlayerStats 的基础体积上
        if (StatBlock?.behavior == WeaponBehaviorType.Boomerang && PlayerStats.Instance != null && PlayerStats.Instance.boomerangMaxCatchStacks > 0) //
        {
            float totalDamageBonusPercent = PlayerStats.Instance.boomerangCatchStacks * PlayerStats.Instance.boomerangStackDamageBonusPercent; //
            float totalScaleBonusPercent = PlayerStats.Instance.boomerangCatchStacks * PlayerStats.Instance.boomerangStackScaleBonusPercent; //

            totalDamageMultiplier = 1f + totalDamageBonusPercent;
            totalScaleMultiplier = 1f + totalScaleBonusPercent;
        }
        // 3. 计算最终伤害和体积
        // 最终伤害 = 基础伤害 * 叠加乘数
        int finalDamage = Mathf.RoundToInt(baseDamage * totalDamageMultiplier);
        // 最终体积 = 玩家基础体积 * 叠加乘数
        float finalScale = baseScale * totalScaleMultiplier; // <-- 体积乘数应用在这里

        Debug.Log($"[FireRoutine] 准备发射. 武器名: {StatBlock?.weaponName}, 行为模式: {StatBlock?.behavior}");
        // --- Firing based on Behavior ---
        switch (StatBlock?.behavior)
        {
            case WeaponBehaviorType.Standard: //
            case WeaponBehaviorType.Pierce: //
                Debug.Log("[FireRoutine] 进入 Standard 分支");
                InstantiateAndFireProjectile(finalTargetDirection, finalDamage); break;

            case WeaponBehaviorType.MeleeAOE:
                // 必须调用 StartCoroutine 启动协程，才能实现“三连刺”
                StartCoroutine(MeleeAttackRoutine(finalDamage, finalScale));
                break;
            case WeaponBehaviorType.ParabolicAOE: //
                // (注意：AOE伤害也需要应用 stoneDmgMod)
                int finalAoeDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * (PlayerStats.Instance.aoeDamageMultiplier + stoneDmgMod) + PlayerStats.Instance.flatAoeDamageBonus); //
                InstantiateAndFireParabolicProjectile(finalTargetDirection, StatBlock, finalDamage, finalAoeDamage); break;
            case WeaponBehaviorType.Chain: //
                if (firstTarget != null) { StartCoroutine(ChainDamageRoutine(firstTarget, StatBlock.baseChainCount, finalDamage, StatBlock.chainRange)); } //
                break;
            case WeaponBehaviorType.SummonDrone: //
                InstantiateAndInitializeDrones(); if (StatBlock.isOneShot) { this.enabled = false; } //
                break;
            case WeaponBehaviorType.PersistentAOE: //
                Transform targetEnemy = FindNearestEnemyTransform();
                if (targetEnemy != null) { InstantiateAndFireAirdropDeployer(targetEnemy.position); } //
                break;
            case WeaponBehaviorType.Boomerang: //
                InstantiateAndFireBoomerang(finalTargetDirection, finalDamage, finalScale); break; //
            case null: Debug.LogError("StatBlock is null!"); break;
        }
        
        // --- Muzzle Flash & Delayed Sound ---
        if (StatBlock != null && StatBlock.muzzleFlashPrefab != null) { Instantiate(StatBlock.muzzleFlashPrefab, firePoint.position, firePoint.rotation); }

        if (floatingVisual != null)
        {
            yield return new WaitForSeconds(hideVisualDuration);
            floatingVisual.ShowWeapon();
        }
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
        if (firePoint == null)
        {
            Debug.LogError($"[发射失败] {gameObject.name} 的 WeaponPart 缺少 FirePoint！请在 Inspector 里赋值。");
            return;
        }
        if (StatBlock?.projectilePrefab == null)
        {
            Debug.LogError($"[发射失败] {StatBlock?.weaponName} 的数据里没拖 ProjectilePrefab！");
            return;
        }
        GameObject bullet = Instantiate(StatBlock.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            float finalSpeed = StatBlock.baseLaunchForce * PlayerStats.Instance.projectileSpeedMultiplier;
            int stonePierceBonus = (currentStone != null) ? (int)currentStone.pierceModifier : 0;
            int finalPierceCount = StatBlock.basePierceCount + PlayerStats.Instance.bonusPierceCount + stonePierceBonus; //
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
              AttackType.Standard,
              this // <--- [!] 传递 launcher 引用
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
                finalStunChance, finalStunDuration, //
                 this
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
            GameObject orbiterGO = Instantiate(StatBlock.orbitalPrefab, orbitalPivot);
            orbiterGO.transform.localPosition = spawnPos; // Position relative to pivot
            orbiterGO.transform.localRotation = Quaternion.Euler(0, angle, 0);
            orbiterGO.GetComponent<Orbiter>()?.Initialize(finalDamage, this);
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
            activeBeamInstance.Initialize(StatBlock, this, lockedBeamTarget); //
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
                    finalDamage,
                    finalRadius,
                    StatBlock.armingTime,
                    StatBlock.mineDuration,
                    WeaponController.Instance.gameObject,
                    StatBlock.explosionEffectPrefab,
                    StatBlock.layersToDamageByAOE,
                    this // <--- [新增] 传递 WeaponPart 自身
                );
            }
            else { Debug.LogWarning($"Mine prefab '{StatBlock.minePrefab.name}' is missing Landmine script.", mineGO); }

            if (landminePlaceSound != null && audioSource != null)
            { AudioSource.PlayClipAtPoint(landminePlaceSound, spawnPosition); } // Play sound at location
        }

        // Reset cooldown
        fireCooldown = (1f / StatBlock.baseFireRate) * PlayerStats.Instance.fireRateMultiplier;
    }
    public void FuseEnergyStone(EnergyStoneSO newStone) 
    {
        if (newStone == null)
        {
            Debug.LogError("[FuseEnergyStone] 失败! 传入的 newStone 是 null!");
            return;
        }

        Debug.Log($"<color=lime>[FuseEnergyStone] 1. 开始融合: {newStone.stoneName}</color>"); //

        EnergyStoneSO oldStone = this.currentStone; // (获取旧石头，用于 PlayerStats)

        // 2. 覆盖旧的能量石
        this.currentStone = newStone; //

        // 3. 立即检查
        if (this.currentStone != null)
        {
            Debug.Log($"<color=lime>[FuseEnergyStone] 2. 变量已设置! this.currentStone 现在是: {this.currentStone.stoneName}</color>");
        }
        else
        {
            // 如果这个日志出现，就意味着发生了严重的内部错误
            Debug.LogError("[FuseEnergyStone] 2. 严重错误! 刚刚设置了 currentStone，但它仍然是 null!");
        }

        // 4. (这是我们为“冰冻计数器” 添加的逻辑，请确保它在你的脚本里)
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.RegisterStone(newStone, oldStone); //
        }
        // --- ^^^ [ 核心调试 ] ^^^ ---


        // 5. 融合后，立即刷新武器状态
        RefreshWeaponStateFromStone(); //
    }

    public void RemoveEnergyStone() 
    {
        EnergyStoneSO oldStone = this.currentStone; // 1. 暂存旧石头

        this.currentStone = null; // 2. 移除石头

        // 3. 向全局计数器报告
        if (oldStone != null && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.RegisterStone(null, oldStone);
        }        

        RefreshWeaponStateFromStone();
    }

    public void RefreshWeaponStateFromStone()
    {
        if (StatBlock.behavior == WeaponBehaviorType.Aura) RefreshAura();
        if (StatBlock.behavior == WeaponBehaviorType.Orbital) RefreshOrbiters();

        // 进化/融合后，刷新模型
        UpdateVisualModel();
    }
    private void UpdateVisualModel()
    {
        if (floatingVisual == null || StatBlock == null) return;

        // 1. 换装
        if (StatBlock.floatingModelPrefab != null)
        {
            GameObject newModelInstance = floatingVisual.SwapModel(StatBlock.floatingModelPrefab);

            // 2. 获取新模型上的材质脚本
            if (newModelInstance != null)
            {
                cooldownMaterial = newModelInstance.GetComponent<WeaponCooldownMaterial>();
                if (cooldownMaterial == null)
                    cooldownMaterial = newModelInstance.GetComponentInChildren<WeaponCooldownMaterial>();
            }
        }

        // --- 【核心修复】同步引用给 PlayerBladeAttack ---
        // 因为未进化时，是 PlayerBladeAttack 在控制攻击和冷却表现
        // 所以必须把新生成的 visual 和 material 告诉它

        var meleeAttackScript = GetComponent<PlayerBladeAttack>();
        if (meleeAttackScript != null)
        {
            // 1. 同步材质引用 (修复发光失效)
            meleeAttackScript.weaponCooldownMaterial = this.cooldownMaterial;

            // 2. 同步控制器引用 (确保能控制显隐)
            meleeAttackScript.floatingWeapon = this.floatingVisual;

            // 调试日志
            // Debug.Log($"[WeaponPart] 已将视觉组件同步给 PlayerBladeAttack. Material: {cooldownMaterial != null}");
        }
        // ---------------------------------------------
    }

    private IEnumerator MeleeAttackRoutine(int damage, float scale)
    {
        int count = StatBlock.multiHitCount;
        if (count < 1) count = 1;

        for (int i = 0; i < count; i++)
        {
            // 1. 处理锁敌转向
            if (StatBlock.autoAimMelee)
            {
                RotateTowardsNearestEnemy();
            }

            // 2. 生成特效 (伤害由特效上的 VFXDamageController 负责)
            SpawnMeleeVFX(damage, scale);

            // 3. 播放音效 (如果有多段攻击，每次都播)
            PlayFireSound();

            // 4. 如果是多段攻击，等待间隔
            if (count > 1)
            {
                yield return new WaitForSeconds(StatBlock.multiHitInterval);
            }
        }
    }
    private void SpawnMeleeVFX(int damage, float scale)
    {
        if (StatBlock.slashEffectPrefab == null) return;

        // 确定生成位置和旋转
        Vector3 spawnPos = firePoint.position;
        Quaternion spawnRot = transform.rotation; // 跟随当前朝向

        // 实例化特效
        GameObject vfxObj = Instantiate(StatBlock.slashEffectPrefab, spawnPos, spawnRot);

        // 应用体积缩放 (雷光刺变长/宽，爆炎斩变大)
        vfxObj.transform.localScale = Vector3.one * (StatBlock.baseAoeRadius * scale);
        // 注意：这里我们利用 baseAoeRadius 作为特效的基础缩放基准

        // 初始化伤害控制器
        VFXDamageController vfxCtrl = vfxObj.GetComponent<VFXDamageController>();
        if (vfxCtrl != null)
        {
            vfxCtrl.Initialize(damage, StatBlock.hitEffectPrefab, this.gameObject, this);
        }
    }
    private void RotateTowardsNearestEnemy()
    {
        // 使用 StatBlock.autoAimRange 或默认一个范围
        float range = StatBlock.autoAimRange > 0 ? StatBlock.autoAimRange : 10f;
        Transform target = FindNearestEnemyTransform(transform.position, range);

        if (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            dir.y = 0; // 保持水平
            if (dir != Vector3.zero)
            {
                // 瞬间转身，保证刺击准确
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    private IEnumerator PerformMultiThrustRoutine(int damage, float scale)
    {
        // 1. 锁定目标方向
        if (StatBlock.autoAimMelee)
        {
            RotateTowardsNearestEnemy();
        }

        // 2. 循环执行刺击
        for (int i = 0; i < StatBlock.multiHitCount; i++)
        {
            // 每次刺击都微调方向 (防止敌人跑太快打空)
            if (StatBlock.autoAimMelee) RotateTowardsNearestEnemy();

            // 播放音效
            PlayFireSound();

            // 生成刺击特效 (雷光特效)
            if (StatBlock.slashEffectPrefab != null)
            {
                // 稍微向前偏移一点，让特效看起来是从武器尖端发出的
                Vector3 spawnPos = firePoint.position + transform.forward * 0.5f;
                GameObject vfx = Instantiate(StatBlock.slashEffectPrefab, spawnPos, transform.rotation);
                vfx.transform.localScale = Vector3.one * scale; // 刺击特效通常比较细长
            }

            // 执行判定 (这里用 BoxCast 模拟刺击的长条形判定)
            PerformThrustHitCheck(damage, scale);

            // 等待间隔
            yield return new WaitForSeconds(StatBlock.multiHitInterval);
        }
    }

    private void PerformThrustHitCheck(int damage, float scale)
    {
        // 刺击参数：长方形判定
        // 宽度由 scale 决定，长度由 baseAoeRadius 决定
        float thrustWidth = 1.5f * scale;
        float thrustDistance = StatBlock.baseAoeRadius * scale;

        Vector3 center = firePoint.position;
        Vector3 halfExtents = new Vector3(thrustWidth / 2, 1f, thrustWidth / 2); // 高度给1f防止漏怪
        Quaternion orientation = transform.rotation;

        // 使用 BoxCastAll 穿透所有敌人
        RaycastHit[] hits = Physics.BoxCastAll(center, halfExtents, transform.forward, orientation, thrustDistance, StatBlock.layersToDamageByAOE);

        foreach (var hit in hits)
        {
            if (!hit.collider.CompareTag("Enemy")) continue;

            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null)
            {
                // 1. 造成伤害
                h.TakeDamage(damage, hit.point, gameObject, AttackType.Standard, null, null, StatBlock.weaponName);

                // 2. 触发闪电链 (如果是雷光刺)
                // 这里的 currentStone 已经在 WeaponPart 中，我们直接检查属性
                if (currentStone != null && (currentStone.applyChain || currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)))
                {
                    // 调用你现有的闪电链逻辑
                    ChainLightningFromTarget(h.transform, StatBlock.baseChainCount, Mathf.RoundToInt(damage * 0.5f), StatBlock.chainRange);
                }

                // 3. 产生命中特效
                if (StatBlock.hitEffectPrefab != null)
                {
                    Instantiate(StatBlock.hitEffectPrefab, hit.point, Quaternion.identity);
                }
            }
        }
    }

    private void PerformMeleeAttack(Vector3 dir, int damage, float scale)
    {
        // 1. 播放特效
        if (StatBlock.slashEffectPrefab != null)
        {
            GameObject vfx = Instantiate(StatBlock.slashEffectPrefab, firePoint.position, Quaternion.LookRotation(dir));
            vfx.transform.localScale = Vector3.one * (StatBlock.baseAoeRadius * scale);
        }       

        // 2. 范围检测
        float range = StatBlock.baseAoeRadius * scale;
        Collider[] hits = Physics.OverlapSphere(firePoint.position, range, StatBlock.layersToDamageByAOE);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            // 角度检测 (简单版)
            Vector3 toTarget = (hit.transform.position - firePoint.position).normalized;
            // Vector3.Angle 返回 0-180。如果 attackAngle 是 180，则只要在前方 90 度内都算命中。
            if (Vector3.Angle(dir, toTarget) < StatBlock.attackAngle / 2)
            {
                Health h = hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(damage, hit.transform.position, gameObject, AttackType.Standard, null, null, StatBlock.weaponName);

                    // 爆炎斩：由于是 MeleeAOE，可以在这里额外施加 StatusEffectReceiver 的 Burn
                    // 这一步其实已经在 WeaponStatBlock 的 currentStone 逻辑里处理了 (如果你的 WeaponPart Update 里有通用命中处理)
                    // 但 Melee 是瞬间判定，所以需要在这里手动补一下石头效果：
                    if (currentStone != null)
                    {
                        StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                        if (receiver != null && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn))
                        {
                            receiver.ApplyBurn(currentStone.burnDamage, currentStone.burnDuration, currentStone.burnTickInterval, StatBlock.weaponName);
                        }
                    }
                }
            }
        }
    }
    #endregion

}

