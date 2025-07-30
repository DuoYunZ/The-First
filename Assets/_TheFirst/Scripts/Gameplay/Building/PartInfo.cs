// PartInfo.cs
using UnityEngine;

public class PartInfo : MonoBehaviour
{
    // 用于跨场景保存和加载预制件的标识符
    public string PrefabName;

    // 用于在移除零件时，重新激活它所连接的吸附点
    // 类型可能是 Transform, GameObject, 或者你自定义的 AttachmentPoint/PartContactPoint 类型
    // 假设它是 GameObject (代表那个吸附点游戏对象)
    //public GameObject connectedToPoint;
    // 或者，如果它是一个 Transform:
     public Transform connectedToPoint;
    // 或者，如果它是一个特定的脚本组件实例:
    // public AttachmentPoint connectedToPointScript; // (AttachmentPoint 是你吸附点脚本的类名)

    // 你还可以有其他零件相关信息
    // public string partDisplayName;
    // public PartType partType; // 假设有 PartType 枚举
    // public int healthBonus;
    // ...等等
}