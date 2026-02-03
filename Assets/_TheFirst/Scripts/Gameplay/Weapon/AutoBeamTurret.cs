using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class AutoBeamTurret : MonoBehaviour
{
    private enum TurretState { Idle, Charging, Firing, Cooldown }
    private TurretState currentState = TurretState.Idle;

    [Header("移动设置")]
    public float moveSpeed = 4.0f;
    public float followDistance = 3.0f;

    [Header("炮塔结构")]
    public Transform rotationPivot; // 炮塔旋转轴
    public Transform firePoint;     // 枪口发射点

    [Header("视觉特效 (VFX)")]
    [Tooltip("枪口蓄力特效")]
    public GameObject chargeVfxPrefab;
    [Tooltip("命中爆裂特效")]
    public GameObject impactVfxPrefab;
    [Tooltip("锁定瞄准圈 UI Prefab (挂载 LockOnEffect 脚本)")]
    public GameObject lockOnPrefab;

    public float fireWidth = 0.6f;
    public Color fireColor = Color.cyan;

    [Header("战斗参数")]
    public float detectionRadius = 15f;
    public float rotationSpeed = 20f;
    public LayerMask enemyLayer;
    public float chargeTime = 1.0f;
    public float fireDuration = 2.0f;
    public float cooldownTime = 1.5f;

    // 内部变量
    private LineRenderer lineRenderer;
    private WeaponPart ownerWeapon;
    private Transform ownerPlayer;
    private Transform currentTarget;
    private float stateTimer;
    private float damageTickTimer;

    // --- 伤害相关 ---
    private int damagePerTick;
    private float tickRate;
    private float critRate;          // 【新增】暴击率
    private float critDamageMult;    // 【新增】暴击倍率

    // 引用变量
    private GameObject currentChargeVfx;
    private GameObject currentLockOnInstance;
    private LockOnEffect currentLockOnScript;

    // 【修改】Initialize 增加暴击参数
    public void Initialize(WeaponPart weapon, int damage, float tickRate, Transform owner, float cRate, float cDmgMult)
    {
        this.ownerWeapon = weapon;
        this.damagePerTick = damage;
        this.tickRate = tickRate;
        this.ownerPlayer = owner;

        // 记录暴击属性
        this.critRate = cRate;
        this.critDamageMult = cDmgMult;

        if (weapon.StatBlock.beamDuration > 0) this.fireDuration = weapon.StatBlock.beamDuration;
    }

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
    }

    void Update()
    {
        if (ownerWeapon == null || ownerWeapon.gameObject == null)
        {
            Destroy(gameObject);
            return;
        }
        HandleMovement();

        switch (currentState)
        {
            case TurretState.Idle: HandleIdle(); break;
            case TurretState.Charging: HandleCharging(); break;
            case TurretState.Firing: HandleFiring(); break;
            case TurretState.Cooldown: HandleCooldown(); break;
        }
    }

    // --- 状态机逻辑 ---

    void HandleIdle()
    {
        FindTarget();
        if (currentTarget != null) StartCoroutine(EnterChargeState());
    }

    IEnumerator EnterChargeState()
    {
        currentState = TurretState.Charging;
        stateTimer = chargeTime;
        lineRenderer.enabled = false;

        if (chargeVfxPrefab != null && firePoint != null)
        {
            if (currentChargeVfx == null)
                currentChargeVfx = Instantiate(chargeVfxPrefab, firePoint.position, firePoint.rotation, firePoint);
            currentChargeVfx.SetActive(true);
        }
        ShowLockOn(true);
        yield return null;
    }

    void HandleCharging()
    {
        if (IsTargetValid())
        {
            TrackTarget();
        }
        else
        {
            ShowLockOn(false);
            CleanupVFX();
            FindTarget();
            if (currentTarget == null) { ResetToIdle(); return; }
            else { ShowLockOn(true); }
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) StartCoroutine(EnterFireState());
    }

    IEnumerator EnterFireState()
    {
        currentState = TurretState.Firing;
        stateTimer = fireDuration;
        damageTickTimer = 0f;

        lineRenderer.enabled = true;
        lineRenderer.startWidth = fireWidth;
        lineRenderer.endWidth = fireWidth;
        lineRenderer.startColor = fireColor;
        lineRenderer.endColor = fireColor;

        if (currentChargeVfx != null) currentChargeVfx.SetActive(false);
        ShowLockOn(false);
        yield return null;
    }

    void HandleFiring()
    {
        if (!IsTargetValid())
        {
            FindTarget();
            if (currentTarget == null) { EnterCooldownState(); return; }
        }

        TrackTarget();
        DrawLaserAndDamage();

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) EnterCooldownState();
    }

    void EnterCooldownState()
    {
        currentState = TurretState.Cooldown;
        stateTimer = cooldownTime;
        lineRenderer.enabled = false;
        CleanupVFX();
        ShowLockOn(false);
    }

    void HandleCooldown()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) currentState = TurretState.Idle;
    }

    // --- 伤害与特效逻辑 ---

    void DrawLaserAndDamage()
    {
        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        Vector3 targetHitPos = GetTargetHitPos();

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, targetHitPos);

        damageTickTimer += Time.deltaTime;
        if (damageTickTimer >= (1f / tickRate))
        {
            damageTickTimer = 0f;

            Vector3 dir = (targetHitPos - startPos).normalized;
            float dist = Vector3.Distance(startPos, targetHitPos);

            // 贯穿伤害
            RaycastHit[] hits = Physics.SphereCastAll(startPos, fireWidth * 0.5f, dir, dist, enemyLayer);

            foreach (var hit in hits)
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    // --- 【核心修改】暴击判定 ---
                    bool isCrit = Random.value <= critRate;
                    int finalDamage = damagePerTick;

                    if (isCrit)
                    {
                        finalDamage = Mathf.RoundToInt(damagePerTick * critDamageMult);
                    }

                    // 传递 isCrit 参数用于飘字
                    h.TakeDamage(finalDamage, hit.point, ownerWeapon.gameObject, AttackType.Standard, null, null, "", isCrit);

                    if (impactVfxPrefab != null)
                    {
                        GameObject hitVfx = Instantiate(impactVfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                        hitVfx.transform.LookAt(startPos);
                        Destroy(hitVfx, 1.0f);
                    }
                }
            }
        }
    }

    void ShowLockOn(bool show)
    {
        if (show)
        {
            if (currentTarget == null) return;
            if (currentLockOnInstance == null && lockOnPrefab != null)
            {
                currentLockOnInstance = Instantiate(lockOnPrefab);
                currentLockOnScript = currentLockOnInstance.GetComponent<LockOnEffect>();
            }
            if (currentLockOnInstance != null)
            {
                currentLockOnInstance.SetActive(true);
                if (currentLockOnScript != null) currentLockOnScript.SetTarget(currentTarget);
                else currentLockOnInstance.transform.position = currentTarget.position + Vector3.up;
            }
        }
        else
        {
            if (currentLockOnInstance != null) currentLockOnInstance.SetActive(false);
        }
    }

    void HandleMovement()
    {
        if (ownerPlayer == null) return;
        bool canMove = (currentState != TurretState.Firing);
        float distToPlayer = Vector3.Distance(transform.position, ownerPlayer.position);

        if (canMove && distToPlayer > followDistance)
        {
            Vector3 dir = (ownerPlayer.position - transform.position).normalized;
            dir.y = 0;
            transform.position += dir * moveSpeed * Time.deltaTime;
            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 5f * Time.deltaTime);
            }
        }
    }

    void TrackTarget()
    {
        if (currentTarget == null) return;
        Vector3 targetPos = GetTargetHitPos();
        Transform tToRotate = rotationPivot != null ? rotationPivot : transform;
        Vector3 dir = (targetPos - tToRotate.position).normalized;
        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            tToRotate.rotation = Quaternion.RotateTowards(tToRotate.rotation, lookRot, rotationSpeed * 100f * Time.deltaTime);
        }
    }

    Vector3 GetTargetHitPos()
    {
        if (currentTarget == null) return transform.position + transform.forward * 10f;
        Transform aimPoint = currentTarget.Find("AimTargetPoint");
        return (aimPoint != null) ? aimPoint.position : currentTarget.position;
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

    bool IsTargetValid()
    {
        if (currentTarget == null) return false;
        if (!currentTarget.gameObject.activeInHierarchy) return false;
        Health h = currentTarget.GetComponentInParent<Health>();
        return h != null && !h.IsDead;
    }

    void CleanupVFX()
    {
        if (currentChargeVfx != null) currentChargeVfx.SetActive(false);
        ShowLockOn(false);
    }

    void ResetToIdle() { currentState = TurretState.Idle; lineRenderer.enabled = false; CleanupVFX(); }
    void OnDisable() { CleanupVFX(); }
    void OnDestroy() { if (currentLockOnInstance != null) Destroy(currentLockOnInstance); }
}