using System.Collections.Generic;
using UnityEngine;

// 定义武器的核心行为类型
public enum WeaponBehaviorType
{
    Standard,       // 标准弹 (命中后消失)
    Pierce,         // 穿透弹
    ParabolicAOE,   // 抛物线范围伤害弹
    Chain,          // 连锁弹
    Orbital,        // 轨道武器
    PersistentAOE,  // 持续区域 (空投)
    SummonDrone,    // 无人机
    Beam,           // 光束
    Funnel,         //超武浮游炮
    SuperMech,
    Landmine,       // 地雷
    MeleeAOE,       // 近战范围
    Boomerang,      // 回旋镖
    Aura,           // 光环
    CreateAndForget, // <--- 【新增】生成后不管 (用于游荡型超武)
    FlyingDagger,    // <--- 【新增】追踪飞刀 (无需发射，本体撞击)
    FrostNova,       // 冰霜新星 (在敌人脚底爆发冰晶)
    LaserCore        // 镭射核心 (浮空跟随，聚焦升温光束)
}

public enum WeaponXpSource
{
    DamageDealt,  // 造成伤害时获得
    EnemyKilled,  // 击杀敌人时获得
    CastCount     // 释放技能时获得
}


public enum WeaponBuildTag
{
    Slash,
    Spell,
    Fire,
    Ice,
    Lightning,
    Wind,
    Mechanical,
    Deployable,
    Guardian,
    Aura,
    Projectile,
    Control
}

[CreateAssetMenu(fileName = "Weapon_Cannon", menuName = "Weapons/Weapon Stat Block")]
public class WeaponStatBlock : ScriptableObject
{
    [Header("核心标识")]
    [Tooltip("程序逻辑专用的唯一ID，不随语言改变 (例如: Fireball, ExplosiveFireball)")]
    public string weaponID; // <--- 【新增】这里填英文ID

    public string weaponName;
    public Sprite weaponIcon;
    [Tooltip("要挂载到机甲上的武器部件预制件 (WeaponPart Prefab)")]
    public GameObject weaponPartPrefab;

    [Header("Build Tags")]
    [Tooltip("Optional explicit build tags. Leave empty to use inferred tags from weaponID/behavior.")]
    public List<WeaponBuildTag> buildTags = new List<WeaponBuildTag>();

    [Header("视觉表现")]
    [Tooltip("这把武器的代表色 (HDR)。例如：斩击用橙色，疾风之刃用青色。")]
    [ColorUsage(true, true)]
    public Color weaponGlowColor = new Color(1f, 0.5f, 0f) * 2f; // 默认橙色高亮
    [Header("视觉模型")]
    [Tooltip("这把武器在背后漂浮时的模型预制体 (包含MeshRenderer和CooldownMaterial脚本)")]
    public GameObject floatingModelPrefab; // <--- 新增这个字段

    [Header("特殊规则")]
    [Tooltip("勾选后，此技能在释放一次后就会被禁用")]
    public bool isOneShot = false; // 是否为一次性技能
    [Header("开火与冷却")]
    [Tooltip("基础射速 (每秒发射次数)")]
    public float baseFireRate = 1f;


    [Header("弹道与发射属性")]
    [Tooltip("该武器发射的炮弹/子弹的预制件")]

    public GameObject projectilePrefab;
    [Tooltip("是否是抛物线弹道？如果不是，则为直线。")]

    public WeaponBehaviorType behavior; // <--- 新增：取代之前的 isParabolic 布尔值

    [Tooltip("基础发射力度(用于抛物线) 或 基础速度(用于直线弹)")]
    public float baseLaunchForce = 20f;

    [Tooltip("抛物线发射角度 (仅当 isParabolic 为 true 时有效)")]
    public float launchAngle = 45f;

    [Tooltip("每次攻击发射的子弹数量")]
    public int projectileCount = 1;

    [Tooltip("多发子弹的总散射角度 (例如 30 度)")]
    public float spreadAngle = 0f;

    [Header("暴击属性 (Critical Stats)")]
    [Tooltip("武器的基础暴击率 (会与角色暴击率相加)。0.1 代表 10%")]
    [Range(0f, 1f)]
    public float baseCritRate = 0f;

    [Tooltip("武器的基础暴击伤害 (会与角色暴击伤害相加)。0.5 代表 +50%")]
    public float baseCritDamage = 0f;

    [Tooltip("此AOE武器造成眩晕的基础几率 (0 到 1)")]
    [Range(0f, 1f)]
    public float baseStunChance = 0.1f; // 例如 10% 基础几率
    [Tooltip("此AOE武器造成眩晕的基础持续时间（秒）")]
    public float baseStunDuration = 1.0f;

    [Tooltip("子弹基础存活时间")]
    public float baseProjectileLifetime = 5f;

    [Header("伤害与效果")]
    [Tooltip("基础直接命中伤害")]
    public int baseDirectDamage = 0;
    [Tooltip("基础范围爆炸伤害")]
    public int baseAoeDamage = 10;
    [Tooltip("基础爆炸范围半径")]
    public float baseAoeRadius = 3f;
    [Tooltip("基础击退力度")]
    public float baseKnockbackForce = 5f; // <--- 【新增】
    [Tooltip("基础穿透次数。1表示击中第一个敌人后消失。")]
    public int basePierceCount = 1;    
    [Tooltip("乱流/风力回旋生成的小飓风预制件（如果为空则使用主飓风预制件）")]
    public GameObject subHurricanePrefab;
    [Tooltip("连锁攻击的基础跳跃次数。0表示不连锁。")]
    public int baseChainCount = 0; // <--- 新增
    [Tooltip("连锁攻击寻找下一个目标的范围半径。")]
    public float chainRange = 10f; // <--- 新增
    [Tooltip("原生雷击的特效 (当没有插雷石时使用)")]
    public GameObject nativeSmiteVfxPrefab;
    [Tooltip("原生闪电链的连线特效 (当没有插雷石时使用)")]
    public GameObject nativeChainVfxPrefab;
    [Tooltip("原生闪电链的受击/命中特效 (击中敌人时的爆炸/火花)")]
    public GameObject nativeChainImpactVfxPrefab;

    [Header("手雷/抛物线进化特有 (Grenade Evolution)")]

    [Tooltip("爆炸后生成的地面残留物 (例如：凝固汽油弹的火海)")]
    public GameObject groundHazardPrefab; // 复用之前给爆炎斩提过的字段，如果没有请加上
    [Tooltip("地面残留物持续时间")]
    public float groundHazardDuration = 5f;

    [Tooltip("【奇点手雷】是否将击退改为吸力？")]
    public bool isBlackHole = false;
    [Tooltip("吸力强度 (会将怪拉向爆炸中心)")]
    public float blackHoleForce = 15f;

    [Tooltip("【分裂毒爆】爆炸后分裂出的子弹预制体 (毒爆虫)")]
    public GameObject subProjectilePrefab;
    [Tooltip("分裂数量")]
    public int subProjectileCount = 6;

    [Tooltip("分裂子弹的命中/爆炸特效 (例如小型的毒液爆炸)")]
    public GameObject subProjectileHitVfx;

    [Header("特效与层设置")]
    [Tooltip("枪口火焰特效预制件")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("直线弹的命中特效预制件")]
    public GameObject impactEffectPrefab;
    [Tooltip("抛物线弹的爆炸特效预制件")]
    public GameObject explosionEffectPrefab;
    [Tooltip("AOE伤害可以影响的层")]
    public LayerMask layersToDamageByAOE;
    [Tooltip("可以触发爆炸的地面或墙体层")]
    public LayerMask layersToExplodeOn;
    [Tooltip("命中【护盾】时的专属特效")]
    public GameObject shieldImpactEffectPrefab;
    [Tooltip("命中【无护盾目标】或【墙壁】时的通用特效")]
    public GameObject defaultImpactEffectPrefab;

    [Header("进化与融合 (未来扩展)")]
    [Tooltip("此武器的最高等级")]
    public int maxLevel = 10;

    [Header("进阶与进化")]
    [Tooltip("当达到最大等级且满足进化条件时，进化成这把武器")]
    public WeaponStatBlock evolutionTarget;

    [Header("分支选择 (纯经验条模式)")]
    [Tooltip("分支选项A (经验满时可选)")]
    public WeaponStatBlock branchOptionA;
    [Tooltip("分支选项B (经验满时可选)")]
    public WeaponStatBlock branchOptionB;
 // 拖入“爆裂火球”的 SO 文件

    [Header("开火行为 (Firing Behavior)")] // 可以新建一个Header
    [Tooltip("如果勾选，此武器将自动瞄准范围内最近的敌人，忽略鼠标朝向")]
    public bool autoAimAtNearestEnemy = false; // <--- 新增
    [Tooltip("自动瞄准的有效范围半径")]
    public float autoAimRange = 360f; // <--- 新增
    // public WeaponStatBlock evolution;
    // public List<WeaponStatBlock> fusionPartners;
    // public WeaponStatBlock fusionResult;

    [Header("轨道武器属性 (仅 Behavior=Orbital 时有效)")]
    [Tooltip("作为轨道物体的预制件 (例如一个飞轮、斧子、能量球)")]
    public GameObject orbitalPrefab;
    [Tooltip("基础轨道物体数量")]
    public int baseOrbitalCount = 1;
    [Tooltip("基础旋转速度 (度/秒)")]
    public float baseOrbitalSpeed = 90f;
    [Tooltip("基础轨道半径")]
    public float baseOrbitalRadius = 3f;
    [Tooltip("基础持续时间（秒）。0 表示永久存在。")]
    public float baseDuration = 15f; // <--- 新增

    [Header("元素效果 (Elemental Effects)")]
    [Tooltip("点燃敌人的概率 (0 ~ 1)，例如 0.3 代表 30%")]
    [Range(0f, 1f)]
    public float ignitionChance = 0f; // 默认为0，只有火系武器填 0.2 或更高

    [Tooltip("点燃伤害系数 (基于直击伤害的比例)，默认 0.2 (20%)")]
    public float burnDamagePercent = 0.2f;

    [Header("持续伤害效果 (DoT)")]
    [Tooltip("每秒跳伤")]
    public int baseDotDamage = 5;
    [Tooltip("持续时间（秒）")]
    public float baseDotDuration = 3f;
    [Tooltip("每跳间隔时长")]
    public float dotTickInterval = 1f; // 例如每秒造成一次傷害

    [Header("持续范围伤害(PersistentAOE)属性")]
    [Tooltip("从空中落下的部署器预制件 (必须挂载 Projectile 脚本)")]
    public GameObject deployerProjectilePrefab; // <--- 新增
    [Tooltip("伤害区域的预制件 (必须挂载 PersistentAoeField 脚本)")]
    public GameObject areaPrefab;
    [Tooltip("部署器的下落速度")]
    public float deployerFallSpeed = 25f;
    [Tooltip("部署器在目标上空多高处生成")]
    public float deployerSpawnHeight = 40f;
    [Tooltip("傷害區域的基礎持續時間")]
    public float baseAreaDuration = 5f;
    [Tooltip("傷害區域的基礎傷害間隔（每隔多久造成一次傷害）")]
    public float baseAreaTickInterval = 0.5f;
    [Tooltip("傷害區域的基礎每跳傷害")]
    public int baseAreaDamagePerTick = 4;
    [Tooltip("光环的视觉特效预制件")]
    public GameObject auraVfxPrefab;

    [Tooltip("VFX预制件的基础缩放乘数 (用于校准视觉和碰撞器半径)")]
    public float vfxBaseScaleMultiplier = 1.0f;

    [Header("控制效果 (Control Effects)")]
    [Tooltip("基础减速百分比 (例如 0.3 表示减速30%)")]
    [Range(0f, 1f)]
    public float baseSlowPercentage = 0f;
    [Tooltip("基础减速持续时间（秒）")]
    public float baseSlowDuration = 0f;
    public float baseFreezeChance = 0f;

    [Header("召唤设置 (Summon Settings)")]
    [Tooltip("要召唤的无人机/召唤物预制件")]
    public GameObject summonPrefab;
    [Tooltip("每次释放技能时召唤的数量")]
    public int summonCount = 1;
    [Tooltip("召唤物的持续时间（秒），0表示永久")]
    public float summonDuration = 20f;

    [Tooltip("【重要】指定召唤物被召唤出来后，应该使用哪种武器数据 (WeaponStatBlock)")]
    public WeaponStatBlock summonWeaponStats;
    [Tooltip("召唤物生成时，在玩家上方的基础高度")]
    public float summonSpawnHeight = 2f; // 【新增】

    // --- 【新增】光束武器属性 ---
    [Header("光束武器属性 (仅 Behavior=Beam 时有效)")]
    [Tooltip("光束类武器的视觉特效预制件 (需要挂载LineRenderer和LaserBeamController脚本)")]
    public GameObject beamVfxPrefab;

    [Tooltip("光束命中目标时，在命中点产生的持续特效")]
    public GameObject beamImpactVfxPrefab;

    [Tooltip("光束的最大射程")]
    public float beamMaxDistance = 25f;

    [Tooltip("光束每秒造成的伤害值 (DPS)")]
    public int beamDamagePerSecond = 20;

    [Tooltip("光束造成伤害的频率（每秒几次）。数值越高，伤害跳字越频繁。")]
    public float beamDamageTickRate = 5f; // 每秒造成5次伤害

    [Tooltip("光束激活后的持续时间（秒）")]
    public float beamDuration = 3f;
    [Tooltip("光束关闭后的冷却时间（秒）")]
    public float beamCooldown = 5f;

    [Tooltip("光束追踪玩家的转向速度。数值越小，光束转向越慢，玩家越容易躲开。")]
    public float beamTurnSpeed = 5f; // 【新增】
    [Tooltip("光束在地面上移动时留下的印记预制件")]
    public GameObject scorchMarkPrefab; // 【新增】
    [Tooltip("生成印记的时间间隔（秒），数值越小，印记越密集")]
    public float scorchMarkInterval = 0.2f; // 【新增】
    [Tooltip("可以生成印记的地面层")]
    public LayerMask beamScorchMarkGroundLayer; // 【新增】


    [Header("地雷武器属性 (仅 Behavior=Landmine 时有效)")]
    [Tooltip("地雷的预制件 (需要挂载Landmine脚本和Trigger碰撞体)")]
    public GameObject minePrefab;

    [Tooltip("地雷在玩家身边生成的最大半径")]
    public float spawnRadius = 5f;

    [Tooltip("地雷放置后，需要多久才能激活（秒）")]
    public float armingTime = 0.5f;

    [Tooltip("地雷放置后，能存在多久（秒），超时会自动消失")]
    public float mineDuration = 10f;

    [Tooltip("引力黑洞预制件（地雷中阶技能：引力黑洞）")]
    public GameObject blackHolePrefab;

    [Tooltip("凝固汽油弹燃烧区域预制件（地雷融合技能：凝固汽油弹）")]
    public GameObject napalmPrefab;

    [Header("近战范围攻击属性 (仅 Behavior=MeleeAOE 时有效)")]
    [Tooltip("攻击时产生的刀光/挥砍特效预制件")]
    public GameObject slashEffectPrefab;

    [Header("近战 - 高级特性 (Melee Advanced)")]
    [Tooltip("多段攻击次数 (例如雷光刺设为 3)")]
    public int multiHitCount = 1;

    [Tooltip("多段攻击的时间间隔 (秒)")]
    public float multiHitInterval = 0.1f;

    [Tooltip("攻击时是否强制朝向最近的敌人 (用于雷光刺)")]
    public bool autoAimMelee = false;

    /*[Tooltip("地面残留物预制体 (例如爆炎斩留下的燃烧区域)")]
    public GameObject groundHazardPrefab;

    [Tooltip("地面残留物持续时间")]
    public float groundHazardDuration = 3f;*/


    [Tooltip("攻击判定的扇形角度。90代表角色前方90度的锥形。360代表圆形。")]
    [Range(0, 360)]
    public float attackAngle = 90f;
    [Header("特效与层设置")]
    [Tooltip("【通用】当本次攻击命中敌人时，在敌人身上产生的特效")] // <--- 新增
    public GameObject hitEffectPrefab; // <--- 新增

    [Header("刃气弹属性 (由技能树解锁)")]
    [Tooltip("刃气弹的预制件 (必须挂载 Projectile 脚本)")]
    public GameObject bladeEnergyPrefab; // 我们用这个来代替旧的bladeEnergyProjectilePrefab
    [Tooltip("刃气弹的基础伤害")]
    public int bladeEnergyDamage = 10;
    [Tooltip("刃气弹的飞行速度")]
    public float bladeEnergySpeed = 15f;
    [Tooltip("刃气弹的穿透次数")]
    public int bladeEnergyPierceCount = 1;

    [Header("回旋镖属性 (Boomerang Properties - Only if Behavior=Boomerang)")]
    [Tooltip("回旋镖飞出的最大距离 (Max outbound distance)")]
    public float maxDistance = 15f;
    [Tooltip("玩家抓取回旋镖的半径 (Player catch radius on return)")]
    public float catchRadius = 2.5f;
    [Tooltip("回旋镖自身的旋转速度 (度/秒) - Y-axis rotation speed")]
    public float rotationSpeed = 720f;
    [Tooltip("回旋镖飞出的最大距离 (Max outbound distance)")]
    public float returnOvershootDistance = 15f;


    [Header("原生元素属性 (Native Elements)")]
    [Tooltip("勾选后，即使不插火石，武器也自带燃烧效果")]
    public bool nativeBurn = false;

    [Tooltip("勾选后，即使不插风石，武器也自带击退效果")]
    public bool nativeKnockback = false;
    [Tooltip("原生击退力度 (仅当 nativeKnockback 为 true 时有效)")]
    public float nativeKnockbackForce = 10f;
    [Header("元素联动 (Elemental Synergy)")]
    [Tooltip("当此抛射物经过 'BurningGround' 标签的物体上方时，留下的火径预制体")]
    public GameObject synergyFireTrailPrefab;
    [Tooltip("火径生成间隔 (秒/个)，数值越小火径越密集")]
    public float fireTrailSpawnRate = 0.15f;

    [Tooltip("原生雷击触发几率")]
    public float nativeLightningChance = 0.2f;
    [Tooltip("原生是否施加感电")]
    public bool nativeElectrify = false;

    [Tooltip("勾选后，即使不插毒石，武器也自带腐蚀/剧毒效果")]
    public bool nativeCorrode = false;
    [Tooltip("原生腐蚀易伤倍率 (例如 1.2 表示增伤 20%)")]
    public float nativeCorrodeMultiplier = 1.2f;
    [Tooltip("原生腐蚀颜色")]
    public Color nativeCorrodeColor = new Color(0.5f, 1f, 0.5f); // 默认毒液绿

    [Header("熟练度与成长 (Proficiency System)")]
    public bool usesProficiency = true; // 是否启用熟练度系统
    public WeaponXpSource xpSource = WeaponXpSource.DamageDealt;

    [Tooltip("每造成1点伤害/击杀1个/释放1次 获得的经验值")]
    public float xpGainFactor = 1.0f;

    [Tooltip("升级所需经验曲线 (X轴=等级, Y轴=所需经验)")]
    public AnimationCurve xpRequirementCurve;

    [Header("每级成长属性 (Level Up Bonus)")]
    [Tooltip("每级增加的伤害倍率 (0.1 = +10%)")]
    public float damageGrowthPerLevel = 0.1f;
    [Tooltip("每级增加的冷却缩减 (0.05 = -5%)")]
    public float cooldownGrowthPerLevel = 0.05f;
    [Tooltip("每级增加的范围 (0.1 = +10%)")]
    public float areaGrowthPerLevel = 0.1f;

    // =========================================================
    // 【新增】能量蓄力与大招系统 (Energy & Ultimate System)
    // =========================================================
    [Header("能量蓄力系统 (Energy System)")]
    [Tooltip("是否启用能量系统")]
    public bool usesEnergy = true;
    [Tooltip("能量上限")]
    public float maxEnergy = 100f;
    [Tooltip("每造成1点伤害获得的能量值")]
    public float energyGainPerDamage = 0.1f;

    [Header("大招配置 (Ultimate Skill)")]
    [Tooltip("大招效果预制件")]
    public GameObject ultimateEffectPrefab;
    [Tooltip("大招伤害")]
    public int ultimateDamage = 100;
    [Tooltip("大招范围")]
    public float ultimateRadius = 5f;
    [Tooltip("大招技能描述（鼠标悬停图标时显示）")]
    [TextArea(2, 4)]
    public string ultimateDescription;
    [Tooltip("大招技能描述（英文）")]
    [TextArea(2, 4)]
    public string ultimateDescriptionEN;
    [Tooltip("大招图标 (能量满时显示)")]
    public Sprite ultimateIcon;

    // =========================================================
    // 【新增】音效设置 (Sound Effects)
    // =========================================================
    [Header("音效设置 (Sound Effects)")]
    [Tooltip("技能释放/开火时的音效（支持多个随机播放）")]
    public AudioClip[] fireSounds;

    [Tooltip("命中敌人时的音效")]
    public AudioClip hitSound;

    [Tooltip("特殊音效（爆炸、冰冻、雷击落地等）")]
    public AudioClip specialSound;

    [Tooltip("开火音效的音量缩放 (默认1.0)")]
    [Range(0f, 2f)]
    public float fireSoundVolume = 1.0f;
}
