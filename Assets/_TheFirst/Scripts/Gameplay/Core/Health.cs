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
    private Rigidbody playerRigidbody; // 缓存玩家刚体引用


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
        originalEmissionColors = new Color[modelRenderers.Length];
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            var mat = modelRenderers[i].material;
            mat.EnableKeyword("_EMISSION");
            originalEmissionColors[i] = mat.HasProperty("_EmissionColor")
                ? mat.GetColor("_EmissionColor")
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

            // --- 吸血处理：玩家对敌人造成伤害时回血 ---
            if (!isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.lifeStealPercent > 0f)
            {
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(remainingDamage * PlayerStats.Instance.lifeStealPercent));
                // 找到玩家的 Health 组件
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Health playerHealth = player.GetComponent<Health>();
                    if (playerHealth != null && !playerHealth.IsDead)
                    {
                        playerHealth.Heal(healAmount);
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
                        freezeReceiver.ApplyFreeze(1.5f); // 冰冻1.5秒
                    }
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

                // --- 【被动道具】荆棘护甲：受伤时反弹伤害给攻击者 ---
                if (PlayerStats.Instance != null && PlayerStats.Instance.thornsReflectPercent > 0f && attacker != null)
                {
                    Health attackerHealth = attacker.GetComponent<Health>();
                    if (attackerHealth != null && !attackerHealth.IsDead)
                    {
                        int thornsDamage = Mathf.Max(1, Mathf.RoundToInt(remainingDamage * PlayerStats.Instance.thornsReflectPercent));
                        attackerHealth.TakeDamage(thornsDamage, attacker.transform.position, gameObject, AttackType.Standard, null, null, "荆棘护甲");
                    }
                }
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
                    modelRenderers[i].material.SetColor("_EmissionColor", damageEmissionColor);
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
                    modelRenderers[i].material.SetColor("_EmissionColor", originalEmissionColors[i]);
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
                if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_BaseColor"))
                {
                    originalBaseColors[i] = modelRenderers[i].material.GetColor("_BaseColor");
                }
                else if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                {
                    originalBaseColors[i] = modelRenderers[i].material.GetColor("_Color");
                }
                else
                {
                    originalBaseColors[i] = Color.white;
                }
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
        Material mat = renderer.material;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
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
        if (isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.revivalCount > 0)
        {
            // 消耗一次复活
            PlayerStats.Instance.revivalCount--;

            // 恢复50%血量
            currentHealth = Mathf.Max(1, maxHealth / 2);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // 触发短暂无敌
            StartCoroutine(InvincibilitySequence());

            Debug.Log($"<color=green>[不死鸟的羽毛] 复活触发！剩余复活次数={PlayerStats.Instance.revivalCount}，恢复血量至{currentHealth}/{maxHealth}</color>");
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
        }

        if (gameObject.CompareTag("Enemy"))
        {
            GameTimelineManager.Instance?.EnemyDefeated();
        }

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

        if (gameObject.CompareTag("Enemy") && BattleStatisticsManager.Instance != null)
            BattleStatisticsManager.Instance.AddKill(); // [新增]
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