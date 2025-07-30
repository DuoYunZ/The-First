using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Animator))]
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
    private Rigidbody rb;
    private bool isInAttackRange = false; // 用于跟踪玩家是否在攻击范围内

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
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (playerTarget == null) return;

        attackCooldownTimer -= Time.deltaTime;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        isInAttackRange = distanceToPlayer <= attackRange;

        if (isInAttackRange)
        {
            // --- 【核心修改 1】玩家在攻击范围内 ---
            // 1. 禁用基础AI，由本脚本接管
            if (enemyAI.enabled)
            {
                enemyAI.enabled = false;
                rb.velocity = Vector3.zero;
                animator.SetBool("isMoving", false);
            }

            // 2. 持续、平滑地转向玩家
            Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

            // 3. 检查冷却并触发攻击协程
            if (attackCooldownTimer <= 0)
            {
                StartCoroutine(AttackSequence());
            }
        }
        else
        {
            // --- 玩家在攻击范围外 ---
            if (!enemyAI.enabled)
            {
                enemyAI.enabled = true; // 恢复移动AI
            }
        }
    }

    IEnumerator AttackSequence()
    {
        // 【核心修改 2】攻击协程现在只负责攻击动作本身
        attackCooldownTimer = 1f / fireRate; // 在攻击流程开始时就重置冷却

        // 1. 触发攻击动画
        // 我们需要在Animator中创建一个名为 "Attack" 的Trigger参数
        animator.SetTrigger("Attack");

        // 2. 等待攻击动画播放
        yield return new WaitForSeconds(attackAnimationDuration);

        // 3. 动画播放完毕后，发射子弹
        if (playerTarget != null && isInAttackRange) // 增加检查，如果玩家在动画期间跑掉了，就不发射
        {
            // 发射方向基于发射点的实时朝向
            Vector3 finalDirection = (playerTarget.position - firePoint.position).normalized;
            GameObject bullet = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(finalDirection));
            Projectile projectileScript = bullet.GetComponent<Projectile>();

            if (projectileScript != null)
            {
                // 【核心修改】将两个特效都传递给子弹
                projectileScript.InitializeAsStraight(
                    finalDirection, projectileSpeed, projectileDamage, true, 1, 5f,
                    this.shieldHitVfxPrefab,   // 护盾特效
                    this.defaultHitVfxPrefab,  // 常规特效
                    0, 0, 0,
                    0, 0,
                    projectileAttackType
                );
            }
        }
    }
}