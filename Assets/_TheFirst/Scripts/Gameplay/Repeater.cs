// --- Repeater.cs ---
using UnityEngine;

// 这是一个装饰节点，它只有一个子节点
public class Repeater : Node
{
    private Node child;

    void Awake()
    {
        // 自动获取唯一的子节点
        Transform firstChild = transform.GetChild(0);
        if (firstChild != null)
        {
            child = firstChild.GetComponent<Node>();
        }
    }

    public override NodeState Evaluate()
    {
        if (child == null) return NodeState.FAILURE;

        // 评估子节点
        child.Evaluate();

        // 【核心】无论子节点返回什么，我们都告诉上级“我还在运行”
        // 这就创建了一个无限循环
        return NodeState.RUNNING;
    }
}