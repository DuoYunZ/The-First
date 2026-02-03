using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class MechController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;

    [Header("旋转设置")]
    public float rotationSpeed = 15f;

    [Header("冲刺设置")] // <--- NEW SECTION
    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 2f;
    [Tooltip("无敌时间，应小于或等于冲刺持续时间")]
    public float invincibilityDuration = 0.2f;
    public AudioClip[] dashSfx; // <--- 新增: 冲刺音效数组
    public GameObject dashVfxPrefab; // <--- 新增: 冲刺特效预制件

    [Header("音效设置")]
    [Tooltip("走路音效剪辑数组，可以放多个以增加随机性")]
    public AudioSource footstepAudioSource;
    [Tooltip("冲刺和其他特效的音效源")]
    public AudioSource dashAudioSource; // <--- 新增: 独立的冲刺音效源
    public AudioClip[] footstepClips;
    // 内部引用
    private Transform visualsTransform;
    private Rigidbody rb;
    private Animator animator; // 【新增】动画控制器引用

    private Vector2 moveInput;
    private PlayerControls playerControls;

    private bool isDashing = false;
    private float lastDashTime = -999f;

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
        
        if (footstepAudioSource == null)
        {
            Debug.LogError("在玩家身上找不到AudioSource组件!", this);
        }
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();

        playerControls.Player.Dash.performed += PerformDash;
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();

        playerControls.Player.Dash.performed -= PerformDash;
    }

    // 我们不再需要 Initialize 方法，因为 Awake 已经可以完成所有工作
    // public void Initialize(Transform visuals) { ... }

    void Update()
    {
        if (!isDashing)
        {
            moveInput = playerControls.Player.Move.ReadValue<Vector2>();
        }

        // 【新增】每帧更新动画参数
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (!isDashing)
        {
            Move();
        }
    }
    private void PerformDash(InputAction.CallbackContext context)
    {
        // 检查冷却时间和是否已在冲刺
        if (!isDashing && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashCoroutine());
        }
    }
    private IEnumerator DashCoroutine()
    {
        // 1. 设置状态
        isDashing = true;
        lastDashTime = Time.time;

        // 启用无敌状态（先开始）
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.isInvincible = true;
        }

        // 同时设置 Health 组件的无敌状态
        Health playerHealth = GetComponent<Health>();
        if (playerHealth != null && playerHealth.isPlayerHealth)
        {
            playerHealth.SetInvincible(invincibilityDuration);
        }

        // 播放音效
        if (dashAudioSource != null && dashSfx != null && dashSfx.Length > 0)
        {
            AudioClip clipToPlay = dashSfx[Random.Range(0, dashSfx.Length)];
            dashAudioSource.PlayOneShot(clipToPlay);
        }

        // 播放冲刺特效
        if (dashVfxPrefab != null)
        {
            Instantiate(dashVfxPrefab, visualsTransform.position, visualsTransform.rotation);
        }

        // 2. 计算冲刺方向
        Vector3 dashDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        if (dashDirection.sqrMagnitude < 0.01f)
        {
            dashDirection = visualsTransform.forward;
        }

        // 3. 施加冲刺力
        rb.velocity = Vector3.zero;
        rb.AddForce(dashDirection * dashForce, ForceMode.Impulse);

        // 触发冲刺动画
        animator?.SetTrigger("Dash");        

        // 4. 等待冲刺持续时间结束（停止移动）
        yield return new WaitForSeconds(dashDuration);

        // 冲刺动作结束，但无敌状态可能还在继续
        isDashing = false;
        rb.velocity = Vector3.zero; // 停止冲刺移动

        Debug.Log($"冲刺移动结束，但无敌状态还将持续 {invincibilityDuration - dashDuration} 秒");

        // 5. 计算剩余的无敌时间并继续等待
        float remainingInvincibility = invincibilityDuration - dashDuration;
        if (remainingInvincibility > 0)
        {
            yield return new WaitForSeconds(remainingInvincibility);
        }

        // 6. 结束无敌状态
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.isInvincible = false;
        }       
        

        Debug.Log("无敌状态结束");
    }
    void Move()
    {
        if (visualsTransform == null) return;

        // --- 【修复】应用移速加成 ---
        float finalSpeed = moveSpeed;
        if (PlayerStats.Instance != null)
        {
            finalSpeed *= PlayerStats.Instance.moveSpeedMultiplier;
        }
        // -------------------------

        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        Vector3 targetVelocity = moveDirection * finalSpeed; // 使用计算后的速度
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
    public void PlayFootstepSound()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];

        if (footstepAudioSource != null)
        {
            footstepAudioSource.PlayOneShot(clip);
        }
    }
}