// PlayerBeamController.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerBeamController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform target; // 模式 A: 锁定目标
    private Vector3 fireDirection; // 模式 B: 固定方向
    private bool isDirectionalMode = false; // 是否为方向模式
    private float beamLength = 10f; // 激光长度
    private LayerMask enemyLayer; // 敌人层级

    private WeaponPart launcher;
    private WeaponStatBlock stats;

    private GameObject activeImpactVfxInstance;
    private float tickTimer;
    private int damagePerTick;

    // 由 WeaponPart 调用
    public void Initialize(WeaponStatBlock stats, WeaponPart launcher, Transform target)
    {
        InitCommon(stats, launcher);
        this.target = target;
        this.isDirectionalMode = false;
    }

    // --- 初始化方法 2: 方向穿透模式 (用于聚焦激光) ---
    public void InitializeDirectional(WeaponStatBlock stats, WeaponPart launcher, Vector3 direction, float length, LayerMask layer)
    {
        InitCommon(stats, launcher);
        this.fireDirection = direction;
        this.beamLength = length;
        this.enemyLayer = layer;
        this.isDirectionalMode = true;
    }

    private void InitCommon(WeaponStatBlock stats, WeaponPart launcher)
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.stats = stats;
        this.launcher = launcher;
        // 计算每一跳的伤害
        this.damagePerTick = Mathf.CeilToInt((float)stats.beamDamagePerSecond / stats.beamDamageTickRate);
    }

    void Update()
    {
        if (isDirectionalMode)
        {
            UpdateDirectionalBeam();
        }
        else
        {
            UpdateTargetedBeam();
        }
    }

    void UpdateTargetedBeam()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 startPoint = transform.position;
        Transform aimPoint = target.Find("AimTargetPoint");
        Vector3 endPoint = (aimPoint != null) ? aimPoint.position : target.position;

        // 更新视觉
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
        UpdateImpactVFX(endPoint);

        // 造成伤害 (单体)
        tickTimer += Time.deltaTime;
        if (tickTimer >= (1f / stats.beamDamageTickRate))
        {
            tickTimer = 0f;
            Health h = target.GetComponentInParent<Health>();
            if (h != null) ApplyDamageAndEffects(h);
        }
    }
    void UpdateDirectionalBeam()
    {
        // 激光始终跟随发射者(玩家/无人机)移动，方向保持初始化时的方向 (或者每帧更新 transform.forward)
        // 这里假设激光跟随父物体旋转：
        Vector3 startPoint = transform.position;
        Vector3 endPoint = startPoint + transform.forward * beamLength;

        // 更新视觉
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // 方向模式下，受击特效通常不显示，或者显示在最远的墙壁上，这里暂时忽略
        if (activeImpactVfxInstance != null) activeImpactVfxInstance.SetActive(false);

        // 造成伤害 (穿透 AOE)
        tickTimer += Time.deltaTime;
        if (tickTimer >= (1f / stats.beamDamageTickRate))
        {
            tickTimer = 0f;
            // 使用 SphereCast 或 BoxCast 检测路径上所有敌人
            RaycastHit[] hits = Physics.SphereCastAll(startPoint, 0.5f, transform.forward, beamLength, enemyLayer);
            foreach (var hit in hits)
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null) ApplyDamageAndEffects(h);
            }
        }
    }

    void UpdateImpactVFX(Vector3 hitPos)
    {
        if (stats.beamImpactVfxPrefab != null)
        {
            if (activeImpactVfxInstance == null)
                activeImpactVfxInstance = Instantiate(stats.beamImpactVfxPrefab, hitPos, Quaternion.identity);

            activeImpactVfxInstance.SetActive(true);
            activeImpactVfxInstance.transform.position = hitPos;
        }
    }
    void ApplyDamageAndEffects(Health enemyHealth)
    {
        if (enemyHealth == null) return;

        // 1. 基础伤害
        enemyHealth.TakeDamage(damagePerTick, enemyHealth.transform.position, launcher.gameObject, AttackType.Standard);

        if (BattleStatisticsManager.Instance != null && stats != null)
        {
            BattleStatisticsManager.Instance.AddDamage(stats.weaponName, damagePerTick);
        }

        // 2. 能量石逻辑 (直接复用你之前的代码，封装在这里)
        StatusEffectReceiver receiver = enemyHealth.GetComponent<StatusEffectReceiver>();
        if (receiver != null && launcher != null && launcher.currentStone != null)
        {
            EnergyStoneSO stone = launcher.currentStone;
           
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) //
            {
                int fireStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyBurn); //
                if (fireStoneCount >= 2 && receiver.IsBurning) //
                    receiver.Ignite(); //
                else if (!receiver.IsBurning)
                    receiver.ApplyBurn(stone.burnDamage, stone.burnDuration, stone.burnTickInterval); //
            }

            // 2. 寒冰石
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow)) //
            {
                receiver.ApplySlow(stone.slowPercentage, stone.slowDuration, stone.slowColor); //
                int iceStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplySlow); //
                if (iceStoneCount >= 2 && receiver.IsSlowed && !receiver.IsStunned && Random.value <= stone.freezeChance) //
                    receiver.ApplyStun(stone.freezeDuration, stone.freezeVfxPrefab); //
            }

            // 3. 雷电石
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain)) //
            {
                int smiteDamage = Mathf.RoundToInt(stone.smiteDamage * (PlayerStats.Instance.damageMultiplier + stone.damageModifier) + PlayerStats.Instance.flatDamageBonus); //
                receiver.GetComponent<Health>()?.TakeDamage(smiteDamage, receiver.transform.position, launcher.gameObject, AttackType.Standard); //
                if (stone.smiteVfxPrefab != null) Instantiate(stone.smiteVfxPrefab, receiver.transform.position, Quaternion.identity); //

                int lightningStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyChain); //
                if (lightningStoneCount >= 2)
                {
                    PlayerStats.Instance.lightningSmiteCounter++; //
                    if (PlayerStats.Instance.lightningSmiteCounter >= 5) //
                    {
                        PlayerStats.Instance.lightningSmiteCounter = 0; //
                        launcher.ChainLightningFromTarget(receiver.transform, stone.chainTargets, smiteDamage, stone.chainRange); //
                    }
                }
            }

            // 4. 大地石 (眩晕)
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyStun)) //
            {
                float finalStunChance = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyStun) >= 2) ? stone.stunChance_Stacked : stone.stunChance; //
                if (Random.value <= finalStunChance && !receiver.IsStunned)
                    receiver.ApplyStun(stone.stunDuration); //
            }

            // 5. 风暴石 (击退)
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback)) //
            {
                float finalKnockbackForce = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback) >= 2) ? stone.knockbackForce_Stacked : stone.knockbackForce; //
                Vector3 pushDir = (receiver.transform.position - launcher.transform.position).normalized;
                pushDir.y = 0;
                receiver.ApplyKnockback(pushDir, finalKnockbackForce); //
            }

            // 6. 腐蚀石
            if (stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode)) //
            {
                float finalCorrodeMultiplier = (PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode) >= 2) ? stone.corrodeMultiplier_Stacked : stone.corrodeMultiplier; //
                receiver.ApplyCorrode(finalCorrodeMultiplier, 5f, stone.corrodeColor); // (5f 是瞬时debuff的示例持续时间)
            }
        }
    }
    void OnDestroy()
    {
        if (activeImpactVfxInstance != null) Destroy(activeImpactVfxInstance);
    }
}