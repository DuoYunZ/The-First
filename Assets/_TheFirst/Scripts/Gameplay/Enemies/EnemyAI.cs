using UnityEngine;
using System.Collections; // <--- 确保有这一行，用于协程
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Chasing, Paused, JumpingAttack, PreparingExplosion }
    private AIState _currentState = AIState.Chasing;
    public AIState CurrentState => _currentState;

    [Header("AI 设置")]   
    private float _originalMoveSpeed; // 新增：用于存储原始速度

    [Header("伤害设置")]
    [Tooltip("怪物每次造成伤害后的冷却时间（秒）")]
    public float damageCooldown = 1.0f;
    private int _touchDamage = 5;
    private bool _canDealDamage = true;


    [Header("随机停顿行为 (Random Pause Behavior)")]
    [Tooltip("勾选此项以启用随机停顿功能")]
    public bool canPause = true;
    [Tooltip("在完成一次追逐后，有多大的几率进入停顿状态 (0到1)")]
    [Range(0f, 1f)]
    public float pauseChance = 0.2f; // 默认20%的几率停顿
    [Tooltip("每次停顿的持续时间范围（秒）")]
    public Vector2 pauseDurationRange = new Vector2(0.5f, 1.5f);
    [Tooltip("每次追逐的持续时间范围（秒）")]
    public Vector2 chaseDurationRange = new Vector2(3f, 7f);


    private Transform playerTransform = null;
    private NavMeshAgent agent;
    private Rigidbody rb; // 【新增】Rigidbody 的引用

    // --- 新增：動畫相關 ---
    private Animator animator;

    private float stateTimer; // 当前状态的剩余时间

    // --- 【新增】用于目标点偏移的变量 ---
    private Vector3 targetOffset;
    private float offsetRecalculateTimer;

    private EnemyExplosionAttack explosionAttackScript;

    // ... Start(), InitializeEnemy(), FixedUpdate() 方法保持不变 ...
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>(); // 【新增】获取 Rigidbody 组件
        animator = GetComponentInChildren<Animator>();
        explosionAttackScript = GetComponent<EnemyExplosionAttack>(); // 【新增】获取爆炸攻击脚本
    }
    void Start()
    {
        // 游戏开始时，让怪物直接进入追逐状态
        EnterChaseState();
    }

    public void InitializeEnemy(float speed, int damage)
    {
        _touchDamage = damage;
        _originalMoveSpeed = speed;

        if (agent != null)
        {
            agent.speed = speed * Random.Range(0.7f, 1.7f);
            agent.acceleration = agent.acceleration * Random.Range(0.7f, 1.7f);
            agent.angularSpeed = agent.angularSpeed * Random.Range(0.7f, 1.7f);
        }
    }

    // --- 新增：一个公共方法来设置移动速度 ---
    public void SetMoveSpeed(float newSpeed)
    {
        if (agent != null)
        {
            agent.speed = newSpeed; // 【修改】控制 NavMeshAgent 的速度
        }
    }

    // --- 新增：一个公共方法来获取原始速度 ---
    public float GetOriginalMoveSpeed() => _originalMoveSpeed;

    void Update()
    {
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
            }
            else
            {
                return;
            }
        }

        // 【修改】当处于特殊攻击状态时，暂停常规的追逐/停顿逻辑
        if (_currentState == AIState.JumpingAttack || _currentState == AIState.PreparingExplosion)
        {
            // 在这些状态下，所有行为都由协程和攻击脚本控制
            return;
        }

        if (!agent.isOnNavMesh) return;

        stateTimer -= Time.deltaTime;

        if (_currentState == AIState.Chasing)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.SetDestination(playerTransform.position);
            }
            if (stateTimer <= 0)
            {
                if (canPause && Random.value < pauseChance)
                {
                    EnterPauseState();
                }
                else
                {
                    EnterChaseState();
                }
            }
        }
        else if (_currentState == AIState.Paused)
        {
            if (stateTimer <= 0)
            {
                EnterChaseState();
            }
        }

        UpdateAnimation();
    }
    private void EnterChaseState()
    {
        _currentState = AIState.Chasing;
        stateTimer = Random.Range(chaseDurationRange.x, chaseDurationRange.y);
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private void EnterPauseState()
    {
        _currentState = AIState.Paused;
        stateTimer = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    public void RequestJumpAttack(Vector3 targetPosition, float jumpDuration, float arcHeight)
    {
        // 只有在追逐状态下才能发起跳跃攻击
        if (_currentState == AIState.Chasing)
        {
            _currentState = AIState.JumpingAttack;
            StartCoroutine(ParabolicJumpCoroutine(targetPosition, jumpDuration, arcHeight));
        }
    }
    public void ResumeNormalBehavior()
    {
        // 攻击结束后，立刻回到追逐状态
        EnterChaseState();
    }
    private IEnumerator ParabolicJumpCoroutine(Vector3 endPoint, float jumpDuration, float arcHeight)
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        bool originalAgentState = agent.enabled;
        agent.enabled = false;
        rb.isKinematic = false;
        rb.useGravity = false;

        Vector3 startPoint = transform.position;
        float timer = 0f;

        while (timer < jumpDuration)
        {
            float t = timer / jumpDuration;
            Vector3 horizontalPosition = Vector3.Lerp(startPoint, endPoint, t);
            float verticalPosition = Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = new Vector3(horizontalPosition.x, startPoint.y + verticalPosition, horizontalPosition.z);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.position = endPoint;
        rb.isKinematic = true;
        agent.enabled = originalAgentState;

        // 【关键】将Agent同步到新位置
        if (agent.isOnNavMesh) agent.Warp(transform.position);

        // 跳跃结束，进入落地准备状态
        _currentState = AIState.PreparingExplosion;

        // 【关键】通知攻击脚本，移动已完成，可以执行后续的爆炸逻辑了
        if (explosionAttackScript != null)
        {
            explosionAttackScript.OnJumpFinished();
        }
        else
        {
            Debug.LogError("找不到 EnemyExplosionAttack 脚本来完成攻击！", this);
            // 如果找不到攻击脚本，恢复正常行为以防卡死
            ResumeNormalBehavior();
        }
    }
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // 我们需要检查 agent.velocity 因为即使 isStopped = true, velocity 也需要一帧才归零
        bool isCurrentlyMoving = !agent.isStopped && agent.velocity.magnitude > 0.1f;

        if (isCurrentlyMoving != animator.GetBool("isMoving"))
        {
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