using UnityEngine;

public class BuildCameraController : MonoBehaviour
{
    [Header("目标与控制")]
    [Tooltip("摄像机围绕旋转的目标 (你的 ChassisCore)")]
    public Transform target; // 将 ChassisCore 拖到这里
    [Tooltip("鼠标右键按下时才允许旋转视角")]
    public bool requireMouseButton = true;
    public int mouseButtonIndex = 1; // 0=左键, 1=右键, 2=中键

    [Header("距离与缩放")]
    [Tooltip("初始以及当前的摄像机与目标的距离")]
    public float distance = 5.0f;
    [Tooltip("缩放速度")]
    public float zoomSpeed = 4f;
    [Tooltip("最小距离")]
    public float minDistance = 1f;
    [Tooltip("最大距离")]
    public float maxDistance = 15f;

    [Header("旋转速度与限制")]
    [Tooltip("水平旋转速度 (X轴)")]
    public float xSpeed = 120.0f;
    [Tooltip("垂直旋转速度 (Y轴)")]
    public float ySpeed = 120.0f;
    [Tooltip("垂直角度的最小限制 (向下看)")]
    public float yMinLimit = -20f;
    [Tooltip("垂直角度的最大限制 (向上看)")]
    public float yMaxLimit = 80f;
    [Tooltip("旋转阻尼 (数值越大，停止越慢)")]
    public float rotationDamping = 3.0f; // 轻微阻尼让旋转更平滑

    // 私有变量
    private float x = 0.0f;
    private float y = 0.0f;
    private Vector3 targetPosition;

    void Start()
    {
        // 初始化角度
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        // 确保有目标
        if (target == null)
        {
            Debug.LogError("BuildCameraController: Target 未设置!", this);
            enabled = false;
            return;
        }
        targetPosition = target.position; // 初始目标位置
    }

    // 使用 LateUpdate 可以确保目标物体已经完成它所有的移动和旋转
    void LateUpdate()
    {
        if (target == null) return; // 如果目标丢失则不执行

        targetPosition = target.position; // 更新目标位置

        // 检查是否按下指定的鼠标按键 (如果 requireMouseButton 为 true)
        bool mouseButtonPressed = !requireMouseButton || Input.GetMouseButton(mouseButtonIndex);

        // 处理旋转输入
        if (mouseButtonPressed)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * 0.02f; // 乘以0.02f是为了与旧版Input Manager行为相似
            y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

            // 限制垂直角度
            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        // 计算旋转
        Quaternion targetRotation = Quaternion.Euler(y, x, 0);

        // 处理缩放输入
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);

        // 计算摄像机位置
        // 从目标点出发，后退 distance 距离
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 targetCamPosition = targetRotation * negDistance + targetPosition;

        // 应用位置和旋转 (使用 Lerp 实现平滑过渡)
        // 如果不需要平滑，可以直接赋值: transform.rotation = targetRotation; transform.position = targetCamPosition;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping * 10f); // 乘以10是为了让阻尼效果更明显
        transform.position = Vector3.Lerp(transform.position, targetCamPosition, Time.deltaTime * rotationDamping * 10f);

    }

    // 工具函数：将角度限制在 min 和 max 之间
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}