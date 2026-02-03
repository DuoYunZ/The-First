using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class FlyingDaggerController : MonoBehaviour
{
    [Header("灵动飞行设置")]
    [Tooltip("飞行的阻尼感，越小越灵敏，越大越惯性")]
    public float smoothTime = 0.5f; 
    [Tooltip("最大飞行速度")]
    public float maxSpeed = 30f;
    [Tooltip("巡航半径")]
    public float orbitRadius = 4f;
    [Tooltip("巡航时的8字形频率")]
    public float orbitFrequency = 1.2f;

    [Header("智能索敌设置")]
    [Tooltip("每隔多久尝试寻找一次敌人")]
    public float searchInterval = 2.0f;
    [Tooltip("锁定敌人的持续时间，期间飞刀会围绕敌人飞行")]
    public float lockDuration = 3.0f;
    [Tooltip("索敌范围")]
    public float searchRange = 15f;
    [Tooltip("焦点切换的平滑度")]
    public float focusSmoothTime = 0.8f;

    [Header("伤害设置")]
    public float damageInterval = 0.5f;
    public LayerMask enemyLayer;

    // --- 内部状态 ---
    public WeaponPart sourceWeapon;
    private Transform ownerTransform;
    private int damageAmount;
    private float knockbackForce;

    // 运动学变量
    private Vector3 currentVelocity; // 用于位置 SmoothDamp
    private Vector3 currentFocalPoint;   // 当前的巡航中心点
    private Vector3 focalPointVelocity;  // 用于焦点移动的 SmoothDamp
    private float timeOffset; 
    
    // 索敌状态
    private Transform lockedTarget;
    private float searchTimer = 0f;
    private float lockTimer = 0f;

    // 伤害记录
    private Dictionary<GameObject, float> hitCooldowns = new Dictionary<GameObject, float>();

    public void Initialize(WeaponStatBlock stats, Transform owner, int damage, float knockback, WeaponPart source)
    {
        this.sourceWeapon = source;
        this.ownerTransform = owner;
        this.damageAmount = damage;
        this.knockbackForce = knockback;
        
        this.timeOffset = Random.Range(0f, 100f);
        this.currentFocalPoint = owner.position; // 初始焦点在玩家

        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (ownerTransform == null) return;

        CleanupCooldowns();
        UpdateTargetingLogic();

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

        // 焦点的平滑移动 (让飞刀看起来是慢慢飘向敌人的，而不是瞬移过去)
        currentFocalPoint = Vector3.SmoothDamp(currentFocalPoint, targetFocalPos, ref focalPointVelocity, focusSmoothTime, maxSpeed * 2f);

        // 2. 叠加 8字形 轨迹
        Vector3 finalPos = CalculateOrbitPosition(currentFocalPoint);

        // 防穿地保护：确保不低以 owner 的脚底 + 0.5米
        float minHeight = ownerTransform.position.y + 0.5f;
        if (finalPos.y < minHeight) finalPos.y = minHeight;

        // 3. 飞刀本体移动
        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref currentVelocity, smoothTime, maxSpeed);

        RotateModel();
    }

    void UpdateTargetingLogic()
    {
        // 如果正锁定这着敌人
        if (lockedTarget != null)
        {
            lockTimer -= Time.deltaTime;
            
            // 检查目标是否失效 (死亡或消失)
            bool isTargetInvalid = false;
            if (!lockedTarget.gameObject.activeInHierarchy) isTargetInvalid = true;
            else 
            {
               Health h = lockedTarget.GetComponent<Health>();
               // 可以选择：如果打死了就立刻换目标，或者继续鞭尸一会儿 (保持运动连续性)
               // 这里选择：打死了就立刻切回玩家，等待下一次搜索
               if (h != null && h.IsDead) isTargetInvalid = true;
            }

            // 锁定时间到 或 目标失效 -> 放弃锁定
            if (lockTimer <= 0f || isTargetInvalid)
            {
                lockedTarget = null;
                // 重置搜索冷却，稍微等一会再找下一个，给人喘息机会
                searchTimer = 0.5f; 
            }
        }
        else
        {
            // 没有目标，进行搜索
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
            {
                searchTimer = searchInterval; // 重置计时器
                FindNewTarget();
            }
        }
    }

    void FindNewTarget()
    {
        Collider[] hits = Physics.OverlapSphere(ownerTransform.position, searchRange, enemyLayer);
        // 简单找个最近的
        float minDist = float.MaxValue;
        Transform best = null;
        
        foreach(var hit in hits)
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
            lockTimer = lockDuration; // 锁定它 N 秒
        }
    }

    // 基于给定的中心点计算8字形
    Vector3 CalculateOrbitPosition(Vector3 centerPos)
    {
        float t = Time.time * orbitFrequency + timeOffset;
        
        float x = Mathf.Sin(t) * orbitRadius;
        float z = Mathf.Sin(t * 2f) * (orbitRadius * 0.6f); 
        float y = Mathf.Sin(t * 1.51f) * 1.0f; // 稍微改点频率防止重合

        Vector3 offset = new Vector3(x, y, z);
        return centerPos + offset;
    }

    void RotateModel()
    {
        // 如果有速度，朝向速度方向
        if (currentVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(currentVelocity.normalized);
            // 带有阻尼的旋转
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 15f * Time.deltaTime);
        }
    }

    // 纯粹的碰触伤害，不影响任何移动状态
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return;
        if (hitCooldowns.ContainsKey(other.gameObject)) return; // 冷却中

        Health h = other.GetComponent<Health>();
        if (h == null) h = other.GetComponentInParent<Health>();

        if (h != null && !h.IsDead)
        {
            // 造成伤害
            h.TakeDamage(damageAmount, transform.position, this.gameObject);
            
            // 记录冷却
            hitCooldowns[other.gameObject] = Time.time + damageInterval;

            // 击退 (按照飞刀当前的飞行方向击退)
            EnemyAI ai = h.GetComponent<EnemyAI>();
            if (ai != null) 
            {
                Vector3 knockbackDir = currentVelocity.normalized;
                if(knockbackDir == Vector3.zero) knockbackDir = transform.forward;
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
