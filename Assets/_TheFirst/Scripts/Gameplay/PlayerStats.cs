using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RuntimePassiveItem
{
    public PassiveItemData data;
    public int currentLevel;
}

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Runtime State")]
    public bool isInvincible = false;

    [Header("Inventory")]
    public List<RuntimePassiveItem> activePassiveItems = new List<RuntimePassiveItem>();

    [Header("Final Stats (Read Only)")]
    public float damageMultiplier = 1f;
    public float aoeDamageMultiplier = 1f;
    public float aoeRadiusMultiplier = 1f;
    public float fireRateMultiplier = 1f;
    public float projectileSpeedMultiplier = 1f;
    public float pickupRadiusMultiplier = 1f;
    public float moveSpeedMultiplier = 1f;
    public float durationMultiplier = 1f; // 【新增】持续时间/生命周期倍率
    public int bonusProjectileCount = 0;
    [Header("暴击属性")]
    public float critRate = 0.05f;   // 基础暴击率 5%
    public float critDamage = 1.5f;  // 基础暴击伤害 150%

    [Header("生存属性")]
    public int revivalCount = 0; // 【新增】复活次数

    public float flatDamageBonus = 0f;
    public float flatAoeDamageBonus = 0f;
    public int bonusPierceCount = 0;
    public int bonusMaxHealth = 0;
    public int bonusSlashCount = 0;   // 刀光数量
    public int bonusOrbitalCount = 0;
    public float armor = 0f;

    // --- 【新增】基础值备份 (用于重算) ---
    private float _baseDamageMultiplier;
    private float _baseAoeDamageMultiplier;
    private float _baseAoeRadiusMultiplier;
    private float _baseFireRateMultiplier;
    private float _baseProjectileSpeedMultiplier;
    private float _basePickupRadiusMultiplier;
    private float _baseMoveSpeedMultiplier;
    private float _baseDurationMultiplier; // 【新增】
    private float _baseFlatDamage;
    private int _baseMaxHealth;
    private float _baseCritRate;
    private float _baseCritDamage;

    // 【核心修复 1】为整数属性添加备份变量
    private int _baseBonusPierceCount;
    private int _baseBonusSlashCount;
    private int _baseBonusOrbitalCount;
    private int _baseBonusProjectileCount;

    [Header("武器特效加成")]
    public float parabolicAoeStunChance = 0f;

    [Header("特殊属性")]
    public float luck = 1.0f;

    [Header("护盾属性")]
    public int maxShield = 0;
    public float shieldCooldown = 5f;

    [Header("回旋镖叠加状态")]
    public int boomerangCatchStacks = 0;
    public int boomerangMaxCatchStacks = 0;
    public float boomerangStackDamageBonusPercent = 0f;
    public float boomerangStackScaleBonusPercent = 0f;

    [Header("能量石计数")]
    public Dictionary<EnergyStoneEffectType, int> ActiveStoneCounts = new Dictionary<EnergyStoneEffectType, int>();

    [Header("雷电石计数器")]
    public int lightningSmiteCounter = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        InitializeBaseStats();
        ApplyPermanentUpgrades();
        RecalculateStats();
    }

    private void InitializeBaseStats()
    {
        _baseDamageMultiplier = damageMultiplier;
        _baseAoeDamageMultiplier = aoeDamageMultiplier;
        _baseAoeRadiusMultiplier = aoeRadiusMultiplier;
        _baseFireRateMultiplier = fireRateMultiplier;
        _baseProjectileSpeedMultiplier = projectileSpeedMultiplier;
        _basePickupRadiusMultiplier = pickupRadiusMultiplier;
        _baseMoveSpeedMultiplier = moveSpeedMultiplier;
        _baseFlatDamage = flatDamageBonus;
        _baseMaxHealth = 0;

        // 【核心修复 2】初始化这些备份变量
        _baseBonusPierceCount = bonusPierceCount;
        _baseBonusSlashCount = bonusSlashCount;
        _baseBonusOrbitalCount = bonusOrbitalCount;
        _baseBonusProjectileCount = bonusProjectileCount;
        _baseDurationMultiplier = 1f;
        _baseCritRate = critRate;
        _baseCritDamage = critDamage;
    }

    private void ApplyPermanentUpgrades()
    {
        if (PlayerProgressManager.Instance == null) return;

        Debug.Log("<color=cyan>正在从 PlayerProgressManager 应用永久属性加成...</color>");

        _baseFlatDamage += PlayerProgressManager.Instance.permanentFlatDamageBonus;
        _baseDamageMultiplier += PlayerProgressManager.Instance.permanentDamagePercentBonus;
        _baseFireRateMultiplier -= PlayerProgressManager.Instance.permanentFireRateBonus;
    }

    public void EquipOrUpgradePassiveItem(PassiveItemData itemData)
    {
        if (itemData == null) return;
        RuntimePassiveItem existingItem = activePassiveItems.Find(x => x.data == itemData);

        if (existingItem != null)
        {
            if (existingItem.currentLevel < itemData.maxLevel)
            {
                existingItem.currentLevel++;
                Debug.Log($"升级被动道具: {itemData.itemName} -> Lv.{existingItem.currentLevel}");
            }
        }
        else
        {
            if (activePassiveItems.Count < 6)
            {
                activePassiveItems.Add(new RuntimePassiveItem { data = itemData, currentLevel = 1 });
                Debug.Log($"获得被动道具: {itemData.itemName}");
            }
            else
            {
                Debug.LogWarning("被动道具栏已满！");
                return;
            }
        }
        RecalculateStats();
    }

    private void RecalculateStats()
    {
        // 1. 重置为基础值 (使用备份变量)
        damageMultiplier = _baseDamageMultiplier;
        aoeDamageMultiplier = _baseAoeDamageMultiplier;
        aoeRadiusMultiplier = _baseAoeRadiusMultiplier;
        fireRateMultiplier = _baseFireRateMultiplier;
        projectileSpeedMultiplier = _baseProjectileSpeedMultiplier;
        pickupRadiusMultiplier = _basePickupRadiusMultiplier;
        moveSpeedMultiplier = _baseMoveSpeedMultiplier;
        flatDamageBonus = _baseFlatDamage;
        bonusMaxHealth = _baseMaxHealth;
        durationMultiplier = _baseDurationMultiplier;

        // 【核心修复 3】不再重置为 0，而是重置为备份值
        bonusPierceCount = _baseBonusPierceCount;
        bonusSlashCount = _baseBonusSlashCount;
        bonusOrbitalCount = _baseBonusOrbitalCount;
        bonusProjectileCount = _baseBonusProjectileCount;
        critRate = _baseCritRate;
        critDamage = _baseCritDamage;

        armor = 0; // 如果你也想通过升级卡加护甲，记得也给它加个 _baseArmor

        // 2. 遍历所有被动道具并叠加
        foreach (var item in activePassiveItems)
        {
            ApplyPassiveItemStat(item.data, item.currentLevel);
        }

        // 3. 通知其他组件
        UpdateDependentComponents();

        Debug.Log($"属性重算完成。当前额外刀光: {bonusSlashCount}");
        Debug.Log($"属性重算完成。持续时间倍率: {durationMultiplier}");
    }

    private void ApplyPassiveItemStat(PassiveItemData data, int level)
    {
        float totalValue = data.valuePerLevel * level;

        switch (data.statType)
        {
            case UpgradeType.WeaponDamage: damageMultiplier += totalValue; break;
            case UpgradeType.AoeDamage: aoeDamageMultiplier += totalValue; break;
            case UpgradeType.AoeRadius: aoeRadiusMultiplier += totalValue; break;
            case UpgradeType.WeaponFireRate:
                fireRateMultiplier -= totalValue;
                if (fireRateMultiplier < 0.2f) fireRateMultiplier = 0.2f;
                break;
            case UpgradeType.MoveSpeed: moveSpeedMultiplier += totalValue; break;
            case UpgradeType.PickupRadius: pickupRadiusMultiplier += totalValue; break;
            case UpgradeType.MaxHealth: bonusMaxHealth += Mathf.RoundToInt(totalValue); break;
            case UpgradeType.Armor: armor += totalValue; break;
            case UpgradeType.PierceCount: bonusPierceCount += Mathf.RoundToInt(totalValue); break;
            case UpgradeType.Luck: luck = 1.0f + totalValue; break;
            case UpgradeType.WeaponDuration:
                durationMultiplier += totalValue;
                break;
            // 如果你想让被动道具也能加刀光，在这里加 case UpgradeType.SlashCount
            case UpgradeType.CritRate: critRate += totalValue; break;
            case UpgradeType.CritDamage: critDamage += totalValue; break;
            case UpgradeType.Revival:
                revivalCount += Mathf.RoundToInt(totalValue);
                break;
        }
    }

    private void UpdateDependentComponents()
    {
        var healthComp = GetComponent<Health>();
        if (healthComp != null)
        {
            healthComp.SetBonusMaxHealth(bonusMaxHealth);
        }
    }

    public void ApplyEffect(UpgradeEffect effect)
    {
        if (effect == null) return;

        if (effect.passiveItemData != null)
        {
            EquipOrUpgradePassiveItem(effect.passiveItemData);
            return;
        }

        if (effect.actionType == EffectActionType.ModifyStat)
        {
            if (effect.statToModify == UpgradeType.MaxShield)
            {
                maxShield += (int)effect.value;
                PlayerShield.Instance?.AddMaxShield((int)effect.value);
            }
            else if (effect.statToModify == UpgradeType.BoomerangStackUpgrade)
            {
                int targetLevel = Mathf.RoundToInt(effect.value);
                switch (targetLevel)
                {
                    case 2:
                        boomerangMaxCatchStacks = 3;
                        boomerangStackDamageBonusPercent = 0.10f;
                        boomerangStackScaleBonusPercent = 0.10f;
                        break;
                    case 3:
                        boomerangMaxCatchStacks = 4;
                        boomerangStackDamageBonusPercent = 0.25f;
                        boomerangStackScaleBonusPercent = 0.25f;
                        break;
                    case 4:
                        boomerangMaxCatchStacks = 5;
                        boomerangStackDamageBonusPercent = 0.60f;
                        boomerangStackScaleBonusPercent = 0.60f;
                        break;
                }
                boomerangCatchStacks = 0;
                Debug.Log($"回旋镖规则更新到 Level {targetLevel}");
            }
            else
            {
                ApplyLegacyEffectToBase(effect);
                RecalculateStats();
            }
        }
        else if (effect.actionType == EffectActionType.UnlockShield)
        {
            if (PlayerShield.Instance != null && effect.shieldToUnlock != null)
            {
                PlayerShield.Instance.EquipShield(effect.shieldToUnlock);
            }
        }
    }

    private void ApplyLegacyEffectToBase(UpgradeEffect effect)
    {
        float val = effect.value;
        bool isIntProperty = effect.statToModify == UpgradeType.PierceCount ||
                             effect.statToModify == UpgradeType.AddProjectile ||
                             effect.statToModify == UpgradeType.SlashCount ||
                             effect.statToModify == UpgradeType.OrbitalCount ||
                             effect.statToModify == UpgradeType.MaxHealth;

        if (effect.modType == ModifierType.Percentage && !isIntProperty)
        {
            val /= 100f;
        }

        switch (effect.statToModify)
        {
            case UpgradeType.WeaponDamage: _baseDamageMultiplier += val; break;
            case UpgradeType.AoeDamage: _baseAoeDamageMultiplier += val; break;
            case UpgradeType.AoeRadius: _baseAoeRadiusMultiplier += val; break;
            case UpgradeType.WeaponFireRate: _baseFireRateMultiplier -= val; break;
            case UpgradeType.WeaponProjectileSpeed: _baseProjectileSpeedMultiplier += val; break;
            case UpgradeType.PickupRadius: _basePickupRadiusMultiplier += val; break;
            case UpgradeType.MoveSpeed: _baseMoveSpeedMultiplier += val; break;

            case UpgradeType.MaxHealth: _baseMaxHealth += (int)val; break;
            case UpgradeType.Armor: armor += val; break;

            // 【核心修复 4】修改 _base 变量，而不是直接改 public 变量
            case UpgradeType.PierceCount: _baseBonusPierceCount += (int)val; break;
            case UpgradeType.SlashCount: _baseBonusSlashCount += (int)val; break;
            case UpgradeType.OrbitalCount: _baseBonusOrbitalCount += (int)val; break;
            case UpgradeType.AddProjectile: _baseBonusProjectileCount += (int)val; break;

            case UpgradeType.ParabolicAoeStunChance: parabolicAoeStunChance += val; break;
            case UpgradeType.WeaponDuration:
                _baseDurationMultiplier += val;
                break;
            case UpgradeType.CritRate:
                // 修正：直接加 val，因为上面已经处理过 /100 了
                _baseCritRate += val;
                break;
            case UpgradeType.CritDamage:
                // 修正：直接加 val
                _baseCritDamage += val;
                break;
        }
    }

    public int GetStoneCount(EnergyStoneEffectType type)
    {
        if (ActiveStoneCounts.TryGetValue(type, out int count)) return count;
        return 0;
    }
    public void RegisterStone(EnergyStoneSO newStone, EnergyStoneSO oldStone)
    {
        if (oldStone != null) UpdateStoneCount(oldStone, -1);
        if (newStone != null) UpdateStoneCount(newStone, 1);
    }
    private void UpdateStoneCount(EnergyStoneSO stone, int delta)
    {
        if (stone.stoneEffects == null) return;
        foreach (EnergyStoneEffectType effectType in stone.stoneEffects) AddToCount(effectType, delta);
        if (stone.applyChain) AddToCount(EnergyStoneEffectType.ApplyChain, delta);
    }

    private void AddToCount(EnergyStoneEffectType type, int amount)
    {
        if (!ActiveStoneCounts.ContainsKey(type)) ActiveStoneCounts[type] = 0;
        ActiveStoneCounts[type] += amount;
    }
}