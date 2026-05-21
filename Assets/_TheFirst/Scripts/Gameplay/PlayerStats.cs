using System.Collections.Generic;
using System.Collections;
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

    [Header("流派型被动属性")]
    public int swordmasterSoulLevel = 0;         // 剑圣之魂：斩击/近战流
    public int arcaneMasteryLevel = 0;           // 奥术精通：命中触发小爆炸
    public float arcaneMasteryChance = 0f;
    public float arcaneMasteryRadius = 2.25f;
    public int elementalResonanceLevel = 0;      // 元素共鸣：多元素武器加成
    public int mechanicalResonanceLevel = 0;     // 机械共鸣：部署/机械武器加成
    public bool elementalResonanceActive = false;
    public int elementalResonanceFamilies = 0;
    public int elementalResonanceSpellCount = 0;
    public float elementalResonanceTriggerChance = 0f;
    public bool mechanicalResonanceActive = false;
    public int mechanicalResonanceWeaponCount = 0;

    private int elementalResonanceHitCounter = 0;
    private float elementalResonanceCooldownTimer = 0f;
    private float mechanicalPulseTimer = 0f;
    private bool mechanicalCapstoneQueued = false;
    private bool mechanicalCapstoneUnlocked = false;
    private static Material pulseLineMaterial;
    private static Material pulseGlowMaterial;
    private static MaterialPropertyBlock pulseLinePropertyBlock;
    private const float PulseCoreEmissionIntensity = 2f;
    private const float PulseGlowEmissionIntensity = 1.5f;
    private readonly List<PulseLineGlowPair> activePulseLineGlows = new List<PulseLineGlowPair>();

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
    private float _baseLuck;

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

    void Update()
    {
        if (elementalResonanceCooldownTimer > 0f)
        {
            elementalResonanceCooldownTimer -= Time.deltaTime;
        }

        if (!mechanicalResonanceActive || mechanicalResonanceLevel <= 0) return;

        mechanicalPulseTimer -= Time.deltaTime;
        if (mechanicalPulseTimer <= 0f)
        {
            TriggerMechanicalResonancePulse();
            mechanicalPulseTimer = Mathf.Max(4f, 8f - mechanicalResonanceLevel * 0.7f);
        }
    }

    void LateUpdate()
    {
        UpdatePulseLineGlows();
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
        _baseLuck = luck;

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
        RuntimePassiveItem trackedItem = existingItem;

        if (existingItem != null)
        {
            if (existingItem.currentLevel < itemData.EffectiveMaxLevel)
            {
                existingItem.currentLevel++;
            }
        }
        else
        {
            if (activePassiveItems.Count < 6)
            {
                trackedItem = new RuntimePassiveItem { data = itemData, currentLevel = 1 };
                activePassiveItems.Add(trackedItem);
            }
            else
            {
                Debug.LogWarning("被动道具栏已满！");
                return;
            }
        }

        if (trackedItem != null)
        {
            PlayerProgressManager.Instance?.RecordPassiveLevelReached(itemData, trackedItem.currentLevel);
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
        luck = _baseLuck;

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
        swordmasterSoulLevel = 0;
        arcaneMasteryLevel = 0;
        arcaneMasteryChance = 0f;
        elementalResonanceLevel = 0;
        mechanicalResonanceLevel = 0;
        elementalResonanceActive = false;
        elementalResonanceFamilies = 0;
        elementalResonanceSpellCount = 0;
        elementalResonanceTriggerChance = 0f;
        mechanicalResonanceActive = false;
        mechanicalResonanceWeaponCount = 0;

        // 2. 遍历所有被动道具并叠加
        foreach (var item in activePassiveItems)
        {
            if (item == null || item.data == null) continue;
            item.currentLevel = Mathf.Clamp(item.currentLevel, 1, item.data.EffectiveMaxLevel);
            ApplyPassiveItemStat(item.data, item.currentLevel);
        }

        // 2.5. 需要根据当前武器组合动态计算的流派型被动
        ApplyBuildSynergyPassiveBonuses();

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
            case UpgradeType.Luck: luck += totalValue; break;
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
            case UpgradeType.SwordmasterSoul:
                swordmasterSoulLevel = level;
                break;
            case UpgradeType.ArcaneMastery:
                arcaneMasteryLevel = level;
                arcaneMasteryChance = totalValue;
                break;
            case UpgradeType.ElementalResonance:
                elementalResonanceLevel = level;
                break;
            case UpgradeType.MechanicalResonance:
                mechanicalResonanceLevel = level;
                break;
        }

        ApplyPassiveMilestoneBonuses(data, level);
    }

    private void ApplyPassiveMilestoneBonuses(PassiveItemData data, int level)
    {
        if (data == null || level <= 0) return;

        bool reachedTierNode = level >= 3;
        bool reachedMaxNode = level >= data.EffectiveMaxLevel;

        switch (data.statType)
        {
            case UpgradeType.WeaponDamage:
                if (reachedTierNode) critRate += 0.05f;
                if (reachedMaxNode) critDamage += 0.20f;
                break;

            case UpgradeType.WeaponFireRate:
                if (reachedTierNode) projectileSpeedMultiplier += 0.15f;
                if (reachedMaxNode) bonusProjectileCount += 1;
                break;

            case UpgradeType.AoeRadius:
                if (reachedTierNode) aoeDamageMultiplier += 0.10f;
                if (reachedMaxNode) parabolicAoeStunChance += 0.12f;
                break;

            case UpgradeType.WeaponDuration:
                if (reachedTierNode) bonusOrbitalCount += 1;
                if (reachedMaxNode) fireRateMultiplier = Mathf.Max(0.2f, fireRateMultiplier - 0.08f);
                break;

            case UpgradeType.PierceCount:
                if (reachedMaxNode) bonusProjectileCount += 1;
                break;

            case UpgradeType.PickupRadius:
                if (reachedTierNode) experienceGainMultiplier += 0.08f;
                if (reachedMaxNode) moveSpeedMultiplier += 0.08f;
                break;

            case UpgradeType.MoveSpeed:
                if (reachedTierNode) pickupRadiusMultiplier += 0.15f;
                if (reachedMaxNode) dashExplosionLevel = Mathf.Max(dashExplosionLevel, 1);
                break;

            case UpgradeType.MaxHealth:
                if (reachedTierNode) armor += 1f;
                if (reachedMaxNode) killHealAmount += 2;
                break;

            case UpgradeType.Armor:
                if (reachedTierNode) armor += 1f;
                if (reachedMaxNode) bonusMaxHealth += 25;
                break;

            case UpgradeType.Luck:
                if (reachedTierNode) critRate += 0.03f;
                if (reachedMaxNode) critDamage += 0.25f;
                break;

            case UpgradeType.ExperienceGain:
                if (reachedTierNode) pickupRadiusMultiplier += 0.15f;
                if (reachedMaxNode) luck += 0.08f;
                break;
        }
    }

    private void ApplyBuildSynergyPassiveBonuses()
    {
        WeaponController controller = WeaponController.Instance;
        if (controller == null) return;

        bool hasBlade = HasOwnedWeapon(controller, IsBladeWeapon);
        if (swordmasterSoulLevel > 0 && hasBlade)
        {
            damageMultiplier += 0.04f * swordmasterSoulLevel;
            critRate += 0.03f * swordmasterSoulLevel;
        }

        int elementalFamilies = CountOwnedWeaponFamilies(controller, GetElementFamily);
        int elementalSpellCount = CountOwnedWeaponSources(controller, GetElementalSpellSourceWeight);
        elementalResonanceFamilies = elementalFamilies;
        elementalResonanceSpellCount = elementalSpellCount;
        if (elementalResonanceLevel > 0 && elementalFamilies >= 2)
        {
            elementalResonanceActive = true;
            elementalResonanceTriggerChance = CalculateElementalResonanceTriggerChance();
            damageMultiplier += 0.04f * elementalResonanceLevel * (elementalFamilies - 1);
            if (elementalFamilies >= 3)
            {
                globalFreezeChance += 0.01f * elementalResonanceLevel;
                thunderWillChance += 0.01f * elementalResonanceLevel;
            }
        }

        int mechanicalWeapons = CountOwnedWeapons(controller, IsMechanicalWeapon);
        mechanicalResonanceWeaponCount = mechanicalWeapons;
        if (mechanicalResonanceLevel > 0 && mechanicalWeapons >= 2)
        {
            mechanicalResonanceActive = true;
            durationMultiplier += 0.08f * mechanicalResonanceLevel;
            fireRateMultiplier = Mathf.Max(0.2f, fireRateMultiplier - 0.03f * mechanicalResonanceLevel);
            if (mechanicalWeapons >= 3)
            {
                bonusOrbitalCount += 1;
            }

            if (mechanicalResonanceLevel >= 3 && mechanicalWeapons >= 3)
            {
                QueueMechanicalCapstoneUnlock(controller);
            }
        }
    }

    private void QueueMechanicalCapstoneUnlock(WeaponController controller)
    {
        if (mechanicalCapstoneUnlocked || mechanicalCapstoneQueued || controller == null) return;
        if (HasOwnedWeapon(controller, IsSuperMechWeapon))
        {
            mechanicalCapstoneUnlocked = true;
            return;
        }

        WeaponStatBlock capstoneWeapon = GetMechanicalCapstoneWeapon();
        if (capstoneWeapon == null) return;

        mechanicalCapstoneQueued = true;
        StartCoroutine(GrantMechanicalCapstoneNextFrame(capstoneWeapon));
    }

    private IEnumerator GrantMechanicalCapstoneNextFrame(WeaponStatBlock capstoneWeapon)
    {
        yield return null;

        mechanicalCapstoneQueued = false;
        WeaponController controller = WeaponController.Instance;
        if (controller == null || capstoneWeapon == null) yield break;
        if (HasOwnedWeapon(controller, IsSuperMechWeapon))
        {
            mechanicalCapstoneUnlocked = true;
            yield break;
        }

        mechanicalCapstoneUnlocked = true;
        controller.AddNewWeapon(capstoneWeapon);
        StartCoroutine(PlayMechanicalCapstoneSummonEffect(transform.position, 3.4f));
        Debug.Log("<color=orange>[Build] Mechanical capstone unlocked: SuperMech.</color>");
    }

    private WeaponStatBlock GetMechanicalCapstoneWeapon()
    {
        if (UpgradeManager.Instance == null || UpgradeManager.Instance.upgradeDatabase == null) return null;
        return UpgradeManager.Instance.upgradeDatabase.mechanicalCapstoneWeapon;
    }

    public void RefreshStats()
    {
        RecalculateStats();
    }

    public bool TryTriggerArcaneMastery(Vector3 position, GameObject attacker, string sourceWeaponName)
    {
        if (arcaneMasteryLevel <= 0 || arcaneMasteryChance <= 0f) return false;
        if (IsBuildFeedbackSource(sourceWeaponName)) return false;
        if (Random.value > arcaneMasteryChance) return false;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt((8 + arcaneMasteryLevel * 4) * damageMultiplier));
        Collider[] hits = Physics.OverlapSphere(position, arcaneMasteryRadius, LayerMask.GetMask("Enemies"));
        int damaged = 0;
        foreach (var hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(finalDamage, position, attacker, AttackType.Standard, null, null, "奥术精通");
                damaged++;
            }
        }
        if (damaged > 0)
        {
            StartCoroutine(PlayArcaneMasteryEffect(position, arcaneMasteryRadius));
        }
        return damaged > 0;
    }

    public bool TryTriggerElementalResonance(Vector3 position, GameObject attacker, string sourceWeaponName)
    {
        if (!elementalResonanceActive || elementalResonanceLevel <= 0) return false;
        if (IsBuildFeedbackSource(sourceWeaponName)) return false;
        if (elementalResonanceCooldownTimer > 0f) return false;

        elementalResonanceHitCounter++;
        float triggerChance = CalculateElementalResonanceTriggerChance();
        elementalResonanceTriggerChance = triggerChance;
        int hitsRequired = Mathf.Max(2, 10 - elementalResonanceLevel - elementalResonanceFamilies - elementalResonanceSpellCount * 2);
        bool chanceTriggered = Random.value <= triggerChance;
        if (!chanceTriggered && elementalResonanceHitCounter < hitsRequired) return false;

        elementalResonanceHitCounter = 0;
        elementalResonanceCooldownTimer = 0.28f;

        float radius = 2.05f
            + elementalResonanceLevel * 0.18f
            + Mathf.Max(0, elementalResonanceFamilies - 2) * 0.18f
            + elementalResonanceSpellCount * 0.12f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt((6 + elementalResonanceLevel * 4 + elementalResonanceFamilies * 2 + elementalResonanceSpellCount * 3) * damageMultiplier));
        Collider[] hits = Physics.OverlapSphere(position, radius, LayerMask.GetMask("Enemies"));
        int damaged = 0;
        foreach (var hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(finalDamage, position, attacker, AttackType.Standard, null, null, "元素共鸣");
                damaged++;
            }
        }

        if (damaged > 0)
        {
            StartCoroutine(PlayElementalPulseEffect(position, radius));
        }
        return damaged > 0;
    }

    private float CalculateElementalResonanceTriggerChance()
    {
        if (elementalResonanceLevel <= 0) return 0f;

        float baseChance = 0.08f;
        float levelBonus = elementalResonanceLevel * 0.04f;
        float spellBonus = elementalResonanceSpellCount * 0.065f;
        float familyBonus = Mathf.Max(0, elementalResonanceFamilies - 2) * 0.025f;
        return Mathf.Clamp(baseChance + levelBonus + spellBonus + familyBonus, 0f, 0.55f);
    }

    private void TriggerMechanicalResonancePulse()
    {
        float radius = 2.5f + mechanicalResonanceLevel * 0.18f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt((6 + mechanicalResonanceLevel * 4) * damageMultiplier));
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, LayerMask.GetMask("Enemies"));
        int damaged = 0;
        foreach (var hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(finalDamage, transform.position, gameObject, AttackType.Standard, null, null, "机械共鸣");
                damaged++;
            }
        }

        if (damaged > 0)
        {
            StartCoroutine(PlayMechanicalOverclockEffect(transform.position, radius));
        }
    }

    private bool IsBuildFeedbackSource(string sourceWeaponName)
    {
        return sourceWeaponName == "奥术精通" ||
               sourceWeaponName == "元素共鸣" ||
               sourceWeaponName == "机械共鸣";
    }

    private IEnumerator PlayElementalPulseEffect(Vector3 position, float radius)
    {
        StartCoroutine(PlayRingPulseEffect(position, radius, new Color(1f, 0.28f, 0.08f, 1f), 0.28f, 0.24f));
        yield return new WaitForSeconds(0.08f);
        StartCoroutine(PlayPolygonPulseEffect(position, radius * 0.85f, 6, 30f, new Color(0.32f, 0.92f, 1f, 1f), 0.3f, 0.2f));
        yield return new WaitForSeconds(0.08f);
        StartCoroutine(PlayLightningBurstEffect(position, radius * 0.95f, new Color(1f, 0.92f, 0.12f, 1f), 0.26f));
        StartCoroutine(PlayElementalVerticalBurstEffect(position, radius));
    }

    private IEnumerator PlayArcaneMasteryEffect(Vector3 position, float radius)
    {
        StartCoroutine(PlayRingPulseEffect(position, radius * 1.05f, new Color(0.88f, 0.38f, 1f, 1f), 0.34f, 0.24f));
        StartCoroutine(PlayStarPulseEffect(position, radius * 0.86f, new Color(0.62f, 0.18f, 1f, 1f), 0.38f));
        StartCoroutine(PlayRadialBurstEffect(position, radius * 1.1f, new Color(0.95f, 0.82f, 1f, 1f), 12, 0.28f));
        StartCoroutine(PlayArcaneAscensionEffect(position, radius));
        yield return null;
    }

    private IEnumerator PlayMechanicalOverclockEffect(Vector3 position, float radius)
    {
        StartCoroutine(PlayGearPulseEffect(position, radius, new Color(0.15f, 0.76f, 1f, 1f), new Color(1f, 0.45f, 0.08f, 1f), 0.48f));
        StartCoroutine(PlayPolygonPulseEffect(position, radius * 0.62f, 4, 45f, new Color(1f, 0.58f, 0.12f, 1f), 0.36f, 0.18f));
        StartCoroutine(PlayMechanicalHoverEffect(position, radius));
        yield return null;
    }

    private IEnumerator PlayMechanicalCapstoneSummonEffect(Vector3 position, float radius)
    {
        StartCoroutine(PlayGearPulseEffect(position, radius, new Color(0.18f, 0.88f, 1f, 1f), new Color(1f, 0.64f, 0.12f, 1f), 0.72f));
        yield return new WaitForSeconds(0.08f);
        StartCoroutine(PlayMechanicalHoverEffect(position, radius * 1.25f));
        StartCoroutine(PlayRadialBurstEffect(position + Vector3.up * 0.3f, radius * 1.2f, new Color(1f, 0.86f, 0.22f, 1f), 16, 0.42f));
    }

    private IEnumerator PlayArcaneAscensionEffect(Vector3 position, float radius)
    {
        const int helixPoints = 34;
        const int pillarCount = 6;
        GameObject root = new GameObject("ArcaneAscension");
        LineRenderer helix = CreatePulseLine(root, "ArcaneHelix", false, helixPoints, 0.11f, new Color(0.95f, 0.62f, 1f, 1f));
        LineRenderer[] pillars = new LineRenderer[pillarCount];
        for (int i = 0; i < pillarCount; i++)
        {
            pillars[i] = CreatePulseLine(root, "ArcanePillar", false, 2, 0.075f, new Color(0.78f, 0.28f, 1f, 1f));
        }

        float duration = 0.44f;
        float height = Mathf.Clamp(radius * 1.15f, 1.6f, 2.8f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Color helixColor = FadePulseColor(new Color(0.95f, 0.62f, 1f, 1f), t);
            helix.startColor = helixColor;
            helix.endColor = helixColor;
            helix.startWidth = Mathf.Lerp(0.13f, 0.03f, t);
            helix.endWidth = helix.startWidth;

            for (int i = 0; i < helixPoints; i++)
            {
                float p = i / (float)(helixPoints - 1);
                float angle = p * Mathf.PI * 4f + t * Mathf.PI * 2f;
                float r = Mathf.Lerp(radius * 0.2f, radius * 0.5f, Mathf.Sin(p * Mathf.PI));
                Vector3 point = position + new Vector3(Mathf.Cos(angle) * r, Mathf.Lerp(0.22f, height, p), Mathf.Sin(angle) * r);
                helix.SetPosition(i, point);
            }

            for (int i = 0; i < pillarCount; i++)
            {
                float angle = i * Mathf.PI * 2f / pillarCount + t * 0.85f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius * 0.38f, 0f, Mathf.Sin(angle) * radius * 0.38f);
                Color pillarColor = FadePulseColor(new Color(0.78f, 0.28f, 1f, 1f), t);
                pillars[i].startColor = pillarColor;
                pillars[i].endColor = pillarColor;
                pillars[i].startWidth = Mathf.Lerp(0.09f, 0.02f, t);
                pillars[i].endWidth = pillars[i].startWidth;
                pillars[i].SetPosition(0, position + offset + Vector3.up * 0.2f);
                pillars[i].SetPosition(1, position + offset * 0.5f + Vector3.up * Mathf.Lerp(height * 0.55f, height, 1f - t));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator PlayElementalVerticalBurstEffect(Vector3 position, float radius)
    {
        GameObject root = new GameObject("ElementalVerticalBurst");
        LineRenderer fire = CreatePulseLine(root, "FireSpout", false, 7, 0.17f, new Color(1f, 0.28f, 0.06f, 1f));
        LineRenderer ice = CreatePulseLine(root, "IceShard", true, 4, 0.13f, new Color(0.62f, 0.95f, 1f, 1f));
        LineRenderer lightning = CreatePulseLine(root, "LightningStrike", false, 6, 0.18f, new Color(1f, 0.92f, 0.08f, 1f));

        float duration = 0.38f;
        float height = Mathf.Clamp(radius * 1.25f, 1.8f, 3.2f);
        Vector3 fireOffset = new Vector3(-radius * 0.18f, 0f, radius * 0.08f);
        Vector3 iceOffset = new Vector3(radius * 0.18f, 0f, radius * 0.04f);
        Vector3 lightningOffset = new Vector3(0f, 0f, -radius * 0.12f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;

            Color fireColor = FadePulseColor(new Color(1f, 0.3f, 0.05f, 1f), t);
            fire.startColor = fireColor;
            fire.endColor = fireColor;
            fire.startWidth = Mathf.Lerp(0.2f, 0.04f, t);
            fire.endWidth = fire.startWidth;
            for (int i = 0; i < 7; i++)
            {
                float p = i / 6f;
                float sway = Mathf.Sin(p * Mathf.PI * 2f + t * 8f) * radius * 0.08f * (1f - p);
                fire.SetPosition(i, position + fireOffset + new Vector3(sway, Mathf.Lerp(0.15f, height * 0.85f, p), Mathf.Sin(p * Mathf.PI) * radius * 0.08f));
            }

            Color iceColor = FadePulseColor(new Color(0.65f, 0.96f, 1f, 1f), t);
            ice.startColor = iceColor;
            ice.endColor = iceColor;
            ice.startWidth = Mathf.Lerp(0.16f, 0.035f, t);
            ice.endWidth = ice.startWidth;
            float shardHeight = Mathf.Lerp(height * 0.35f, height * 0.95f, Mathf.Clamp01(t * 1.4f));
            ice.SetPosition(0, position + iceOffset + new Vector3(0f, 0.2f, radius * 0.16f));
            ice.SetPosition(1, position + iceOffset + new Vector3(radius * 0.16f, 0.2f, -radius * 0.08f));
            ice.SetPosition(2, position + iceOffset + Vector3.up * shardHeight);
            ice.SetPosition(3, position + iceOffset + new Vector3(-radius * 0.16f, 0.2f, -radius * 0.08f));

            Color lightningColor = FadePulseColor(new Color(1f, 0.94f, 0.08f, 1f), t);
            lightning.startColor = lightningColor;
            lightning.endColor = lightningColor;
            lightning.startWidth = Mathf.Lerp(0.2f, 0.035f, t);
            lightning.endWidth = lightning.startWidth;
            for (int i = 0; i < 6; i++)
            {
                float p = i / 5f;
                float jitterX = (i % 2 == 0 ? 1f : -1f) * radius * 0.11f * (1f - Mathf.Abs(p - 0.5f));
                float jitterZ = (i % 3 == 0 ? -1f : 1f) * radius * 0.08f * (1f - p);
                lightning.SetPosition(i, position + lightningOffset + new Vector3(jitterX, Mathf.Lerp(height * 1.25f, 0.18f, p), jitterZ));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator PlayMechanicalHoverEffect(Vector3 position, float radius)
    {
        GameObject root = new GameObject("MechanicalHoverOverclock");
        LineRenderer hoverGear = CreatePulseLine(root, "HoverGear", true, 24, 0.13f, new Color(0.2f, 0.82f, 1f, 1f));
        LineRenderer hoverSquare = CreatePulseLine(root, "HoverSquare", true, 4, 0.11f, new Color(1f, 0.5f, 0.08f, 1f));
        LineRenderer antenna = CreatePulseLine(root, "EnergyAntenna", false, 2, 0.09f, new Color(0.85f, 0.95f, 1f, 1f));

        float duration = 0.55f;
        float hoverHeight = 1.25f;
        float currentRadius = Mathf.Clamp(radius * 0.34f, 0.8f, 1.25f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 center = position + Vector3.up * Mathf.Lerp(hoverHeight, hoverHeight + 0.45f, Mathf.Sin(t * Mathf.PI));

            Color gearColor = FadePulseColor(new Color(0.2f, 0.82f, 1f, 1f), t);
            hoverGear.startColor = gearColor;
            hoverGear.endColor = gearColor;
            hoverGear.startWidth = Mathf.Lerp(0.15f, 0.035f, t);
            hoverGear.endWidth = hoverGear.startWidth;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24 + t * 2.8f;
                float r = currentRadius * (i % 2 == 0 ? 1f : 0.78f);
                hoverGear.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }

            Color squareColor = FadePulseColor(new Color(1f, 0.5f, 0.08f, 1f), t);
            hoverSquare.startColor = squareColor;
            hoverSquare.endColor = squareColor;
            hoverSquare.startWidth = Mathf.Lerp(0.13f, 0.03f, t);
            hoverSquare.endWidth = hoverSquare.startWidth;
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f - t * 1.8f;
                hoverSquare.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * currentRadius * 0.62f, 0f, Mathf.Sin(angle) * currentRadius * 0.62f));
            }

            Color antennaColor = FadePulseColor(new Color(0.85f, 0.95f, 1f, 1f), t);
            antenna.startColor = antennaColor;
            antenna.endColor = antennaColor;
            antenna.startWidth = Mathf.Lerp(0.1f, 0.02f, t);
            antenna.endWidth = antenna.startWidth;
            antenna.SetPosition(0, position + Vector3.up * 0.2f);
            antenna.SetPosition(1, center);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator PlayRingPulseEffect(Vector3 position, float radius, Color color, float duration, float startWidth)
    {
        const int segments = 64;
        GameObject pulse = new GameObject("BuildRingPulse");
        LineRenderer line = CreatePulseLine(pulse, "Ring", true, segments, startWidth, color);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentRadius = Mathf.Lerp(radius * 0.18f, radius, t);
            Color currentColor = FadePulseColor(color, t);
            line.startColor = currentColor;
            line.endColor = currentColor;
            float width = Mathf.Lerp(startWidth, 0.04f, t);
            line.startWidth = width;
            line.endWidth = width;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * currentRadius, 0.11f, Mathf.Sin(angle) * currentRadius));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pulse);
    }

    private IEnumerator PlayStarPulseEffect(Vector3 position, float radius, Color color, float duration)
    {
        const int points = 12;
        GameObject pulse = new GameObject("ArcaneStarPulse");
        LineRenderer star = CreatePulseLine(pulse, "ArcaneStar", true, points, 0.18f, color);
        LineRenderer core = CreatePulseLine(pulse, "ArcaneCore", true, 32, 0.1f, new Color(1f, 0.9f, 1f, 1f));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentRadius = Mathf.Lerp(radius * 0.35f, radius, t);
            float innerRadius = currentRadius * 0.42f;
            Color currentColor = FadePulseColor(color, t);
            star.startColor = currentColor;
            star.endColor = currentColor;
            star.startWidth = Mathf.Lerp(0.18f, 0.035f, t);
            star.endWidth = star.startWidth;

            for (int i = 0; i < points; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / points + t * 2.4f;
                float r = (i % 2 == 0) ? currentRadius : innerRadius;
                star.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * r, 0.14f, Mathf.Sin(angle) * r));
            }

            Color coreColor = FadePulseColor(new Color(1f, 0.88f, 1f, 1f), t);
            core.startColor = coreColor;
            core.endColor = coreColor;
            float coreRadius = Mathf.Lerp(radius * 0.14f, radius * 0.32f, t);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32;
                core.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * coreRadius, 0.145f, Mathf.Sin(angle) * coreRadius));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pulse);
    }

    private IEnumerator PlayPolygonPulseEffect(Vector3 position, float radius, int sides, float rotationDegrees, Color color, float duration, float startWidth)
    {
        GameObject pulse = new GameObject("BuildPolygonPulse");
        LineRenderer line = CreatePulseLine(pulse, "Polygon", true, sides, startWidth, color);
        float rotation = rotationDegrees * Mathf.Deg2Rad;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentRadius = Mathf.Lerp(radius * 0.22f, radius, t);
            Color currentColor = FadePulseColor(color, t);
            line.startColor = currentColor;
            line.endColor = currentColor;
            float width = Mathf.Lerp(startWidth, 0.04f, t);
            line.startWidth = width;
            line.endWidth = width;

            for (int i = 0; i < sides; i++)
            {
                float angle = rotation + i * Mathf.PI * 2f / sides - t * 0.8f;
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * currentRadius, 0.12f, Mathf.Sin(angle) * currentRadius));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(pulse);
    }

    private IEnumerator PlayLightningBurstEffect(Vector3 position, float radius, Color color, float duration)
    {
        const int boltCount = 5;
        const int boltPoints = 5;
        GameObject root = new GameObject("ElementalLightningBurst");
        LineRenderer[] bolts = new LineRenderer[boltCount];
        Vector3[][] boltPaths = new Vector3[boltCount][];

        for (int i = 0; i < boltCount; i++)
        {
            bolts[i] = CreatePulseLine(root, "LightningBolt", false, boltPoints, 0.16f, color);
            boltPaths[i] = new Vector3[boltPoints];
            float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / boltCount + Random.Range(-0.18f, 0.18f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);

            for (int p = 0; p < boltPoints; p++)
            {
                float distance = radius * p / (boltPoints - 1);
                float jitter = (p == 0 || p == boltPoints - 1) ? 0f : Random.Range(-0.18f, 0.18f) * radius;
                boltPaths[i][p] = position + dir * distance + side * jitter + Vector3.up * 0.16f;
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Color currentColor = FadePulseColor(color, t);
            for (int i = 0; i < boltCount; i++)
            {
                bolts[i].startColor = currentColor;
                bolts[i].endColor = currentColor;
                bolts[i].startWidth = Mathf.Lerp(0.18f, 0.03f, t);
                bolts[i].endWidth = bolts[i].startWidth;
                for (int p = 0; p < boltPoints; p++)
                {
                    bolts[i].SetPosition(p, Vector3.Lerp(position + Vector3.up * 0.16f, boltPaths[i][p], Mathf.Clamp01(t * 1.4f)));
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator PlayRadialBurstEffect(Vector3 position, float radius, Color color, int rayCount, float duration)
    {
        GameObject root = new GameObject("ArcaneRadialBurst");
        LineRenderer[] rays = new LineRenderer[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            rays[i] = CreatePulseLine(root, "ArcaneRay", false, 2, 0.12f, color);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Color currentColor = FadePulseColor(color, t);
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount + t * 0.6f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float inner = Mathf.Lerp(0.12f, radius * 0.35f, t);
                float outer = Mathf.Lerp(radius * 0.4f, radius, t);
                rays[i].startColor = currentColor;
                rays[i].endColor = currentColor;
                rays[i].startWidth = Mathf.Lerp(0.14f, 0.02f, t);
                rays[i].endWidth = rays[i].startWidth;
                rays[i].SetPosition(0, position + dir * inner + Vector3.up * 0.15f);
                rays[i].SetPosition(1, position + dir * outer + Vector3.up * 0.15f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator PlayGearPulseEffect(Vector3 position, float radius, Color gearColor, Color tickColor, float duration)
    {
        const int teeth = 16;
        const int points = teeth * 2;
        GameObject root = new GameObject("MechanicalGearPulse");
        LineRenderer gear = CreatePulseLine(root, "Gear", true, points, 0.18f, gearColor);
        LineRenderer square = CreatePulseLine(root, "OverclockSquare", true, 4, 0.16f, tickColor);
        LineRenderer[] spokes = new LineRenderer[8];
        for (int i = 0; i < spokes.Length; i++)
        {
            spokes[i] = CreatePulseLine(root, "GearSpoke", false, 2, 0.1f, tickColor);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentRadius = Mathf.Lerp(radius * 0.25f, radius, t);
            Color gearCurrent = FadePulseColor(gearColor, t);
            Color tickCurrent = FadePulseColor(tickColor, t);
            gear.startColor = gearCurrent;
            gear.endColor = gearCurrent;
            gear.startWidth = Mathf.Lerp(0.2f, 0.04f, t);
            gear.endWidth = gear.startWidth;

            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points + t * 1.2f;
                float toothRadius = currentRadius * ((i % 2 == 0) ? 1f : 0.78f);
                gear.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * toothRadius, 0.13f, Mathf.Sin(angle) * toothRadius));
            }

            square.startColor = tickCurrent;
            square.endColor = tickCurrent;
            square.startWidth = Mathf.Lerp(0.18f, 0.035f, t);
            square.endWidth = square.startWidth;
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f - t * 0.9f;
                square.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * currentRadius * 0.58f, 0.145f, Mathf.Sin(angle) * currentRadius * 0.58f));
            }

            for (int i = 0; i < spokes.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / spokes.Length - t * 1.4f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                spokes[i].startColor = tickCurrent;
                spokes[i].endColor = tickCurrent;
                spokes[i].startWidth = Mathf.Lerp(0.11f, 0.02f, t);
                spokes[i].endWidth = spokes[i].startWidth;
                spokes[i].SetPosition(0, position + dir * currentRadius * 0.24f + Vector3.up * 0.15f);
                spokes[i].SetPosition(1, position + dir * currentRadius * 0.72f + Vector3.up * 0.15f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(root);
    }

    private LineRenderer CreatePulseLine(GameObject root, string name, bool loop, int positionCount, float width, Color color)
    {
        Color coreColor = BoostPulseColor(color, 2.45f, 1f);
        Color glowColor = BoostPulseColor(color, 1.55f, 0.48f);

        GameObject glowObject = new GameObject(name + "_Glow");
        glowObject.transform.SetParent(root.transform);
        LineRenderer glow = glowObject.AddComponent<LineRenderer>();
        SetupPulseLineRenderer(glow, loop, positionCount, width * 3.4f, glowColor, GetPulseGlowMaterial(), 29, PulseGlowEmissionIntensity);

        GameObject lineObject = new GameObject(name + "_Core");
        lineObject.transform.SetParent(root.transform);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        SetupPulseLineRenderer(line, loop, positionCount, width, coreColor, GetPulseLineMaterial(), 30, PulseCoreEmissionIntensity);

        activePulseLineGlows.Add(new PulseLineGlowPair(line, glow, 3.4f, 0.46f, 1.55f, PulseCoreEmissionIntensity, PulseGlowEmissionIntensity));
        return line;
    }

    private void SetupPulseLineRenderer(LineRenderer line, bool loop, int positionCount, float width, Color color, Material material, int sortingOrder, float emissionIntensity)
    {
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = positionCount;
        line.material = material;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        ApplyPulseLinePropertyBlock(line, color, emissionIntensity);
    }

    private Color FadePulseColor(Color color, float t)
    {
        float normalizedTime = Mathf.Clamp01(t);
        float intensity = Mathf.Lerp(2.45f, 1.15f, normalizedTime);
        return BoostPulseColor(color, intensity, Mathf.Lerp(1f, 0.34f, normalizedTime));
    }

    private Color BoostPulseColor(Color color, float intensity, float alphaScale)
    {
        return new Color(color.r * intensity, color.g * intensity, color.b * intensity, Mathf.Clamp01(color.a * alphaScale));
    }

    private Material GetPulseLineMaterial()
    {
        if (pulseLineMaterial != null) return pulseLineMaterial;

        Shader shader = FindPulseShader();
        pulseLineMaterial = new Material(shader);
        ConfigurePulseMaterial(pulseLineMaterial, true);
        return pulseLineMaterial;
    }

    private Material GetPulseGlowMaterial()
    {
        if (pulseGlowMaterial != null) return pulseGlowMaterial;

        Shader shader = FindPulseShader();
        pulseGlowMaterial = new Material(shader);
        ConfigurePulseMaterial(pulseGlowMaterial, true);
        return pulseGlowMaterial;
    }

    private Shader FindPulseShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }

    private void ConfigurePulseMaterial(Material material, bool additive)
    {
        if (material == null) return;

        material.hideFlags = HideFlags.DontSave;
        material.renderQueue = 3000;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white * PulseCoreEmissionIntensity);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_EMISSION");
    }

    private void UpdatePulseLineGlows()
    {
        for (int i = activePulseLineGlows.Count - 1; i >= 0; i--)
        {
            if (!activePulseLineGlows[i].TryUpdate())
            {
                activePulseLineGlows.RemoveAt(i);
            }
        }
    }

    private static void ApplyPulseLinePropertyBlock(LineRenderer line, Color color, float emissionIntensity)
    {
        if (line == null) return;

        if (pulseLinePropertyBlock == null)
        {
            pulseLinePropertyBlock = new MaterialPropertyBlock();
        }

        Color visibleColor = NormalizePulseHue(color);
        visibleColor.a = color.a;
        Color emissionColor = new Color(
            visibleColor.r * emissionIntensity,
            visibleColor.g * emissionIntensity,
            visibleColor.b * emissionIntensity,
            1f);

        pulseLinePropertyBlock.Clear();
        pulseLinePropertyBlock.SetColor("_BaseColor", visibleColor);
        pulseLinePropertyBlock.SetColor("_Color", visibleColor);
        pulseLinePropertyBlock.SetColor("_EmissionColor", emissionColor);
        pulseLinePropertyBlock.SetTexture("_EmissionMap", Texture2D.whiteTexture);
        line.SetPropertyBlock(pulseLinePropertyBlock);
    }

    private static Color NormalizePulseHue(Color color)
    {
        float maxChannel = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (maxChannel <= 1f || maxChannel <= 0.001f)
        {
            return color;
        }

        return new Color(color.r / maxChannel, color.g / maxChannel, color.b / maxChannel, color.a);
    }

    private bool HasOwnedWeapon(WeaponController controller, System.Func<WeaponStatBlock, bool> predicate)
    {
        foreach (var owned in controller.ownedWeapons)
        {
            if (OwnedWeaponMatches(owned, predicate)) return true;
        }
        return false;
    }

    private int CountOwnedWeapons(WeaponController controller, System.Func<WeaponStatBlock, bool> predicate)
    {
        int count = 0;
        foreach (var owned in controller.ownedWeapons)
        {
            if (OwnedWeaponMatches(owned, predicate)) count++;
        }
        return count;
    }

    private int CountOwnedWeaponSources(WeaponController controller, System.Func<WeaponStatBlock, int> weightSelector)
    {
        if (controller == null || weightSelector == null) return 0;

        int count = 0;
        HashSet<WeaponStatBlock> countedSources = new HashSet<WeaponStatBlock>();
        foreach (var owned in controller.ownedWeapons)
        {
            foreach (WeaponStatBlock source in GetOwnedWeaponBuildSources(owned))
            {
                if (source == null || !countedSources.Add(source)) continue;
                count += Mathf.Max(0, weightSelector(source));
            }
        }

        return count;
    }

    private int CountOwnedWeaponFamilies(WeaponController controller, System.Func<WeaponStatBlock, string> familySelector)
    {
        HashSet<string> families = new HashSet<string>();
        foreach (var owned in controller.ownedWeapons)
        {
            foreach (WeaponStatBlock source in GetOwnedWeaponBuildSources(owned))
            {
                string family = familySelector(source);
                if (!string.IsNullOrEmpty(family)) families.Add(family);
            }
        }
        return families.Count;
    }

    private bool OwnedWeaponMatches(OwnedWeapon owned, System.Func<WeaponStatBlock, bool> predicate)
    {
        if (owned == null || predicate == null) return false;

        foreach (WeaponStatBlock source in GetOwnedWeaponBuildSources(owned))
        {
            if (predicate(source)) return true;
        }

        return false;
    }

    private IEnumerable<WeaponStatBlock> GetOwnedWeaponBuildSources(OwnedWeapon owned)
    {
        if (owned == null) yield break;

        HashSet<WeaponStatBlock> seen = new HashSet<WeaponStatBlock>();
        if (owned.stats != null && seen.Add(owned.stats)) yield return owned.stats;

        if (owned.weaponPartInstance != null &&
            owned.weaponPartInstance.StatBlock != null &&
            seen.Add(owned.weaponPartInstance.StatBlock))
        {
            yield return owned.weaponPartInstance.StatBlock;
        }

        if (owned.inheritedSkillSources == null) yield break;
        foreach (WeaponStatBlock source in owned.inheritedSkillSources)
        {
            if (source != null && seen.Add(source)) yield return source;
        }
    }

    private bool IsBladeWeapon(WeaponStatBlock stats)
    {
        if (WeaponBuildTagUtility.IsSlashWeapon(stats)) return true;
        if (stats == null) return false;
        string id = stats.weaponID ?? "";
        string name = stats.weaponName ?? "";
        return id.Contains("Blade") || name.Contains("斩") || name.Contains("刃");
    }

    private bool IsMechanicalWeapon(WeaponStatBlock stats)
    {
        if (WeaponBuildTagUtility.IsMechanicalWeapon(stats)) return true;
        if (stats == null) return false;
        string id = stats.weaponID ?? "";
        string name = stats.weaponName ?? "";
        return id.Contains("Landmine") || id.Contains("Laser") || id.Contains("Beam") ||
               id.Contains("Orbit") || id.Contains("Turret") || id.Contains("Drone") ||
               id.Contains("SuperMech") || name.Contains("地雷") || name.Contains("炮塔") ||
               name.Contains("喷火塔") || name.Contains("塔") || name.Contains("Beam") ||
               name.Contains("镭射") || name.Contains("盾");
    }

    private bool IsElementalSpellWeapon(WeaponStatBlock stats)
    {
        return GetElementalSpellSourceWeight(stats) > 0;
    }

    private int GetElementalSpellSourceWeight(WeaponStatBlock stats)
    {
        if (stats == null) return 0;
        if (string.IsNullOrEmpty(GetElementFamily(stats))) return 0;

        HashSet<WeaponBuildTag> tags = WeaponBuildTagUtility.GetTags(stats);
        if (tags.Contains(WeaponBuildTag.Spell)) return 1;

        switch (stats.behavior)
        {
            case WeaponBehaviorType.Standard:
            case WeaponBehaviorType.Pierce:
            case WeaponBehaviorType.ParabolicAOE:
            case WeaponBehaviorType.Chain:
            case WeaponBehaviorType.FrostNova:
                return 1;
            default:
                return 0;
        }
    }

    private bool IsSuperMechWeapon(WeaponStatBlock stats)
    {
        if (stats == null) return false;
        string id = stats.weaponID ?? "";
        string name = stats.weaponName ?? "";
        return id.Contains("SuperMech") || name.Contains("巨大机器人") || name.Contains("机器人");
    }

    private string GetElementFamily(WeaponStatBlock stats)
    {
        string taggedFamily = WeaponBuildTagUtility.GetPrimaryElementFamily(stats);
        if (!string.IsNullOrEmpty(taggedFamily)) return taggedFamily;
        if (stats == null) return "";
        string id = stats.weaponID ?? "";
        string name = stats.weaponName ?? "";
        if (id.Contains("Fire") || id.Contains("Flame") || name.Contains("火") || name.Contains("炎")) return "Fire";
        if (id.Contains("Ice") || id.Contains("Frost") || name.Contains("冰") || name.Contains("霜")) return "Ice";
        if (id.Contains("Lightning") || id.Contains("Thunder") || name.Contains("雷") || name.Contains("电")) return "Thunder";
        if (id.Contains("Hurricane") || id.Contains("Wind") || name.Contains("风")) return "Wind";
        if (id.Contains("Corrode") || name.Contains("毒") || name.Contains("腐蚀")) return "Corrode";
        return "";
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

    private class PulseLineGlowPair
    {
        private readonly LineRenderer core;
        private readonly LineRenderer glow;
        private readonly float widthMultiplier;
        private readonly float alphaMultiplier;
        private readonly float colorIntensity;
        private readonly float coreEmissionIntensity;
        private readonly float glowEmissionIntensity;

        public PulseLineGlowPair(LineRenderer core, LineRenderer glow, float widthMultiplier, float alphaMultiplier, float colorIntensity, float coreEmissionIntensity, float glowEmissionIntensity)
        {
            this.core = core;
            this.glow = glow;
            this.widthMultiplier = widthMultiplier;
            this.alphaMultiplier = alphaMultiplier;
            this.colorIntensity = colorIntensity;
            this.coreEmissionIntensity = coreEmissionIntensity;
            this.glowEmissionIntensity = glowEmissionIntensity;
        }

        public bool TryUpdate()
        {
            if (core == null || glow == null)
            {
                return false;
            }

            glow.enabled = core.enabled;
            glow.useWorldSpace = core.useWorldSpace;
            glow.loop = core.loop;

            if (glow.positionCount != core.positionCount)
            {
                glow.positionCount = core.positionCount;
            }

            for (int i = 0; i < core.positionCount; i++)
            {
                glow.SetPosition(i, core.GetPosition(i));
            }

            glow.startWidth = core.startWidth * widthMultiplier;
            glow.endWidth = core.endWidth * widthMultiplier;
            glow.startColor = ToGlowColor(core.startColor);
            glow.endColor = ToGlowColor(core.endColor);
            ApplyPulseLinePropertyBlock(core, core.startColor, coreEmissionIntensity);
            ApplyPulseLinePropertyBlock(glow, glow.startColor, glowEmissionIntensity);
            return true;
        }

        private Color ToGlowColor(Color color)
        {
            return new Color(
                color.r * colorIntensity,
                color.g * colorIntensity,
                color.b * colorIntensity,
                Mathf.Clamp01(color.a * alphaMultiplier));
        }
    }
}
