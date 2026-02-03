// --- Parallel.cs ---
using System.Collections.Generic;
using UnityEngine;

public class Parallel : Node
{
    protected List<Node> children = new List<Node>();

    void Awake()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Node>() != null)
            {
                children.Add(child.GetComponent<Node>());
            }
        }
    }

    public override NodeState Evaluate()
    {
        if (children.Count == 0) return NodeState.SUCCESS;

        int successCount = 0;
        bool anyChildIsRunning = false;

        // 在每一帧，我们都需要评估所有的子节点，以保证它们都能被更新
        foreach (Node node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.RUNNING:
                    // 只要有任何一个子节点还在运行，就标记一下
                    anyChildIsRunning = true;
                    break;
                case NodeState.SUCCESS:
                    // 记录成功的子节点数量
                    successCount++;
                    break;
                case NodeState.FAILURE:
                    // 如果任何一个子节点失败了，则整个并行节点立即失败
                    // 这是一个严格的策略，也可以根据需要修改
                    ResetChildren(); // 重置所有子节点的状态
                    return NodeState.FAILURE;
            }
        }

        // 只有当所有子节点都成功时，整个并行节点才算成功
        if (successCount == children.Count)
        {
            ResetChildren();
            return NodeState.SUCCESS;
        }

        // 只要还有任何一个节点在运行，就继续返回RUNNING
        return anyChildIsRunning ? NodeState.RUNNING : NodeState.SUCCESS;
    }

    // 当并行节点结束（无论是成功还是失败）时，重置所有子节点的状态
    // 这是一个好习惯，可以防止状态污染
    private void ResetChildren()
    {
        foreach (Node node in children)
        {
            // 这里我们假设Action节点内部有自己的重置逻辑（比如我们做的currentState = Ready）
            // 如果需要更严格的重置，可以为Node基类添加一个OnReset()虚方法
        }
    }
}