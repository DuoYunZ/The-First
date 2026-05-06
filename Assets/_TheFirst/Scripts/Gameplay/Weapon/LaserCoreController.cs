// LaserCoreController.cs
// 镭射核心控制器 — 漂浮在玩家身旁的能量核心，自动发射聚焦光束
// 核心机制：持续照射同一目标时逐层叠加伤害加成（聚焦升温），满层后过热爆发AOE
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class LaserCoreController : MonoBehaviour
{
    // === 状态枚举 ===
    private enum CoreState { Idle, Firing, Overheat }
    private CoreState currentState = CoreState.Idle;

    [Header("跟随设置")]
    [Tooltip("跟随平滑时间，越小越灵敏")]
    public float followSmoothTime = 0.25f;
    [Tooltip("悬浮在玩家上方的基础高度")]
    public float hoverHeight = 2.0f;
    [Tooltip("上下浮动的幅度")]
    public float hoverAmplitude = 0.15f;
    [Tooltip("上下浮动的频率")]
    public float hoverFrequency = 1.5f;
    [Tooltip("相对玩家的水平偏移")]
    public float followOffset = 0.8f;

    [Header("战斗参数")]
    public float detectionRadius = 15f;
    public float rotationSpeed = 10f;
    public LayerMask enemyLayer;

    [Header("光束视觉")]
    public float baseBeamWidth = 0.3f;
    public Color beamColorMin = new Color(0.3f, 0.7f, 1f);   // 冷蓝（低聚焦）
    public Color beamColorMax = new Color(1f, 0.4f, 0.1f);    // 橙红（满聚焦）

    [Header("聚焦升温")]
    [Tooltip("最大聚焦层数")]
    public int maxFocusStacks = 5;
    [Tooltip("每层伤害加成 (0.15 = 15%)")]
    public float focusDamageBonus = 0.15f;
    [Tooltip("每隔多久叠一层聚焦（秒）")]
    public float focusStackInterval = 1.0f;
    [Tooltip("满层后再照射多久触发过热（秒）")]
    public float overheatDelay = 2.0f;

    [Header("过热爆发")]
    [Tooltip("过热AOE伤害 = 基础伤害 × 此倍率")]
    public float overheatDamageMultiplier = 1.5f;
    [Tooltip("过热AOE范围")]
    public float overheatAoeRadius = 3f;
    [Tooltip("过热后的冷却时间（秒）")]
    public float cooldownTime = 3.0f;
    [Tooltip("过热爆发特效预制件")]
    public GameObject overheatVfxPrefab;

    [Header("核心熔毁（灼烧区域）")]
    [Tooltip("是否启用灼烧区域（核心熔毁技能解锁后开启）")]
    public bool meltdownEnabled = false;
    [Tooltip("灼烧区域持续时间（秒）")]
    public float meltdownDuration = 3f;
    [Tooltip("灼烧区域每跳间隔（秒）")]
    public float meltdownTickInterval = 0.5f;
    [Tooltip("灼烧区域的地面特效预制件")]
    public GameObject meltdownZonePrefab;

    [Header("命中特效")]
    [Tooltip("主光束命中特效预制件")]
    public GameObject impactVfxPrefab;

    // ========================
    // 内部变量
    // ========================

    // 组件
    private LineRenderer mainLine;
    private WeaponPart ownerWeapon;
    private Transform ownerPlayer;

    // 运动
    private Vector3 followVelocity;

    // 目标
    private Transform currentTarget;

    // 伤害
    private int baseDamagePerTick;
    private float tickRate;
    private float damageTickTimer;
    private float critRate;
    private float critDamageMult;

    // 聚焦
    private int currentFocusStacks = 0;
    private float focusTimer = 0f;
    private float overheatTimer = 0f;

    // 冷却
    private float cooldownTimer = 0f;

    // 主光束命中特效
    private GameObject activeImpactVfx;

    // 折射系统
    private int refractionCount = 0;                           // 折射目标数量
    private float refractionDamageDecay = 0.7f;                // 折射伤害衰减系数
    private List<LineRenderer> refractionLines = new List<LineRenderer>();
    private List<GameObject> refractionImpactVfxList = new List<GameObject>();

    // ========================
    // 初始化
    // ========================

    /// <summary>
    /// 由 WeaponPart.SetupLaserCore() 调用
    /// </summary>
    public void Initialize(WeaponPart weapon, int damage, float tickRate,
                           Transform owner, float cRate, float cDmgMult,
                           int refraction = 0)
    {
        this.ownerWeapon = weapon;
        this.baseDamagePerTick = damage;
        this.tickRate = tickRate;
        this.ownerPlayer = owner;
        this.critRate = cRate;
        this.critDamageMult = cDmgMult;
        this.refractionCount = refraction;
    }

    void Start()
    {
        mainLine = GetComponent<LineRenderer>();
        mainLine.useWorldSpace = true;
        mainLine.enabled = false;
        mainLine.positionCount = 2;
    }

    void Update()
    {
        // 安全检查：如果宿主武器被销毁则自毁
        if (ownerWeapon == null || ownerPlayer == null)
        {
            Destroy(gameObject);
            return;
        }

        HandleMovement();

        switch (currentState)
        {
            case CoreState.Idle:
                HandleIdle();
                break;
            case CoreState.Firing:
                HandleFiring();
                break;
            case CoreState.Overheat:
                HandleOverheat();
                break;
        }
    }

    // ========================
    // 移动：浮空跟随
    // ========================
    void HandleMovement()
    {
        if (ownerPlayer == null) return;

        // 目标位置：玩家头顶偏侧 + 轻微上下浮动
        float bobOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        Vector3 targetPos = ownerPlayer.position
            + Vector3.up * (hoverHeight + bobOffset)
            + ownerPlayer.right * followOffset;

        // 平滑跟随
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPos, ref followVelocity, followSmoothTime);

        // 朝向当前目标
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            Vector3 lookDir = (currentTarget.position - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // ========================
    // Idle 状态
    // ========================
    void HandleIdle()
    {
        mainLine.enabled = false;
        HideMainImpactVfx();
        HideAllRefractionLines();

        FindTarget();
        if (currentTarget != null)
        {
            // 进入射击状态
            currentState = CoreState.Firing;
            currentFocusStacks = 0;
            focusTimer = 0f;
            overheatTimer = 0f;
            damageTickTimer = 0f;
        }
    }

    // ========================
    // Firing 状态
    // ========================
    void HandleFiring()
    {
        // 检查目标有效性
        if (!IsTargetValid(currentTarget))
        {
            // 目标无效，尝试寻找新目标
            FindTarget();
            if (currentTarget == null)
            {
                // 没有目标，回到 Idle
                currentState = CoreState.Idle;
                currentFocusStacks = 0;
                return;
            }
            // 切换到新目标，聚焦重置
            currentFocusStacks = 0;
            focusTimer = 0f;
            overheatTimer = 0f;
        }

        mainLine.enabled = true;

        // 1. 更新光束视觉
        UpdateMainBeamVisual();

        // 2. 处理折射光束
        UpdateRefractionBeams();

        // 3. 主光束伤害
        DealMainBeamDamage();

        // 4. 折射光束伤害
        DealRefractionDamage();

        // 5. 聚焦层数累积
        UpdateFocusStacks();

        // 6. 过热判定
        if (currentFocusStacks >= maxFocusStacks)
        {
            overheatTimer += Time.deltaTime;
            if (overheatTimer >= overheatDelay)
            {
                TriggerOverheat();
            }
        }
    }

    // ========================
    // Overheat 状态（过热冷却中）
    // ========================
    void HandleOverheat()
    {
        mainLine.enabled = false;
        HideMainImpactVfx();
        HideAllRefractionLines();

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            currentState = CoreState.Idle;
        }
    }

    // ========================
    // 聚焦升温
    // ========================
    void UpdateFocusStacks()
    {
        if (currentFocusStacks >= maxFocusStacks) return;

        focusTimer += Time.deltaTime;
        if (focusTimer >= focusStackInterval)
        {
            focusTimer -= focusStackInterval;
            currentFocusStacks++;

            if (currentFocusStacks >= maxFocusStacks)
            {
                Debug.Log($"<color=#FF8800>[镭射聚焦] 满层！{overheatDelay}秒后将触发过热爆发</color>");
            }
        }
    }

    /// <summary>
    /// 计算当前聚焦层数对应的伤害乘数
    /// </summary>
    float GetFocusDamageMultiplier()
    {
        return 1f + currentFocusStacks * focusDamageBonus;
    }

    // ========================
    // 过热爆发
    // ========================
    void TriggerOverheat()
    {
        // 过热爆发位置（在当前照射目标位置或核心位置）
        Vector3 burstPos = (currentTarget != null) ? currentTarget.position : transform.position;

        // 1. 释放 AOE 伤害
        int aoeDamage = Mathf.RoundToInt(baseDamagePerTick * overheatDamageMultiplier * GetFocusDamageMultiplier());
        Collider[] hits = Physics.OverlapSphere(burstPos, overheatAoeRadius, enemyLayer);

        Debug.Log($"<color=#FF6600>[镭射过热] 爆发！AOE伤害={aoeDamage}, 范围={overheatAoeRadius:F1}, 命中={hits.Length}个敌人, 熔毁={meltdownEnabled}</color>");

        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                h.TakeDamage(aoeDamage, burstPos, ownerWeapon.gameObject, AttackType.Standard);
            }
        }

        // 2. 播放爆发特效
        if (overheatVfxPrefab != null)
        {
            GameObject vfx = Instantiate(overheatVfxPrefab, burstPos, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 3.【核心熔毁】如果已解锁，生成持续灼烧区域
        if (meltdownEnabled)
        {
            StartCoroutine(MeltdownZoneCoroutine(burstPos, aoeDamage));
        }

        // 4. 进入冷却
        currentState = CoreState.Overheat;
        cooldownTimer = cooldownTime;
        currentFocusStacks = 0;
        focusTimer = 0f;
        overheatTimer = 0f;
        currentTarget = null;
    }

    /// <summary>
    /// 核心熔毁：在过热爆发位置生成持续灼烧区域，每隔一段时间对范围内敌人造成伤害
    /// </summary>
    private IEnumerator MeltdownZoneCoroutine(Vector3 zoneCenter, int baseDamage)
    {
        // 生成灼烧区域视觉特效
        GameObject zoneVfx = null;
        if (meltdownZonePrefab != null)
        {
            zoneVfx = Instantiate(meltdownZonePrefab, zoneCenter, Quaternion.identity);
        }

        // 灼烧伤害 = 爆发伤害的50%
        int burnDamage = Mathf.RoundToInt(baseDamage * 0.5f);
        float elapsed = 0f;

        while (elapsed < meltdownDuration)
        {
            yield return new WaitForSeconds(meltdownTickInterval);
            elapsed += meltdownTickInterval;

            // 对灼烧区域内的敌人造成伤害
            Collider[] enemies = Physics.OverlapSphere(zoneCenter, overheatAoeRadius, enemyLayer);
            foreach (var col in enemies)
            {
                Health h = col.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    h.TakeDamage(burnDamage, zoneCenter, ownerWeapon.gameObject, AttackType.Standard);
                }
            }
        }

        // 清理特效
        if (zoneVfx != null) Destroy(zoneVfx);
    }

    // ========================
    // 主光束视觉
    // ========================
    void UpdateMainBeamVisual()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = GetTargetHitPos(currentTarget);

        mainLine.SetPosition(0, startPos);
        mainLine.SetPosition(1, endPos);

        // 颜色和宽度随聚焦层数变化
        float t = maxFocusStacks > 0 ? (float)currentFocusStacks / maxFocusStacks : 0f;
        Color currentColor = Color.Lerp(beamColorMin, beamColorMax, t);
        mainLine.startColor = currentColor;
        mainLine.endColor = currentColor;

        float currentWidth = baseBeamWidth * (1f + t * 0.8f);
        mainLine.startWidth = currentWidth;
        mainLine.endWidth = currentWidth;

        // 命中特效
        ShowImpactVfx(ref activeImpactVfx, impactVfxPrefab, endPos);
    }

    // ========================
    // 主光束伤害
    // ========================
    void DealMainBeamDamage()
    {
        damageTickTimer += Time.deltaTime;
        if (damageTickTimer < (1f / tickRate)) return;
        damageTickTimer = 0f;

        if (currentTarget == null) return;

        Health h = currentTarget.GetComponentInParent<Health>();
        if (h == null || h.IsDead) return;

        // 计算伤害 = 基础伤害 × 聚焦倍率
        int damage = Mathf.RoundToInt(baseDamagePerTick * GetFocusDamageMultiplier());

        // 暴击判定
        bool isCrit = Random.value <= critRate;
        if (isCrit)
        {
            damage = Mathf.RoundToInt(damage * critDamageMult);
        }

        h.TakeDamage(damage, currentTarget.position, ownerWeapon.gameObject,
                      AttackType.Standard, null, null, "", isCrit);
    }

    // ========================
    // 折射光束系统
    // ========================

    /// <summary>
    /// 更新折射光束的视觉表现
    /// </summary>
    void UpdateRefractionBeams()
    {
        if (refractionCount <= 0)
        {
            HideAllRefractionLines();
            return;
        }

        // 确保有足够的 LineRenderer 子物体
        EnsureRefractionLineCount(refractionCount);

        HashSet<Transform> usedTargets = new HashSet<Transform>();
        usedTargets.Add(currentTarget);

        Transform prevTarget = currentTarget;
        Vector3 prevHitPos = GetTargetHitPos(currentTarget);

        for (int i = 0; i < refractionCount; i++)
        {
            // 从上一个目标位置寻找最近的新目标
            Transform refTarget = FindNearestEnemy(prevHitPos, detectionRadius * 0.6f, usedTargets);

            if (refTarget != null)
            {
                Vector3 refEndPos = GetTargetHitPos(refTarget);

                refractionLines[i].enabled = true;
                refractionLines[i].SetPosition(0, prevHitPos);
                refractionLines[i].SetPosition(1, refEndPos);

                // 折射光束颜色略淡
                float t = maxFocusStacks > 0 ? (float)currentFocusStacks / maxFocusStacks : 0f;
                Color refColor = Color.Lerp(beamColorMin, beamColorMax, t);
                refractionLines[i].startColor = refColor;
                refractionLines[i].endColor = refColor;
                refractionLines[i].startWidth = baseBeamWidth * 0.5f;
                refractionLines[i].endWidth = baseBeamWidth * 0.5f;

                // 折射命中特效
                EnsureRefractionImpactVfx(i);
                GameObject refVfx = refractionImpactVfxList[i];
                ShowImpactVfx(ref refVfx, impactVfxPrefab, refEndPos);
                refractionImpactVfxList[i] = refVfx;

                usedTargets.Add(refTarget);
                prevTarget = refTarget;
                prevHitPos = refEndPos;
            }
            else
            {
                refractionLines[i].enabled = false;
                HideRefractionImpactVfx(i);
            }
        }
    }

    /// <summary>
    /// 折射光束造成伤害
    /// </summary>
    void DealRefractionDamage()
    {
        if (refractionCount <= 0) return;
        // 只在主光束伤害跳字的同一帧造成折射伤害
        // （damageTickTimer 在 DealMainBeamDamage 中被重置为0，这里检查是否刚刚重置过）
        // 更简单的做法：共用一个 tick 计时器，在 DealMainBeamDamage 里一起处理
        // 但为了解耦，这里用一个简单标记

        // 实际上，因为 DealMainBeamDamage 先执行并重置 damageTickTimer=0，
        // 此时 damageTickTimer 刚好是0（或接近0），我们可以检查这个
        if (damageTickTimer > 0.01f) return; // 不是刚刚 tick 过

        HashSet<Transform> usedTargets = new HashSet<Transform>();
        usedTargets.Add(currentTarget);

        Vector3 prevHitPos = GetTargetHitPos(currentTarget);
        float currentDecay = refractionDamageDecay;

        for (int i = 0; i < refractionCount; i++)
        {
            if (i >= refractionLines.Count || !refractionLines[i].enabled) break;

            Transform refTarget = FindNearestEnemy(prevHitPos, detectionRadius * 0.6f, usedTargets);
            if (refTarget == null) break;

            Health h = refTarget.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                // 折射伤害 = 主光束伤害 × 衰减系数 × 聚焦倍率
                int refDamage = Mathf.RoundToInt(baseDamagePerTick * currentDecay * GetFocusDamageMultiplier());

                bool isCrit = Random.value <= critRate;
                if (isCrit)
                {
                    refDamage = Mathf.RoundToInt(refDamage * critDamageMult);
                }

                h.TakeDamage(refDamage, refTarget.position, ownerWeapon.gameObject,
                              AttackType.Standard, null, null, "", isCrit);
            }

            usedTargets.Add(refTarget);
            prevHitPos = GetTargetHitPos(refTarget);
            currentDecay *= refractionDamageDecay; // 每级折射继续衰减
        }
    }

    // ========================
    // 折射 LineRenderer 管理
    // ========================

    /// <summary>
    /// 确保有足够数量的折射用 LineRenderer 子物体
    /// </summary>
    void EnsureRefractionLineCount(int count)
    {
        while (refractionLines.Count < count)
        {
            GameObject lineObj = new GameObject($"RefractionBeam_{refractionLines.Count}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.enabled = false;

            // 复制主光束的材质
            if (mainLine.material != null)
            {
                lr.material = mainLine.material;
            }

            refractionLines.Add(lr);
        }
    }

    void EnsureRefractionImpactVfx(int index)
    {
        while (refractionImpactVfxList.Count <= index)
        {
            refractionImpactVfxList.Add(null);
        }
    }

    void HideAllRefractionLines()
    {
        for (int i = 0; i < refractionLines.Count; i++)
        {
            if (refractionLines[i] != null)
                refractionLines[i].enabled = false;
            HideRefractionImpactVfx(i);
        }
    }

    void HideRefractionImpactVfx(int index)
    {
        if (index < refractionImpactVfxList.Count && refractionImpactVfxList[index] != null)
        {
            refractionImpactVfxList[index].SetActive(false);
        }
    }

    // ========================
    // 命中特效辅助
    // ========================

    void ShowImpactVfx(ref GameObject vfxInstance, GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        if (vfxInstance == null)
        {
            vfxInstance = Instantiate(prefab, pos, Quaternion.identity);
        }
        vfxInstance.SetActive(true);
        vfxInstance.transform.position = pos;
    }

    void HideMainImpactVfx()
    {
        if (activeImpactVfx != null)
            activeImpactVfx.SetActive(false);
    }

    // ========================
    // 目标管理
    // ========================

    void FindTarget()
    {
        currentTarget = FindNearestEnemy(transform.position, detectionRadius, null);
    }

    /// <summary>
    /// 在指定位置和范围内寻找最近的活着的敌人（排除已用目标）
    /// </summary>
    Transform FindNearestEnemy(Vector3 center, float radius, HashSet<Transform> exclude)
    {
        Collider[] enemies = Physics.OverlapSphere(center, radius, enemyLayer);
        float minDist = float.MaxValue;
        Transform best = null;

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            Transform t = h.transform;
            if (exclude != null && exclude.Contains(t)) continue;

            float d = Vector3.Distance(center, col.transform.position);
            if (d < minDist)
            {
                minDist = d;
                best = t;
            }
        }

        return best;
    }

    bool IsTargetValid(Transform target)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;

        // 超出攻击范围则判定无效，触发重新寻敌
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > detectionRadius) return false;

        Health h = target.GetComponentInParent<Health>();
        return h != null && !h.IsDead;
    }

    Vector3 GetTargetHitPos(Transform target)
    {
        if (target == null) return transform.position + transform.forward * 10f;
        // 优先使用瞄准点
        Transform aimPoint = target.Find("AimTargetPoint");
        return (aimPoint != null) ? aimPoint.position : target.position;
    }

    // ========================
    // 生命周期
    // ========================

    void OnDestroy()
    {
        if (activeImpactVfx != null) Destroy(activeImpactVfx);

        foreach (var vfx in refractionImpactVfxList)
        {
            if (vfx != null) Destroy(vfx);
        }

        foreach (var lr in refractionLines)
        {
            if (lr != null) Destroy(lr.gameObject);
        }
    }

    void OnDisable()
    {
        HideMainImpactVfx();
        HideAllRefractionLines();
    }
}
