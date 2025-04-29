using UnityEngine;

public class WheelPart : MonoBehaviour
{
    [Tooltip("轮子围绕哪个局部坐标轴旋转 (例如 Vector3.right 或 Vector3.up)")]
    public Vector3 hingeAxis = Vector3.right; // *** 需要根据你的轮子模型朝向来设置 ***

    [Tooltip("是否默认启用马达驱动？")]
    public bool useMotorByDefault = true; // 如果你想通过马达驱动轮子

    [Tooltip("默认马达驱动力")]
    public float motorForce = 100f; // 示例值

    [Tooltip("默认马达目标速度")]
    public float motorTargetVelocity = 1000f; // 示例值

    // 可以在这里添加其他轮子特有的属性，如半径、摩擦系数等
}