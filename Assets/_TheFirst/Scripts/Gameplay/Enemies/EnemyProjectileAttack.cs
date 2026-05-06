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

        bool wasInRange = isInAttackRange;
        isInAttackRange = distanceToPlayer <= attackRange;

        if (isInAttackRange)
        {
            // --- 刚进入攻击范围时，通知 EnemyAI 让出控制权 ---
            if (!wasInRange)
            {
                if (enemyAI != null) enemyAI.SetRangedAttackingState(true);
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                animator.SetBool("isMoving", false);
            }

            // 持续、平滑地转向玩家
            Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

            // 检查冷却并触发攻击协程
            if (attackCooldownTimer <= 0 && !isAttacking)
            {
                attackCoroutine = StartCoroutine(AttackSequence());
            }
        }
        else
        {
            // --- 离开攻击范围且不在攻击中时，归还控制权给 EnemyAI ---
            if (wasInRange && !isAttacking)
            {
                if (enemyAI != null) enemyAI.SetRangedAttackingState(false);
            }
            // 攻击结束后也需要归还（攻击中离开范围的情况在 AttackSequence 结束时处理）
            else if (!isAttacking && enemyAI != null && enemyAI.CurrentState == EnemyAI.AIState.RangedAttacking)
            {
                enemyAI.SetRangedAttackingState(false);
            }
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

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        // 立即重置状态
        isAttacking = false;
        attackCoroutine = null;
        attackCooldownTimer = 1f / fireRate; // 让它进入冷却

        // 归还 EnemyAI 控制权
        if (enemyAI != null) enemyAI.SetRangedAttackingState(false);

        // 重置动画器
        if (animator != null)
        {
            animator.ResetTrigger("Attack");
        }
    }
}