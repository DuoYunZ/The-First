using UnityEngine;
using System.Collections.Generic;

public class MetaUpgradeManager : MonoBehaviour
{
    public static MetaUpgradeManager Instance;

    [Header("配置")]
    public List<MetaUpgradeSO> allUpgrades; // 把做好的 SO 拖进去

    [Header("存档键前缀")]
    public string saveKeyPrefix = "MetaUpgrade_";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景存在
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 游戏开始时，应用所有已购买的属性
        ApplyAllUpgrades();
    }

    // --- 核心功能：应用属性到 PlayerStats ---
    public void ApplyAllUpgrades()
    {
        // 确保 PlayerStats 存在
        if (PlayerStats.Instance == null) return;

        foreach (var upgrade in allUpgrades)
        {
            int level = GetLevel(upgrade);
            if (level > 0)
            {
                float bonus = upgrade.GetTotalBonus(level);
                ApplyStatToPlayer(upgrade.statType, bonus);
            }
        }
    }

    private void ApplyStatToPlayer(UpgradeType type, float value)
    {
        PlayerStats stats = PlayerStats.Instance;
        switch (type)
        {
            case UpgradeType.MaxHealth:
                stats.bonusMaxHealth += (int)value;
                break;
            case UpgradeType.WeaponDamage:
                // 修改基础值，防止RecalculateStats丢失
                stats._baseDamageMultiplier += value;
                stats.damageMultiplier += value;
                break;
            case UpgradeType.CritRate:
                stats._baseCritRate += value;
                stats.critRate += value;
                break;
            case UpgradeType.Armor:
                stats.armor += (int)value;
                break;
            case UpgradeType.MoveSpeed:
                stats._baseMoveSpeedMultiplier += value;
                stats.moveSpeedMultiplier += value;
                break;
                // 如果你有其他属性（比如复活次数），在这里加 case
        }
    }

    // --- 购买逻辑 ---
    public bool TryPurchaseUpgrade(MetaUpgradeSO upgrade)
    {
        int currentLevel = GetLevel(upgrade);
        if (currentLevel >= upgrade.maxLevel) return false;

        int cost = upgrade.GetCost(currentLevel);

        int playerGold = GetPlayerGold();

        if (playerGold >= cost)
        {
            DeductPlayerGold(cost);
            SetLevel(upgrade, currentLevel + 1);

            // 购买后立即生效
            // 注意：这里我们只加“增量”，比如从Lv1升Lv2，只加那一级的差值？
            // 或者更简单的做法：我们不用增量，而是重新覆盖应用。
            // 但因为 ApplyAllUpgrades 是累加的，这里为了安全，
            // 我们只把“刚刚升的那一级”增加的数值加上去。
            ApplyStatToPlayer(upgrade.statType, upgrade.valuePerLevel);

            return true;
        }

        return false;
    }

    // --- 存取接口 ---
    public int GetLevel(MetaUpgradeSO upgrade)
    {
        return PlayerPrefs.GetInt(saveKeyPrefix + upgrade.name, 0);
    }

    private void SetLevel(MetaUpgradeSO upgrade, int level)
    {
        PlayerPrefs.SetInt(saveKeyPrefix + upgrade.name, level);
        PlayerPrefs.Save();
    }

    // ==========================================
    // 【修正】金币接口适配 -> 连接 PlayerProgressManager
    // ==========================================
    public int GetPlayerGold()
    {
        if (PlayerProgressManager.Instance != null)
        {
            return PlayerProgressManager.Instance.currentGold;
        }
        return 0;
    }

    private void DeductPlayerGold(int amount)
    {
        if (PlayerProgressManager.Instance != null)
        {
            // 1. 扣钱并更新UI
            PlayerProgressManager.Instance.SpendGold(amount);

            // 2. 【重要】保存进度，防止扣了钱没存档
            PlayerProgressManager.Instance.SaveGame();
        }
    }
}