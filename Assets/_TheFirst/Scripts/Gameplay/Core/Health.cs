using UnityEngine;
using UnityEngine.Events; // 用于死亡事件

public class Health : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField] private int maxHealth = 100; // 最大生命值 (可以在 Inspector 中修改)
    private int currentHealth; // 当前生命值

    [Header("死亡事件")]
    [Tooltip("当生命值归零时触发的事件 (可以在 Inspector 中关联特效生成、声音播放、分数增加等)")]
    public UnityEvent OnDeath; // 死亡时触发的事件

    public bool IsDead => currentHealth <= 0; // 判断是否已死亡

    void Awake() // 使用 Awake 确保在 Start 前初始化
    {
        currentHealth = maxHealth; // 游戏开始时设置为满血
    }

    /// <summary>
    /// 对此物体造成伤害。
    /// </summary>
    /// <param name="damageAmount">造成的伤害值</param>
    public void TakeDamage(int damageAmount)
    {
        if (IsDead) return; // 如果已经死亡，不再接受伤害

        currentHealth -= damageAmount;
        Debug.Log($"{gameObject.name} 受到 {damageAmount} 点伤害, 剩余生命: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0; // 防止负数
            Die(); // 执行死亡逻辑
        }
        // (可选) 在这里可以触发受伤事件、播放受伤音效、显示伤害数字等
        // OnTakeDamage?.Invoke(damageAmount);
    }

    /// <summary>
    /// 死亡逻辑。
    /// </summary>
    private void Die()
    {
        Debug.Log($"{gameObject.name} 已被摧毁!");
        OnDeath?.Invoke();

        // --- 添加简单的死亡反馈 ---
        // 例如: 实例化一个爆炸特效预设
        // public GameObject deathEffectPrefab; // 需要在 Health 脚本添加这个变量并在 Inspector 指定
        // if (deathEffectPrefab != null) Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        // 例如: 播放死亡音效
        // public AudioClip deathSound; // 需要添加 AudioSource 组件和这个变量
        // AudioSource audioSource = GetComponent<AudioSource>();
        // if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
        // -----------------------

        Destroy(gameObject);
    }

    // (可选) 提供获取当前/最大生命值的方法
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
}