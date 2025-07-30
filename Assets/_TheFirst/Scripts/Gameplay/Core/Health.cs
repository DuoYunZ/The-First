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

    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<int, int> { }
    [Header("事件")]
    public HealthChangedEvent OnHealthChanged;

    [Header("死亡事件")]
    [Tooltip("当生命值归零时触发的事件")]
    public UnityEvent OnDeath;
    public bool IsDead => currentHealth <= 0;

    [Header("掉落设置 (可选)")]
    [Tooltip("死亡时掉落的经验宝石预设")]
    public GameObject experienceGemPrefab;

    [Header("视觉效果 (可选)")]
    [Tooltip("受到伤害时生成的跳字预制件")]
    public GameObject damagePopupPrefab; 

    /// <summary>
    /// Awake 在对象实例化后立即被调用。
    /// 这将为所有使用此脚本的对象（包括玩家和敌人）提供一个初始的满血状态。
    /// </summary>
    void Awake()
    {
        currentHealth = maxHealth;       
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
    public void InitializeHealth(int initialMaxHealth)
    {
        maxHealth = initialMaxHealth;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void AddMaxHealth(int amountToAdd)
    {
        if (amountToAdd <= 0) return;

        maxHealth += amountToAdd;
        currentHealth += amountToAdd;

        // 确保当前生命值不会意外超过新的最大值（虽然在此逻辑中不会发生，但这是个好习惯）
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        Debug.Log($"{gameObject.name} 的最大生命值增加了 {amountToAdd}，当前生命值为: {currentHealth}/{maxHealth}");
    }

    public bool TakeDamage(int damageAmount, Vector3 hitPosition, GameObject attacker = null, AttackType type = AttackType.Standard, Projectile projectile = null, EnemyBeamAttack beamAttacker = null)
    {
        if (IsDead) return false;

        int remainingDamage = damageAmount;
        bool wasReflected = false;

        if (isPlayerHealth && PlayerShield.Instance != null && PlayerShield.Instance.GetCurrentShield() > 0)
        {
            // 将 beamAttacker 传递给护盾
            remainingDamage = PlayerShield.Instance.AbsorbDamage(damageAmount, hitPosition, type, projectile, beamAttacker, out wasReflected);
        }

        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage;

            // 同时，让生命值跳字也使用这个精确的命中位置
            if (damagePopupPrefab != null)
            {
                // Vector3 popupPosition = transform.position + Vector3.up * 1.5f; // 旧代码
                Vector3 popupPosition = hitPosition + Vector3.up * 1.5f; // 新代码
                GameObject popupGO = Instantiate(damagePopupPrefab, popupPosition, Quaternion.identity);

                DamagePopup damagePopup = popupGO.GetComponent<DamagePopup>();
                if (damagePopup != null)
                {
                    damagePopup.Setup(remainingDamage, false); // false 代表是生命值伤害
                }
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }
        return wasReflected;
    }


    private void Die()
    {
        // --- 新增：檢查死亡時是否在燃燒 ---
        StatusEffectReceiver receiver = GetComponent<StatusEffectReceiver>();
        if (receiver != null && receiver.IsBurning)
        {
            Debug.Log($"{gameObject.name} 在燃燒中死亡，觸發爆炸！");
            ExplodeOnDeath(); // 呼叫一個新的爆炸方法
            // 在這裡實例化一個小型的爆炸特效和傷害判定
            // 這個邏輯可以復用 Projectile 的 Explode 方法，或者自己寫一個簡單的
            // 例如：
            // ExplodeOnDeath(transform.position); 
        }
        OnDeath?.Invoke();
        if (gameObject.CompareTag("Enemy"))
        {
            WaveManager.Instance?.EnemyDefeated();
        }
        if (experienceGemPrefab != null)
        {
            Instantiate(experienceGemPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
    private void ExplodeOnDeath()
    {
        // 這個邏輯和 Projectile 的 Explode 方法非常相似
        // 我們需要獲取爆炸的範圍、傷害等資訊。
        // 為了簡單起見，我們可以暫時寫死或從一個全局管理器獲取。
        // 一個更好的方法是讓 StatusEffectReceiver 記住是哪個武器施加的燃燒，然後從那裡讀取爆炸屬性。
        // 我們先用一個簡單的實現：

        float explosionRadius = 5f; // 可以先用一個固定值
        int explosionDamage = 10; // 死亡爆炸的傷害
        LayerMask damageableLayers = LayerMask.GetMask("Enemies"); // 假設你的敵人在 "Enemies" 層

        // 視覺特效
        // if (deathExplosionPrefab != null) Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);

        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, explosionRadius, damageableLayers);
        foreach (Collider hitCollider in collidersInRange)
        {
            if (hitCollider.gameObject == this.gameObject) continue; // 不要傷害到自己（雖然自己馬上要被銷毀了）

            Health healthComponent = hitCollider.GetComponent<Health>();
            if (healthComponent != null && !healthComponent.IsDead)
            {
                // 對周圍敵人造成爆炸傷害
                healthComponent.TakeDamage(explosionDamage, transform.position, this.gameObject);

                // --- 核心：將燃燒 BUFF 傳播出去 ---
                StatusEffectReceiver nearbyReceiver = healthComponent.GetComponent<StatusEffectReceiver>();
                if (nearbyReceiver != null)
                {
                    // 這裡的燃燒傷害和持續時間可以是一個新值，或者繼承原來的
                    nearbyReceiver.ApplyBurn(5, 3f, 1f); // 例如，傳播的燃燒效果稍弱一些
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