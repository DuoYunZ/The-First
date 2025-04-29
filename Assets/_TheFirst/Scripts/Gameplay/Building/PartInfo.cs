using UnityEngine;

/// <summary>
/// 存储已放置零件的附加信息，例如它连接到的连接点。
/// </summary>
public class PartInfo : MonoBehaviour
{
    // 引用当初放置此零件时所连接到的那个目标连接点 (AttachmentPoint 或 PartContactPoint)
    public Transform connectedToPoint;
    // (未来可以添加更多信息，比如零件的健康值、状态等)
}