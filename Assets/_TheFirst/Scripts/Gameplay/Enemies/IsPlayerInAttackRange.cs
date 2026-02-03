// --- IsPlayerInAttackRange.cs (修正版) ---
using UnityEngine;

public class IsPlayerInAttackRange : Node
{
    [Header("条件设置")]
    [Tooltip("定义多近算是'攻击范围'")]
    public float attackRange = 12f;

    private Transform playerTarget;
    private Transform selfTransform; // Boss根对象的Transform

    void Awake()
    {
        // 【核心修正】使用GetComponentInParent来安全、可靠地找到Boss的刚体，从而获取其根Transform
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null)
        {
            selfTransform = bossRb.transform;
        }
        else
        {
            // 如果找不到，这会是一个明确的错误提示
            Debug.LogError("IsPlayerInAttackRange 无法在父级中找到 Rigidbody 组件!", this);
        }
    }

    public override NodeState Evaluate()
    {
        // 如果没有成功获取到自身Transform，或找不到玩家，则条件不成立
        if (selfTransform == null) return NodeState.FAILURE;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }
        else
        {
            return NodeState.FAILURE;
        }

        // 检查距离
        if (Vector3.Distance(selfTransform.position, playerTarget.position) <= attackRange)
        {
            // 在范围内，条件成立，返回成功
            return NodeState.SUCCESS;
        }
        else
        {
            // 不在范围内，条件不成立，返回失败
            return NodeState.FAILURE;
        }
    }
}