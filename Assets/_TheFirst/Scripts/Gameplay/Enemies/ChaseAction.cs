// --- ChaseAction.cs ---
using UnityEngine;

public class ChaseAction : Node
{
    [Header("追击设置")]
    [Tooltip("追击状态持续的时间（秒）")]
    public float chaseDuration = 3f;

    // 私有变量
    private EnemyAI regularAI;
    private Animator animator; // 【新增】动画控制器引用
    private float chaseTimer;
    private bool isChasing = false;

    void Awake()
    {
        regularAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>(); // 【新增】获取Animator组件
    }

    public override NodeState Evaluate()
    {
        if (regularAI == null || animator == null) return NodeState.FAILURE;

        if (!isChasing)
        {
            isChasing = true;
            chaseTimer = 0f;
            regularAI.enabled = true;
            animator.SetBool("isMoving", true); // 【新增】开始追击时，设置isMoving为true
            return NodeState.RUNNING;
        }
        else
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= chaseDuration)
            {
                isChasing = false;
                regularAI.enabled = false;
                animator.SetBool("isMoving", false); // 【新增】追击结束时，设置isMoving为false
                return NodeState.SUCCESS;
            }
            else
            {
                return NodeState.RUNNING;
            }
        }
    }
}