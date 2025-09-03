// --- Selector.cs (带记忆功能的最终版) ---
using System.Collections.Generic;
using UnityEngine;

public class Selector : Node
{
    protected List<Node> children = new List<Node>();
    private int currentChildIndex = 0; // 【核心】用于“记忆”当前正在尝试的子节点索引

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
        if (currentChildIndex < children.Count)
        {
            Node currentNode = children[currentChildIndex];
            switch (currentNode.Evaluate())
            {
                case NodeState.RUNNING:
                    // 如果子节点正在运行，则整个Selector也处于运行状态，并“记住”是哪个子节点
                    return NodeState.RUNNING;

                case NodeState.SUCCESS:
                    // 任何一个子节点成功，则整个Selector立即成功
                    Reset(); // 重置索引，以便下次决策可以从头开始
                    return NodeState.SUCCESS;

                case NodeState.FAILURE:
                    // 【核心修正】如果子节点失败了，则尝试下一个
                    currentChildIndex++;
                    // 如果还有更多子节点，则立即在本帧尝试下一个
                    if (currentChildIndex < children.Count)
                    {
                        return Evaluate(); // 通过递归调用自己来实现
                    }
                    else
                    {
                        // 所有子节点都失败了，则整个Selector才算失败
                        Reset();
                        return NodeState.FAILURE;
                    }
            }
        }

        // 如果没有子节点，则默认失败
        Reset();
        return NodeState.FAILURE;
    }

    // 重置状态
    private void Reset()
    {
        currentChildIndex = 0;
    }
}