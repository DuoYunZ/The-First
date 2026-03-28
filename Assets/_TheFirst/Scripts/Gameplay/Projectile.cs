using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class Projectile : MonoBehaviour
{
    public AttackType attackType = AttackType.Standard;

    [Header("视觉效果")]
    public GameObject shieldImpactEffectPrefab;
    public GameObject defaultImpactEffectPrefab;

    // --- 弹道控制 ---
    private bool isParabolic = false; // 现在默认为 false，因为模式更常用

    [Header("直线参数")]
    private Vector3 direction;
    public float speed;

    // --- 抛物线轨迹参数 ---
    private Vector3 currentVelocity;
    public float gravity = 9.8f;
    public bool faceMovementDirection = true;

    // --- 通用子弹参数 ---
    public float lifetime = 5f;
    public int damage = 0;
    public GameObject owner;
    private int aoeDamage = 0;
    private bool hasExploded = false;
    [HideInInspector] public bool isCritical = false;
    [System.NonSerialized] public bool canSplit = true; // 【新增】防止无限分裂（不序列化，运行时始终为 true）

    [HideInInspector] public bool isUltimate = false; // 标记是否为大招发出的子弹
    [HideInInspector] public bool isSubHurricane = false; // 标记是否为子飓风，防止无限递归生成
    [HideInInspector] public int remainingBounces = 0; // 榴弹弹跳剩余次数
    [HideInInspector] public float knockbackForce = 0f; // 击退力度（0=不击退）

    private float spawnProtectionTimer = 0.5f; // 【新增】生成保护时间（秒）：在这个时间内不进行撞墙/撞地检测，防止出生距地太近直接销毁。


    [Header("效果与范围 (由Initialize方法设置)")]
    public GameObject impactEffectPrefab;
    public GameObject explosionEffectPrefab;
    public float explosionRadius = 3f;
    public LayerMask damageableLayers;
    public LayerMask groundAndWallLayers;

    private float trailSpawnTimer = 0f; // 用于控制火径生成频率的计时器

    // --- 穿透相关变量 ---
    private int pierceCount = 1;
    private int piercedEnemies = 0;

    // --- 连锁相关变量 ---
    private int remainingChains = 0;
    private float _chainRange = 0f;
    private List<Health> hitEnemies = new List<Health>();

    // --- DoT/Debuff 属性 ---
    private int dotDamage;
    private float dotDuration;
    private float dotTickInterval;
    private float slowPercentage;
    private float slowDuration;

    private float stunChance = 0f;
    private float stunDuration = 0f;
    // --- 弹道和行为模式 ---
    private enum ProjectileMode { Straight, Parabolic, AirdropDeployer, Homing, Boomerang } // <-- 添加 Boomerang
    private ProjectileMode mode;
    private bool isEnemyProjectile = false;
    private Transform homingTarget;
    private float homingTurnSpeed = 5f;

    // --- 部署器专用的“有效载荷”信息 ---
    private GameObject areaPrefabPayload;
    private int areaDamagePayload;
    private float areaDurationPayload;
    private float areaIntervalPayload;
    private GameObject creatorAttacker;

    // --- vvv 新增：回旋镖属性 vvv ---
    private enum BoomerangState { Outbound, Inbound }
    private BoomerangState boomerangState;
    public WeaponPart sourceWeapon; // 抓取时需要通知的发射器
    private Vector3 spawnPoint; // 发射点
    private Transform playerTransform; // 玩家引用，用于返回
    private float maxDistance; // 最大飞行距离    
    private float catchRadius;
    private float rotationSpeed;
    private float returnOvershootDistance; // <-- 新增
    private Vector3 returnTargetPoint;     // <-- 新增: 最终返回的目标点
    private Vector3 returnDirection;       // <-- 新增: 固定的返回方向
    private Vector3 currentReturnDirection;

    private bool hasBeenCaught = false; // 是否已被抓取

    private float freezeChance = 0f; // 【新增】

    private Rigidbody rb;
    // --- ^^^ 新增结束 ^^^ ---


    // --- 伤害冷却字典 ---
    private Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private float hitCooldown = 0.5f;

    [HideInInspector] public float ignoreEnemyTimer = 0f; // 短时间内忽略敌人碰撞

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            // 如果 Awake 时找不到，尝试在子物体找 (作为后备)
            rb = GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {

            }
        }

        // 最终检查
        if (rb == null)
        {

        }
        else
        {
            // 基础设置
            rb.useGravity = false;
        }
    }
    public void InitializeAsReflectable(Vector3 dir, float spd, int dmg, float life, GameObject vfx)
    {
        this.mode = ProjectileMode.Straight; // 假设激光是直线
        this.attackType = AttackType.Reflectable; // 【关键】设置类型
        this.isEnemyProjectile = true; // 可反弹的子弹来自敌人

        this.direction = dir;
        this.speed = spd;
        this.damage = dmg;
        this.lifetime = life;
        this.impactEffectPrefab = vfx;

        Destroy(gameObject, this.lifetime);
    }

    public void MarkAsPlayerProjectile()
    {
        this.isEnemyProjectile = false;
        this.tag = "PlayerProjectile"; // （可选）改变标签以便调试
        // 更新可伤害层，现在它可以伤害敌人了
        this.damageableLayers = LayerMask.GetMask("Enemies");
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectiles");
        this.attackType = AttackType.Standard;
    }

    public void SetNewDirection(Vector3 newDirection)
    {
        this.direction = newDirection.normalized;
        // 让子弹朝向新方向
        transform.rotation = Quaternion.LookRotation(this.direction);
    }

    public void InitializeAsHoming(Transform target, float spd, int dmg, bool isEnemyBullet, float turnSpeed, float life,
                               GameObject shieldVfx, GameObject defaultVfx) // <-- 同样更新追踪弹
    {
        if (rb == null)
        {
           
        }
        this.mode = ProjectileMode.Homing;
        this.isParabolic = false;
        this.homingTarget = target;
        this.speed = spd;
        this.damage = dmg;
        this.homingTurnSpeed = turnSpeed;
        this.isEnemyProjectile = isEnemyBullet;
        this.lifetime = life;
        this.explosionRadius = 0f; // 【修复】重置爆炸半径，防止走进 Explode() 路径
        this.aoeDamage = 0; // 追踪弹不应有 AOE 伤害
        this.damageableLayers = isEnemyBullet ? LayerMask.GetMask("Player") : LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");
        this.shieldImpactEffectPrefab = shieldVfx;
        this.defaultImpactEffectPrefab = defaultVfx;
        if (rb != null)
        {
            rb.isKinematic = false; // 确保非运动学
            rb.useGravity = false;  // 强制关闭重力
                                    // 可以选择性地设置 Constraints，如果直线弹不需要旋转的话
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }
        Destroy(gameObject, life);
    }
    /// <summary>
    /// 【新方法】：初始化为从天而降的“部署器”
    /// </summary>
    public void InitializeAsAirdropDeployer(Vector3 startPosition, Vector3 flightDirection, float fallSpeed, GameObject areaPrefab, int dmg, float dur, float interval, GameObject attacker)
    {
        this.mode = ProjectileMode.AirdropDeployer;
        this.isParabolic = false;
        transform.position = startPosition;
        this.direction = flightDirection.normalized;
        this.speed = fallSpeed;
        this.areaPrefabPayload = areaPrefab;
        this.areaDamagePayload = dmg;
        this.areaDurationPayload = dur;
        this.areaIntervalPayload = interval;
        this.creatorAttacker = attacker;
        this.groundAndWallLayers = LayerMask.GetMask("Enemies", "Ground"); // 修正：部署器也应该能打敌人
        this.damageableLayers = LayerMask.GetMask("Enemies"); // 部署区域伤害敌人
        Destroy(gameObject, 10f);
    }
    /// <summary>
    /// 为直线弹道设计的初始化方法
    /// </summary>
    public void InitializeAsStraight(Vector3 dir, float spd, int directDmg, bool isEnemyBullet,
                                          int pierce, float life, GameObject shieldVfx, GameObject defaultVfx,
                                          int dotDmg, float dotDur, float dotTick, float slowPct, float slowDur,
                                          AttackType type = AttackType.Standard,
                                          WeaponPart launcher = null,
                                          int aoeDmg = 0,
                                          float aoeRad = 0f,
                                          GameObject explodeVfx = null,
                                          float freezeChance = 0f)
    {

        if (rb == null && spd > 0)
        {
            
        }
        else if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false; // 强制关闭重力，防止因预制体勾选了重力导致直射变斜下射
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        this.mode = ProjectileMode.Straight;
        this.isParabolic = false;
        this.attackType = type;
        this.shieldImpactEffectPrefab = shieldVfx;
        this.defaultImpactEffectPrefab = defaultVfx;
        this.direction = dir;
        this.speed = spd;
        this.damage = directDmg;
        this.isEnemyProjectile = isEnemyBullet;
        this.pierceCount = pierce > 0 ? pierce : 1;
        this.lifetime = life;
        this.dotDamage = dotDmg;
        this.dotDuration = dotDur;
        this.dotTickInterval = dotTick;
        this.slowPercentage = slowPct;
        this.slowDuration = slowDur;
        this.sourceWeapon = launcher;
        this.damageableLayers = isEnemyBullet ? LayerMask.GetMask("Player") : LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");

        this.aoeDamage = aoeDmg;
        this.explosionRadius = aoeRad;
        this.explosionEffectPrefab = explodeVfx;
        this.freezeChance = freezeChance;
        // ==========================================
        // 【新增逻辑】计算暴击（全局 + 局部）
        // ==========================================
        float totalCritRate = 0f;
        float totalCritDmgMult = 1.5f; // 默认1.5倍

        // 1. 获取全局玩家属性
        if (PlayerStats.Instance != null)
        {
            totalCritRate += PlayerStats.Instance.critRate;
            totalCritDmgMult = PlayerStats.Instance.critDamage;
        }

        // 2. 获取武器局部属性 (localCritRateBonus)
        if (this.sourceWeapon != null) // 替换 launcher
        {
            totalCritRate += this.sourceWeapon.localCritRateBonus;
            totalCritDmgMult += this.sourceWeapon.localCritDamageBonus;
        }

        // 3. 判定暴击
        // 如果随机数小于总暴击率，则判定为暴击
        if (Random.value <= totalCritRate)
        {
            this.isCritical = true;
            // 直接在这里把伤害乘上去
            this.damage = Mathf.RoundToInt(this.damage * totalCritDmgMult);

            // 可以把子弹变大一点表示暴击
            transform.localScale *= 1.2f;
        }
        else
        {
            this.isCritical = false;
        }
        // ==========================================

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }
        Destroy(gameObject, this.lifetime);
    }

    /// <summary>
    /// 初始化为【抛物线】弹道。由 WeaponPart 调用。
    /// </summary>
    public void InitializeAsParabolic(Vector3 initialVelocity, int projectileDirectDamage, int projectileAoeDamage,
                                       float projectileLifetime, GameObject explosionVfxPrefab, float aoeRadius,
                                       LayerMask layersToDamage, LayerMask layersToExplodeOn,
                                       int dotDmg, float dotDur, float dotTick,
                                       float newStunChance, float newStunDuration,
                                       WeaponPart weaponPart) // <--- vvv [新增] vvv
    {
        this.mode = ProjectileMode.Parabolic;
        this.isParabolic = true;
        this.currentVelocity = initialVelocity;
        Rigidbody rb = GetComponent<Rigidbody>(); // 抛物线需要 Rigidbody
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false; // 必须是非运动学才能受力
        rb.useGravity = false; // 我们手动模拟重力
        rb.velocity = initialVelocity;

        this.damage = projectileDirectDamage;
        this.aoeDamage = projectileAoeDamage;
        this.lifetime = projectileLifetime;
        this.explosionEffectPrefab = explosionVfxPrefab;
        this.explosionRadius = aoeRadius;
        this.damageableLayers = layersToDamage;
        this.groundAndWallLayers = layersToExplodeOn;
        this.dotDamage = dotDmg;
        this.dotDuration = dotDur;
        this.dotTickInterval = dotTick;

        this.stunChance = newStunChance;
        this.stunDuration = newStunDuration;

        this.sourceWeapon = weaponPart; // <--- vvv [新增] vvv
        Collider col = GetComponent<Collider>(); // 抛物线需要非触发器
        if (col != null) col.isTrigger = false;

        // 读取弹跳次数
        if (sourceWeapon != null)
            this.remainingBounces = sourceWeapon.localBounceCount;

        Destroy(gameObject, this.lifetime);
    }

    public void InitializeAsChaining(Vector3 dir, float spd, int dmg, int chains, float range, float life, GameObject vfx)
    {
        this.mode = ProjectileMode.Straight; // 连锁基于直线
        this.isParabolic = false;
        this.direction = dir;
        this.speed = spd;
        this.damage = dmg;
        this.remainingChains = chains;
        this._chainRange = range;
        this.lifetime = life;
        this.impactEffectPrefab = vfx;
        this.pierceCount = 1;
        this.hitEnemies.Clear();
        this.damageableLayers = LayerMask.GetMask("Enemies"); // 连锁打敌人
        this.groundAndWallLayers = LayerMask.GetMask("Ground");
        Destroy(gameObject, this.lifetime);
    }

    public void InitializeAsBoomerang(Vector3 dir, float spd, int dmg, float maxDist, float catchRad, float life,
                                      GameObject shieldVFX, GameObject defaultVFX, WeaponPart launcherPart,
                                      float rotSpeed, float overshootDist) // <-- 加回 overshootDist
    {

        if (rb == null)
        {            
            Destroy(gameObject);
            return;
        }
        // 添加详细日志

        this.mode = ProjectileMode.Boomerang;
        this.isParabolic = false;
        this.attackType = AttackType.Standard;
        this.shieldImpactEffectPrefab = shieldVFX;
        this.defaultImpactEffectPrefab = defaultVFX;
        this.direction = dir.normalized; // 初始飞出方向
        this.speed = spd; // 必须大于 0
        this.damage = dmg;
        this.pierceCount = 999;
        this.lifetime = life; // 总生命周期，必须大于 0
        this.maxDistance = maxDist;
        this.catchRadius = catchRad;
        this.rotationSpeed = rotSpeed;
        this.returnOvershootDistance = overshootDist; // <-- 存储过冲距离
        this.sourceWeapon = launcherPart;
        // 记录发射点
        this.spawnPoint = transform.position; // 使用 projectile 自己的初始位置
                                              // 
        this.boomerangState = BoomerangState.Outbound;
        this.hasBeenCaught = false;

        this.damageableLayers = LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");

        rb.isKinematic = false; // 确保非运动学
        rb.velocity = Vector3.zero;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // 结束日志
    }

    private void SetReturnState()
    {
        if (boomerangState == BoomerangState.Outbound)
        {
            boomerangState = BoomerangState.Inbound;

            // --- 【核心修改】计算返回目标点 (出生点后方) ---
            // 使用初始飞出方向的反方向 (-direction) 来计算过冲点
            returnTargetPoint = spawnPoint - direction * returnOvershootDistance;
            // --- 计算结束 ---

            // --- 【核心修改】计算【固定】的返回方向 (从当前点指向目标点) ---
            currentReturnDirection = (returnTargetPoint - transform.position).normalized;
            // --- 计算结束 ---

            // 清除当前速度，以便立即应用返回速度
            if (rb != null) rb.velocity = Vector3.zero;
        }
    }
    private void OnDrawGizmos()
    {
        if (isParabolic)
        {
            Gizmos.color = new Color(1, 0.92f, 0.016f, 0.5f); // 黄色代表爆炸范围
            Gizmos.DrawWireSphere(transform.position, this.explosionRadius);
        }
    }
    void Update()
    {
        if (hasExploded) return;

        if (spawnProtectionTimer > 0f)
        {
            spawnProtectionTimer -= Time.deltaTime;
        }
        if (ignoreEnemyTimer > 0f)
        {
            ignoreEnemyTimer -= Time.deltaTime;
        }

        // 自转逻辑
        if (mode == ProjectileMode.Boomerang)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        // 冷却逻辑
        if (hitCooldowns.Count > 0)
        {
            List<Health> keys = new List<Health>(hitCooldowns.Keys);
            foreach (var key in keys)
            {
                if (key == null) { hitCooldowns.Remove(key); continue; }
                hitCooldowns[key] -= Time.deltaTime;
                if (hitCooldowns[key] <= 0) { hitCooldowns.Remove(key); }
            }
        }

        // 生命周期
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // 替换 launcher
            if (mode == ProjectileMode.Boomerang && !hasBeenCaught && sourceWeapon != null)
            {
                sourceWeapon.StartCooldownIfNotCaught();
            }
            Destroy(gameObject);
        }

        // =========================================================
        // 【最终修正】元素联动逻辑
        // =========================================================

        WeaponStatBlock stats = (sourceWeapon != null) ? sourceWeapon.StatBlock : null;

        if (stats != null && stats.synergyFireTrailPrefab != null)
        {
            trailSpawnTimer += Time.deltaTime;

            if (trailSpawnTimer >= stats.fireTrailSpawnRate)
            {
                // 从天而降的射线 (确保能穿透火海)
                Vector3 rayOrigin = transform.position + Vector3.up * 5f;
                // 注意：必须包含 Trigger，因为火海通常是 Trigger
                RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 15f, ~0, QueryTriggerInteraction.Collide);

                bool foundFire = false;
                Vector3 groundPosition = Vector3.zero;
                bool foundGround = false;

                // --- 第一步：遍历所有打中的东西，收集信息 ---
                foreach (RaycastHit hit in hits)
                {
                    // 【核心修复】在这里检测火海！
                    if (hit.collider.CompareTag("BurningGround"))
                    {
                        foundFire = true;
                    }

                    // 检测地面 (排除 Trigger、敌人、玩家)
                    if (!hit.collider.isTrigger)
                    {
                        // 排除敌人和玩家
                        if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Player"))
                        {
                            continue;
                        }

                        // 双重保险：检查 Layer
                        int enemyLayer = LayerMask.NameToLayer("Enemies");
                        if (hit.collider.gameObject.layer == enemyLayer)
                        {
                            continue;
                        }

                        // 剩下的才认为是合法的地面
                        // 找最高点 (防止打中地底下的东西)
                        if (!foundGround || hit.point.y > groundPosition.y)
                        {
                            groundPosition = hit.point;
                            foundGround = true;
                        }
                    }
                }

                // --- 第二步：只有当“有火”且“有地面”时才生成 ---
                if (foundFire && foundGround)
                {
                    trailSpawnTimer = 0f;
                    Vector3 spawnPos = groundPosition + Vector3.up * 0.05f;
                    Vector3 flatForward = transform.forward;

                    flatForward.y = 0;

                    Quaternion spawnRot = Quaternion.identity;
                    if (flatForward.sqrMagnitude > 0.01f)
                    {
                        spawnRot = Quaternion.LookRotation(flatForward.normalized);
                    }

                    GameObject trailObj = Instantiate(stats.synergyFireTrailPrefab, spawnPos, spawnRot);

                    GroundHazard newTrailScript = trailObj.GetComponent<GroundHazard>();
                    if (newTrailScript != null)
                    {
                        // --- 继承伤害逻辑 ---
                        int finalTrailDamage = 0;

                        // 重新遍历寻找那个火海组件来读取伤害
                        foreach (var hit in hits)
                        {
                            if (hit.collider.CompareTag("BurningGround"))
                            {
                                GroundHazard originalFire = hit.collider.GetComponentInParent<GroundHazard>();
                                if (originalFire != null)
                                {
                                    // 完美继承：原来的火是多少伤，扩散出去的火就是多少伤
                                    finalTrailDamage = originalFire.DamagePerTick;
                                    break;
                                }
                            }
                        }

                        // 保底逻辑
                        if (finalTrailDamage == 0)
                        {
                            finalTrailDamage = Mathf.RoundToInt(damage * 0.3f);
                            if (finalTrailDamage < 1) finalTrailDamage = 1;
                        }

                        string weaponName = stats.weaponName;
                        GameObject ownerObj = (sourceWeapon != null) ? sourceWeapon.gameObject : this.gameObject;

                        newTrailScript.Initialize(finalTrailDamage, 3f, weaponName, ownerObj);
                    }
                }
            }
        }
    }

    void FixedUpdate()
    {
       
        if (hasExploded || rb == null || rb.isKinematic) return;

        switch (mode)
        {
            // ... (Keep your existing cases for Straight, Parabolic, Homing) ...
            case ProjectileMode.Straight:
            case ProjectileMode.AirdropDeployer:
                rb.velocity = direction * speed;
                break;
            case ProjectileMode.Parabolic:
                rb.velocity += Vector3.down * gravity * Time.fixedDeltaTime;
                if (faceMovementDirection && rb.velocity.sqrMagnitude > 0.01f)
                { rb.MoveRotation(Quaternion.LookRotation(rb.velocity)); }
                break;
           case ProjectileMode.Homing:
                   if (homingTarget != null)
                   {
                      Vector3 directionToTarget = (homingTarget.position - rb.position).normalized;
                      Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                      Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, homingTurnSpeed * Time.fixedDeltaTime);
                      rb.MoveRotation(newRotation);
                   }
                   rb.velocity = transform.forward * speed;
                  break;

            case ProjectileMode.Boomerang:
                if (boomerangState == BoomerangState.Outbound)
                {
                    rb.velocity = direction * speed; // 保持飞出速度
                    float distanceTraveled = Vector3.Distance(spawnPoint, rb.position);
                    if (distanceTraveled >= maxDistance)
                    {
                        SetReturnState(); // 到达距离，切换状态
                    }
                }
                else if (boomerangState == BoomerangState.Inbound)
                {
                    // --- 【核心修改】使用固定的返回方向 ---
                    rb.velocity = currentReturnDirection * speed;
                    // --- 【核心修改结束】 ---

                    // 【重要】移除 FixedUpdate 中的销毁逻辑，完全交给 Update 和 OnTriggerEnter
                    // if (!hasBeenCaught) { /* ... 移除距离判断和 Destroy ... */ }
                }
                break;
                // --- ^^^ 核心修改结束 ^^^ ---
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // 仅在抛物线模式下处理物理碰撞
        if (mode == ProjectileMode.Parabolic)
        {
            // 检查是否碰撞到了我们关心的层 (地面/墙壁 或 可伤害层)
            int hitLayer = collision.gameObject.layer;

            // 如果碰到了地面/墙壁，检查是否还在出生保护期
            if (((1 << hitLayer) & groundAndWallLayers) != 0)
            {
                if (spawnProtectionTimer > 0f) return; // 保护期内，忽略地形碰撞
            }

            // 使用你在 InitializeAsParabolic 中设置的层掩码
            if (((1 << hitLayer) & groundAndWallLayers) != 0 || ((1 << hitLayer) & damageableLayers) != 0)
            {
                // 在第一个碰撞点爆炸，并传递命中的 collider
                // Explode 方法会处理直接伤害和 AOE 伤害
                Explode(collision.contacts[0].point, collision.collider);
            }
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;


        if (mode == ProjectileMode.Parabolic)
        {
            // 检查是否碰到了可伤害层 (即敌人)
            if (((1 << other.gameObject.layer) & damageableLayers) != 0)
            {
                // 在敌人身上爆炸
                Explode(SafeClosestPoint(other, transform.position), other);
            }
            // 无论碰到什么，抛物线模式都不应执行下面的其他逻辑（如回旋镖抓取）
            return;
        }

        // 抓取逻辑
        if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Inbound && other.CompareTag("Player"))
        {
            Transform currentPlayerTransform = GameManager.Instance?.playerTransform;
            if (!hasBeenCaught && currentPlayerTransform != null && Vector3.Distance(transform.position, currentPlayerTransform.position) < catchRadius)
            {
                hasBeenCaught = true;
                
                if (sourceWeapon != null)
                {
                    sourceWeapon.OnBoomerangCaught(transform.position); // 替换 launcher
                }
                // 【确认】抓住后立刻销毁当前实例
                Destroy(gameObject);
            }
            return; // 碰到玩家不再处理其他
        }       
        // 伤害敌人 (保持不变但加入新判断)
        int targetLayer = isEnemyProjectile ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Enemies");
        if (other.gameObject.layer == targetLayer)
        {
            if (ignoreEnemyTimer > 0f) return; // 【新增】短时间内忽略敌人碰撞
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null) { HandleHit(targetHealth, other); }
        }
        // 撞墙/地面 (保持不变)
        else if (((1 << other.gameObject.layer) & groundAndWallLayers) != 0)
        {
            if (spawnProtectionTimer > 0f) return; // 【新增】保护期内，忽略地形碰撞

            if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Outbound)
            {
                SetReturnState(); // 撞墙开始返回
            }
            else if (mode != ProjectileMode.Boomerang)
            {
                // 【核心修改】如果是爆裂弹，撞墙也要爆炸
                if (explosionRadius > 0)
                {
                    Explode(SafeClosestPoint(other, transform.position), other);
                }
                else
                {
                    // 普通子弹：播个撞击特效然后销毁
                    // 添加日志帮助排查子弹不明原因消失的问题
                    if (!this.gameObject.name.Contains("Spark")) 
                    {

                    }
                    HandleImpactEffect(false, SafeClosestPoint(other, transform.position));
                    Destroy(gameObject);
                }
            }
        }
    }

    void HandleEnemyHit(Health enemyHealth)
    {
        // --- 【修改】使用正确的签名传递名称，尽管这个方法可能未被调用 ---
        string weaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) ? sourceWeapon.StatBlock.weaponName : "";
        enemyHealth.TakeDamage(damage, enemyHealth.transform.position, this.gameObject, AttackType.Standard, null, null, weaponName);

        hitEnemies.Add(enemyHealth);
        if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, enemyHealth.transform.position, Quaternion.identity);

        if (pierceCount > 1)
        {
            piercedEnemies++;
            if (piercedEnemies >= pierceCount)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (remainingChains > 0)
        {
            remainingChains--;
            Transform nextTarget = FindNextTarget(enemyHealth.transform.position);
            if (nextTarget != null)
            {
                this.direction = (nextTarget.position - transform.position).normalized;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    Transform FindNextTarget(Vector3 currentPosition)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(currentPosition, _chainRange, damageableLayers);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;

        foreach (var col in nearbyColliders)
        {
            Health potentialTargetHealth = col.GetComponentInParent<Health>();
            // 检查：是敌人，活着，并且没被这颗子弹打过
            if (potentialTargetHealth != null && !potentialTargetHealth.IsDead && !hitEnemies.Contains(potentialTargetHealth))
            {
                float distSqr = (currentPosition - col.transform.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestTarget = col.transform;
                }
            }
        }
        return closestTarget;
    }

    void Explode(Vector3 explosionPoint, Collider initiallyHitCollider = null)
    {
        if (hasExploded) return;
        hasExploded = true;

        // 0. 准备数据
        string weaponName = "";
        WeaponStatBlock stats = null;
        if (sourceWeapon != null && sourceWeapon.StatBlock != null)
        {
            stats = sourceWeapon.StatBlock;
            weaponName = stats.weaponName;
        }

        // 播放爆炸特效 (受范围加成影响缩放)
        if (explosionEffectPrefab != null)
        {
            GameObject vfxObj = Instantiate(explosionEffectPrefab, explosionPoint, Quaternion.identity);

            // 【新增】根据范围加成缩放特效
            float vfxScale = 1f;
            if (sourceWeapon != null)
            {
                float areaBonus = sourceWeapon.localAreaBonus; // 局部范围加成
                float globalMult = (PlayerStats.Instance != null) ? PlayerStats.Instance.aoeRadiusMultiplier : 1f;
                vfxScale = globalMult + areaBonus; // 例: 基础1.0 + 爆发0.8 = 1.8倍
            }
            if (vfxScale > 1f)
            {
                vfxObj.transform.localScale *= vfxScale;
            }
        }

        bool hasCreatedHazard = false;
        // =========================================================
        //  逻辑 1: 凝固汽油弹 (生成地面火海)
        // =========================================================
        if (stats != null && stats.groundHazardPrefab != null)
        {
            hasCreatedHazard = true;

            Vector3 spawnPos = explosionPoint;
            bool snapToGround = !stats.isBlackHole;

            if (snapToGround)
            {
                int groundMask = LayerMask.GetMask("Ground", "Terrain");
                if (groundMask == 0) groundMask = ~(LayerMask.GetMask("Enemies", "Player", "PlayerProjectile"));

                if (Physics.Raycast(explosionPoint + Vector3.up * 1.0f, Vector3.down, out RaycastHit hit, 50f, groundMask))
                {
                    spawnPos = hit.point + Vector3.up * 0.05f;
                }
            }
            else
            {
                spawnPos += Vector3.up * 1.0f; // 黑洞悬浮
            }

            float scaleFactor = 1f;
            if (stats.baseAoeRadius > 0)
            {
                //scaleFactor = this.explosionRadius / stats.baseAoeRadius;

                float areaBonus = 0f;
                if (sourceWeapon != null)
                {
                    areaBonus = sourceWeapon.localAreaBonus; // 获取局外升级的范围加成
                }

                // 获取全局加成 (防止漏掉 PlayerStats)
                float globalMult = (PlayerStats.Instance != null) ? PlayerStats.Instance.aoeRadiusMultiplier : 1f;

                // 公式：基础 * (全局倍率 + 局部加成)
                float finalRadius = stats.baseAoeRadius * (globalMult + areaBonus);

                // 算出最终缩放比
                scaleFactor = finalRadius / stats.baseAoeRadius;

                // [调试]
                // Debug.Log($"[范围修正] 基础:{stats.baseAoeRadius}, 加成:{areaBonus}, 最终缩放:{scaleFactor}");
            }

            GameObject hazardObj = Instantiate(stats.groundHazardPrefab, spawnPos, Quaternion.identity);

            hazardObj.transform.localScale = Vector3.one * scaleFactor;
            // ==========================================
            // 【核心修改 B】应用火海持续时间
            // ==========================================
            float finalDuration = stats.groundHazardDuration;

            // 必须把 sourceWeapon (WeaponPart) 里的 durationBonus 乘进去
            if (sourceWeapon != null)
            {
                // 全局持续时间加成 + 燃烧瓶局外持续时间加成
                float totalDurationMult = PlayerStats.Instance.durationMultiplier + sourceWeapon.localDurationBonus;
                finalDuration *= totalDurationMult;
            }


            GroundHazard fireScript = hazardObj.GetComponentInChildren<GroundHazard>();
            // 2. 再找黑洞脚本
            BlackHoleField blackHoleScript = hazardObj.GetComponentInChildren<BlackHoleField>();

            if (fireScript != null)
            {
                // 【核心修改 1】去掉 0.2f 的限制，让火海继承 100% 的 AOE 伤害
                // 原代码: int hazardDmg = Mathf.RoundToInt(aoeDamage * 0.2f);
                int hazardDmg = aoeDamage; // 直接使用全额伤害 (40)

                if (hazardDmg < 1) hazardDmg = 1;
                fireScript.Initialize(hazardDmg, finalDuration, weaponName, sourceWeapon != null ? sourceWeapon.gameObject : null);
            }
            else if (blackHoleScript != null)
            {
                // 是黑洞：初始化黑洞
                blackHoleScript.Initialize(stats.blackHoleForce, stats.groundHazardDuration);
            }
            else
            {
                Destroy(hazardObj, stats.groundHazardDuration);
            }
        }


    // =========================================================
    //  逻辑 2: 分裂子弹 (火花/追踪虫)
    // =========================================================
    // 先检查 canSplit，防止火花弹再次分裂导致数量爆炸
    if (canSplit)
    {
        // 支持局部子弹分裂数量加成
        int finalSubProjectileCount = (stats != null) ? stats.subProjectileCount : 0;
        if (sourceWeapon != null)
        {
            finalSubProjectileCount += sourceWeapon.localSubProjectileCountBonus;
        }

        // 获取子弹 Prefab（优先用 stats 上的，如果没有则尝试从投射物自身获取）
        GameObject subPrefab = (stats != null) ? stats.subProjectilePrefab : null;

        if (subPrefab != null && finalSubProjectileCount > 0)
        {
            SpawnClusterProjectiles(explosionPoint, stats, finalSubProjectileCount);
        }
        else if (finalSubProjectileCount > 0)
        {

        }
    }

    // =========================================================
    //  逻辑 3: 伤害与物理效果 (含奇点手雷)
    // =========================================================
    if (stats != null && stats.baseAoeRadius > 0)
    {

        // A. 处理直接命中 (如果有)
        Health directlyHitEnemyHealth = null;
        if (initiallyHitCollider != null && initiallyHitCollider.CompareTag("Enemy"))
        {
            directlyHitEnemyHealth = initiallyHitCollider.GetComponentInParent<Health>();
            if (directlyHitEnemyHealth != null && !directlyHitEnemyHealth.IsDead)
            {
                directlyHitEnemyHealth.TakeDamage(damage, explosionPoint, this.gameObject, AttackType.Standard, null, null, weaponName);
                ApplyElementalEffects(directlyHitEnemyHealth, weaponName);

                // 直接命中也要检查眩晕
                if (stunChance > 0f && stunDuration > 0f && Random.value <= stunChance)
                {
                    StatusEffectReceiver directReceiver = directlyHitEnemyHealth.GetComponent<StatusEffectReceiver>();
                    if (directReceiver != null) directReceiver.ApplyStun(stunDuration);
                }
            }
        }
        if (!hasCreatedHazard)
        {
            // B. 处理 AOE 范围目标
            Collider[] collidersInRange = Physics.OverlapSphere(explosionPoint, explosionRadius, damageableLayers);
            foreach (Collider hitCollider in collidersInRange)
            {
                if (hitCollider.CompareTag("Player")) continue;

                Health healthComponent = hitCollider.GetComponentInParent<Health>();

                // 避免重复伤害直接命中的单位
                if (healthComponent != null && healthComponent == directlyHitEnemyHealth) continue;

                if (healthComponent != null)
                {
                    // 1. 造成 AOE 伤害
                    healthComponent.TakeDamage(aoeDamage, explosionPoint, this.gameObject, AttackType.Standard, null, null, weaponName);
                    ApplyElementalEffects(healthComponent, weaponName);

                    // 2. 处理物理效果 + 眩晕
                    StatusEffectReceiver receiver = healthComponent.GetComponent<StatusEffectReceiver>();
                    if (receiver != null && stats != null)
                    {
                        if (stats.isBlackHole)
                        {
                            Vector3 pullDir = (explosionPoint - healthComponent.transform.position).normalized;
                            pullDir.y = 0;
                            receiver.ApplyKnockback(pullDir, stats.blackHoleForce);
                        }
                    }
                    // 眩晕判定（独立于黑洞判定）
                    if (stunChance > 0f && stunDuration > 0f && Random.value <= stunChance)
                    {
                        StatusEffectReceiver stunReceiver = receiver ?? healthComponent.GetComponent<StatusEffectReceiver>();
                        if (stunReceiver != null) stunReceiver.ApplyStun(stunDuration);
                    }
                }
            }
        }

        // === 弹跳榴弹逻辑 ===
        if (remainingBounces > 0 && isParabolic)
        {
            // 找爆炸点附近最近的存活敌人
            Health nearestEnemy = null;
            float nearestDist = float.MaxValue;
            Collider[] nearby = Physics.OverlapSphere(explosionPoint, 30f, LayerMask.GetMask("Enemies"));
            foreach (var col in nearby)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                float d = Vector3.Distance(explosionPoint, h.transform.position);
                if (d < nearestDist) { nearestDist = d; nearestEnemy = h; }
            }

            // 无敌人则不弹跳
            if (nearestEnemy == null)
            {
                Destroy(gameObject);
                return;
            }

            remainingBounces--;
            hasExploded = false; // 重置爆炸标记，允许再次爆炸

            // 弹跳方向 = 敌人朝向，弹跳距离固定
            Vector3 bounceDir = nearestEnemy.transform.forward;
            bounceDir.y = 0;
            if (bounceDir.sqrMagnitude < 0.01f) bounceDir = Vector3.forward;
            bounceDir.Normalize();

            float bounceDist = Mathf.Clamp(nearestDist * 0.5f, 2f, 6f);
            Vector3 bounceTarget = nearestEnemy.transform.position + bounceDir * bounceDist;

            // 计算弹跳弹道
            Vector3 toTarget = bounceTarget - explosionPoint;
            toTarget.y = 0;
            float dist = toTarget.magnitude;
            float bounceHeight = Mathf.Clamp(dist * 0.4f, 1f, 4f);
            float gravity = 20f;

            float timeToApex = Mathf.Sqrt(2f * bounceHeight / gravity);
            float totalTime = timeToApex * 2f;
            float vy = gravity * timeToApex;
            Vector3 horizontalVel = toTarget / totalTime;
            Vector3 bounceVelocity = horizontalVel + Vector3.up * vy;

            // 重置物理状态
            transform.position = explosionPoint + Vector3.up * 0.3f;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = bounceVelocity;
                rb.isKinematic = false;
            }

            return; // 不销毁，继续飞行
        }

        Destroy(gameObject);
    }
}

private void SpawnClusterProjectiles(Vector3 origin, WeaponStatBlock stats, int countOverride = -1, bool useRingSpread = false)
{
    int countToSpawn = (countOverride > 0) ? countOverride : stats.subProjectileCount;

    // 1. --- 生成基准点 ---
    Vector3 spawnBasePos = origin + Vector3.up * 0.5f;

    for (int i = 0; i < countToSpawn; i++)
    {
        // 2. --- 计算散布方向 ---
        Vector3 spreadDir;
        if (useRingSpread)
        {
            // 环形均匀分布
            float angle = (360f / countToSpawn) * i;
            spreadDir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
        }
        else
        {
            // 随机方向
            spreadDir = Random.onUnitSphere;
            spreadDir.y = 0;
            spreadDir.Normalize();
        }

        // 稍微随机化一下生成位置，避免所有火花弹完全重叠在一起
        Vector3 finalSpawnPos = spawnBasePos + spreadDir * Random.Range(0.2f, 0.8f);

        // 3. --- 生成子弹 ---
        GameObject subObj = Instantiate(stats.subProjectilePrefab, finalSpawnPos, Quaternion.LookRotation(spreadDir));

        // 【修复】分裂弹使用固定最小 Scale，不继承母弹的缩放
        // 冰锥母弹 Scale 只有 0.3，乘 0.6 后仅 0.18，碰撞器有效半径只有 0.16 太小导致穿过敌人
        float subScale = Mathf.Max(transform.localScale.x * 0.6f, 0.5f);
        subObj.transform.localScale = Vector3.one * subScale;


        // 4. --- 初始化子弹 ---
        Projectile subScript = subObj.GetComponent<Projectile>();

        // 【修复】如果分裂预制体上没有 Projectile 脚本（例如纯粒子特效预制体），
        // 则动态添加必要组件，否则分裂弹无法造成伤害（会直接穿过敌人）
        if (subScript == null)
        {
            // 添加 Rigidbody（Projectile.Awake 需要）
            Rigidbody subRb = subObj.GetComponent<Rigidbody>();
            if (subRb == null)
            {
                subRb = subObj.AddComponent<Rigidbody>();
            }
            subRb.useGravity = false;
            subRb.isKinematic = false;

            // 添加 SphereCollider 作为触发器（用于 OnTriggerEnter 检测敌人）
            SphereCollider subCol = subObj.GetComponent<SphereCollider>();
            if (subCol == null)
            {
                subCol = subObj.AddComponent<SphereCollider>();
            }
            subCol.isTrigger = true;
            subCol.radius = 0.5f; // 合适的碰撞半径

            // 设置为 PlayerProjectiles 层，避免与玩家碰撞
            int playerProjLayer = LayerMask.NameToLayer("PlayerProjectiles");
            if (playerProjLayer >= 0)
                subObj.layer = playerProjLayer;

            // 添加 Projectile 脚本
            subScript = subObj.AddComponent<Projectile>();
        }

        if (subScript != null)
        {
            // 防止子弹再次分裂 (解决无限递归问题)
            subScript.canSplit = false;

            // 在地面基准点附近找目标
            Transform target = FindRandomNearbyEnemy(spawnBasePos, 15f);
// ...
            // 【修复】基础伤害计算：优先用AOE伤害，AOE为0时用直线伤害（冰锥等非爆炸弹）
            int baseDmg = (aoeDamage > 0) ? aoeDamage : this.damage;
            int subDmg = Mathf.RoundToInt(baseDmg * 0.5f);
            if (subDmg < 1) subDmg = 1;

            // 【新增】应用分裂伤害加成（冰片伤害增幅）
            if (sourceWeapon != null && sourceWeapon.localSubProjectileDamageBonus > 0f)
            {
                subDmg = Mathf.RoundToInt(subDmg * (1f + sourceWeapon.localSubProjectileDamageBonus));
                if (subDmg < 1) subDmg = 1;
            }

            // 确定特效：优先用专属特效，没有就用默认保底
            GameObject vfxToUse = stats.subProjectileHitVfx != null ? stats.subProjectileHitVfx : defaultImpactEffectPrefab;

            // 【新增】计算继承属性
            int subPierce = 0;
            float subFreezeChance = 0f;
            if (sourceWeapon != null && sourceWeapon.subProjectileInheritEnabled)
            {
                // 继承穿透
                subPierce = this.pierceCount; // 继承母弹的穿透次数
                if (sourceWeapon.localPierceCountBonus > 0)
                    subPierce += sourceWeapon.localPierceCountBonus;

                // 继承冰冻概率
                subFreezeChance = this.freezeChance;
                if (subFreezeChance <= 0f && sourceWeapon.StatBlock != null)
                    subFreezeChance = sourceWeapon.StatBlock.baseFreezeChance + sourceWeapon.localFreezeChanceBonus;
            }

            if (subPierce > 0)
            {
                // 有穿透 → 使用直线飞行模式，继承穿透和冰冻
                subScript.InitializeAsStraight(
                    dir: spreadDir,
                    spd: 8f,
                    directDmg: subDmg,
                    isEnemyBullet: false,
                    pierce: subPierce,
                    life: 2f,
                    shieldVfx: null,
                    defaultVfx: vfxToUse,
                    dotDmg: 0, dotDur: 0f, dotTick: 0f,
                    slowPct: 0f, slowDur: 0f,
                    type: AttackType.Standard,
                    launcher: sourceWeapon,
                    aoeDmg: 0, aoeRad: 0f,
                    explodeVfx: null,
                    freezeChance: subFreezeChance
                );
            }
            else if (useRingSpread)
            {
                // 环形扩散 → 直线飞行，不追踪
                subScript.InitializeAsStraight(
                    dir: spreadDir,
                    spd: 8f,
                    directDmg: subDmg,
                    isEnemyBullet: false,
                    pierce: 1,
                    life: 2f,
                    shieldVfx: null,
                    defaultVfx: vfxToUse,
                    dotDmg: 0, dotDur: 0f, dotTick: 0f,
                    slowPct: 0f, slowDur: 0f,
                    type: AttackType.Standard,
                    launcher: sourceWeapon,
                    aoeDmg: 0, aoeRad: 0f,
                    explodeVfx: null,
                    freezeChance: subFreezeChance
                );
            }
            else
            {
                // 火球分裂 → 追踪模式
                subScript.InitializeAsHoming(
                    target,
                    8f,
                    subDmg,
                    false,
                    15f,
                    2f,
                    vfxToUse,
                    vfxToUse
                );
            }

            // 【核心】：传递发射器引用，这让子弹能继承属性！
            subScript.sourceWeapon = this.sourceWeapon;
        }
    }
}

    /// <summary>
    /// 非爆炸型弹（冰锥等）命中时触发分裂
    /// 仅在技能树解锁分裂后才生效（isSubProjectileEnabled=true）
    /// </summary>
    private void TryNonExplosiveSplit(Vector3 hitPoint)
    {
        if (!canSplit) return;
        if (sourceWeapon == null) return;

        // 必须通过技能树解锁了分裂才生效
        if (!sourceWeapon.isSubProjectileEnabled) return;

        WeaponStatBlock stats = sourceWeapon.StatBlock;
        if (stats == null) return;

        GameObject subPrefab = stats.subProjectilePrefab;
        if (subPrefab == null) return;

        int finalCount = sourceWeapon.localSubProjectileCountBonus;
        if (finalCount <= 0) return;

        // 【关键】分裂后关闭canSplit，防止穿透弹每次命中都分裂
        canSplit = false;

        // 【修复】将分裂点Y坐标投影到地面
        // 冰锥飞行高度较高(~1.5m)，hitPoint在敌人碰撞器表面也较高
        // SpawnClusterProjectiles 内部会 +Vector3.up*0.5f
        // 如果Y太高，子弹被FreezePositionY锁定后会飞过敌人上方
        // 火球爆炸点(explosionPoint)通常在地面附近，所以火球分裂没问题
        Vector3 groundedHitPoint = hitPoint;
        groundedHitPoint.y = 0f; // 投影到地面，让子弹在 Y=0.5 处飞行

        SpawnClusterProjectiles(groundedHitPoint, stats, finalCount, useRingSpread: true);
    }

    private Transform FindRandomNearbyEnemy(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, damageableLayers);
        if (hits.Length == 0) return null;

        // 随机选一个
        Collider randomCol = hits[Random.Range(0, hits.Length)];
        return randomCol.transform;
    }
    void HandleHit(Health targetHealth, Collider hitCollider)
    {
        if (targetHealth.IsDead || hitEnemies.Contains(targetHealth)) return;
        if (hitCooldowns.ContainsKey(targetHealth)) return;
        hitCooldowns[targetHealth] = hitCooldown;

        Vector3 hitPoint = SafeClosestPoint(hitCollider, transform.position);
        bool targetHasShield = isEnemyProjectile && targetHealth.HasActiveShield();        

        GameObject attacker = owner;

        // 2. 如果没有 owner，再尝试用 creatorAttacker (部署器)
        if (attacker == null) attacker = creatorAttacker;

        // 3. 最后尝试用 launcher (普通武器)
        if (attacker == null && sourceWeapon != null)
        {
            attacker = sourceWeapon.gameObject;
        }
        // ------------------
        // 【修复】穿透型子弹即使有爆炸半径，也不应在穿透用完前直接销毁
        if (explosionRadius > 0 && pierceCount <= 1)
        {
            // 无穿透的爆炸弹：直接引爆并销毁
            Explode(SafeClosestPoint(hitCollider, transform.position), hitCollider);
            return;
        }

        string weaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) ? sourceWeapon.StatBlock.weaponName : "";
        bool wasReflected = targetHealth.TakeDamage(
            damage,
            SafeClosestPoint(hitCollider, transform.position),
            attacker,
            this.attackType,
            this,
            null,
            weaponName,
            this.isCritical // <--- 补上这一块！
        );


        if (!wasReflected)
        {
            // 在应用元素效果前，再次安全检查
            if (sourceWeapon != null)
            {
                ApplyElementalEffects(targetHealth, weaponName);
            }

            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();
            if (receiver != null)
            {
                if (dotDamage > 0) receiver.ApplyBurn(dotDamage, dotDuration, dotTickInterval, weaponName);
            }
            HandleImpactEffect(targetHasShield, hitPoint);

            // 击退效果（风刃等子弹命中时平滑推开敌人）
            if (knockbackForce > 0f && targetHealth.transform != null && !targetHealth.IsDead)
            {
                Vector3 pushDir = (targetHealth.transform.position - transform.position);
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.01f) pushDir = direction;
                pushDir.Normalize();
                targetHealth.StartCoroutine(SmoothKnockback(targetHealth.transform, pushDir, knockbackForce, 0.2f));
            }

            hitEnemies.Add(targetHealth);
            piercedEnemies++;

            // 【飓风-乱流】命中时尝试生成小飓风
            if (!isSubHurricane)
            {
                HurricaneProjectile hc = GetComponent<HurricaneProjectile>();
                if (hc != null) hc.TrySpawnTurbulence(hitPoint, piercedEnemies);
            }

            // 【修复】每次命中都尝试触发分裂（冰锥：第一次命中就分裂）
            // TryNonExplosiveSplit 内部会用 canSplit 防止多次分裂
            TryNonExplosiveSplit(hitPoint);

            if (mode != ProjectileMode.Boomerang && piercedEnemies >= pierceCount)
            {

                // 【飓风-风力回旋】穿透耗尽时尝试再发一道飓风
                if (!isSubHurricane)
                {
                    HurricaneProjectile hc = GetComponent<HurricaneProjectile>();
                    if (hc != null) hc.TryWindReturn();
                }

                if (remainingChains > 0) HandleChaining(hitPoint);
                else Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// 平滑击退协程：在 duration 秒内将目标沿 direction 推开 totalDistance 距离
    /// </summary>
    public static System.Collections.IEnumerator SmoothKnockback(Transform target, Vector3 dir, float totalDistance, float duration)
    {
        float elapsed = 0f;
        float speed = totalDistance / duration;
        while (elapsed < duration && target != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            target.position += dir * speed * dt;
            yield return null;
        }
    }

    private void HandleBoomerangOutbound()
    {
        transform.position += direction * speed * Time.deltaTime;
        float distanceTraveled = Vector3.Distance(spawnPoint, transform.position);
        if (distanceTraveled >= maxDistance)
        {
            boomerangState = BoomerangState.Inbound;
        }
    }

    private void HandleBoomerangInbound()
    {
        if (playerTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        transform.position += directionToPlayer * speed * Time.deltaTime;

        // 【优化】只有在未被抓取时才检测距离并销毁
        if (!hasBeenCaught)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer < 0.5f) // 飞到玩家附近但没触发抓取（可能玩家移速太快），销毁
            {
                Destroy(gameObject);
            }
        }
        // 如果已被抓取，它会继续按当前方向飞，直到生命周期结束
    }
    // --- ^^^ 新增结束 ^^^ ---
    void HandleChaining(Vector3 hitPoint)
    {
        remainingChains--;
        Transform nextTarget = FindNextTarget(hitPoint);
        if (nextTarget != null)
        {
            this.direction = (nextTarget.position - transform.position).normalized;
            // 重置命中计数器，允许再次穿透
            piercedEnemies = 0;
            // 可选：重置冷却字典，允许再次伤害之前命中的敌人
            // hitCooldowns.Clear();
        }
        else
        {
            Destroy(gameObject); // 没有下一个目标
        }
    }
    /// <summary>
    /// 安全版 ClosestPoint：对非凸 MeshCollider 回退到 bounds.ClosestPoint，避免报错
    /// </summary>
    private Vector3 SafeClosestPoint(Collider col, Vector3 point)
    {
        // BoxCollider, SphereCollider, CapsuleCollider, 凸 MeshCollider 都支持 ClosestPoint
        if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
        {
            return col.ClosestPoint(point);
        }

        if (col is MeshCollider mc && mc.convex)
        {
            return col.ClosestPoint(point);
        }

        // 非凸 MeshCollider 或其他类型：用 bounds 近似
        return col.bounds.ClosestPoint(point);
    }

    private void HandleImpactEffect(bool hitShield, Vector3 position) // 添加此方法
    {
        GameObject effectToPlay = hitShield ? shieldImpactEffectPrefab : defaultImpactEffectPrefab;
        if (effectToPlay != null)
        {
            Instantiate(effectToPlay, position, Quaternion.identity);
        }
    }

    private void ApplyElementalEffects(Health target, string weaponName)
    {
        // 1. 安全检查
        if (target == null || sourceWeapon == null || sourceWeapon.StatBlock == null) return; // 替换 launcher

        WeaponStatBlock stats = sourceWeapon.StatBlock;
        StatusEffectReceiver receiver = target.GetComponent<StatusEffectReceiver>();


        // =========================================================
        // 1. 雷击逻辑 (Thunder Strike / Smite) - 基于暴击触发      
       
        if (this.isCritical)
        {
            // A. 播放雷击特效 (Smite)
            if (stats.nativeSmiteVfxPrefab != null)
            {
                // 生成位置：敌人位置上方
                Instantiate(stats.nativeSmiteVfxPrefab, target.transform.position + Vector3.up * 2f, Quaternion.identity);
            }

            // B. 触发闪电链 (如果这是你想要的：暴击必定触发闪电链)
            // 即使 StatBlock 里 ChainCount 是 0，只要暴击了，我们也强制触发一次连锁
            // 你可以根据需求调整这里的参数 (例如连锁 3 次)
            int chainCount = stats.baseChainCount > 0 ? stats.baseChainCount : 3;
            int chainDmg = Mathf.RoundToInt(damage * 0.5f); // 连锁伤害减半
            if (chainDmg < 1) chainDmg = 1;

            // 调用发射器的连锁逻辑
            sourceWeapon.ChainLightningFromTarget(target.transform, chainCount, chainDmg, stats.chainRange); // 替换 launcher
        }

        // =========================================================
        // 2. 闪电链逻辑 (Chain Lightning) - 独立逻辑
        // =========================================================
        // 只有在武器配置了 baseChainCount > 0 时才触发，与暴击无关
        if (stats.baseChainCount > 0)
        {
            // 调用 WeaponPart 里的连锁方法 (代码见下方 Part 2)
            // 传递参数：目标，连锁次数，连锁伤害(通常比直伤低)，连锁范围
            int chainDmg = Mathf.RoundToInt(damage * 0.8f); // 假设连锁伤害是本体的 80%
            if (chainDmg < 1) chainDmg = 1;

            sourceWeapon.ChainLightningFromTarget(target.transform, stats.baseChainCount, chainDmg, stats.chainRange); // 替换 launcher
        }

        if (receiver == null) return; // 如果目标没有状态接收器，后面就不需要处理了

        if (this.freezeChance > 0)
        {
            // A. 施加减速
            float slowPct = (stats.baseSlowPercentage > 0) ? stats.baseSlowPercentage : 0.5f;
            float slowDur = (stats.baseSlowDuration > 0) ? stats.baseSlowDuration : 2.0f;

            // 记录打中前的状态
            bool wasAlreadyCold = receiver.IsSlowed || receiver.IsFrozen;

            // 先给减速 (这会触发 SlowRoutine，现在它会把 IsSlowed 设为 true 了)
            receiver.ApplySlow(slowPct, slowDur, Color.cyan);

            // B. 连携判定
            if (wasAlreadyCold)
            {
                // 只有处于减速状态的敌人才会判定冰冻
                if (Random.value <= this.freezeChance)
                {
                    receiver.ApplyFreeze(2.0f, null);
                }
                else
                {

                }
            }
            else
            {

            }
        }

        // =========================================================
        // 3. 击退 (Knockback)
        // =========================================================
        if (stats.nativeKnockback)
        {
            Vector3 pushDir = (target.transform.position - transform.position).normalized;
            pushDir.y = 0;
            receiver.ApplyKnockback(pushDir, stats.nativeKnockbackForce);
        }

        // =========================================================
        // 4. 燃烧 (Burn)
        float finalChance = (sourceWeapon != null) ? sourceWeapon.GetIgnitionChance() : stats.ignitionChance;
        // 【新增】读取局部加成
        float burnDurationBonus = (sourceWeapon != null) ? sourceWeapon.localBurnDurationBonus : 0f;
        float maxHpBurnPct = (sourceWeapon != null) ? sourceWeapon.localMaxHealthBurnPercent : 0f;

        bool appliedBurn = false;

        // 2. 概率判定
        if (finalChance > 0)
        {
            // 掷骰子
            if (Random.value <= finalChance)
            {
                // 计算百分比伤害 (比如直击的 20%)
                int burnDmg = Mathf.CeilToInt(this.damage * stats.burnDamagePercent);
                if (burnDmg < 1) burnDmg = 1; 

                // 【修改】加上局部持续时间加成 + 传递最大HP%伤害
                float finalBurnDuration = stats.baseDotDuration + burnDurationBonus;
                receiver.ApplyBurn(burnDmg, finalBurnDuration, stats.dotTickInterval, weaponName, maxHpBurnPct);
                appliedBurn = true;

                if (PlayerProgressManager.Instance != null)
                {
                    // "Ignite_Count" 必须和 PlayerProgressManager.CheckUnlocks 里的 Key 一致
                    PlayerProgressManager.Instance.AddStat("Ignite_Count", 1);
                }
            }
        }

        // 3. 向下兼容：如果没触发概率，但勾选了 nativeBurn (必燃)，则补上
        if (!appliedBurn && stats.nativeBurn)
        {
            if (!receiver.IsBurning)
            {
                float finalBurnDuration2 = stats.baseDotDuration + burnDurationBonus;
                receiver.ApplyBurn(stats.baseDotDamage, finalBurnDuration2, stats.dotTickInterval, weaponName, maxHpBurnPct);
            }
        }

        // =========================================================
        // 5. 减速 (Slow)
        // =========================================================
        if (stats.baseSlowPercentage > 0)
        {
            receiver.ApplySlow(stats.baseSlowPercentage, stats.baseSlowDuration, Color.blue);
        }

        // =========================================================
        // 6. 腐蚀 (Corrode)
        // =========================================================
        if (stats.nativeCorrode)
        {
            receiver.ApplyCorrode(stats.nativeCorrodeMultiplier, 5f, stats.nativeCorrodeColor, weaponName);
        }

    }
}