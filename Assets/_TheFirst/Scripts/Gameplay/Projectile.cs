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
    public bool isCritical = false;

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

    private Rigidbody rb;
    // --- ^^^ 新增结束 ^^^ ---


    // --- 伤害冷却字典 ---
    private Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private float hitCooldown = 0.5f;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            // 如果 Awake 时找不到，尝试在子物体找 (作为后备)
            rb = GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                Debug.LogWarning("Rigidbody found on child, not self. Consider moving component.", this);
            }
        }

        // 最终检查
        if (rb == null)
        {
            Debug.LogError("Projectile CRITICAL: Could not find Rigidbody component!", this);
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
            Debug.LogError("[Projectile Init Homing] FAILED: Rigidbody reference is null (check Awake).", this);
            // Destroy(gameObject);
            // return;
        }
        this.mode = ProjectileMode.Homing;
        this.isParabolic = false;
        this.homingTarget = target;
        this.speed = spd;
        this.damage = dmg;
        this.homingTurnSpeed = turnSpeed;
        this.isEnemyProjectile = isEnemyBullet;
        this.lifetime = life;
        this.shieldImpactEffectPrefab = shieldVfx;
        this.defaultImpactEffectPrefab = defaultVfx;
        if (rb != null)
        {
            rb.isKinematic = false; // 确保非运动学
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
                                          GameObject explodeVfx = null)
    {

        Debug.Log($"[Projectile Debug] 直线初始化。传入的 launcher 是: {(launcher != null ? launcher.name : "NULL (空)")}");
        if (rb == null && spd > 0)
        {
            Debug.LogError("[Projectile Init Straight] FAILED: Rigidbody reference is null.", this);
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
            Debug.LogError("[Projectile Init Boomerang] FAILED: Rigidbody reference is null (check Awake).", this);
            Destroy(gameObject);
            return;
        }
        // 添加详细日志
        Debug.Log($"[Projectile Init Boomerang] START - Dir:{dir}, Spd:{spd}, Dmg:{dmg}, MaxDist:{maxDist}, CatchRad:{catchRad}, Life:{life}, RotSpd:{rotSpeed}");

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
        Debug.Log($"[Projectile Init Boomerang] FINISHED - Initial Velocity set in FixedUpdate. Lifetime={this.lifetime}");
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

            Debug.Log($"[Projectile Boomerang] State changed to Inbound. TargetPoint={returnTargetPoint}, ReturnDir={currentReturnDirection}");

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
                Explode(other.ClosestPoint(transform.position), other);
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
        // 伤害敌人 (保持不变)
        int targetLayer = isEnemyProjectile ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Enemies");
        if (other.gameObject.layer == targetLayer)
        {
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null) { HandleHit(targetHealth, other); }
        }
        // 撞墙/地面 (保持不变)
        else if (((1 << other.gameObject.layer) & groundAndWallLayers) != 0)
        {
            if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Outbound)
            {
                SetReturnState(); // 撞墙开始返回
            }
            else if (mode != ProjectileMode.Boomerang)
            {
                // 【核心修改】如果是爆裂弹，撞墙也要爆炸
                if (explosionRadius > 0)
                {
                    Explode(other.ClosestPoint(transform.position), other);
                }
                else
                {
                    // 普通子弹：播个撞击特效然后销毁
                    HandleImpactEffect(false, other.ClosestPoint(transform.position));
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
                Debug.Log($"连锁到新目标: {nextTarget.name}");
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

        // 播放爆炸特效
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, explosionPoint, Quaternion.identity);
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
                // 既不是火焰也不是黑洞，才销毁并报错 (或者只作为纯特效)
                Debug.LogWarning($"[Explode] 生成了 {hazardObj.name}，但上面既没有 GroundHazard 也没有 BlackHoleField 脚本。纯视觉销毁。");
                Destroy(hazardObj, stats.groundHazardDuration);
            }
        }


        // =========================================================
        //  逻辑 2: 分裂毒爆 (生成追踪虫)
        // =========================================================
        if (stats != null && stats.subProjectilePrefab != null && stats.subProjectileCount > 0)
        {
            SpawnClusterProjectiles(explosionPoint, stats);
        }

        // =========================================================
        //  逻辑 3: 伤害与物理效果 (含奇点手雷)
        // =========================================================

        // A. 处理直接命中 (如果有)
        Health directlyHitEnemyHealth = null;
        if (initiallyHitCollider != null && initiallyHitCollider.CompareTag("Enemy"))
        {
            directlyHitEnemyHealth = initiallyHitCollider.GetComponentInParent<Health>();
            if (directlyHitEnemyHealth != null && !directlyHitEnemyHealth.IsDead)
            {
                directlyHitEnemyHealth.TakeDamage(damage, explosionPoint, this.gameObject, AttackType.Standard, null, null, weaponName);
                ApplyElementalEffects(directlyHitEnemyHealth, weaponName);
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

                    // 3. 处理【奇点手雷 (Black Hole)】 vs 普通击退
                    StatusEffectReceiver receiver = healthComponent.GetComponent<StatusEffectReceiver>();
                    if (receiver != null && stats != null)
                    {
                        if (stats.isBlackHole)
                        {
                            // --- 奇点逻辑：吸力 ---
                            // 方向：从敌人位置 -> 指向爆炸中心
                            Vector3 pullDir = (explosionPoint - healthComponent.transform.position).normalized;
                            pullDir.y = 0; // 保持水平
                                           // 施加吸力 (复用 ApplyKnockback，因为本质都是位移)
                            receiver.ApplyKnockback(pullDir, stats.blackHoleForce);
                        }
                        else
                        {
                            // --- 普通逻辑：爆炸击退 (可选) ---
                            // 如果你也想让普通手雷有击退，可以在这里写：
                            // Vector3 pushDir = (healthComponent.transform.position - explosionPoint).normalized;
                            // receiver.ApplyKnockback(pushDir, 5f); 
                        }
                    }
                }
            }
        }

        Destroy(gameObject);
    }

    private void SpawnClusterProjectiles(Vector3 origin, WeaponStatBlock stats)
    {
        // 1. --- 寻找地面生成点 ---
        Vector3 spawnBasePos = origin;
        // 定义地面层级 (排除敌人和玩家，防止生成在头顶)
        int groundMask = LayerMask.GetMask("Ground",  "Terrain");
        if (groundMask == 0) groundMask = ~(LayerMask.GetMask("Enemies", "Player", "PlayerProjectile"));

        RaycastHit hit;
        // 从爆炸点上方一点向下射，找地面
        if (Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out hit, 50f, groundMask))
        {
            // 找到了地面，基准点设为地面点
            spawnBasePos = hit.point;
        }
        else
        {
            // 没找到地面 (空中爆炸)，就勉强用空中点，但最好确保你的地图有地面层
            // spawnBasePos = origin; 
        }

        for (int i = 0; i < stats.subProjectileCount; i++)
        {
            // 2. --- 计算随机散布方向 (仅水平面) ---
            Vector3 spreadDir = Random.onUnitSphere; // 生成一个随机球体方向
            spreadDir.y = 0; // 压平到水平面，让蜘蛛在地上爬散开
            spreadDir.Normalize();

            // 生成点稍微抬高一点点，防止卡进地里
            Vector3 finalSpawnPos = spawnBasePos + Vector3.up * 0.1f;

            // 3. --- 生成子弹 ---
            GameObject subObj = Instantiate(stats.subProjectilePrefab, finalSpawnPos, Quaternion.LookRotation(spreadDir));

            // 可选：让分裂出来的小蜘蛛看起来小一点
            subObj.transform.localScale = transform.localScale * 0.6f;

            // 4. --- 初始化子弹 ---
            Projectile subScript = subObj.GetComponent<Projectile>();
            if (subScript != null)
            {
                // 在地面基准点附近找目标
                Transform target = FindRandomNearbyEnemy(spawnBasePos, 15f);

                // 伤害减半
                int subDmg = Mathf.RoundToInt(aoeDamage * 0.5f);
                if (subDmg < 1) subDmg = 1;

                // 确定特效：优先用专属特效，没有就用默认保底
                GameObject vfxToUse = stats.subProjectileHitVfx != null ? stats.subProjectileHitVfx : defaultImpactEffectPrefab;

                // 初始化为追踪弹 (Homing)
                // 注意：最后两个参数传入我们确定的 vfxToUse
                subScript.InitializeAsHoming(
                    target,
                    8f,  // 速度稍微慢点，像爬行
                    subDmg,
                    false,
                    15f, // 转向速度快点，更灵活
                    4f,  // 存活时间略长
                    vfxToUse, // 命中特效 (Hit VFX)
                    vfxToUse  // 障碍物特效 (Obstacle VFX) - 蜘蛛通常用同一个
                );

                // 【核心】：传递发射器引用，这让子弹能继承毒石属性！
                subScript.sourceWeapon = this.sourceWeapon;
            }
        }
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

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
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
        if (explosionRadius > 0)
        {
            // 在接触点引爆 (Explode 方法里已经包含了 AOE 伤害、特效和销毁自身的逻辑)
            Explode(hitCollider.ClosestPoint(transform.position), hitCollider);
            return; // 爆炸后子弹销毁，不再执行穿透/连锁逻辑
        }

        string weaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) ? sourceWeapon.StatBlock.weaponName : "";
        bool wasReflected = targetHealth.TakeDamage(
            damage,
            hitCollider.ClosestPoint(transform.position),
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
            hitEnemies.Add(targetHealth);
            piercedEnemies++;

            if (mode != ProjectileMode.Boomerang && piercedEnemies >= pierceCount)
            {
                if (remainingChains > 0) HandleChaining(hitPoint);
                else Destroy(gameObject);
            }
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
            Debug.Log($"连锁到新目标: {nextTarget.name}");
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

        // 调试日志：看看现在的概率到底是 0.1 还是 0.4
        Debug.Log($"[燃烧判定] 来源: {weaponName}, 最终概率: {finalChance}");

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

                // 应用燃烧 (去掉了 !IsBurning 限制，允许刷新)
                receiver.ApplyBurn(burnDmg, stats.baseDotDuration, stats.dotTickInterval, weaponName);
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
            // 旧逻辑保留防覆盖检查 (或者你也想让它能刷新，就去掉 if)
            if (!receiver.IsBurning)
            {
                receiver.ApplyBurn(stats.baseDotDamage, stats.baseDotDuration, stats.dotTickInterval, weaponName);
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