// --- FacePlayerAction.cs ---
using UnityEngine;

public class FacePlayerAction : Node
{
    private Transform playerTarget;
    private Transform selfTransform;

    void Awake()
    {
        // 同样，安全地获取Boss的根Transform
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null)
        {
            selfTransform = bossRb.transform;
        }
    }

    public override NodeState Evaluate()
    {
        if (selfTransform == null) return NodeState.FAILURE;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }
        else
        {
            return NodeState.FAILURE;
        }

        // 计算望向玩家的方向
        Vector3 direction = (playerTarget.position - selfTransform.position).normalized;
        direction.y = 0; // 保持水平

        // 创建目标旋转值并让Boss瞬间转向
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        selfTransform.rotation = targetRotation;

        // 这个动作瞬间完成，所以直接返回成功
        return NodeState.SUCCESS;
    }
}