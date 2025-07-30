using UnityEngine;
using System.Collections; // <--- 确保有这一行，用于协程

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI 设置")]
    [Tooltip("敌人转向玩家的速度")]
    public float rotationSpeed = 5f; // <--- 新增
    private float _moveSpeed = 3f;
    private int _touchDamage = 5;
    private float _originalMoveSpeed; // 新增：用于存储原始速度

    [Header("伤害设置")]
    [Tooltip("怪物每次造成伤害后的冷却时间（秒）")]
    public float damageCooldown = 1.0f;
    private bool _canDealDamage = true;

    private Transform playerTransform = null;
    private Rigidbody rb;

    // --- 新增：動畫相關 ---
    private Animator animator;

    // ... Start(), InitializeEnemy(), FixedUpdate() 方法保持不变 ...

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"在敵人 '{gameObject.name}' 上沒有找到 Animator 元件！");
        }
    }

    public void InitializeEnemy(float speed, int damage)
    {
        _moveSpeed = speed;
        _touchDamage = damage;
        _originalMoveSpeed = speed; // 在初始化时记录下原始速度
    }

    // --- 新增：一个公共方法来设置移动速度 ---
    public void SetMoveSpeed(float newSpeed)
    {
        _moveSpeed = newSpeed;
    }

    // --- 新增：一个公共方法来获取原始速度 ---
    public float GetOriginalMoveSpeed()
    {
        return _originalMoveSpeed;
    }
    void FixedUpdate()
    {
        // 獲取玩家引用的邏輯保持不變
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null) { rb.velocity = Vector3.zero; return; }
            }
            else { rb.velocity = Vector3.zero; return; }
        }

        // 計算方向的邏輯保持不變
        Vector3 directionToPlayer = (playerTransform.position - rb.position).normalized;
        directionToPlayer.y = 0; // 確保在水平面移動

        // --- 設定移動速度 (保持不變) ---
        Vector3 targetVelocity = directionToPlayer * _moveSpeed;
        rb.velocity = targetVelocity;

        // --- 新增：設定旋轉朝向 ---
        if (directionToPlayer.sqrMagnitude > 0.01f) // 確保有移動方向時才旋轉
        {
            // 1. 計算目標旋轉值 (讓敵人的前方對準 directionToPlayer)
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);

            // 2. 使用 Slerp 平滑地從當前旋轉插值到目標旋轉
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // 3. 將新的旋轉值應用到 Rigidbody
            rb.MoveRotation(newRotation);
        }
        // ------------------------

        // 更新動畫狀態的方法呼叫保持不變
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        // 檢查1：確認 animator 引用是否有效
        if (animator == null)
        {
            // 這個日誌只會在 animator 為空時顯示一次
            Debug.LogError($"[EnemyAI] 敵人 '{gameObject.name}' 的 Animator 元件為空，無法播放動畫！");
            return;
        }

        // 檢查2：獲取剛體的當前速度大小
        float currentSpeed = rb.velocity.magnitude;
        bool isCurrentlyMoving = currentSpeed > 0.1f;

        // 檢查3：查看 animator 中 isMoving 參數的當前值
        bool animatorIsMovingState = animator.GetBool("isMoving");

        // 日誌 A: 持續打印當前狀態，方便觀察
        // 你可以取消下面這行的註解來進行詳細偵錯
        // Debug.Log($"[EnemyAI] 速度: {currentSpeed.ToString("F2")}, isCurrentlyMoving: {isCurrentlyMoving}, Animator's isMoving: {animatorIsMovingState}");

        // 只有在需要改變狀態時才呼叫 SetBool，這是一種優化
        if (isCurrentlyMoving != animatorIsMovingState)
        {
            // 日誌 B: 確認 SetBool 是否被呼叫
            Debug.Log($"<color=lime>[EnemyAI] 狀態改變！將 'isMoving' 參數設定為: {isCurrentlyMoving}</color>");
            animator.SetBool("isMoving", isCurrentlyMoving);
        }
    }

    // --- 修改后的碰撞逻辑 ---
    void OnTriggerStay(Collider other)
    {
        if (_canDealDamage && other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                // 【核心修改】在调用TakeDamage时，加入 AttackType.Standard
                playerHealth.TakeDamage(_touchDamage, transform.position, this.gameObject, AttackType.Standard);

                _canDealDamage = false;
                StartCoroutine(DamageCooldownRoutine());
            }
        }
    }

    // --- 新增的冷却协程 ---
    IEnumerator DamageCooldownRoutine()
    {
        yield return new WaitForSeconds(damageCooldown);
        _canDealDamage = true;
    }
    public void TriggerDamageCooldown()
    {
        if (_canDealDamage)
        {
            _canDealDamage = false;
            // 复用已有的协程来重置计时器
            StartCoroutine(DamageCooldownRoutine());
        }
    }
}