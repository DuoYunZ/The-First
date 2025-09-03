// --- WaitForAnimatorStateAction.cs ---
using UnityEngine;

public class WaitForAnimatorStateAction : Node
{
    [Header("等待设置")]
    [Tooltip("我们希望等待的Animator状态的准确名称")]
    public string targetStateName;
    [Tooltip("（可选）要检查的动画层级，0是基础层")]
    public int layerIndex = 0;
    [Tooltip("超时时间（秒），防止无限等待卡死")]
    public float timeout = 2f;

    private Animator animator;
    private float timer;
    private bool isWaiting = false;

    void Awake()
    {
        animator = GetComponentInParent<Animator>();
    }

    public override NodeState Evaluate()
    {
        if (animator == null || string.IsNullOrEmpty(targetStateName))
        {
            return NodeState.FAILURE;
        }

        if (!isWaiting)
        {
            isWaiting = true;
            timer = 0f;
        }

        // 获取当前动画状态信息
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);

        // 检查当前状态的名称是否是我们期望的
        if (stateInfo.IsName(targetStateName))
        {
            // 是期望的状态，等待成功！
            isWaiting = false;
            return NodeState.SUCCESS;
        }

        // 检查是否超时
        timer += Time.deltaTime;
        if (timer > timeout)
        {
            Debug.LogWarning($"WaitForAnimatorStateAction: 等待状态 '{targetStateName}' 超时！", this.gameObject);
            isWaiting = false;
            return NodeState.FAILURE; // 超时则失败
        }

        // 如果还没到期望状态，就继续等待
        return NodeState.RUNNING;
    }
}