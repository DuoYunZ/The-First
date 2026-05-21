using UnityEngine;

[CreateAssetMenu(fileName = "NewPassiveItem", menuName = "Gameplay/Passive Item Data")]
public class PassiveItemData : ScriptableObject
{
    public const int PassiveCapstoneLevel = 3;

    [Header("UI 信息")]
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("属性影响")]
    [Tooltip("该道具影响哪项属性")]
    public UpgradeType statType;

    [Tooltip("每升一级增加的数值 (百分比请用小数，如 0.1 代表 10%)")]
    public float valuePerLevel;

    [Header("等级限制")]
    public int maxLevel = PassiveCapstoneLevel;

    public int EffectiveMaxLevel => Mathf.Max(1, Mathf.Min(maxLevel, PassiveCapstoneLevel));

    [Header("触发型被动（可选）")]
    [Tooltip("是否为触发型被动（如燃烧轨迹、击杀回血等），触发型被动由 PassiveEffectManager 管理运行时逻辑")]
    public bool isTriggerPassive = false;

    [Tooltip("触发型被动使用的预制件（如火焰区域、雷击AOE冲击波等）")]
    public GameObject triggerVfxPrefab;

    [Header("前置条件（可选）")]
    [Tooltip("需要拥有指定武器才能出现在卡池中 (留空 = 无限制)")]
    public WeaponStatBlock requiredWeapon;

    [Header("图鉴解锁条件")]
    [Tooltip("默认是否已在图鉴中解锁 (属性类被动建议勾选)")]
    public bool isDefaultUnlocked = true;

    [Tooltip("解锁所需的统计键值 (留空则只依赖 isDefaultUnlocked 或 unlockedItems)")]
    public string unlockStatKey = "";

    [Tooltip("解锁所需的目标数量")]
    public int unlockThreshold = 0;

    [TextArea]
    [Tooltip("未解锁时在图鉴中显示的提示文本")]
    public string lockedDescription = "在游戏中获取此道具以解锁";

    /// <summary>
    /// 获取指定等级的描述文本
    /// </summary>
    public string GetDescription(int level)
    {
        // 判断是否为百分比类型
        bool isPercent = statType != UpgradeType.MaxHealth
                      && statType != UpgradeType.Armor
                      && statType != UpgradeType.PierceCount
                      && statType != UpgradeType.Revival
                      && statType != UpgradeType.KillHeal;

        float val = valuePerLevel * level;
        string valStr = isPercent ? $"{val * 100}%" : $"{val}";
        return $"{description} (+{valStr})";
    }

    public string GetMilestoneUnlockDescription(int currentLevel, int nextLevel)
    {
        int effectiveMaxLevel = EffectiveMaxLevel;
        int cappedNextLevel = Mathf.Clamp(nextLevel, 0, effectiveMaxLevel);
        if (cappedNextLevel <= currentLevel) return "";

        if (currentLevel < effectiveMaxLevel && cappedNextLevel >= effectiveMaxLevel)
        {
            string maxDescription = GetMilestoneDescriptionForLevel(effectiveMaxLevel, true);
            if (!string.IsNullOrEmpty(maxDescription)) return maxDescription;
        }

        if (currentLevel < 3 && cappedNextLevel >= 3)
        {
            return GetMilestoneDescriptionForLevel(3, false);
        }

        return "";
    }

    private string GetMilestoneDescriptionForLevel(int milestoneLevel, bool isMaxLevel)
    {
        string prefix = isMaxLevel ? $"{milestoneLevel}级红宝石" : $"{milestoneLevel}级节点";

        switch (statType)
        {
            case UpgradeType.WeaponDamage:
                return isMaxLevel ? $"{prefix}: 暴击伤害 +20%" : $"{prefix}: 暴击率 +5%";
            case UpgradeType.WeaponFireRate:
                return isMaxLevel ? $"{prefix}: 子弹数量 +1" : $"{prefix}: 弹道速度 +15%";
            case UpgradeType.AoeRadius:
                return isMaxLevel ? $"{prefix}: 抛物线爆炸眩晕 +12%" : $"{prefix}: 范围伤害 +10%";
            case UpgradeType.WeaponDuration:
                return isMaxLevel ? $"{prefix}: 冷却额外缩短 8%" : $"{prefix}: 环绕/部署数量 +1";
            case UpgradeType.PierceCount:
                return isMaxLevel ? $"{prefix}: 子弹数量 +1" : "";
            case UpgradeType.PickupRadius:
                return isMaxLevel ? $"{prefix}: 移动速度 +8%" : $"{prefix}: 经验获取 +8%";
            case UpgradeType.MoveSpeed:
                return isMaxLevel ? $"{prefix}: 冲刺结束释放冲击波" : $"{prefix}: 拾取范围 +15%";
            case UpgradeType.MaxHealth:
                return isMaxLevel ? $"{prefix}: 每100击杀恢复2HP" : $"{prefix}: 护甲 +1";
            case UpgradeType.Armor:
                return isMaxLevel ? $"{prefix}: 最大生命 +25" : $"{prefix}: 护甲 +1";
            case UpgradeType.Luck:
                return isMaxLevel ? $"{prefix}: 暴击伤害 +25%" : $"{prefix}: 暴击率 +3%";
            case UpgradeType.ExperienceGain:
                return isMaxLevel ? $"{prefix}: 幸运 +8%" : $"{prefix}: 拾取范围 +15%";
        }

        return "";
    }
}
