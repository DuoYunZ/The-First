using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("归属设置")]
    [Tooltip("勾选此项，如果这个Health组件属于玩家")]
    public bool isPlayerHealth = false;

    [Header("生命值设置")]
    [SerializeField] public int maxHealth = 100; // 在 Inspector 中为预制件设置一个默认最大生命值
    public int currentHealth;

    private int _baseMaxHealth = 0;

    public EnemyType EnemyTypeData { get; private set; }

    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<int, int> { }
    [Header("事件")]
    public HealthChangedEvent OnHealthChanged;

    [Header("死亡事件")]
    [Tooltip("当生命值归零时触发的事件")]
    public UnityEvent OnDeath;
    public bool IsDead { get; private set; }

    // 全局静态事件：当敌人死亡时触发，供光环等技能监听
    public static event System.Action<Health> OnEnemyDied;
    public static event System.Action<int> OnPlayerHealthDamaged;
    [Header("受伤无敌与视觉 (新增)")]
    [Tooltip("玩家受伤后的无敌时间 (秒)")]
    public float invincibilityDuration = 1.0f;
    [Tooltip("受击闪烁的持续时间 (秒) - 建议很短，如 0.15")]
    public float flashDuration = 0.15f;
    [Tooltip("受伤时 Emission 的颜色 (HDR)")]
    [ColorUsage(true, true)] // 允许在Inspector里调节HDR亮度
    public Color damageEmissionColor = new Color(1f, 0f, 0f, 1f) * 3f; // 默认红光，强度3

    [Header("受击反馈")]
    [Tooltip("玩家受击时的击退力度（0 = 不击退）")]
    public float knockbackForce = 8f;
    [Tooltip("受击震屏强度（0 = 不震屏）")]
    public float hitShakeIntensity = 3f;
    [Tooltip("受击震屏持续时间（秒）")]
    public float hitShakeDuration = 0.15f;
    [Tooltip("玩家受击时播放的音效（随机选一个）")]
    public AudioClip[] playerHitSounds;
    [Tooltip("受击音效音量")]
    [Range(0f, 1f)]
    public float playerHitSoundVolume = 0.8f;

    private bool isPostHitInvincible = false; // 是否处于受击后的短暂无敌状态
    private Renderer[] modelRenderers; // 角色模型渲染器数组
    private Color[] originalEmissionColors; // 每个 Renderer 的原始发光颜色
    private MaterialPropertyBlock materialPropertyBlock;
    private Rigidbody playerRigidbody; // 缓存玩家刚体引用

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");


    [Header("掉落设置 (可选)")]
    [Tooltip("死亡时掉落的经验宝石预设")]
    public GameObject experienceGemPrefab;
    [Tooltip("死亡时掉落的金币预设")] // <--- 新增
    public GameObject goldCoinPrefab;
    [Tooltip("掉落金币的几率 (0到1之间)")] // <--- 新增
    [Range(0f, 1f)]
    public float goldDropChance = 0.5f; // 默认50%几率

    [Tooltip("死亡时掉落的血包预设")]
    public GameObject healthPickupPrefab; // <--- 新增
    [Tooltip("掉落血包的几率 (0到1)")]
    [Range(0f, 1f)]
    public float healthDropChance = 0.005f; // <--- 新增 (默认10%)

    [Tooltip("死亡时掉落的宝箱预设")]
    public GameObject treasureChestPrefab;

    /// <summary>
    /// 标记该敌人是否为精英怪（由 EnemySpawner 在生成时设置）
    /// </summary>
    [HideInInspector]
    public bool isElite = false;

    [Header("视觉效果 (可选)")]
    [Tooltip("受到伤害时生成的跳字预制件")]
    public GameObject damagePopupPrefab;

    [Header("音效设置 (可选)")]
    [Tooltip("受到伤害时播放的音效")]
    public AudioClip[] impactSounds;
    private AudioSource audioSource;

    private StatusEffectReceiver statusReceiver; // <--- vvv [新增] vvv

    [Header("视觉和受击点")]
    [Tooltip("子弹和特效命中的视觉目标点")]
    public Transform AimTargetPoint; // <--- vvv 新增


    /// <summary>
    /// Awake 在对象实例化后立即被调用。
    /// 这将为所有使用此脚本的对象（包括玩家和敌人）提供一个初始的满血状态。
    /// </summary>
    void Awake()
    {
        _baseMaxHealth = maxHealth; // 记录初始值
        currentHealth = maxHealth;

        // 缓存刚体引用（用于击退）
        if (isPlayerHealth)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        audioSource = GetComponent<AudioSource>();
        statusReceiver = GetComponent<StatusEffectReceiver>();

        // 获取角色模型的渲染器（优先查找 Visuals 子对象下的 SkinnedMeshRenderer/MeshRenderer）
        Transform visualsRoot = transform.Find("Visuals");
        if (visualsRoot != null)
        {
            // 只获取角色模型的渲染器，排除粒子特效等
            modelRenderers = visualsRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (modelRenderers.Length == 0)
            {
                modelRenderers = visualsRoot.GetComponentsInChildren<MeshRenderer>();
            }
        }
        else
        {
            // 没有 Visuals 层级，回退为获取第一个 Renderer
            var r = GetComponentInChildren<Renderer>();
            modelRenderers = r != null ? new Renderer[] { r } : new Renderer[0];
        }

        // 初始化发光颜色备份
        materialPropertyBlock = new MaterialPropertyBlock();
        originalEmissionColors = new Color[modelRenderers.Length];
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            var mat = modelRenderers[i] != null ? modelRenderers[i].sharedMaterial : null;
            originalEmissionColors[i] = mat != null && mat.HasProperty(EmissionColorProperty)
                ? mat.GetColor(EmissionColorProperty)
                : Color.black;
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 设为3D音效
        }
        if (AimTargetPoint == null)
        {
            AimTargetPoint = transform;
        }
    }

    void Start()
    {
        
        if (isPlayerHealth && PlayerStats.Instance != null)
        {
            
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // 【图鉴成就】半血存活秒数累计计时器
    private float halfHpSurviveAccumulator = 0f;

    void Update()
    {
        // 仅玩家血量组件执行半血存活时间追踪
        if (!isPlayerHealth || IsDead) return;

        // 判断是否低于50%血量
        if (currentHealth > 0 && currentHealth <= maxHealth / 2)
        {
            halfHpSurviveAccumulator += Time.deltaTime;

            // 每累计1秒记录一次，减少调用频率
            if (halfHpSurviveAccumulator >= 1f)
            {
                int seconds = Mathf.FloorToInt(halfHpSurviveAccumulator);
                halfHpSurviveAccumulator -= seconds;

                if (PlayerProgressManager.Instance != null)
                {
                    PlayerProgressManager.Instance.AddStat("HalfHP_Survive_Seconds", seconds);
                }
            }
        }
        else
        {
            // 血量高于50%时重置累计器（不影响已记录的总秒数）
            halfHpSurviveAccumulator = 0f;
        }
    }
    /// <summary>
    /// 提供一个公共方法来从外部初始化或重置生命值。
    /// EnemySpawner 将为每个生成的敌人调用此方法，用计算出的新值覆盖 Awake 中设置的初始值。
    /// </summary>
    /// <param name="initialMaxHealth">根据波次计算出的最大生命值</param>
    /// 
    public void SetBonusMaxHealth(int bonus)
    {
        if (_baseMaxHealth == 0) _baseMaxHealth = maxHealth; // 保险

        int oldMax = maxHealth;
        maxHealth = _baseMaxHealth + bonus;

        // 如果上限增加了，当前血量也按比例增加，或者直接增加差值
        if (maxHealth > oldMax)
        {
            int diff = maxHealth - oldMax;
            currentHealth += diff;
        }
        // 如果上限减少了（例如卸下道具），裁剪当前血量
        else if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);       
    }
    public void InitializeHealth(int initialMaxHealth, EnemyType typeData, bool elite = false)
    {
        maxHealth = initialMaxHealth;
        currentHealth = maxHealth;
        this.EnemyTypeData = typeData;
        this.isElite = elite;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    public void InitializeHealth(int initialMaxHealth)
    {
        InitializeHealth(initialMaxHealth, null);
    }
    public void AddMaxHealth(int amountToAdd)
    {
        // 旧方法保留，但建议主要使用 SetBonusMaxHealth
        if (amountToAdd <= 0) return;
        maxHealth += amountToAdd;
        _baseMaxHealth += amountToAdd; // 视为永久增加基础值
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool TakeDamage(int damageAmount, Vector3 hitPosition, GameObject attacker = null, AttackType type = AttackType.Standard, Projectile projectile = null, EnemyBeamController beamController = null, string sourceWeaponName = "", bool isCritical = false)
    {
        if (isCritical)
        {
            // Debug.Log($"<color=orange>[Health] 🩸 收到 TakeDamage! 伤害: {damageAmount}, isCritical: {isCritical}</color>");
        }
        if (IsDead) return false;

        // 1. 无敌判断
        if (isPlayerHealth && (isPostHitInvincible || (PlayerStats.Instance != null && PlayerStats.Instance.isInvincible)))
        {
            return false;
        }

        // 2. 状态效果计算 (腐蚀、脆弱印记等)
        if (statusReceiver != null)
        {
            if (statusReceiver.IsCorroded)
                damageAmount = Mathf.RoundToInt(damageAmount * statusReceiver.corrodeDamageMultiplier);
            if (statusReceiver.IsFragile)
                damageAmount = Mathf.RoundToInt(damageAmount * statusReceiver.fragileDamageMultiplier);
        }

        // 3. 玩家护甲计算
        if (isPlayerHealth && PlayerStats.Instance != null)
        {
            float armorValue = PlayerStats.Instance.armor;
            if (armorValue > 0)
            {
                damageAmount -= Mathf.RoundToInt(armorValue);
                if (damageAmount < 1) damageAmount = 1;
            }

            // 3.5 剑圣之道：精准斩击停顿期间减伤
            PlayerBladeAttack blade = FindFirstObjectByType<PlayerBladeAttack>();
            if (blade != null)
            {
                float reduction = blade.GetKenseiDamageReduction();
                if (reduction > 0f)
                {
                    damageAmount = Mathf.RoundToInt(damageAmount * (1f - reduction));
                    if (damageAmount < 1) damageAmount = 1;
                }
            }
        }

        int remainingDamage = damageAmount;
        bool wasReflected = false;

        // 4. 护盾计算
        if (isPlayerHealth && PlayerShield.Instance != null && PlayerShield.Instance.GetCurrentShield() > 0)
        {
            remainingDamage = PlayerShield.Instance.AbsorbDamage(damageAmount, hitPosition, type, projectile, beamController, out wasReflected);
        }

        // 5. 实际扣血与经验处理
        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage;
            if (isPlayerHealth)
            {
                OnPlayerHealthDamaged?.Invoke(remainingDamage);
            }

            // --- 【核心新增】尝试获取攻击源头的 WeaponPart ---
            WeaponPart sourcePart = null;

            // A. 尝试从参数 projectile 获取
            if (projectile != null)
                sourcePart = projectile.sourceWeapon;

            // B. 尝试从 attacker 身上获取 Projectile 组件 (如果是碰撞体触发)
            if (sourcePart == null && attacker != null)
            {
                Projectile p = attacker.GetComponent<Projectile>();
                if (p != null) sourcePart = p.sourceWeapon;
            }

            // C. 尝试从 attacker 身上获取 VFXDamageController (如果是近战特效)
            if (sourcePart == null && attacker != null)
            {
                VFXDamageController vfx = attacker.GetComponent<VFXDamageController>();
                if (vfx != null) sourcePart = vfx.sourceWeapon;
            }

            // D. 【新增】尝试从 attacker 身上获取 FlyingDaggerController (如果是飞刀)
            if (sourcePart == null && attacker != null)
            {
                FlyingDaggerController dagger = attacker.GetComponent<FlyingDaggerController>();
                if (dagger != null) sourcePart = dagger.sourceWeapon;
            }
            
            // E. 【新增】尝试从 attacker 身上获取 Landmine (如果是地雷)
            if (sourcePart == null && attacker != null)
            {
                Landmine landmine = attacker.GetComponent<Landmine>();
                if (landmine != null) sourcePart = landmine.sourceWeapon;
            }

            // G. 【新增】尝试从 attacker 身上获取 Orbiter / MagneticOrbiter (环绕武器)
            if (sourcePart == null && attacker != null)
            {
                Orbiter orbiter = attacker.GetComponent<Orbiter>();
                if (orbiter != null) sourcePart = orbiter.launcher;
            }
            if (sourcePart == null && attacker != null)
            {
                MagneticOrbiter magOrbiter = attacker.GetComponent<MagneticOrbiter>();
                if (magOrbiter != null) sourcePart = magOrbiter.launcher;
            }
            
            // F. 【新增】尝试从 attacker 身上直接获取 WeaponPart (如果是Aura类型武器)
            if (sourcePart == null && attacker != null)
            {
                WeaponPart weaponPart = attacker.GetComponent<WeaponPart>();
                if (weaponPart != null) sourcePart = weaponPart;
            }
            // ----------------------------------------------------
            
            // 【修补】大招特殊处理：屏蔽大招的能量获取
            bool isUltimateHit = false;
            // 优先从传入的 projectile 参数检查（因为 attacker 可能是 WeaponPart 而非 Projectile）
            if (projectile != null && projectile.isUltimate)
            {
                isUltimateHit = true;
            }
            // 检查龙卷风是否来自融合大招（火焰风暴等），阻止能量增加
            if (!isUltimateHit && attacker != null)
            {
                TornadoController tornado = attacker.GetComponent<TornadoController>();
                if (tornado != null && tornado.isComboUltimate)
                    isUltimateHit = true;
            }



            // --- 造成伤害获得经验/能量 ---
            if (!isUltimateHit && sourcePart != null && sourcePart.StatBlock != null &&
                sourcePart.StatBlock.xpSource == WeaponXpSource.DamageDealt)
            {
                float xp = remainingDamage * sourcePart.StatBlock.xpGainFactor;               
                sourcePart.GainProficiencyXP(xp);
                
                // 触发XP粒子飞向技能图标（仅在大招已解锁时显示能量吸收特效）
                if (WeaponUI.Instance != null && sourcePart.isUltimateUnlocked)
                {
                    WeaponUI.Instance.SpawnXpParticlesToWeapon(sourcePart, hitPosition);
                }
            }

            // ------------------------------------

            if (!string.IsNullOrEmpty(sourceWeaponName) && !isPlayerHealth && BattleStatisticsManager.Instance != null)
                BattleStatisticsManager.Instance.AddDamage(sourceWeaponName, remainingDamage);

            // 【图鉴成就】追踪雷系武器造成的伤害 (雷鸣意志解锁条件)
            if (!isPlayerHealth && PlayerProgressManager.Instance != null)
            {
                bool isLightning = false;
                // 通过武器ID或武器名称判断是否为雷系
                if (sourcePart != null && sourcePart.StatBlock != null)
                {
                    string wid = sourcePart.StatBlock.weaponID ?? "";
                    string wname = sourcePart.StatBlock.weaponName ?? "";
                    if (wid.Contains("ChainLightning") || wid.Contains("LightningStrike") ||
                        wid.Contains("MagneticStorm") || wid.Contains("Thunder") ||
                        wname.Contains("闪电") || wname.Contains("雷") || wname.Contains("电弧"))
                    {
                        isLightning = true;
                    }
                }
                // 也检查 sourceWeaponName 参数（某些伤害来源不走 sourcePart）
                if (!isLightning && !string.IsNullOrEmpty(sourceWeaponName))
                {
                    if (sourceWeaponName.Contains("闪电") || sourceWeaponName.Contains("雷") ||
                        sourceWeaponName.Contains("Lightning") || sourceWeaponName.Contains("Thunder") ||
                        sourceWeaponName.Contains("电弧"))
                    {
                        isLightning = true;
                    }
                }
                if (isLightning)
                {
                    PlayerProgressManager.Instance.AddStat("Lightning_Damage", remainingDamage);
                }
            }

            // --- 吸血处理：玩家对敌人造成伤害时累积，满阈值回血 ---
            if (!isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.lifeStealPercent > 0f)
            {
                // 将本次伤害按吸血比例累积到全局计数器
                PassiveEffectManager.lifeStealDamageAccumulator += remainingDamage * PlayerStats.Instance.lifeStealPercent;

                // 每累积 1000 点等效伤害恢复 1 点 HP
                const int LIFESTEAL_THRESHOLD = 1000;
                if (PassiveEffectManager.lifeStealDamageAccumulator >= LIFESTEAL_THRESHOLD)
                {
                    int healTimes = Mathf.FloorToInt(PassiveEffectManager.lifeStealDamageAccumulator / LIFESTEAL_THRESHOLD);
                    PassiveEffectManager.lifeStealDamageAccumulator -= healTimes * LIFESTEAL_THRESHOLD;

                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        Health playerHealth = player.GetComponent<Health>();
                        if (playerHealth != null && !playerHealth.IsDead)
                        {
                            playerHealth.Heal(healTimes);
                        }
                    }
                }
            }

            // 【提前播放】玩家受击音效 — 确保第一时间有声音反馈
            if (isPlayerHealth && !IsDead && playerHitSounds != null && playerHitSounds.Length > 0)
            {
                AudioClip clip = playerHitSounds[Random.Range(0, playerHitSounds.Length)];
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySoundEffect(clip, playerHitSoundVolume);
                }
                else if (audioSource != null)
                {
                    audioSource.PlayOneShot(clip, playerHitSoundVolume);
                }
            }

            // 跳字逻辑
            if (damagePopupPrefab != null)
            {
                // 根据怪物实际高度计算跳字位置（避免被大体积怪物遮挡）
                float topY = GetEntityTopY();
                float randomX = Random.Range(-0.5f, 0.5f);
                float randomY = Random.Range(0.2f, 0.7f);
                Vector3 popupPos = new Vector3(transform.position.x + randomX, topY + randomY, transform.position.z);
                GameObject popupGO = Instantiate(damagePopupPrefab, popupPos, Quaternion.identity);
                DamagePopup damagePopup = popupGO.GetComponent<DamagePopup>();
                if (damagePopup != null) damagePopup.InitPopup(remainingDamage, isCritical);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // --- 【被动道具】玩家血量变化时通知 PlayerStats 重算条件型被动 ---
            if (isPlayerHealth && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnPlayerHealthChanged();
            }

            // --- 【被动道具】冰霜之触：全局冰冻概率（仅对敌人生效）---
            if (!isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.globalFreezeChance > 0f)
            {
                if (Random.value < PlayerStats.Instance.globalFreezeChance)
                {
                    StatusEffectReceiver freezeReceiver = GetComponent<StatusEffectReceiver>();
                    if (freezeReceiver != null && !freezeReceiver.IsFrozen)
                    {
                        freezeReceiver.ApplyFreeze(1.5f, null, PlayerStats.Instance.frozenMaxHealthDamageCapstone); // 冰冻1.5秒
                    }
                }
            }

            // --- 【被动道具】奥术精通：玩家伤害命中时概率引发小范围爆炸 ---
            if (!isPlayerHealth && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.TryTriggerArcaneMastery(
                    transform.position,
                    attacker != null ? attacker : gameObject,
                    sourceWeaponName);
                PlayerStats.Instance.TryTriggerElementalResonance(
                    transform.position,
                    attacker != null ? attacker : gameObject,
                    sourceWeaponName);
                if (isCritical)
                {
                    PlayerStats.Instance.TryTriggerThunderCritChain(
                        this,
                        transform.position,
                        attacker != null ? attacker : gameObject,
                        sourceWeaponName);
                }
            }

            // 受击音效：优先使用攻击来源武器SO的hitSound，回退到Health自身的impactSounds
            AudioClip hitClip = null;
            if (sourcePart != null && sourcePart.StatBlock != null && sourcePart.StatBlock.hitSound != null)
            {
                hitClip = sourcePart.StatBlock.hitSound;
            }

            if (hitClip != null)
            {
                // 使用武器SO配置的命中音效
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySoundEffect(hitClip, sourcePart.StatBlock.fireSoundVolume);
                else if (audioSource != null)
                    audioSource.PlayOneShot(hitClip);
            }
            else if (impactSounds != null && impactSounds.Length > 0)
            {
                // 回退到旧的受击音效
                audioSource.PlayOneShot(impactSounds[Random.Range(0, impactSounds.Length)]);
            }

            if (isPlayerHealth && !IsDead)
            {
                StartCoroutine(InvincibilitySequence());

                // --- 受击反馈：击退 ---
                if (knockbackForce > 0f && attacker != null)
                {
                    MechController mechCtrl = GetComponent<MechController>();
                    if (mechCtrl != null)
                    {
                        Vector3 knockbackDir = transform.position - attacker.transform.position;
                        mechCtrl.ApplyKnockback(knockbackDir, knockbackForce);
                    }
                }

                // --- 受击反馈：震屏 ---
                if (hitShakeIntensity > 0f && CameraShakeManager.Instance != null)
                {
                    CameraShakeManager.Instance.Shake(hitShakeIntensity, hitShakeDuration);
                }

                // 荆棘反伤暂时禁用：当前 demo 的受击反弹不匹配核心清怪玩法。
            }

            // 6. 死亡处理
            if (currentHealth <= 0)
            {
                currentHealth = 0;

                // --- 【核心新增】击杀敌人获得经验 ---
                // 在调用 Die() 之前或之后都可以，只要确认死透了
                if (sourcePart != null && sourcePart.StatBlock != null &&
                    sourcePart.StatBlock.xpSource == WeaponXpSource.EnemyKilled)
                {
                    // 击杀获得固定经验 (通常是 1 * 系数)
                    sourcePart.GainProficiencyXP(1f * sourcePart.StatBlock.xpGainFactor);
                }
                // ------------------------------------

                Die();
            }
        }
        return wasReflected;
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    /// <returns>如果成功恢复了生命（之前没满血），返回 true</returns>
    public bool Heal(int amount)
    {
        if (IsDead || currentHealth >= maxHealth) return false;

        int oldHealth = currentHealth;
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (currentHealth != oldHealth)
        {
            int healedAmount = currentHealth - oldHealth;
            if (isPlayerHealth && healedAmount > 0 && PlayerProgressManager.Instance != null)
            {
                PlayerProgressManager.Instance.AddStat("Player_TotalHealing", healedAmount);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            // 血量回升可能影响条件型被动（如狂战士之心）
            if (isPlayerHealth && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnPlayerHealthChanged();
            }
            return true;
        }
        return false;
    }
    private IEnumerator InvincibilitySequence()
    {
        // 1. 开启无敌标记
        isPostHitInvincible = true;

        // 2. 设置所有模型 Renderer 的 Emission 为高亮红
        if (modelRenderers != null)
        {
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null)
                {
                    SetRendererEmission(modelRenderers[i], damageEmissionColor);
                }
            }
        }

        // 3. 等待闪红时间
        yield return new WaitForSeconds(flashDuration);

        // 4. 恢复 Emission 颜色
        if (modelRenderers != null)
        {
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null)
                {
                    SetRendererEmission(modelRenderers[i], originalEmissionColors[i]);
                }
            }
        }

        // 5. 剩余无敌时间：半透闪烁
        float remainingInvincibility = invincibilityDuration - flashDuration;
        if (remainingInvincibility > 0 && modelRenderers != null)
        {
            float flickerInterval = 0.08f; // 闪烁间隔（秒）
            float elapsed = 0f;
            bool isTransparent = false;

            // 备份原始颜色
            Color[] originalBaseColors = new Color[modelRenderers.Length];
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                originalBaseColors[i] = GetRendererBaseColor(modelRenderers[i]);
            }

            while (elapsed < remainingInvincibility)
            {
                isTransparent = !isTransparent;

                for (int i = 0; i < modelRenderers.Length; i++)
                {
                    if (modelRenderers[i] == null) continue;

                    if (isTransparent)
                    {
                        // 半透明灰色效果
                        Color fadedColor = originalBaseColors[i] * 0.4f;
                        fadedColor.a = 0.3f;
                        SetRendererColor(modelRenderers[i], fadedColor);
                    }
                    else
                    {
                        // 恢复正常
                        SetRendererColor(modelRenderers[i], originalBaseColors[i]);
                    }
                }

                yield return new WaitForSeconds(flickerInterval);
                elapsed += flickerInterval;
            }

            // 确保恢复为完全正常
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null)
                {
                    SetRendererColor(modelRenderers[i], originalBaseColors[i]);
                }
            }
        }

        // 6. 结束无敌
        isPostHitInvincible = false;
    }

    /// <summary>
    /// 辅助方法：设置 Renderer 的基础颜色（兼容 URP _BaseColor 和 Standard _Color）
    /// </summary>
    private void SetRendererColor(Renderer renderer, Color color)
    {
        Material mat = renderer != null ? renderer.sharedMaterial : null;
        if (mat == null) return;

        int propertyId = 0;
        if (mat.HasProperty(BaseColorProperty))
        {
            propertyId = BaseColorProperty;
        }
        else if (mat.HasProperty(ColorProperty))
        {
            propertyId = ColorProperty;
        }
        else return;

        ApplyRendererColor(renderer, propertyId, color);
    }

    private void SetRendererEmission(Renderer renderer, Color color)
    {
        Material mat = renderer != null ? renderer.sharedMaterial : null;
        if (mat == null || !mat.HasProperty(EmissionColorProperty)) return;

        ApplyRendererColor(renderer, EmissionColorProperty, color);
    }

    private Color GetRendererBaseColor(Renderer renderer)
    {
        Material mat = renderer != null ? renderer.sharedMaterial : null;
        if (mat == null) return Color.white;
        if (mat.HasProperty(BaseColorProperty)) return mat.GetColor(BaseColorProperty);
        if (mat.HasProperty(ColorProperty)) return mat.GetColor(ColorProperty);
        return Color.white;
    }

    private void ApplyRendererColor(Renderer renderer, int propertyId, Color color)
    {
        if (renderer == null) return;
        if (materialPropertyBlock == null) materialPropertyBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(propertyId, color);
        renderer.SetPropertyBlock(materialPropertyBlock);
    }

    // 缓存实体高度（避免每次受伤都重新计算）
    private float cachedEntityTopOffset = -1f;

    /// <summary>
    /// 获取实体顶部的世界Y坐标（基于Collider或Renderer的包围盒）
    /// </summary>
    private float GetEntityTopY()
    {
        // 首次调用时计算并缓存偏移量
        if (cachedEntityTopOffset < 0f)
        {
            cachedEntityTopOffset = 1.5f; // 默认回退值

            // 优先使用 Collider 的包围盒
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                cachedEntityTopOffset = col.bounds.max.y - transform.position.y + 0.3f;
            }
            else
            {
                // 回退：使用所有 Renderer 的合并包围盒
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds combinedBounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        combinedBounds.Encapsulate(renderers[i].bounds);
                    }
                    cachedEntityTopOffset = combinedBounds.max.y - transform.position.y + 0.3f;
                }
            }
        }

        return transform.position.y + cachedEntityTopOffset;
    }

    public void Die(bool destroyImmediately = true)
    {
        if (IsDead) return;

        // --- 【被动道具】不死鸟的羽毛：复活检查 ---
        if (isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.TryUsePhoenixRevive(maxHealth, out int reviveHealth))
        {
            currentHealth = Mathf.Clamp(reviveHealth, 1, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            PlayerStats.Instance.OnPlayerHealthChanged();

            // 触发短暂无敌
            StartCoroutine(InvincibilitySequence());

            Debug.Log($"<color=green>[不死鸟的羽毛] 复活触发！恢复血量至{currentHealth}/{maxHealth}，冷却={PlayerStats.Instance.phoenixReviveCooldownRemaining:F1}s</color>");
            return; // 不执行死亡逻辑
        }

        IsDead = true;

        currentHealth = 0;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // --- 整合了您的“燃烧时死亡会爆炸”逻辑 ---
        /*StatusEffectReceiver receiver = GetComponent<StatusEffectReceiver>();
        if (receiver != null && receiver.IsBurning)
        {
            ExplodeOnDeath();
        }*/
        // --- 整合结束 ---

        OnDeath?.Invoke();

        // 触发全局敌人死亡事件（供光环生命汲取等技能监听）
        if (!isPlayerHealth)
        {
            OnEnemyDied?.Invoke(this);

            // 疑影无踪：击杀敌人时概率生成影分身
            PlayerBladeAttack blade = FindFirstObjectByType<PlayerBladeAttack>();
            if (blade != null)
            {
                blade.TrySpawnShadowClone(transform.position);
            }
        }

        // 检查是否是 Boss 死亡
        BossUnit bossUnit = GetComponent<BossUnit>();
        if (bossUnit == null) bossUnit = GetComponentInParent<BossUnit>();
        if (bossUnit == null) bossUnit = GetComponentInChildren<BossUnit>();
        bool isBossDeath = (bossUnit != null);
        GameObject bossCeremonyObject = isBossDeath ? GetBossCeremonyObject(bossUnit) : null;

        if (isBossDeath)
        {
            StopBossCombatOnDeath(bossCeremonyObject);
        }

        bool shouldCountAsEnemyDeath = gameObject.CompareTag("Enemy") || isBossDeath;

        if (shouldCountAsEnemyDeath)
        {
            // 如果是 Boss，先把信息注册到 GameTimelineManager，供死亡表演使用
            if (isBossDeath)
            {
                Vector3 bossDeathPosition = bossCeremonyObject != null ? bossCeremonyObject.transform.position : transform.position;
                GameObject bossDeathObject = bossCeremonyObject != null ? bossCeremonyObject : gameObject;

                if (GameTimelineManager.Instance != null)
                {
                    GameTimelineManager.Instance.BossDefeated(bossDeathPosition, bossDeathObject);
                }
                else if (BossDeathCeremony.Instance != null)
                {
                    BossDeathCeremony.Instance.StartCeremony(bossDeathPosition, bossDeathObject);
                }
            }
            else if (GetComponent<PressureSpawnedEnemy>() != null)
            {
                GameTimelineManager.Instance?.PressureEnemyDefeated();
            }
            else
            {
                GameTimelineManager.Instance?.EnemyDefeated();
            }
        }

        // Boss 不执行普通掉落和立即销毁（由 BossDeathCeremony 接管）
        if (!isBossDeath)
        {
            HandleDrops();
            var enemyAI = GetComponent<EnemyAI>();

            if (EnemyTypeData != null && EnemyTypeData.deathVfxPrefab != null)
            {
                // 在当前物体的位置生成死亡特效
                Instantiate(EnemyTypeData.deathVfxPrefab, transform.position, Quaternion.identity);
            }

            if (destroyImmediately)
            {
                Destroy(gameObject);
            }
        }

        if (shouldCountAsEnemyDeath)
        {
            if (BattleStatisticsManager.Instance != null)
                BattleStatisticsManager.Instance.AddKill(); // [新增]

            if (PlayerProgressManager.Instance != null)
                PlayerProgressManager.Instance.AddStat("Kill_Count", 1);
        }
    }

    private GameObject GetBossCeremonyObject(BossUnit bossUnit)
    {
        if (bossUnit == null) return gameObject;

        if (bossUnit.GetComponent<Health>() != null)
        {
            return bossUnit.gameObject;
        }

        if (GetComponentInParent<BossUnit>() == bossUnit)
        {
            return bossUnit.gameObject;
        }

        return gameObject;
    }

    private void StopBossCombatOnDeath(GameObject bossRoot)
    {
        GameObject targetRoot = bossRoot != null ? bossRoot : gameObject;

        foreach (BehaviorTree behaviorTree in targetRoot.GetComponentsInChildren<BehaviorTree>(true))
        {
            behaviorTree.enabled = false;
        }

        foreach (EnemyAI ai in targetRoot.GetComponentsInChildren<EnemyAI>(true))
        {
            ai.enabled = false;
        }

        foreach (UnityEngine.AI.NavMeshAgent agent in targetRoot.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
        {
            if (!agent.enabled) continue;

            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        foreach (BossBeamController beam in targetRoot.GetComponentsInChildren<BossBeamController>(true))
        {
            if (beam != null)
            {
                Destroy(beam.gameObject);
            }
        }

        foreach (Collider col in targetRoot.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        foreach (Rigidbody body in targetRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.Sleep();
        }
    }

    private void HandleDrops()
    {
        // 1. (保持不变) 掉落经验
        if (experienceGemPrefab != null) //
        {
            Instantiate(experienceGemPrefab, transform.position, Quaternion.identity); //
        }

        // 2. (保持不变) 掉落金币
        if (goldCoinPrefab != null && Random.value <= goldDropChance) //
        {
            Instantiate(goldCoinPrefab, transform.position, Quaternion.identity); //
        }

        // 3. (新逻辑) 掉落能量石
        // 【修复】用 if 嵌套代替 return，防止阻断后续的血包和宝箱掉落
        if (GameManager.Instance != null && EnemyTypeData != null)
        {
            List<EnergyStoneSO> lootTable = GameManager.Instance.energyStoneLootTable;
            if (lootTable != null && lootTable.Count > 0)
            {
                // A. 掷骰子
                float dropChance = EnemyTypeData.energyStoneDropChance;
                if (Random.value <= dropChance)
                {
                    // B. 随机选择一个石头 *数据*
                    EnergyStoneSO chosenStone = lootTable[Random.Range(0, lootTable.Count)];

                    if (chosenStone != null)
                    {
                        // C. 从石头 *数据* 中获取它专属的 *预制件*
                        GameObject prefabToDrop = chosenStone.pickupPrefab;

                        if (prefabToDrop != null)
                        {
                            // D. 实例化专属预制件
                            GameObject stoneGO = Instantiate(prefabToDrop, transform.position, Quaternion.identity);

                            // E. 将石头数据 赋给掉落物
                            EnergyStonePickup pickupScript = stoneGO.GetComponent<EnergyStonePickup>();
                            if (pickupScript != null)
                            {
                                pickupScript.stoneData = chosenStone;
                            }
                            else
                            {
                                Debug.LogError($"能量石掉落失败: 预制件 '{prefabToDrop.name}' 缺少 'EnergyStonePickup' 脚本!", prefabToDrop);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"能量石掉落失败: 'EnergyStoneSO' 资产 '{chosenStone.stoneName}' 没有分配 'Pickup Prefab' 字段。", chosenStone);
                        }
                    }
                }
            }
        }

        // 4. 掉落血包
        if (healthPickupPrefab != null)
        {
            if (Random.value <= healthDropChance)
            {
                Instantiate(healthPickupPrefab, transform.position, Quaternion.identity);
            }
        }

        // 5. 掉落宝箱（按 EnemyType 的掉率判定）
        if (treasureChestPrefab != null && EnemyTypeData != null)
        {
            float chestChance = EnemyTypeData.treasureChestDropChance;
            if (Random.value <= chestChance)
            {
                Instantiate(treasureChestPrefab, transform.position, Quaternion.identity);
                Debug.Log($"<color=yellow>[宝箱掉落] 在 {transform.position} 生成了宝箱！掉率={chestChance}</color>");
            }
        }
        else
        {
            // 调试日志：帮助排查为什么没掉宝箱
            if (treasureChestPrefab == null)
                Debug.Log("<color=red>[宝箱掉落] treasureChestPrefab 未设置！请在怪物预制件的 Health 组件上设置。</color>");
            if (EnemyTypeData == null)
                Debug.Log("<color=red>[宝箱掉落] EnemyTypeData 为 null！怪物可能未正确初始化。</color>");
        }
    }

    public void SetInvincible(float duration)
    {
        // 如果已经在无敌中，且新时间更长，则重置
        // 这里简单起见，直接启动新协程
        StartCoroutine(ManualInvincibilityRoutine(duration));
    }

    private IEnumerator ManualInvincibilityRoutine(float duration)
    {
        isPostHitInvincible = true;
        // 可选：你可以在这里加一些视觉效果，比如变透明或者残影
        // if (modelRenderer != null) modelRenderer.material.color = new Color(1,1,1,0.5f); 

        yield return new WaitForSeconds(duration);

        isPostHitInvincible = false;
        // if (modelRenderer != null) modelRenderer.material.color = Color.white;
    }
    private void ExplodeOnDeath()
    {
        float explosionRadius = 5f;
        int explosionDamage = 10;
        LayerMask damageableLayers = LayerMask.GetMask("Enemies");

        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, explosionRadius, damageableLayers);
        foreach (Collider hitCollider in collidersInRange)
        {
            if (hitCollider.gameObject == this.gameObject) continue;

            Health healthComponent = hitCollider.GetComponent<Health>();
            if (healthComponent != null && !healthComponent.IsDead)
            {
                // 【核心修复】这里的 TakeDamage 调用已更新为新版签名
                healthComponent.TakeDamage(explosionDamage, hitCollider.transform.position, this.gameObject, AttackType.Standard);

                StatusEffectReceiver nearbyReceiver = healthComponent.GetComponent<StatusEffectReceiver>();
                if (nearbyReceiver != null)
                {
                    nearbyReceiver.ApplyBurn(5, 3f, 1f);
                }
            }
        }
    }

    // --- 用于UI更新的公共方法 ---
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    public bool HasActiveShield()
    {
        // 如果护盾实例存在，并且当前护盾值大于0，则返回true
        return PlayerShield.Instance != null && PlayerShield.Instance.GetCurrentShield() > 0;
    }
}
