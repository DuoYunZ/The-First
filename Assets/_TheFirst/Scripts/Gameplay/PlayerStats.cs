// PlayerStats.cs (升级版)
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Runtime State")]
    [Tooltip("当此值为true时，玩家不会受到伤害")]
    public bool isInvincible = false; // <--- ADD THIS LINE

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
    public int bonusSlashCount = 0; // <--- ADD THIS LINE
    public int bonusOrbitalCount = 0; // <--- 【新增】额外轨道武器数量

    [Header("武器特效加成")]
    [Tooltip("抛物线AOE造成眩晕的几率 (0 到 1)")]
    public float parabolicAoeStunChance = 0f;

    [Header("特殊属性")]
    public float luck = 1.0f; // 幸运值, 1.0代表100% (基础值)

    [Header("护盾属性")]
    public int maxShield = 0;
    public float shieldCooldown = 5f; // 基础冷却时间5秒

    [Header("回旋镖叠加状态 (Boomerang Stacking)")]
    [Tooltip("当前连续接住次数")]
    public int boomerangCatchStacks = 0;
    [Tooltip("当前等级允许的最大叠加次数")]
    public int boomerangMaxCatchStacks = 0;
    [Tooltip("当前等级下，每次叠加增加的伤害百分比 (0.1 = 10%)")]
    public float boomerangStackDamageBonusPercent = 0f;
    [Tooltip("当前等级下，每次叠加增加的体积百分比 (0.1 = 10%)")]
    public float boomerangStackScaleBonusPercent = 0f;

    [Header("能量石计数 (运行时)")]
    [Tooltip("追踪玩家当前装备的所有能量石类型和数量")]
    public Dictionary<EnergyStoneEffectType, int> ActiveStoneCounts = new Dictionary<EnergyStoneEffectType, int>();

    [Header("雷电石计数器 (运行时)")]
    public int lightningSmiteCounter = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return; // 【新增】如果不是单例，则不执行后续Awake代码
        }

        // 【修改】在角色初始化时，应用所有永久性加成
        ApplyPermanentUpgrades();
    }

    private void ApplyPermanentUpgrades()
    {
        // 确保 PlayerProgressManager 已经存在
        if (PlayerProgressManager.Instance == null)
        {
            Debug.LogWarning("PlayerProgressManager 未找到，无法应用永久升级。这在游戏初次启动时是正常的。");
            return;
        }

        Debug.Log("<color=cyan>正在从 PlayerProgressManager 应用永久属性加成...</color>");

        // 将 PlayerProgressManager 中存储的永久加成，直接加到 PlayerStats 的对应属性上
        // 这些将成为本局游戏的“初始值”
        this.flatDamageBonus += PlayerProgressManager.Instance.permanentFlatDamageBonus;
        this.damageMultiplier += PlayerProgressManager.Instance.permanentDamagePercentBonus;

        // 射速是减法，所以我们要用乘法来应用（例如 1 * (1 - 0.1)）
        // 但为了简化，我们暂时也用加法，但记得在计算最终射速时处理
        // 更准确的做法是修改 fireRateMultiplier 的逻辑，但我们先保持简单
        this.fireRateMultiplier -= PlayerProgressManager.Instance.permanentFireRateBonus;


        Debug.Log($"应用后 -> 初始固定伤害: {this.flatDamageBonus}, 初始伤害乘数: {this.damageMultiplier}, 初始射速乘数: {this.fireRateMultiplier}");
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

            case UpgradeType.ParabolicAoeStunChance:
                if (effect.modType == ModifierType.Percentage) // 假设几率是百分比
                {
                    parabolicAoeStunChance += effect.value; // 例如 value = 0.1 (代表+10%)
                }
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

            case UpgradeType.SlashCount:
                // Slash count is always a flat bonus (e.g., +1 slash)
                bonusSlashCount += (int)effect.value;
                break;
            case UpgradeType.OrbitalCount:
                // 轨道武器数量也是固定值增加
                bonusOrbitalCount += (int)effect.value;
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
            case UpgradeType.BoomerangStackUpgrade:
                // effect.value 在这里代表目标等级 (例如 2, 3, 4)
                int targetLevel = Mathf.RoundToInt(effect.value);
                Debug.Log($"应用回旋镖叠加升级，目标等级: {targetLevel}");

                switch (targetLevel)
                {
                    case 2:
                        boomerangMaxCatchStacks = 3;
                        boomerangStackDamageBonusPercent = 0.10f; // 10%
                        boomerangStackScaleBonusPercent = 0.10f;  // 10%
                        break;
                    case 3:
                        boomerangMaxCatchStacks = 4;
                        boomerangStackDamageBonusPercent = 0.25f; // 25%
                        boomerangStackScaleBonusPercent = 0.25f;  // 25%
                        break;
                    case 4:
                        boomerangMaxCatchStacks = 5;
                        boomerangStackDamageBonusPercent = 0.60f; // 60%
                        boomerangStackScaleBonusPercent = 0.60f;  // 60%
                        break;
                    // 可以添加更多等级...
                    default:
                        Debug.LogWarning($"收到未知的回旋镖叠加等级: {targetLevel}");
                        // 可以选择保持上一个等级的设置或重置
                        // boomerangMaxCatchStacks = 0; // 例如重置
                        break;
                }
                // 【重要】升级规则时，重置当前层数
                boomerangCatchStacks = 0;
                Debug.Log($"回旋镖叠加规则更新: MaxStacks={boomerangMaxCatchStacks}, DmgBonus={boomerangStackDamageBonusPercent * 100}%, ScaleBonus={boomerangStackScaleBonusPercent * 100}%");
                break;
        }
    }
    public void RegisterStone(EnergyStoneSO newStone, EnergyStoneSO oldStone)
    {
        // 1. 注销旧石头
        if (oldStone != null)
        {
            UpdateStoneCount(oldStone, -1);
        }

        // 2. 注册新石头
        if (newStone != null)
        {
            UpdateStoneCount(newStone, 1);
        }
    }
    private void UpdateStoneCount(EnergyStoneSO stone, int delta)
    {
        // 这是一个健壮的实现，它会检查石头上的 *所有* 布尔值
        // (我们使用 EnergyStoneEffectType 作为 Key)

        if (stone.stoneEffects == null) return; //

        foreach (EnergyStoneEffectType effectType in stone.stoneEffects) //
        {
            AddToCount(effectType, delta); //
        }

        if (stone.applyChain) AddToCount(EnergyStoneEffectType.ApplyChain, delta);
        // ... (在这里添加 AddHoming, AddPierce 等...)
    }

    private void AddToCount(EnergyStoneEffectType type, int amount)
    {
        if (!ActiveStoneCounts.ContainsKey(type))
        {
            ActiveStoneCounts[type] = 0;
        }
        ActiveStoneCounts[type] += amount;
    }

    /// <summary>
    /// (由 Projectile.cs 或 WeaponPart.cs 调用)
    /// 获取当前装备了多少颗指定类型的石头
    /// </summary>
    public int GetStoneCount(EnergyStoneEffectType type)
    {
        if (ActiveStoneCounts.TryGetValue(type, out int count))
        {
            return count;
        }
        return 0;
    }
}