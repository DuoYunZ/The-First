using System.Collections.Generic;
using UnityEngine;

// 定义武器的核心行为类型
public enum WeaponBehaviorType
{
    Standard,   // 标准弹 (命中后消失)
    Pierce,     // 穿透弹
    ParabolicAOE, // 抛物线范围伤害弹 (我们的火炮)
    Chain,       // 连锁弹
    Orbital, // <--- 轨道武器
    PersistentAOE,
    SummonDrone, // 无人机
    Beam
}


[CreateAssetMenu(fileName = "Weapon_Cannon", menuName = "Weapons/Weapon Stat Block")]
public class WeaponStatBlock : ScriptableObject
{
    public string weaponName;
    public Sprite weaponIcon;
    [Tooltip("要挂载到机甲上的武器部件预制件 (WeaponPart Prefab)")]
    public GameObject weaponPartPrefab;

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

    [Tooltip("子弹基础存活时间")]
    public float baseProjectileLifetime = 5f;

    [Header("伤害与效果")]
    [Tooltip("基础直接命中伤害")]
    public int baseDirectDamage = 0;
    [Tooltip("基础范围爆炸伤害")]
    public int baseAoeDamage = 10;
    [Tooltip("基础爆炸范围半径")]
    public float baseAoeRadius = 3f;
    [Tooltip("基础穿透次数。1表示击中第一个敌人后消失。")]
    public int basePierceCount = 1;    
    [Tooltip("连锁攻击的基础跳跃次数。0表示不连锁。")]
    public int baseChainCount = 0; // <--- 新增
    [Tooltip("连锁攻击寻找下一个目标的范围半径。")]
    public float chainRange = 10f; // <--- 新增

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
    public int maxLevel = 8;

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

    [Header("控制效果 (Control Effects)")]
    [Tooltip("基础减速百分比 (例如 0.3 表示减速30%)")]
    [Range(0f, 1f)]
    public float baseSlowPercentage = 0f;
    [Tooltip("基础减速持续时间（秒）")]
    public float baseSlowDuration = 0f;

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
}