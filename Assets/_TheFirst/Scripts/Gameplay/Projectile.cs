using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

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
    private float speed;

    // --- 抛物线轨迹参数 ---
    private Vector3 currentVelocity;
    public float gravity = 9.8f;
    public bool faceMovementDirection = true;

    // --- 通用子弹参数 ---
    public float lifetime = 5f;
    private int directDamage = 0;
    private int aoeDamage = 0;
    private bool hasExploded = false;

    [Header("效果与范围 (由Initialize方法设置)")]
    public GameObject impactEffectPrefab;
    public GameObject explosionEffectPrefab;
    public float explosionRadius = 3f;
    public LayerMask damageableLayers;
    public LayerMask groundAndWallLayers;

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
    private WeaponPart launcher; // 抓取时需要通知的发射器
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
        this.directDamage = dmg;
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
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");
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
        this.directDamage = dmg;
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
    public void InitializeAsStraight(Vector3 dir, float spd, int directDmg, bool isEnemyBullet, int pierce, float life, GameObject shieldVfx, GameObject defaultVfx, int dotDmg, float dotDur, float dotTick, float slowPct, float slowDur, AttackType type = AttackType.Standard)
    {
        if (rb == null && spd > 0) // 只在需要移动时报错
        {
            Debug.LogError("[Projectile Init Straight] FAILED: Rigidbody reference is null (check Awake).", this);
            // Destroy(gameObject); // 可以选择销毁或让它停在原地
            // return;
        }

        this.mode = ProjectileMode.Straight;
        this.isParabolic = false;
        this.attackType = type;
        this.shieldImpactEffectPrefab = shieldVfx;
        this.defaultImpactEffectPrefab = defaultVfx;
        this.direction = dir;
        this.speed = spd;
        this.directDamage = directDmg;
        this.isEnemyProjectile = isEnemyBullet;
        this.pierceCount = pierce > 0 ? pierce : 1;
        this.lifetime = life;
        this.dotDamage = dotDmg;
        this.dotDuration = dotDur;
        this.dotTickInterval = dotTick;
        this.slowPercentage = slowPct;
        this.slowDuration = slowDur;
        this.damageableLayers = isEnemyBullet ? LayerMask.GetMask("Player") : LayerMask.GetMask("Enemies"); // 自动设置
        this.groundAndWallLayers = LayerMask.GetMask("Ground"); // 默认只与地面碰撞销毁
        if (rb != null)
        {
            rb.isKinematic = false; // 确保非运动学
                                    // 可以选择性地设置 Constraints，如果直线弹不需要旋转的话
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
                                      float newStunChance, float newStunDuration)
    {
        this.mode = ProjectileMode.Parabolic;
        this.isParabolic = true;
        this.currentVelocity = initialVelocity;
        Rigidbody rb = GetComponent<Rigidbody>(); // 抛物线需要 Rigidbody
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false; // 必须是非运动学才能受力
        rb.useGravity = false; // 我们手动模拟重力
        rb.velocity = initialVelocity;

        this.directDamage = projectileDirectDamage;
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
        this.directDamage = dmg;
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
        this.directDamage = dmg;
        this.pierceCount = 999;
        this.lifetime = life; // 总生命周期，必须大于 0
        this.maxDistance = maxDist;
        this.catchRadius = catchRad;
        this.rotationSpeed = rotSpeed;
        this.returnOvershootDistance = overshootDist; // <-- 存储过冲距离
        this.launcher = launcherPart;
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

        // --- Existing Damage Cooldown Logic ---
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
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // 【核心修改】生命周期结束时的处理
            Debug.Log($"[Projectile Destroy] {gameObject.name} Lifetime ended ({lifetime:F2}s). Mode={mode}, Boomerang Caught={hasBeenCaught}");

            // 只有当它是【未被抓住】的回旋镖时，才通知冷却
            if (mode == ProjectileMode.Boomerang && !hasBeenCaught && launcher != null)
            {
                launcher.StartCooldownIfNotCaught();
            }

            // 只要生命周期结束就销毁 (除非是特殊情况，但这里我们简化处理)
            // 抛物线由其碰撞逻辑处理销毁，这里不需要特殊判断了
            Destroy(gameObject);
            // 【核心修改结束】
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
                Debug.Log("Boomerang Caught!");
                if (launcher != null)
                {
                    launcher.OnBoomerangCaught(transform.position); // 通知 WeaponPart (它会重新发射)
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
                HandleImpactEffect(false, other.ClosestPoint(transform.position));
                Destroy(gameObject);
            }
        }
    }

    void HandleEnemyHit(Health enemyHealth)
    {
        // 1. 造成伤害并记录
        enemyHealth.TakeDamage(directDamage, enemyHealth.transform.position, this.gameObject);
        hitEnemies.Add(enemyHealth);
        if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, enemyHealth.transform.position, Quaternion.identity);

        // 2. 处理穿透逻辑
        if (pierceCount > 1)
        {
            piercedEnemies++;
            if (piercedEnemies >= pierceCount)
            {
                Destroy(gameObject);
            }
            // 穿透和连锁通常是互斥的，我们可以在WeaponPart中决定初始化哪种
            return; // 如果是穿透弹，处理完后就结束
        }

        // 3. 处理连锁逻辑
        if (remainingChains > 0)
        {
            remainingChains--;
            Transform nextTarget = FindNextTarget(enemyHealth.transform.position);
            if (nextTarget != null)
            {
                // 找到了新目标，更新方向
                Debug.Log($"连锁到新目标: {nextTarget.name}");
                this.direction = (nextTarget.position - transform.position).normalized;
                // （可选）可以加一个瞬移或转向效果
            }
            else
            {
                // 没找到下一个目标，销毁子弹
                Destroy(gameObject);
            }
        }
        else
        {
            // 穿透和连锁次数都用完了，销毁子弹
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

        // ================== 新增的诊断日志 ==================
        if (initiallyHitCollider != null)
        {
            Debug.Log($"[诊断] 炮弹爆炸，初始碰撞体是: '{initiallyHitCollider.name}', 它的标签是: '{initiallyHitCollider.tag}', 它的层是: '{LayerMask.LayerToName(initiallyHitCollider.gameObject.layer)}'");
        }
        else
        {
            Debug.LogWarning("[诊断] 炮弹爆炸，但初始碰撞体为 null！");
            // 如果是 null，后续逻辑肯定不会执行，直接返回
            Destroy(gameObject);
            return;
        }

        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, explosionPoint, Quaternion.identity);
        }
        Health directlyHitEnemyHealth = null;

        // --- 1. 处理直接命中伤害 ---
        if (initiallyHitCollider.CompareTag("Enemy"))
        {
            // 只有当上面的诊断日志显示标签确实是 "Enemy" 时，才会进入这里
            directlyHitEnemyHealth = initiallyHitCollider.GetComponentInParent<Health>();
            if (directlyHitEnemyHealth != null && !directlyHitEnemyHealth.IsDead)
            {
                // 这条日志现在应该能正常显示了
                Debug.Log($"[Projectile] 炮弹爆炸，判定 '{initiallyHitCollider.name}' 为直接命中目标，造成 {directDamage} 点直接伤害。");
                directlyHitEnemyHealth.TakeDamage(directDamage, explosionPoint, this.gameObject, AttackType.Standard);

                StatusEffectReceiver receiver = directlyHitEnemyHealth.GetComponent<StatusEffectReceiver>();
                if (receiver != null && this.dotDamage > 0 && this.dotDuration > 0)
                {
                    receiver.ApplyBurn(this.dotDamage, this.dotDuration, this.dotTickInterval);
                }
            }
        }

        // --- 2. 处理范围伤害 ---
        Collider[] collidersInRange = Physics.OverlapSphere(explosionPoint, explosionRadius, damageableLayers);
        foreach (Collider hitCollider in collidersInRange)
        {
            if (hitCollider.CompareTag("Player")) continue;

            Health healthComponent = hitCollider.GetComponentInParent<Health>();

            if (healthComponent != null && healthComponent == directlyHitEnemyHealth)
            {
                continue;
            }

            if (healthComponent != null)
            {
                healthComponent.TakeDamage(aoeDamage, explosionPoint, this.gameObject, AttackType.Standard);

                StatusEffectReceiver receiver = healthComponent.GetComponent<StatusEffectReceiver>();
                if (receiver != null && this.dotDamage > 0 && this.dotDuration > 0)
                {
                    receiver.ApplyBurn(this.dotDamage, this.dotDuration, this.dotTickInterval);
                }

                if (this.stunChance > 0 && this.stunDuration > 0)
                {
                    // 进行概率摇奖
                    if (Random.value <= this.stunChance)
                    {
                        // 你需要去 StatusEffectReceiver.cs 中添加这个 ApplyStun 方法
                        receiver.ApplyStun(this.stunDuration);
                    }
                }
            }
        }

        Destroy(gameObject);
    }
    void HandleHit(Health targetHealth, Collider hitCollider)
    {
        if (targetHealth.IsDead || hitEnemies.Contains(targetHealth)) return;
        if (hitCooldowns.ContainsKey(targetHealth)) return;
        hitCooldowns[targetHealth] = hitCooldown;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        bool targetHasShield = isEnemyProjectile && targetHealth.HasActiveShield();
        GameObject attacker = creatorAttacker ?? launcher?.gameObject; // Use correct attacker
        bool wasReflected = targetHealth.TakeDamage(directDamage, hitPoint, attacker, this.attackType, this);

        if (!wasReflected)
        {
            // Apply Status Effects (if any were initialized)
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();
            if (receiver != null)
            {
                if (dotDamage > 0) receiver.ApplyBurn(dotDamage, dotDuration, dotTickInterval);
                if (slowPercentage > 0) receiver.ApplySlow(slowPercentage, slowDuration);
            }
            HandleImpactEffect(targetHasShield, hitPoint);
            hitEnemies.Add(targetHealth);
            piercedEnemies++;

            // --- vvv CRUCIAL: Ensure Boomerang DOES NOT destroy on hit vvv ---
            // Only destroy if NOT boomerang AND pierce count is exceeded
            if (mode != ProjectileMode.Boomerang && piercedEnemies >= pierceCount)
            {
                // Handle Chaining if applicable for non-boomerangs
                if (remainingChains > 0) HandleChaining(hitPoint);
                else Destroy(gameObject);
            }
            // --- ^^^ CRUCIAL End ^^^ ---
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
}