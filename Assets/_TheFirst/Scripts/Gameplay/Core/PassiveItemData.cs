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

    public string GetDescription(int level)
    {
        // 简单格式化描述，例如 "伤害 +10%"
        bool isPercent = statType != UpgradeType.MaxHealth && statType != UpgradeType.Armor && statType != UpgradeType.PierceCount && statType != UpgradeType.Revival;
        float val = valuePerLevel * level;

        string valStr = isPercent ? $"{val * 100}%" : $"{val}";
        return $"{description} (+{valStr})";
    }
}