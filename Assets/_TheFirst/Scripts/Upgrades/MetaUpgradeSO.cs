using UnityEngine;

[CreateAssetMenu(menuName = "Meta Game/Upgrade Definition")]
public class MetaUpgradeSO : ScriptableObject
{
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("费用设置")]
    public int baseCost = 100;
    public float costMultiplier = 1.5f; // 每级涨价倍率

    [Header("属性设置")]
    public UpgradeType statType; // 复用你现有的枚举
    public float valuePerLevel = 10f; // 每级增加多少数值
    public int maxLevel = 10;

    // 计算当前等级的费用
    public int GetCost(int currentLevel)
    {
        // 简单公式：基础 * (倍率 ^ 等级)
        return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, currentLevel));
    }

    // 计算当前等级的总加成
    public float GetTotalBonus(int currentLevel)
    {
        return valuePerLevel * currentLevel;
    }
}