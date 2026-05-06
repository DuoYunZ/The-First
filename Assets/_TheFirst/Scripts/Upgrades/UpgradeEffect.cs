using UnityEngine;

// 品质枚举保持不变
public enum Rarity { Common, Uncommon, Rare, Epic }

// 效果行为类型枚举
public enum EffectActionType { ModifyStat, UnlockWeapon, UnlockShield, EvolveWeapon, UnlockUltimate, ActivateCharSkill }

// 【新增】修改类型枚举，用于区分是增加固定值还是百分比
public enum ModifierType { Flat, Percentage }

[System.Serializable]
public class UpgradeEffect
{
    [Tooltip("升级效果的行为类型")]
    public EffectActionType actionType = EffectActionType.ModifyStat;

    // --- 核心新增：关联被动道具 ---
    [Header("【如果这是一个被动道具卡片】")]
    public PassiveItemData passiveItemData;
    // ---------------------------

    [Header("【如果 Action Type 是 ModifyStat】")]
    [Tooltip("这个效果属于哪个属性")]
    public UpgradeType statToModify;
    [Tooltip("这个效果提供的数值")]
    public float value;
    [Tooltip("数值的类型是固定值(Flat)还是百分比(Percentage)")]
    public ModifierType modType = ModifierType.Percentage;

    [Header("【如果 Action Type 是 UnlockWeapon/Evolve】")]
    [Tooltip("要解锁的武器数据")]
    public WeaponStatBlock weaponToUnlock;

    [Header("【如果 Action Type 是 UnlockShield】")]
    [Tooltip("要解锁的护盾数据")]
    public ShieldData shieldToUnlock;

    [Header("【如果 Action Type 是 ActivateCharSkill】")]
    [Tooltip("要激活的角色技能标识符（如 PrecisionSlash、Sword_Talent_Wind 等）")]
    public string skillIdentifier;
}