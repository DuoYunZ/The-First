using UnityEngine;

[CreateAssetMenu(fileName = "NewPassiveItem", menuName = "Gameplay/Passive Item Data")]
public class PassiveItemData : ScriptableObject
{
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
    public int maxLevel = 5;

    [Header("触发型被动（可选）")]
    [Tooltip("是否为触发型被动（如燃烧轨迹、击杀回血等），触发型被动由 PassiveEffectManager 管理运行时逻辑")]
    public bool isTriggerPassive = false;

    [Tooltip("触发型被动使用的预制件（如火焰区域、雷击AOE冲击波等）")]
    public GameObject triggerVfxPrefab;

    [Header("前置条件（可选）")]
    [Tooltip("需要拥有指定武器才能出现在卡池中 (留空 = 无限制)")]
    public WeaponStatBlock requiredWeapon;

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
}