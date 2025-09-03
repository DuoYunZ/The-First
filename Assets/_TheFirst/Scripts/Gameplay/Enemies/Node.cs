using UnityEngine;
public abstract class Node : MonoBehaviour
{
    // 节点的三种状态
    public enum NodeState
    {
        RUNNING, // 运行中
        SUCCESS, // 成功
        FAILURE  // 失败
    }

    protected NodeState state;

    // 核心评估方法，所有子类都必须实现这个方法，它包含了节点的逻辑
    public abstract NodeState Evaluate();
}