using UnityEngine;
using UnityEngine.InputSystem;

public class MechController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;

    [Header("旋转设置")]
    public float rotationSpeed = 15f;

    // 内部引用
    private Transform visualsTransform;
    private Rigidbody rb;
    private Animator animator; // 【新增】动画控制器引用

    private Vector2 moveInput;
    private PlayerControls playerControls;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("MechController: Rigidbody 组件未找到!", this);
            enabled = false;
        }

        playerControls = new PlayerControls();

        // 自动查找视觉模型和动画控制器
        visualsTransform = transform.Find("Visuals");
        if (visualsTransform != null)
        {
            animator = visualsTransform.GetComponent<Animator>(); // 【新增】获取Animator组件
        }

        if (visualsTransform == null)
        {
            Debug.LogError("MechController: 在 '" + gameObject.name + "' 的子级中未能找到名为 'Visuals' 的对象！", this);
            enabled = false;
        }
        if (animator == null)
        {
            Debug.LogWarning("MechController: 在 'Visuals' 对象上未能找到 Animator 组件！", this);
        }
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
    }

    // 我们不再需要 Initialize 方法，因为 Awake 已经可以完成所有工作
    // public void Initialize(Transform visuals) { ... }

    void Update()
    {
        moveInput = playerControls.Player.Move.ReadValue<Vector2>();

        // 【新增】每帧更新动画参数
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        Move();
    }

    void Move()
    {
        // ... 移动和旋转逻辑保持不变 ...
        if (visualsTransform == null) return;

        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        rb.velocity = targetVelocity;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            visualsTransform.rotation = Quaternion.Slerp(visualsTransform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// 【新增】根据当前速度更新动画状态
    /// </summary>
    void UpdateAnimation()
    {
        if (animator == null) return;

        // 检查刚体的速度大小，判断是否在移动
        bool isMoving = rb.velocity.sqrMagnitude > 0.1f;

        // 将移动状态传递给 Animator 的 "isMoving" 参数
        animator.SetBool("isMoving", isMoving);
    }
}