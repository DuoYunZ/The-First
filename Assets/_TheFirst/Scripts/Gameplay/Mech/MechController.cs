using UnityEngine;

// 脚本挂载在父级 'MechRoot' 上
public class MechController : MonoBehaviour
{
    [Header("移动设置 (Movement)")]
    [Tooltip("移动速度 (米/秒)")]
    public float moveSpeed = 7f; // 可以根据需要调整

    [Header("旋转设置 (Rotation)")]
    [Tooltip("机甲朝向鼠标光标的旋转速度")]
    public float rotationSpeed = 15f; // 值越大，转向越快

    private Camera mainCamera; // 战斗场景的主摄像机
    private Rigidbody chassisRigidbody; // 子对象 ChassisCore 的 Rigidbody (可选, 可能用于获取位置?)
    private Transform chassisCoreTransform; // 子对象 ChassisCore 的 Transform

    private Vector2 moveInput; // 使用 Vector2 存储 WASD 输入

    void Start()
    {
        // 获取战斗场景的主摄像机 (确保场景中主摄像机 Tag 设置正确)
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("MechController: 未找到主摄像机 (Main Camera)! 请确保场景中有且只有一个 Tag 为 'MainCamera' 的摄像机。", this);
            enabled = false;
            return;
        }

        // 获取子对象 ChassisCore 的引用 (如果需要)
        // 可以通过 Find 或在 GameManager 中设置引用传递过来
        chassisCoreTransform = transform.Find("ChassisCore"); // 假设 ChassisCore 是直接子对象且名字不变
        if (chassisCoreTransform != null)
        {
            chassisRigidbody = chassisCoreTransform.GetComponent<Rigidbody>();
        }
        if (chassisCoreTransform == null || chassisRigidbody == null)
        {
            Debug.LogWarning("MechController: 未能找到子对象 ChassisCore 或其 Rigidbody。", this);
            // 可能不影响移动，但后续功能可能需要
        }

    }

    void Update()
    {
        // --- 获取输入 ---
        // GetAxisRaw 提供 -1, 0, 1 的离散值，适合方向控制
        moveInput.x = Input.GetAxisRaw("Horizontal"); // A/D -> X轴输入
        moveInput.y = Input.GetAxisRaw("Vertical");   // W/S -> Y轴输入 (屏幕上下)

        // --- 处理旋转 (让机甲朝向鼠标) ---
        RotateTowardsMouse();

        // --- 处理移动 (直接修改 Transform) ---
        Move();
    }

    void Move()
    {
        // --- 计算摄像机相对的移动方向 ---
        // 1. 获取摄像机的前方向量，并投射到水平面 (忽略 Y 轴)
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0;
        camForward.Normalize();

        // 2. 获取摄像机的右方向量，并投射到水平面
        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        // 3. 根据输入和摄像机方向计算最终移动方向
        // moveInput.y 控制沿摄像机前向(屏幕上下)移动，moveInput.x 控制沿摄像机右向(屏幕左右)移动
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // 4. 计算本帧的移动量
        Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;

        // 5. 应用移动 (直接修改 Transform)
        transform.Translate(movement, Space.World); // 在世界空间中移动
    }

    void RotateTowardsMouse()
    {
        // 1. 创建一个射线从摄像机发出，穿过鼠标当前位置
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 2. 创建一个假想的水平面 (与机甲当前 Y 轴同高)
        Plane groundPlane = new Plane(Vector3.up, transform.position); // 使用机甲当前高度作为平面

        // 3. 计算射线与平面的交点
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            // 获取鼠标在世界空间中水平面上的点
            Vector3 mouseWorldPos = mouseRay.GetPoint(distance);

            // 4. 计算从机甲指向鼠标点的方向向量
            Vector3 lookDirection = mouseWorldPos - transform.position;
            lookDirection.y = 0; // 确保只在水平面旋转，不抬头低头

            // 5. 如果方向向量有效 (避免原地旋转)
            if (lookDirection.sqrMagnitude > 0.01f) // 检查长度平方避免开根号
            {
                // 计算目标旋转
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                // 6. 使用 Slerp 平滑地转向目标旋转
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}