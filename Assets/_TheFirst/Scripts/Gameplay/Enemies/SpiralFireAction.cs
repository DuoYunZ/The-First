// --- SpiralFireAction.cs (修正起始角度最终版) ---
using UnityEngine;

public class SpiralFireAction : Node
{
    // ... 所有public参数保持不变 ...
    [Header("武器设置")]
    public WeaponStatBlock weaponToFire;
    public Transform firePoint;
    [Header("动作表现与时间")]
    public string windupAnimationTrigger;
    public float windupDuration = 1f;
    public string firingAnimationBool = "isFiringSpiral";
    public string recoveryAnimationTrigger;
    public float recoveryDuration = 1f;
    [Header("螺旋弹幕参数")]
    public int totalProjectiles = 60;
    public float totalDuration = 5f;
    public float rotationSpeed = 180f;
    [Header("冷却设置")]
    public string attackName = "SpiralAttack";
    public float cooldownDuration = 15f;

    [Header("配合特效的预制件")] // 【新增】
    public GameObject windupEffectPrefab;
    public GameObject firingLoopEffectPrefab; // 持续施法循环特效
    public GameObject recoveryEffectPrefab;

    // 内部状态机
    private enum ActionState { Ready, WindingUp, Firing, Recovering, Completed }
    private ActionState currentState;

    // 私有变量
    private int projectilesFired;
    private float actionTimer;
    private float fireIntervalTimer;
    private float fireInterval;
    private float currentRotationOffset; // 当前的旋转偏移角度
    private Vector3 initialForwardDirection; // 【核心】施法开始时的初始前方
    private Transform selfTransform;
    private EnemyAI regularAI;
    private Animator animator;

    private GameObject activeLoopEffect; // 用于存储循环特效的实例

    void Awake()
    {
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null) selfTransform = bossRb.transform;
        animator = GetComponentInParent<Animator>();
        regularAI = GetComponentInParent<EnemyAI>();
    }

    public override NodeState Evaluate()
    {
        if (currentState == ActionState.Completed)
        {
            currentState = ActionState.Ready;
        }

        if (currentState == ActionState.Ready)
        {
            if (regularAI != null) regularAI.enabled = false;
            currentState = ActionState.WindingUp;
            actionTimer = 0f;
            if (animator != null && !string.IsNullOrEmpty(windupAnimationTrigger))
            {
                animator.SetTrigger(windupAnimationTrigger);
            }
            GetComponentInParent<BehaviorTree>().StartCooldown(attackName, cooldownDuration);
            return NodeState.RUNNING;
        }

        switch (currentState)
        {
            case ActionState.WindingUp:
                actionTimer += Time.deltaTime;
                if (actionTimer >= windupDuration)
                {
                    currentState = ActionState.Firing;
                    projectilesFired = 0;
                    fireIntervalTimer = 0f;

                    // 【核心修正】在开火前，记录下Boss当前的正前方作为初始方向
                    initialForwardDirection = selfTransform.forward;
                    currentRotationOffset = 0f; // 旋转偏移量从0开始

                    if (totalProjectiles > 0)
                    {
                        fireInterval = totalDuration / totalProjectiles;
                    }
                    if (animator != null && !string.IsNullOrEmpty(firingAnimationBool))
                    {
                        animator.SetBool(firingAnimationBool, true);
                    }
                    if (firingLoopEffectPrefab != null)
                    {
                        Transform spawnPoint = (firePoint != null) ? firePoint : selfTransform;
                        activeLoopEffect = Instantiate(firingLoopEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
                    }
                }
                break;

            case ActionState.Firing:
                currentRotationOffset += rotationSpeed * Time.deltaTime;

                fireIntervalTimer += Time.deltaTime;
                if (fireIntervalTimer >= fireInterval)
                {
                    fireIntervalTimer -= fireInterval;
                    Fire();
                    projectilesFired++;
                }

                if (projectilesFired >= totalProjectiles)
                {
                    currentState = ActionState.Recovering;
                    actionTimer = 0f;
                    if (animator != null && !string.IsNullOrEmpty(firingAnimationBool))
                    {
                        animator.SetBool(firingAnimationBool, false);
                    }
                    if (animator != null && !string.IsNullOrEmpty(recoveryAnimationTrigger))
                    {
                        animator.SetTrigger(recoveryAnimationTrigger);
                    }
                }
                break;

            case ActionState.Recovering:
                actionTimer += Time.deltaTime;
                if (actionTimer >= recoveryDuration)
                {
                    currentState = ActionState.Completed;
                    return NodeState.SUCCESS;
                }
                break;
        }

        return NodeState.RUNNING;
    }

    private void Fire()
    {
        Vector3 firePosition = (firePoint != null) ? firePoint.position : selfTransform.position;

        // 【核心修正】在初始方向的基础上，应用当前的旋转偏移量
        Vector3 fireDirection = Quaternion.Euler(0, currentRotationOffset, 0) * initialForwardDirection;
        Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

        GameObject projectileGO = Instantiate(weaponToFire.projectilePrefab, firePosition, fireRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.InitializeAsStraight(fireDirection, weaponToFire.baseLaunchForce, weaponToFire.baseDirectDamage, true, weaponToFire.basePierceCount, weaponToFire.baseProjectileLifetime, weaponToFire.shieldImpactEffectPrefab, weaponToFire.defaultImpactEffectPrefab, weaponToFire.baseDotDamage, weaponToFire.baseDotDuration, weaponToFire.dotTickInterval, weaponToFire.baseSlowPercentage, weaponToFire.baseSlowDuration, AttackType.Standard);
        }
    }
    public void TriggerWindupEffect()
    {
        if (windupEffectPrefab != null)
        {
            // 特效通常在发射点(firePoint)或模型根节点(selfTransform)生成
            Transform spawnPoint = (firePoint != null) ? firePoint : selfTransform;
            Instantiate(windupEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }
    }

    public void StartFiringLoopEffect()
    {
        if (firingLoopEffectPrefab != null)
        {
            Transform spawnPoint = (firePoint != null) ? firePoint : selfTransform;
            activeLoopEffect = Instantiate(firingLoopEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }
    }

    public void StopFiringLoopEffect()
    {
        if (activeLoopEffect != null)
        {
            // 这里可以调用特效上的粒子停止播放方法，或者直接销毁
            Destroy(activeLoopEffect);
        }
    }

    public void TriggerRecoveryEffect()
    {
        if (recoveryEffectPrefab != null)
        {
            Transform spawnPoint = (firePoint != null) ? firePoint : selfTransform;
            Instantiate(recoveryEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }
    }
}