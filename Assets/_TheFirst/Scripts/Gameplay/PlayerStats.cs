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

    [Header("角色技能树属性")]
    public float cooldownReduction = 0f;     // 全局冷却缩减（如 0.1 = 减少10%冷却）
    public float energyGainMultiplier = 1f;  // 能量获取倍率
    public float lifeStealPercent = 0f;      // 伤害吸血百分比

    [Header("临时Buff（不参与RecalculateStats重置）")]
    [Tooltip("协程类临时移速加成，独立于moveSpeedMultiplier，读取时叠加")]
    public float tempMoveSpeedBonus = 0f;
    [Tooltip("协程类临时攻速加成")]
    public float tempFireRateBonus = 0f;
    [Tooltip("协程类临时暴击加成")]
    public float tempCritRateBonus = 0f;
    [Tooltip("协程类临时范围加成")]
    public float tempAoeRadiusBonus = 0f;
    [Tooltip("协程类临时吸血加成")]
    public float tempLifeStealBonus = 0f;

    [Header("触发型被动属性")]
    public float berserkerDamagePerLevel = 0f;  // 狂战士之心：每级的增伤比例（实际增伤在RecalcStats中根据血量动态计算）
    public int berserkerLevel = 0;              // 狂战士之心等级
    public float thornsReflectPercent = 0f;     // 荆棘护甲反弹比例
    public int killHealAmount = 0;              // 击杀回血值
    public float globalFreezeChance = 0f;       // 全局冰冻概率
    public float thunderWillChance = 0f;        // 雷霆意志触发概率
    public float thunderWillDamageBonus = 0f;   // 雷霆意志伤害加成
    public int flameTrailLevel = 0;             // 燃烧轨迹等级
    public int dashExplosionLevel = 0;          // 冲刺余烬等级
    public float experienceGainMultiplier = 1f; // 经验获取倍率

    // --- 【新增】基础值备份 (用于重算) ---
    internal float _baseDamageMultiplier;
    private float _baseAoeDamageMultiplier;
    private float _baseAoeRadiusMultiplier;
    private float _baseFireRateMultiplier;
    private float _baseProjectileSpeedMultiplier;
    private float _basePickupRadiusMultiplier;
    internal float _baseMoveSpeedMultiplier;
    private float _baseDurationMultiplier; // 【新增】
    private float _baseFlatDamage;
    private int _baseMaxHealth;
    internal float _baseCritRate;
    private float _baseCritDamage;

    // 【核心修复 1】为整数属性添加备份变量
    private int _baseBonusPierceCount;
    private int _baseBonusSlashCount;
    private int _baseBonusOrbitalCount;
    private int _baseBonusProjectileCount;

    // 触发型被动备份
    private float _baseThornsReflect;
    private int _baseKillHeal;
    private float _baseGlobalFreezeChance;
    private float _baseThunderWillChance;
    private float _baseThunderWillDmgBonus;
    private float _baseLifeStealPercent_fromPassive;
    private float _baseExperienceGainMultiplier;

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

    void OnDestroy()
    {
        // 角色切换时，旧角色被销毁，需要清除单例引用
        // 否则新角色 Instantiate 时 Awake() 检测到 Instance 仍存在，会自毁
        if (Instance == this) Instance = null;
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
        _baseCooldownReduction = 0f;
        _baseEnergyGainMultiplier = 1f;
        _baseLifeStealPercent = 0f;

        // 触发型被动初始化
        _baseThornsReflect = 0f;
        _baseKillHeal = 0;
        _baseGlobalFreezeChance = 0f;
        _baseThunderWillChance = 0f;
        _baseThunderWillDmgBonus = 0f;
        _baseLifeStealPercent_fromPassive = 0f;
        _baseExperienceGainMultiplier = 1f;
    }

    // 角色技能树属性备份
    private float _baseCooldownReduction;
    private float _baseEnergyGainMultiplier;
    private float _baseLifeStealPercent;

    private void ApplyPermanentUpgrades()
    {
        if (PlayerProgressManager.Instance == null) return;
        var ppm = PlayerProgressManager.Instance;

        // --- 武器技能树属性 ---
        _baseFlatDamage += ppm.permanentFlatDamageBonus;
        _baseDamageMultiplier += ppm.permanentDamagePercentBonus;
        _baseFireRateMultiplier -= ppm.permanentFireRateBonus;

        // --- 角色技能树属性 ---
        _baseDamageMultiplier += ppm.permanentCharDamagePercentBonus; // 角色技能树的攻击力加成
        _baseMaxHealth += ppm.permanentMaxHealthBonus;
        armor += ppm.permanentArmorBonus;
        _baseMoveSpeedMultiplier += ppm.permanentMoveSpeedBonus;
        // 冷却缩减直接作用于开火间隔（如 0.1 = 开火间隔变为90%）
        _baseCooldownReduction += ppm.permanentCooldownReduction;
        _baseFireRateMultiplier *= (1f - _baseCooldownReduction);
        _baseEnergyGainMultiplier += ppm.permanentEnergyGainBonus;
        _baseLifeStealPercent += ppm.permanentLifeStealPercent;

        Debug.Log($"<color=cyan>[PlayerStats] 永久加成来源：" +
            $"武器树DmgPct={ppm.permanentDamagePercentBonus:F2}, " +
            $"角色树DmgPct={ppm.permanentCharDamagePercentBonus:F2}, " +
            $"最终_baseDamageMultiplier={_baseDamageMultiplier:F2}</color>");
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
            }
        }
        else
        {
            if (activePassiveItems.Count < 6)
            {
                activePassiveItems.Add(new RuntimePassiveItem { data = itemData, currentLevel = 1 });
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

        armor = 0;
        cooldownReduction = _baseCooldownReduction;
        energyGainMultiplier = _baseEnergyGainMultiplier;
        lifeStealPercent = _baseLifeStealPercent;

        // 触发型被动重置
        berserkerLevel = 0;
        berserkerDamagePerLevel = 0f;
        thornsReflectPercent = _baseThornsReflect;
        killHealAmount = _baseKillHeal;
        globalFreezeChance = _baseGlobalFreezeChance;
        thunderWillChance = _baseThunderWillChance;
        thunderWillDamageBonus = _baseThunderWillDmgBonus;
        flameTrailLevel = 0;
        dashExplosionLevel = 0;
        experienceGainMultiplier = _baseExperienceGainMultiplier;

        // 2. 遍历所有被动道具并叠加
        foreach (var item in activePassiveItems)
        {
            ApplyPassiveItemStat(item.data, item.currentLevel);
        }

        // 3. 条件型加成（狂战士之心）—— 每次重算都检查血量
        ApplyBerserkerBonus();

        // 4. 叠加临时Buff（协程类临时加成，不参与重置）
        moveSpeedMultiplier += tempMoveSpeedBonus;
        fireRateMultiplier += tempFireRateBonus;
        critRate += tempCritRateBonus;
        aoeRadiusMultiplier += tempAoeRadiusBonus;
        lifeStealPercent += tempLifeStealBonus;

        // 5. 通知其他组件
        UpdateDependentComponents();

    }

    /// <summary>
    /// 狂战士之心：生命值低于50%时叠加增伤
    /// 每次 RecalculateStats 后自动调用，也可由 Health 变化时外部触发
    /// </summary>
    private void ApplyBerserkerBonus()
    {
        if (berserkerLevel <= 0) return;

        // 查找玩家 Health 组件
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth == null) return;

        // 血量低于50%时激活增伤
        if (playerHealth.GetHealthPercentage() < 0.5f)
        {
            float bonus = berserkerDamagePerLevel * berserkerLevel;
            damageMultiplier += bonus;
        }
    }

    /// <summary>
    /// 外部调用：当玩家血量变化时重新检查条件型被动效果
    /// 由 Health.OnHealthChanged 事件触发
    /// </summary>
    public void OnPlayerHealthChanged()
    {
        if (berserkerLevel > 0)
        {
            RecalculateStats();
        }
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

            // === 触发型被动道具 ===
            case UpgradeType.BerserkerHeart:
                berserkerLevel = level;
                berserkerDamagePerLevel = data.valuePerLevel;
                break;
            case UpgradeType.FlameTrail:
                flameTrailLevel = level;
                break;
            case UpgradeType.ThornsDamage:
                thornsReflectPercent += totalValue;
                break;
            case UpgradeType.KillHeal:
                killHealAmount += Mathf.RoundToInt(totalValue);
                break;
            case UpgradeType.GlobalFreezeChance:
                globalFreezeChance += totalValue;
                break;
            case UpgradeType.ThunderWill:
                // valuePerLevel 同时控制概率和伤害: 概率 = level * 0.08, 伤害加成 = level * 0.15
                thunderWillChance = level * 0.08f;
                thunderWillDamageBonus = level * 0.15f;
                break;
            case UpgradeType.LifeStealPassive:
                lifeStealPercent += totalValue;
                break;
            case UpgradeType.DashExplosion:
                dashExplosionLevel = level;
                break;
            case UpgradeType.ExperienceGain:
                experienceGainMultiplier += totalValue;
                Debug.Log($"<color=cyan>[PlayerStats] 经验倍率更新: +{totalValue*100f:F0}% → 当前总倍率={experienceGainMultiplier*100f:F0}% (道具:{data.itemName} Lv{level})</color>");
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