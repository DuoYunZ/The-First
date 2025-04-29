using UnityEngine;

public class WheelAnimator : MonoBehaviour
{
    [Tooltip("需要旋转的轮子视觉部分的 Transform (如果不指定，则尝试获取第一个子对象)")]
    public Transform wheelVisualTransform;
    [Tooltip("轮子滚动的半径 (用于计算转速)")]
    public float wheelRadius = 0.5f; // *** 需要根据你的轮子模型大小精确设置 ***
    [Tooltip("旋转轴 (轮子模型自身的局部轴)")]
    public Vector3 rotationAxis = Vector3.right; // *** 需要根据你的轮子模型朝向设置 ***

    private Rigidbody chassisRigidbody; // 机甲主刚体

    void Start()
    {
        // 尝试自动查找父级或根级的 Rigidbody
        chassisRigidbody = GetComponentInParent<Rigidbody>();
        if (chassisRigidbody == null)
        {
            Debug.LogError("WheelAnimator 未能在父级找到 Rigidbody!", this);
            enabled = false;
            return;
        }

        // 如果没有手动指定视觉 Transform，尝试获取第一个子对象作为视觉部分
        if (wheelVisualTransform == null && transform.childCount > 0)
        {
            wheelVisualTransform = transform.GetChild(0);
            Debug.LogWarning($"WheelAnimator 未指定 wheelVisualTransform，已自动使用第一个子对象: {wheelVisualTransform.name}", this);
        }
        else if (wheelVisualTransform == null)
        {
            // 如果也没有子对象，就用自身 Transform (如果模型就在根节点)
            wheelVisualTransform = transform;
            Debug.LogWarning($"WheelAnimator 未指定 wheelVisualTransform 且无子对象，将旋转自身 Transform", this);
        }

        if (wheelRadius <= 0)
        {
            Debug.LogError("WheelAnimator 的 Wheel Radius 必须大于 0!", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (chassisRigidbody == null || wheelVisualTransform == null) return;

        // 1. 获取刚体在自身前进方向上的速度
        // Vector3 localVelocity = transform.InverseTransformDirection(chassisRigidbody.velocity);
        // float forwardSpeed = localVelocity.z; // 获取局部 Z 轴速度 (假设 Z 轴是轮子前进方向)
        // --- 或者更通用的方法：计算沿机甲前进方向的速度投影 ---
        Vector3 worldVelocity = chassisRigidbody.velocity;
        Vector3 forwardDir = chassisRigidbody.transform.forward; // 使用底盘的前进方向
        float forwardSpeed = Vector3.Dot(worldVelocity, forwardDir);


        // 2. 计算轮子周长
        float circumference = 2f * Mathf.PI * wheelRadius;

        // 3. 计算每秒需要旋转多少圈 (速度 / 周长)
        float rotationsPerSecond = (circumference > 0) ? (forwardSpeed / circumference) : 0;

        // 4. 计算每帧需要旋转的角度 (圈数 * 360度 * 时间)
        float angleDelta = rotationsPerSecond * 360f * Time.deltaTime;

        // 5. 旋转轮子的视觉 Transform
        // 使用 Space.Self 表示绕局部轴旋转
        wheelVisualTransform.Rotate(rotationAxis, angleDelta, Space.Self);
    }
}