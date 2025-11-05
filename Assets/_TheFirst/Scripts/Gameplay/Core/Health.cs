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


    /// <summary>
    /// Awake 在对象实例化后立即被调用。
    /// 这将为所有使用此脚本的对象（包括玩家和敌人）提供一个初始的满血状态。
    /// </summary>
    void Awake()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 设为3D音效
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

    public bool TakeDamage(int damageAmount, Vector3 hitPosition, GameObject attacker = null, AttackType type = AttackType.Standard, Projectile projectile = null, EnemyBeamController beamController = null)
    {
        if (IsDead) return false;

        if (isPlayerHealth && PlayerStats.Instance != null && PlayerStats.Instance.isInvincible)
        {
            return false; // 如果是玩家且处于无敌状态，则不承受任何伤害
        }

        int remainingDamage = damageAmount;
        bool wasReflected = false;

        if (isPlayerHealth && PlayerShield.Instance != null && PlayerShield.Instance.GetCurrentShield() > 0)
        {
            // 将接收到的 beamController 传递给护盾
            remainingDamage = PlayerShield.Instance.AbsorbDamage(damageAmount, hitPosition, type, projectile, beamController, out wasReflected);
        }

        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage;

            // 同时，让生命值跳字也使用这个精确的命中位置
            if (damagePopupPrefab != null)
            {
                Vector3 popupPosition = transform.position + Vector3.up * 1.5f; 
              
                GameObject popupGO = Instantiate(damagePopupPrefab, popupPosition, Quaternion.identity);

                DamagePopup damagePopup = popupGO.GetComponent<DamagePopup>();
                if (damagePopup != null)
                {
                    damagePopup.Setup(remainingDamage, false); // false 代表是生命值伤害
                }
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (impactSounds != null && impactSounds.Length > 0)
            {
                AudioClip clipToPlay = impactSounds[Random.Range(0, impactSounds.Length)];
                audioSource.PlayOneShot(clipToPlay);
            }

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }
        return wasReflected;
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
    }
    private void HandleDrops()
    {
        // 掉落经验
        if (experienceGemPrefab != null)
        {
            Instantiate(experienceGemPrefab, transform.position, Quaternion.identity);
        }

        // 掉落金币
        if (goldCoinPrefab != null)
        {
            // Random.value 会返回一个0到1之间的随机数
            if (Random.value <= goldDropChance)
            {
                Instantiate(goldCoinPrefab, transform.position, Quaternion.identity);
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