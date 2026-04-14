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
    BurstCount,         // 【新增】连射次数（一次开火连续释放多次）
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
    Stun,                   // 眩晕
    SubProjectile,           // 【新增】分裂子弹（开启分裂）
    SubProjectileCount,      // 【新增】分裂数量
    IgnitionChance,          // 【新增】点燃概率加成
    BurnDuration,            // 【新增】燃烧持续时间加成
    MaxHealthBurn,           // 【新增】猛烈燃烧（基于目标最大生命值的百分比伤害）
    FreezeChance,            // 【新增】冰冻概率加成
    SubProjectileDamageBonus,// 【新增】分裂子弹伤害加成（百分比）
    SubProjectileInherit,    // 【新增】分裂子弹继承母弹属性（穿透/冰冻等）

    // 雷击类
    StunDuration,            // 眩晕/麻痹持续时间加成
    LightningRepeatCount,    // 连续雷击次数（每次落雷后0.3秒再落一道）
    MagneticStormBurst,      // 磁暴（一次性范围攻击，落雷后触发）
    ElectricField,           // 电磁场（持续范围效果，提升暴击率）
    ElectricFieldDamage,     // 电磁场伤害加成
    ElectricFieldDuration,   // 电磁场持续时间加成
    OnKillChainLightning,    // 击杀时触发连锁雷击

    // 飓风术类
    KnockbackForce,          // 击退力度加成
    VacuumPull,              // 真空牵引（命中时吸引周围怪物）
    VacuumDamage,            // 真空伤害加成
    WindReturn,              // 风力回旋（穿透耗尽后向其他方向释放一道飓风）
    Turbulence,              // 乱流（命中时生成小飓风）
    TurbulenceIntensify,     // 乱流加剧（每次命中都生成小飓风）

    // 榴弹类
    GrenadeBounce,           // 弹跳榴弹（爆炸后弹跳再炸一次）

    // 闪电链类
    ChainCount,              // 弹射次数增加
    IonExplosion,            // 离子爆破（开启链式命中爆炸）
    IonExplosionDamage,      // 离子爆破伤害加成
    IonExplosionRadius,      // 离子爆破范围加成

    // 冰霜新星类
    FrostNovaExtraCast,      // 冰霜新星额外释放次数
    FreezeDuration,          // 冻结持续时间增加
    FrostNovaCenterDamage,   // 寒霜之心（中心区域额外伤害）
    AbsoluteZero,            // 绝对零度（中心区域冻结翻倍）

    // 冰霜融合类（冰霜新星+冰锥术）
    FrostBite,               // 刺骨寒霜（冻结怪物每秒扣1%最大生命值）
    IceCrystalShatter,       // 冰晶碎裂（新星结束后分裂小冰锥数量）
    CooldownReduction,       // 冷却缩减（百分比）

    // 环绕武器类
    OrbitalAbsorbProjectiles,  // 动能吸附（摧毁弹射物延长持续时间）
    OrbitalExpansionBreathing, // 引力呼吸（半径周期性改变）
    OrbitalReleaseExplosion,   // 充能释放（结束时释放爆炸）

    // 地雷类
    LandmineEnergyRecovery,    // 能量回收（击杀时有概率获得额外大招能量）
    LandmineStun,              // 震撼弹片（爆炸附加眩晕）
    LandmineGravityTrap,       // 引力陷阱（武装后微弱吸引怪物）
    LandmineBlackHole,         // 引力黑洞（爆炸后留下黑洞吸引怪物）

    // 地雷融合类（地雷+火球）
    FusionNapalm,              // 凝固汽油弹（爆炸后留下燃烧区域）

    // Aura辅助型光环类（通过 value 区分等级：1=I级，2=II级）
    AuraHealingPulse,          // 生命脉动（value=1: 回3血, value=2: 回6血）
    AuraSluggishField,         // 迟缓力场（value=1: 减速25%, value=2: 减速35%）
    AuraFragileMark,           // 脆弱印记（value=1: 增伤8%, value=2: 增伤15%）

    // 灵能飞刀类（通过 value 区分等级）
    DaggerDamageBoost,         // 烈焰增幅（value=1: 伤害+30%速度-15%, value=2: 伤害+60%速度-25%）
    DaggerExtraCount,          // 多重飞刀（value=1: +1刀伤害-15%, value=2: +2刀伤害-25%）
    DaggerSpeedBoost,          // 焰舞加速（value=1: 速度x1.3间隔-20%, value=2: 速度x1.6间隔-35%）
    DaggerHoming,              // 锁魂追击（索敌+50%，锁定+2秒，半径-50%）
    DaggerClone,               // 刃影分身（1%生成分身，半径-50%）
    DaggerIgnite,              // 灵能烙印（飞刀点燃，需火球Ignite前置）
    DaggerLifeSteal,           // 灵魂收割（击杀回2%最大HP）
    DaggerChainExplosion,      // 连锁灵刃（命中点燃敌人爆破）

    // ============================================================
    // 被动道具 — 触发机制型
    // ============================================================
    BerserkerHeart,            // 狂战士之心（生命值低于50%时增伤）
    FlameTrail,                // 燃烧轨迹（移动时留下火焰区域）
    ThornsDamage,              // 荆棘护甲（受伤时反弹伤害给攻击者）
    KillHeal,                  // 灵魂汲取（击杀敌人回血）
    GlobalFreezeChance,        // 冰霜之触（所有攻击附加冰冻概率）
    ThunderWill,               // 雷霆意志（击杀时有概率在原地召唤雷击AOE）
    LifeStealPassive,          // 吸血之牙（造成伤害吸血，与角色技能树吸血叠加）
    DashExplosion,             // 冲刺余烬（冲刺结束后释放冲击波）

    // 被动道具 — 职业联动型
    SwordmasterSoul,           // 剑圣之魂（斩击命中多个敌人后短暂攻速提升）
    ArcaneMastery,             // 奥术精通（射弹命中时有概率产生小型爆炸）
    ElementalResonance,        // 元素共鸣（拥有多种元素武器时全局增伤）
    MechanicalResonance        // 机械共鸣（部署类武器数量+1、冷却缩减）
}