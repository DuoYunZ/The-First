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

    [Tooltip("当 Agent 速度为 0 时使用的备用速度")]
    public float fallbackSpeed = 5f;

    // 内部状态
    private NavMeshAgent agent;
    private EnemyAI regularAI;
    private Animator animator;
    private Transform selfTransform;
    private float timer;
    private bool isMoving = false;
    private float originalSpeed;
    private bool wasAIDisabledByMe = false; // 记录是否是由本节点禁用的 AI

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
        if (agent == null || selfTransform == null)
        {
            Debug.LogWarning($"<color=red>[MoveAction] FAILURE: agent={agent != null}, selfTransform={selfTransform != null}, name={gameObject.name}</color>");
            return NodeState.FAILURE;
        }

        // 如果正在移动期间 Agent 被禁用（比如被击退），安全退出
        if (isMoving && !agent.enabled)
        {
            Debug.LogWarning($"<color=yellow>[MoveAction] Agent 被外部禁用（可能是击退），安全退出移动。name={gameObject.name}</color>");
            CleanupMove();
            return NodeState.FAILURE;
        }

        // 初始化移动
        if (!isMoving)
        {
            // 确保 Agent 已启用
            if (!agent.enabled)
            {
                Debug.LogWarning($"<color=yellow>[MoveAction] Agent 未启用，跳过移动。name={gameObject.name}</color>");
                return NodeState.FAILURE;
            }

            // 如果不在 NavMesh 上，尝试 Warp 回去
            if (!agent.isOnNavMesh)
            {
                if (!TryWarpToNavMesh())
                {
                    Debug.LogWarning($"<color=red>[MoveAction] Agent 不在 NavMesh 上且无法恢复! name={gameObject.name}</color>");
                    return NodeState.FAILURE;
                }
            }

            isMoving = true;
            timer = 0f;

            // 禁用常规AI追击，由MoveAction接管移动
            if (regularAI != null && regularAI.enabled)
            {
                regularAI.enabled = false;
                wasAIDisabledByMe = true;
            }
            else
            {
                wasAIDisabledByMe = false;
            }

            // 保存原速度并应用倍率
            originalSpeed = agent.speed;

            // 防止速度为 0 的情况（可能是被冻结/暂停后遗留）
            if (originalSpeed < 0.1f)
            {
                originalSpeed = fallbackSpeed;
                Debug.LogWarning($"<color=yellow>[MoveAction] 检测到 agent.speed 接近 0 ({agent.speed:F2})，使用备用速度 {fallbackSpeed}。name={gameObject.name}</color>");
            }

            agent.speed = originalSpeed * speedMultiplier;
            agent.isStopped = false;

            // 设置目标位置
            Vector3 targetPos = CalculateTargetPosition();
            agent.SetDestination(targetPos);

            // 播放移动动画
            if (animator != null) animator.SetBool("isMoving", true);

            return NodeState.RUNNING;
        }

        // 持续移动阶段 —— 做安全性检查
        if (!agent.isOnNavMesh)
        {
            // 移动途中脱离了 NavMesh（被击退弹出去了）
            Debug.LogWarning($"<color=yellow>[MoveAction] 移动途中脱离 NavMesh，尝试恢复。name={gameObject.name}</color>");
            if (TryWarpToNavMesh())
            {
                // Warp 成功，重新设置目标
                Vector3 newTarget = CalculateTargetPosition();
                agent.SetDestination(newTarget);
                agent.isStopped = false;
            }
            else
            {
                // 无法恢复，结束移动
                CleanupMove();
                return NodeState.FAILURE;
            }
        }

        timer += Time.deltaTime;

        // 如果到达目的地或超时，结束移动
        bool reachedDestination = agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        if (timer >= moveDuration || reachedDestination)
        {
            CleanupMove();
            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }

    /// <summary>
    /// 清理移动状态，恢复 AI 和 Agent
    /// </summary>
    private void CleanupMove()
    {
        isMoving = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = originalSpeed;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // 停止移动动画
        if (animator != null) animator.SetBool("isMoving", false);

        // 只恢复由本节点禁用的 AI
        if (wasAIDisabledByMe && regularAI != null)
        {
            regularAI.enabled = true;
            wasAIDisabledByMe = false;
        }
    }

    /// <summary>
    /// 尝试将 Agent Warp 到最近的 NavMesh 有效位置
    /// </summary>
    private bool TryWarpToNavMesh()
    {
        NavMeshHit hit;
        // 在当前位置附近 5 米范围内搜索有效的 NavMesh 位置
        if (NavMesh.SamplePosition(selfTransform.position, out hit, 5f, NavMesh.AllAreas))
        {
            agent.enabled = false;
            selfTransform.position = hit.position;
            agent.enabled = true;

            if (agent.isOnNavMesh)
            {
                agent.Warp(hit.position);
                Debug.Log($"<color=green>[MoveAction] 成功 Warp 回 NavMesh: {hit.position}。name={gameObject.name}</color>");
                return true;
            }
        }

        // 扩大搜索范围
        if (NavMesh.SamplePosition(selfTransform.position, out hit, 15f, NavMesh.AllAreas))
        {
            agent.enabled = false;
            selfTransform.position = hit.position;
            agent.enabled = true;

            if (agent.isOnNavMesh)
            {
                agent.Warp(hit.position);
                Debug.Log($"<color=green>[MoveAction] 扩大范围后 Warp 回 NavMesh: {hit.position}。name={gameObject.name}</color>");
                return true;
            }
        }

        return false;
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
