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
    [SerializeField] private int maxHealth = 100; // 在 Inspector 中为预制件设置一个默认最大生命值
    private int currentHealth;

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
        // 游戏开始时，立即通知一次UI，以便显示初始血量
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    /// <summary>
    /// 提供一个公共方法来从外部初始化或重置生命值。
    /// EnemySpawner 将为每个生成的敌人调用此方法，用计算出的新值覆盖 Awake 中设置的初始值。
    /// </summary>
    /// <param name="initialMaxHealth">根据波次计算出的最大生命值</param>
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
        if (amountToAdd <= 0) return;

        maxHealth += amountToAdd;

        // 确保当前生命值不会意外超过新的最大值（虽然在此逻辑中不会发生，但这是个好习惯）
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        Debug.Log($"{gameObject.name} 的最大生命值增加了 {amountToAdd}，当前生命值为: {currentHealth}/{maxHealth}");
    }

    public bool TakeDamage(int damageAmount, Vector3 hitPosition, GameObject attacker = null, AttackType type = AttackType.Standard, Projectile projectile = null, EnemyBeamController beamController = null, string sourceWeaponName = "")
    {
        if (IsDead) return false;

        // 1. [新增] 检查受击无敌状态 (只对玩家有效，或者你可以去掉 isPlayerHealth 限制让敌人也有无敌时间)
        if (isPlayerHealth && isPostHitInvincible)
        {
            return false; // 处于无敌状态，不受伤害
        }

        // 2. 检查腐蚀易伤
        if (statusReceiver != null && statusReceiver.IsCorroded)
        {
            damageAmount = Mathf.RoundToInt(damageAmount * statusReceiver.corrodeDamageMultiplier);
        }

        // 3. 检查全局无敌 (作弊/剧情)
        if (isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.isInvincible)
        {
            return false;
        }

        int remainingDamage = damageAmount;
        bool wasReflected = false;

        // 4. 护盾逻辑
        if (isPlayerHealth && PlayerShield.Instance != null && PlayerShield.Instance.GetCurrentShield() > 0)
        {
            remainingDamage = PlayerShield.Instance.AbsorbDamage(damageAmount, hitPosition, type, projectile, beamController, out wasReflected);
        }

        // 5. 实际扣血
        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage;

            // 统计数据
            if (!string.IsNullOrEmpty(sourceWeaponName) && !isPlayerHealth && BattleStatisticsManager.Instance != null)
            {
                BattleStatisticsManager.Instance.AddDamage(sourceWeaponName, remainingDamage);
            }

            // 飘字
            if (damagePopupPrefab != null)
            {
                // [优化] 添加随机偏移，防止多段伤害(如雷击)重叠
                // X轴随机左右偏移 0.5，Y轴随机向上浮动 0.5
                float randomX = Random.Range(-0.5f, 0.5f);
                float randomY = Random.Range(0f, 0.5f);
                Vector3 randomOffset = new Vector3(randomX, randomY, 0);

                // 基础高度 1.5 + 随机偏移
                Vector3 popupPosition = transform.position + Vector3.up * 1.5f + randomOffset;

                GameObject popupGO = Instantiate(damagePopupPrefab, popupPosition, Quaternion.identity);
                DamagePopup damagePopup = popupGO.GetComponent<DamagePopup>();
                if (damagePopup != null)
                {
                    damagePopup.Setup(remainingDamage, false);
                }
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (impactSounds != null && impactSounds.Length > 0)
            {
                AudioClip clipToPlay = impactSounds[Random.Range(0, impactSounds.Length)];
                audioSource.PlayOneShot(clipToPlay);
            }

            // --- [新增] 触发受击无敌协程 ---
            if (isPlayerHealth && !IsDead)
            {
                StartCoroutine(InvincibilitySequence());
            }
            // ---------------------------

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }
        return wasReflected;
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
        StatusEffectReceiver receiver = GetComponent<StatusEffectReceiver>();
        if (receiver != null && receiver.IsBurning)
        {
            Debug.Log($"{gameObject.name} 在燃燒中死亡，觸發爆炸！");
            ExplodeOnDeath();
        }
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