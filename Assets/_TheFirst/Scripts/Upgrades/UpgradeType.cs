/// <summary>
/// 定义升级效果的类型，这有助于我们在代码中根据类型来应用不同的强化逻辑。
/// </summary>
public enum UpgradeType
{
    // 武器类
    WeaponDamage,       // 武器伤害
    AoeDamage,          // 范围伤害
    AoeRadius,          // 爆炸范围
    WeaponFireRate,     // 武器射速
    WeaponProjectileSpeed, // 子弹速度
    AddProjectile,      // 增加发射物数量
    PierceCount,        // 穿透
    SlashCount,         // 刀光数量
    OrbitalCount,       // 轨道武器数量
    OrbitalSpeed,
    WeaponDuration,
    CritRate,
    CritDamage,

    // 机甲属性类
    MaxHealth,          // 最大生命值
    UnlockShield,       // 解锁护盾
    MaxShield,          // 最大护盾值
    ShieldCooldown,     // 护盾冷却缩减
    Armor,              // 护甲/减伤
    MoveSpeed,          // 移动速度
    Revival,            // 【新增】复活次数 (解决报错)

    // 资源/拾取类
    PickupRadius,       // 拾取半径
    ExperienceGain,     // 经验获取率
    Luck,               // 【新增】幸运值

    // 特殊机制
    BoomerangStackUpgrade, // 回旋镖叠加规则
    ParabolicAoeStunChance, // 抛物线眩晕几率
    Stun                   // 眩晕
}