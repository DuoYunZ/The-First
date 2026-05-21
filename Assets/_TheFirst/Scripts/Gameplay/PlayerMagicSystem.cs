using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 法师专属战斗系统 —— 管理法师角色卡激活的技能效果
/// 类比剑士的 PlayerBladeAttack，由法师底盘预制件上挂载
///
/// 当前支持的技能：
///   - IcePath / FirePath: 分支选择（初始武器替换由 CombatSceneInitializer 处理）
///   - Mage_Ice_Barrage: 冰锥连射（穿透计数 → 8枚冰锥爆发）
///   - Mage_Ice_Hail: 冰雹风暴（大招增强 - 暂为框架）
///   - Mage_Ice_Thunder: 毁灭雷击（冰冻目标100%暴击）
///   - Mage_Fire_Ignite: 燃烧大地（火球造伤概率生成火海）
///   - Mage_Fire_Trail: 烈焰轨迹（大招路径燃烧 - 暂为框架）
///   - Mage_Fire_Wind: 风助火势（飓风扩散火海 - 暂为框架）
///   - Mage_Talent_Blizzard: 永冻领域
///   - Mage_Talent_Inferno: 炼狱之焰
/// </summary>
public class PlayerMagicSystem : MonoBehaviour
{
    public static PlayerMagicSystem Instance { get; private set; }

    [Header("冰锥连射设置")]
    [Tooltip("冰锥穿透多少次触发一次连射")]
    public int iceBarragePenetrateThreshold = 20;
    [Tooltip("连射时发射多少枚冰锥")]
    public int iceBarrageProjectileCount = 8;

    [Header("燃烧大地设置")]
    [Tooltip("火球造伤时点燃地面的概率")]
    public float fireIgniteChance = 0.15f;
    [Tooltip("火海预制件")]
    public GameObject firePoolPrefab;
    [Tooltip("火海持续时间")]
    public float firePoolDuration = 3f;

    [Header("永冻领域设置")]
    [Tooltip("永冻领域预制件")]
    public GameObject blizzardZonePrefab;
    [Tooltip("永冻领域冷却时间")]
    public float blizzardCooldown = 20f;
    [Tooltip("永冻领域持续时间")]
    public float blizzardDuration = 6f;

    [Header("冰雹风暴设置")]
    [Tooltip("冰雹风暴预制件（带 HailStormZone 组件 + 粒子特效）")]
    public GameObject hailStormPrefab;
    [Tooltip("冰雹风暴伤害（每秒）")]
    public int hailDamagePerTick = 20;
    [Tooltip("冰雹风暴范围")]
    public float hailRadius = 8f;
    [Tooltip("冰雹风暴持续时间")]
    public float hailDuration = 5f;
    [Tooltip("Mage_Ice_Hail: ice weapon hits required before spawning hail.")]
    public int iceHailHitThreshold = 14;
    [Tooltip("Mage_Ice_Hail: minimum seconds between hail spawns.")]
    public float iceHailCooldown = 7f;

    [Header("炼狱之焰设置")]
    [Tooltip("触发炼狱所需的最少火海数量")]
    public int infernoRequiredFirePools = 3;
    [Tooltip("炼狱冷却时间")]
    public float infernoCooldown = 25f;
    [Tooltip("炼狱持续时间")]
    public float infernoDuration = 5f;
    [Tooltip("炼狱期间火海伤害倍率")]
    public float infernoDamageMultiplier = 3f;

    // 运行时状态
    private int icePenetrateCount = 0;
    private int iceHailHitCount = 0;
    private float blizzardTimer = 0f;
    private float iceHailTimer = 0f;
    private float infernoTimer = 0f;
    private bool infernoActive = false;

    /// <summary>
    /// 永冻领域是否激活中（冰锥穿透不消耗）
    /// </summary>
    [HideInInspector]
    public bool isBlizzardActive = false;

    // 场上活跃的火海列表（由 FirePoolController 注册/注销）
    private List<GameObject> activeFirePools = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (DemoContentGate.DemoModeEnabled && DemoContentGate.DisableUltimateSystemInDemo)
        {
            return;
        }

        // 延迟订阅大招事件（UltimateManager 可能还没初始化）
        StartCoroutine(DelayedSubscribe());
    }

    /// <summary>
    /// 延迟订阅，确保 UltimateManager 已初始化
    /// </summary>
    private IEnumerator DelayedSubscribe()
    {
        // 等待 UltimateManager 初始化完成
        while (UltimateManager.Instance == null)
        {
            yield return null;
        }
        UltimateManager.Instance.OnUltimateReleasedWithID += OnUltimateReleasedByID;
        Debug.Log("<color=cyan>[法师系统] 成功订阅大招事件</color>");
    }

    void OnDestroy()
    {
        if (UltimateManager.Instance != null)
        {
            UltimateManager.Instance.OnUltimateReleasedWithID -= OnUltimateReleasedByID;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 大招释放回调（通过 weaponID 判断） —— 触发冰雹风暴
    /// </summary>
    private void OnUltimateReleasedByID(string weaponID)
    {
        if (!HasMageSkill("Mage_Ice_Hail")) return;

        // 通过 weaponID 判断是否为冰锥武器的大招
        if (string.IsNullOrEmpty(weaponID) || !weaponID.Contains("IceShard")) return;

        Debug.Log($"<color=cyan>[冰雹风暴] 检测到冰锥大招释放 (weaponID={weaponID})，触发冰雹!</color>");
        SpawnHailStorm();
    }

    /// <summary>
    /// 在玩家位置生成冰雹风暴区域
    /// </summary>
    private void SpawnHailStorm()
    {
        Transform playerT = GameManager.Instance?.playerTransform;
        if (playerT == null) return;

        SpawnHailStorm(playerT.position);
    }

    private void SpawnHailStorm(Vector3 spawnPos)
    {
        spawnPos.y = 0.05f;

        // 如果有预制件，使用预制件
        if (hailStormPrefab != null)
        {
            GameObject hailGo = Instantiate(hailStormPrefab, spawnPos, Quaternion.identity);
            HailStormZone zone = hailGo.GetComponent<HailStormZone>();
            if (zone == null) zone = hailGo.AddComponent<HailStormZone>();
            zone.radius = hailRadius;
            zone.duration = hailDuration;
            zone.damagePerTick = hailDamagePerTick;
        }
        else
        {
            // 没有预制件时创建纯逻辑区域
            GameObject hailGo = new GameObject("HailStormZone_Runtime");
            hailGo.transform.position = spawnPos;
            HailStormZone zone = hailGo.AddComponent<HailStormZone>();
            zone.radius = hailRadius;
            zone.duration = hailDuration;
            zone.damagePerTick = hailDamagePerTick;
        }

        Debug.Log($"<color=cyan>[冰雹风暴] 在 {spawnPos} 生成冰雹区域! 半径:{hailRadius} 持续:{hailDuration}秒</color>");
    }

    void Update()
    {
        // 永冻领域计时
        if (blizzardTimer > 0f) blizzardTimer -= Time.deltaTime;
        if (iceHailTimer > 0f) iceHailTimer -= Time.deltaTime;

        // 炼狱之焰计时
        if (infernoTimer > 0f) infernoTimer -= Time.deltaTime;

        // 检查永冻领域触发
        if (HasMageSkill("Mage_Talent_Blizzard") && blizzardTimer <= 0f)
        {
            TriggerBlizzardZone();
            blizzardTimer = blizzardCooldown;
        }

        // 检查炼狱之焰触发
        if (HasMageSkill("Mage_Talent_Inferno") && !infernoActive && infernoTimer <= 0f)
        {
            CleanupDeadFirePools();
            if (activeFirePools.Count >= infernoRequiredFirePools)
            {
                StartCoroutine(TriggerInferno());
            }
        }
    }

    // =============================================
    // 公共接口（供其他系统调用）
    // =============================================

    /// <summary>
    /// 冰锥穿透时由弹体调用，累加穿透计数
    /// </summary>
    public void OnIcePenetrate(WeaponPart sourceWeapon)
    {
        Vector3 hitPosition = sourceWeapon != null ? sourceWeapon.transform.position : transform.position;
        OnIceWeaponHit(sourceWeapon, hitPosition);
    }

    public void OnIceWeaponHit(WeaponPart sourceWeapon, Vector3 hitPosition)
    {
        if (HasMageSkill("Mage_Ice_Barrage"))
        {
            icePenetrateCount++;
            if (icePenetrateCount >= iceBarragePenetrateThreshold)
            {
                icePenetrateCount = 0;
                StartCoroutine(FireIceBarrageSequential(sourceWeapon));
            }
        }

        if (!HasMageSkill("Mage_Ice_Hail")) return;

        iceHailHitCount++;
        if (iceHailHitCount >= Mathf.Max(1, iceHailHitThreshold) && iceHailTimer <= 0f)
        {
            iceHailHitCount = 0;
            iceHailTimer = iceHailCooldown;
            SpawnHailStorm(hitPosition);
        }
    }

    /// <summary>
    /// 火球造成伤害时调用，概率生成火海
    /// </summary>
    public void OnFireballDamage(Vector3 hitPosition, float baseDamage)
    {
        if (!HasMageSkill("Mage_Fire_Ignite")) return;

        if (Random.value < fireIgniteChance)
        {
            SpawnFirePool(hitPosition, baseDamage);
        }
    }

    /// <summary>
    /// 注册/注销火海（由 FirePoolController 调用）
    /// </summary>
    public void RegisterFirePool(GameObject pool)
    {
        if (pool != null && !activeFirePools.Contains(pool))
            activeFirePools.Add(pool);
    }

    public void UnregisterFirePool(GameObject pool)
    {
        activeFirePools.Remove(pool);
    }

    /// <summary>
    /// 检查冰冻目标是否获得100%暴击（毁灭雷击）
    /// </summary>
    public bool ShouldCritFrozenTarget()
    {
        return HasMageSkill("Mage_Ice_Thunder");
    }

    /// <summary>
    /// 获取炼狱是否激活中（火海伤害倍率用）
    /// </summary>
    public float GetFirePoolDamageMultiplier()
    {
        return infernoActive ? infernoDamageMultiplier : 1f;
    }

    // =============================================
    // 内部实现
    // =============================================

    /// <summary>
    /// 冰锥连射：向周围8个方向发射冰锥
    /// </summary>
    /// <summary>
    /// 冰锥连射（协程版）：顺时针依次发射冰锥，每枚之间有短暂间隔
    /// </summary>
    private IEnumerator FireIceBarrageSequential(WeaponPart sourceWeapon)
    {
        if (sourceWeapon == null || sourceWeapon.StatBlock == null) yield break;
        if (sourceWeapon.StatBlock.projectilePrefab == null) yield break;

        Transform playerT = GameManager.Instance?.playerTransform;
        if (playerT == null) yield break;

        WeaponStatBlock stats = sourceWeapon.StatBlock;
        // 连射弹伤害为武器基础伤害的50%
        int barrageDamage = Mathf.RoundToInt(stats.baseDirectDamage * 0.5f);
        if (barrageDamage < 1) barrageDamage = 1;

        Debug.Log($"<color=cyan>[冰锥连射] 开始顺时针发射 {iceBarrageProjectileCount} 枚冰锥!</color>");

        float angleStep = 360f / iceBarrageProjectileCount;
        float delayBetween = 0.15f; // 每枚之间间隔 150ms（顺时针旋转效果更明显）

        for (int i = 0; i < iceBarrageProjectileCount; i++)
        {
            // 重新获取玩家位置（玩家可能在移动）
            playerT = GameManager.Instance?.playerTransform;
            if (playerT == null) yield break;

            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject proj = Instantiate(
                stats.projectilePrefab,
                playerT.position + Vector3.up * 0.5f,
                Quaternion.LookRotation(dir)
            );

            Projectile projScript = proj.GetComponent<Projectile>();
            if (projScript != null)
            {
                projScript.isBarrageProjectile = true;
                projScript.InitializeAsStraight(
                    dir,
                    stats.baseLaunchForce,
                    barrageDamage,
                    false,
                    3,
                    stats.baseProjectileLifetime,
                    stats.shieldImpactEffectPrefab,
                    stats.defaultImpactEffectPrefab,
                    0, 0, 0, 0, 0,
                    AttackType.Standard,
                    sourceWeapon,
                    0, 0, null,
                    0f
                );
            }

            // 顺时针间隔发射
            yield return new WaitForSeconds(delayBetween);
        }
    }

    /// <summary>
    /// 在指定位置生成火海
    /// </summary>
    private void SpawnFirePool(Vector3 position, float baseDamage)
    {
        // 将位置降到地面
        position.y = 0.05f;

        GameObject pool;
        if (firePoolPrefab != null)
        {
            pool = Instantiate(firePoolPrefab, position, Quaternion.identity);
        }
        else
        {
            // 没有预制件时创建纯逻辑区域
            pool = new GameObject("FirePool_Runtime");
            pool.transform.position = position;
        }

        // 挂载/配置 FirePoolZone 组件
        FirePoolZone fpz = pool.GetComponent<FirePoolZone>();
        if (fpz == null) fpz = pool.AddComponent<FirePoolZone>();
        fpz.radius = 2f;
        fpz.duration = firePoolDuration;
        fpz.tickInterval = 0.5f;
        fpz.damagePerTick = Mathf.Max(3, Mathf.RoundToInt(baseDamage * 0.15f)); // 火海伤害为触发伤害的15%
        fpz.burnDotDamage = 3;
        fpz.burnDotDuration = 2f;

        Debug.Log($"<color=orange>[燃烧大地] 在 {position} 生成火海! 每跳伤害:{fpz.damagePerTick}</color>");
    }

    /// <summary>
    /// 触发永冻领域
    /// </summary>
    private void TriggerBlizzardZone()
    {
        Transform playerT = GameManager.Instance?.playerTransform;
        if (playerT == null) return;

        GameObject zone;
        if (blizzardZonePrefab != null)
        {
            zone = Instantiate(blizzardZonePrefab, playerT.position, Quaternion.identity);
        }
        else
        {
            // 没有预制件时创建纯逻辑区域
            zone = new GameObject("BlizzardZone_Runtime");
            zone.transform.position = playerT.position;
        }

        // 挂载/配置 BlizzardZone 组件
        BlizzardZone bz = zone.GetComponent<BlizzardZone>();
        if (bz == null) bz = zone.AddComponent<BlizzardZone>();
        bz.radius = 8f;
        bz.duration = blizzardDuration;
        bz.slowPercent = 0.7f;      // 减速70%
        bz.freezeChance = 0.3f;     // 每秒30%概率冰冻
        bz.freezeDuration = 1.5f;   // 冰冻1.5秒

        Debug.Log($"<color=cyan>[永冻领域] 在 {playerT.position} 生成! 半径:8 持续:{blizzardDuration}秒</color>");
    }

    /// <summary>
    /// 触发炼狱之焰
    /// </summary>
    private IEnumerator TriggerInferno()
    {
        infernoActive = true;
        infernoTimer = infernoCooldown;

        Debug.Log("<color=red>[炼狱之焰] 全场火海伤害暴涨!</color>");

        yield return new WaitForSeconds(infernoDuration);

        infernoActive = false;
    }

    /// <summary>
    /// 清理已销毁的火海引用
    /// </summary>
    private void CleanupDeadFirePools()
    {
        activeFirePools.RemoveAll(p => p == null);
    }

    /// <summary>
    /// 查询法师角色卡技能是否已激活（公开，供 Projectile 等外部调用）
    /// </summary>
    public bool HasMageSkill(string skillID)
    {
        return UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill(skillID);
    }

    /// <summary>
    /// 烈焰轨迹：在指定位置生成小型火海（由大招火球飞行时调用）
    /// </summary>
    public void SpawnFireTrailPool(Vector3 position)
    {
        position.y = 0.05f;

        GameObject pool;
        if (firePoolPrefab != null)
        {
            pool = Instantiate(firePoolPrefab, position, Quaternion.identity);
        }
        else
        {
            pool = new GameObject("FireTrail_Runtime");
            pool.transform.position = position;
        }

        // 轨迹火海参数：比普通火海更小但持续更久
        FirePoolZone fpz = pool.GetComponent<FirePoolZone>();
        if (fpz == null) fpz = pool.AddComponent<FirePoolZone>();
        fpz.radius = 1.5f;               // 小范围
        fpz.duration = firePoolDuration;  // 与普通火海同持续时间
        fpz.tickInterval = 0.5f;
        fpz.damagePerTick = 3;            // 低伤害
        fpz.burnDotDamage = 2;
        fpz.burnDotDuration = 1.5f;
    }

    /// <summary>
    /// 风助火势：飓风弹经过时检测附近火海，在火海周围扩散新火海
    /// </summary>
    public void TryWindSpreadFire(Vector3 hurricanePos)
    {
        CleanupDeadFirePools();

        float spreadDetectRadius = 5f;  // 检测半径
        float spreadOffset = 3f;        // 扩散偏移距离

        foreach (var pool in activeFirePools.ToArray())
        {
            if (pool == null) continue;

            float dist = Vector3.Distance(hurricanePos, pool.transform.position);
            if (dist > spreadDetectRadius) continue;

            // 在火海周围随机位置生成新火海
            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            Vector3 spreadPos = pool.transform.position + randomDir * spreadOffset;
            spreadPos.y = 0.05f;

            // 防止在同一位置重复扩散（检测新位置附近是否已有火海）
            bool tooClose = false;
            foreach (var existing in activeFirePools)
            {
                if (existing != null && Vector3.Distance(existing.transform.position, spreadPos) < 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            GameObject newPool;
            if (firePoolPrefab != null)
            {
                newPool = Instantiate(firePoolPrefab, spreadPos, Quaternion.identity);
            }
            else
            {
                newPool = new GameObject("FireSpread_Runtime");
                newPool.transform.position = spreadPos;
            }

            FirePoolZone fpz = newPool.GetComponent<FirePoolZone>();
            if (fpz == null) fpz = newPool.AddComponent<FirePoolZone>();
            fpz.radius = 2f;
            fpz.duration = firePoolDuration;
            fpz.tickInterval = 0.5f;
            fpz.damagePerTick = 4;
            fpz.burnDotDamage = 3;
            fpz.burnDotDuration = 2f;

            Debug.Log($"<color=orange>[风助火势] 飓风扩散火海到 {spreadPos}</color>");
            break; // 每次检测只扩散一个，防止爆炸式增长
        }
    }
}
