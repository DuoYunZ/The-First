using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public AttackType attackType = AttackType.Standard;

    [Header("视觉效果")]
    [Tooltip("命中护盾时播放的专属特效")]
    public GameObject shieldImpactEffectPrefab;
    [Tooltip("命中无护盾玩家或敌人时播放的通用特效")]
    public GameObject defaultImpactEffectPrefab;

    // --- 弹道控制 ---
    private bool isParabolic = true; // 默认为抛物线

    [Header("直线参数")]
    private Vector3 direction;
    private float speed;

    // --- 抛物线轨迹参数 ---
    private Vector3 currentVelocity;            // 子弹当前的飞行速度和方向
    public float gravity = 9.8f;                // 重力加速度（可以根据游戏感觉调整）
    public bool faceMovementDirection = true;   // 子弹是否朝向其飞行方向

    // --- 通用子弹参数 ---
    public float lifetime = 5f;                 // 子弹存活时间（抛物线可能需要更长存活期）
    private int directDamage = 0;   // 直接命中伤害
    private int aoeDamage = 0;                  // 范围爆炸伤害
    private bool hasExploded = false;           // 防止重复爆炸的标记

    [Header("效果与范围 (由Initialize方法设置)")]
    public GameObject impactEffectPrefab;  // 直线弹的命中特效
    public GameObject explosionEffectPrefab; // 抛物线弹的爆炸特效
    public float explosionRadius = 3f;
    public LayerMask damageableLayers;
    public LayerMask groundAndWallLayers;

    // --- 穿透相关变量 ---
    private int pierceCount = 1; // 此子弹允许的总命中次数
    private int piercedEnemies = 0; // 已命中的敌人数量

    // --- 连锁相关变量 ---
    private int remainingChains = 0;
    private float _chainRange = 0f;
    private List<Health> hitEnemies = new List<Health>(); // 存储本颗子弹已经命中过的敌人，防止重复攻击或无限循环

    // 用于存储持续伤害(DoT)效果的变量 ---
    private int dotDamage;
    private float dotDuration;
    private float dotTickInterval;

    // --- 弹道和行为模式 ---
    private enum ProjectileMode { Straight, Parabolic, AirdropDeployer, Homing }
    private ProjectileMode mode;
    private bool isEnemyProjectile = false; // 【新增】用于区分敌我
    private Transform homingTarget;
    private float homingTurnSpeed = 5f;

    // --- 部署器专用的“有效载荷”信息 ---
    private GameObject areaPrefabPayload;
    private int areaDamagePayload;
    private float areaDurationPayload;
    private float areaIntervalPayload;
    private GameObject creatorAttacker; // 用于传递攻击者

    // --- 新增：用于存储减速效果的变量 ---
    private float slowPercentage;
    private float slowDuration;


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
    public void InitializeAsAirdropDeployer(Vector3 startPosition, Vector3 flightDirection, float fallSpeed,
                                         GameObject areaPrefab, int dmg, float dur, float interval, GameObject attacker)
    {
        this.mode = ProjectileMode.AirdropDeployer;
        this.isParabolic = false;

        // 直接使用傳入的起始位置和飛行方向
        transform.position = startPosition;
        this.direction = flightDirection.normalized;
        this.speed = fallSpeed;

        // 保存“有效載荷”信息
        this.areaPrefabPayload = areaPrefab;
        this.areaDamagePayload = dmg;
        this.areaDurationPayload = dur;
        this.areaIntervalPayload = interval;
        this.creatorAttacker = attacker;

        // 設置碰撞層
        this.groundAndWallLayers = LayerMask.GetMask("Enemies", "Ground");
        Destroy(gameObject, 10f); // 安全銷毀
    }
    /// <summary>
    /// 为直线弹道设计的初始化方法
    /// </summary>
    public void InitializeAsStraight(Vector3 dir, float spd, int directDmg, bool isEnemyBullet, int pierce, float life,
                                     GameObject shieldVfx, GameObject defaultVfx, // <-- 两个特效参数
                                     int dotDmg, float dotDur, float dotTick,
                                     float slowPct, float slowDur, AttackType type = AttackType.Standard)
    {
        this.mode = ProjectileMode.Straight;
        this.isParabolic = false;
        this.attackType = type; // 【核心修改】设置子弹的攻击类型
        this.shieldImpactEffectPrefab = shieldVfx;   // 接收护盾特效
        this.defaultImpactEffectPrefab = defaultVfx; // 接收常规特效
        this.direction = dir;
        this.speed = spd;
        this.directDamage = directDmg;
        this.isEnemyProjectile = isEnemyBullet; // 记录身份
        this.pierceCount = pierce > 0 ? pierce : 1; // 确保穿透至少为1this.pierceCount = pierce > 0 ? pierce : 1; // 确保穿透至少为1
        this.aoeDamage = 0; // 直线弹没有范围伤害
        this.lifetime = life;       
        this.explosionRadius = 0; // 直线弹没有爆炸范围

        // 保存燃燒參數
        this.dotDamage = dotDmg;
        this.dotDuration = dotDur;
        this.dotTickInterval = dotTick;
        // 减速
        this.slowPercentage = slowPct;
        this.slowDuration = slowDur;

        Destroy(gameObject, this.lifetime);
    }

    /// <summary>
    /// 初始化为【抛物线】弹道。由 WeaponPart 调用。
    /// </summary>
    public void InitializeAsParabolic(Vector3 initialVelocity, int projectileDirectDamage, int projectileAoeDamage, float projectileLifetime,
                                    GameObject explosionVfxPrefab, float aoeRadius, LayerMask layersToDamage, LayerMask layersToExplodeOn,
                                    int dotDmg, float dotDur, float dotTick) // <-- 新增的参数
    {
        this.mode = ProjectileMode.Parabolic;
        this.isParabolic = true;
        this.currentVelocity = initialVelocity;
        this.directDamage = projectileDirectDamage;
        this.aoeDamage = projectileAoeDamage;
        this.lifetime = projectileLifetime;
        this.explosionEffectPrefab = explosionVfxPrefab;
        this.explosionRadius = aoeRadius;
        this.damageableLayers = layersToDamage;
        this.groundAndWallLayers = layersToExplodeOn;

        // 保存燃烧参数
        this.dotDamage = dotDmg;
        this.dotDuration = dotDur;
        this.dotTickInterval = dotTick;

        Destroy(gameObject, this.lifetime);
    }

    public void InitializeAsChaining(Vector3 dir, float spd, int dmg, int chains, float range, float life, GameObject vfx)
    {
        this.isParabolic = false; // 连锁闪电通常是直线或瞬移
        this.direction = dir;
        this.speed = spd;
        this.directDamage = dmg;
        this.remainingChains = chains;
        this._chainRange = range;
        this.lifetime = life;
        this.impactEffectPrefab = vfx;
        this.pierceCount = 1; // 连锁弹每次只命中一个目标，所以穿透为1
        this.hitEnemies.Clear(); // 确保列表是空的
        Destroy(gameObject, this.lifetime);
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
        if (hasExploded) return; // 如果已经爆炸，则不再执行任何操作

        if (mode == ProjectileMode.Straight || mode == ProjectileMode.AirdropDeployer)
        {
            transform.position += direction * speed * Time.deltaTime;
        }
        else if (mode == ProjectileMode.Parabolic)
        {
            // 抛物线运动逻辑
            currentVelocity.y -= gravity * Time.deltaTime;
            transform.position += currentVelocity * Time.deltaTime;
            if (faceMovementDirection && currentVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(currentVelocity);
            }
        }
        else if (mode == ProjectileMode.Homing)
        {
            if (homingTarget != null)
            {
                // 计算朝向目标的方向
                Vector3 directionToTarget = (homingTarget.position - transform.position).normalized;
                // 计算理想的旋转
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                // 平滑地转向目标
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, homingTurnSpeed * Time.deltaTime);
            }
            // 永远朝自己的正前方飞行
            transform.position += transform.forward * speed * Time.deltaTime;
        }
    }

    // --- 碰撞/触发处理 ---
    // 如果子弹的 Collider 的 Is Trigger 勾选了:
    void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        // 优先处理抛物线弹的爆炸逻辑
        if (isParabolic)
        {
            bool canExplode = ((1 << other.gameObject.layer) & groundAndWallLayers) != 0 || other.CompareTag("Enemy") || other.CompareTag("Player");
            if (canExplode)
            {
                Explode(other.ClosestPoint(transform.position), other);
            }
            return;
        }

        Health targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth != null)
        {
            HandleHit(targetHealth, other);
        }
        else if (((1 << other.gameObject.layer) & groundAndWallLayers) != 0)
        {
            if (defaultImpactEffectPrefab != null) Instantiate(defaultImpactEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
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
    private void HandleHit(Health targetHealth, Collider hitCollider)
    {
        // 验证攻击目标是否有效 (敌我识别，是否已命中等)
        bool isValidTarget = (isEnemyProjectile && hitCollider.CompareTag("Player")) ||
                             (!isEnemyProjectile && hitCollider.CompareTag("Enemy"));
        if (!isValidTarget || hitEnemies.Contains(targetHealth)) return;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);

        // 1. 询问目标是否有激活的护盾
        bool targetHasShield = isEnemyProjectile && targetHealth.HasActiveShield();

        // 2. 造成伤害，并检查是否被反弹
        bool wasReflected = targetHealth.TakeDamage(directDamage, hitPoint, this.gameObject, this.attackType, this);

        // 3. 如果子弹的生命周期结束了（没有被反弹）
        if (!wasReflected)
        {
            // 根据护盾状态选择要播放的特效
            GameObject effectToPlay = targetHasShield ? shieldImpactEffectPrefab : defaultImpactEffectPrefab;

            if (effectToPlay != null)
            {
                Instantiate(effectToPlay, hitPoint, Quaternion.identity);
            }

            hitEnemies.Add(targetHealth);
            piercedEnemies++;
            if (piercedEnemies >= pierceCount)
            {
                Destroy(gameObject);
            }
        }
    }
}