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

    private EnemyType enemyTypeData;

    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<int, int> { }
    [Header("事件")]
    public HealthChangedEvent OnHealthChanged;

    [Header("死亡事件")]
    [Tooltip("当生命值归零时触发的事件")]
    public UnityEvent OnDeath;
    public bool IsDead { get; private set; }
    [Header("受伤无敌与视觉 (新增)")]
    [Tooltip("玩家受伤后的无敌时间 (秒)")]
    public float invincibilityDuration = 1.0f;
    [Tooltip("受击闪烁的持续时间 (秒) - 建议很短，如 0.15")]
    public float flashDuration = 0.15f;
    [Tooltip("受伤时 Emission 的颜色 (HDR)")]
    [ColorUsage(true, true)] // 允许在Inspector里调节HDR亮度
    public Color damageEmissionColor = new Color(1f, 0f, 0f, 1f) * 3f; // 默认红光，强度3

    private bool isPostHitInvincible = false; // 是否处于受击后的短暂无敌状态
    private Renderer modelRenderer;
    private Color originalEmissionColor; // 记录原始发光颜色


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

        audioSource = GetComponent<AudioSource>();
        statusReceiver = GetComponent<StatusEffectReceiver>();

        modelRenderer = GetComponentInChildren<Renderer>();
        if (modelRenderer != null)
        {
            // 1. 确保启用 Emission 关键字 (如果是 Standard/URP Lit Shader)
            modelRenderer.material.EnableKeyword("_EMISSION");

            // 2. 记录原始 Emission 颜色 (有些材质默认为黑)
            if (modelRenderer.material.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = modelRenderer.material.GetColor("_EmissionColor");
            }
            else
            {
                originalEmissionColor = Color.black;
            }
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
    public void InitializeHealth(int initialMaxHealth, EnemyType typeData)
    {
        maxHealth = initialMaxHealth;
        currentHealth = maxHealth;
        this.enemyTypeData = typeData;
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

        // 2. 状态效果计算 (腐蚀等)
        if (statusReceiver != null && statusReceiver.IsCorroded)
            damageAmount = Mathf.RoundToInt(damageAmount * statusReceiver.corrodeDamageMultiplier);

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
            // ----------------------------------------------------
            if (sourcePart == null)
            {
                
            }
            else
            {
                

                if (sourcePart.StatBlock == null) ;
                else if (!sourcePart.StatBlock.usesProficiency) ;
                else if (sourcePart.StatBlock.xpSource != WeaponXpSource.DamageDealt);
            }

            // --- 【核心新增】造成伤害获得经验 ---
            if (sourcePart != null && sourcePart.StatBlock != null &&
                sourcePart.StatBlock.xpSource == WeaponXpSource.DamageDealt)
            {
                float xp = remainingDamage * sourcePart.StatBlock.xpGainFactor;               
                sourcePart.GainProficiencyXP(xp);
            }
            // ------------------------------------

            if (!string.IsNullOrEmpty(sourceWeaponName) && !isPlayerHealth && BattleStatisticsManager.Instance != null)
                BattleStatisticsManager.Instance.AddDamage(sourceWeaponName, remainingDamage);

            // 跳字逻辑
            if (damagePopupPrefab != null)
            {
                float randomX = Random.Range(-0.5f, 0.5f);
                float randomY = Random.Range(0f, 0.5f);
                GameObject popupGO = Instantiate(damagePopupPrefab, transform.position + Vector3.up * 1.5f + new Vector3(randomX, randomY, 0), Quaternion.identity);
                DamagePopup damagePopup = popupGO.GetComponent<DamagePopup>();
                if (damagePopup != null) damagePopup.InitPopup(remainingDamage, isCritical);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (impactSounds != null && impactSounds.Length > 0)
                audioSource.PlayOneShot(impactSounds[Random.Range(0, impactSounds.Length)]);

            if (isPlayerHealth && !IsDead) StartCoroutine(InvincibilitySequence());

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
            return true;
        }
        return false;
    }
    private IEnumerator InvincibilitySequence()
    {
        // 1. 开启无敌标记
        isPostHitInvincible = true;

        // 2. 设置 Emission 为高亮红 (只闪一次)
        if (modelRenderer != null)
        {
            modelRenderer.material.SetColor("_EmissionColor", damageEmissionColor);
            // 强制刷新一下 Global Illumination (有时需要)
            DynamicGI.UpdateEnvironment();
        }

        // 3. 等待闪烁时间 (例如 0.15秒)
        yield return new WaitForSeconds(flashDuration);

        // 4. 恢复 Emission 颜色
        if (modelRenderer != null)
        {
            modelRenderer.material.SetColor("_EmissionColor", originalEmissionColor);
            DynamicGI.UpdateEnvironment();
        }

        // 5. 计算剩余的无敌时间
        float remainingInvincibility = invincibilityDuration - flashDuration;
        if (remainingInvincibility > 0)
        {
            yield return new WaitForSeconds(remainingInvincibility);
        }

        // 6. 结束无敌
        isPostHitInvincible = false;
    }


    public void Die(bool destroyImmediately = true)
    {
        if (IsDead) return;
        IsDead = true;

        currentHealth = 0;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // --- 整合了您的“燃烧时死亡会爆炸”逻辑 ---
        /*StatusEffectReceiver receiver = GetComponent<StatusEffectReceiver>();
        if (receiver != null && receiver.IsBurning)
        {
            Debug.Log($"{gameObject.name} 在燃燒中死亡，觸發爆炸！");
            ExplodeOnDeath();
        }*/
        // --- 整合结束 ---

        OnDeath?.Invoke();

        if (gameObject.CompareTag("Enemy"))
        {
            GameTimelineManager.Instance?.EnemyDefeated();
        }

        HandleDrops();
        var enemyAI = GetComponent<EnemyAI>();

       
        if (enemyTypeData != null && enemyTypeData.deathVfxPrefab != null)
        {
            // 在当前物体的位置生成死亡特效
            Instantiate(enemyTypeData.deathVfxPrefab, transform.position, Quaternion.identity);
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
        // 检查: (是否可以掉落?)
        if (GameManager.Instance == null || enemyTypeData == null) return; //

        List<EnergyStoneSO> lootTable = GameManager.Instance.energyStoneLootTable; //
        if (lootTable == null || lootTable.Count == 0) return; //

        // A. 掷骰子
        float dropChance = enemyTypeData.energyStoneDropChance; //
        if (Random.value <= dropChance)
        {
            // B. 随机选择一个石头 *数据*
            EnergyStoneSO chosenStone = lootTable[Random.Range(0, lootTable.Count)]; //
            if (chosenStone == null) return;

            // C. [新] 从石头 *数据* 中获取它专属的 *预制件*
            GameObject prefabToDrop = chosenStone.pickupPrefab; //

            if (prefabToDrop != null)
            {
                // D. 实例化专属预制件
                GameObject stoneGO = Instantiate(prefabToDrop, transform.position, Quaternion.identity); //

                // E. 将石头数据 赋给掉落物
                EnergyStonePickup pickupScript = stoneGO.GetComponent<EnergyStonePickup>(); //
                if (pickupScript != null)
                {
                    pickupScript.stoneData = chosenStone; //
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
        if (healthPickupPrefab != null)
        {
            // 你也可以乘上幸运值: healthDropChance * PlayerStats.Instance.luck
            if (Random.value <= healthDropChance)
            {
                Instantiate(healthPickupPrefab, transform.position, Quaternion.identity);
            }
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