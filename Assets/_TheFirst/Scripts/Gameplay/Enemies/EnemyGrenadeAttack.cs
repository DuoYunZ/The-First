using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人投掷榴弹攻击：朝玩家投掷抛物线弹体，目标点提前出现预警圈，落地后爆炸造成范围伤害
/// 榴弹使用独立的 GrenadeFlyer 组件，敌人死亡后榴弹仍能正常飞行和爆炸
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class EnemyGrenadeAttack : MonoBehaviour
{
    [Header("投掷物设置")]
    [Tooltip("榴弹预制件（会沿抛物线飞行）")]
    public GameObject grenadePrefab;
    [Tooltip("投掷物的生成位置（如怪物手部骨骼）。不设置则使用怪物自身位置")]
    public Transform throwPoint;
    [Tooltip("抛物线最高点的高度")]
    public float arcHeight = 5f;

    [Header("攻击设置")]
    [Tooltip("进入此范围后，怪物会开始投掷")]
    public float attackRange = 15f;
    [Tooltip("攻击冷却时间（秒）")]
    public float cooldown = 4f;
    [Tooltip("预警持续时间（榴弹飞行时间 = 预警时间）")]
    public float warningDuration = 1.2f;

    [Header("爆炸设置")]
    [Tooltip("爆炸半径")]
    public float explosionRadius = 3f;
    [Tooltip("爆炸伤害")]
    public int explosionDamage = 30;
    [Tooltip("爆炸特效预制件")]
    public GameObject explosionVfxPrefab;

    [Header("预警设置")]
    [Tooltip("预警圈预制件（需要有 AoeIndicatorController 脚本）")]
    public GameObject warningIndicatorPrefab;

    [Header("目标偏移")]
    [Tooltip("目标位置的随机偏移范围（增加不可预测性）")]
    public float targetRandomOffset = 1.5f;

    [Header("连续投弹设置")]
    [Tooltip("每次攻击连续投掷的榴弹数量")]
    public int burstCount = 1;
    [Tooltip("连续投弹时每颗之间的间隔（秒）")]
    public float burstInterval = 0.5f;
    [Tooltip("投掷动画触发器名称")]
    public string attackTriggerName = "doAttack";

    // 私有变量
    private Transform playerTarget;
    private float attackCooldownTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private bool isCasting = false;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // 如果没有指定投掷点，使用自身位置
        if (throwPoint == null)
        {
            throwPoint = transform;
        }
    }

    void Update()
    {
        if (playerTarget == null || isCasting) return;

        attackCooldownTimer -= Time.deltaTime;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        if (distToPlayer <= attackRange && attackCooldownTimer <= 0)
        {
            StartCoroutine(GrenadeAttackSequence());
            attackCooldownTimer = cooldown;
        }
    }

    IEnumerator GrenadeAttackSequence()
    {
        isCasting = true;

        // 1. 停下移动
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null) animator.SetBool("isMoving", false);

        // 2. 连续投弹循环
        for (int i = 0; i < burstCount; i++)
        {
            // 面朝玩家（每次重新瞄准）
            if (playerTarget != null)
            {
                Vector3 lookTarget = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
                transform.LookAt(lookTarget);
            }

            // 播放投掷动画
            if (animator != null)
            {
                animator.SetTrigger(attackTriggerName);
            }

            // 计算目标位置（玩家当前位置 + 随机偏移）
            Vector3 targetPos = playerTarget.position;
            if (targetRandomOffset > 0)
            {
                Vector2 randomCircle = Random.insideUnitCircle * targetRandomOffset;
                targetPos += new Vector3(randomCircle.x, 0, randomCircle.y);
            }

            // 对齐到地面
            RaycastHit groundHit;
            if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out groundHit, 20f, LayerMask.GetMask("Ground")))
            {
                targetPos = groundHit.point;
            }
            else
            {
                targetPos.y = playerTarget.position.y;
            }

            // 生成预警圈
            if (warningIndicatorPrefab != null)
            {
                GameObject indicator = Instantiate(warningIndicatorPrefab, targetPos, Quaternion.identity);
                AoeIndicatorController indicatorCtrl = indicator.GetComponent<AoeIndicatorController>();
                if (indicatorCtrl != null)
                {
                    indicatorCtrl.Animate(warningDuration, explosionRadius);
                }
                else
                {
                    Destroy(indicator, warningDuration);
                }
            }

            // 生成榴弹（独立飞行）
            Vector3 startPos = throwPoint.position;
            if (grenadePrefab != null)
            {
                GameObject grenadeInstance = Instantiate(grenadePrefab, startPos, Quaternion.identity);
                GrenadeFlyer flyer = grenadeInstance.GetComponent<GrenadeFlyer>();
                if (flyer == null)
                {
                    flyer = grenadeInstance.AddComponent<GrenadeFlyer>();
                }
                flyer.Initialize(
                    from: startPos,
                    to: targetPos,
                    height: arcHeight,
                    duration: warningDuration,
                    radius: explosionRadius,
                    damage: explosionDamage,
                    vfxPrefab: explosionVfxPrefab,
                    attacker: this.gameObject
                );
            }

            // 如果不是最后一颗，等待间隔再投下一颗
            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        // 3. 等待最后一颗榴弹飞行结束后恢复移动
        yield return new WaitForSeconds(warningDuration + 0.2f);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        isCasting = false;
    }
}
