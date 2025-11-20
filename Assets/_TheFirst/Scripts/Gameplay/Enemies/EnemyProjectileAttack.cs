using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))] // 新增
public class EnemyProjectileAttack : MonoBehaviour
{
    [Header("攻击设置")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float attackRange = 15f;
    [Tooltip("攻击频率（次/秒）")]
    public float fireRate = 0.5f;
    [Tooltip("攻击动画的持续时间（秒）。子弹会在这段时间后发射。")]
    public float attackAnimationDuration = 1f; // <-- 取代了之前的 aimDelay
    [Tooltip("怪物在攻击范围内转向玩家的速度")]
    public float turnSpeed = 5f;

    public int projectileDamage = 10;
    public float projectileSpeed = 20f;

    [Tooltip("设置此攻击发射的子弹类型")]
    public AttackType projectileAttackType = AttackType.Standard;

    [Header("视觉效果")]
    // --- 【核心修改】两个独立的特效字段 ---
    [Tooltip("子弹命中【玩家护盾】时的专属特效")]
    public GameObject shieldHitVfxPrefab;
    [Tooltip("子弹命中【无护盾玩家】时的通用特效")]
    public GameObject defaultHitVfxPrefab;



    // 私有变量
    private Transform playerTarget;
    private float attackCooldownTimer;
    private EnemyAI enemyAI;
    private Animator animator;
    private NavMeshAgent agent; 
    private bool isInAttackRange = false; // 用于跟踪玩家是否在攻击范围内

    private Coroutine attackCoroutine;
    private bool isAttacking = false;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerAimTarget;
        }
        else
        {
            Debug.LogError("EnemyProjectileAttack: 未能找到 GameManager 或玩家引用！", this);
            enabled = false;
        }
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>(); 
    }

    void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (playerTarget == null)
        {
            // 可选：如果希望怪物停下，可以重置状态
            if (isAttacking) InterruptAttack();
            return;
        }

        attackCooldownTimer -= Time.deltaTime;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        isInAttackRange = distanceToPlayer <= attackRange;

        if (isInAttackRange)
        {
            // --- 【核心修改 1】玩家在攻击范围内 ---
            // 1. 禁用基础AI，由本脚本接管
            if (agent.isStopped == false)
            {
                agent.isStopped = true; // 修改
                agent.velocity = Vector3.zero; // 确保停稳
                animator.SetBool("isMoving", false);
            }

            // 2. 持续、平滑地转向玩家
            Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

            // 3. 检查冷却并触发攻击协程
            if (attackCooldownTimer <= 0 && !isAttacking)
            {
                // 保存协程引用，以便我们可以打断它
                attackCoroutine = StartCoroutine(AttackSequence());
            }
            // --- ^^^ [修改] ^^^ ---
        }
        else
        {
            // --- vvv [修改] vvv ---
            // (增加 isAttacking 检查，防止在攻击动画播放时恢复移动)
            if (agent.isStopped == true && !isAttacking)
            {
                agent.isStopped = false;
            }
            // --- ^^^ [修改] ^^^ ---
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true; // [!] 标记攻击开始
        attackCooldownTimer = 1f / fireRate;

        animator.SetTrigger("Attack"); //

        yield return new WaitForSeconds(attackAnimationDuration); //

        // 3. 动画播放完毕后，发射子弹
        if (playerTarget != null && isInAttackRange && agent.isActiveAndEnabled) // (增加 agent 检查)
        {
            Vector3 finalDirection = (playerTarget.position - firePoint.position).normalized;
            GameObject bullet = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(finalDirection));
            Projectile projectileScript = bullet.GetComponent<Projectile>();

            if (projectileScript != null)
            {
                projectileScript.InitializeAsStraight( //
                    finalDirection, projectileSpeed, projectileDamage, true, 1, 5f,
                    this.shieldHitVfxPrefab,
                    this.defaultHitVfxPrefab,
                    0, 0, 0,
                    0, 0,
                    projectileAttackType
                );
            }
        }

        isAttacking = false; // [!] 标记攻击结束
        attackCoroutine = null;
    }
    public void InterruptAttack()
    {
        if (!isAttacking) return; // 没有在攻击，不打断

        Debug.Log($"<color=yellow>[{gameObject.name}] Projectile Attack INTERRUPTED!</color>");

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine); // [!] 停止攻击协程
        }

        // 立即重置状态
        isAttacking = false;
        attackCoroutine = null;
        attackCooldownTimer = 1f / fireRate; // 让它进入冷却

        // 重置动画器
        if (animator != null)
        {
            animator.ResetTrigger("Attack"); //
        }

        // (我们不需要在这里设置 agent.isStopped = false, 
        //  因为 Stun/Knockback 会保持它停止，
        //  或者 Update() 会在下一帧处理它)
    }
}