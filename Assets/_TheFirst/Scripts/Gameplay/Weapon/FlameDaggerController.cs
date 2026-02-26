using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 火焰飞刀控制器 - 飞行过程中生成地面火焰
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlameDaggerController : MonoBehaviour
{
    [Header("火焰生成设置")]
    [Tooltip("地面火焰预制件")]
    public GameObject groundHazardPrefab;
    [Tooltip("生成火焰的间隔")]
    public float flameSpawnInterval = 0.5f;
    [Tooltip("火焰伤害")]
    public int flameDamage = 5;
    [Tooltip("火焰持续时间")]
    public float flameDuration = 3f;
    [Tooltip("地面检测Layer")]
    public LayerMask groundLayer = -1; // 默认全部Layer
    
    [Header("飞行设置")]
    public float smoothTime = 0.5f;
    public float maxSpeed = 30f;
    public float orbitRadius = 4f;
    public float orbitFrequency = 1.2f;

    [Header("索敌设置")]
    public float searchInterval = 2.0f;
    public float lockDuration = 3.0f;
    public float searchRange = 15f;
    public float focusSmoothTime = 0.8f;

    [Header("伤害设置")]
    public float damageInterval = 0.5f;
    public LayerMask enemyLayer;

    // 内部状态
    public WeaponPart sourceWeapon;
    private Transform ownerTransform;
    private int damageAmount;
    private float knockbackForce;

    // 运动学变量
    private Vector3 currentVelocity;
    private Vector3 currentFocalPoint;
    private Vector3 focalPointVelocity;
    private float timeOffset;

    // 索敌状态
    private Transform lockedTarget;
    private float searchTimer = 0f;
    private float lockTimer = 0f;

    // 火焰计时
    private float flameTimer = 0f;

    // 伤害记录
    private Dictionary<GameObject, float> hitCooldowns = new Dictionary<GameObject, float>();

    public void Initialize(WeaponStatBlock stats, Transform owner, int damage, float knockback, WeaponPart source)
    {
        this.sourceWeapon = source;
        this.ownerTransform = owner;
        this.damageAmount = damage;
        this.knockbackForce = knockback;
        
        this.timeOffset = Random.Range(0f, 100f);
        this.currentFocalPoint = owner.position;

        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (ownerTransform == null) return;

        CleanupCooldowns();
        UpdateTargetingLogic();
        UpdateFlameSpawn(); // 火焰生成逻辑

        // 1. 计算当前的理想中心点 (在玩家和锁定目标之间平滑切换)
        Vector3 targetFocalPos = ownerTransform.position;
        if (lockedTarget != null && lockedTarget.gameObject.activeInHierarchy)
        {
            // 如果有锁定的目标，焦点设为目标位置 (稍微抬高一点)
            targetFocalPos = lockedTarget.position + Vector3.up * 1.0f;
        }
        else
        {
            // 没有目标，焦点回玩家头顶
            targetFocalPos = ownerTransform.position + Vector3.up * 1.5f;
            lockedTarget = null; // 安全清理
        }

        currentFocalPoint = Vector3.SmoothDamp(currentFocalPoint, targetFocalPos, ref focalPointVelocity, focusSmoothTime, maxSpeed * 2f);

        Vector3 finalPos = CalculateOrbitPosition(currentFocalPoint);
        float minHeight = ownerTransform.position.y + 0.5f;
        if (finalPos.y < minHeight) finalPos.y = minHeight;

        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref currentVelocity, smoothTime, maxSpeed);
        RotateModel();
    }

    /// <summary>
    /// 火焰生成逻辑 - 定期在飞刀下方生成火焰
    /// </summary>
    void UpdateFlameSpawn()
    {
        if (groundHazardPrefab == null) return;
        
        flameTimer -= Time.deltaTime;
        if (flameTimer <= 0f)
        {
            flameTimer = flameSpawnInterval;
            SpawnFlameAtPosition();
        }
    }

    void SpawnFlameAtPosition()
    {
        // 向下射线检测地面
        RaycastHit hit;
        Vector3 origin = transform.position;
        
        // 使用groundLayer进行检测，如果没设置(-1)则检测所有Layer
        bool didHit = groundLayer == -1 
            ? Physics.Raycast(origin, Vector3.down, out hit, 10f)
            : Physics.Raycast(origin, Vector3.down, out hit, 10f, groundLayer);
        
        if (didHit)
        {
            Vector3 spawnPos = hit.point + Vector3.up * 0.1f;
            GameObject hazard = Instantiate(groundHazardPrefab, spawnPos, Quaternion.identity);
            
            GroundHazard gh = hazard.GetComponent<GroundHazard>();
            if (gh != null)
            {
                string weaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) 
                    ? sourceWeapon.StatBlock.weaponName : "FlameDagger";
                gh.Initialize(flameDamage, flameDuration, weaponName, ownerTransform.gameObject);
            }
        }
    }

    void UpdateTargetingLogic()
    {
        if (lockedTarget != null)
        {
            lockTimer -= Time.deltaTime;
            
            bool isTargetInvalid = false;
            if (!lockedTarget.gameObject.activeInHierarchy) isTargetInvalid = true;
            else
            {
                Health h = lockedTarget.GetComponent<Health>();
                if (h != null && h.IsDead) isTargetInvalid = true;
            }

            if (lockTimer <= 0f || isTargetInvalid)
            {
                lockedTarget = null;
                searchTimer = 0.5f;
            }
        }
        else
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
            {
                searchTimer = searchInterval;
                FindNewTarget();
            }
        }
    }

    void FindNewTarget()
    {
        Collider[] hits = Physics.OverlapSphere(ownerTransform.position, searchRange, enemyLayer);
        float minDist = float.MaxValue;
        Transform best = null;

        foreach (var hit in hits)
        {
            Health h = hit.GetComponent<Health>();
            if (h != null && !h.IsDead)
            {
                float d = Vector3.Distance(ownerTransform.position, hit.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    best = hit.transform;
                }
            }
        }

        if (best != null)
        {
            lockedTarget = best;
            lockTimer = lockDuration;
        }
    }

    Vector3 CalculateOrbitPosition(Vector3 centerPos)
    {
        float t = Time.time * orbitFrequency + timeOffset;
        float x = Mathf.Sin(t) * orbitRadius;
        float z = Mathf.Sin(t * 2f) * (orbitRadius * 0.6f);
        float y = Mathf.Sin(t * 1.51f) * 1.0f;

        return centerPos + new Vector3(x, y, z);
    }

    void RotateModel()
    {
        if (currentVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(currentVelocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 15f * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return;
        if (hitCooldowns.ContainsKey(other.gameObject)) return;

        Health h = other.GetComponent<Health>();
        if (h == null) h = other.GetComponentInParent<Health>();

        if (h != null && !h.IsDead)
        {
            h.TakeDamage(damageAmount, transform.position, this.gameObject);
            hitCooldowns[other.gameObject] = Time.time + damageInterval;

            EnemyAI ai = h.GetComponent<EnemyAI>();
            if (ai != null)
            {
                Vector3 knockbackDir = currentVelocity.normalized;
                if (knockbackDir == Vector3.zero) knockbackDir = transform.forward;
                ai.ApplyKnockback(knockbackDir, knockbackForce);
            }
        }
    }

    void CleanupCooldowns()
    {
        if (hitCooldowns.Count == 0) return;

        List<GameObject> toRemove = new List<GameObject>();
        foreach (var kvp in hitCooldowns)
        {
            if (Time.time >= kvp.Value || kvp.Key == null || !kvp.Key.activeInHierarchy)
                toRemove.Add(kvp.Key);
        }
        foreach (var k in toRemove) hitCooldowns.Remove(k);
    }
}
