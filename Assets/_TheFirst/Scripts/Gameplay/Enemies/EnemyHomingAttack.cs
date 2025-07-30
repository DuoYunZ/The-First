using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Animator))] // 【新增】确保怪物有Animator组件
public class EnemyHomingAttack : MonoBehaviour
{
    [Header("攻击设置")]
    public GameObject homingProjectilePrefab;
    public Transform firePoint;
    public float attackRange = 20f;
    [Tooltip("攻击频率（次/秒）")]
    public float fireRate = 0.3f;
    [Tooltip("攻击动画的持续时间（秒）。子弹会在这段时间后发射。")]
    public float attackAnimationDuration = 1.2f; // 新增：攻击前摇/动画时长
    [Tooltip("怪物在攻击范围内转向玩家的速度")]
    public float turnSpeed = 5f;

    [Header("子弹属性")]
    public int projectileDamage = 15;
    public float projectileSpeed = 15f;
    [Tooltip("子弹的转向速度，数值越大转弯越急")]
    public float homingTurnSpeed = 8f;

    [Header("视觉效果")]
    [Tooltip("子弹命中【玩家护盾】时的专属特效")]
    public GameObject shieldHitVfxPrefab;
    [Tooltip("子弹命中【无护盾玩家】时的通用特效")]
    public GameObject defaultHitVfxPrefab;

    // 私有变量
    private Transform playerTarget;
    private float attackCooldownTimer;
    private EnemyAI enemyAI;
    private Animator animator; // 【新增】动画控制器引用
    private Rigidbody rb;
    private bool isInAttackRange = false;
    private bool isInAttackSequence = false;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerAimTarget;
        }
        else
        {
            Debug.LogError("EnemyHomingAttack: 未能找到 GameManager 或玩家引用！", this);
            enabled = false;
        }
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (playerTarget == null || isInAttackSequence) return;

        attackCooldownTimer -= Time.deltaTime;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        isInAttackRange = distanceToPlayer <= attackRange;

        if (isInAttackRange)
        {
            // --- 玩家在攻击范围内 ---
            if (enemyAI.enabled)
            {
                enemyAI.enabled = false;
                rb.velocity = Vector3.zero;
                animator.SetBool("isMoving", false);
            }

            // 持续、平滑地转向玩家
            Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

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
                enemyAI.enabled = true;
            }
        }
    }

    IEnumerator AttackSequence()
    {
        isInAttackSequence = true;
        attackCooldownTimer = 1f / fireRate;

        // 1. 触发攻击动画
        animator.SetTrigger("Attack");

        // 2. 等待攻击动画播放
        yield return new WaitForSeconds(attackAnimationDuration);

        // 3. 动画播放完毕后，发射子弹
        if (playerTarget != null && isInAttackRange)
        {
            GameObject bullet = Instantiate(homingProjectilePrefab, firePoint.position, firePoint.rotation);
            Projectile projectileScript = bullet.GetComponent<Projectile>();

            if (projectileScript != null)
            {
                // 调用追踪弹的初始化方法
                projectileScript.InitializeAsHoming(playerTarget, projectileSpeed, projectileDamage, true, homingTurnSpeed,  8f, this.shieldHitVfxPrefab, this.defaultHitVfxPrefab);
            }
        }

        isInAttackSequence = false;
    }
}