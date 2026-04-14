using UnityEngine;

/// <summary>
/// 触发型被动道具的运行时统一管理器
/// 挂在玩家根物体上，负责监听全局事件并驱动事件驱动型被动
/// 
/// 管理范围：
/// - 灵魂汲取（击杀回血）
/// - 雷霆意志（击杀触发雷击AOE）
/// 
/// 已迁移到其他组件：
/// - 狂战士之心 → PlayerStats.ApplyBerserkerBonus()
/// - 冲刺余烬 → MechController.TriggerDashExplosion()
/// - 燃烧轨迹 → MechController.UpdateFlameTrail()
/// </summary>
public class PassiveEffectManager : MonoBehaviour
{
    public static PassiveEffectManager Instance { get; private set; }

    [Header("引用（自动获取）")]
    private Health playerHealth;

    [Header("雷霆意志")]
    [Tooltip("雷击AOE预制件（可选，没有也能造成伤害）")]
    public GameObject thunderStrikePrefab;
    [Tooltip("雷击AOE基础伤害")]
    public int thunderStrikeBaseDamage = 30;
    [Tooltip("雷击AOE半径")]
    public float thunderStrikeRadius = 3f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        // 缓存玩家Health引用
        playerHealth = GetComponent<Health>();
        if (playerHealth == null)
        {
            Debug.LogError("[PassiveEffectManager] 未找到 Health 组件！请确保挂在玩家根物体上。", this);
        }

        // 订阅全局敌人死亡事件
        Health.OnEnemyDied += HandleEnemyDied;

        Debug.Log("<color=cyan>[PassiveEffectManager] 初始化完成，已订阅 OnEnemyDied 事件</color>");
    }

    void OnDestroy()
    {
        Health.OnEnemyDied -= HandleEnemyDied;
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 敌人死亡事件处理
    // ============================================================
    /// <summary>
    /// 监听全局敌人死亡事件，驱动击杀回血和雷霆意志
    /// </summary>
    private void HandleEnemyDied(Health enemyHealth)
    {
        if (PlayerStats.Instance == null || playerHealth == null) return;

        // --- 灵魂汲取：击杀回血 ---
        if (PlayerStats.Instance.killHealAmount > 0)
        {
            if (!playerHealth.IsDead)
            {
                playerHealth.Heal(PlayerStats.Instance.killHealAmount);
            }
        }

        // --- 雷霆意志：击杀时有概率触发雷击AOE ---
        if (PlayerStats.Instance.thunderWillChance > 0f)
        {
            float roll = Random.value;
            float chance = PlayerStats.Instance.thunderWillChance;
            if (roll < chance)
            {
                TriggerThunderStrike(enemyHealth.transform.position);
            }
        }
    }

    // ============================================================
    // 雷霆意志 — 击杀触发雷击AOE
    // ============================================================
    /// <summary>
    /// 在指定位置触发雷击AOE
    /// </summary>
    private void TriggerThunderStrike(Vector3 position)
    {
        // 计算最终伤害
        float dmgMultiplier = (1f + PlayerStats.Instance.thunderWillDamageBonus) * PlayerStats.Instance.damageMultiplier;
        int finalDamage = Mathf.RoundToInt(thunderStrikeBaseDamage * dmgMultiplier);

        // 生成视觉特效
        if (thunderStrikePrefab != null)
        {
            GameObject vfx = Instantiate(thunderStrikePrefab, position, Quaternion.identity);
            Destroy(vfx, 2f); // 2秒后自动清理
        }

        // 对范围内敌人造成伤害
        Collider[] hits = Physics.OverlapSphere(position, thunderStrikeRadius, LayerMask.GetMask("Enemies"));
        foreach (var hit in hits)
        {
            Health targetHealth = hit.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(finalDamage, position, gameObject, AttackType.Standard, null, null, "雷霆意志");
            }
        }

        Debug.Log($"<color=yellow>[雷霆意志] 触发雷击！伤害={finalDamage}, 命中={hits.Length}</color>");
    }
}
