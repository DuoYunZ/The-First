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
    private bool isParabolic = false;

    [Header("直线参数")]
    private Vector3 direction;
    private float speed;

    // --- 抛物线轨迹参数 ---
    private Vector3 currentVelocity;
    public float gravity = 9.8f;
    public bool faceMovementDirection = true;

    // --- 通用子弹参数 ---
    private float lifetime = 5f; // Default value, will be overridden by Initialize
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
    private enum ProjectileMode { Straight, Parabolic, AirdropDeployer, Homing, Boomerang }
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

    // --- 回旋镖属性 ---
    private enum BoomerangState { Outbound, Inbound }
    private BoomerangState boomerangState;
    private WeaponPart launcher;
    private Vector3 spawnPoint; // 发射点 (记录玩家位置)
    private float maxDistance;
    private float catchRadius;
    private bool hasBeenCaught = false;
    private float rotationSpeed = 720f;
    private float returnOvershootDistance = 2f;
    private Vector3 returnDirection; // 返回时使用的方向

    // --- 伤害冷却字典 ---
    private Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private float hitCooldown = 0.5f;

    private float timeSinceReturnStarted = 0f;

    // --- 初始化方法 ---

    public void InitializeAsReflectable(Vector3 dir, float spd, int dmg, float life, GameObject vfx)
    {
        this.mode = ProjectileMode.Straight;
        this.attackType = AttackType.Reflectable;
        this.isEnemyProjectile = true;
        this.direction = dir;
        this.speed = spd;
        this.directDamage = dmg;
        this.lifetime = life;
        this.impactEffectPrefab = vfx;
        Destroy(gameObject, this.lifetime);
        // Debug Log
        // Debug.Log($"[Projectile Init] Reflectable: Lifetime={this.lifetime}");
    }

    public void MarkAsPlayerProjectile()
    {
        this.isEnemyProjectile = false;
        this.tag = "PlayerProjectile";
        this.damageableLayers = LayerMask.GetMask("Enemies");
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");
        this.attackType = AttackType.Standard;
    }

    public void SetNewDirection(Vector3 newDirection)
    {
        this.direction = newDirection.normalized;
        transform.rotation = Quaternion.LookRotation(this.direction);
    }

    public void InitializeAsHoming(Transform target, float spd, int dmg, bool isEnemyBullet, float turnSpeed, float life, GameObject shieldVfx, GameObject defaultVfx)
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
        // Debug Log
        // Debug.Log($"[Projectile Init] Homing: Lifetime={this.lifetime}");
    }

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
        this.groundAndWallLayers = LayerMask.GetMask("Enemies", "Ground");
        this.damageableLayers = LayerMask.GetMask("Enemies");
        // Use a longer default lifetime for deployers in case they miss the ground
        this.lifetime = 10f;
        Destroy(gameObject, this.lifetime);
        // Debug Log
        // Debug.Log($"[Projectile Init] Airdrop: Lifetime={this.lifetime}");
    }

    public void InitializeAsStraight(Vector3 dir, float spd, int directDmg, bool isEnemyBullet, int pierce, float life, GameObject shieldVfx, GameObject defaultVfx, int dotDmg, float dotDur, float dotTick, float slowPct, float slowDur, AttackType type = AttackType.Standard)
    {
        this.mode = ProjectileMode.Straight;
        this.isParabolic = false;
        this.attackType = type;
        this.shieldImpactEffectPrefab = shieldVfx;
        this.defaultImpactEffectPrefab = defaultVfx;
        this.direction = dir.normalized;
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
        this.damageableLayers = isEnemyBullet ? LayerMask.GetMask("Player") : LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");
        Destroy(gameObject, this.lifetime);
        // Debug Log
        // Debug.Log($"[Projectile Init] Straight: Lifetime={this.lifetime}");
    }

    public void InitializeAsParabolic(Vector3 initialVelocity, int projectileDirectDamage, int projectileAoeDamage, float projectileLifetime, GameObject explosionVfxPrefab, float aoeRadius, LayerMask layersToDamage, LayerMask layersToExplodeOn, int dotDmg, float dotDur, float dotTick)
    {
        this.mode = ProjectileMode.Parabolic;
        this.isParabolic = true;
        this.currentVelocity = initialVelocity;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
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

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        Destroy(gameObject, this.lifetime);
        // Debug Log
        // Debug.Log($"[Projectile Init] Parabolic: Lifetime={this.lifetime}");
    }

    public void InitializeAsChaining(Vector3 dir, float spd, int dmg, int chains, float range, float life, GameObject vfx)
    {
        this.mode = ProjectileMode.Straight;
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
        this.damageableLayers = LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");
        Destroy(gameObject, this.lifetime);
        // Debug Log
        // Debug.Log($"[Projectile Init] Chaining: Lifetime={this.lifetime}");
    }

    public void InitializeAsBoomerang(Vector3 dir, float spd, int dmg, float maxDist, float catchRad, float life,
                                     GameObject shieldVFX, GameObject defaultVFX, WeaponPart launcherPart,
                                     float rotSpeed, float overshootDist)
    {
        // --- vvv Added Diagnostics vvv ---
        Debug.Log($"[Projectile Init Boomerang] Received Lifetime: {life}, Speed: {spd}, MaxDist: {maxDist}");
        if (life <= 0.1f) // Check if the received lifetime is suspiciously short
        {
            Debug.LogError($"[Projectile Init Boomerang] WARNING: Received very short lifetime ({life})! Check WeaponStatBlock 'Base Projectile Lifetime'.", this);
        }
        // --- ^^^ Added Diagnostics ^^^ ---

        this.mode = ProjectileMode.Boomerang;
        this.isParabolic = false;
        this.attackType = AttackType.Standard;
        this.shieldImpactEffectPrefab = shieldVFX;
        this.defaultImpactEffectPrefab = defaultVFX;
        this.direction = dir.normalized;
        this.speed = spd;
        this.directDamage = dmg;
        this.pierceCount = 999;
        this.lifetime = life; // Assign the received lifetime

        this.launcher = launcherPart;
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            this.spawnPoint = GameManager.Instance.playerTransform.position;
        else
        {
            Debug.LogError("Projectile (Boomerang): GameManager 未找到，无法获取玩家位置！ Using fire point as fallback.", this);
            this.spawnPoint = transform.position; // Fallback to initial position if player not found
        }
        transform.position = this.spawnPoint; // Start at the recorded spawn point

        this.maxDistance = maxDist;
        this.catchRadius = catchRad;
        this.rotationSpeed = rotSpeed;
        this.returnOvershootDistance = overshootDist;

        this.boomerangState = BoomerangState.Outbound;
        this.hasBeenCaught = false;

        this.damageableLayers = LayerMask.GetMask("Enemies");
        this.groundAndWallLayers = LayerMask.GetMask("Ground");

        // Boomerang destruction is handled in Update
        Debug.Log($"[Projectile Init Boomerang] Finished. Mode={this.mode}, State={this.boomerangState}, Lifetime set to {this.lifetime}");
    }

    private void OnDrawGizmos()
    {
        if (isParabolic)
        {
            Gizmos.color = new Color(1, 0.92f, 0.016f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, this.explosionRadius);
        }
        // Optional: Draw boomerang catch radius
        // if(mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Inbound)
        // {
        //     Gizmos.color = Color.cyan;
        //     Gizmos.DrawWireSphere(transform.position, catchRadius);
        // }
    }

    void Update()
    {
        if (hasExploded) return;

        // --- Movement Logic ---
        switch (mode)
        {
            case ProjectileMode.Straight:
            case ProjectileMode.AirdropDeployer:
                transform.position += direction * speed * Time.deltaTime;
                break;
            case ProjectileMode.Parabolic:
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity += Vector3.down * gravity * Time.deltaTime;
                    if (faceMovementDirection && rb.velocity.sqrMagnitude > 0.01f)
                    { transform.rotation = Quaternion.LookRotation(rb.velocity); }
                }
                break;
            case ProjectileMode.Homing:
                if (homingTarget != null)
                {
                    Vector3 directionToTarget = (homingTarget.position - transform.position).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, homingTurnSpeed * Time.deltaTime);
                }
                transform.position += transform.forward * speed * Time.deltaTime;
                break;
            case ProjectileMode.Boomerang:
                // Self-rotation
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
                // Movement based on state
                if (boomerangState == BoomerangState.Outbound) { HandleBoomerangOutbound(); }
                else if (boomerangState == BoomerangState.Inbound) { HandleBoomerangInbound(); }
                break;
        }

        // --- Damage Cooldown Update ---
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

        // --- Lifetime Check and Destruction Logic ---
        // Add diagnostic log before check
        // Debug.Log($"[Projectile Update] {gameObject.name} - Lifetime: {lifetime:F2}, Mode: {mode}, Boomerang Caught: {hasBeenCaught}");

        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // Only destroy if NOT parabolic (handled by collision/explosion)
            // AND if (NOT boomerang OR (is boomerang AND NOT caught))
            if (mode != ProjectileMode.Parabolic && (mode != ProjectileMode.Boomerang || !hasBeenCaught))
            {
                Debug.Log($"[Projectile Destroy] {gameObject.name} Lifetime ended. Mode={mode}, Boomerang Caught={hasBeenCaught}");
                if (mode == ProjectileMode.Boomerang && launcher != null)
                {
                    launcher.StartCooldownIfNotCaught(); // Notify timeout
                }
                Destroy(gameObject);
            }
            // Parabolic projectiles are destroyed on collision/explosion
            // Caught boomerangs just continue until they fade naturally (or hit something else after being caught, though unlikely)
            // If a caught boomerang needs to disappear after a certain time *after being caught*, we'd need more logic.
        }
    }

    // --- Collision/Trigger Handling ---

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded || mode == ProjectileMode.Parabolic) return; // Parabolic uses OnCollisionEnter

        // --- Boomerang Catch Logic ---
        if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Inbound && other.CompareTag("Player"))
        {
            Transform currentPlayerTransform = GameManager.Instance?.playerTransform;
            if (!hasBeenCaught && currentPlayerTransform != null && Vector3.Distance(transform.position, currentPlayerTransform.position) < catchRadius)
            {
                hasBeenCaught = true;
                if (launcher != null)
                {
                    launcher.OnBoomerangCaught(transform.position);
                }
                Debug.Log("Boomerang Caught!");
                // Do not return here yet, allow it to potentially hit walls/enemies right after catch if needed
            }
            // If caught, it won't be destroyed prematurely by distance check in HandleBoomerangInbound
            return; // Important: Don't process other collisions if it hits the player on return
        }

        // --- Damage Target Logic ---
        int targetLayer = isEnemyProjectile ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Enemies");
        if (other.gameObject.layer == targetLayer)
        {
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                HandleHit(targetHealth, other);
            }
        }
        // --- Ground/Wall Collision Logic ---
        else if (((1 << other.gameObject.layer) & groundAndWallLayers) != 0)
        {
            if (mode == ProjectileMode.Boomerang && boomerangState == BoomerangState.Outbound)
            {
                SetReturnState(); // Hit wall on way out, start returning
            }
            else if (mode != ProjectileMode.Boomerang) // Non-boomerang projectiles
            {
                HandleImpactEffect(false, other.ClosestPoint(transform.position));
                Destroy(gameObject);
            }
            // Boomerang hitting wall on return continues until lifetime ends or hits spawn point
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded || mode != ProjectileMode.Parabolic) return;

        if (((1 << collision.gameObject.layer) & groundAndWallLayers) != 0)
        {
            Explode(collision.contacts[0].point, collision.collider);
        }
        else if (((1 << collision.gameObject.layer) & damageableLayers) != 0)
        {
            Explode(collision.contacts[0].point, collision.collider);
        }
    }

    // --- Helper Methods ---

    void HandleHit(Health targetHealth, Collider hitCollider)
    {
        if (targetHealth.IsDead || hitEnemies.Contains(targetHealth)) return; // Skip dead or already hit by this projectile instance
        if (hitCooldowns.ContainsKey(targetHealth)) return; // Skip if on cooldown for this specific target

        hitCooldowns[targetHealth] = hitCooldown; // Apply cooldown for this target

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        bool targetHasShield = isEnemyProjectile && targetHealth.HasActiveShield();
        GameObject attacker = creatorAttacker ?? launcher?.gameObject; // Determine attacker
        bool wasReflected = targetHealth.TakeDamage(directDamage, hitPoint, attacker, this.attackType, this);

        if (!wasReflected)
        {
            // Apply Status Effects
            StatusEffectReceiver receiver = targetHealth.GetComponent<StatusEffectReceiver>();
            if (receiver != null)
            {
                if (dotDamage > 0) receiver.ApplyBurn(dotDamage, dotDuration, dotTickInterval);
                if (slowPercentage > 0) receiver.ApplySlow(slowPercentage, slowDuration);
            }
            // Play Impact Effect
            HandleImpactEffect(targetHasShield, hitPoint);
            // Mark as hit
            hitEnemies.Add(targetHealth);
            piercedEnemies++;

            // Handle Pierce/Chain/Destroy (Only for non-Boomerang)
            if (mode != ProjectileMode.Boomerang && piercedEnemies >= pierceCount)
            {
                if (remainingChains > 0) HandleChaining(hitPoint);
                else Destroy(gameObject);
            }
            // Boomerang continues flying after hitting enemies
        }
        // If reflected, do nothing, let the TakeDamage handle it
    }

    private void HandleBoomerangOutbound()
    {
        transform.position += direction * speed * Time.deltaTime;
        float distanceTraveled = Vector3.Distance(spawnPoint, transform.position);
        if (distanceTraveled >= maxDistance)
        {
            SetReturnState(); // Reached max distance, start returning
        }
    }

    private void HandleBoomerangInbound()
    {
        // Increment the return timer
        timeSinceReturnStarted += Time.deltaTime;

        // Calculate direction towards spawn point dynamically
        returnDirection = (spawnPoint - transform.position).normalized;
        transform.position += returnDirection * speed * Time.deltaTime;

        // Check if near spawn point AND not caught yet
        if (!hasBeenCaught)
        {
            // --- Introduce a minimum return time before checking distance ---
            // Only check proximity if we've been returning for at least a fraction of a second
            if (timeSinceReturnStarted > 1f) // e.g., wait 0.1 seconds
            {
                float distanceToSpawn = Vector3.Distance(transform.position, spawnPoint);
                if (distanceToSpawn < 0.5f) // Reached proximity of spawn point
                {
                    Debug.Log($"[Projectile Destroy] Boomerang reached spawn point proximity ({distanceToSpawn:F2}m) after returning for {timeSinceReturnStarted:F2}s without being caught.");
                    if (launcher != null)
                    {
                        launcher.StartCooldownIfNotCaught(); // Notify timeout
                    }
                    Destroy(gameObject);
                }
            }
        }
        // If caught, it continues moving until lifetime expires (handled in Update)
    }

    private void SetReturnState()
    {
        if (boomerangState == BoomerangState.Outbound) // Ensure this only happens once
        {
            boomerangState = BoomerangState.Inbound;
            timeSinceReturnStarted = 0f; // Reset the return timer when state changes
            Debug.Log("[Projectile] Boomerang entering Inbound state.");
            // Return direction is now calculated dynamically in HandleBoomerangInbound
        }
    }

    void HandleChaining(Vector3 hitPoint)
    {
        remainingChains--;
        Transform nextTarget = FindNextTarget(hitPoint);
        if (nextTarget != null)
        {
            // Debug.Log($"Chaining to target: {nextTarget.name}");
            this.direction = (nextTarget.position - transform.position).normalized;
            piercedEnemies = 0; // Reset pierce count for the new chain link
                                // hitCooldowns.Clear(); // Optionally reset cooldowns for chaining flexibility
        }
        else
        {
            Destroy(gameObject); // No more targets in range
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
            if (potentialTargetHealth != null && !potentialTargetHealth.IsDead && !hitEnemies.Contains(potentialTargetHealth))
            {
                float distSqr = (currentPosition - col.transform.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestTarget = potentialTargetHealth.transform; // Target the Health component's transform
                }
            }
        }
        return closestTarget;
    }

    void Explode(Vector3 explosionPoint, Collider initiallyHitCollider = null)
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, explosionPoint, Quaternion.identity);
        }

        Health directlyHitEnemyHealth = null;

        // Direct Hit Damage (if applicable)
        if (initiallyHitCollider != null && ((1 << initiallyHitCollider.gameObject.layer) & damageableLayers) != 0)
        {
            directlyHitEnemyHealth = initiallyHitCollider.GetComponentInParent<Health>();
            if (directlyHitEnemyHealth != null && !directlyHitEnemyHealth.IsDead)
            {
                // Debug.Log($"[Projectile Explode] Direct Hit on {initiallyHitCollider.name} for {directDamage} damage.");
                GameObject attacker = creatorAttacker ?? launcher?.gameObject;
                directlyHitEnemyHealth.TakeDamage(directDamage, explosionPoint, attacker, AttackType.Standard); // Use explosionPoint for damage source location?
                StatusEffectReceiver receiver = directlyHitEnemyHealth.GetComponent<StatusEffectReceiver>();
                if (receiver != null && dotDamage > 0)
                { receiver.ApplyBurn(dotDamage, dotDuration, dotTickInterval); }
            }
        }

        // Area Damage
        Collider[] collidersInRange = Physics.OverlapSphere(explosionPoint, explosionRadius, damageableLayers);
        foreach (Collider hitCollider in collidersInRange)
        {
            // Skip player and the directly hit enemy (if any)
            if (hitCollider.CompareTag("Player")) continue;
            Health healthComponent = hitCollider.GetComponentInParent<Health>();
            if (healthComponent != null && healthComponent != directlyHitEnemyHealth && !healthComponent.IsDead)
            {
                // Debug.Log($"[Projectile Explode] AOE Hit on {hitCollider.name} for {aoeDamage} damage.");
                GameObject attacker = creatorAttacker ?? launcher?.gameObject;
                healthComponent.TakeDamage(aoeDamage, explosionPoint, attacker, AttackType.Standard);
                StatusEffectReceiver receiver = healthComponent.GetComponent<StatusEffectReceiver>();
                if (receiver != null && dotDamage > 0)
                { receiver.ApplyBurn(dotDamage, dotDuration, dotTickInterval); }
            }
        }

        Destroy(gameObject); // Destroy after explosion logic
    }

    private void HandleImpactEffect(bool hitShield, Vector3 position)
    {
        GameObject effectToPlay = hitShield ? shieldImpactEffectPrefab : defaultImpactEffectPrefab;
        if (effectToPlay != null)
        {
            Instantiate(effectToPlay, position, Quaternion.identity);
        }
    }
}

