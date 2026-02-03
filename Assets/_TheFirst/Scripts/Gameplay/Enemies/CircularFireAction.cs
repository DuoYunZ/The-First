// --- CircularFireAction.cs (带完整动作表现版) ---
using UnityEngine;

public class CircularFireAction : Node
{
    [Header("武器设置")]
    public WeaponStatBlock weaponToFire;
    public int projectileCount = 12;
    public Transform firePoint;

    [Header("动作表现")]
    [Tooltip("前摇动画的触发器名称")]
    public string windupAnimationTrigger;
    [Tooltip("前摇持续时间（秒）")]
    public float windupDuration = 1f;
    [Tooltip("后摇动画的触发器名称")]
    public string recoveryAnimationTrigger;
    [Tooltip("后摇持续时间（秒）")]
    public float recoveryDuration = 0.5f;

    [Header("冷却设置")]
    public string attackName = "CircularAttack";
    public float cooldownDuration = 5f;

    [Header("配合特效的预制件")] // 【新增】
    public GameObject windupEffectPrefab;
    public GameObject recoveryEffectPrefab;

    // 内部状态机
    private enum ActionState { Ready, WindingUp, Firing, Recovering, Completed }
    private ActionState currentState;
    private float timer;
    private Animator animator;
    private EnemyAI regularAI;
    private Transform selfTransform;

    void Awake()
    {
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null) selfTransform = bossRb.transform;
        animator = GetComponentInParent<Animator>();
        regularAI = GetComponentInParent<EnemyAI>();
    }

    // 当节点被行为树重新评估时，确保状态被重置
    public override NodeState Evaluate()
    {
        if (currentState == ActionState.Completed)
        {
            currentState = ActionState.Ready;
        }

        if (currentState == ActionState.Ready)
        {
            if (regularAI != null) regularAI.enabled = false; // 夺取控制权
            currentState = ActionState.WindingUp;
            timer = 0f;
            if (animator != null && !string.IsNullOrEmpty(windupAnimationTrigger))
            {
                animator.SetTrigger(windupAnimationTrigger); // 播放前摇动画
            }
            GetComponentInParent<BehaviorTree>().StartCooldown(attackName, cooldownDuration);
            return NodeState.RUNNING;
        }

        switch (currentState)
        {
            case ActionState.WindingUp:
                timer += Time.deltaTime;
                if (timer >= windupDuration)
                {
                    currentState = ActionState.Firing; // 前摇结束，准备开火
                }
                break;

            case ActionState.Firing:
                FireInCircle(); // 瞬间完成开火
                currentState = ActionState.Recovering; // 立即进入后摇
                timer = 0f;
                if (animator != null && !string.IsNullOrEmpty(recoveryAnimationTrigger))
                {
                    animator.SetTrigger(recoveryAnimationTrigger); // 播放后摇动画
                }
                break;

            case ActionState.Recovering:
                timer += Time.deltaTime;
                if (timer >= recoveryDuration)
                {
                    currentState = ActionState.Completed; // 后摇结束，整个动作完成
                    return NodeState.SUCCESS;
                }
                break;
        }

        return NodeState.RUNNING;
    }

    private void FireInCircle()
    {
        if (weaponToFire == null) return;
        Transform spawnPoint = firePoint != null ? firePoint : selfTransform;
        float angleStep = 360f / projectileCount;
        float currentAngle = 0f;
        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 fireDirection = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            GameObject projectileGO = Instantiate(weaponToFire.projectilePrefab, spawnPoint.position, Quaternion.LookRotation(fireDirection));
            Projectile projectileScript = projectileGO.GetComponent<Projectile>();
            if (projectileScript != null)
            {
                projectileScript.InitializeAsStraight(fireDirection, weaponToFire.baseLaunchForce, weaponToFire.baseDirectDamage, true, weaponToFire.basePierceCount, weaponToFire.baseProjectileLifetime, weaponToFire.shieldImpactEffectPrefab, weaponToFire.defaultImpactEffectPrefab, weaponToFire.baseDotDamage, weaponToFire.baseDotDuration, weaponToFire.dotTickInterval, weaponToFire.baseSlowPercentage, weaponToFire.baseSlowDuration, AttackType.Standard);
            }
            currentAngle += angleStep;
        }
    }
    public void TriggerWindupEffect()
    {
        if (windupEffectPrefab != null)
        {
            Transform spawnPoint = (firePoint != null) ? firePoint : selfTransform;
            Instantiate(windupEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
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