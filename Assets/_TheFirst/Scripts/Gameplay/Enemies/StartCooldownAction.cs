// --- StartCooldownAction.cs ---
using UnityEngine;

public class StartCooldownAction : Node
{
    [Header("冷却触发器")]
    public string attackName;
    public float cooldownDuration;

    public override NodeState Evaluate()
    {
        BehaviorTree behaviorTree = GetComponentInParent<BehaviorTree>();
        if (behaviorTree != null && !string.IsNullOrEmpty(attackName))
        {
            behaviorTree.StartCooldown(attackName, cooldownDuration);
        }
        // 这个动作是瞬时完成的
        return NodeState.SUCCESS;
    }
}