// PlayerStats.cs (升级版)
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("基础乘数 (会受升级影响)")]
    public float damageMultiplier = 1f;       // 所有直接伤害
    public float aoeDamageMultiplier = 1f;      // 所有范围伤害
    public float aoeRadiusMultiplier = 1f;      // 所有范围半径
    public float fireRateMultiplier = 1f;       // 射速 (这个值越小越快)
    public float projectileSpeedMultiplier = 1f; // 投射物速度
    public float pickupRadiusMultiplier = 1f;   // 拾取半径
    public float moveSpeedMultiplier = 1f;      // 移动速度

    [Header("固定值加成 (受升级影响)")]
    public float flatDamageBonus = 0f;
    public float flatAoeDamageBonus = 0f;
    public int bonusPierceCount = 0;          // 额外穿透数量
    public int bonusMaxHealth = 0;            // 额外最大生命值

    [Header("特殊属性")]
    public float luck = 1.0f; // 幸运值, 1.0代表100% (基础值)

    [Header("护盾属性")]
    public int maxShield = 0;
    public float shieldCooldown = 5f; // 基础冷却时间5秒


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 如果需要跨场景，可以在这里 DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 【核心新方法】应用一个具体的升级效果
    /// </summary>
    public void ApplyEffect(UpgradeEffect effect)
    {
        if (effect == null || effect.actionType != EffectActionType.ModifyStat) return;

        Debug.Log($"应用效果: 类型:{effect.statToModify}, 数值:{effect.value}, 方式:{effect.modType}");

        // 【核心修改】在应用百分比效果前，先将数值转换为小数
        float valueToApply = effect.value;
        if (effect.modType == ModifierType.Percentage)
        {
            valueToApply /= 100f; // 例如，15 会变成 0.15
        }

        switch (effect.statToModify)
        {
            case UpgradeType.WeaponDamage:
                if (effect.modType == ModifierType.Flat)
                    flatDamageBonus += valueToApply;
                else
                    damageMultiplier += valueToApply;
                break;

            case UpgradeType.AoeDamage:
                if (effect.modType == ModifierType.Flat)
                    flatAoeDamageBonus += valueToApply; // (假设您未来会添加 flatAoeDamageBonus 变量)
                else
                    aoeDamageMultiplier += valueToApply;
                break;

            case UpgradeType.AoeRadius:
                // 半径通常也是百分比
                aoeRadiusMultiplier += valueToApply;
                break;

            case UpgradeType.WeaponFireRate:
                // 射速是减法
                fireRateMultiplier -= valueToApply;
                if (fireRateMultiplier < 0.1f) fireRateMultiplier = 0.1f;
                break;

            case UpgradeType.PierceCount:
                // 穿透是固定值
                bonusPierceCount += (int)valueToApply;
                break;

            // ... 为其他所有属性应用 valueToApply ...
            case UpgradeType.MaxHealth:
                bonusMaxHealth += (int)valueToApply;
                GetComponent<Health>()?.AddMaxHealth((int)valueToApply);
                break;
            case UpgradeType.MaxShield:
                int shieldToAdd = (int)effect.value;
                maxShield += shieldToAdd;
                // 通知 PlayerShield 组件
                PlayerShield.Instance?.AddMaxShield(shieldToAdd);
                break;

            case UpgradeType.ShieldCooldown:
                // 冷却是减法
                shieldCooldown -= effect.value;
                if (shieldCooldown < 1f) shieldCooldown = 1f; // 设置一个最小冷却时间
                break;

            case UpgradeType.MoveSpeed:
                moveSpeedMultiplier += valueToApply;
                break;
        }
    }
}