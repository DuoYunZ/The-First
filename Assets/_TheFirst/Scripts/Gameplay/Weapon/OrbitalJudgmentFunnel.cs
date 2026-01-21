using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class OrbitalJudgmentFunnel : MonoBehaviour
{
    private enum State { Orbiting, Attacking, Cooldown }
    private State currentState = State.Orbiting;

    [Header("浮游炮姿态")]
    public float orbitDistance = 4.0f;
    public float orbitHeight = 5.0f;      // 飞高一点，像神之光环
    public float orbitSpeed = 45f;        // 转慢一点，显威严
    public float smoothTime = 2.0f;

    [Header("阶段一：星陨导弹")]
    public GameObject missilePrefab;
    public int missileCount = 4;
    public float missileInterval = 0.15f;

    [Header("阶段二：天基打击")]
    public float laserDuration = 2.0f;     // 打击持续时间
    public float laserWidth = 1.2f;        // 光柱非常粗
    public Color laserColor = new Color(1f, 0.8f, 0f); // 金色
    public GameObject chargeVfxPrefab;     // 浮游炮口的引导特效
    public GameObject orbitalBeamVfxPrefab; // 【新增】天降光柱的底部冲击特效
    public GameObject lockOnPrefab;        // 锁定圈

    [Header("战斗参数")]
    public float detectionRadius = 25f;
    public float cooldownTime = 2.0f;
    public LayerMask enemyLayer;
    public Transform firePoint;

    // 内部变量
    private Transform ownerPlayer;
    private WeaponPart ownerWeapon;
    private Transform currentTarget;
    private LineRenderer lineRenderer;
    private float orbitAngle;

    // 引用
    private GameObject currentLockOn;
    private LockOnEffect currentLockOnScript;
    private GameObject currentChargeVfx;

    // 伤害
    private int damage;
    private float tickRate;
    private float damageTickTimer;

    public void Initialize(WeaponPart weapon, int dmg, float tickRate, Transform owner, int index, int totalCount)
    {
        this.ownerWeapon = weapon;
        this.damage = dmg;
        this.tickRate = tickRate;
        this.ownerPlayer = owner;
        this.orbitAngle = (360f / totalCount) * index;
    }

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;

        // 设置光柱材质颜色
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
    }

    void Update()
    {
        if (ownerPlayer == null) return;

        switch (currentState)
        {
            case State.Orbiting:
                HandleOrbit();
                FindTarget();
                if (currentTarget != null) StartCoroutine(ComboAttackRoutine());
                break;

            case State.Attacking:
                // 攻击时，浮游炮稍微停顿或减速，保持高逼格
                HandleOrbit(0.2f);
                break;

            case State.Cooldown:
                HandleOrbit(1.5f); // 冷却时快速归位
                break;
        }
    }

    IEnumerator ComboAttackRoutine()
    {
        currentState = State.Attacking;

        // === 阶段 1: 星陨导弹 (修复版) ===
        ShowLockOn(true);

        for (int i = 0; i < missileCount; i++)
        {
            if (currentTarget == null) FindTarget();
            if (currentTarget != null) FireMissileFixed(); // 调用修复后的发射逻辑
            yield return new WaitForSeconds(missileInterval);
        }

        yield return new WaitForSeconds(0.3f);

        // === 阶段 2: 引导天罚 ===
        // 浮游炮亮起，表示正在向卫星发送坐标
        if (chargeVfxPrefab != null && firePoint != null)
        {
            currentChargeVfx = Instantiate(chargeVfxPrefab, firePoint.position, Quaternion.identity, firePoint);
        }
        yield return new WaitForSeconds(0.4f); // 短暂延迟

        // === 阶段 3: 天降光柱 (Vertical Beam) ===
        if (currentChargeVfx != null) Destroy(currentChargeVfx);

        float laserTimer = laserDuration;
        lineRenderer.enabled = true;

        // 生成地面的持续冲击特效
        GameObject groundImpact = null;
        if (orbitalBeamVfxPrefab != null && currentTarget != null)
        {
            groundImpact = Instantiate(orbitalBeamVfxPrefab, currentTarget.position, Quaternion.identity);
        }

        while (laserTimer > 0)
        {
            if (currentTarget == null) FindTarget();

            // 核心修改：光柱不再连接浮游炮，而是从天而降
            UpdateOrbitalBeam(groundImpact);

            laserTimer -= Time.deltaTime;
            yield return null;
        }

        // === 结束 ===
        lineRenderer.enabled = false;
        ShowLockOn(false);
        if (groundImpact != null) Destroy(groundImpact);

        currentState = State.Cooldown;
        yield return new WaitForSeconds(cooldownTime);
        currentState = State.Orbiting;
    }

    // --- 【修复】导弹发射逻辑 ---
    void FireMissileFixed()
    {
        if (missilePrefab == null || firePoint == null) return;

        // 1. 生成
        GameObject missileObj = Instantiate(missilePrefab, firePoint.position, firePoint.rotation);

        // 2. 获取核心脚本
        Projectile proj = missileObj.GetComponent<Projectile>();

        // 3. 【关键】手动初始化！模拟 WeaponPart 的行为
        if (proj != null)
        {
            // 如果你的导弹是用 Homing 模式
            // 我们手动调用 InitializeAsHoming，或者 InitializeAsStraight 然后由 HomingProjectile 接管
            // 这里假设你的导弹 Prefab 挂了 HomingProjectile 且 Projectile.mode 设为了 Homing

            // 赋予伤害和主人
            proj.damage = Mathf.RoundToInt(damage * 0.5f); // 导弹伤害减半
            proj.owner = ownerWeapon.gameObject;

            // 如果 Projectile 脚本里有 InitializeAsHoming，最好调用它
            // 这里我们手动模拟初始化参数，防止它发呆
            proj.speed = 25f;
            // 确保它属于玩家子弹
            proj.MarkAsPlayerProjectile();

            // 强制激活 Rigidbody
            Rigidbody rb = missileObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                // 给一个向上的初速度，制造“发射”感
                Vector3 launchDir = (firePoint.up + Random.insideUnitSphere * 0.2f).normalized;
                rb.velocity = launchDir * 10f;
            }
        }

        // 4. 处理 HomingProjectile 脚本
        HomingProjectile hp = missileObj.GetComponent<HomingProjectile>();
        if (hp != null)
        {
            // 现在的 HomingProjectile 会在 Start 里自动找目标
            // 但为了保险，我们把当前锁定的目标直接塞给它（如果你修改过代码支持外部赋值）
            // 如果没有外部赋值接口，它会在 Start 里的 FindNearestTarget 自动生效
            // 只要 Projectile.owner 设对了，它就不会打自己人
        }
    }

    // --- 【新】天降光柱逻辑 ---
    void UpdateOrbitalBeam(GameObject impactVfx)
    {
        // 目标点：敌人位置 (如果敌人死了，保持在最后的位置)
        Vector3 targetPos = transform.position + transform.forward * 10f;
        if (currentTarget != null) targetPos = currentTarget.position;
        else if (currentLockOn != null) targetPos = currentLockOn.transform.position;

        // 锁定圈跟随
        if (currentLockOnScript != null && currentTarget != null)
            currentLockOnScript.SetTarget(currentTarget);
        else if (currentLockOn != null)
            currentLockOn.transform.position = targetPos;

        // 特效跟随
        if (impactVfx != null) impactVfx.transform.position = targetPos;

        // 1. 绘制垂直光柱
        // 起点：云端 (目标上方 30米)
        Vector3 skyPos = targetPos + Vector3.up * 30f;
        // 终点：地底 (目标下方 2米，确保穿透)
        Vector3 groundPos = targetPos - Vector3.up * 2.0f;

        lineRenderer.SetPosition(0, skyPos);
        lineRenderer.SetPosition(1, groundPos);

        // 2. 造成伤害 (圆柱体判定)
        damageTickTimer += Time.deltaTime;
        if (damageTickTimer >= (1f / tickRate))
        {
            damageTickTimer = 0f;
            // 从天而降的判定
            RaycastHit[] hits = Physics.SphereCastAll(skyPos, laserWidth * 0.5f, Vector3.down, 40f, enemyLayer);

            foreach (var hit in hits)
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    h.TakeDamage(damage, hit.point, ownerWeapon.gameObject, AttackType.Standard);
                }
            }
        }
    }

    void HandleOrbit(float speedMultiplier = 1.0f)
    {
        orbitAngle += orbitSpeed * speedMultiplier * Time.deltaTime;
        float rad = orbitAngle * Mathf.Deg2Rad;

        // 椭圆轨迹，让它看起来更立体
        Vector3 offset = new Vector3(Mathf.Cos(rad) * orbitDistance, orbitHeight + Mathf.Sin(rad * 0.5f), Mathf.Sin(rad) * orbitDistance);
        Vector3 targetPos = ownerPlayer.position + offset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothTime);

        // 始终朝向外侧，或者朝向目标
        if (currentState == State.Attacking && currentTarget != null)
        {
            // 攻击时，炮口指向天空（召唤姿态）或者指向敌人
            // 这里设为指向敌人，但光柱是垂直的，形成反差
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 10f);
        }
        else
        {
            // 巡航时自转
            transform.Rotate(Vector3.up, 30f * Time.deltaTime);
        }
    }

    void FindTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        float minDist = float.MaxValue;
        Transform best = null;
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                float d = Vector3.Distance(transform.position, col.transform.position);
                if (d < minDist) { minDist = d; best = col.transform; }
            }
        }
        currentTarget = best;
    }

    void ShowLockOn(bool show)
    {
        if (show)
        {
            if (currentLockOn == null && lockOnPrefab != null)
            {
                currentLockOn = Instantiate(lockOnPrefab);
                currentLockOnScript = currentLockOn.GetComponent<LockOnEffect>();
            }
            if (currentLockOn != null)
            {
                currentLockOn.SetActive(true);
                if (currentTarget != null && currentLockOnScript != null) currentLockOnScript.SetTarget(currentTarget);
            }
        }
        else
        {
            if (currentLockOn != null) currentLockOn.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (currentLockOn != null) Destroy(currentLockOn);
        if (currentChargeVfx != null) Destroy(currentChargeVfx);
    }
}