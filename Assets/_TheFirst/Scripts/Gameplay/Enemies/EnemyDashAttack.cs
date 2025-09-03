using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyDashAttack : MonoBehaviour
{
    [Header("冲刺设置")]
    [Tooltip("地面冲刺路径的预警特效")]
    public GameObject dashWarningPrefab;
    [Tooltip("进入此范围后，怪物会开始准备冲刺")]
    public float attackRange = 12f;
    [Tooltip("冲刺冷却时间（秒）")]
    public float cooldown = 6f;
    [Tooltip("预警特效显示时长（秒）")]
    public float warningDuration = 1.2f;
    [Tooltip("冲刺时的移动速度")]
    public float dashSpeed = 30f;
    [Tooltip("冲刺的最大距离")]
    public float dashDistance = 15f;
    [Tooltip("冲刺时造成的伤害")]
    public int dashDamage = 20;
    [Tooltip("冲刺路径的宽度")]
    public float dashWidth = 2f;

    [Header("【新增】特效预制件")]
    [Tooltip("冲刺启动时在脚下生成的烟尘特效")]
    public GameObject dashDustEffectPrefab;
    [Tooltip("冲刺过程中跟随身体的持续速度线/拖尾特效")]
    public GameObject dashSpeedEffectPrefab;


    // 私有变量
    private Transform playerTarget;
    private float attackCooldownTimer;
    private EnemyAI enemyAI;
    private Rigidbody rb;
    private bool isAttacking = false;
    private Collider damageCollider; // 用于在冲刺时激活的伤害碰撞体
    private Animator animator;
    private GameObject currentDashSpeedEffectInstance; // 【新增】用于存储持续特效的实例

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerAimTarget;
        }
        else
        {
            Debug.LogError("EnemyDashAttack: 未能找到 GameManager 或玩家引用！", this);
            enabled = false;
        }

        enemyAI = GetComponent<EnemyAI>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>(); // 【新增】在Start中获取Animator

        // 最好有一个专门的碰撞体用于冲刺伤害，避免与主碰撞体冲突
        // 你可以为怪物创建一个子对象，挂载一个BoxCollider(设为IsTrigger)，并在这里获取它
        damageCollider = transform.Find("DashDamageTrigger")?.GetComponent<Collider>();
        if(damageCollider != null) damageCollider.enabled = false;
    }

    void Update()
    {
        if (playerTarget == null || isAttacking) return;

        attackCooldownTimer -= Time.deltaTime;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0)
        {
            StartCoroutine(DashSequence());
            attackCooldownTimer = cooldown;
        }
    }

    IEnumerator DashSequence()
    {
        isAttacking = true;

        // 1. 攻击前置动作：停止AI，面朝玩家
        enemyAI.enabled = false;
        rb.velocity = Vector3.zero;
        if (animator != null) animator.SetBool("isMoving", false);

        Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
        transform.rotation = targetRotation;

        // 【核心修改 A】触发“前摇”动画
        if (animator != null)
        {
            animator.SetTrigger("doWarning");
        }


        // 2. 【核心修改】生成动态预警并调用其动画
        if (dashWarningPrefab != null)
        {
            // 依然在怪物前方生成预警
            Vector3 warningPosition = transform.position + transform.forward * (dashDistance / 2f); // 中心点在冲刺路径的中间
                                                                                                    // ... (贴地逻辑可以保留) ...

            GameObject warningIndicator = Instantiate(dashWarningPrefab, warningPosition, transform.rotation);

            // 获取控制器并调用动画
            DashIndicatorController indicatorController = warningIndicator.GetComponent<DashIndicatorController>();
            if (indicatorController != null)
            {
                // 传入预警时长和冲刺的宽度/长度
                indicatorController.Animate(warningDuration, this.dashWidth, this.dashDistance);
            }
        }

        // 3. 等待预警（填充动画）播放完毕
        yield return new WaitForSeconds(warningDuration);

        // 4. 冲锋！
        // (可选) 触发“冲刺”动画: animator.SetTrigger("Dash");
        if (animator != null)
        {
            animator.SetTrigger("doAttack");
        }

        
        if (dashDustEffectPrefab != null)
        {
            Instantiate(dashDustEffectPrefab, transform.position, transform.rotation);
        }

        
        if (dashSpeedEffectPrefab != null)
        {
            currentDashSpeedEffectInstance = Instantiate(dashSpeedEffectPrefab, transform.position, transform.rotation, transform);
        }

        float dashDuration = dashDistance / dashSpeed;
        rb.velocity = transform.forward * dashSpeed;

        // 5. 冲锋结束后停止
        yield return new WaitForSeconds(dashDuration);
        rb.velocity = Vector3.zero;

        if (currentDashSpeedEffectInstance != null)
        {
            Destroy(currentDashSpeedEffectInstance);
        }
        // 6. 攻击结束，恢复AI
        enemyAI.enabled = true;
        isAttacking = false;
    }

    // (推荐) 如果你使用了独立的伤害触发器，需要在这里处理伤害逻辑

    void OnTriggerEnter(Collider other)
    {
        // 您的脚本可能使用 isAttacking 或 damageCollider.enabled 来判断是否在攻击中
        // 我们保留原有逻辑，只修改TakeDamage的调用
        if (isAttacking && other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null)
            {
                // 【核心修改】攻击类型设置为 ShieldBreaking！
                playerHealth.TakeDamage(dashDamage, transform.position, this.gameObject, AttackType.ShieldBreaking);

                // 冲刺通常只造成一次伤害
                if (enemyAI != null)
                {
                    enemyAI.TriggerDamageCooldown();
                }
            }
        }
    }
}