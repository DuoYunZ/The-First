using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponPart : MonoBehaviour
{
    [Header("武器数据蓝图 (在预制件中设置)")]
    public WeaponStatBlock myStatBlock;

    public int currentLevel = 1;
    public int maxLevel = 10;

    [Header("组件引用 (在预制件中设置)")]
    public Transform firePoint;

    [Header("视觉表现组件 (可选)")]
    [Tooltip("控制背后悬浮武器动画的脚本")]
    public FloatingWeaponController floatingVisual;
    [Tooltip("控制材质发光冷却的脚本")]
    public WeaponCooldownMaterial cooldownMaterial;
    [Tooltip("开火时隐藏武器模型的时间 (模拟挥砍/发射动作)")]
    public float hideVisualDuration = 0.15f; // 远程通常比近战快，0.15秒比较合适

    [Header("特效预制件")]
    [Tooltip("连锁闪电的视觉特效预制件")]
    public GameObject lightningChainPrefab;

    [Header("音效设置")]
    [Tooltip("标准、穿透、抛物线等发射型武器的开火音效")]
    public AudioClip[] fireSounds;
    [Tooltip("放置地雷的音效")]
    public AudioClip landminePlaceSound;
    [Tooltip("光束武器持续发出的循环音效")]
    public AudioClip beamLoopSound;
    [Tooltip("开火音效相对于子弹发射的延迟（秒）。负数为提前播放。")]
    public float fireSoundDelay = -0.05f;

    [Header("光环状态 (运行时)")]
    private float auraDebuffRefreshTimer = 0f; //
    private HashSet<StatusEffectReceiver> aura_ActiveSlows = new HashSet<StatusEffectReceiver>(); //
    private HashSet<StatusEffectReceiver> aura_ActiveWeaKens = new HashSet<StatusEffectReceiver>(); //
    private HashSet<StatusEffectReceiver> aura_ActiveCorrodes = new HashSet<StatusEffectReceiver>(); // <--- [新增]
    private float auraTickTimer = 0f;
    private float lightningStrikeTimer = 0f;
    private SphereCollider auraCollider;
    private GameObject auraVfxInstance;
    [Tooltip("光环只应检测这些层上的敌人")]
    public LayerMask enemyLayerMask; // <--- vvv 新增

    [Tooltip("光环磁铁应检测的掉落物层")]
    public LayerMask pickupLayerMask;

    private AudioSource audioSource;

    [Header("召唤物与特殊武器管理")]
    // --- 【新增】修复 activeDrones 报错 ---
    private List<GameObject> activeDrones = new List<GameObject>();
    private List<GameObject> activeFlyingDaggers = new List<GameObject>(); // <--- 【新增】飞刀列表

    private List<GameObject> activeLaserTanks = new List<GameObject>();

    private List<GameObject> activeFunneIs = new List<GameObject>(); // 浮游炮列表

    private PlayerBeamController currentBeamInstance;

    private GameObject currentSuperMech;

    [Header("能量石 (运行时)")]
    [Tooltip("此武器当前镶嵌的能量石")]
    public EnergyStoneSO currentStone { get; private set; }

    private float auraKnockbackTimer = 0f;

    private float auraMagnetTimer = 0f;

    [Header("运行时局部属性 (Local Stats)")]
    [HideInInspector] public float localCritRateBonus = 0f;
    [HideInInspector] public float localCritDamageBonus = 0f;

    [HideInInspector] public int localOrbitalCountBonus = 0;
    [HideInInspector] public int localSlashCountBonus = 0;   // 【新增】给近战刀光用的
    [HideInInspector] public float localDamageBonus = 0f;      // 局部伤害加成 (0.1 = +10%)
    [HideInInspector] public float localFireRateBonus = 0f;    // 局部攻速/冷却加成
    [HideInInspector] public float localAreaBonus = 0f;        // 局部范围加成
    [HideInInspector] public float localSpeedBonus = 0f;       // 局部飞行/旋转速度加成 (OrbitalSpeed 用这个)
    [HideInInspector] public float localDurationBonus = 0f;    // 局部持续时间加成
    [HideInInspector] public float localStunChanceBonus = 0f; // 局部麻痹概率
    [HideInInspector] public float localFreezeChanceBonus = 0f; // 【新增】冰冻概率加成
    [HideInInspector] public int localPierceCountBonus = 0; // 这个你应该早就有了，如果没有请加上
    [HideInInspector] public int localBurstCountBonus = 0; // 【新增】连射次数加成（连珠火球用）
    [HideInInspector] public int localSubProjectileCountBonus = 0; // 【新增】分裂子弹数量加成
    [HideInInspector] public bool isSubProjectileEnabled = false; // 【新增】是否开启分裂
    [HideInInspector] public float localIgnitionChanceBonus = 0f; // 【新增】点燃概率加成
    [HideInInspector] public float localBurnDurationBonus = 0f;   // 【新增】燃烧持续时间加成（秒）
    [HideInInspector] public float localMaxHealthBurnPercent = 0f; // 【新增】猛烈燃烧：基于最大生命值的百分比伤害
    [HideInInspector] public float localSubProjectileDamageBonus = 0f; // 【新增】分裂子弹伤害加成（百分比，如 0.8 = +80%）
    [HideInInspector] public bool subProjectileInheritEnabled = false; // 【新增】分裂子弹是否继承母弹属性（穿透/冰冻等）

    // 雷击类
    [HideInInspector] public int localLightningRepeatCount = 0;  // 连续雷击次数（落雷后0.3秒再落一道）
    [HideInInspector] public float localStunDurationBonus = 0f;  // 眩晕持续时间加成
    [HideInInspector] public bool isMagneticStormEnabled = false; // 磁暴开关（落雷后触发范围爆炸）
    [HideInInspector] public bool isElectricFieldEnabled = false; // 电磁场开关（磁暴后生成暴击率提升区域）
    [HideInInspector] public float localElectricFieldDamageBonus = 0f; // 电磁场伤害加成
    [HideInInspector] public float localElectricFieldDurationBonus = 0f; // 电磁场持续时间加成
    [HideInInspector] public bool isOnKillChainEnabled = false;  // 击杀时触发连锁雷击

    // 飓风术类
    [HideInInspector] public float localKnockbackBonus = 0f;     // 击退力度加成
    [HideInInspector] public bool isVacuumPullEnabled = false;   // 真空牵引开关
    [HideInInspector] public float localVacuumDamageBonus = 0f;  // 真空伤害加成
    [HideInInspector] public bool isWindReturnEnabled = false;   // 风力回旋开关
    [HideInInspector] public int localTurbulenceLevel = 0;       // 乱流等级（0=无，1=乱流，2+=乱流加剧）

    // 榴弹类
    [HideInInspector] public int localBounceCount = 0;           // 弹跳次数（爆炸后再弹跳几次）

    // 闪电链类
    [HideInInspector] public int localChainCountBonus = 0;       // 弹射次数加成
    [HideInInspector] public bool localIonExplosionEnabled = false; // 离子爆破开关
    [HideInInspector] public float localIonExplosionDamageBonus = 0f; // 离子爆破伤害加成
    [HideInInspector] public float localIonExplosionRadiusBonus = 0f; // 离子爆破范围加成

    // 冰霜新星类
    [HideInInspector] public int localFrostNovaExtraCast = 0;        // 额外释放次数（连环霜爆）
    [HideInInspector] public float localFreezeDurationBonus = 0f;    // 冻结持续时间加成
    [HideInInspector] public bool localFrostNovaCenterDmg = false;   // 寒霜之心（中心额外伤害）
    [HideInInspector] public bool localAbsoluteZero = false;         // 绝对零度（中心冻结翻倍）

    // 冰霜融合类
    [HideInInspector] public bool localFrostBite = false;            // 刺骨寒霜（冻结扣血）
    [HideInInspector] public int localIceCrystalShatter = 0;         // 冰晶碎裂（分裂冰锥数量）
    [HideInInspector] public float localCooldownReduction = 0f;      // 冷却缩减（百分比小数）

    // 环绕武器类 (Orbiter)
    [HideInInspector] public bool isOrbitalAbsorbEnabled = false;    // 动能吸附开关
    [HideInInspector] public bool isOrbitalBreathingEnabled = false; // 引力呼吸开关
    [HideInInspector] public bool isOrbitalReleaseEnabled = false;   // 充能释放开关
    [HideInInspector] public float orbitalSpeedMultiplier = 1f;      // 终极：旋转速度倍率
    [HideInInspector] public bool isUltimateBuffActive = false;      // BUFF型大招激活中（阻止能量积累）

    // 地雷类 (Landmine)
    [HideInInspector] public bool isMineEnergyRecovery = false;      // 能量回收开关
    [HideInInspector] public bool isMineStun = false;                // 震撼弹片开关
    [HideInInspector] public bool isMineGravityTrap = false;         // 引力陷阱开关
    [HideInInspector] public bool isMineBlackHole = false;           // 引力黑洞开关
    [HideInInspector] public bool isMineFusionNapalm = false;        // 凝固汽油弹开关
    [HideInInspector] public int localMineCountBonus = 0;            // 雷区扩张：额外地雷数量

    // Aura辅助型光环类（值由 SO 的 value 字段直接控制）
    [HideInInspector] public float auraHealAmount = 0;              // 生命脉动回血量（0=未开启）
    [HideInInspector] public float auraSlowPercent = 0;             // 迟缓力场减速百分比（0=未开启，如25表示25%减速）
    [HideInInspector] public float auraFragilePercent = 0;          // 脆弱印记增伤百分比（0=未开启，如8表示8%增伤）

    // === 灵能飞刀升级字段 ===
    [HideInInspector] public float daggerDamageBoost = 0;           // 烈焰增幅伤害加成百分比（30/60）
    [HideInInspector] public float daggerSpeedPenalty = 0;          // 烈焰增幅速度惩罚百分比（15/25）
    [HideInInspector] public int daggerExtraCount = 0;              // 多重飞刀额外数量（1/2）
    [HideInInspector] public float daggerCountDmgPenalty = 0;       // 多重飞刀伤害惩罚百分比（15/25）
    [HideInInspector] public float daggerSpeedBoost = 0;            // 焰舞加速倍率（1.3/1.6）
    [HideInInspector] public float daggerIntervalReduction = 0;     // 焰舞加速间隔缩减百分比（20/35）
    [HideInInspector] public bool daggerHomingUpgrade = false;      // 锁魂追击
    [HideInInspector] public bool daggerCloneUpgrade = false;       // 刃影分身
    [HideInInspector] public bool daggerIgniteUpgrade = false;      // 灵能烙印（需火球前置）
    [HideInInspector] public bool daggerLifeStealUpgrade = false;   // 灵魂收割
    [HideInInspector] public bool daggerChainExplosion = false;     // 连锁灵刃

    // === 镭射核心升级字段 ===
    [HideInInspector] public int localLaserRefractionCount = 0;      // 棱镜折射目标数量
    [HideInInspector] public float localLaserFocusBonus = 0f;        // 聚焦每层加成提升（如0.05表示每层+5%）
    [HideInInspector] public bool localLaserMeltdownEnabled = false;  // 核心熔毁：过热时生成灼烧区域
    private List<GameObject> activeLaserCores = new List<GameObject>(); // 已生成的镭射核心列表

    // 由 WeaponController 在运行时赋值
    public WeaponStatBlock StatBlock
    {
        get { return myStatBlock; }
        set { myStatBlock = value; }
    }   

    private bool isBoomerangOut = false;
    private int activeBoomerangCount = 0;
    [HideInInspector] public float boomerangReturnDamageBonus = 0f;
    [HideInInspector] public int boomerangReturnPulseEveryHits = 0;
    [HideInInspector] public float boomerangReturnPulseDamageMultiplier = 0f;
    [HideInInspector] public float boomerangReturnPulseRadius = 0f;
    [HideInInspector] public float boomerangRecallBurstDamageMultiplier = 0f;
    [HideInInspector] public float boomerangRecallBurstRadius = 0f;
    private float fireCooldown = 0f;
    private float orbitalCooldownTimer = 0f;
    private bool isOrbitalActive = false;
    private Transform orbitalPivot;
    private float currentOrbitalDuration = 0f;

    /// <summary>
    /// 获取光环上的指定组件（由 UltimateManager 调用）
    /// </summary>
    public T GetAuraComponent<T>() where T : Component
    {
        if (orbitalPivot == null) return null;
        return orbitalPivot.GetComponent<T>();
    }

    public void ExtendOrbitalDuration(float extraTime)
    {
        if (isOrbitalActive)
        {
            currentOrbitalDuration += extraTime;
        }
    }

    public bool IsReadyToFire => fireCooldown <= 0f;

    private PlayerBeamController activeBeamInstance = null;
    private float beamEnergyTimer = 0f;
    private float beamCooldownTimer = 0f;
    private Transform lockedBeamTarget = null;

    [Header("运行时熟练度 (Runtime Proficiency)")]
    public int currentProficiencyLevel = 1;
    public float currentProficiencyXP = 0f;
    public float xpToNextLevel = 200f; // 初始值 (Base阶段默认200)

    [Header("武器阶段 (Weapon Stage)")]
    public WeaponStage currentStage = WeaponStage.Base; // 当前阶段
    public bool hasBranched = false; // 是否已选择分支

    [Header("大招解锁状态")]
    [Tooltip("是否已解锁大招（需要先集齐5颗宝石才能积累能量）")]
    public bool isUltimateUnlocked = false;

    // 事件：当武器升级时触发 (用于播放特效、UI更新)
    public System.Action<int> OnWeaponLevelUp;
    // 事件：当经验变化时 (用于UI条)
    public System.Action<float, float> OnWeaponXpChanged;
    // 事件：当需要选择分支时 (用于弹出分支选择UI)
    public System.Action<WeaponPart> OnBranchChoiceRequired;

    // =========================================================
    // 【新增】能量蓄力系统 (Energy System)
    // =========================================================
    [Header("能量蓄力 (Energy)")]
    public float currentEnergy = 0f;
    public bool IsEnergyFull => StatBlock != null && StatBlock.usesEnergy && currentEnergy >= StatBlock.maxEnergy;
    // 事件：当能量变化时 (current, max)
    public System.Action<float, float> OnEnergyChanged;
    // 事件：当能量满时
    public System.Action<WeaponPart> OnEnergyFull;

    #region Unity Lifecycle Methods

    public void Activate()
    {
        if (StatBlock != null)
        {
            // 雷击升级
            if (StatBlock.weaponID == "LightningStrike")
            {
                ApplyLightningStrikeMetaUpgrades();
            }

            // 燃烧瓶升级 (虽然它在Start也能跑，但为了统一建议也搬过来)
            if (StatBlock.weaponID == "Molotov")
            {
                ApplyMolotovMetaUpgrades();
            }
            if (StatBlock.weaponID == "IceShard")
            {
                ApplyIceShardMetaUpgrades();
            }
        }

        gameObject.SetActive(true);

        // --- 1. 这里是初始化实体模型的地方 ---
        if (StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            SetupOrbiters();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.SummonDrone)
        {           
            SetupDrones();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.FlyingDagger) // <--- 【新增】
        {
            SetupFlyingDaggers(); // 刷新飞刀（数量变化时）
        }
        // 【直接在这里添加 Aura 的判断】
        else if (StatBlock.behavior == WeaponBehaviorType.Aura)
        {
            SetupAura();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            SetupAutoBeam();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.LaserCore)
        {
            SetupLaserCore();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.Funnel) // 假设你加了这个枚举，或者暂时复用 Beam
        {
            SetupFunnelSystem();
        }
        else if (StatBlock.behavior == WeaponBehaviorType.SuperMech)
        {
            SetupSuperMech();
        }
        // --- 2. 初始化光束计时器 ---
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            beamEnergyTimer = StatBlock.beamDuration;
            beamCooldownTimer = 0;
        }

        // --- 3. 你原本的 Orbital 保护逻辑 (保留即可) ---
        // 注意：因为上面已经判断过一次 SetupOrbiters 了，这里的 !isOrbitalActive 会起作用防止重复生成，
        // 但如果 behavior 是 Aura，这里条件不满足，直接跳过，所以逻辑是安全的。
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            if (!isOrbitalActive && orbitalCooldownTimer <= 0)
            {
                SetupOrbiters();
            }
        }

        // --- 4. 初始化回旋镖 ---
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Boomerang)
        {
            isBoomerangOut = false;
        }

        // --- 5. 初始化武器阶段和经验 ---
        // 重置阶段状态（防止预制件序列化值影响）
        currentStage = WeaponStage.Base;
        hasBranched = false;
        currentProficiencyXP = 0f;
        CalculateNextLevelXP();        

        // --- 6. 自动注册分支选择事件 ---
        if (WeaponBranchManager.Instance != null)
        {
            WeaponBranchManager.Instance.RegisterWeapon(this);
        }
    }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
        }

        auraCollider = GetComponent<SphereCollider>();
        if (StatBlock == null) return;

       
        if (StatBlock.weaponID == "Fireball" && IsMetaUnlocked("Fireball_Meta_StartEvolved"))
        {
            if (StatBlock.evolutionTarget != null)
            {
                StatBlock = StatBlock.evolutionTarget;
            }
        }
        if (StatBlock.weaponID == "LightningStrike" && IsMetaUnlocked("Lightning_Meta_StartEvolved"))
        {
            if (StatBlock.evolutionTarget != null)
            {
               
                StatBlock = StatBlock.evolutionTarget;
            }
        }
        if (StatBlock.weaponID == "IceShard" && IsMetaUnlocked("Ice_Meta_StartEvolved"))
        {
            if (StatBlock.evolutionTarget != null)
            {
                StatBlock = StatBlock.evolutionTarget;
               
            }
        }
    }
    void Start()
    {
        if (StatBlock != null) maxLevel = StatBlock.maxLevel;
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam) beamEnergyTimer = StatBlock.beamDuration;
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Aura) SetupAura();

        // Robust Component Finding
        if (floatingVisual == null) floatingVisual = GetComponentInChildren<FloatingWeaponController>();
        if (cooldownMaterial == null) cooldownMaterial = GetComponentInChildren<WeaponCooldownMaterial>();

        // Initialize Color from Weapon Data
        if (cooldownMaterial != null && StatBlock != null)
        {
            // Default to StatBlock color
            if (StatBlock.weaponGlowColor.maxColorComponent > 0)
                cooldownMaterial.SetEmissionColor(StatBlock.weaponGlowColor);
        }
        if ((StatBlock.weaponName == "Fireball" || StatBlock.weaponName == "火球术") && IsMetaUnlocked("Fireball_Meta_StartEvolved"))
        {
            if (StatBlock.evolutionTarget != null)
            {
               
                StatBlock = StatBlock.evolutionTarget; // 直接替换数据蓝图
            }
        }

        // Demo tuning: all weapons can fill two gem rounds.
        if (StatBlock != null)
        {
            maxLevel = Mathf.Max(10, StatBlock.maxLevel);
        }

        // 1. 火球术攻击力提升 (作为局部加成)
        if ((StatBlock.weaponName == "Fireball" || StatBlock.weaponName == "火球术") && IsMetaUnlocked("Fireball_Meta_Damage"))
        {
            // 假设提升 20% 伤害
            localDamageBonus += 0.2f;
        }
                
        UpdateVisualModel();
        CalculateNextLevelXP();
    }

    private void ApplyLightningStrikeMetaUpgrades()
    {
        // 1. 初始伤害 +30% (Lv1 节点)
        // 假设节点名叫 "Lightning_Meta_Damage"
        if(IsMetaUnlocked("Lightning_Meta_Damage"))
        {
            // 原来是 localDamageBonus += 0.3f;
            // 现在改为加暴击率 (例如 +10% 或 +20%)
            localCritRateBonus += 0.2f;
            
        }

        // 2. 麻痹概率 +10% (假设这是 Lv3 或 Lv5 节点)
        // 假设节点名叫 "Lightning_Meta_Stun"
        if (IsMetaUnlocked("Lightning_Meta_Stun"))
        {
            localCritRateBonus += 0.3f; 
            
        }

        // 如果还有其他升级(如范围、频率)，按同样方式写
    }

    private void ApplyIceShardMetaUpgrades()
    {
        // 1. 增加穿透 (Lv1)
        if (IsMetaUnlocked("Ice_Meta_Pierce"))
        {
            localPierceCountBonus += 1; // 假设你有这个变量，如果没有，请定义它
           
        }

        // 2. 增加冰冻概率 (Lv3)
        if (IsMetaUnlocked("Ice_Meta_Freeze"))
        {
            localFreezeChanceBonus += 0.15f; // +15% 概率
            
        }
    }
    private void ApplyMolotovMetaUpgrades()
    {
        // 1. 伤害 (Damage)
        // 假设节点文件叫 "Molotov_Meta_Damage"，提升 50%
        if (IsMetaUnlocked("Molotov_Meta_Damage"))
        {
            localDamageBonus += 0.5f;
           
        }

        // 2. 冷却/攻速 (Cooldown / FireRate)
        // 假设节点文件叫 "Molotov_Meta_Cooldown"，攻速提升 50%
        if (IsMetaUnlocked("Molotov_Meta_Cooldown"))
        {
            localFireRateBonus += 0.5f;
            
        }

        // 3. 范围 (Area)
        // 假设节点文件叫 "Molotov_Meta_Area"，范围扩大 100%
        if (IsMetaUnlocked("Molotov_Meta_Area"))
        {
            localAreaBonus += 1f;
           
        }

        // 4. 持续时间 (Duration)
        // 假设节点文件叫 "Molotov_Meta_Duration"，持续时间增加 100%
        if (IsMetaUnlocked("Molotov_Meta_Duration"))
        {
            localDurationBonus += 1f;
           
        }
    }

    public float GetIgnitionChance()
    {
        if (StatBlock == null) return 0f;

        float chance = StatBlock.ignitionChance;

        string nodeName = "Fireball_Meta_Ignite";
        bool isNodeUnlocked = IsMetaUnlocked(nodeName);

        bool isFireball = (StatBlock.weaponID == "Fireball" || StatBlock.weaponName == "火球术");
        // ---------------------------------------------------------
        // 3. 应用加成
        // ---------------------------------------------------------
        if (isFireball && isNodeUnlocked)
        {
            chance += 0.3f; // 加上 30%
        }

        // 【新增】叠加局部点燃概率加成（来自技能树 UpgradeType.IgnitionChance）
        chance += localIgnitionChanceBonus;

        return chance;
    }

    public void GainProficiencyXP(float amount)
    {
        if (StatBlock == null) return;

        // 伤害转为能量蓄力（用于大招），不再触发武器独立升级
        GainEnergy(amount);
    }

    public bool IsCharacterSkillActive(string skillIdentifier)
    {
        return UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill(skillIdentifier);
    }

    private bool IsEngineerSkillActive(string skillIdentifier)
    {
        return IsCharacterSkillActive(skillIdentifier);
    }

    private float GetEngineerMechanicalFireRateBonus()
    {
        if (!IsEngineerSkillActive("Engineer_Talent_AssemblyLine")) return 0f;
        return IsMechanicalWeaponFamily() ? 0.18f : 0f;
    }

    private bool IsMechanicalWeaponFamily()
    {
        if (StatBlock == null) return false;
        if (WeaponBuildTagUtility.IsMechanicalWeapon(StatBlock)) return true;

        string id = StatBlock.weaponID ?? "";
        string weaponName = StatBlock.weaponName ?? "";
        return id.Contains("Landmine") || id.Contains("Laser") || id.Contains("Beam") ||
               id.Contains("Orbit") || id.Contains("Turret") || id.Contains("Drone") ||
               id.Contains("SuperMech") || weaponName.Contains("地雷") || weaponName.Contains("炮塔") ||
               weaponName.Contains("喷火塔") || weaponName.Contains("塔") || weaponName.Contains("Beam") ||
               weaponName.Contains("镭射") || weaponName.Contains("盾");
    }

    private bool IsFlameTurretWeapon()
    {
        if (StatBlock == null) return false;

        string id = StatBlock.weaponID ?? "";
        string weaponName = StatBlock.weaponName ?? "";
        return id.Contains("FlameTurret") || weaponName.Contains("喷火塔");
    }

    private void SpawnEngineerSupportFlameTurret(Vector3 origin, Vector3 baseScale, int finalDamage, float finalLifetime, float finalRange, float finalInterval)
    {
        if (StatBlock == null || StatBlock.projectilePrefab == null) return;

        Vector2 offset2D = Random.insideUnitCircle.normalized;
        if (offset2D.sqrMagnitude < 0.01f) offset2D = Vector2.right;

        Vector3 spawnPos = origin + new Vector3(offset2D.x, 0f, offset2D.y) * 2.2f;
        RaycastHit hit;
        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out hit, 10f, LayerMask.GetMask("Ground", "Default", "Terrain")))
        {
            spawnPos = hit.point;
        }

        GameObject supportObj = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        supportObj.transform.localScale = baseScale * 0.9f;

        FlamethrowerTurret supportTurret = supportObj.GetComponent<FlamethrowerTurret>();
        if (supportTurret == null)
        {
            Destroy(supportObj);
            return;
        }

        supportTurret.Initialize(
            Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.75f)),
            Mathf.Max(1f, finalLifetime * 0.65f),
            WeaponController.Instance != null ? WeaponController.Instance.gameObject : this.gameObject,
            finalRange * 0.9f,
            finalInterval,
            StatBlock.weaponName
        );
    }

    /// <summary>
    /// 【新增】获得能量（伤害或特色行为触发）
    /// </summary>
    public void GainEnergy(float damageAmount)
    {
        if (StatBlock == null || !StatBlock.usesEnergy) return;
        if (!isUltimateUnlocked) return; // 大招未解锁前不积累能量
        if (IsEnergyFull) return; // 已满，不再累加
        if (isUltimateBuffActive) return; // BUFF型大招期间不积累能量

        float energyGain = damageAmount * StatBlock.energyGainPerDamage;
        // 应用角色技能树的能量获取倍率
        if (PlayerStats.Instance != null)
            energyGain *= PlayerStats.Instance.energyGainMultiplier;
        currentEnergy = Mathf.Min(currentEnergy + energyGain, StatBlock.maxEnergy);

        // 通知 UI 更新
        OnEnergyChanged?.Invoke(currentEnergy, StatBlock.maxEnergy);

        // 能量满了
        if (IsEnergyFull)
        {
            OnEnergyFull?.Invoke(this);
            
        }
    }

    /// <summary>
    /// 消耗能量（释放大招后调用）
    /// </summary>
    public void ConsumeAllEnergy()
    {
        currentEnergy = 0f;
        OnEnergyChanged?.Invoke(0f, StatBlock.maxEnergy);
    }

    // 纯经验条模式：经验满时的处理
    private void HandleXpFull()
    {
        // [已弃用] 改为使用 LevelUpWeapon + UpgradeManager
       
    }

    // 外部调用：玩家选择分支后
    public void ApplyBranch(WeaponStatBlock branchStatBlock)
    {
               
        // 【新增】清理已存在的飞刀实例（进化/融合时需要重新生成新类型的飞刀）
        ClearActiveFlyingDaggers();
        
        // 【新增】清理已存在的光环（进化/融合时需要重新生成新类型的光环）
        ClearActiveAura();
        
        StatBlock = branchStatBlock;
        PlayerBladeAttack bladeAttack = GetComponent<PlayerBladeAttack>();
        if (bladeAttack != null)
        {
            bladeAttack.attackData = branchStatBlock;
            bladeAttack.ApplyModeFromWeapon(branchStatBlock);
        }
        currentStage = WeaponStage.Branched;
        hasBranched = true;
        currentProficiencyXP = 0f; // 重置经验
        CalculateNextLevelXP();
        UpdateVisualModel();

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital)
        {
            SetupOrbiters();
        }
        
        Time.timeScale = 1f; // 恢复游戏
        OnWeaponLevelUp?.Invoke(1); // 通知UI刷新
    }
    
    /// <summary>
    /// 清理所有活跃的飞刀实例（用于进化/融合时清理旧飞刀）
    /// </summary>
    private void ClearActiveFlyingDaggers()
    {
        foreach (var dagger in activeFlyingDaggers)
        {
            if (dagger != null) Destroy(dagger);
        }
        activeFlyingDaggers.Clear();
        
    }
    
    /// <summary>
    /// 清理活跃的光环实例（用于进化/融合时清理旧光环）
    /// </summary>
    private void ClearActiveAura()
    {
        if (orbitalPivot != null)
        {
            Destroy(orbitalPivot.gameObject);
            orbitalPivot = null;
           
        }

        isOrbitalActive = false;
        currentOrbitalDuration = 0f;
    }

    // 获取可用的分支选项列表
    public List<WeaponStatBlock> GetBranchOptions()
    {
        var options = new List<WeaponStatBlock>();
        if (StatBlock.branchOptionA != null) options.Add(StatBlock.branchOptionA);
        if (StatBlock.branchOptionB != null) options.Add(StatBlock.branchOptionB);
        return options;
    }



    private void LevelUpWeapon()
    {
        currentProficiencyXP -= xpToNextLevel; // 溢出的经验保留
        currentProficiencyLevel++;
        PlayerProgressManager.Instance?.RecordWeaponLevelReached(StatBlock, currentProficiencyLevel);

        // 应用成长属性 (简单粗暴地加到 localBonus 上)
        // 这里的公式是：当前等级 * 成长系数
        // 注意：你可能需要根据你的数值设计调整这个公式
        localDamageBonus += StatBlock.damageGrowthPerLevel;
        localFireRateBonus += StatBlock.cooldownGrowthPerLevel;
        localAreaBonus += StatBlock.areaGrowthPerLevel;

        /* [修改] 禁用旧的自动进化逻辑，现在由 UpgradeManager 的技能树卡片接管
        if (currentProficiencyLevel >= 10 && StatBlock.evolutionTarget != null)
        {
            bool canEvolve = false;

            // 1. 检查火球术进化
            if (StatBlock.weaponID == "Fireball" && IsMetaUnlocked("Fireball_Meta_Evolution"))
            {
                canEvolve = true;
            }

            // ==========================================
            // 【新增】2. 检查雷击进化
            // ==========================================
            // 假设节点文件名叫 "Lightning_Meta_Evolution"
            if (StatBlock.weaponID == "LightningStrike" && IsMetaUnlocked("Lightning_Meta_Evolution"))
            {
                canEvolve = true;
            }

            // 执行进化
            if (canEvolve)
            {
                EvolveWeapon(StatBlock.evolutionTarget);
                return;
            }
        }
        */

       
        OnWeaponLevelUp?.Invoke(currentProficiencyLevel);

        // 武器技能树已整合到通用升级池，不再需要单独触发
        // if (UpgradeManager.Instance != null)
        // {
        //     UpgradeManager.Instance.HandleWeaponLevelUp(this);
        // }

        CalculateNextLevelXP();

        // 检查是否再次升级 (如果一次获得巨量经验)
        if (currentProficiencyXP >= xpToNextLevel) LevelUpWeapon();
    }

    private void EvolveWeapon(WeaponStatBlock newBlock)
    {
        

        // 1. 替换数据
        StatBlock = newBlock;

        // 2. 重置等级 (通常进化后变成新武器的 Lv1)
        currentStage = WeaponStage.Evolved;
        currentProficiencyXP = 0f; // 重新计算新武器的升级经验

        // 3. 重置属性 (因为新武器的基础属性可能已经包含了旧武器的加成，或者你需要保留加成)
        // 这里的策略看你：是“继承旧武器的加成”还是“重置为新武器白板”
        // 建议：如果是数值进化，保留 localBonus；如果是形态改变，可能需要调整。
        // 这里暂时保留 localBonus。

        // 4. 刷新模型和特效
        UpdateVisualModel();

        // 5. 通知 UI
        OnWeaponLevelUp?.Invoke(currentProficiencyLevel); // 触发刷新
    }

    private void CalculateNextLevelXP()
    {
        // 纯经验条模式：根据阶段确定固定经验需求
        switch (currentStage)
        {
            case WeaponStage.Base:
                xpToNextLevel = 200f; // 基础阶段需要200经验触发分支
                break;
            case WeaponStage.Branched:
                xpToNextLevel = 400f; // 分支阶段需要400经验触发进化
                break;
            case WeaponStage.Evolved:
                xpToNextLevel = float.MaxValue; // 已进化，不需要更多经验
                break;
        }
       
    }
    void Update()
    {
        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;
        if (orbitalCooldownTimer > 0f) orbitalCooldownTimer -= Time.deltaTime;
        if (auraKnockbackTimer > 0f) auraKnockbackTimer -= Time.deltaTime; //
        if (auraDebuffRefreshTimer > 0f) auraDebuffRefreshTimer -= Time.deltaTime;
        // --- ^^^ [新增] ^^^ ---

        if (beamCooldownTimer > 0f)
        {
            beamCooldownTimer -= Time.deltaTime;
            if (beamCooldownTimer <= 0)
            {
                beamEnergyTimer = StatBlock.beamDuration;
            }
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Orbital && orbitalPivot != null)
        {
            // 使用 localSpeedBonus + 大招的 orbitalSpeedMultiplier
            float finalOrbitalSpeed = StatBlock.baseOrbitalSpeed * (1f + localSpeedBonus) * orbitalSpeedMultiplier;
            orbitalPivot.Rotate(Vector3.up, finalOrbitalSpeed * Time.deltaTime);
        }

        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Landmine)
        {
            HandleLandminePlacement();
        }
        /*if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Beam)
        {
            HandleBeamWeapon();
        }*/
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Aura)
        {
            if (IsLightningStrikeWeapon())
            {
                HandleLightningStrikeTick();
            }
            else
            {
                HandleAuraDamageTick();
                HandleAuraKnockback();
                HandleAuraPersistentDebuffs();
                HandleAuraMagnet();
            }
        }
    }
    public int GetTotalCount()
    {
        if (StatBlock == null) return 0;

        // 1. 基础数量 (WSB配置)
        int baseCount = StatBlock.baseOrbitalCount;
        // 如果你的无人机用的是 AddProjectile 字段，请把上面改成 StatBlock.projectileCount

        // 2. 局部升级加成
        int localBonus = localOrbitalCountBonus;

        // 3. 全局被动加成 (PlayerStats)
        int globalBonus = 0;
        if (PlayerStats.Instance != null)
        {
            // 读取全局的 bonusOrbitalCount 或 bonusProjectileCount
            globalBonus = PlayerStats.Instance.bonusOrbitalCount;
        }

        int engineerBonus = 0;
        if (IsEngineerSkillActive("Engineer_Overclock_RotorArray") && IsMechanicalWeaponFamily())
        {
            engineerBonus += 1;
        }

        return baseCount + localBonus + globalBonus + engineerBonus;
    }
    private void SetupAura()
    {       

        // === 详细调试日志 ===
        if (StatBlock == null) 
        { 
            Debug.LogError("[SetupAura] 失败: StatBlock 是空的！"); 
            return; 
        }
        if (IsLightningStrikeWeapon())
        {
            return;
        }
        if (StatBlock.orbitalPrefab == null) 
        { 
            Debug.LogError($"[SetupAura] 失败: 武器 '{StatBlock.weaponName}' 的 OrbitalPrefab 没赋值！"); 
            return; 
        }
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[SetupAura] 失败: PlayerStats.Instance 是空的！");
            return;
        }
        if (WeaponController.Instance == null)
        {
            Debug.LogWarning("[SetupAura] 警告: WeaponController.Instance 是空的，将使用自身transform作为anchor");
        }

        // 1. 清理旧光环
        if (orbitalPivot != null) Destroy(orbitalPivot.gameObject);

        // 2. 找到挂载点
        Transform anchor = FindStableAnchor();
        // 3. 生成光环
        GameObject auraGO = Instantiate(StatBlock.orbitalPrefab, anchor);
        auraGO.transform.localPosition = Vector3.zero;
        auraGO.transform.localRotation = Quaternion.identity;

        orbitalPivot = auraGO.transform;
        // 4. 初始化脚本
        MagneticStormAura auraScript = auraGO.GetComponent<MagneticStormAura>();
        if (auraScript != null)
        {            
            int finalBaseDamage = Mathf.RoundToInt(
                StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier +
                PlayerStats.Instance.flatDamageBonus
            );

            float rangeMult = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus;
            int finalCount = GetTotalCount();

            // 【核心修改】计算麻痹概率 (基础概率 + 局部加成)
            // 你可以在 SO 里加一个 baseStunChance，或者默认 0
            float finalCritRate = PlayerStats.Instance.critRate + localCritRateBonus;
            // 传入 finalCritRate
            auraScript.Initialize(finalBaseDamage, rangeMult, this, finalCount, finalCritRate);
        }
        else
        {
            // 辅助型光环
            SupportAura supportAuraScript = auraGO.GetComponent<SupportAura>();
            if (supportAuraScript != null)
            {
                int finalBaseDamage = Mathf.RoundToInt(
                    ((StatBlock.baseAoeDamage > 0) ? StatBlock.baseAoeDamage : StatBlock.baseDirectDamage) *
                    (PlayerStats.Instance.damageMultiplier + localDamageBonus) +
                    PlayerStats.Instance.flatDamageBonus
                );
                if (finalBaseDamage <= 0) finalBaseDamage = 10;
                float rangeMult = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus;
                
                supportAuraScript.Initialize(finalBaseDamage, rangeMult, this);
            }
            else
            {
                Debug.LogError($"[SetupAura] 失败: 光环预制件 '{StatBlock.orbitalPrefab.name}' 上没有 MagneticStormAura 或 SupportAura 脚本！");
            }
        }
    }




    public void RefreshAura()
    {
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.Aura) return;
        if (IsLightningStrikeWeapon()) return;

        // --- 【核心修复 4】强制重建光环逻辑对象 ---
        // 只有重建，才能把最新的 damage/range/count 传给 MagneticStormAura
        SetupAura();

        // (原本的 VFX 逻辑可以保留，用于处理武器自身的视觉效果，但核心逻辑必须由 SetupAura 重置)

        // 1. 计算最终半径 (仅用于 WeaponPart 自身的碰撞器和 VFX，逻辑对象已在 SetupAura 处理)
        float stoneScaleBonus = (currentStone != null) ? currentStone.scaleModifier : 0f;
        float finalRadius = StatBlock.baseAoeRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus + stoneScaleBonus);

        // 2. 更新碰撞器 (用于 HandleAuraDamageTick，如果那个还在运行的话)
        if (auraCollider != null) { auraCollider.radius = finalRadius; }

        // 3. 更新 VFX (保持你原有的换皮逻辑)
        GameObject prefabToUse = StatBlock.auraVfxPrefab;
        float scaleMultiplier = StatBlock.vfxBaseScaleMultiplier;

        if (currentStone != null && currentStone.auraVfxOverride != null)
        {
            prefabToUse = currentStone.auraVfxOverride;
            scaleMultiplier = currentStone.overrideVfxScaleMultiplier;
        }

        bool isWrongVfx = (auraVfxInstance != null && (auraVfxInstance.name.StartsWith(prefabToUse.name) == false));

        if (auraVfxInstance == null || (isWrongVfx && prefabToUse != null))
        {
            if (auraVfxInstance != null) Destroy(auraVfxInstance);
            if (prefabToUse != null)
            {
                auraVfxInstance = Instantiate(prefabToUse, transform.position, Quaternion.identity, transform);
                auraVfxInstance.name = prefabToUse.name;
            }
        }

        if (auraVfxInstance != null)
        {
            auraVfxInstance.transform.localScale = Vector3.one * finalRadius * scaleMultiplier;
        }
    }
    private bool IsLightningStrikeWeapon()
    {
        return StatBlock != null && StatBlock.weaponID == "LightningStrike";
    }

    private void HandleLightningStrikeTick()
    {
        if (StatBlock == null) return;

        lightningStrikeTimer -= Time.deltaTime;
        if (lightningStrikeTimer > 0f) return;

        Health firstTarget = FindLightningStrikeTarget(null);
        if (firstTarget == null)
        {
            lightningStrikeTimer = 0.2f;
            return;
        }

        lightningStrikeTimer = GetLightningStrikeCooldown();
        if (cooldownMaterial != null) cooldownMaterial.StartCooldown(lightningStrikeTimer);
        PlayFireSound();

        int strikeCount = Mathf.Max(1, GetTotalCount());
        HashSet<Health> selectedTargets = new HashSet<Health>();

        for (int i = 0; i < strikeCount; i++)
        {
            Health target = i == 0 ? firstTarget : FindLightningStrikeTarget(selectedTargets);
            if (target == null) break;

            selectedTargets.Add(target);
            ExecuteLightningStrike(target, 1f, localLightningRepeatCount);
        }
    }

    private float GetLightningStrikeCooldown()
    {
        float fireRateMultiplier = 1f;
        if (PlayerStats.Instance != null)
        {
            fireRateMultiplier = PlayerStats.Instance.fireRateMultiplier - localFireRateBonus - GetEngineerMechanicalFireRateBonus();
        }

        if (currentStone != null)
        {
            fireRateMultiplier *= 1f + currentStone.fireRateModifier;
        }

        fireRateMultiplier = Mathf.Max(0.1f, fireRateMultiplier);
        float baseRate = Mathf.Max(0.01f, StatBlock.baseFireRate);
        return Mathf.Max(0.08f, (1f / baseRate) * fireRateMultiplier);
    }

    private Health FindLightningStrikeTarget(HashSet<Health> excludedTargets)
    {
        if (StatBlock == null) return null;

        float searchRadius = StatBlock.autoAimRange > 0f ? StatBlock.autoAimRange : 30f;
        LayerMask searchMask = GetLightningEnemyMask();
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, searchMask);

        Health bestTarget = null;
        float bestDistanceSqr = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;
            if (excludedTargets != null && excludedTargets.Contains(health)) continue;
            if (!hit.CompareTag("Enemy") && !health.CompareTag("Enemy")) continue;

            float distanceSqr = (health.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = health;
            }
        }

        return bestTarget;
    }

    private LayerMask GetLightningEnemyMask()
    {
        if (enemyLayerMask.value != 0) return enemyLayerMask;
        if (StatBlock != null && StatBlock.layersToDamageByAOE.value != 0) return StatBlock.layersToDamageByAOE;
        return LayerMask.GetMask("Enemies");
    }

    private void ExecuteLightningStrike(Health target, float damageScale, int repeatRemaining)
    {
        if (target == null || target.IsDead || StatBlock == null) return;

        Vector3 strikePoint = GetLightningStrikePoint(target);
        SpawnLightningStrikeVfx(strikePoint);

        bool isCrit = PlayerStats.Instance != null && Random.value <= (PlayerStats.Instance.critRate + StatBlock.baseCritRate + localCritRateBonus);
        int directDamage = CalculateLightningStrikeDamage(StatBlock.baseDirectDamage, damageScale, isCrit);
        int splashDamage = CalculateLightningStrikeDamage(StatBlock.baseAoeDamage > 0 ? StatBlock.baseAoeDamage : StatBlock.baseDirectDamage, damageScale, isCrit);
        float radius = GetLightningStrikeRadius();

        ApplyLightningStrikeDamage(target, strikePoint, directDamage, isCrit, true);
        ApplyLightningStrikeSplash(strikePoint, radius, splashDamage, isCrit, target);

        if (isMagneticStormEnabled)
        {
            TriggerLightningMagneticStorm(strikePoint, splashDamage, isCrit);
        }

        if (repeatRemaining > 0)
        {
            StartCoroutine(RepeatLightningStrikeRoutine(target, repeatRemaining - 1, damageScale * 0.7f));
        }
    }

    private IEnumerator RepeatLightningStrikeRoutine(Health originalTarget, int repeatRemaining, float damageScale)
    {
        yield return new WaitForSeconds(0.22f);

        Health target = originalTarget != null && !originalTarget.IsDead
            ? originalTarget
            : FindLightningStrikeTarget(null);

        if (target != null)
        {
            ExecuteLightningStrike(target, damageScale, repeatRemaining);
        }
    }

    private int CalculateLightningStrikeDamage(int baseDamage, float damageScale, bool isCrit)
    {
        float damageMultiplier = 1f + localDamageBonus;
        float flatDamage = 0f;

        if (PlayerStats.Instance != null)
        {
            damageMultiplier = PlayerStats.Instance.damageMultiplier + localDamageBonus;
            flatDamage = PlayerStats.Instance.flatDamageBonus;
        }

        if (currentStone != null)
        {
            damageMultiplier += currentStone.damageModifier;
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt((Mathf.Max(1, baseDamage) * damageMultiplier + flatDamage) * damageScale));

        if (isCrit && PlayerStats.Instance != null)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * (PlayerStats.Instance.critDamage + StatBlock.baseCritDamage + localCritDamageBonus)));
        }

        return damage;
    }

    private float GetLightningStrikeRadius()
    {
        float radiusMultiplier = 1f + localAreaBonus;
        if (PlayerStats.Instance != null)
        {
            radiusMultiplier = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus;
        }
        if (currentStone != null)
        {
            radiusMultiplier += currentStone.scaleModifier;
        }

        return Mathf.Max(0.5f, StatBlock.baseAoeRadius * Mathf.Max(0.1f, radiusMultiplier));
    }

    private Vector3 GetLightningStrikePoint(Health target)
    {
        if (target == null) return transform.position;
        return target.transform.position;
    }

    private Vector3 GetLightningHitPoint(Health target)
    {
        if (target == null) return transform.position;
        return target.AimTargetPoint != null ? target.AimTargetPoint.position : target.transform.position;
    }

    private void SpawnLightningStrikeVfx(Vector3 position)
    {
        GameObject vfxPrefab = null;
        if (StatBlock.nativeSmiteVfxPrefab != null)
        {
            vfxPrefab = StatBlock.nativeSmiteVfxPrefab;
        }
        else if (StatBlock.ultimateEffectPrefab != null)
        {
            vfxPrefab = StatBlock.ultimateEffectPrefab;
        }

        if (vfxPrefab == null) return;

        GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.identity);
        Destroy(vfx, 2.5f);
    }

    private void ApplyLightningStrikeSplash(Vector3 center, float radius, int damage, bool isCrit, Health primaryTarget)
    {
        LayerMask damageMask = GetLightningEnemyMask();
        Collider[] hits = Physics.OverlapSphere(center, radius, damageMask);
        HashSet<Health> damagedTargets = new HashSet<Health>();
        if (primaryTarget != null) damagedTargets.Add(primaryTarget);

        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead || damagedTargets.Contains(health)) continue;
            if (!hit.CompareTag("Enemy") && !health.CompareTag("Enemy")) continue;

            damagedTargets.Add(health);
            ApplyLightningStrikeDamage(health, center, damage, isCrit, false);
        }
    }

    private void ApplyLightningStrikeDamage(Health target, Vector3 strikePoint, int damage, bool isCrit, bool allowOnKillChain)
    {
        if (target == null || target.IsDead) return;

        target.TakeDamage(damage, GetLightningHitPoint(target), this.gameObject, AttackType.Standard, null, null, StatBlock.weaponName, isCrit);

        StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>();
        if (receiver != null && !target.IsDead)
        {
            receiver.ApplyParalyze(GetLightningParalyzeDuration());
        }

        if (allowOnKillChain && isOnKillChainEnabled && target.IsDead)
        {
            int chainCount = Mathf.Max(3, StatBlock.baseChainCount);
            float chainRange = StatBlock.chainRange > 0f ? StatBlock.chainRange : 8f;
            int chainDamage = Mathf.Max(1, Mathf.RoundToInt(damage * 0.55f));
            ChainLightningFromTarget(target.transform, chainCount, chainDamage, chainRange, StatBlock.nativeChainVfxPrefab, StatBlock.nativeChainImpactVfxPrefab);
        }
    }

    private float GetLightningParalyzeDuration()
    {
        return Mathf.Clamp(0.35f + localStunDurationBonus * 0.15f, 0.15f, 0.9f);
    }

    private void TriggerLightningMagneticStorm(Vector3 center, int baseDamage, bool isCrit)
    {
        float radius = GetLightningStrikeRadius() * 1.35f;
        int stormDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (0.45f + localElectricFieldDamageBonus)));

        if (StatBlock.nativeChainImpactVfxPrefab != null)
        {
            GameObject impact = Instantiate(StatBlock.nativeChainImpactVfxPrefab, center, Quaternion.identity);
            Destroy(impact, 2f);
        }

        ApplyLightningFieldDamage(center, radius, stormDamage, isCrit);

        if (isElectricFieldEnabled)
        {
            StartCoroutine(LightningElectricFieldRoutine(center, radius, stormDamage));
        }
    }

    private IEnumerator LightningElectricFieldRoutine(Vector3 center, float radius, int damagePerPulse)
    {
        float duration = Mathf.Max(1f, 1.6f + localElectricFieldDurationBonus);
        float tickInterval = 0.5f;
        float elapsed = 0f;

        GameObject fieldVfx = null;
        if (StatBlock.auraVfxPrefab != null)
        {
            fieldVfx = Instantiate(StatBlock.auraVfxPrefab, center, Quaternion.identity);
            fieldVfx.transform.localScale = Vector3.one * Mathf.Max(0.5f, radius * StatBlock.vfxBaseScaleMultiplier);
        }

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            ApplyLightningFieldDamage(center, radius, damagePerPulse, false);
        }

        if (fieldVfx != null) Destroy(fieldVfx);
    }

    private void ApplyLightningFieldDamage(Vector3 center, float radius, int damage, bool isCrit)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, GetLightningEnemyMask());
        HashSet<Health> damagedTargets = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead || damagedTargets.Contains(health)) continue;
            if (!hit.CompareTag("Enemy") && !health.CompareTag("Enemy")) continue;

            damagedTargets.Add(health);
            ApplyLightningStrikeDamage(health, center, damage, isCrit, false);
        }
    }

    private void HandleAuraDamageTick()
    {
        auraTickTimer -= Time.deltaTime;
        if (auraTickTimer <= 0f)
        {
            if (StatBlock == null) return; //

            // --- vvv [ 核心修复 3 ] vvv ---
            // (重新添加 finalDamage 和 chainedTargets 的声明)

            float stoneFireRateBonus = (currentStone != null) ? currentStone.fireRateModifier : 0f; //
            float finalTickInterval = StatBlock.baseAreaTickInterval * (1f - stoneFireRateBonus); //
            auraTickTimer = finalTickInterval;

            // [!] 'finalDamage' 的声明在这里
            float stoneDamageBonus = (currentStone != null) ? currentStone.damageModifier : 0f; //
            int finalDamage = Mathf.RoundToInt( //
                (StatBlock.baseAreaDamagePerTick * (PlayerStats.Instance.aoeDamageMultiplier + stoneDamageBonus)) + //
                PlayerStats.Instance.flatAoeDamageBonus //
            );

            // [!] 'chainedTargets' 的声明在这里
            List<Health> chainedTargets = null; //
            if (currentStone != null && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) //
            {
                chainedTargets = ApplyAuraChainDamage(finalDamage); //
            }
            // --- ^^^ [ 核心修复 3 ] ^^^ ---

            if (auraCollider == null) return; //
            if (enemyLayerMask == 0) return; //

            Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue; //

                Health target = hit.GetComponentInParent<Health>(); //
                if (target == null || target.IsDead) //
                {
                    continue;
                }

                // (使用 'finalDamage' 和 'chainedTargets')
                if (chainedTargets == null || !chainedTargets.Contains(target))
                {
                    // [修复 3] 传入 StatBlock.weaponName
                    target.TakeDamage(finalDamage, target.transform.position, this.gameObject, AttackType.Standard, null, null, StatBlock.weaponName);
                }

                if (currentStone != null)
                {
                    StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>();
                    if (receiver != null)
                    {
                        if (currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn) && Random.value <= currentStone.burnChance)
                        {
                            // [修复 4] 传入 StatBlock.weaponName 给 ApplyBurn
                            receiver.ApplyBurn(currentStone.burnDamage, currentStone.burnDuration, currentStone.burnTickInterval, StatBlock.weaponName);
                        }
                    }
                }
            }
        }
    }

    public void ChainLightningFromTarget(Transform startTarget, int maxChains, int damage, float range,
                                          GameObject overrideChainVfx = null, GameObject overrideImpactVfx = null)
    {
        // 1. 基础安全检查
        if (startTarget == null || maxChains <= 0) return;

        GameObject chainVfxToUse = null;
        GameObject impactVfxToUse = null;

        // --- 【核心修改】优先级判断 ---

        // A. 如果传入了覆盖特效，拥有最高优先级 (MagneticOrbiter 专用)
        if (overrideChainVfx != null)
        {
            chainVfxToUse = overrideChainVfx;
            impactVfxToUse = overrideImpactVfx; // 即使是 null 也会覆盖，所以 MagneticOrbiter 必须配置
        }
        // B. 如果没有覆盖，则走原有逻辑 (检查石头 -> 检查 StatBlock)
        else
        {
            if (currentStone != null && currentStone.chainVfxPrefab != null)
            {
                chainVfxToUse = currentStone.chainVfxPrefab;
                impactVfxToUse = currentStone.chainImpactVfxPrefab;
            }
            else
            {
                chainVfxToUse = StatBlock?.nativeChainVfxPrefab;
                impactVfxToUse = StatBlock?.defaultImpactEffectPrefab;
            }

            // 保底逻辑
            if (chainVfxToUse == null) chainVfxToUse = this.lightningChainPrefab;
            if (impactVfxToUse == null) impactVfxToUse = StatBlock?.defaultImpactEffectPrefab;
        }

        // 2. 启动协程
        StartCoroutine(ChainLightningRoutine(startTarget, maxChains, damage, range, chainVfxToUse, impactVfxToUse));
    }
    private IEnumerator ChainLightningRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange, GameObject chainVfx, GameObject impactVfx)
    {
        var hitEnemies = new List<Health>();
        // 【修复】使用AimTargetPoint作为起始位置，而不是脚底
        Health startHealth = currentTarget.GetComponent<Health>();
        Vector3 lastHitPosition = (startHealth?.AimTargetPoint != null) ? startHealth.AimTargetPoint.position : currentTarget.position;

        while (currentTarget != null && remainingChains >= 0)
        {
            Vector3 currentTargetHitPoint = currentTarget.GetComponent<Health>()?.AimTargetPoint?.position ?? currentTarget.position; //
            Health targetHealth = currentTarget.GetComponent<Health>(); //

            if (targetHealth != null && !hitEnemies.Contains(targetHealth) && !targetHealth.IsDead) //
            {
                hitEnemies.Add(targetHealth);

                // (我们假设第一个目标已经在 Projectile.cs 中受到了伤害，
                //  这个协程只伤害 *后续* 目标)
                if (hitEnemies.Count > 1)
                {
                    targetHealth.TakeDamage(damage, currentTargetHitPoint, this.gameObject, AttackType.Standard); //
                }

                // 播放连锁VFX (从上一个点到这个点)
                if (chainVfx != null)
                {
                    var chainVFX_GO = Instantiate(chainVfx, Vector3.zero, Quaternion.identity); //
                    chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, currentTargetHitPoint); //
                }
                // 播放受击VFX
                if (impactVfx != null)
                {
                    Instantiate(impactVfx, currentTargetHitPoint, Quaternion.identity); //
                }
            }

            yield return new WaitForSeconds(0.05f); // 连锁之间的微小延迟

            // (查找下一个目标... 逻辑保持不变)
            Transform nextTarget = FindNextChainTarget(currentTargetHitPoint, chainRange, hitEnemies);
            remainingChains--;
            lastHitPosition = currentTargetHitPoint;
            currentTarget = nextTarget;
        }
    }
    private void HandleAuraPersistentDebuffs()
    {
        auraDebuffRefreshTimer -= Time.deltaTime;
        if (auraDebuffRefreshTimer > 0f) return;
        auraDebuffRefreshTimer = 0.25f; // 优化：每秒检查4次

        if (currentStone == null || auraCollider == null || enemyLayerMask == 0)
        {
            ClearAllAuraDebuffs(); // 如果没有石头或碰撞器，清除所有
            return;
        }

        bool stoneHasSlow = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow); //
        bool stoneHasWeaken = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyWeaken); //
        bool stoneHasCorrode = (currentStone != null) && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode); //

        // 1. 获取半径内的所有敌人
        HashSet<StatusEffectReceiver> enemiesInRadius = new HashSet<StatusEffectReceiver>();
        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

        // --- vvv [ 核心修复 ] vvv ---
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue; //

            // 1. 先获取 Health 组件并检查 IsDead
            Health targetHealth = hit.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead) //
            {
                continue; // 跳过死亡或没有 Health 的物体
            }

            // 2. 只有在存活时，才获取 StatusEffectReceiver
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>(); //
            if (receiver != null)
            {
                enemiesInRadius.Add(receiver);
            }
        }
        // --- ^^^ [ 核心修复 ] ^^^ ---

        // --- 2. 处理减速 (Slow) ---
        ProcessDebuffList(enemiesInRadius, aura_ActiveSlows, stoneHasSlow,
            (receiver) => { receiver.ApplyPersistentSlow(this, currentStone.slowPercentage, currentStone.slowColor); }, //
            (receiver) => { receiver.RemovePersistentSlow(this); }
        );

        // --- 3. 处理弱化 (Weaken) ---
        ProcessDebuffList(enemiesInRadius, aura_ActiveWeaKens, stoneHasWeaken,
            (receiver) => { receiver.ApplyPersistentWeaken(this, currentStone.weakenPercentage); }, //
            (receiver) => { receiver.RemovePersistentWeaken(this); }
        );

        float finalCorrodeMultiplier = 1.0f;
        if (stoneHasCorrode && currentStone != null)
        {
            int corrodeStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode); //
            if (corrodeStoneCount >= 2) //
            {
                finalCorrodeMultiplier = currentStone.corrodeMultiplier_Stacked; // [!] 使用堆叠后的乘数
            }
            else
            {
                finalCorrodeMultiplier = currentStone.corrodeMultiplier; // [!] 使用基础乘数
            }
        }
        // 4. 处理腐蚀 (Corrode)
        ProcessDebuffList(enemiesInRadius, aura_ActiveCorrodes, stoneHasCorrode,
            (receiver) => { receiver.ApplyPersistentCorrode(this, finalCorrodeMultiplier, currentStone.corrodeColor); }, //
            (receiver) => { receiver.RemovePersistentCorrode(this); }
        );
    }

    private void HandleAuraMagnet()
    {
        auraMagnetTimer -= Time.deltaTime;
        if (auraMagnetTimer > 0f) return;
        auraMagnetTimer = 0.5f; // 优化：每秒检查2次

        if (currentStone == null || !currentStone.applyMagnet || auraCollider == null || pickupLayerMask == 0)
        {
            return;
        }

        // 1. 获取玩家 Transform (用于传递给掉落物)
        Transform playerTransform = GameManager.Instance?.playerTransform;
        if (playerTransform == null) return;

        // 2. 计算磁力半径
        // (光环的基础半径 + 能量石的额外百分比加成)
        float finalRadius = auraCollider.radius * (1f + currentStone.magnetRadiusBonusPercent);

        // 3. 扫描掉落物
        Collider[] hits = Physics.OverlapSphere(transform.position, finalRadius, pickupLayerMask);

        foreach (var hit in hits)
        {
            // 尝试获取经验球
            ExperienceGem gem = hit.GetComponent<ExperienceGem>(); //
            if (gem != null)
            {
                gem.TriggerMagnet(playerTransform);
                continue; // 下一个
            }

            // 尝试获取金币
            GoldPickup gold = hit.GetComponent<GoldPickup>(); //
            if (gold != null)
            {
                gold.TriggerMagnet(playerTransform);
            }
        }
    }

    /// <summary>
    /// (新增) 比较新旧列表并应用/移除debuff的通用帮助方法
    /// </summary>
    private void ProcessDebuffList(
        HashSet<StatusEffectReceiver> enemiesInRadius,
        HashSet<StatusEffectReceiver> activeList,
        bool stoneHasEffect,
        System.Action<StatusEffectReceiver> OnApply,
        System.Action<StatusEffectReceiver> OnRemove)
    {
        if (!stoneHasEffect)
        {
            // 如果石头没有这个效果，移除所有
            foreach (var receiver in activeList) { OnRemove(receiver); }
            activeList.Clear();
            return;
        }

        // 1. 应用新debuff (在范围内，但不在旧列表里)
        foreach (var receiver in enemiesInRadius)
        {
            if (activeList.Add(receiver)) // Add() 只有在元素 *不* 存在时才返回 true
            {
                OnApply(receiver);
            }
        }

        // 2. 移除旧debuff (在旧列表里，但不在范围内)
        activeList.RemoveWhere(receiver =>
        {
            if (receiver == null || !enemiesInRadius.Contains(receiver))
            {
                OnRemove(receiver);
                return true; // 从 activeList 中移除
            }
            return false;
        });
    }
    private void ClearAllAuraDebuffs()
    {
        foreach (var receiver in aura_ActiveSlows) { receiver?.RemovePersistentSlow(this); }
        aura_ActiveSlows.Clear();

        foreach (var receiver in aura_ActiveWeaKens) { receiver?.RemovePersistentWeaken(this); }
        aura_ActiveWeaKens.Clear();

        foreach (var receiver in aura_ActiveCorrodes) { receiver?.RemovePersistentCorrode(this); } //
        aura_ActiveCorrodes.Clear();
    }
    private List<Health> ApplyAuraChainDamage(int baseDamage)
    {
        // --- vvv [ 核心修复 ] vvv ---
        // 1. 不再读取列表，而是立即扫描
        if (currentStone == null || auraCollider == null) return null;
        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //
                                                                                                          // --- ^^^ [ 核心修复 ] ^^^ ---


        // 计算连锁伤害
        int chainDamage = Mathf.RoundToInt(baseDamage * currentStone.chainDamageMultiplier); //
        if (chainDamage <= 0) chainDamage = 1;

        List<Health> hitTargets = new List<Health>();

        // --- vvv [ 核心修复 ] vvv ---
        // 2. 从扫描结果 (hits) 中构建潜在目标列表
        List<Health> potentialTargets = new List<Health>();
        foreach (Collider hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && !target.IsDead) //
            {
                potentialTargets.Add(target);
            }
        }
        // --- ^^^ [ 核心修复 ] ^^^ ---

        if (potentialTargets.Count == 0) return null;

        // ... (确定 firstTarget 的逻辑保持不变) ...
        Health firstTarget = potentialTargets
            .OrderBy(t => (t.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();

        if (firstTarget == null) return null;
        potentialTargets.Remove(firstTarget);

        Health currentTarget = firstTarget;
        Vector3 lastVFXOriginPoint = transform.position;

        // (连锁循环逻辑保持不变)
        for (int i = 0; i <= currentStone.chainTargets; i++) //
        {
            if (currentTarget == null) break;

            Vector3 currentTargetHitPoint = currentTarget.AimTargetPoint != null ? currentTarget.AimTargetPoint.position : currentTarget.transform.position; //

            currentTarget.TakeDamage(chainDamage, currentTargetHitPoint, this.gameObject, AttackType.Standard); //
            hitTargets.Add(currentTarget);

            if (currentStone.chainImpactVfxPrefab != null) //
            {
                Instantiate(currentStone.chainImpactVfxPrefab, currentTargetHitPoint, Quaternion.identity); //
            }
            else if (StatBlock.defaultImpactEffectPrefab != null)
            {
                Instantiate(StatBlock.defaultImpactEffectPrefab, currentTargetHitPoint, Quaternion.identity); //
            }

            if (currentStone.chainVfxPrefab != null) //
            {
                Vector3 vfxOrigin = (i == 0) ? transform.position : lastVFXOriginPoint;
                var chainVFX_GO = Instantiate(currentStone.chainVfxPrefab, Vector3.zero, Quaternion.identity); //
                chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(vfxOrigin, currentTargetHitPoint); //
            }

            lastVFXOriginPoint = currentTargetHitPoint;

            Health nextTarget = null;
            float minSqrDist = currentStone.chainRange * currentStone.chainRange; //

            foreach (Health potential in potentialTargets)
            {
                if (potential == null || hitTargets.Contains(potential)) continue;
                float sqrDist = (potential.transform.position - lastVFXOriginPoint).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nextTarget = potential;
                }
            }

            if (nextTarget != null)
            {
                potentialTargets.Remove(nextTarget);
                currentTarget = nextTarget;
            }
            else
            {
                break;
            }
        }

        return hitTargets;
    }

    private void HandleAuraKnockback()
    {
        if (currentStone == null || !currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback) || auraKnockbackTimer > 0f) //
        {
            return;
        }

        // (检查通过，重置计时器)
        auraKnockbackTimer = currentStone.knockbackInterval; //

        // --- vvv [ 调试日志 ] vvv ---
        if (auraCollider == null) //
        {
            Debug.LogError("Knockback FAILED: auraCollider is NULL!");
            return;
        }
        if (enemyLayerMask == 0) //
        {
            Debug.LogWarning("Knockback Check: 'Enemy Layer Mask' (in Inspector) is not set!");
            return;
        }
        // --- ^^^ [ 调试日志 ] ^^^ ---

        Collider[] hits = Physics.OverlapSphere(transform.position, auraCollider.radius, enemyLayerMask); //

        if (hits.Length == 0)
        {
            // 这是你之前看到的日志的新版本
            Debug.LogWarning("Knockback Fire: OverlapSphere found 0 targets. (Check LayerMask?)");
            return;
        }

        int knockbackStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback); //

        float finalKnockbackForce;

        if (knockbackStoneCount >= 2) //
        {
            // 使用堆叠后的力度
            finalKnockbackForce = currentStone.knockbackForce_Stacked; //
        }
        else
        {
            // 使用基础力度
            finalKnockbackForce = currentStone.knockbackForce; //
        }


        foreach (Collider hit in hits)
        {
            // --- vvv [ 核心修复 ] vvv ---
            // 强制检查 Tag
            if (!hit.CompareTag("Enemy"))
            {
                continue;
            }
            // --- ^^^ [ 核心修复 ] ^^^ ---

            Health target = hit.GetComponentInParent<Health>(); //
            if (target == null || target.IsDead) continue; //

            StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>(); //
            if (receiver != null)
            {
                Vector3 pushDir = (target.transform.position - transform.position).normalized; //
                pushDir.y = 0; //

                // [!] 使用 finalKnockbackForce
                receiver.ApplyKnockback(pushDir, finalKnockbackForce); //
            }
            else
            {
                Debug.LogWarning($"Knockback Check: Target {target.name} IS 'Enemy' but is missing StatusEffectReceiver!");
            }
        }
    }
    private void OnDestroy()
    {
        if (currentStone != null)
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.RegisterStone(null, currentStone);
            }
        }
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        if (activeBeamInstance != null) { Destroy(activeBeamInstance.gameObject); }
    }
    #endregion

    #region Public Control Methods
   

    public void Fire(Vector3 initialDirection)
    {
         
        if (!IsReadyToFire) return;
        // Then check specific boomerang state
        if (StatBlock != null && StatBlock.behavior == WeaponBehaviorType.Boomerang && isBoomerangOut) return;

        StartCoroutine(FireRoutine(initialDirection));
    }


    private IEnumerator FireRoutine(Vector3 initialDirection)
    {
        // 1. 获取能量石加成
        float stoneDmgMod = (currentStone != null) ? currentStone.damageModifier : 0f;
        float stoneScaleMod = (currentStone != null) ? currentStone.scaleModifier : 0f;
        float stoneFireRateMod = (currentStone != null) ? currentStone.fireRateModifier : 0f;

        // 2. 检查回旋镖状态
        if (StatBlock.behavior == WeaponBehaviorType.Boomerang)
        {
            if (isBoomerangOut) yield break;
            isBoomerangOut = true;
            activeBoomerangCount = GetBoomerangProjectileCount();
        }
        else
        {
            // 计算冷却
            float finalFireRateMultiplier = (PlayerStats.Instance.fireRateMultiplier - localFireRateBonus - GetEngineerMechanicalFireRateBonus()) * (1f + stoneFireRateMod);
            if (finalFireRateMultiplier <= 0.1f) finalFireRateMultiplier = 0.1f;
            fireCooldown = (1f / StatBlock.baseFireRate) * finalFireRateMultiplier;
        }

        // 3-6. 视觉、音效、特殊类型检查 (保持不变)
        if (cooldownMaterial != null) cooldownMaterial.StartCooldown(fireCooldown);
        if (floatingVisual != null) floatingVisual.HideWeapon();
        if (fireSoundDelay < 0) { PlayFireSound(); yield return new WaitForSeconds(Mathf.Abs(fireSoundDelay)); }
        if (StatBlock?.behavior == WeaponBehaviorType.Beam) yield break;
        if (StatBlock?.behavior == WeaponBehaviorType.Orbital)
        {
            if (!isOrbitalActive && orbitalCooldownTimer <= 0) { SetupOrbiters(); }
            yield break;
        }
        // 【修复】Aura类型武器不需要走Fire流程，在Start/SetupAura中已初始化
        // 光环自身有Update持续运行，不需要每次Fire都刷新（否则会重置strikeTimer）
        if (StatBlock?.behavior == WeaponBehaviorType.Aura)
        {
            yield break;
        }

        // 7. 自动瞄准逻辑 (保持不变)
        Vector3 finalTargetDirection = initialDirection;
        Transform firstTarget = null;
        if (StatBlock.autoAimAtNearestEnemy && GetComponentInParent<DroneAI>() == null)
        {
            Transform nearestEnemy = FindNearestEnemyTransform();
            if (nearestEnemy != null)
            {
                finalTargetDirection = (nearestEnemy.position - firePoint.position);
                finalTargetDirection.y = 0;
                finalTargetDirection.Normalize();
                firstTarget = nearestEnemy;
            }
            else if (StatBlock.behavior != WeaponBehaviorType.Boomerang) { yield break; }
        }
        if (finalTargetDirection.sqrMagnitude < 0.01f)
        {
            if (StatBlock?.behavior == WeaponBehaviorType.Boomerang) isBoomerangOut = false;
            if (StatBlock?.behavior == WeaponBehaviorType.Boomerang) activeBoomerangCount = 0;
            yield break;
        }

        // =========================================================
        // 【核心修复】智能选择基础伤害值
        // =========================================================
        int baseDamageToUse = StatBlock.baseDirectDamage;
        // 如果是 AOE 类武器，优先使用 AOE 面板伤害
        if (StatBlock.behavior == WeaponBehaviorType.MeleeAOE ||
            StatBlock.behavior == WeaponBehaviorType.ParabolicAOE ||
            StatBlock.behavior == WeaponBehaviorType.Landmine ||
            StatBlock.behavior == WeaponBehaviorType.PersistentAOE ||
            StatBlock.behavior == WeaponBehaviorType.Aura || 
            StatBlock.behavior == WeaponBehaviorType.CreateAndForget ||
            StatBlock.behavior == WeaponBehaviorType.FlyingDagger) // <--- 【新增】
        {
            if (StatBlock.baseAoeDamage > 0) baseDamageToUse = StatBlock.baseAoeDamage;
        }
        // For Aura, CreateAndForget, FlyingDagger, we need to ensure they are set up.
        // 确保只初始化一次
        if (StatBlock.behavior == WeaponBehaviorType.Aura || 
            StatBlock.behavior == WeaponBehaviorType.CreateAndForget ||
            StatBlock.behavior == WeaponBehaviorType.FlyingDagger) // <--- 【新增】
        {
            if (StatBlock.behavior == WeaponBehaviorType.Aura) SetupAura();
            // CreateAndForget 应该在 switch 中处理，不在这里调用，因为它需要 finalDamage
            else if (StatBlock.behavior == WeaponBehaviorType.FlyingDagger) SetupFlyingDaggers(); // <--- 【新增】
        }
        int baseDamage = 0;
        float baseScale = 1f;

        if (StatBlock != null && PlayerStats.Instance != null)
        {
            // --- 计算基础伤害 (使用 baseDamageToUse) ---
            baseDamage = Mathf.RoundToInt(
                baseDamageToUse * (PlayerStats.Instance.damageMultiplier + localDamageBonus + stoneDmgMod) +
                PlayerStats.Instance.flatDamageBonus
            );

            // --- 计算基础体积 ---
            baseScale = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus + stoneScaleMod;
        }

        // 8. 回旋镖叠加机制 (保持不变)
        int finalDamage = baseDamage;
        float finalScale = baseScale;

        // 10. Switch 行为模式
        // 【新增】计算连射次数 (BurstCount)
        int burstCount = 1 + localBurstCountBonus; // 基础1次 + 连射加成
        float burstInterval = 0.1f; // 连射间隔（秒）

        for (int burst = 0; burst < burstCount; burst++)
        {
            // 连射时等待间隔（第一发不等待）
            if (burst > 0)
            {
                yield return new WaitForSeconds(burstInterval);
            }

            switch (StatBlock?.behavior)
            {
                case WeaponBehaviorType.Standard:
                case WeaponBehaviorType.Pierce:
                    // ==========================================
                    // 支持多重射击与扇形散射 (齐射逻辑)
                    // ==========================================

                    // 1. 计算总子弹数（齐射：同时发射的数量）
                    int finalProjCount = StatBlock.projectileCount + PlayerStats.Instance.bonusProjectileCount + localOrbitalCountBonus;

                    // 特殊保底：如果是极寒冰锥，强制至少 3 发
                    if (StatBlock.weaponID == "ExtremeIceShard" && finalProjCount < 3)
                    {
                        finalProjCount = 3;
                    }

                    // 2. 准备发射
                    if (finalProjCount <= 1)
                    {
                        // 单发逻辑
                        InstantiateAndFireProjectile(finalTargetDirection, finalDamage);
                    }
                    else
                    {
                        // 多发散射逻辑（齐射：同时左右排开）
                        float totalSpread = StatBlock.spreadAngle;
                        float startAngle = -totalSpread / 2f;
                        float stepAngle = (finalProjCount > 1) ? (totalSpread / (finalProjCount - 1)) : 0f;

                        for (int i = 0; i < finalProjCount; i++)
                        {
                            float currentAngle = startAngle + (stepAngle * i);
                            Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
                            Vector3 spreadDirection = rotation * finalTargetDirection;
                            InstantiateAndFireProjectile(spreadDirection, finalDamage);
                        }
                    }
                    break;

            case WeaponBehaviorType.CreateAndForget:
                InstantiateAndFireCreateAndForget(finalDamage, finalScale);
                break;
            
            case WeaponBehaviorType.FlyingDagger:
                SetupFlyingDaggers();
                break;

            case WeaponBehaviorType.MeleeAOE:
                StartCoroutine(MeleeAttackRoutine(finalDamage, finalScale));
                break;

            case WeaponBehaviorType.ParabolicAOE:
                // 榴弹必须有目标且在射程内才发射
                Transform grenadeTarget = firstTarget ?? FindNearestEnemyTransform();
                if (grenadeTarget == null) { yield break; } // 无敌人不发射

                float grenadeRange = StatBlock.autoAimRange > 0 ? StatBlock.autoAimRange : 15f;
                float distToTarget = Vector3.Distance(firePoint.position, grenadeTarget.position);
                if (distToTarget > grenadeRange) { yield break; } // 超出射程不发射

                int finalParabolicAoe = Mathf.RoundToInt(
                    StatBlock.baseAoeDamage * (PlayerStats.Instance.aoeDamageMultiplier + localDamageBonus + stoneDmgMod) +
                    PlayerStats.Instance.flatAoeDamageBonus
                );
                InstantiateAndFireParabolicProjectile(grenadeTarget.position, StatBlock, finalDamage, finalParabolicAoe);
                break;

            case WeaponBehaviorType.Chain:
                if (firstTarget != null)
                {
                    StartCoroutine(ChainDamageRoutine(firstTarget, StatBlock.baseChainCount, finalDamage, StatBlock.chainRange));

                    // 交叉闪电：额外闪电链，0.3秒后对另一个目标发射
                    if (localOrbitalCountBonus > 0)
                    {
                        StartCoroutine(CrossLightningRoutine(firstTarget, StatBlock.baseChainCount, finalDamage, StatBlock.chainRange, localOrbitalCountBonus));
                    }
                }
                break;

            case WeaponBehaviorType.PersistentAOE:
                Transform targetEnemy = FindNearestEnemyTransform();
                if (targetEnemy != null) { InstantiateAndFireAirdropDeployer(targetEnemy.position); }
                break;

            case WeaponBehaviorType.Boomerang:
                FireBoomerangVolley(finalTargetDirection, finalDamage, finalScale);
                break;

            case WeaponBehaviorType.FrostNova:
                Transform frostTarget = firstTarget ?? FindNearestEnemyTransform();
                if (frostTarget != null)
                {
                    StartCoroutine(FrostNovaRoutine(frostTarget.position, finalDamage, finalScale));
                }
                break;
        }
        } // for burst 循环结束

        // 11-13. 枪口特效与后续 (保持不变)
        if (StatBlock != null && StatBlock.muzzleFlashPrefab != null)
        {
            Instantiate(StatBlock.muzzleFlashPrefab, firePoint.position, firePoint.rotation);
        }
        if (floatingVisual != null)
        {
            yield return new WaitForSeconds(hideVisualDuration);
            floatingVisual.ShowWeapon();
        }
        if (fireSoundDelay >= 0)
        {
            yield return new WaitForSeconds(fireSoundDelay);
            PlayFireSound();
        }
    }

    public float GetBoomerangReturnDamageMultiplier()
    {
        return Mathf.Max(0.1f, 1.15f + boomerangReturnDamageBonus);
    }

    public void OnBoomerangReturnHit(Vector3 hitPosition, int returnHitCount, int projectileDamage)
    {
        if (StatBlock == null || returnHitCount <= 0) return;
        if (boomerangReturnPulseEveryHits <= 0 || boomerangReturnPulseDamageMultiplier <= 0f) return;
        if (returnHitCount % boomerangReturnPulseEveryHits != 0) return;

        int pulseDamage = Mathf.Max(1, Mathf.RoundToInt(projectileDamage * boomerangReturnPulseDamageMultiplier));
        float pulseRadius = Mathf.Max(0.5f, boomerangReturnPulseRadius);
        DealBoomerangAreaDamage(hitPosition, pulseRadius, pulseDamage);
    }

    public void OnBoomerangCaught(Vector3 catchPosition, int returnHitCount = 0, int projectileDamage = 0)
    {
        if (!isBoomerangOut || StatBlock == null)
        {
            // Debug.LogWarning($"[OnBoomerangCaught] Ignored. isBoomerangOut={isBoomerangOut}, StatBlock Null? {StatBlock == null}, PlayerStats Null? {PlayerStats.Instance == null}");
            return;
        }

        if (returnHitCount > 0 && boomerangRecallBurstDamageMultiplier > 0f)
        {
            int baseDamage = projectileDamage > 0 ? projectileDamage : Mathf.Max(1, StatBlock.baseDirectDamage);
            float hitBonus = 1f + Mathf.Min(returnHitCount, 10) * 0.08f;
            int burstDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * boomerangRecallBurstDamageMultiplier * hitBonus));
            float burstRadius = Mathf.Max(0.75f, boomerangRecallBurstRadius + Mathf.Min(returnHitCount, 8) * 0.15f);
            DealBoomerangAreaDamage(catchPosition, burstRadius, burstDamage);
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.boomerangCatchStacks = 0;
        }

        activeBoomerangCount = Mathf.Max(0, activeBoomerangCount - 1);

        // --- 核心修改：增加 PlayerStats 中的叠加层数 ---
        // --- 核心修改结束 ---

        if (activeBoomerangCount <= 0)
        {
            isBoomerangOut = false;
            ResetCooldown(); // 重置冷却
        }
    }

    private void DealBoomerangAreaDamage(Vector3 center, float radius, int damage)
    {
        if (StatBlock == null || damage <= 0 || radius <= 0f) return;

        LayerMask damageMask = enemyLayerMask.value != 0
            ? enemyLayerMask
            : (StatBlock.layersToDamageByAOE.value != 0 ? StatBlock.layersToDamageByAOE : LayerMask.GetMask("Enemies"));

        Collider[] hits = Physics.OverlapSphere(center, radius, damageMask);
        HashSet<Health> damagedTargets = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead || damagedTargets.Contains(health)) continue;
            if (!hit.CompareTag("Enemy") && !health.CompareTag("Enemy")) continue;

            damagedTargets.Add(health);
            Vector3 hitPoint = health.AimTargetPoint != null ? health.AimTargetPoint.position : hit.ClosestPoint(center);
            health.TakeDamage(damage, hitPoint, gameObject, AttackType.Standard, null, null, StatBlock.weaponName);
        }

        GameObject vfxPrefab = StatBlock.defaultImpactEffectPrefab != null
            ? StatBlock.defaultImpactEffectPrefab
            : StatBlock.explosionEffectPrefab;
        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, center, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }

    public void StartCooldownIfNotCaught()
    {
        if (isBoomerangOut)
        {
            activeBoomerangCount = Mathf.Max(0, activeBoomerangCount - 1);
            if (activeBoomerangCount > 0) return;

            isBoomerangOut = false;
            float fireRateMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.fireRateMultiplier : 1f;
            fireCooldown = (1f / StatBlock.baseFireRate) * fireRateMultiplier;
            // Debug.Log($"Boomerang '{StatBlock?.weaponName}' missed, starting cooldown: {fireCooldown}s.");

            // --- 【核心修改】重置 PlayerStats 中的叠加层数 ---
            if (PlayerStats.Instance != null && PlayerStats.Instance.boomerangCatchStacks > 0)
            {
                PlayerStats.Instance.boomerangCatchStacks = 0;
            }
            // --- 【核心修改结束】 ---
        }
    }

    public void ResetCooldown()
    {
        fireCooldown = 0.01f; // Set a very small cooldown to allow immediate firing after catch
    }
    #endregion

    #region Private Helper Methods
    private void PlayFireSound()
    {
        // 优先从 SO 读取音效（进化/分支后自动跟随新数据）
        AudioClip[] clips = (StatBlock != null && StatBlock.fireSounds != null && StatBlock.fireSounds.Length > 0)
            ? StatBlock.fireSounds
            : fireSounds; // 回退到预制件上的旧字段

        if (clips != null && clips.Length > 0)
        {
            AudioClip clipToPlay = clips[Random.Range(0, clips.Length)];
            float volume = (StatBlock != null) ? StatBlock.fireSoundVolume : 1.0f;

            // 优先使用全局 AudioManager（2D音效），回退到本地 AudioSource（3D音效）
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySoundEffect(clipToPlay, volume);
            }
            else if (audioSource != null)
            {
                audioSource.PlayOneShot(clipToPlay, volume);
            }
        }
    }

    // Updated to accept finalDamage
    private void InstantiateAndFireProjectile(Vector3 direction, int inputDamage)
    {
        if (firePoint == null) return;
        if (StatBlock?.projectilePrefab == null) return;

        GameObject bullet = Instantiate(StatBlock.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));

        bool isCrit = Random.value <= (PlayerStats.Instance.critRate + localCritRateBonus);
        int finalDamage = isCrit ? Mathf.RoundToInt(inputDamage * (PlayerStats.Instance.critDamage + localCritDamageBonus)) : inputDamage;

        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // --- 速度与寿命 ---
            float finalSpeed = StatBlock.baseLaunchForce * (PlayerStats.Instance.projectileSpeedMultiplier + localSpeedBonus);
            float finalLifetime = StatBlock.baseProjectileLifetime * (PlayerStats.Instance.durationMultiplier + localDurationBonus);

            // ==========================================
            // 【核心修改 1】穿透计算 (加入冰锥术逻辑)
            // ==========================================
            int stonePierceBonus = (currentStone != null) ? (int)currentStone.pierceModifier : 0;

            // 基础穿透 + 玩家全局穿透 + 能量石穿透 + 局外升级穿透(localPierceCountBonus)
            int finalPierceCount = StatBlock.basePierceCount + PlayerStats.Instance.bonusPierceCount + stonePierceBonus + localPierceCountBonus; // <--- 加上 localPierceCountBonus
           
            // 特殊逻辑：极寒冰锥 (进化后) 无限穿透
            // 可以判断 ID (ExtremeIceShard) 或者名字
            if (StatBlock.weaponID == "ExtremeIceShard")
            {
                finalPierceCount = 999;
            }

            // ==========================================
            // 【核心修改 2】冰冻概率计算
            // ==========================================
            // 基础概率 + 局外升级概率
            float finalFreezeChance = StatBlock.baseFreezeChance + localFreezeChanceBonus;


            // --- 其他参数 (保持不变) ---
            int finalDotDamage = Mathf.RoundToInt(StatBlock.baseDotDamage);
            float finalDotDuration = StatBlock.baseDotDuration;
            float finalDotTickInterval = StatBlock.dotTickInterval;
            float finalSlowPercentage = StatBlock.baseSlowPercentage;
            float finalSlowDuration = StatBlock.baseSlowDuration;

            int finalAoeDamage = 0;
            float finalAoeRadius = 0f;
            GameObject explodeVfx = null;

            if (StatBlock.baseAoeRadius > 0)
            {
                finalAoeRadius = StatBlock.baseAoeRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus);
                finalAoeDamage = finalDamage;
                explodeVfx = StatBlock.explosionEffectPrefab;
            }

            // ==========================================
            // 【核心修改 3】传递参数 (注意最后加了 freezeChance)
            // ==========================================
            // 此时你需要去 Projectile.cs 修改 InitializeAsStraight 的参数列表，接收这个 float
            projectileScript.InitializeAsStraight(
               direction, finalSpeed, finalDamage,
               false, finalPierceCount,
               finalLifetime,
               StatBlock.shieldImpactEffectPrefab, StatBlock.defaultImpactEffectPrefab,
               finalDotDamage, finalDotDuration, finalDotTickInterval, finalSlowPercentage, finalSlowDuration,
               AttackType.Standard,
               this,
               finalAoeDamage,
               finalAoeRadius,
               explodeVfx,
               finalFreezeChance // <--- 【新增】传入冰冻概率
            );

            projectileScript.isCritical = isCrit;
            if (isCrit)
            {
                // Debug.Log($"[WeaponPart] 发射了一颗暴击子弹！武器: {StatBlock.weaponName}");
            }

            // 【飓风】如果预制件上有 HurricaneProjectile 组件，初始化它
            HurricaneProjectile hc = bullet.GetComponent<HurricaneProjectile>();
            if (hc != null)
            {
                hc.Setup(this);
            }
        }
        else { Destroy(bullet); }
    }

    // Updated to accept finalDirectDamage and finalAoeDamage
    private void InstantiateAndFireParabolicProjectile(Vector3 targetPos, WeaponStatBlock statsToUse, int finalDirectDamage, int finalAoeDamage)
    {
        if (firePoint == null || statsToUse?.projectilePrefab == null) return;

        // 计算最终属性
        float finalAoeRadius = statsToUse.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
        int finalDotDamage = Mathf.RoundToInt(statsToUse.baseDotDamage);
        float finalDotDuration = statsToUse.baseDotDuration;
        float finalDotTickInterval = statsToUse.dotTickInterval;
        float finalStunChance = statsToUse.baseStunChance + PlayerStats.Instance.parabolicAoeStunChance;
        float finalStunDuration = statsToUse.baseStunDuration + localStunDurationBonus;
        // 有眩晕加成时强制100%概率
        if (localStunDurationBonus > 0f && finalStunChance <= 0f) finalStunChance = 1f;

        // === 精确弹道计算 ===
        Vector3 startPos = firePoint.position;
        Vector3 toTarget = targetPos - startPos;
        float horizontalDist = new Vector3(toTarget.x, 0, toTarget.z).magnitude;
        float heightDiff = toTarget.y;
        Vector3 horizontalDir = new Vector3(toTarget.x, 0, toTarget.z).normalized;

        // 根据距离动态调整弧线高度：近距离低弧，远距离高弧
        float gravity = 20f;
        float arcHeight = Mathf.Clamp(horizontalDist * 0.5f, 1.5f, 8f); // 距离的一半，限制在1.5~8之间

        // 飞行时间由弧线高度决定（上升+下降）
        float timeToApex = Mathf.Sqrt(2f * arcHeight / gravity);
        float totalTime = timeToApex + Mathf.Sqrt(2f * (arcHeight - heightDiff) / gravity);

        // 计算初始速度
        float vy = gravity * timeToApex; // 垂直初速度
        Vector3 horizontalVel = horizontalDir * (horizontalDist / totalTime); // 水平速度 = 距离/时间
        Vector3 initialVelocity = horizontalVel + Vector3.up * vy;

        // 生成子弹
        GameObject bullet = Instantiate(statsToUse.projectilePrefab, startPos, Quaternion.LookRotation(horizontalDir));
        bool isCrit = Random.value <= PlayerStats.Instance.critRate;
        int damageToUse = isCrit ? Mathf.RoundToInt(finalDirectDamage * PlayerStats.Instance.critDamage) : finalDirectDamage;
        
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.InitializeAsParabolic(
                 initialVelocity, finalDirectDamage, finalAoeDamage,
                 statsToUse.baseProjectileLifetime, statsToUse.explosionEffectPrefab, finalAoeRadius,
                 statsToUse.layersToDamageByAOE, statsToUse.layersToExplodeOn,
                 finalDotDamage, finalDotDuration, finalDotTickInterval,
                 finalStunChance, finalStunDuration,
                 this
             );
            projectileScript.isCritical = isCrit;
        }
        else { Destroy(bullet); }
    }

    private int GetBoomerangProjectileCount()
    {
        return Mathf.Clamp(1 + localOrbitalCountBonus, 1, 4);
    }

    private void FireBoomerangVolley(Vector3 direction, int finalDamage, float finalScale)
    {
        int count = GetBoomerangProjectileCount();
        activeBoomerangCount = count;

        if (count <= 1)
        {
            InstantiateAndFireBoomerang(direction, finalDamage, finalScale);
            return;
        }

        float spread = Mathf.Min(36f, 12f + (count - 1) * 8f);
        float startAngle = -spread * 0.5f;
        float step = count > 1 ? spread / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 shotDirection = Quaternion.Euler(0f, startAngle + step * i, 0f) * direction;
            InstantiateAndFireBoomerang(shotDirection.normalized, finalDamage, finalScale);
        }
    }

    // Updated to accept finalDamage and finalScale
    private void InstantiateAndFireBoomerang(Vector3 direction, int finalDamage, float finalScale, Vector3? launchPosition = null) // <-- 添加可选参数
    {
        // 如果没有提供发射位置，则使用默认的 firePoint
        Vector3 spawnPos = launchPosition ?? firePoint.position;

        if (StatBlock?.projectilePrefab == null) // 简化 null 检查
        {
            isBoomerangOut = false;
            activeBoomerangCount = 0;
            Debug.LogError($"[WeaponPart] Boomerang fire failed: ProjectilePrefab missing for {StatBlock?.weaponName}");
            return;
        }

        GameObject bullet = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
        bullet.transform.localScale = Vector3.one * finalScale;

        float speedMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.projectileSpeedMultiplier + localSpeedBonus : 1f + localSpeedBonus;
        float durationMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.durationMultiplier + localDurationBonus : 1f + localDurationBonus;
        float finalSpeed = StatBlock.baseLaunchForce * Mathf.Max(0.1f, speedMultiplier);
        float finalMaxDistance = StatBlock.maxDistance * Mathf.Max(0.1f, durationMultiplier);
        float finalLifetime = StatBlock.baseProjectileLifetime * Mathf.Max(0.1f, durationMultiplier);
        float finalCatchRadius = Mathf.Clamp(StatBlock.catchRadius, 0.35f, 0.95f);

        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // --- 调用最终的 InitializeAsBoomerang ---
            projectileScript.InitializeAsBoomerang(
                direction,
                finalSpeed,
                finalDamage,
                finalMaxDistance,
                finalCatchRadius,
                finalLifetime,
                StatBlock.shieldImpactEffectPrefab,
                StatBlock.defaultImpactEffectPrefab,
                this,
                StatBlock.rotationSpeed,
                StatBlock.returnOvershootDistance // <-- 传递这个值
            );
        }
        else
        {
            isBoomerangOut = false;
            activeBoomerangCount = 0;
            Debug.LogError($"[WeaponPart] Boomerang fire failed: Projectile script missing on prefab for {StatBlock?.weaponName}");
            Destroy(bullet);
        }
    }

    private void InstantiateAndFireCreateAndForget(int finalDamage, float finalScale)
    {
        // --- [排查 1] 检查核心引用 ---
        if (StatBlock == null)
        {
            Debug.LogError("[排查] 失败: StatBlock 是空的！");
            return;
        }
        if (StatBlock.projectilePrefab == null)
        {
            Debug.LogError($"[排查] 失败: 武器 '{StatBlock.weaponName}' 的 ProjectilePrefab 是空的！请检查 ScriptableObject。");
            return;
        }
        // ---------------------------

        // 1. 确定生成位置
        Vector3 spawnPos = transform.position; // 默认为武器位置
        if (WeaponController.Instance != null)
        {
            spawnPos = WeaponController.Instance.transform.position; // 优先用玩家位置
        }

        // --- [排查 2] 检查 Raycast 地面检测 ---
        RaycastHit hit;
        // 尝试从玩家头顶 (y+2) 向下射线检测
        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out hit, 10f, LayerMask.GetMask("Ground", "Default", "Terrain")))
        {
            spawnPos = hit.point;
        }
        else
        {
            Debug.LogWarning($"[排查] 警告: 未检测到地面！塔可能生成在半空或地底。当前位置 y={spawnPos.y}");
        }
        // -----------------------------------

        // 2. 生成实例
        GameObject obj = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.identity);

        // --- [排查 3] 检查生成后的物体状态 ---
        if (obj == null)
        {
            Debug.LogError("[排查] 致命错误: Instantiate 返回了 null！");
            return;
        }
        // -----------------------------------

        // 3. 应用缩放
        // --- [排查 4] 检查缩放系数 ---
        float configScale = StatBlock.vfxBaseScaleMultiplier;
        if (configScale <= 0.01f)
        {
            Debug.LogError($"[排查] 严重警告: StatBlock.vfxBaseScaleMultiplier 为 {configScale}！物体将不可见。请在配置里设为 1。");
        }

        Vector3 targetScale = Vector3.one * (configScale * finalScale);
        obj.transform.localScale = targetScale;
        // ---------------------------

        // 4. 组件初始化逻辑
        WanderingAOE wanderScript = obj.GetComponent<WanderingAOE>();
        if (wanderScript != null)
        {
            if (WeaponController.Instance != null)
                wanderScript.target = WeaponController.Instance.transform;
            else
                wanderScript.target = this.transform.root;
        }

        // --- [排查 5] 检查 FlamethrowerTurret 组件 ---
        FlamethrowerTurret turret = obj.GetComponent<FlamethrowerTurret>();
        if (turret != null)
        {
            float finalLifetime = StatBlock.baseProjectileLifetime * (PlayerStats.Instance.durationMultiplier + localDurationBonus);
            float finalRange = StatBlock.baseAoeRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus);
            float finalInterval = StatBlock.dotTickInterval;

            turret.Initialize(
                finalDamage,
                finalLifetime,
                WeaponController.Instance != null ? WeaponController.Instance.gameObject : this.gameObject,
                finalRange,
                finalInterval,
                StatBlock.weaponName
            );

            if (IsFlameTurretWeapon() && IsEngineerSkillActive("Engineer_Fortress_AutoTurret"))
            {
                SpawnEngineerSupportFlameTurret(spawnPos, targetScale, finalDamage, finalLifetime, finalRange, finalInterval);
            }
        }
        else
        {
            // 如果不是其他类型，也不是塔，那就可能是脚本挂错了
            if (obj.GetComponent<TornadoController>() == null && obj.GetComponent<PersistentAoeField>() == null && obj.GetComponent<Projectile>() == null)
            {
                Debug.LogError($"[排查] 生成的物体 '{obj.name}' 上没有找到 FlamethrowerTurret 脚本！也没有其他已知脚本。请检查 Prefab。");
            }
        }
        // ----------------------------------------

        // ... (其他 Tornado/Persistent 逻辑保留) ...
        TornadoController tc = obj.GetComponent<TornadoController>();
        if (tc != null) tc.Setup(finalDamage, this);

        PersistentAoeField aoe = obj.GetComponent<PersistentAoeField>();
        if (aoe != null)
        {
            GameObject attackerObj = WeaponController.Instance != null ? WeaponController.Instance.gameObject : this.gameObject;
            aoe.Setup(finalDamage, StatBlock.baseAreaTickInterval, StatBlock.baseAreaDuration, attackerObj);
        }

        // Projectile 初始化保底
        Projectile p = obj.GetComponent<Projectile>();
        if (p != null && turret == null) // 如果是塔，通常不需要初始化为 Projectile，除非你的塔同时是 Projectile
        {
            p.InitializeAsStraight(transform.forward, 0f, finalDamage, false, 999, StatBlock.baseProjectileLifetime, null, null, 0, 0, 0, 0, 0, AttackType.Standard, this);
        }
    }

    /// <summary>
    /// 冰霜新星：在目标位置爆发冰晶刺，对范围内敌人造成伤害+冻结
    /// </summary>
    private IEnumerator FrostNovaRoutine(Vector3 center, int damage, float scale)
    {
        float baseRadius = StatBlock.baseAoeRadius * scale * (1f + localAreaBonus);
        float freezeDuration = 1f + localFreezeDurationBonus; // 基础冻结2秒 + 加成
        int totalCasts = 1 + localFrostNovaExtraCast; // 连环霜爆

        HashSet<Transform> hitTargets = new HashSet<Transform>(); // 记录已命中的敌人，优先找新目标

        for (int cast = 0; cast < totalCasts; cast++)
        {
            if (cast > 0)
            {
                yield return new WaitForSeconds(0.6f);
                
                // 额外释放时，优先找范围内【未被本轮冰霜新星命中过】的最近敌人
                Transform newTarget = null;
                float minDist = float.MaxValue;
                float searchRadius = StatBlock?.autoAimRange ?? 20f;
                Collider[] cols = Physics.OverlapSphere(transform.position, searchRadius, LayerMask.GetMask("Enemies"));
                
                foreach (var c in cols)
                {
                    Health pH = c.GetComponentInParent<Health>();
                    if (pH == null || pH.IsDead || hitTargets.Contains(pH.transform)) continue;
                    
                    float dist = Vector3.Distance(transform.position, pH.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        newTarget = pH.transform;
                    }
                }
                
                // 如果范围内没有新目标了，则回退逻辑：找离玩家最近的任何活着的敌人（允许重复命中）
                if (newTarget == null)
                {
                    newTarget = FindNearestEnemyTransform();
                }

                if (newTarget != null) center = newTarget.position;
            }

            // 生成冰晶刺特效
            if (StatBlock.impactEffectPrefab != null)
            {
                var vfx = Instantiate(StatBlock.impactEffectPrefab, center, Quaternion.identity);
                vfx.transform.localScale = Vector3.one * (baseRadius / StatBlock.baseAoeRadius);
                Destroy(vfx, 3f);
            }

            PlayFireSound();

            // 对范围内敌人造成伤害
            Collider[] hits = Physics.OverlapSphere(center, baseRadius, LayerMask.GetMask("Enemies"));
            foreach (var col in hits)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;

                hitTargets.Add(h.transform); // 记录被命中的敌人

                float distToCenter = Vector3.Distance(center, h.transform.position);
                bool isInCenter = distToCenter <= baseRadius * 0.4f; // 内圈40%为中心区域

                h.TakeDamage(damage, h.transform.position, this.gameObject, sourceWeaponName: StatBlock.weaponName);

                // 寒霜之心：中心区域额外伤害
                if (localFrostNovaCenterDmg && isInCenter)
                {
                    h.TakeDamage(damage, h.transform.position, this.gameObject, sourceWeaponName: StatBlock.weaponName);
                }

                // 冻结效果
                StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                if (receiver != null)
                {
                    float finalFreeze = freezeDuration;
                    // 绝对零度：中心区域冻结翻倍
                    if (localAbsoluteZero && isInCenter)
                    {
                        finalFreeze *= 2f;
                    }
                    receiver.ApplyFreeze(finalFreeze, null, localFrostBite);
                }
            }

        }

        // === 冰晶碎裂：新星结束后从被冻结敌人处发射小冰锥 ===
        if (localIceCrystalShatter > 0 && StatBlock.subProjectilePrefab != null)
        {
            yield return new WaitForSeconds(0.2f); // 短暂延迟后碎裂

            // 找所有被冻结的敌人
            Collider[] frozenEnemies = Physics.OverlapSphere(center, baseRadius * 1.5f, LayerMask.GetMask("Enemies"));
            foreach (var col in frozenEnemies)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;

                StatusEffectReceiver ser = h.GetComponent<StatusEffectReceiver>();
                if (ser == null || !ser.IsFrozen) continue;

                // 从 AimTargetPoint 发射冰锥
                Vector3 spawnPos = (h.AimTargetPoint != null) ? h.AimTargetPoint.position : h.transform.position + Vector3.up;

                for (int s = 0; s < localIceCrystalShatter; s++)
                {
                    // 随机方向（水平散射）
                    float angle = (360f / localIceCrystalShatter) * s + Random.Range(-15f, 15f);
                    Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

                    GameObject shard = Instantiate(StatBlock.subProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
                    Projectile p = shard.GetComponent<Projectile>();
                    if (p != null)
                    {
                        int shardDmg = Mathf.RoundToInt(damage * 0.3f); // 冰锥伤害为新星30%
                        p.InitializeAsStraight(dir, 12f,
                            shardDmg, false, 1, 3f, null, null, 0, 0, 0, 0, 0, AttackType.Standard, this);
                        p.ignoreEnemyTimer = 0.5f; // 【新增】0.5秒内不触发敌人碰撞
                    }
                }

            }
        }
    }

    /// <summary>
    /// 大招闪电链：弹射次数极多，允许重复命中同一敌人
    /// </summary>
    public IEnumerator UltimateChainLightning(Transform currentTarget, int totalChains, int damage, float chainRange)
    {
        var recentlyHit = new List<Health>(); // 仅记录最近3个，允许回弹
        Vector3 lastHitPosition = firePoint.position;
        int remaining = totalChains;

        while (currentTarget != null && remaining > 0)
        {
            Health targetHealth = currentTarget.GetComponent<Health>();
            Vector3 hitPoint = (targetHealth != null && targetHealth.AimTargetPoint != null)
                ? targetHealth.AimTargetPoint.position : currentTarget.position;

            if (targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(damage, hitPoint, this.gameObject);

                // 闪电链特效
                if (lightningChainPrefab != null)
                {
                    var vfx = Instantiate(lightningChainPrefab, Vector3.zero, Quaternion.identity);
                    vfx.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, hitPoint);
                }
                if (StatBlock?.impactEffectPrefab != null)
                {
                    Instantiate(StatBlock.impactEffectPrefab, hitPoint, Quaternion.identity);
                }

                // 记录最近命中（只保留最近3个，允许链式回弹）
                recentlyHit.Add(targetHealth);
                if (recentlyHit.Count > 3) recentlyHit.RemoveAt(0);
            }

            yield return new WaitForSeconds(0.05f);

            // 找下一个目标（排除最近命中的）
            Transform nextTarget = null;
            float closestDist = float.MaxValue;
            Collider[] nearby = Physics.OverlapSphere(hitPoint, chainRange, LayerMask.GetMask("Enemies"));
            foreach (var col in nearby)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead || recentlyHit.Contains(h)) continue;
                float d = Vector3.Distance(hitPoint, col.transform.position);
                if (d < closestDist) { closestDist = d; nextTarget = col.transform; }
            }
            // 没有新目标就回弹到任意存活敌人
            if (nextTarget == null)
            {
                foreach (var col in nearby)
                {
                    Health h = col.GetComponentInParent<Health>();
                    if (h == null || h.IsDead) continue;
                    nextTarget = col.transform;
                    break;
                }
            }

            remaining--;
            lastHitPosition = hitPoint;
            currentTarget = nextTarget;
        }
    }

    /// <summary>
    /// 交叉闪电：每0.3秒对另一个目标发射额外闪电链
    /// </summary>
    private IEnumerator CrossLightningRoutine(Transform firstTarget, int chainCount, int damage, float chainRange, int extraCount)
    {
        for (int i = 0; i < extraCount; i++)
        {
            yield return new WaitForSeconds(0.6f);

            // 找另一个目标
            Transform crossTarget = null;
            float closestDist = float.MaxValue;
            Collider[] enemies = Physics.OverlapSphere(firePoint.position, chainRange, LayerMask.GetMask("Enemies"));
            foreach (var col in enemies)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                // 优先选不同于 firstTarget 的目标
                if (col.transform == firstTarget) continue;
                float d = Vector3.Distance(firePoint.position, col.transform.position);
                if (d < closestDist) { closestDist = d; crossTarget = col.transform; }
            }

            // 只有一个敌人时对同一目标
            if (crossTarget == null) crossTarget = firstTarget;
            if (crossTarget == null) yield break;

            StartCoroutine(ChainDamageRoutine(crossTarget, chainCount, damage, chainRange));
        }
    }

    private IEnumerator ChainDamageRoutine(Transform currentTarget, int remainingChains, int damage, float chainRange)
    {
        // 弹射次数加成
        remainingChains += localChainCountBonus;

        var hitEnemies = new List<Health>();
        Vector3 lastHitPosition = firePoint.position;

        while (currentTarget != null && remainingChains >= 0)
        {
            Health targetHealth = currentTarget.GetComponent<Health>();
            // 使用 AimTargetPoint 避免特效在脚底
            Vector3 hitPoint = (targetHealth != null && targetHealth.AimTargetPoint != null)
                ? targetHealth.AimTargetPoint.position : currentTarget.position;

            if (targetHealth != null && !hitEnemies.Contains(targetHealth) && !targetHealth.IsDead)
            {
                hitEnemies.Add(targetHealth);
                targetHealth.TakeDamage(damage, hitPoint, this.gameObject);

                // 闪电链特效
                if (lightningChainPrefab != null)
                {
                    var chainVFX_GO = Instantiate(lightningChainPrefab, Vector3.zero, Quaternion.identity);
                    chainVFX_GO.GetComponent<ChainLightningVFX>()?.Setup(lastHitPosition, hitPoint);
                }
                if (StatBlock?.impactEffectPrefab != null)
                {
                    Instantiate(StatBlock.impactEffectPrefab, hitPoint, Quaternion.identity);
                }

                // 麻痹效果（使用感电特效，不和眩晕共用）
                if (localStunDurationBonus > 0f)
                {
                    StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();
                    if (receiver != null) receiver.ApplyParalyze(localStunDurationBonus);
                }

                // 离子爆破
                if (localIonExplosionEnabled)
                {
                    float ionRadius = 2f * (1f + localIonExplosionRadiusBonus / 100f);
                    int ionDmg = Mathf.RoundToInt(damage * 0.5f * (1f + localIonExplosionDamageBonus / 100f));

                    Collider[] ionHits = Physics.OverlapSphere(hitPoint, ionRadius, LayerMask.GetMask("Enemies"));
                    foreach (var col in ionHits)
                    {
                        Health h = col.GetComponentInParent<Health>();
                        if (h == null || h.IsDead || hitEnemies.Contains(h)) continue;
                        h.TakeDamage(ionDmg, hitPoint, this.gameObject);

                        if (localStunDurationBonus > 0f)
                        {
                            StatusEffectReceiver r = h.GetComponent<StatusEffectReceiver>();
                            if (r != null) r.ApplyParalyze(localStunDurationBonus);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.05f);

            Transform nextTarget = FindNextChainTarget(hitPoint, chainRange, hitEnemies);
            remainingChains--;
            lastHitPosition = hitPoint;
            currentTarget = nextTarget;
        }
    }

    public void RefreshOrbiters() // Re-instantiate orbiters with current stats
    {
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital || !isOrbitalActive) return;
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false; // Mark inactive before setup
        StopCoroutine(nameof(OrbitalLifetimeRoutine)); // Stop potential old lifetime timer
        SetupOrbiters(); // Call setup again
    }

    /// <summary>
    /// 大招专用：强制重置冷却并立即召唤环绕武器（无视当前冷却状态）
    /// </summary>
    public void ForceSpawnOrbiters()
    {
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital) return;

        // 销毁当前活跃的环绕武器
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false;
        StopCoroutine(nameof(OrbitalLifetimeRoutine));
        StopCoroutine(nameof(OrbitalFlickerRoutine));
        StopCoroutine(nameof(OrbitalBreathingRoutine));

        // 重置冷却计时器
        orbitalCooldownTimer = 0f;

        // 立即生成新的环绕武器
        SetupOrbiters();
    }

    /// <summary>
    /// 用自定义预制件替换当前环绕武器（融合大招用）
    /// 结束后需要调用 ForceSpawnOrbiters 恢复普通环绕武器
    /// </summary>
    public Transform ForceSpawnOrbitersWithPrefab(GameObject customPrefab, float duration)
    {
        if (myStatBlock == null || myStatBlock.behavior != WeaponBehaviorType.Orbital) return null;

        // 销毁当前活跃的环绕武器
        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false;
        StopCoroutine(nameof(OrbitalLifetimeRoutine));
        StopCoroutine(nameof(OrbitalFlickerRoutine));
        StopCoroutine(nameof(OrbitalBreathingRoutine));

        // 重置冷却计时器
        orbitalCooldownTimer = 0f;

        // 用自定义预制件生成环绕武器
        Transform stableAnchor = FindStableAnchor();

        orbitalPivot = new GameObject($"{StatBlock.weaponName}_ComboPivot").transform;
        orbitalPivot.SetParent(stableAnchor);
        orbitalPivot.localPosition = Vector3.zero;
        orbitalPivot.localRotation = Quaternion.identity;

        isOrbitalActive = true;
        int finalOrbitalCount = GetTotalCount();
        float finalOrbitalRadius = StatBlock.baseOrbitalRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus);
        int finalDamage = Mathf.RoundToInt(
            StatBlock.baseDirectDamage * (PlayerStats.Instance.damageMultiplier + localDamageBonus) +
            PlayerStats.Instance.flatDamageBonus
        );
        float finalScale = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus;

        for (int i = 0; i < finalOrbitalCount; i++)
        {
            float angle = i * (360f / finalOrbitalCount);
            Vector3 spawnPos = Quaternion.Euler(0, angle, 0) * (Vector3.forward * finalOrbitalRadius);

            // 使用自定义预制件替代 orbitalPrefab
            GameObject orbiterGO = Instantiate(customPrefab, orbitalPivot);
            orbiterGO.transform.localPosition = spawnPos;
            orbiterGO.transform.localRotation = Quaternion.Euler(0, angle, 0);
            orbiterGO.transform.localScale = Vector3.one * finalScale;

            // 尝试初始化 Orbiter 组件（如果有的话，保留碰撞伤害逻辑）
            Orbiter orb = orbiterGO.GetComponent<Orbiter>();
            if (orb != null)
            {
                orb.Initialize(finalDamage, this);
            }
        }

        // 融合大招持续时间由外部控制，设置当前持续时间
        currentOrbitalDuration = duration;
        StartCoroutine(OrbitalLifetimeRoutine(duration));

        return orbitalPivot;
    }

    private void SetupOrbiters() // Creates the orbital system
    {
        if (StatBlock == null || StatBlock.orbitalPrefab == null) return; // Need data

        Transform stableAnchor = FindStableAnchor(); // Find player's stable anchor point

        orbitalPivot = new GameObject($"{StatBlock.weaponName}_Pivot").transform;
        orbitalPivot.SetParent(stableAnchor);
        orbitalPivot.localPosition = Vector3.zero;
        orbitalPivot.localRotation = Quaternion.identity;

        isOrbitalActive = true; // Mark as active *after* pivot creation
        int finalOrbitalCount = GetTotalCount();
        float finalOrbitalRadius = StatBlock.baseOrbitalRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus);
        int finalDamage = Mathf.RoundToInt(
        StatBlock.baseDirectDamage * (PlayerStats.Instance.damageMultiplier + localDamageBonus) +
        PlayerStats.Instance.flatDamageBonus
         );

        float finalScale = PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus;

        for (int i = 0; i < finalOrbitalCount; i++)
        {
            float angle = i * (360f / finalOrbitalCount);
            Vector3 spawnPos = Quaternion.Euler(0, angle, 0) * (Vector3.forward * finalOrbitalRadius);

            GameObject orbiterGO = Instantiate(StatBlock.orbitalPrefab, orbitalPivot);
            orbiterGO.transform.localPosition = spawnPos;
            orbiterGO.transform.localRotation = Quaternion.Euler(0, angle, 0);
            orbiterGO.transform.localScale = Vector3.one * finalScale;

            // --- 【核心修改】区分普通盾和磁暴盾 ---

            // 1. 优先尝试初始化为磁暴盾 (MagneticOrbiter)
            MagneticOrbiter mag = orbiterGO.GetComponent<MagneticOrbiter>();
            if (mag != null)
            {
                mag.Initialize(finalDamage, this);
            }
            // 2. 如果不是，则初始化为普通岩石盾 (Orbiter)
            else
            {
                Orbiter orb = orbiterGO.GetComponent<Orbiter>();
                if (orb != null)
                {
                    orb.Initialize(finalDamage, this);
                }
            }
        }

        float finalDuration = StatBlock.baseDuration * (PlayerStats.Instance.durationMultiplier + localDurationBonus);
        if (finalDuration > 0)
        {
            currentOrbitalDuration = finalDuration;
            StartCoroutine(OrbitalLifetimeRoutine(finalDuration));

            if (isOrbitalBreathingEnabled)
            {
                StartCoroutine(OrbitalBreathingRoutine(finalDuration));
            }
        }
    }

    private IEnumerator OrbitalLifetimeRoutine(float initialDuration)
    {
        float warningTime = 2f; // 最后2秒开始闪烁警告
        bool isFlickering = false;

        while (currentOrbitalDuration > 0)
        {
            yield return null;
            currentOrbitalDuration -= Time.deltaTime;

            // 若隐若现的闪烁警告：剩余时间不足2秒时开始
            if (!isFlickering && currentOrbitalDuration <= warningTime && orbitalPivot != null)
            {
                isFlickering = true;
                StartCoroutine(OrbitalFlickerRoutine());
            }
        }

        // 充能释放 (Charged Release)
        if (isOrbitalReleaseEnabled && orbitalPivot != null)
        {
            if (StatBlock != null && StatBlock.explosionEffectPrefab != null)
            {
                Instantiate(StatBlock.explosionEffectPrefab, transform.position, Quaternion.identity);
            }
            float releaseRadius = StatBlock.baseAoeRadius * (PlayerStats.Instance.aoeRadiusMultiplier + localAreaBonus) * 2f; 
            int releaseDmg = Mathf.RoundToInt(StatBlock.baseDirectDamage * 2f * (PlayerStats.Instance.damageMultiplier + localDamageBonus));
            Collider[] hits = Physics.OverlapSphere(transform.position, releaseRadius, LayerMask.GetMask("Enemies"));
            foreach (var col in hits)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                     h.TakeDamage(releaseDmg, transform.position, this.gameObject, AttackType.Standard);
                }
            }
        }

        // 平滑缩放消失（0.5秒内缩小到0）
        if (orbitalPivot != null)
        {
            yield return StartCoroutine(OrbitalScaleDownRoutine(0.5f));
        }

        if (orbitalPivot != null) { Destroy(orbitalPivot.gameObject); }
        isOrbitalActive = false;
        
        float finalFireRateMultiplier = (PlayerStats.Instance.fireRateMultiplier - localFireRateBonus - localCooldownReduction);
        if (finalFireRateMultiplier <= 0.1f) finalFireRateMultiplier = 0.1f;
        orbitalCooldownTimer = (1f / StatBlock.baseFireRate) * finalFireRateMultiplier;
    }

    /// <summary>
    /// 环绕武器消失前的闪烁警告效果
    /// </summary>
    private IEnumerator OrbitalFlickerRoutine()
    {
        if (orbitalPivot == null) yield break;

        Renderer[] renderers = orbitalPivot.GetComponentsInChildren<Renderer>();
        float flickerInterval = 0.15f; // 闪烁间隔

        while (isOrbitalActive && orbitalPivot != null && currentOrbitalDuration > 0)
        {
            // 隐藏
            foreach (var r in renderers) { if (r != null) r.enabled = false; }
            yield return new WaitForSeconds(flickerInterval);

            // 显示
            foreach (var r in renderers) { if (r != null) r.enabled = true; }
            yield return new WaitForSeconds(flickerInterval);

            // 越接近消失，闪烁越快
            flickerInterval = Mathf.Max(0.05f, flickerInterval * 0.9f);
        }

        // 确保最终可见（给缩放消失用）
        foreach (var r in renderers) { if (r != null) r.enabled = true; }
    }

    /// <summary>
    /// 环绕武器平滑缩放消失
    /// </summary>
    private IEnumerator OrbitalScaleDownRoutine(float duration)
    {
        if (orbitalPivot == null) yield break;

        Vector3 startScale = orbitalPivot.localScale;
        float timer = 0f;

        while (timer < duration && orbitalPivot != null)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            orbitalPivot.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
    }

    private IEnumerator OrbitalBreathingRoutine(float duration)
    {
        if (orbitalPivot == null) yield break;
        
        float timer = 0f;
        float baseScale = orbitalPivot.localScale.x;
        
        while (isOrbitalActive && orbitalPivot != null)
        {
            timer += Time.deltaTime;
            // 呼吸频率: 每秒1次循环，缩放范围0.8 - 1.2
            float scaleMultiplier = 1f + 0.2f * Mathf.Sin(timer * Mathf.PI * 2f);
            orbitalPivot.localScale = Vector3.one * (baseScale * scaleMultiplier);
            yield return null;
        }
    }

    private Transform FindNearestEnemyTransform() { return FindNearestEnemyTransform(transform.position, StatBlock?.autoAimRange ?? 0f); }

    // Overload for searching from a specific point
    private Transform FindNearestEnemyTransform(Vector3 searchCenter, float searchRadius)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;
        if (StatBlock == null || searchRadius <= 0) return null;

        LayerMask layersToSearch = StatBlock.layersToDamageByAOE == 0 ? LayerMask.GetMask("Enemies") : StatBlock.layersToDamageByAOE;

        Collider[] colliders = Physics.OverlapSphere(searchCenter, searchRadius, layersToSearch);
        foreach (Collider hitCollider in colliders)
        {
            Health enemyHealth = hitCollider.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead && hitCollider.CompareTag("Enemy")) // Check tag too
            {
                float dSqrToTarget = (searchCenter - hitCollider.transform.position).sqrMagnitude;
                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    nearestEnemy = enemyHealth.transform;
                }
            }
        }
        return nearestEnemy;
    }

    private Transform FindNextChainTarget(Vector3 currentPosition, float range, List<Health> alreadyHit)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(currentPosition, range, StatBlock.layersToDamageByAOE);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;
        foreach (var col in nearbyColliders)
        {
            Health potentialTargetHealth = col.GetComponentInParent<Health>();
            if (potentialTargetHealth != null && !potentialTargetHealth.IsDead && !alreadyHit.Contains(potentialTargetHealth))
            {
                float distSqr = (currentPosition - col.transform.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestTarget = potentialTargetHealth.transform;
                }
            }
        }
        return closestTarget;
    }

    /*private void HandleBeamWeapon()
    {
        // 确保激光实例存在
        if (currentBeamInstance == null)
        {
            // 生成激光预制体
            GameObject beamGO = Instantiate(StatBlock.projectilePrefab, firePoint.position, firePoint.rotation, firePoint); // 挂在枪口下
            currentBeamInstance = beamGO.GetComponent<PlayerBeamController>();

            // 初始化为【方向模式】
            // 长度 20，层级 Enemy
            if (currentBeamInstance != null)
            {
                currentBeamInstance.InitializeDirectional(
                    StatBlock,
                    this,
                    firePoint.forward,
                    20f,
                    LayerMask.GetMask("Enemies")
                );
            }
        }

        // 激光是一直存在的，PlayerBeamController 的 Update 会处理伤害
        // 我们只需要确保它跟着枪口转 (因为做了 SetParent，所以自动跟了)
    }*/

    private void ValidateOrFindTarget()
    {
        if (StatBlock == null) return;
        if (lockedBeamTarget != null)
        {
            if (!lockedBeamTarget.gameObject.activeInHierarchy || Vector3.Distance(firePoint.position, lockedBeamTarget.position) > StatBlock.beamMaxDistance)
            { lockedBeamTarget = null; }
        }
        if (lockedBeamTarget == null)
        { lockedBeamTarget = FindNearestEnemyTransform(firePoint.position, StatBlock.beamMaxDistance); }
    }

    private void StartBeam()
    {
        if (StatBlock?.beamVfxPrefab == null || firePoint == null) return;
        GameObject beamGO = Instantiate(StatBlock.beamVfxPrefab, firePoint.position, firePoint.rotation, firePoint);
        activeBeamInstance = beamGO.GetComponent<PlayerBeamController>();
        if (activeBeamInstance != null)
        {
            activeBeamInstance.Initialize(StatBlock, this, lockedBeamTarget); //
            if (beamLoopSound != null && audioSource != null && !audioSource.isPlaying)
            { audioSource.clip = beamLoopSound; audioSource.loop = true; audioSource.Play(); }
        }
        else { Debug.LogError($"Beam VFX missing PlayerBeamController script!", this); Destroy(beamGO); }
    }

    private void StopBeamForStandby()
    {
        if (activeBeamInstance == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        lockedBeamTarget = null;
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); }
    }

    private void StopBeamAndStartCooldown()
    {
        if (activeBeamInstance == null || StatBlock == null) return;
        Destroy(activeBeamInstance.gameObject);
        activeBeamInstance = null;
        beamCooldownTimer = StatBlock.beamCooldown;
        beamEnergyTimer = 0;
        lockedBeamTarget = null;
        if (audioSource != null && audioSource.clip == beamLoopSound) { audioSource.Stop(); }
    }

    public void DeactivateBeam() // When weapon is unequipped
    {
        StopBeamForStandby();
        beamEnergyTimer = 0;
    }

    private Transform FindStableAnchor() // Finds a stable point on the player rig for parenting orbitals/pivots
    {
        // Try finding a specific marker component first
        if (WeaponController.Instance != null)
        {
            StableAnchorMarker marker = WeaponController.Instance.GetComponentInChildren<StableAnchorMarker>();
            if (marker != null) return marker.transform;
        }
        // Fallback to the WeaponController transform itself (player root)
        return WeaponController.Instance?.transform ?? transform; // Use own transform if controller missing
    }
    private void SetupDrones()
    {
        if (StatBlock == null)
        {
            Debug.LogError("[SetupDrones] 失败: StatBlock 是空的！");
            return;
        }

        // 注意：这里保留你原来的 orbitalPrefab，不要改动
        if (StatBlock.orbitalPrefab == null)
        {
            Debug.LogError($"[SetupDrones] 失败: 武器 '{StatBlock.weaponName}' 的 Orbital Prefab 为空！");
            return;
        }

        // 1. 清理旧无人机 (这部分是对的，保留)
        foreach (var drone in activeDrones)
        {
            if (drone != null) Destroy(drone);
        }
        activeDrones.Clear();

        // =========================================================
        // 【核心修改】计算数量时，加上 localOrbitalCountBonus
        // =========================================================

        // 方法 A: 如果你刚才在 WeaponPart 里加了 GetTotalCount() 方法，直接调用它：
        int count = GetTotalCount();

        // 方法 B: 如果你没加那个方法，就手动加上变量：
        // int count = StatBlock.baseOrbitalCount + PlayerStats.Instance.bonusProjectileCount + localOrbitalCountBonus;

        // =========================================================

        int dmg = Mathf.RoundToInt(StatBlock.baseDirectDamage * PlayerStats.Instance.damageMultiplier);
        float fr = PlayerStats.Instance.fireRateMultiplier;

        // 3. 生成 (循环部分基本不用动，保持原样即可)
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 2f;
            spawnPos.y = transform.position.y + 2f;

            GameObject droneGO = Instantiate(StatBlock.orbitalPrefab, spawnPos, Quaternion.identity);

            DroneAI droneScript = droneGO.GetComponent<DroneAI>();
            if (droneScript != null)
            {
                WeaponStatBlock weaponToGive = StatBlock.summonWeaponStats;
                if (weaponToGive == null)
                {
                    // 为了防止空指针报错，建议这里做一个保底，如果没有专属武器，就用本体属性
                    weaponToGive = StatBlock;
                }

                // 初始化
                droneScript.Initialize(weaponToGive, 0, transform.root, dmg, fr);
            }
            activeDrones.Add(droneGO);
        }
    }
    public void RefreshDrones()
    {
        // 1. 安全检查：如果不是召唤类武器，直接退出，防止报错
        if (StatBlock == null || StatBlock.behavior != WeaponBehaviorType.SummonDrone) return;

        // 2. 直接调用我们刚才修改过的 SetupDrones
        // (SetupDrones 里已经包含了“销毁旧飞机”和“重新生成”的逻辑，所以直接调它就行)
        SetupDrones();
    }
    private void SetupAutoBeam()
    {
        if (StatBlock == null || StatBlock.projectilePrefab == null) return;

        // 1. 清理旧坦克
        foreach (var tank in activeLaserTanks)
        {
            if (tank != null) Destroy(tank);
        }
        activeLaserTanks.Clear();

        // 2. --- 【核心修改】计算数量 ---
        // 使用 GetTotalCount() 自动包含：基础 + 全局加成 + 局部加成(升级卡)
        int count = GetTotalCount();
        if (count < 1) count = 1; // 保底 1 辆

        // 3. --- 【核心修改】计算伤害 ---
        // 伤害公式：面板DPS / 频率 * (全局倍率 + 局部倍率 + 石头倍率)
        float stoneDmgMod = (currentStone != null) ? currentStone.damageModifier : 0f;
        float finalDmgMult = PlayerStats.Instance.damageMultiplier + localDamageBonus + stoneDmgMod;

        int damagePerTick = Mathf.CeilToInt((float)StatBlock.beamDamagePerSecond / StatBlock.beamDamageTickRate);
        damagePerTick = Mathf.RoundToInt(damagePerTick * finalDmgMult);

        // 4. --- 【核心修改】计算暴击 ---
        float finalCritRate = PlayerStats.Instance.critRate + StatBlock.baseCritRate + localCritRateBonus;
        float finalCritDmg = PlayerStats.Instance.critDamage + StatBlock.baseCritDamage + localCritDamageBonus;

        // 5. 生成坦克
        for (int i = 0; i < count; i++)
        {
            // 生成在玩家周围地面上
            Vector3 spawnOffset = Quaternion.Euler(0, i * 360f / count, 0) * Vector3.forward * 3f;
            Vector3 potentialPos = transform.position + spawnOffset;
            RaycastHit hit;
            if (Physics.Raycast(potentialPos + Vector3.up * 5f, Vector3.down, out hit, 10f, LayerMask.GetMask("Default", "Ground", "Terrain")))
            {
                potentialPos.y = hit.point.y;
            }
            else
            {
                potentialPos.y = 0;
            }

            GameObject tankGO = Instantiate(StatBlock.projectilePrefab, potentialPos, Quaternion.identity);

            // 初始化脚本
            AutoBeamTurret tankScript = tankGO.GetComponent<AutoBeamTurret>();
            if (tankScript != null)
            {
                // 【修改】传入暴击参数
                tankScript.Initialize(this, damagePerTick, StatBlock.beamDamageTickRate, transform.root, finalCritRate, finalCritDmg);
            }
            activeLaserTanks.Add(tankGO);
        }
    }

    /// <summary>
    /// 部署镭射核心 — 浮空跟随玩家，发射聚焦光束
    /// </summary>
    private void SetupLaserCore()
    {
        if (StatBlock == null || StatBlock.projectilePrefab == null) return;

        // 1. 清理旧核心
        foreach (var core in activeLaserCores)
        {
            if (core != null) Destroy(core);
        }
        activeLaserCores.Clear();

        // 2. 计算伤害
        float stoneDmgMod = (currentStone != null) ? currentStone.damageModifier : 0f;
        float finalDmgMult = PlayerStats.Instance.damageMultiplier + localDamageBonus + stoneDmgMod;

        int damagePerTick = Mathf.CeilToInt((float)StatBlock.beamDamagePerSecond / StatBlock.beamDamageTickRate);
        damagePerTick = Mathf.RoundToInt(damagePerTick * finalDmgMult);

        // 3. 计算暴击
        float finalCritRate = PlayerStats.Instance.critRate + StatBlock.baseCritRate + localCritRateBonus;
        float finalCritDmg = PlayerStats.Instance.critDamage + StatBlock.baseCritDamage + localCritDamageBonus;

        // 4. 计算核心数量（基础1 + 数量加成）
        int count = GetTotalCount();
        if (count < 1) count = 1;
        if (IsEngineerSkillActive("Engineer_Overclock_LaserGrid"))
        {
            count += 1;
            damagePerTick = Mathf.RoundToInt(damagePerTick * 1.12f);
        }

        // 5. 折射数量
        int refraction = localLaserRefractionCount;
        if (IsEngineerSkillActive("Engineer_Overclock_LaserGrid"))
        {
            refraction += 1;
        }

        // 6. 生成镭射核心
        for (int i = 0; i < count; i++)
        {
            // 生成在玩家头顶（核心脚本会自己跟随）
            Vector3 spawnPos = transform.position + Vector3.up * 2f
                             + Quaternion.Euler(0, i * 360f / count, 0) * Vector3.right * 0.8f;

            GameObject coreGO = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.identity);

            LaserCoreController coreScript = coreGO.GetComponent<LaserCoreController>();
            if (coreScript != null)
            {
                coreScript.Initialize(this, damagePerTick, StatBlock.beamDamageTickRate,
                                      transform.root, finalCritRate, finalCritDmg, refraction);

                // 将技能树中的聚焦加成传入核心
                if (localLaserFocusBonus > 0)
                {
                    coreScript.focusDamageBonus += localLaserFocusBonus;
                }

                // 从 StatBlock 读取光束持续时间和冷却
                if (StatBlock.beamDuration > 0) coreScript.overheatDelay = StatBlock.beamDuration;
                if (StatBlock.beamCooldown > 0) coreScript.cooldownTime = StatBlock.beamCooldown;
                if (IsEngineerSkillActive("Engineer_Overclock_LaserGrid"))
                {
                    coreScript.cooldownTime = Mathf.Max(0.5f, coreScript.cooldownTime * 0.8f);
                }

                // 过热AOE范围：基础 × (1 + 局部范围加成)
                if (localAreaBonus > 0)
                {
                    coreScript.overheatAoeRadius *= (1f + localAreaBonus);
                }

                // 冷却缩减：冷却时间 × (1 - 局部冷却缩减)
                if (localFireRateBonus != 0)
                {
                    coreScript.cooldownTime = Mathf.Max(0.5f, coreScript.cooldownTime * (1f - localFireRateBonus));
                }

                // 核心熔毁：开启灼烧区域
                coreScript.meltdownEnabled = localLaserMeltdownEnabled;

                // 命中特效
                if (StatBlock.beamImpactVfxPrefab != null)
                {
                    coreScript.impactVfxPrefab = StatBlock.beamImpactVfxPrefab;
                }
            }

            activeLaserCores.Add(coreGO);
        }
    }

    private void SetupFunnelSystem()
    {
        if (StatBlock == null || StatBlock.projectilePrefab == null) return;

        // 1. 清理旧浮游炮
        foreach (var f in activeFunneIs) { if (f != null) Destroy(f); }
        activeFunneIs.Clear();

        // 2. 设定数量 (固定6个，或者受加成影响)
        int count = 6 + PlayerStats.Instance.bonusProjectileCount;

        // 3. 计算伤害
        int laserDmg = Mathf.RoundToInt(StatBlock.beamDamagePerSecond / StatBlock.beamDamageTickRate * PlayerStats.Instance.damageMultiplier);

        // 4. 生成阵列
        for (int i = 0; i < count; i++)
        {
            // 初始生成在玩家身后
            Vector3 spawnPos = transform.position + Vector3.up * 5f;

            GameObject funnelGO = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.identity);

            OrbitalJudgmentFunnel script = funnelGO.GetComponent<OrbitalJudgmentFunnel>();
            if (script != null)
            {
                // 初始化：传入 index 用于计算环绕角度
                script.Initialize(this, laserDmg, StatBlock.beamDamageTickRate, transform.root, i, count);

                // 可以在这里把导弹预制体传进去 (如果 WeaponStatBlock 里有槽位的话)
                // script.missilePrefab = StatBlock.summonWeaponStats.projectilePrefab; 
            }
            activeFunneIs.Add(funnelGO);
        }
    }
    private void SetupSuperMech()
    {
        if (StatBlock == null || StatBlock.projectilePrefab == null) return;

        // 清理旧的
        if (currentSuperMech != null) Destroy(currentSuperMech);

        // 生成在玩家旁边
        Vector3 spawnPos = transform.position + transform.right * 2f;

        // Raycast 找地面，确保贴地生成
        RaycastHit hit;
        if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out hit, 10f, LayerMask.GetMask("Default", "Ground")))
        {
            spawnPos.y = hit.point.y;
        }

        currentSuperMech = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.identity);

        SuperMechAI mechScript = currentSuperMech.GetComponent<SuperMechAI>();
        if (mechScript != null)
        {
            // 传入整个 StatBlock，让机甲自己读取伤害和持续时间配置
            mechScript.Initialize(this, StatBlock, transform.root);
        }
    }
    private void InstantiateAndFireAirdropDeployer(Vector3 targetPosition)
    {
        if (StatBlock?.deployerProjectilePrefab == null || firePoint == null || WeaponController.Instance == null) return;

        float spawnHeight = StatBlock.deployerSpawnHeight;
        float horizontalOffset = 5f; // Offset from target

        Vector3 directionFromPlayer = (targetPosition - WeaponController.Instance.transform.position).normalized; // Use player position
        directionFromPlayer.y = 0;
        Vector3 startPosition = targetPosition + (Vector3.up * spawnHeight) - (directionFromPlayer * horizontalOffset);
        Vector3 fallDirection = (targetPosition - startPosition).normalized; // Direction towards target on ground

        GameObject deployerGO = Instantiate(StatBlock.deployerProjectilePrefab, startPosition, Quaternion.LookRotation(fallDirection));
        Projectile projectileScript = deployerGO.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            int finalDamage = Mathf.RoundToInt(StatBlock.baseAreaDamagePerTick * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
            float finalDuration = StatBlock.baseAreaDuration;
            float finalInterval = StatBlock.baseAreaTickInterval;

            projectileScript.InitializeAsAirdropDeployer(
                startPosition, fallDirection, StatBlock.deployerFallSpeed, StatBlock.areaPrefab,
                finalDamage, finalDuration, finalInterval, WeaponController.Instance.gameObject // Attacker is the Player object
            );
        }
        else { Destroy(deployerGO); } // Clean up
    }

    private void HandleLandminePlacement()
    {
        if (StatBlock == null || !IsReadyToFire || WeaponController.Instance == null) return;

        int mineCount = 1 + localMineCountBonus;
        bool engineerMinefield = IsEngineerSkillActive("Engineer_Fortress_Minefield");
        if (engineerMinefield)
        {
            mineCount += 1;
        }

        for (int i = 0; i < mineCount; i++)
        {
            Vector2 randomCirclePoint = Random.insideUnitCircle * StatBlock.spawnRadius;
            Vector3 spawnPositionBase = WeaponController.Instance.transform.position + new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);

            RaycastHit hit;
            Vector3 spawnPosition = spawnPositionBase;
            LayerMask groundMask = StatBlock.beamScorchMarkGroundLayer != 0 ? StatBlock.beamScorchMarkGroundLayer : LayerMask.GetMask("Ground");
            if (Physics.Raycast(spawnPositionBase + Vector3.up * 5f, Vector3.down, out hit, 10f, groundMask))
            { spawnPosition = hit.point; }

            if (StatBlock.minePrefab != null)
            {
                GameObject mineGO = Instantiate(StatBlock.minePrefab, spawnPosition, Quaternion.identity);
                Landmine mineScript = mineGO.GetComponent<Landmine>();
                if (mineScript != null)
                {
                    int finalDamage = Mathf.RoundToInt(StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier + PlayerStats.Instance.flatAoeDamageBonus);
                    float finalRadius = StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier;
                    if (engineerMinefield)
                    {
                        finalDamage = Mathf.RoundToInt(finalDamage * 1.12f);
                        finalRadius *= 1.2f;
                    }
                    mineScript.Initialize(
                        finalDamage,
                        finalRadius,
                        StatBlock.armingTime,
                        StatBlock.mineDuration,
                        WeaponController.Instance.gameObject,
                        StatBlock.explosionEffectPrefab,
                        StatBlock.layersToDamageByAOE,
                        this
                    );
                }
                else { Debug.LogWarning($"Mine prefab '{StatBlock.minePrefab.name}' is missing Landmine script.", mineGO); }

                if (i == 0 && landminePlaceSound != null && audioSource != null)
                { AudioSource.PlayClipAtPoint(landminePlaceSound, spawnPosition); }
            }
        }

        // Reset cooldown
        fireCooldown = (1f / StatBlock.baseFireRate) * Mathf.Max(0.1f, PlayerStats.Instance.fireRateMultiplier - GetEngineerMechanicalFireRateBonus());
    }
    public void FuseEnergyStone(EnergyStoneSO newStone) 
    {
        if (newStone == null)
        {
            Debug.LogError("[FuseEnergyStone] 失败! 传入的 newStone 是 null!");
            return;
        }



        EnergyStoneSO oldStone = this.currentStone; // (获取旧石头，用于 PlayerStats)

        // 2. 覆盖旧的能量石
        this.currentStone = newStone; //

        // 3. 立即检查
        if (this.currentStone != null)
        {
        }
        else
        {
            // 如果这个日志出现，就意味着发生了严重的内部错误
            Debug.LogError("[FuseEnergyStone] 2. 严重错误! 刚刚设置了 currentStone，但它仍然是 null!");
        }

        // 4. (这是我们为“冰冻计数器” 添加的逻辑，请确保它在你的脚本里)
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.RegisterStone(newStone, oldStone); //
        }
        // --- ^^^ [ 核心调试 ] ^^^ ---


        // 5. 融合后，立即刷新武器状态
        RefreshWeaponStateFromStone(); //
    }

    public void RemoveEnergyStone() 
    {
        EnergyStoneSO oldStone = this.currentStone; // 1. 暂存旧石头

        this.currentStone = null; // 2. 移除石头

        // 3. 向全局计数器报告
        if (oldStone != null && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.RegisterStone(null, oldStone);
        }        

        RefreshWeaponStateFromStone();
    }

    public void RefreshWeaponStateFromStone()
    {
        if (StatBlock.behavior == WeaponBehaviorType.Aura) RefreshAura();
        if (StatBlock.behavior == WeaponBehaviorType.Orbital) RefreshOrbiters();

        if (StatBlock.behavior == WeaponBehaviorType.SummonDrone) RefreshDrones();
        if (StatBlock.behavior == WeaponBehaviorType.FlyingDagger) SetupFlyingDaggers(); // Refresh flying daggers

        if (StatBlock.behavior == WeaponBehaviorType.Beam) SetupAutoBeam();
        if (StatBlock.behavior == WeaponBehaviorType.LaserCore) SetupLaserCore();

        // 进化/融合后，刷新模型
        UpdateVisualModel();
    }
    private void UpdateVisualModel()
    {
        if (floatingVisual == null || StatBlock == null) return;

        // 1. 换装
        if (StatBlock.floatingModelPrefab != null)
        {
            GameObject newModelInstance = floatingVisual.SwapModel(StatBlock.floatingModelPrefab);

            // 2. 获取新模型上的材质脚本
            if (newModelInstance != null)
            {
                cooldownMaterial = newModelInstance.GetComponent<WeaponCooldownMaterial>();
                if (cooldownMaterial == null)
                    cooldownMaterial = newModelInstance.GetComponentInChildren<WeaponCooldownMaterial>();
            }
        }

        // --- 【核心修复】同步引用给 PlayerBladeAttack ---
        // 因为未进化时，是 PlayerBladeAttack 在控制攻击和冷却表现
        // 所以必须把新生成的 visual 和 material 告诉它

        var meleeAttackScript = GetComponent<PlayerBladeAttack>();
        if (meleeAttackScript != null)
        {
            // 1. 同步材质引用 (修复发光失效)
            meleeAttackScript.weaponCooldownMaterial = this.cooldownMaterial;

            // 2. 同步控制器引用 (确保能控制显隐)
            meleeAttackScript.floatingWeapon = this.floatingVisual;

            // 调试日志
            // Debug.Log($"[WeaponPart] 已将视觉组件同步给 PlayerBladeAttack. Material: {cooldownMaterial != null}");
        }
        // ---------------------------------------------
    }

    private IEnumerator MeleeAttackRoutine(int baseDamage, float scale)
    {
        int count = StatBlock.multiHitCount;
        if (count < 1) count = 1;

        for (int i = 0; i < count; i++)
        {
            if (StatBlock.autoAimMelee) RotateTowardsNearestEnemy();

            // 【修正】这里直接传 baseDamage，让 VFXDamageController 去碰瓷的时候自己算暴击
            SpawnMeleeVFX(baseDamage, scale);

            PlayFireSound();

            if (count > 1) yield return new WaitForSeconds(StatBlock.multiHitInterval);
        }
    }
    private void SpawnMeleeVFX(int damage, float scale) // 【注意】这里可以删掉上一轮加的 bool isCrit 参数了
    {
        if (StatBlock.slashEffectPrefab == null) return;

        Vector3 spawnPos = firePoint.position;
        Quaternion spawnRot = transform.rotation;

        GameObject vfxObj = Instantiate(StatBlock.slashEffectPrefab, spawnPos, spawnRot);
        vfxObj.transform.localScale = Vector3.one * (StatBlock.baseAoeRadius * scale);

        VFXDamageController vfxCtrl = vfxObj.GetComponent<VFXDamageController>();
        if (vfxCtrl != null)
        {
            // 【修正】去掉 isCrit 参数，只传 damage
            vfxCtrl.Initialize(damage, StatBlock.hitEffectPrefab, this.gameObject, this);
        }
    }
    private void RotateTowardsNearestEnemy()
    {
        // 使用 StatBlock.autoAimRange 或默认一个范围
        float range = StatBlock.autoAimRange > 0 ? StatBlock.autoAimRange : 10f;
        Transform target = FindNearestEnemyTransform(transform.position, range);

        if (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            dir.y = 0; // 保持水平
            if (dir != Vector3.zero)
            {
                // 瞬间转身，保证刺击准确
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    private IEnumerator PerformMultiThrustRoutine(int damage, float scale)
    {
        // 1. 锁定目标方向
        if (StatBlock.autoAimMelee)
        {
            RotateTowardsNearestEnemy();
        }

        // 2. 循环执行刺击
        for (int i = 0; i < StatBlock.multiHitCount; i++)
        {
            // 每次刺击都微调方向 (防止敌人跑太快打空)
            if (StatBlock.autoAimMelee) RotateTowardsNearestEnemy();

            // 播放音效
            PlayFireSound();

            // 生成刺击特效 (雷光特效)
            if (StatBlock.slashEffectPrefab != null)
            {
                // 稍微向前偏移一点，让特效看起来是从武器尖端发出的
                Vector3 spawnPos = firePoint.position + transform.forward * 0.5f;
                GameObject vfx = Instantiate(StatBlock.slashEffectPrefab, spawnPos, transform.rotation);
                vfx.transform.localScale = Vector3.one * scale; // 刺击特效通常比较细长
            }

            // 执行判定 (这里用 BoxCast 模拟刺击的长条形判定)
            PerformThrustHitCheck(damage, scale);

            // 等待间隔
            yield return new WaitForSeconds(StatBlock.multiHitInterval);
        }
    }

    private void PerformThrustHitCheck(int baseDamage, float scale)
    {
        // 刺击参数：长方形判定
        // 宽度由 scale 决定，长度由 baseAoeRadius 决定
        float thrustWidth = 1.5f * scale;
        float thrustDistance = StatBlock.baseAoeRadius * scale;

        Vector3 center = firePoint.position;
        Vector3 halfExtents = new Vector3(thrustWidth / 2, 1f, thrustWidth / 2); // 高度给1f防止漏怪
        Quaternion orientation = transform.rotation;

        // 使用 BoxCastAll 穿透所有敌人
        RaycastHit[] hits = Physics.BoxCastAll(center, halfExtents, transform.forward, orientation, thrustDistance, StatBlock.layersToDamageByAOE);

        foreach (var hit in hits)
        {
            if (!hit.collider.CompareTag("Enemy")) continue;

            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null)
            {
                // --- 【核心修改】暴击判定 ---
                bool isCrit = Random.value <= PlayerStats.Instance.critRate;

                // 计算最终伤害
                int finalDamage = baseDamage;
                if (isCrit)
                {
                    finalDamage = Mathf.RoundToInt(baseDamage * PlayerStats.Instance.critDamage);
                }

                // 造成伤害 (可以把 isCrit 传给 TakeDamage 用于飘字显示暴击颜色，如果你的 Health 支持的话)
                // 这里暂时用 null 占位，或者你可以扩展 AttackType
                h.TakeDamage(finalDamage, hit.point, gameObject, AttackType.Standard, null, null, StatBlock.weaponName);

                // --- 【核心修改】触发逻辑：暴击触发闪电链 ---
                bool triggerChain = false;

                // 条件 1: 是雷霆长矛 (通过名字或 Tag 判断，暂时用名字示例) 且发生了暴击
                // 或者你可以去 WeaponStatBlock 加个 bool triggerChainOnCrit;
                if (StatBlock.weaponName.Contains("雷霆长矛") && isCrit)
                {
                    triggerChain = true;
                }

                // 条件 2: 原有的雷电石逻辑 (保持兼容)
                if (currentStone != null && (currentStone.applyChain || currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)))
                {
                    triggerChain = true;
                }

                if (triggerChain)
                {
                    // 触发闪电链：伤害通常衰减 (比如 50% 或 100%)
                    // 这里的 chainCount 和 range 可以从 StatBlock 读取
                    int chainDmg = Mathf.RoundToInt(finalDamage * 0.5f); // 连锁伤害减半
                    int chainCount = StatBlock.baseChainCount > 0 ? StatBlock.baseChainCount : 3; // 保底 3 个
                    float chainRange = StatBlock.chainRange > 0 ? StatBlock.chainRange : 8f;

                    // 调用现有的连锁方法
                    ChainLightningFromTarget(h.transform, chainCount, chainDmg, chainRange);

                    // 可选：暴击时的特殊音效或特效
                    if (isCrit)
                    {
                        // PlayCritSound(); 
                    }
                }

                // 产生命中特效
                if (StatBlock.hitEffectPrefab != null)
                {
                    Instantiate(StatBlock.hitEffectPrefab, hit.point, Quaternion.identity);
                }
            }
        }
    }

    private void PerformMeleeAttack(Vector3 dir, int damage, float scale)
    {
        // 1. 播放特效
        if (StatBlock.slashEffectPrefab != null)
        {
            GameObject vfx = Instantiate(StatBlock.slashEffectPrefab, firePoint.position, Quaternion.LookRotation(dir));
            vfx.transform.localScale = Vector3.one * (StatBlock.baseAoeRadius * scale);
        }       

        // 2. 范围检测
        float range = StatBlock.baseAoeRadius * scale;
        Collider[] hits = Physics.OverlapSphere(firePoint.position, range, StatBlock.layersToDamageByAOE);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            // 角度检测 (简单版)
            Vector3 toTarget = (hit.transform.position - firePoint.position).normalized;
            // Vector3.Angle 返回 0-180。如果 attackAngle 是 180，则只要在前方 90 度内都算命中。
            if (Vector3.Angle(dir, toTarget) < StatBlock.attackAngle / 2)
            {
                Health h = hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(damage, hit.transform.position, gameObject, AttackType.Standard, null, null, StatBlock.weaponName);

                    // 爆炎斩：由于是 MeleeAOE，可以在这里额外施加 StatusEffectReceiver 的 Burn
                    // 这一步其实已经在 WeaponStatBlock 的 currentStone 逻辑里处理了 (如果你的 WeaponPart Update 里有通用命中处理)
                    // 但 Melee 是瞬间判定，所以需要在这里手动补一下石头效果：
                    if (currentStone != null)
                    {
                        StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                        if (receiver != null && currentStone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn))
                        {
                            receiver.ApplyBurn(currentStone.burnDamage, currentStone.burnDuration, currentStone.burnTickInterval, StatBlock.weaponName);
                        }
                    }
                }
            }
        }
    }
    // --- 新增：追踪飞刀设置 ---
    void SetupFlyingDaggers()
    {
        if (StatBlock.projectilePrefab == null) return;

        int desiredCount = GetTotalCount() + daggerExtraCount;
        int currentCount = activeFlyingDaggers.Count;

        float damageMult = PlayerStats.Instance != null ? (PlayerStats.Instance.damageMultiplier + localDamageBonus) : 1f;
        int finalDamage = Mathf.RoundToInt(StatBlock.baseDirectDamage * damageMult + 
            (PlayerStats.Instance != null ? PlayerStats.Instance.flatDamageBonus : 0));
        float knockback = StatBlock.baseKnockbackForce;

        // 情况1：数量不足，补充生成
        if (currentCount < desiredCount)
        {
            int needed = desiredCount - currentCount;
            for (int i = 0; i < needed; i++)
            {
                // 生成飞刀，初始位置在玩家周围
                Vector3 spawnPos = transform.position + Random.onUnitSphere * 2f;
                spawnPos.y = transform.position.y + 1f; // 抬高一点

                GameObject daggerObj = Instantiate(StatBlock.projectilePrefab, spawnPos, Quaternion.identity);
                
                // 获取控制器并初始化 - FlameDaggerController 优先（含升级逻辑）
                FlameDaggerController flameController = daggerObj.GetComponent<FlameDaggerController>();
                FlyingDaggerController flyingController = daggerObj.GetComponent<FlyingDaggerController>();
                
                if (flameController != null)
                {
                    // 移除多余的 FlyingDaggerController，避免双重碰撞伤害
                    if (flyingController != null) Destroy(flyingController);
                    flameController.Initialize(this.StatBlock, transform, finalDamage, knockback, this);
                }
                else if (flyingController != null)
                {
                    flyingController.Initialize(this.StatBlock, transform, finalDamage, knockback, this);
                }
                else
                {
                    // 预制件上没有任何飞刀控制器，添加默认的FlyingDaggerController
                    flyingController = daggerObj.AddComponent<FlyingDaggerController>();
                    flyingController.Initialize(this.StatBlock, transform, finalDamage, knockback, this);
                }

                activeFlyingDaggers.Add(daggerObj);
            }
        }
        // 情况2：数量过多，销毁多余 (极少发生，除非洗点)
        else if (currentCount > desiredCount)
        {
            int removeCount = currentCount - desiredCount;
            for (int i = 0; i < removeCount; i++)
            {
                int lastIdx = activeFlyingDaggers.Count - 1;
                GameObject obj = activeFlyingDaggers[lastIdx];
                if (obj != null) Destroy(obj);
                activeFlyingDaggers.RemoveAt(lastIdx);
            }
        }
        
        // 情况3：仅仅是属性刷新 (比如升级了伤害)
        // 遍历所有现存飞刀，更新属性
        foreach (var dagger in activeFlyingDaggers)
        {
            if (dagger != null)
            {
                // FlameDaggerController 优先（含升级逻辑）
                FlameDaggerController flameController = dagger.GetComponent<FlameDaggerController>();
                FlyingDaggerController flyingController = dagger.GetComponent<FlyingDaggerController>();
                
                if (flameController != null)
                {
                    // 移除多余的 FlyingDaggerController，避免双重碰撞伤害
                    if (flyingController != null) Destroy(flyingController);
                    flameController.Initialize(this.StatBlock, transform, finalDamage, knockback, this);
                }
                else if (flyingController != null)
                {
                    flyingController.Initialize(this.StatBlock, transform, finalDamage, knockback, this);
                }
            }
        }
    }

    private bool IsMetaUnlocked(string nodeFileName)
    {
        if (PlayerProgressManager.Instance != null)
        {
            // 这里我们直接检查 unlockedNodeIDs 集合
            // 注意：nodeFileName 必须和你创建的 WeaponUpgradeNode 文件名完全一致
            return PlayerProgressManager.Instance.unlockedItems.Contains(nodeFileName) ||
                   PlayerProgressManager.Instance.IsNodeUnlockedRaw(nodeFileName);
        }
        return false;
    }

    void OnDisable()
    {
        // 清理无人机
        foreach (var drone in activeDrones)
        {
            if (drone != null) Destroy(drone);
        }
        activeDrones.Clear();

        // 清理激光坦克 (如果你之前加了这个列表)
        foreach (var tank in activeLaserTanks)
        {
            if (tank != null) Destroy(tank);
        }
        activeLaserTanks.Clear();

        // 清理超武机甲 (防止机甲残留)
        if (currentSuperMech != null) Destroy(currentSuperMech);
    }
    #endregion

}

