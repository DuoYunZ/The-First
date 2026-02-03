using UnityEngine;
using UnityEngine.InputSystem; // 【新增】

public class BuildCameraController : MonoBehaviour
{
    [Header("目标与控制")]
    [Tooltip("摄像机围绕旋转的目标 (你的 ChassisCore)")]
    public Transform target; // 将 ChassisCore 拖到这里
    [Tooltip("鼠标右键按下时才允许旋转视角")]
    public bool requireMouseButton = true;
    //public int mouseButtonIndex = 1; // 0=左键, 1=右键, 2=中键

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

    private PlayerControls playerControls;

    void Awake()
    {
        playerControls = new PlayerControls();
    }
    void OnEnable()
    {
        playerControls.Builder.Enable();
    }

    void OnDisable()
    {
        playerControls.Builder.Disable();
    }

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
        if (target == null) return;

        targetPosition = target.position;

        // 【修改】检查鼠标右键是否按下 (我们复用之前创建的 SecondaryAction)
        bool mouseButtonPressed = !requireMouseButton || playerControls.Builder.SecondaryAction.IsPressed();

        if (mouseButtonPressed)
        {
            // 【修改】读取鼠标移动增量
            Vector2 lookDelta = playerControls.Builder.CameraLook.ReadValue<Vector2>();
            x += lookDelta.x * xSpeed * 0.02f;
            y -= lookDelta.y * ySpeed * 0.02f;

            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        Quaternion targetRotation = Quaternion.Euler(y, x, 0);

        // 【修改】读取鼠标滚轮输入
        float scrollValue = playerControls.Builder.CameraZoom.ReadValue<Vector2>().y;
        // 滚轮向上是正值，向下是负值。我们需要反转它以符合直觉（向上滚是拉近，距离变小）
        distance -= scrollValue * zoomSpeed * 0.01f; // 乘以一个小数让速度更可控
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 targetCamPosition = targetRotation * negDistance + targetPosition;

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping * 10f);
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