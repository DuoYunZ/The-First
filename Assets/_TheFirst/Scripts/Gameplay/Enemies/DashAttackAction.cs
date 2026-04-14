// --- DashAttackAction.cs (最终版，支持动画事件) ---
using UnityEngine;
using UnityEngine.AI;

public class DashAttackAction : Node
{
    [Header("攻击阶段设置")]
    public float windupDuration = 1.2f;
    public float dashSpeed = 30f;
    public float dashDistance = 15f;
    public float recoveryDuration = 1.5f;

    [Header("动作表现")]
    public string windupAnimationTrigger = "doDashWarning";
    public string dashAnimationTrigger = "doDash";
    public string recoveryAnimationTrigger; // (可选) 后摇动画触发器

    [Header("配合特效的预制件")]
    public GameObject windupEffectPrefab;
    public GameObject recoveryEffectPrefab;
    public GameObject dashEffectPrefab; // 【新增】Dash过程特效
    private GameObject currentDashEffectInstance; // 用于存储当前特效实例

    [Header("侧边子弹设置")]
    public Transform[] sideFirePoints;
    public WeaponStatBlock sideWeaponToFire;
    public float sideFireInterval = 0.1f;

    [Header("冷却设置")]
    public string attackName = "DashAttack";
    public float cooldownDuration = 8f;

    [Header("障碍物碰撞检测")]
    [Tooltip("冲刺时检测的障碍物层（防止穿墙）")]
    public LayerMask obstacleLayer = 1; // 默认为Default层，用户需在Inspector设置为Ground/Building等
    [Tooltip("碰撞检测射线的起点高度偏移")]
    public float raycastHeightOffset = 0.5f;

    // 内部状态
    private enum ActionState { Ready, WindingUp, Dashing, Recovering, Completed }
    private ActionState currentState;
    private float timer;
    private float sideFireTimer;
    private float calculatedDashDuration;
    private Rigidbody rb;
    private Animator animator;
    private EnemyAI regularAI;
    private NavMeshAgent agent; // 【修改】
    private Transform selfTransform; // 【修正】增加了selfTransform的引用

    void Awake()
    {
        rb = GetComponentInParent<Rigidbody>();
        if (rb != null) selfTransform = rb.transform; // 【修正】在Awake中获取selfTransform
        animator = GetComponentInParent<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();
    }

    public override NodeState Evaluate()
    {
        if (currentState == ActionState.Completed)
        {
            currentState = ActionState.Ready;
        }

        if (currentState == ActionState.Ready)
        {
            if (agent != null) agent.isStopped = true;

            Transform playerTarget = GameManager.Instance?.playerTransform;
            if (playerTarget != null)
            {
                Vector3 direction = (playerTarget.position - rb.transform.position).normalized;
                direction.y = 0;
                rb.transform.rotation = Quaternion.LookRotation(direction);
            }

            currentState = ActionState.WindingUp;
            timer = 0f;
            if (animator != null && !string.IsNullOrEmpty(windupAnimationTrigger))
            {
                animator.SetTrigger(windupAnimationTrigger);
            }

            // 【修正】将StartCooldown移动到这里，确保只执行一次
            GetComponentInParent<BehaviorTree>().StartCooldown(attackName, cooldownDuration);
            return NodeState.RUNNING;
        }

        switch (currentState)
        {
            case ActionState.WindingUp:
                timer += Time.deltaTime;
                if (timer >= windupDuration)
                {
                    currentState = ActionState.Dashing;
                    timer = 0f;
                    calculatedDashDuration = (dashSpeed > 0.01f) ? dashDistance / dashSpeed : 0f;
                    if (animator != null && !string.IsNullOrEmpty(dashAnimationTrigger))
                    {
                        animator.SetTrigger(dashAnimationTrigger);
                    }
                    if (agent != null) agent.enabled = false;
                    rb.isKinematic = false; // 确保Rigidbody可以被速度驱动
                    rb.velocity = rb.transform.forward * dashSpeed;

                    sideFireTimer = 0f;
                }
                break;

            case ActionState.Dashing:
                timer += Time.deltaTime;

                // 【新增】碰撞检测：如果前方有障碍物，提前终止冲刺
                float checkDistance = dashSpeed * Time.deltaTime * 2f + 1f; // 前方检测距离
                Vector3 rayOrigin = selfTransform.position + Vector3.up * raycastHeightOffset;
                if (Physics.Raycast(rayOrigin, selfTransform.forward, checkDistance, obstacleLayer))
                {
                    // 撞到障碍物，提前终止冲刺进入恢复阶段
                    currentState = ActionState.Recovering;
                    timer = 0f;
                    rb.velocity = Vector3.zero;
                    rb.isKinematic = true;
                    if (agent != null)
                    {
                        agent.enabled = true;
                        agent.Warp(selfTransform.position);
                        agent.isStopped = true;
                    }
                    if (animator != null && !string.IsNullOrEmpty(recoveryAnimationTrigger))
                    {
                        animator.SetTrigger(recoveryAnimationTrigger);
                    }
                    if (currentDashEffectInstance != null)
                    {
                        Destroy(currentDashEffectInstance);
                        currentDashEffectInstance = null;
                    }
                    break;
                }

                // ... (侧边发射子弹的逻辑保持不变)
                if (sideWeaponToFire != null && sideFirePoints != null && sideFirePoints.Length > 0)
                {
                    sideFireTimer += Time.deltaTime;
                    if (sideFireTimer >= sideFireInterval)
                    {
                        sideFireTimer -= sideFireInterval;
                        foreach (Transform firePoint in sideFirePoints)
                        {
                            if (firePoint != null)
                            {
                                GameObject projectileGO = Instantiate(sideWeaponToFire.projectilePrefab, firePoint.position, firePoint.rotation);
                                Projectile projectileScript = projectileGO.GetComponent<Projectile>();
                                if (projectileScript != null)
                                {
                                    projectileScript.InitializeAsStraight(firePoint.forward, sideWeaponToFire.baseLaunchForce, sideWeaponToFire.baseDirectDamage, true, sideWeaponToFire.basePierceCount, sideWeaponToFire.baseProjectileLifetime, sideWeaponToFire.shieldImpactEffectPrefab, sideWeaponToFire.defaultImpactEffectPrefab, sideWeaponToFire.baseDotDamage, sideWeaponToFire.baseDotDuration, sideWeaponToFire.dotTickInterval, sideWeaponToFire.baseSlowPercentage, sideWeaponToFire.baseSlowDuration, AttackType.Standard);
                                }
                            }
                        }
                    }
                }
                if (timer <= Time.deltaTime && dashEffectPrefab != null && currentDashEffectInstance == null)
                {
                      currentDashEffectInstance = Instantiate(dashEffectPrefab, selfTransform.position, selfTransform.rotation, selfTransform);
                }

                if (timer >= calculatedDashDuration)
                {
                    currentState = ActionState.Recovering;
                    timer = 0f;
                    rb.velocity = Vector3.zero;
                    rb.isKinematic = true;
                    if (agent != null)
                    {
                        agent.enabled = true;
                        // 【关键】将 agent 的位置同步到冲刺结束后的新位置
                        agent.Warp(selfTransform.position);
                        // 保持暂停状态，直到后摇结束
                        agent.isStopped = true;
                    }
                    if (animator != null && !string.IsNullOrEmpty(recoveryAnimationTrigger))
                    {
                        animator.SetTrigger(recoveryAnimationTrigger);
                    }

                    // 【新增】在 Dash 结束时销毁特效
                    if (currentDashEffectInstance != null)
                    {
                        Destroy(currentDashEffectInstance);
                        currentDashEffectInstance = null;
                    }
                }
                break;

            case ActionState.Recovering:
                timer += Time.deltaTime;
                if (timer >= recoveryDuration)
                {
                    currentState = ActionState.Completed;
                    return NodeState.SUCCESS;
                }
                break;
        }

        return NodeState.RUNNING;
    }

    // --- 用于被动画事件调用的公开方法 ---
    public void TriggerWindupEffect()
    {
        if (windupEffectPrefab != null && selfTransform != null)
        {
            Instantiate(windupEffectPrefab, selfTransform.position, selfTransform.rotation, selfTransform);
        }
    }

    public void TriggerRecoveryEffect()
    {
        if (recoveryEffectPrefab != null && selfTransform != null)
        {
            Instantiate(recoveryEffectPrefab, selfTransform.position, selfTransform.rotation, selfTransform);
        }
    }
}