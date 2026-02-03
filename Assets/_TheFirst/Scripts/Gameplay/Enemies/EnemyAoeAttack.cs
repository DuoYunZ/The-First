using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(NavMeshAgent))] // 新增
public class EnemyAoeAttack : MonoBehaviour
{
    [Header("攻击设置")]
    [Tooltip("范围预警特效（例如地面上的一个圆圈贴图）")]
    public GameObject warningDecalPrefab;
    [Tooltip("技能命中时的爆炸或法术特效")]
    public GameObject explosionVfxPrefab;
    [Tooltip("进入此范围后，怪物会开始准备攻击")]
    public float attackRange = 20f;
    [Tooltip("攻击冷却时间（秒）")]
    public float cooldown = 5f;
    [Tooltip("预警特效显示时长（秒）")]
    public float warningDuration = 1.5f;
    [Tooltip("技能的伤害半径")]
    public float aoeRadius = 3f;
    [Tooltip("技能造成的伤害值")]
    public int aoeDamage = 25;

    // 私有变量
    private Transform playerAimTarget; // 【修改】使用精确的瞄准点
    private Transform playerRootTransform; // 【新增】用于伤害判定的玩家根对象
    private float attackCooldownTimer;
    private EnemyAI enemyAI;
    private bool isAttacking = false;
    private Animator animator;
    private NavMeshAgent agent;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            // 【修改】同时获取瞄准点和根对象
            playerAimTarget = GameManager.Instance.playerAimTarget;
            playerRootTransform = GameManager.Instance.playerTransform;
        }
        else
        {
            Debug.LogError("EnemyAoeAttack: 未能找到 GameManager 或玩家引用！", this);
            enabled = false;
        }

        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>(); // 新增
    }

    void Update()
    {
        if (playerAimTarget == null || isAttacking) return;

        attackCooldownTimer -= Time.deltaTime;

        float distanceToPlayer = Vector3.Distance(transform.position, playerAimTarget.position);

        if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0)
        {
            StartCoroutine(AoeSequence());
            attackCooldownTimer = cooldown;
        }
    }

    IEnumerator AoeSequence()
    {
        isAttacking = true;

        // 1. 攻击前置动作
        agent.isStopped = true;
        Vector3 directionToPlayer = (playerAimTarget.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));

        if (animator != null)
        {
            animator.SetTrigger("Attack"); // 触发 "施法" 动画
        }

        // 2. 记录目标位置（使用瞄准点），但在地面上生成预警
        Vector3 targetPosition = playerAimTarget.position;
        // 将预警位置的y轴设置为与怪物自身相同，以确保它贴近地面
        targetPosition.y = transform.position.y;

        GameObject warningDecal = Instantiate(warningDecalPrefab, targetPosition, Quaternion.Euler(90, 0, 0));
        warningDecal.transform.localScale = Vector3.one * aoeRadius * 2;

        // 3. 等待预警时间
        yield return new WaitForSeconds(warningDuration);
        Destroy(warningDecal);

        // 4. 在目标位置生成爆炸特效
        if (explosionVfxPrefab != null) Instantiate(explosionVfxPrefab, targetPosition, Quaternion.identity);

        // 5. 【核心修改】进行范围伤害检测时，使用玩家的根对象（脚底）
        if (playerRootTransform != null && Vector3.Distance(playerRootTransform.position, targetPosition) <= aoeRadius)
        {
            // 如果玩家在爆炸发生时，其根对象（脚底）仍然在伤害范围内
            Health playerHealth = playerRootTransform.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(aoeDamage, targetPosition, this.gameObject, AttackType.Standard);
            }
        }

        // 6. 攻击结束，恢复移动
        agent.isStopped = false;
        isAttacking = false;
    }
}