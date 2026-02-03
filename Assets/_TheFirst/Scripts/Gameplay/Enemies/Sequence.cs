// --- Sequence.cs (带记忆功能的最终版) ---
using System.Collections.Generic;
using UnityEngine;

public class Sequence : Node
{
    protected List<Node> children = new List<Node>();
    private int currentChildIndex = 0; // 【核心】用于“记忆”当前正在运行的子节点索引

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
        // 如果有子节点，则从当前“记忆”的索引开始执行
        if (currentChildIndex < children.Count)
        {
            Node currentNode = children[currentChildIndex];
            switch (currentNode.Evaluate())
            {
                case NodeState.RUNNING:
                    // 子节点还在运行，保持当前状态，下一帧继续从这个子节点评估
                    return NodeState.RUNNING;

                case NodeState.SUCCESS:
                    // 子节点成功，准备评估下一个子节点
                    currentChildIndex++;
                    // 如果这已经是最后一个子节点，则整个序列成功
                    if (currentChildIndex >= children.Count)
                    {
                        Reset(); // 重置索引，以便下次序列可以从头开始
                        return NodeState.SUCCESS;
                    }
                    // 否则，因为本帧已经成功一个，我们立即尝试运行下一个，这能让瞬时完成的节点串联起来
                    // 通过递归调用自己来实现这一点
                    return Evaluate();

                case NodeState.FAILURE:
                    // 任何一个子节点失败，则整个序列失败
                    Reset(); // 重置索引
                    return NodeState.FAILURE;
            }
        }

        // 如果没有子节点或所有子节点都已成功，则默认成功
        Reset();
        return NodeState.SUCCESS;
    }

    // 一个用于重置状态的辅助方法
    private void Reset()
    {
        currentChildIndex = 0;
    }
}