using UnityEngine;
using System.Collections;

[RequireComponent(typeof(WeaponPart))]
public class DroneAI : MonoBehaviour
{
    private enum DroneState { Orbiting, Attacking, Reloading }
    private DroneState currentState = DroneState.Orbiting;

    [Header("AI 基础设置")]
    public float detectionRange = 25f;
    public float idealDistance = 10f; // 理想交战距离
    public float moveSpeed = 15f;     // 恢复旧版速度
    public float turnSpeed = 10f;
    public LayerMask enemyLayer;

    [Header("弹幕攻击设置")]
    public int maxAmmo = 6;             // 弹匣
    public float burstInterval = 0.1f;  // 连发间隔 (越小越快)
    public float reloadTime = 2.0f;     // 换弹时间
    [Range(0, 180)]
    public float spreadAngle = 60f;     // 散射角度 (设大一点，配合延迟追踪实现弧线)

    [Header("旧版巡航设置 (保留原汁原味)")]
    public float orbitSpeed = 50f;
    public float orbitNoiseStrength = 0.5f; // 噪声强度
    public float orbitNoiseSpeed = 0.2f;    // 噪声速度
    public float followDistance = 5f;       // 跟随玩家的距离

    // 内部变量
    private Transform currentTarget;
    private WeaponPart myWeaponPart;
    private float flightAltitude;
    private Transform ownerTransform;

    // 巡航专用变量
    private float orbitAngle = 0f;
    private float noiseSeed;

    // 战斗专用变量
    private int currentAmmo;
    private bool isFiringBarrage = false;

    public void Initialize(WeaponStatBlock stats, float duration, Transform owner, int damage, float fireRateMult)
    {
        myWeaponPart = GetComponent<WeaponPart>();
        if (myWeaponPart != null) myWeaponPart.StatBlock = stats;

        this.ownerTransform = owner;
        this.flightAltitude = transform.position.y;

        // 恢复随机种子，保证每架飞机飞行轨迹不同
        this.orbitAngle = Random.Range(0f, 360f);
        this.noiseSeed = Random.Range(0f, 100f);

        currentAmmo = maxAmmo; // 初始满弹

        if (duration > 0) Destroy(gameObject, duration);
    }

    void Update()
    {
        // 状态机分流
        switch (currentState)
        {
            case DroneState.Reloading:
                // 换弹时继续巡航
                OrbitOwner();
                break;

            case DroneState.Attacking:
                AttackTarget();
                break;

            case DroneState.Orbiting:
                FindTarget();
                OrbitOwner();
                break;
        }
    }

    void FindTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRange, enemyLayer);
        if (enemies.Length > 0)
        {
            float minDist = float.MaxValue;
            Transform best = null;
            foreach (var e in enemies)
            {
                // 简单的找最近
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < minDist) { minDist = d; best = e.transform; }
            }
            currentTarget = best;
            if (currentTarget != null) currentState = DroneState.Attacking;
        }
    }

    // --- 【恢复】旧版巡航逻辑 ---
    void OrbitOwner()
    {
        if (ownerTransform == null) return;

        // 1. 更新角度
        orbitAngle += orbitSpeed * Time.deltaTime;

        // 2. 柏林噪声计算偏移 (丝滑的关键)
        float noise = (Mathf.PerlinNoise(noiseSeed, Time.time * orbitNoiseSpeed) - 0.5f) * 2f;
        float currentFollowDist = followDistance + noise * orbitNoiseStrength;

        // 3. 计算目标位置
        float offsetX = Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * currentFollowDist;
        float offsetZ = Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * currentFollowDist;

        Vector3 orbitPos = ownerTransform.position + new Vector3(offsetX, 0, offsetZ);
        orbitPos.y = this.flightAltitude; // 保持原本的生成高度

        // 4. 移动
        // 使用 Slerp 使得转身平滑，不会横着飞
        Vector3 moveDir = (orbitPos - transform.position).normalized;
        transform.position = Vector3.Lerp(transform.position, orbitPos, moveSpeed * Time.deltaTime);

        // 5. 朝向移动方向
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion lookRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
        }
    }

    void AttackTarget()
    {
        if (currentTarget == null)
        {
            currentState = DroneState.Orbiting;
            return;
        }

        // --- 移动逻辑：既要攻击，又要保持旧版的手感 ---
        // 我们不完全覆盖旧版逻辑，而是让它尝试向目标外围移动

        Vector3 enemyPos = currentTarget.position;
        Vector3 dirToEnemy = (enemyPos - transform.position).normalized;

        // 目标悬停点：敌人反方向 idealDistance 处
        Vector3 hoverPos = enemyPos - (dirToEnemy * idealDistance);
        // 高度维持
        hoverPos.y = this.flightAltitude;

        // 移动
        transform.position = Vector3.Lerp(transform.position, hoverPos, moveSpeed * 0.5f * Time.deltaTime); // 稍微慢一点，稳一点

        // 转向：始终盯着敌人
        Quaternion lookRot = Quaternion.LookRotation(dirToEnemy);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);

        // --- 开火逻辑 ---
        if (!isFiringBarrage && currentAmmo > 0)
        {
            StartCoroutine(FireBarrageRoutine());
        }
    }

    IEnumerator FireBarrageRoutine()
    {
        isFiringBarrage = true;
        int shotsToFire = currentAmmo;

        // --- 核心优化：强制重置 WeaponPart 的冷却 (如果能访问的话) ---
        // 但最稳妥的还是你在 Inspector 里把 Fire Rate 设为 0.05

        for (int i = 0; i < shotsToFire; i++)
        {
            // 如果目标没了，且没有上一个目标的残留位置，就停止
            if (currentTarget == null) break;

            // 1. 获取基础方向
            Vector3 targetPoint = currentTarget.position;
            Transform aimPoint = currentTarget.Find("AimTargetPoint");
            if (aimPoint != null) targetPoint = aimPoint.position;

            Vector3 baseDir = (targetPoint - transform.position).normalized;

            // 2. 【核心修改】计算特定的钳形散射
            // 我们不希望随机乱射，而是希望向“左右两侧”发射，并且稍微“向上”抬起，绝不向下

            // Y轴 (左右)：大幅度随机，实现“向两侧发射”
            // 比如 -80度 到 +80度，子弹会横着飞出去
            float ySpread = Random.Range(-spreadAngle, spreadAngle);

            // X轴 (上下)：限制只许往上飘，不许往下打
            // 在 Unity 中，-X 是抬头 (Look Up)，+X 是低头。我们取 -30 到 -5 度。
            // 这样子弹一定会有一个向上的初速度，防止打地
            float xSpread = Random.Range(-30f, -5f);

            // 组合旋转
            Quaternion spreadRot = Quaternion.Euler(xSpread, ySpread, 0);

            // 应用旋转
            Vector3 finalDir = (Quaternion.LookRotation(baseDir) * spreadRot) * Vector3.forward;

            // 3. 开火
            if (myWeaponPart != null)
            {
                // 这里的 Fire 可能会被 WeaponPart 内部的 cooldown 拒绝
                // 所以请务必确保 Weapon_DroneGun 的 FireRate 设置为了 0.05
                myWeaponPart.Fire(finalDir);
            }
            currentAmmo--;

            yield return new WaitForSeconds(burstInterval);
        }

        isFiringBarrage = false;

        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    IEnumerator ReloadRoutine()
    {
        currentState = DroneState.Reloading;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        currentState = DroneState.Orbiting; // 换弹完回巡航找人
    }
}