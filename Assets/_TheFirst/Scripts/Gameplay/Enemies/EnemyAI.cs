using UnityEngine;
using System.Collections; // <--- 确保有这一行，用于协程
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Chasing, Paused, JumpingAttack, PreparingExplosion, MeleeAttacking, RangedAttacking }
    private AIState _currentState = AIState.Chasing;
    public AIState CurrentState => _currentState;

    private bool isStunned = false; // <--- vvv 新增 vvv

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

    private Coroutine knockbackCoroutine;


    private Transform playerTransform = null;
    private NavMeshAgent agent;
    private Rigidbody rb; // 【新增】Rigidbody 的引用

    // --- 新增：動畫相關 ---
    private Animator animator;

    private StatusEffectReceiver statusReceiver; // <--- vvv 新增
    private float stateTimer; // 当前状态的剩余时间

    // --- 【新增】用于目标点偏移的变量 ---
    private Vector3 targetOffset;
    private float offsetRecalculateTimer;

    private EnemyExplosionAttack explosionAttackScript;
    private EnemyMeleeAttack meleeAttackScript; // 用于在眩晕/击退时中断近战攻击
    private BossUnit bossUnit; // 缓存 Boss 组件引用（用于击退免疫等检查）

    // ... Start(), InitializeEnemy(), FixedUpdate() 方法保持不变 ...
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>(); // 【新增】获取 Rigidbody 组件
        animator = GetComponentInChildren<Animator>();
        explosionAttackScript = GetComponent<EnemyExplosionAttack>();
        meleeAttackScript = GetComponent<EnemyMeleeAttack>(); // 【新增】获取爆炸攻击脚本
        statusReceiver = GetComponent<StatusEffectReceiver>();
        bossUnit = GetComponent<BossUnit>();
    }
    void Start()
    {
        // 游戏开始时，让怪物直接进入追逐状态
        EnterChaseState();
    }

    public void SetMeleeAttackingState(bool isAttacking)
    {
        if (isAttacking)
        {
            _currentState = AIState.MeleeAttacking;
            // 我们信任 EnemyMeleeAttack 脚本会处理 agent.isStopped
        }
        else
        {
            // 只有当我们处于攻击状态时，才切换回追逐
            if (_currentState == AIState.MeleeAttacking)
            {
                _currentState = AIState.Chasing;
                // MeleeAttack 脚本会恢复 agent.isStopped
            }
        }
    }

    /// <summary>
    /// 远程攻击脚本在进入/离开攻击范围时调用，防止 EnemyAI 干扰导航控制
    /// </summary>
    public void SetRangedAttackingState(bool isAttacking)
    {
        if (isAttacking)
        {
            _currentState = AIState.RangedAttacking;
        }
        else
        {
            if (_currentState == AIState.RangedAttacking)
            {
                EnterChaseState(); // 恢复追逐
            }
        }
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

    public void SetStunned(bool stunned)
    {
        isStunned = stunned;

        // 被眩晕时，立即中断正在进行的攻击
        if (stunned)
        {
            // 中断近战攻击
            if (meleeAttackScript != null)
            {
                meleeAttackScript.InterruptAttack();
            }
            // 中断远程攻击
            EnemyProjectileAttack rangedAttack = GetComponent<EnemyProjectileAttack>();
            if (rangedAttack != null)
            {
                rangedAttack.InterruptAttack();
            }
        }

        if (agent != null && agent.isOnNavMesh)
        {
            // 立即停止或恢复 NavMeshAgent 的移动
            agent.isStopped = stunned;
        }
        if (animator != null)
        {
            if (stunned)
            {
                // 当被眩晕时，强制 "isMoving" 为 false，
                // 这将触发向“待机”状态的过渡。
                animator.SetBool("isMoving", false);
            }
            // 当眩晕结束 (stunned == false) 时，我们不需要在这里设置回 true。
            // 稍后 Update() 循环中的 UpdateAnimation() 方法会接管，
            // 并根据 agent.velocity 自动将其设置回 true。
        }
    }

    public void ApplyKnockback(Vector3 forceDirection, float forceAmount, float duration = 0.3f)
    {
        // Boss 免疫击退检查
        if (bossUnit != null && bossUnit.immuneToKnockback) return;

        // 眩晕时 或已经在被击退时，不触发
        if (isStunned || knockbackCoroutine != null) return;

        // 【修复】被击退时，立即中断正在进行的近战攻击并清理预警特效
        if (meleeAttackScript != null)
        {
            meleeAttackScript.InterruptAttack();
        }

        knockbackCoroutine = StartCoroutine(KnockbackRoutine(forceDirection * forceAmount, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 force, float duration)
    {
        // 1. 禁用 NavMeshAgent 对位置的控制
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        agent.enabled = false;

        // 2. 启用 Rigidbody 并施加力
        rb.isKinematic = false;
        rb.AddForce(force, ForceMode.Impulse);       

        // 3. 等待击退效果结束
        yield return new WaitForSeconds(duration);        

        // 4. 恢复 Rigidbody 和 NavMeshAgent
        rb.isKinematic = true;
        agent.enabled = true;

        // 5. 将 Agent 传送到物理模拟结束的新位置
        if (agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
            agent.isStopped = false;
        }
        else
        {
            // 被弹出 NavMesh！尝试找到最近的有效位置并恢复
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;

                if (agent.isOnNavMesh)
                {
                    agent.Warp(hit.position);
                    agent.isStopped = false;
                }
            }
        }

        knockbackCoroutine = null;
    }

    // --- 新增：一个公共方法来获取原始速度 ---
    public float GetOriginalMoveSpeed() => _originalMoveSpeed;

    void Update()
    {
        if (isStunned) return;
        if (knockbackCoroutine != null)
        {
            // 协程正在处理击退，Update() 应该停止干预
            return;
        }

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
        if (_currentState == AIState.MeleeAttacking || _currentState == AIState.RangedAttacking)
        {
            return; // 暂停所有移动和动画逻辑
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
                // --- vvv [ 核心修改 ] vvv ---
                // 1. 获取弱化乘数 (如果 receiver 为 null, 默认为 1.0)
                float multiplier = (statusReceiver != null) ? statusReceiver.weakenDamageMultiplier : 1.0f;

                // 2. 计算最终伤害
                int finalDamage = Mathf.RoundToInt(_touchDamage * multiplier);

                // 3. 使用最终伤害
                playerHealth.TakeDamage(finalDamage, transform.position, this.gameObject, AttackType.Standard); //
                // --- ^^^ [ 核心修改 ] ^^^ ---

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
