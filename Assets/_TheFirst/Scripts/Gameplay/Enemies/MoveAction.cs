// --- MoveAction.cs ---
// 行为树移动节点：让BOSS向玩家移动、远离玩家或随机方向移动
using UnityEngine;
using UnityEngine.AI;

public class MoveAction : Node
{
    public enum MoveDirection
    {
        TowardsPlayer,  // 向玩家移动
        AwayFromPlayer, // 远离玩家
        Random          // 随机方向
    }

    [Header("移动设置")]
    [Tooltip("移动方向策略")]
    public MoveDirection moveDirection = MoveDirection.TowardsPlayer;

    [Tooltip("移动持续时间（秒）")]
    public float moveDuration = 2f;

    [Tooltip("移动速度倍率（基于NavMeshAgent的基础速度）")]
    public float speedMultiplier = 1f;

    [Tooltip("远离玩家时的目标距离（仅 AwayFromPlayer 模式）")]
    public float retreatDistance = 10f;

    [Tooltip("随机移动的范围半径（仅 Random 模式）")]
    public float randomMoveRadius = 8f;

    // 内部状态
    private NavMeshAgent agent;
    private EnemyAI regularAI;
    private Animator animator;
    private Transform selfTransform;
    private float timer;
    private bool isMoving = false;
    private float originalSpeed;

    void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        regularAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>();

        Rigidbody rb = GetComponentInParent<Rigidbody>();
        if (rb != null) selfTransform = rb.transform;
    }

    public override NodeState Evaluate()
    {
        if (agent == null || selfTransform == null) return NodeState.FAILURE;

        // 初始化移动
        if (!isMoving)
        {
            isMoving = true;
            timer = 0f;

            // 禁用常规AI追击，由MoveAction接管移动
            if (regularAI != null) regularAI.enabled = false;

            // 保存原速度并应用倍率
            originalSpeed = agent.speed;
            agent.speed = originalSpeed * speedMultiplier;
            agent.isStopped = false;

            // 设置目标位置
            Vector3 targetPos = CalculateTargetPosition();
            agent.SetDestination(targetPos);

            // 播放移动动画
            if (animator != null) animator.SetBool("isMoving", true);

            return NodeState.RUNNING;
        }

        // 持续移动
        timer += Time.deltaTime;

        // 如果到达目的地或超时，结束移动
        bool reachedDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        if (timer >= moveDuration || reachedDestination)
        {
            // 恢复状态
            isMoving = false;
            agent.speed = originalSpeed;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            // 停止移动动画
            if (animator != null) animator.SetBool("isMoving", false);

            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }

    /// <summary>
    /// 根据移动策略计算目标位置
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        Transform player = GameManager.Instance?.playerTransform;

        switch (moveDirection)
        {
            case MoveDirection.TowardsPlayer:
                if (player != null)
                {
                    return player.position;
                }
                // 如果没有玩家，随机移动
                return GetRandomNavMeshPoint();

            case MoveDirection.AwayFromPlayer:
                if (player != null)
                {
                    // 计算远离玩家的方向
                    Vector3 awayDir = (selfTransform.position - player.position).normalized;
                    awayDir.y = 0;
                    Vector3 retreatPos = selfTransform.position + awayDir * retreatDistance;

                    // 确保目标点在NavMesh上
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(retreatPos, out hit, retreatDistance, NavMesh.AllAreas))
                    {
                        return hit.position;
                    }
                }
                return GetRandomNavMeshPoint();

            case MoveDirection.Random:
                return GetRandomNavMeshPoint();

            default:
                return selfTransform.position;
        }
    }

    /// <summary>
    /// 获取随机的NavMesh上的点
    /// </summary>
    private Vector3 GetRandomNavMeshPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * randomMoveRadius;
        randomDir.y = 0;
        Vector3 candidate = selfTransform.position + randomDir;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, randomMoveRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // 如果找不到有效点，返回当前位置
        return selfTransform.position;
    }
}
