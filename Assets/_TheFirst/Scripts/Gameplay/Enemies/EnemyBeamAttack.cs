// EnemyBeamAttack.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Animator))]
public class EnemyBeamAttack : MonoBehaviour
{
    [Header("攻击数据")]
    [Tooltip("将对应此攻击的 EnemyAttackData 资产拖到这里")]
    public EnemyAttackData attackData;


    [Header("攻击设置")]
    public Transform firePoint;
    [Tooltip("进入此范围后，怪物会准备发射激光")]
    public float attackRange = 20f;
    [Tooltip("攻击前摇/播放动画的时间")]
    public float animationDuration = 1f;

    // 私有变量
    private Transform playerTarget;
    private float cooldownTimer;
    private EnemyAI enemyAI;
    private Animator animator;
    private bool isAttacking = false;
    private EnemyBeamController activeBeamInstance = null;
    private int attackCycleCounter = 0; // 【新增】攻击轮次计数器
    private float durationTimer;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerAimTarget;
        }
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();

        // 确保敌人有 "Enemy" 标签，以便LaserBeamController能正确识别目标
        if (!gameObject.CompareTag("Enemy"))
        {
            Debug.LogWarning($"激光敌人 '{gameObject.name}' 缺少 'Enemy' 标签，可能会导致伤害判定失败！");
        }
    }

    void Update()
    {
        if (playerTarget == null || isAttacking || attackData == null) return;

        // 更新冷却计时器
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 检查是否可以攻击
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer <= attackRange && cooldownTimer <= 0f)
        {
            StartCoroutine(BeamSequence());
        }
    }

    IEnumerator BeamSequence()
    {
        isAttacking = true;
        enemyAI.enabled = false;
        if (GetComponent<Rigidbody>() != null) GetComponent<Rigidbody>().velocity = Vector3.zero;

        transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(animationDuration);

        if (activeBeamInstance != null) Destroy(activeBeamInstance.gameObject);

        if (attackData.beamVfxPrefab != null)
        {
            GameObject beamGO = Instantiate(attackData.beamVfxPrefab, firePoint.position, firePoint.rotation, firePoint);
            activeBeamInstance = beamGO.GetComponent<EnemyBeamController>(); // 获取新脚本的引用
            if (activeBeamInstance != null)
            {
                // 使用新脚本的初始化方法
                activeBeamInstance.Initialize(attackData, this.gameObject, playerTarget);
            }
        }

        durationTimer = attackData.beamDuration;

        // --- 【核心修改】循环中不再更新光束位置，只检查距离和计时 ---
        while (durationTimer > 0f)
        {
            if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy || Vector3.Distance(transform.position, playerTarget.position) > attackRange)
            {
                break;
            }

            // 不再需要在这里调用 UpdateBeamPositions
            durationTimer -= Time.deltaTime;
            yield return null;
        }

        StopAndCleanupBeam();

        cooldownTimer = attackData.beamCooldown;
        isAttacking = false;
        if (enemyAI != null) enemyAI.enabled = true;
    }
    private void StopAndCleanupBeam()
    {
        if (activeBeamInstance != null)
        {
            Destroy(activeBeamInstance.gameObject);
            activeBeamInstance = null;
        }
    }

    public float GetRemainingDuration()
    {
        return durationTimer;
    }
    private void OnDestroy()
    {
        StopAndCleanupBeam();
    }
}