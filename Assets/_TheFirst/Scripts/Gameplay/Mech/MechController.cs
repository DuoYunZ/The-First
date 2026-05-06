using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class MechController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;

    [Header("旋转设置")]
    public float rotationSpeed = 15f;

    [Header("冲刺设置")]
    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    [Tooltip("每格能量的充能时间（秒）")]
    public float dashCooldown = 2f;
    [Tooltip("最大冲刺充能格数（不同角色可设为1~3）")]
    public int maxDashCharges = 2;
    [Tooltip("无敌时间，应小于或等于冲刺持续时间")]
    public float invincibilityDuration = 0.2f;
    public AudioClip[] dashSfx; // 冲刺音效数组
    public GameObject dashVfxPrefab; // 冲刺特效预制件

    [Header("跑步烟尘特效")]
    [Tooltip("挂在角色脚底的烟尘粒子系统")]
    public ParticleSystem runDustVfx;

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
    private bool isKnockedBack = false; // 是否处于受击退状态
    private float knockbackTimer = 0f;  // 击退剩余时间

    /// <summary>
    /// 外部移动锁定标记（精准斩击停顿等场景使用）
    /// </summary>
    [HideInInspector] public bool isMovementLocked = false;

    // 冲刺充能格系统
    private int currentCharges;
    private float[] chargeTimers; // 每格独立的充能计时器

    /// <summary>
    /// 当前可用的冲刺充能格数
    /// </summary>
    public int CurrentDashCharges => currentCharges;
    /// <summary>
    /// 最大冲刺充能格数
    /// </summary>
    public int MaxDashCharges => maxDashCharges;
    /// <summary>
    /// 获取指定格子的充能进度 (0~1)，1 = 已充满
    /// </summary>
    public float GetChargeProgress(int index)
    {
        if (index < 0 || index >= maxDashCharges) return 0;
        if (index < currentCharges) return 1f; // 已充满
        if (index == currentCharges && chargeTimers != null && index < chargeTimers.Length)
        {
            // 正在充能的那一格
            return chargeTimers[index] / dashCooldown;
        }
        return 0f; // 还没轮到充能
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("MechController: Rigidbody 组件未找到!", this);
            enabled = false;
        }

        playerControls = new PlayerControls();
        // 应用已保存的自定义键位
        KeyBindingManager.ApplyOverrides(playerControls);

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

        // 初始化充能格
        currentCharges = maxDashCharges;
        chargeTimers = new float[maxDashCharges];
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();

        playerControls.Player.Dash.performed += PerformDash;
        playerControls.Player.Ultimate.performed += PerformUltimate;

        // 订阅改键事件，运行时改键后立即刷新绑定
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingChanged += OnBindingChanged;
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();

        playerControls.Player.Dash.performed -= PerformDash;
        playerControls.Player.Ultimate.performed -= PerformUltimate;

        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingChanged -= OnBindingChanged;
    }

    /// <summary>
    /// 运行时改键后重新应用覆盖到当前 PlayerControls 实例
    /// </summary>
    private void OnBindingChanged(string actionName, int bindingIndex)
    {
        KeyBindingManager.ApplyOverrides(playerControls);
    }

    // 我们不再需要 Initialize 方法，因为 Awake 已经可以完成所有工作
    // public void Initialize(Transform visuals) { ... }

    void Update()
    {
        if (!isDashing)
        {
            moveInput = playerControls.Player.Move.ReadValue<Vector2>();
        }

        // 滚轮切换大招主武器（通过 Input System Action 读取）
        float scroll = playerControls.Player.ScrollWeapon.ReadValue<float>();
        if (Mathf.Abs(scroll) > 0.1f && UltimateManager.Instance != null)
        {
            UltimateManager.Instance.ScrollSelectWeapon(scroll);
        }

        // 每帧更新动画参数
        UpdateAnimation();

        // 冲刺充能恢复
        UpdateDashCharges();

        // 更新击退计时器
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
            }
        }

        // --- 【被动道具】燃烧轨迹：移动时留下火焰区域 ---
        UpdateFlameTrail();
    }

    /// <summary>
    /// 大招释放（通过 Input System Action 触发，支持改键）
    /// </summary>
    private void PerformUltimate(InputAction.CallbackContext context)
    {
        if (UltimateManager.Instance != null)
        {
            UltimateManager.Instance.TryReleaseUltimate();
        }
    }

    /// <summary>
    /// 每帧更新冲刺充能格，按顺序一格一格充
    /// </summary>
    void UpdateDashCharges()
    {
        if (currentCharges >= maxDashCharges) return; // 已满，无需充能

        // 当前正在充能的格子索引 = currentCharges
        int chargingIndex = currentCharges;
        chargeTimers[chargingIndex] += Time.deltaTime;

        if (chargeTimers[chargingIndex] >= dashCooldown)
        {
            // 这一格充满了
            chargeTimers[chargingIndex] = 0f;
            currentCharges++;
        }
    }

    void FixedUpdate()
    {
        if (!isDashing && !isKnockedBack && !isMovementLocked)
        {
            Move();
        }
        else if (isMovementLocked)
        {
            // 移动锁定期间强制速度归零
            rb.velocity = Vector3.zero;
        }
    }
    private void PerformDash(InputAction.CallbackContext context)
    {
        // 检查是否有可用充能格且未在冲刺中
        if (!isDashing && currentCharges > 0)
        {
            currentCharges--;
            // 重置当前及所有更高索引的计时器（防止残留进度导致瞬间充满）
            for (int i = currentCharges; i < maxDashCharges; i++)
            {
                chargeTimers[i] = 0f;
            }
            StartCoroutine(DashCoroutine());
        }
    }
    private IEnumerator DashCoroutine()
    {
        // 1. 设置状态
        isDashing = true;

        // 【图鉴成就】记录冲刺次数 (冲刺余烬解锁条件)
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddStat("Dash_Count", 1);
        }

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

        // --- 【被动道具】冲刺余烬：冲刺结束后释放冲击波 ---
        if (PlayerStats.Instance != null && PlayerStats.Instance.dashExplosionLevel > 0)
        {
            TriggerDashExplosion(transform.position);
        }

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
        

    }
    void Move()
    {
        if (visualsTransform == null) return;

        // --- 移速加成（临时Buff已在RecalculateStats中叠加到主字段） ---
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
    /// 【新增】外部调用：施加击退力
    /// </summary>
    /// <param name="direction">击退方向（会自动归一化）</param>
    /// <param name="force">击退力度</param>
    /// <param name="duration">击退持续时间（期间不更新移动）</param>
    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.15f)
    {
        if (isDashing) return; // 冲刺中不被击退

        direction.y = 0f;
        direction = direction.normalized;

        isKnockedBack = true;
        knockbackTimer = duration;

        // 清零当前速度后施加击退冲击
        rb.velocity = Vector3.zero;
        rb.AddForce(direction * force, ForceMode.Impulse);
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

        // 控制跑步烟尘特效
        if (runDustVfx != null)
        {
            if (isMoving && !runDustVfx.isPlaying)
            {
                runDustVfx.Play();
            }
            else if (!isMoving && runDustVfx.isPlaying)
            {
                runDustVfx.Stop();
            }
        }
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

    // ============================================================
    // 冲刺余烬 — 冲刺结束后的AOE冲击波
    // ============================================================

    [Header("冲刺余烬（被动道具）")]
    [Tooltip("冲击波VFX预制件（可选，没有也能造成伤害）")]
    public GameObject dashExplosionPrefab;
    [Tooltip("冲击波基础伤害")]
    public int dashExplosionBaseDamage = 50;
    [Tooltip("冲击波基础半径")]
    public float dashExplosionBaseRadius = 8f;

    /// <summary>
    /// 冲刺结束后在指定位置释放冲击波AOE
    /// </summary>
    private void TriggerDashExplosion(Vector3 position)
    {
        int level = PlayerStats.Instance.dashExplosionLevel;
        float damageMultiplier = 1f + (level * 0.25f); // 每级+25%伤害
        float radiusMultiplier = 1f + (level * 0.10f); // 每级+10%范围

        int finalDamage = Mathf.RoundToInt(dashExplosionBaseDamage * damageMultiplier * PlayerStats.Instance.damageMultiplier);
        float finalRadius = dashExplosionBaseRadius * radiusMultiplier;

        // 生成冲击波VFX
        if (dashExplosionPrefab != null)
        {
            GameObject vfx = Instantiate(dashExplosionPrefab, position, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * radiusMultiplier;
            Destroy(vfx, 2f); // 2秒后自动清理
        }

        // 对范围内敌人造成伤害和击退
        Collider[] hits = Physics.OverlapSphere(position, finalRadius, LayerMask.GetMask("Enemies"));
        foreach (var hit in hits)
        {
            Health targetHealth = hit.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(finalDamage, position, gameObject, AttackType.Standard, null, null, "冲刺余烬");

                // 施加击退
                StatusEffectReceiver receiver = hit.GetComponent<StatusEffectReceiver>();
                if (receiver != null)
                {
                    Vector3 knockDir = (hit.transform.position - position).normalized;
                    receiver.ApplyKnockback(knockDir, 8f, 0.3f);
                }
            }
        }

        Debug.Log($"<color=orange>[冲刺余烬] Lv{level} 触发！伤害={finalDamage}, 半径={finalRadius:F1}, 命中={hits.Length}</color>");
    }

    // ============================================================
    // 燃烧轨迹 — 移动时在地面留下火焰区域
    // ============================================================

    [Header("燃烧轨迹（被动道具）")]
    [Tooltip("燃烧区域VFX预制件（可选，没有也能造成伤害）")]
    public GameObject flameZonePrefab;
    [Tooltip("满级时的留痕间距（米）")]
    public float flameTrailMinInterval = 1.5f;
    [Tooltip("1级时的留痕间距（米）")]
    public float flameTrailMaxInterval = 3.0f;
    [Tooltip("燃烧区域存在时间（秒）")]
    public float flameZoneDuration = 3f;
    [Tooltip("燃烧区域半径")]
    public float flameZoneRadius = 1.5f;
    [Tooltip("每跳伤害基础值")]
    public int flameBaseDamagePerTick = 3;
    [Tooltip("每跳间隔（秒）")]
    public float flameTickInterval = 0.5f;

    private Vector3 lastFlameDropPosition;
    private bool flameTrailInitialized = false;

    /// <summary>
    /// 每帧检查是否需要放置燃烧区域
    /// </summary>
    private void UpdateFlameTrail()
    {
        if (PlayerStats.Instance == null) return;
        int level = PlayerStats.Instance.flameTrailLevel;
        if (level <= 0) return;

        // 首次激活时初始化位置
        if (!flameTrailInitialized)
        {
            lastFlameDropPosition = transform.position;
            flameTrailInitialized = true;
            return;
        }

        // 根据等级插值计算掉落间距（等级越高间距越小，火焰越密集）
        float dropInterval = Mathf.Lerp(flameTrailMaxInterval, flameTrailMinInterval, (float)(level - 1) / 4f);

        float distance = Vector3.Distance(transform.position, lastFlameDropPosition);
        if (distance >= dropInterval)
        {
            DropFlameZone(level);
            lastFlameDropPosition = transform.position;
        }
    }

    /// <summary>
    /// 在当前位置放下一个燃烧区域
    /// </summary>
    private void DropFlameZone(int level)
    {
        // 计算伤害（每级+20%）
        float dmgMultiplier = 1f + (level - 1) * 0.2f;
        int finalDamage = Mathf.RoundToInt(flameBaseDamagePerTick * dmgMultiplier);

        // 应用玩家全局伤害加成
        if (PlayerStats.Instance != null)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * PlayerStats.Instance.damageMultiplier);
        }

        Vector3 spawnPos = new Vector3(transform.position.x, 0.05f, transform.position.z);

        // 生成燃烧区域
        if (flameZonePrefab != null)
        {
            GameObject zone = Instantiate(flameZonePrefab, spawnPos, Quaternion.identity);
            FlameTrailZone flameZone = zone.GetComponent<FlameTrailZone>();
            if (flameZone != null)
            {
                flameZone.Initialize(finalDamage, flameZoneDuration, flameZoneRadius, flameTickInterval);
            }
            else
            {
                // 预制件上没有FlameTrailZone脚本，自动添加
                flameZone = zone.AddComponent<FlameTrailZone>();
                flameZone.Initialize(finalDamage, flameZoneDuration, flameZoneRadius, flameTickInterval);
            }
        }
        else
        {
            // 没有预制件也能工作：程序化创建简易燃烧区域
            GameObject zone = new GameObject("FlameTrailZone");
            zone.transform.position = spawnPos;
            FlameTrailZone flameZone = zone.AddComponent<FlameTrailZone>();
            flameZone.Initialize(finalDamage, flameZoneDuration, flameZoneRadius, flameTickInterval);
        }
    }
}