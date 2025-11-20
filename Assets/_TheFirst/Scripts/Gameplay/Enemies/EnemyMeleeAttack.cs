// --- EnemyMeleeAttack.cs ---
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Health))]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("攻击设置")]
    [Tooltip("攻击预警特效（例如地上的扇形或圆形）")]
    public GameObject warningIndicatorPrefab;
    [Tooltip("攻击命中时的特效")]
    public GameObject hitEffectPrefab;
    [Tooltip("攻击特效")]
    public GameObject slashEffectPrefab; // 【新增】刀光特效
    [Tooltip("进入此范围后，怪物会开始准备攻击")]
    public float attackRange = 3f;
    [Tooltip("攻击冷却时间（秒）")]
    public float cooldown = 4f;

    [Header("技能效果")]
    [Tooltip("攻击前摇（蓄力）的时长")]
    public float windupDuration = 1.2f;
    [Tooltip("攻击判定的范围（例如扇形半径或圆形半径）")]
    public float attackRadius = 2.5f;
    [Tooltip("【可选】如果是扇形攻击，这里是扇形的夹角")]
    [Range(0, 360)]
    public float attackAngle = 90f; // 0或360代表圆形攻击
    [Tooltip("技能造成的伤害值")]
    public int damage = 15;

    [Header("视觉效果")]
    [Tooltip("预警特效的视觉半径，应大于攻击半径。根据你的测试，这个值应为10")]
    public float indicatorVisualRadius = 10f; // 【关键】请在Inspector中确认此值为10
   



    // 私有变量
    private Transform playerTarget;
    private float cooldownTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private bool isAttacking = false;
    private GameObject activeWarningIndicator;
    private EnemyAI enemyAI; // <--- [新增]

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        enemyAI = GetComponent<EnemyAI>();

        Health health = GetComponent<Health>();
        if (health != null)
        {
            health.OnDeath.AddListener(OnDeath);
        }
    }

    private void OnDeath()
    {
        // 停止所有正在进行的攻击协程
        StopAllCoroutines();

        // 如果当前有激活的预警特效，立即销毁它
        if (activeWarningIndicator != null)
        {
            Destroy(activeWarningIndicator);
        }
    }
    void Update()
    {
        if (playerTarget == null || isAttacking) return;

        cooldownTimer -= Time.deltaTime;

        if (Vector3.Distance(transform.position, playerTarget.position) <= attackRange && cooldownTimer <= 0)
        {
            StartCoroutine(MeleeSequence());
            cooldownTimer = cooldown;
        }
    }

    IEnumerator MeleeSequence()
    {
        isAttacking = true;
        enemyAI.SetMeleeAttackingState(true);

        // 1. 准备阶段：停止移动，面朝玩家
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            // 额外强制将速度设置为0，确保立即停止
            agent.velocity = Vector3.zero;
        }
        yield return null;
        if (animator != null)
        {
            animator.SetBool("isMoving", false);
            animator.SetTrigger("doWarning");
        }

        transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));


        // 2. 预警阶段：在身前生成预警特效
        if (warningIndicatorPrefab != null)
        {
            activeWarningIndicator = Instantiate(warningIndicatorPrefab, transform.position, transform.rotation);
            MeleeWarningIndicatorController indicatorController = activeWarningIndicator.GetComponent<MeleeWarningIndicatorController>();
            if (indicatorController != null)
            {
                indicatorController.Animate(this.windupDuration, this.attackRadius, this.attackAngle, this.indicatorVisualRadius);
            }
            else
            {
                Destroy(activeWarningIndicator, windupDuration);
            }
        }

        // 3. 蓄力等待
        yield return new WaitForSeconds(windupDuration);

        // 4. 攻击判定阶段
        if (playerTarget != null)
        {
            transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));
        }
        if (animator != null) animator.SetTrigger("doAttack");

        if (slashEffectPrefab != null)
        {
            Instantiate(slashEffectPrefab, transform.position, transform.rotation);
        }
        // 5. 恢复阶段 (可以加一个短暂的后摇等待)
        // yield return new WaitForSeconds(0.5f); 
        PerformAttack();

        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
        }
        isAttacking = false;
        enemyAI.SetMeleeAttackingState(false);
        activeWarningIndicator = null;
    }

    public void InterruptAttack()
    {
        // 1. 检查是否真的在攻击中
        if (!isAttacking)
        {
            return; // 没有在攻击，什么也不做
        }

        Debug.Log($"<color=yellow>[{gameObject.name}] Melee Attack INTERRUPTED!</color>");

        // 2. 停止正在运行的 MeleeSequence 协程
        //    (我们使用 StopAllCoroutines() 是最简单的方式，
        //     因为它也会停止我们可能添加的任何其他攻击协程)
        StopAllCoroutines(); //

        // 3. 立即销毁预警特效 (正如你要求的)
        if (activeWarningIndicator != null)
        {
            Destroy(activeWarningIndicator); //
        }

        if (animator != null)
        {
            // 1. 重置导致我们卡住的触发器
            animator.ResetTrigger("doWarning"); //
            animator.ResetTrigger("doAttack"); //

            // 2. 强制动画器回到“待机”状态
            //    (击退/眩晕协程会接管并让它保持停止)
            //    (当协程结束后，EnemyAI.UpdateAnimation 会正确地将其设置回 true)
            animator.SetBool("isMoving", false); //
        }
        if (enemyAI != null)
        {
            enemyAI.SetMeleeAttackingState(false);
        }
        // 4. 重置所有状态
        isAttacking = false; //
        activeWarningIndicator = null; //
        cooldownTimer = cooldown; // 重置冷却时间，否则怪物可能会卡住

        // 5. [关键] 恢复 NavMeshAgent 的控制权
        //    (我们的协程在第 87 行 将其设置为了 true)
        if (agent.isActiveAndEnabled && agent.isStopped)
        {
            agent.isStopped = false;
        }
        // (眩晕/击退协程会立即再次接管并设置 isStopped = true，
        // 但这确保了 *这个脚本* 不会再“霸占”控制权)
    }
    void PerformAttack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRadius);
        foreach (var hit in hits)
        {
            if (hit.transform == playerTarget)
            {
                Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
                if (attackAngle <= 0 || Vector3.Angle(transform.forward, directionToPlayer) < attackAngle / 2)
                {
                   
                    Health playerHealth = playerTarget.GetComponent<Health>();
                    if (playerHealth != null)
                    {
                        // 【核心修正】将伤害事件的发生位置，从怪物自身(transform.position)
                        // 改为玩家的位置(playerTarget.position)
                        playerHealth.TakeDamage(damage, playerTarget.position, this.gameObject, AttackType.Standard);

                        if (hitEffectPrefab != null)
                        {
                            Instantiate(hitEffectPrefab, playerTarget.position, Quaternion.identity);
                        }
                    }
                    break;
                }
            }
        }
    }
    void OnDrawGizmosSelected()
    {
        // 设置辅助线的颜色
        Gizmos.color = new Color(0, 0.8f, 1f, 0.5f); // 半透明的蓝色

        // 获取攻击的起点和方向
        Vector3 position = transform.position;
        Vector3 forward = transform.forward;
        float radius = attackRadius;
        float angle = attackAngle;

        // 如果是圆形攻击，直接画一个完整的圆盘
        if (angle >= 360 || angle <= 0)
        {
            Gizmos.DrawWireSphere(position, radius);
            return;
        }

        // 如果是扇形攻击，则绘制一个扇形
        Vector3 leftDir = Quaternion.Euler(0, -angle / 2, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, angle / 2, 0) * forward;

        // 画两条边线
        Gizmos.DrawLine(position, position + leftDir * radius);
        Gizmos.DrawLine(position, position + rightDir * radius);

        // 使用 UnityEditor.Handles 来绘制更平滑的圆弧 (这段代码只在编辑器中有效)
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawSolidArc(position, Vector3.up, leftDir, angle, radius);
#endif
    }
}