// --- AreaRestrictionAction.cs (瞬发版) ---
using UnityEngine;
using System.Collections;

public class AreaRestrictionAction : Node
{
    [Header("区域限制设置")]
    [Tooltip("“墙壁”或“牢笼”的预制件")]
    public GameObject arenaWallPrefab;

    [Header("冷却设置")]
    [Tooltip("为这个一次性技能设置一个非常长的CD")]
    public float cooldownDuration = 9999f; // 默认一个极大值
    public string attackName = "TrapAttack";

    public override NodeState Evaluate()
    {
        if (arenaWallPrefab == null) return NodeState.FAILURE;

        // 【核心修改】整个方法被极大简化

        // 1. 找到玩家位置
        Transform playerTarget = GameManager.Instance?.playerTransform;
        if (playerTarget == null) return NodeState.FAILURE;

        // 2. 在玩家位置生成墙壁
        GameObject wallGO = Instantiate(arenaWallPrefab, playerTarget.position, Quaternion.identity);
        ArenaWall activeWall = wallGO.GetComponent<ArenaWall>();

        if (activeWall != null)
        {
            activeWall.Activate(); // 命令墙壁“出现”（它会自己管自己何时消失）
        }

        // 3. 立即为这个技能开启冷却
        GetComponentInParent<BehaviorTree>().StartCooldown(attackName, cooldownDuration);

        // 4. 立即报告成功，让行为树可以继续执行下一个动作
        return NodeState.SUCCESS;
    }
}