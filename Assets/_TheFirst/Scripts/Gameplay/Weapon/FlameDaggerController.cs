using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 灵能飞刀控制器 - 飞行过程中生成地面火焰，支持多种技能升级
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
    public LayerMask groundLayer = -1;
    
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

    [Header("爆破特效")]
    [Tooltip("连锁灵刃爆破特效")]
    public GameObject explosionVfxPrefab;

    [Header("分身设置")]
    [Tooltip("分身飞刀材质（半透明/发光等，留空则不改变材质）")]
    public Material cloneMaterial;
    [Tooltip("分身生成点（留空则在飞刀当前位置生成）")]
    public Transform cloneSpawnPoint;

    // 内部状态
    public WeaponPart sourceWeapon;
    private Transform ownerTransform;
    private int damageAmount;
    private float knockbackForce;
    private bool initialized = false;

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

    // 伤害记录（使用 Collider 的 instanceID 作为 key，避免对象销毁问题）
    private Dictionary<int, float> hitCooldowns = new Dictionary<int, float>();

    // === 技能升级后的最终值 ===
    private float effectiveOrbitRadius;
    private float effectiveOrbitFrequency;
    private float effectiveDamageInterval;
    private float effectiveSearchRange;
    private float effectiveLockDuration;
    private bool isClone = false;

    // 大招状态
    [HideInInspector] public bool isUltimateActive = false;
    [HideInInspector] public bool isFlameUltimateActive = false;

    // 原始缩放，用于大招恢复
    private Vector3 originalScale;

    // 大招特效实例（挂载在飞刀上）
    private GameObject ultimateVfxInstance;

    public void Initialize(WeaponStatBlock stats, Transform owner, int damage, float knockback, WeaponPart source)
    {
        this.sourceWeapon = source;
        this.ownerTransform = owner;
        this.damageAmount = damage;
        this.knockbackForce = knockback;

        // 只在第一次初始化时设置运动状态（避免刷新属性时飞刀位置跳变）
        if (!initialized)
        {
            this.timeOffset = Random.Range(0f, 100f);
            this.currentFocalPoint = owner.position;
            GetComponent<Collider>().isTrigger = true;
            originalScale = transform.localScale;
            initialized = true;
        }

        // 应用技能升级
        ApplyUpgrades();
    }

    /// <summary>
    /// 应用 WeaponPart 上的升级字段到飞刀实际属性
    /// </summary>
    void ApplyUpgrades()
    {
        if (sourceWeapon == null) return;

        int baseDmg = damageAmount; // 记录传入的基础伤害

        // 从配置基础值开始（每次都从原始值重新计算，避免反复叠加）
        effectiveOrbitRadius = orbitRadius;
        effectiveOrbitFrequency = orbitFrequency;
        effectiveDamageInterval = damageInterval;
        effectiveSearchRange = searchRange;
        effectiveLockDuration = lockDuration;

        // 烈焰增幅：伤害加成 + 速度惩罚
        if (sourceWeapon.daggerDamageBoost > 0)
        {
            damageAmount = Mathf.RoundToInt(damageAmount * (1f + sourceWeapon.daggerDamageBoost / 100f));
            effectiveOrbitFrequency *= (1f - sourceWeapon.daggerSpeedPenalty / 100f);
        }

        // 多重飞刀：伤害惩罚
        if (sourceWeapon.daggerCountDmgPenalty > 0)
        {
            damageAmount = Mathf.RoundToInt(damageAmount * (1f - sourceWeapon.daggerCountDmgPenalty / 100f));
        }

        // 焰舞加速：速度倍率 + 间隔缩减（提升攻击欲望）
        if (sourceWeapon.daggerSpeedBoost > 0)
        {
            effectiveOrbitFrequency *= sourceWeapon.daggerSpeedBoost;
            effectiveDamageInterval *= (1f - sourceWeapon.daggerIntervalReduction / 100f);
        }

        // 锁魂追击：索敌+50%，锁定+2秒，半径-50%
        if (sourceWeapon.daggerHomingUpgrade)
        {
            effectiveSearchRange *= 1.5f;
            effectiveLockDuration += 2f;
            effectiveOrbitRadius *= 0.5f;
        }

        // 刃影分身：不再减少半径
        // （分身效果仅触发分身生成，不影响本体环绕行为）

        // === 调试日志 ===
    }

    void Update()
    {
        if (ownerTransform == null) return;

        CleanupCooldowns();
        UpdateTargetingLogic();
        UpdateFlameSpawn();

        // 计算当前的理想中心点
        Vector3 targetFocalPos;
        if (lockedTarget != null && lockedTarget.gameObject.activeInHierarchy)
        {
            targetFocalPos = lockedTarget.position + Vector3.up * 1.0f;
        }
        else
        {
            targetFocalPos = ownerTransform.position + Vector3.up * 1.5f;
            lockedTarget = null;
        }

        currentFocalPoint = Vector3.SmoothDamp(currentFocalPoint, targetFocalPos, ref focalPointVelocity, focusSmoothTime, maxSpeed * 2f);

        Vector3 finalPos = CalculateOrbitPosition(currentFocalPoint);
        float minHeight = ownerTransform.position.y + 0.5f;
        if (finalPos.y < minHeight) finalPos.y = minHeight;

        // 大招期间降低阻尼、提高最大速度，使速度变化更明显
        float currentSmoothTime = smoothTime;
        float currentMaxSpeed = maxSpeed;
        if (isUltimateActive)
        {
            currentSmoothTime *= 0.15f; // 大幅降低阻尼
            currentMaxSpeed *= 4f;       // 大幅提高最大速度
        }
        else if (effectiveOrbitFrequency > orbitFrequency * 1.1f)
        {
            // 焰舞加速等升级时也稍微降低阻尼
            float speedRatio = effectiveOrbitFrequency / orbitFrequency;
            currentSmoothTime /= speedRatio;
            currentMaxSpeed *= speedRatio;
        }

        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref currentVelocity, currentSmoothTime, currentMaxSpeed);
        RotateModel();
    }

    /// <summary>
    /// 火焰生成逻辑
    /// </summary>
    void UpdateFlameSpawn()
    {
        if (groundHazardPrefab == null) return;
        // 火焰只在融合大招（炎刃流星）激活时生成
        if (!isFlameUltimateActive) return;

        float interval = flameSpawnInterval * 0.3f; // 融合大招期间加速生成
        
        flameTimer -= Time.deltaTime;
        if (flameTimer <= 0f)
        {
            flameTimer = interval;
            SpawnFlameAtPosition();
        }
    }

    void SpawnFlameAtPosition()
    {
        RaycastHit hit;
        Vector3 origin = transform.position;
        
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

                int finalFlameDmg = flameDamage;
                float finalFlameDur = flameDuration;
                if (isFlameUltimateActive)
                {
                    finalFlameDmg *= 3;
                    finalFlameDur *= 2f;
                }

                gh.Initialize(finalFlameDmg, finalFlameDur, weaponName, ownerTransform.gameObject);
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
                searchTimer = 0.3f; // 快速搜索下一个目标
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
        Collider[] hits = Physics.OverlapSphere(ownerTransform.position, effectiveSearchRange, enemyLayer);
        float minDist = float.MaxValue;
        Transform best = null;

        foreach (var hit in hits)
        {
            Health h = hit.GetComponent<Health>();
            if (h == null) h = hit.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
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
            lockTimer = effectiveLockDuration;
        }
    }

    Vector3 CalculateOrbitPosition(Vector3 centerPos)
    {
        float freq = effectiveOrbitFrequency;
        float rad = effectiveOrbitRadius;

        // 大招：速度x3
        if (isUltimateActive) freq *= 3f;

        float t = Time.time * freq + timeOffset;
        float x = Mathf.Sin(t) * rad;
        float z = Mathf.Sin(t * 2f) * (rad * 0.6f);
        float y = Mathf.Sin(t * 1.51f) * 1.0f;

        return centerPos + new Vector3(x, y, z);
    }

    /// <summary>
    /// 大招特效挂载到飞刀上
    /// </summary>
    public void AttachUltimateVfx(GameObject vfxPrefab)
    {
        if (vfxPrefab == null) return;
        // 移除旧特效
        if (ultimateVfxInstance != null) Destroy(ultimateVfxInstance);

        ultimateVfxInstance = Instantiate(vfxPrefab, transform);
        ultimateVfxInstance.transform.localPosition = Vector3.zero;
        ultimateVfxInstance.transform.localRotation = Quaternion.identity;
        ultimateVfxInstance.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 移除大招特效
    /// </summary>
    public void RemoveUltimateVfx()
    {
        if (ultimateVfxInstance != null)
        {
            Destroy(ultimateVfxInstance);
            ultimateVfxInstance = null;
        }
    }

    void RotateModel()
    {
        if (currentVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(currentVelocity.normalized);
            float rotSpeed = isUltimateActive ? 30f : 15f;
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 飞刀停留在敌人碰撞体内时持续伤害（提升攻击欲望）
    /// </summary>
    void OnTriggerStay(Collider other)
    {
        TryDealDamage(other);
    }

    // 冷却通过 CleanupCooldowns 自然过期，不在 OnTriggerExit 中清除
    // 避免飞刀快速进出碰撞体边缘导致重复伤害

    /// <summary>
    /// 统一伤害处理（Enter和Stay共用）
    /// </summary>
    void TryDealDamage(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return;

        int id = other.gameObject.GetInstanceID();

        // 检查冷却
        if (hitCooldowns.ContainsKey(id))
        {
            if (Time.time < hitCooldowns[id]) return;
            hitCooldowns.Remove(id);
        }

        Health h = other.GetComponent<Health>();
        if (h == null) h = other.GetComponentInParent<Health>();

        if (h != null && !h.IsDead)
        {
            // 计算最终伤害
            int finalDmg = damageAmount;
            if (isUltimateActive) finalDmg *= 2;

            // 记录击杀前HP（用于判断是否击杀）
            int hpBefore = h.GetCurrentHealth();

            // 【修复】传递武器名称，确保伤害统计正确记录
            string weaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) ? sourceWeapon.StatBlock.weaponName : "FlameDagger";
            h.TakeDamage(finalDmg, transform.position, this.gameObject, AttackType.Standard, null, null, weaponName);
            hitCooldowns[id] = Time.time + effectiveDamageInterval;

            // 积累能量
            if (sourceWeapon != null)
            {
                sourceWeapon.GainEnergy(finalDmg);
            }

            // 击退
            EnemyAI ai = h.GetComponent<EnemyAI>();
            if (ai == null) ai = h.GetComponentInParent<EnemyAI>();
            if (ai != null)
            {
                Vector3 knockbackDir = currentVelocity.normalized;
                if (knockbackDir == Vector3.zero) knockbackDir = transform.forward;
                ai.ApplyKnockback(knockbackDir, knockbackForce);
            }

            // === 技能效果 ===
            if (sourceWeapon != null)
            {
                // 灵能烙印：点燃敌人
                if (sourceWeapon.daggerIgniteUpgrade)
                {
                    StatusEffectReceiver status = other.GetComponent<StatusEffectReceiver>();
                    if (status == null) status = other.GetComponentInParent<StatusEffectReceiver>();
                    if (status != null && Random.value < 0.2f)
                    {
                        // 复用外层已声明的 weaponName 变量
                        status.ApplyBurn(Mathf.RoundToInt(finalDmg * 0.3f), 6f, 1f, weaponName);
                    }
                }

                // 连锁灵刃：命中被点燃敌人触发爆破
                if (sourceWeapon.daggerChainExplosion)
                {
                    StatusEffectReceiver status = other.GetComponent<StatusEffectReceiver>();
                    if (status == null) status = other.GetComponentInParent<StatusEffectReceiver>();
                    if (status != null && status.IsBurning)
                    {
                        TriggerChainExplosion(h.transform.position, finalDmg);
                    }
                }

                // 刃影分身：1%概率生成分身
                if (sourceWeapon.daggerCloneUpgrade && !isClone)
                {
                    if (Random.value < 0.01f)
                    {
                        SpawnCloneDagger();
                    }
                }

                // 灵魂收割：用HP差判断是否击杀（而非 IsDead，避免时序问题）
                if (sourceWeapon.daggerLifeStealUpgrade)
                {
                    bool killed = h.IsDead || h.GetCurrentHealth() <= 0 || (hpBefore > 0 && h.GetCurrentHealth() <= 0);
                    if (killed)
                    {
                        Health playerHealth = ownerTransform.GetComponent<Health>();
                        if (playerHealth == null) playerHealth = ownerTransform.GetComponentInParent<Health>();
                        if (playerHealth != null)
                        {
                            int maxHp = playerHealth.GetMaxHealth();
                            int heal = Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.02f));
                            playerHealth.Heal(heal);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 连锁灵刃：小范围爆破
    /// </summary>
    void TriggerChainExplosion(Vector3 center, int baseDamage)
    {
        float explosionRadius = 3f;
        int explosionDmg = Mathf.RoundToInt(baseDamage * 0.5f);

        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, center, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, enemyLayer);
        foreach (var col in hits)
        {
            Health h = col.GetComponent<Health>();
            if (h == null) h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                // 【修复】传递武器名称，确保连锁爆炸伤害也被统计
                string chainWeaponName = (sourceWeapon != null && sourceWeapon.StatBlock != null) ? sourceWeapon.StatBlock.weaponName : "FlameDagger";
                h.TakeDamage(explosionDmg, center, this.gameObject, AttackType.Standard, null, null, chainWeaponName);
            }
        }
    }

    /// <summary>
    /// 刃影分身：生成短暂分身飞刀
    /// </summary>
    void SpawnCloneDagger()
    {
        if (sourceWeapon == null || sourceWeapon.StatBlock == null) return;
        if (sourceWeapon.StatBlock.projectilePrefab == null) return;

        // 确定生成位置：优先使用挂载点，否则在飞刀当前位置
        Vector3 spawnPos = cloneSpawnPoint != null ? cloneSpawnPoint.position : transform.position;
        Quaternion spawnRot = cloneSpawnPoint != null ? cloneSpawnPoint.rotation : transform.rotation;

        GameObject cloneObj = Instantiate(sourceWeapon.StatBlock.projectilePrefab, spawnPos, spawnRot);
        cloneObj.transform.localScale = transform.localScale * 0.7f;
        cloneObj.name = "分身飞刀";

        // 应用分身材质（半透明/发光效果）
        if (cloneMaterial != null)
        {
            Renderer[] renderers = cloneObj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Material[] mats = new Material[r.materials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = cloneMaterial;
                r.materials = mats;
            }
        }

        FlameDaggerController cloneCtrl = cloneObj.GetComponent<FlameDaggerController>();
        if (cloneCtrl != null)
        {
            int cloneDamage = Mathf.RoundToInt(damageAmount * 0.2f);
            cloneCtrl.Initialize(sourceWeapon.StatBlock, ownerTransform, cloneDamage, knockbackForce * 0.5f, sourceWeapon);
            cloneCtrl.isClone = true;
            cloneCtrl.sourceWeapon = null; // 分身不再触发分身
        }

        Destroy(cloneObj, 10f);
    }

    void CleanupCooldowns()
    {
        if (hitCooldowns.Count == 0) return;

        List<int> toRemove = new List<int>();
        foreach (var kvp in hitCooldowns)
        {
            if (Time.time >= kvp.Value)
                toRemove.Add(kvp.Key);
        }
        foreach (var k in toRemove) hitCooldowns.Remove(k);
    }
}
