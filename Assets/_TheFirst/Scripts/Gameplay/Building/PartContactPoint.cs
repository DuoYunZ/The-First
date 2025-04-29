using UnityEngine;

/// <summary>
/// 标记一个游戏对象作为零件上的连接点 (接触点)。
/// 用于 MechBuilder 的吸附和连接逻辑。
/// </summary>
public class PartContactPoint : MonoBehaviour
{
    [Header("连接点设置")]
    [Tooltip("连接点的类型，可用于匹配兼容的连接。例如: Structural, Power, Small, Large")]
    public string pointType = "Structural"; // 默认类型为 "Structural"

    [Tooltip("连接成功后，是否禁用此接触点自身？")]
    public bool disableSelfOnConnect = true;

    [Tooltip("连接成功后，是否也尝试禁用目标连接点？(如果目标点也是 PartContactPoint)")]
    public bool disableTargetOnConnect = true; // 通常成对禁用

    // --- 未来可扩展的属性 ---
    // [Header("高级对齐 (可选)")]
    // [Tooltip("定义此连接点的局部“向上”方向，用于更精确的旋转对齐")]
    // public Vector3 localUp = Vector3.up;
    // [Tooltip("定义此连接点的局部“向前”或“朝外”方向")]
    // public Vector3 localForward = Vector3.forward;

    // 可以在 Start 或 Awake 中添加一些验证逻辑 (可选)
    // void Start()
    // {
    //     // 可以在这里检查是否附加在正确的游戏对象层级等
    // }

    // (可选) 在编辑器中绘制 Gizmo 以便可视化
    void OnDrawGizmos()
    {
        // 绘制一个小的坐标轴来显示连接点的朝向
        float gizmoSize = 0.1f; // Gizmo 大小
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        // 绘制局部坐标轴: X (红色), Y (绿色), Z (蓝色)
        Gizmos.color = Color.red;   // X 轴
        Gizmos.DrawRay(pos, rot * Vector3.right * gizmoSize);
        Gizmos.color = Color.green; // Y 轴 (通常视为“向上”)
        Gizmos.DrawRay(pos, rot * Vector3.up * gizmoSize);
        Gizmos.color = Color.blue;  // Z 轴 (通常视为“向前”/“朝外”)
        Gizmos.DrawRay(pos, rot * Vector3.forward * gizmoSize);

        // 可以再画一个小球体标记位置
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, gizmoSize * 0.2f);
    }
}