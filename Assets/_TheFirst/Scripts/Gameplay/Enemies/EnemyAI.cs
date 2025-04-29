using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // 确保有 Rigidbody
public class EnemyAI : MonoBehaviour
{
    [Header("AI 设置")]
    [Tooltip("敌人移动速度")]
    public float moveSpeed = 3f;
    private Transform playerTransform = null;
    private Rigidbody rb;
    private bool canMove = false; // 是否可以开始移动 (防止 Start 时玩家还没找到)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        // 不再这里查找玩家
    }

    void FixedUpdate() // 在 FixedUpdate 中移动 Rigidbody
    {
        // 如果还没有玩家引用，尝试从 GameManager 获取
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat) // 只在战斗模式查找
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null)
                {
                    // 等待 GameManager 准备好
                    return; // 退出 FixedUpdate
                }
            }
            else
            {
                // GameManager 不可用或不是战斗状态
                return; // 退出 FixedUpdate
            }
        }

            // --- 计算朝向玩家的方向 ---
            Vector3 directionToPlayer = (playerTransform.position - rb.position); // 使用 rb.position 更准确
        directionToPlayer.y = 0; // 忽略 Y 轴差异，只在水平面移动
        directionToPlayer.Normalize(); // 获取单位方向向量

        // --- 移动敌人 ---
        // 使用 MovePosition 通常比直接设置 velocity 更稳定，尤其是在有碰撞时
        Vector3 nextPosition = rb.position + directionToPlayer * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);

        // (可选) 让敌人视觉上朝向玩家 (如果需要的话)
        // transform.LookAt(playerTransform.position); // 这可能会导致因旋转约束产生的抖动，如果冻结了旋转，这行就没用

    }
}