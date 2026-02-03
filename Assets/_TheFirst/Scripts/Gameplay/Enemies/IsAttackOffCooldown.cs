// --- IsAttackOffCooldown.cs ---
using UnityEngine;

public class IsAttackOffCooldown : Node
{
    [Header("冷却检查")]
    [Tooltip("要检查冷却状态的技能的唯一名称（需要和Action里设置的名称一致）")]
    public string attackName;

    private BehaviorTree behaviorTree;

    void Awake()
    {
        // 在启动时就找到我们的“大脑”
        behaviorTree = GetComponentInParent<BehaviorTree>();
    }

    public override NodeState Evaluate()
    {
        if (behaviorTree == null || string.IsNullOrEmpty(attackName))
        {
            return NodeState.FAILURE;
        }

        // 询问“大脑”，这个技能是否在冷却中
        if (behaviorTree.IsOnCooldown(attackName))
        {
            return NodeState.FAILURE; // 在冷却，条件不满足
        }
        else
        {
            return NodeState.SUCCESS; // 没在冷却，条件满足
        }
    }
}