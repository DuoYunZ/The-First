using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyAI))]
public class EnemyAdvancedAoeAttack : MonoBehaviour
{
    // 【新增】定义目标模式的枚举
    public enum AoeTargetingMode { Self, Player }

    [Header("攻击设置")]
    [Tooltip("我们制作的那个带内外圈的预警特效预制件")]
    public GameObject aoeIndicatorPrefab;
    [Tooltip("技能命中时的爆炸或法术特效")]
    public GameObject explosionVfxPrefab;
    [Tooltip("进入此范围后，怪物会开始准备攻击")]
    public float attackRange = 20f;
    [Tooltip("攻击冷却时间（秒）")]
    public float cooldown = 5f;

    [Header("技能效果")]
    [Tooltip("预警的持续时间，也是内圈扩张的时间")]
    public float warningDuration = 1.5f;
    [Tooltip("技能的伤害半径")]
    public float aoeRadius = 3f;
    [Tooltip("技能造成的伤害值")]
    public int aoeDamage = 25;

    [Header("预警位置模式")]
    [Tooltip("【新增】选择预警圈的中心点是怪物自身还是玩家")]
    public AoeTargetingMode targetingMode = AoeTargetingMode.Self;
    [Tooltip("预警圈生成的偏移位置列表。坐标会基于上面选择的目标模式来计算。")]
    public List<Vector3> aoePatternOffsets;

    // 私有变量
    private Transform playerTarget;
    private float attackCooldownTimer;
    private EnemyAI enemyAI;
    private Animator animator;
    private bool isCasting = false;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            // 注意：我们现在只需要玩家的根Transform即可，不再需要AimTarget
            playerTarget = GameManager.Instance.playerTransform;
        }
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (playerTarget == null || isCasting || aoePatternOffsets.Count == 0) return;

        attackCooldownTimer -= Time.deltaTime;

        if (Vector3.Distance(transform.position, playerTarget.position) <= attackRange && attackCooldownTimer <= 0)
        {
            StartCoroutine(AoeSequence());
            attackCooldownTimer = cooldown;
        }
    }

    IEnumerator AoeSequence()
    {
        isCasting = true;

        if (enemyAI.enabled)
        {
            enemyAI.enabled = false;
            // 如果有刚体，确保速度为零
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.zero;

            // 确保动画状态立即切换到待机
            if (animator != null) animator.SetBool("isMoving", false);
        }

        // 面朝玩家
        transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));

        // 【动画修复第二步】触发“施法”动画
        if (animator != null)
        {
            animator.SetTrigger("Attack"); // 假设您的施法触发器名为 "Attack"
        }

        // 【核心修改】根据目标模式，确定攻击模式的中心点
        Vector3 patternCenter;
        if (targetingMode == AoeTargetingMode.Player)
        {
            patternCenter = playerTarget.position; // 以玩家为中心
        }
        else // AoeTargetingMode.Self
        {
            patternCenter = transform.position; // 以怪物自身为中心
        }

        // 批量生成预警圈
        List<Vector3> explosionPositions = new List<Vector3>();
        foreach (Vector3 offset in aoePatternOffsets)
        {
            Vector3 worldPosition;
            // 如果是以自身为中心，我们希望偏移量能跟随怪物自身的朝向旋转
            if (targetingMode == AoeTargetingMode.Self)
            {
                worldPosition = patternCenter + transform.rotation * offset;
            }
            else // 如果以玩家为中心，偏移量通常使用世界坐标，不受怪物朝向影响
            {
                worldPosition = patternCenter + offset;
            }

            // 对齐到地面
            RaycastHit hit;
            if (Physics.Raycast(worldPosition + Vector3.up * 5f, Vector3.down, out hit, 10f, LayerMask.GetMask("Ground")))
            {
                worldPosition = hit.point;
            }

            explosionPositions.Add(worldPosition);

            if (aoeIndicatorPrefab != null)
            {
                GameObject indicator = Instantiate(aoeIndicatorPrefab, worldPosition, Quaternion.identity);
                indicator.GetComponent<AoeIndicatorController>()?.Animate(warningDuration, aoeRadius);
            }
        }

        // 等待预警时间结束
        yield return new WaitForSeconds(warningDuration);

        // 在记录的所有位置产生爆炸和伤害
        foreach (Vector3 pos in explosionPositions)
        {
            if (explosionVfxPrefab != null) Instantiate(explosionVfxPrefab, pos, Quaternion.identity);

            if (Vector3.Distance(playerTarget.position, pos) <= aoeRadius)
            {
                playerTarget.GetComponent<Health>()?.TakeDamage(aoeDamage, pos, this.gameObject, AttackType.Standard);
            }
        }

        if (!enemyAI.enabled)
        {
            enemyAI.enabled = true;
        }
        isCasting = false;
    }
}