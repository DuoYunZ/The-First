using UnityEngine;
using System.Collections.Generic;

// 这个枚举将定义升级的具体效果类型
public enum PermanentUpgradeType
{
    FlatDamage,
    DamagePercent,
    FireRatePercent,
    ProjectileSpeedPercent,
    // ... 未来可以添加更多，如暴击率、穿透+1等
    MeleeAoeFlatDamage,         // 近战范围固定伤害 (为您的刀光量身定做)
    UnlockBladeEnergyProjectile,     // 解锁“刃气弹”机制
    ImproveBladeEnergyFrequency,   // 提升“刃气弹”触发频率
    ImproveBladeEnergyRange,       // 提升“刃气弹”距离/速度

    // --- 角色技能树专用 ---
    MaxHealthFlat,              // 最大生命值（固定值）
    ArmorFlat,                  // 护甲（固定值）
    MoveSpeedPercent,           // 移动速度（百分比）
    CooldownReductionPercent,   // 全局冷却缩减（百分比）
    EnergyGainPercent,          // 能量获取效率（百分比）
    LifeStealPercent            // 伤害吸血（百分比）
}

// 这个结构体定义了单次升级的具体效果
[System.Serializable]
public struct PermanentUpgradeEffect
{
    public PermanentUpgradeType upgradeType;
    public float value;
}

[CreateAssetMenu(fileName = "NewWeaponUpgradeNode", menuName = "Skill Tree/Weapon Upgrade Node")]
public class WeaponUpgradeNode : ScriptableObject
{
    [Header("UI & 描述")]
    public string upgradeName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;

    [Header("升级效果")]
    public List<PermanentUpgradeEffect> effects;

    [Header("解锁与消耗")]
    public int cost;
    [Tooltip("解锁此节点前，必须先解锁的前置节点")]
    public WeaponUpgradeNode prerequisiteNode;

    // 这个字段我们暂时不用，但在未来构建UI时会很有用
    [HideInInspector]

    public Vector2 nodePosition; // 用于在UI中定位节点的位置
}