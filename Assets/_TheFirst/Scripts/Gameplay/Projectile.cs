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

    private bool hasBeenCaught = false; // 是否已被抓取
    // --- ^^^ 新增结束 ^^^ ---


    // --- 伤害冷却字典 ---
    private Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private float hitCooldown = 0.5f;

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
        Destroy(gameObject, this.lifetime);
    }

    /// <summary>
    /// 初始化为【抛物线】弹道。由 WeaponPart 调用。
    /// </summary>
    public void InitializeAsParabolic(Vector3 initialVelocity, int projectileDirectDamage, int projectileAoeDamage, float projectileLifetime, GameObject explosionVfxPrefab, float aoeRadius, LayerMask layersToDamage, LayerMask layersToExplodeOn, int dotDmg, float dotDur, float dotTick)
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

    public void InitializeAsBoomerang(Vector3 dir, float spd, int dmg, float maxDist,
                                           float rotSpeed, float overshootDist) // <-- 新增 overshootDist
    {
        this.mode = ProjectileMode.Boomerang;
        this.isParabolic = false;
        this.attackType = AttackType.Standard;
        this.direction = dir.normalized; // 初始飞行方向
        this.speed = spd;
        this.directDamage = dmg;
        this.pierceCount = 999;
        this.lifetime = 10f; // 保持固定生命周期
        this.maxDistance = maxDist;
        this.rotationSpeed = rotSpeed;
        this.spawnPoint = transform.position; // 记录发射时的确切位置
        this.returnOvershootDistance = overshootDist; // <-- 接收过冲距离

        // --- 设置初始状态 ---
        this.boomerangState = BoomerangState.Outbound; // <-- 设置初始状态
        // --- 设置结束 ---

        this.damageableLayers = LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // 暂时保留生命周期销毁
        Destroy(gameObject, this.lifetime);

        Debug.Log($"[Projectile Init Boomerang Step2] Initialized: Speed={spd}, MaxDist={maxDist}, RotSpeed={rotSpeed}, Overshoot={overshootDist}, Lifetime={this.lifetime}");
    }

    private void SetReturnState()
    {
        // 确保只从 Outbound 状态切换一次
        if (boomerangState == BoomerangState.Outbound)
        {
            boomerangState = BoomerangState.Inbound;

            // 计算包含过冲的最终返回点
            // 使用初始方向的反方向 (-direction) 来计算过冲点
            returnTargetPoint = spawnPoint - direction * returnOvershootDistance;

            // 计算从当前位置到目标点的固定返回方向
            returnDirection = (returnTargetPoint - transform.position).normalized;

            Debug.Log($"[Projectile Boomerang] Entering Inbound state. Target: {returnTargetPoint}");

            // （可选）如果使用 Rigidbody，可以在这里清除一下当前速度，使转向更干脆
            Rigidbody rb = GetComponent<Rigidbody>();
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
        /*lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // 只要生命周期结束就销毁，除非是特殊情况（例如抛物线还没落地）
            // 回旋镖如果超时未接住，也在此处销毁
            if (mode != ProjectileMode.Parabolic) // 抛物线由碰撞或自身逻辑销毁
            {
                Debug.Log($"[Projectile Destroy] {gameObject.name} Lifetime ended ({lifetime:F2}s). Mode={mode}, Boomerang Caught={hasBeenCaught}");
                // 如果是回旋镖且未被抓住，通知冷却
                if (mode == ProjectileMode.Boomerang && !hasBeenCaught && launcher != null)
                {
                    launcher.StartCooldownIfNotCaught();
                }
                Destroy(gameObject);
            }
        }*/
    }

    void FixedUpdate()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
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
                    rb.velocity = direction * speed; // 继续飞出
                                                     // 检查是否到达最大距离
                    float distanceTraveled = Vector3.Distance(spawnPoint, rb.position);
                    if (distanceTraveled >= maxDistance)
                    {
                        SetReturnState(); // 到达距离，开始返回
                    }
                }
                else if (boomerangState == BoomerangState.Inbound)
                {
                    // 朝计算好的返回方向移动
                    rb.velocity = returnDirection * speed;

                    // (可选) 检查是否已飞过返回点，如果飞过了也可以销毁
                    // if (Vector3.Dot(returnDirection, (returnTargetPoint - rb.position)) < 0)
                    // {
                    //     Debug.Log("[Projectile Boomerang] Overshot return target. Destroying.");
                    //     Destroy(gameObject);
                    // }
                }
                break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded || mode == ProjectileMode.Parabolic) return;

        // --- 碰撞玩家逻辑 (将在下一步实现抓取) ---
        // if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Inbound && other.CompareTag("Player"))
        // {
        //     // Step 3: Implement Catch Logic Here
        //     return;
        // }

        // --- 碰撞敌人逻辑 ---
        int targetLayer = isEnemyProjectile ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Enemies");
        if (other.gameObject.layer == targetLayer)
        {
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                HandleHit(targetHealth, other); // HandleHit 不会销毁回旋镖
            }
        }
        // --- 碰撞墙壁/地面逻辑 ---
        else if (((1 << other.gameObject.layer) & groundAndWallLayers) != 0)
        {
            // --- vvv 修改回旋镖撞墙逻辑 vvv ---
            if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Outbound)
            {
                Debug.Log("[Projectile Boomerang] Hit wall while outbound. Returning.");
                SetReturnState(); // 撞墙，开始返回
            }
            // --- ^^^ 修改结束 ^^^ ---
            else if (mode != ProjectileMode.Boomerang) // 其他子弹撞墙销毁
            {
                HandleImpactEffect(false, other.ClosestPoint(transform.position));
                Destroy(gameObject);
            }
            // 回旋镖在返回途中撞墙会继续飞行
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