// --- WaitAction.cs ---
using UnityEngine;

public class WaitAction : Node
{
    [Tooltip("How long to wait before returning Success.")]
    public float duration = 1f;

    private float startTime;
    private bool isWaiting = false;

    // 【新增】获取对AI和物理的控制权
    private EnemyAI regularAI;
    private Rigidbody rb;

    void Awake()
    {
        regularAI = GetComponentInParent<EnemyAI>();
        rb = GetComponentInParent<Rigidbody>();
    }

    public override NodeState Evaluate()
    {
        if (isWaiting == false)
        {
            isWaiting = true;
            startTime = Time.time;

            // 【核心修改】在等待开始时，强制停止AI和物理移动
            if (regularAI != null) regularAI.enabled = false;
            if (rb != null) rb.velocity = Vector3.zero;

            // 【可选】在这里也可以触发待机动画
            // Animator animator = GetComponentInParent<Animator>();
            // if (animator != null) animator.SetBool("isMoving", false);

            return NodeState.RUNNING;
        }
        else
        {
            if (Time.time - startTime >= duration)
            {
                isWaiting = false;
                // 等待结束，返回成功。后续节点(比如ChaseAction)会负责重新激活AI
                return NodeState.SUCCESS;
            }
            else
            {
                return NodeState.RUNNING;
            }
        }
    }
}