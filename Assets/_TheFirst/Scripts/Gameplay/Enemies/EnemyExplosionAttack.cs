// EnemyExplosionAttack.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyExplosionAttack : MonoBehaviour
{ 

    private Transform playerTarget;
    private EnemyAI enemyAI;
    private Health health;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private EnemyType enemyData; // 用于存储从Spawner传入的数据

    private bool isAttacking = false;
    private bool requestedAttack = false;


    public void Initialize(EnemyType data)
    {
        this.enemyData = data;
    }

    void Start()
    {
        // 获取核心组件的引用
        enemyAI = GetComponent<EnemyAI>();
        health = GetComponent<Health>();

        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }

        // 安全检查
        if (enemyData == null)
        {
            Debug.LogWarning($"'{gameObject.name}' 上的 EnemyExplosionAttack 没有被正确初始化 EnemyType 数据！", this);
            enabled = false;
        }
    }

    public void OnJumpFinished()
    {
        // 开始执行跳跃结束后的逻辑（地面预警 -> 爆炸）
        StartCoroutine(ArmAndExplodeSequenceAfterJump());
    }


    void Update()
    {
        if (playerTarget == null || requestedAttack || health.IsDead) return;

        // 【关键修改】现在检查的是 EnemyAI 的公共状态 CurrentState
        // 只有当AI处于追逐状态，并且满足距离条件时，才“请求”攻击
        if (enemyAI.CurrentState == EnemyAI.AIState.Chasing &&
            Vector3.Distance(transform.position, playerTarget.position) <= enemyData.jumpTriggerRange)
        {
            requestedAttack = true;
            // 向主控制器“请求”执行跳跃攻击，传递所有需要的参数
            enemyAI.RequestJumpAttack(playerTarget.position, enemyData.jumpAirTime, enemyData.jumpArcHeight);
        }
    }

    IEnumerator ArmAndExplodeSequenceAfterJump()
    {
        GameObject warningIndicator = null;
        if (enemyData.armingWarningPrefab != null)
        {
            warningIndicator = Instantiate(enemyData.armingWarningPrefab, transform.position, Quaternion.identity);
            warningIndicator.transform.localScale = Vector3.one * enemyData.explosionRadius * 2;
        }

        yield return new WaitForSeconds(enemyData.armingTime);

        if (warningIndicator != null) Destroy(warningIndicator);

        Explode();
    }

    private void Explode()
    {
        // 1. 让自己死亡（但不立即销毁，以便完成爆炸逻辑）
        health.Die(false);

        // 2. 播放爆炸特效
        if (enemyData.explosionVfxPrefab != null)
        {
            Instantiate(enemyData.explosionVfxPrefab, transform.position, Quaternion.identity);
        }

        // 3. 进行范围伤害检测
        Collider[] hits = Physics.OverlapSphere(transform.position, enemyData.explosionRadius, LayerMask.GetMask("Player"));
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<Health>(out var playerHealth))
            {
                playerHealth.TakeDamage(enemyData.explosionDamage, playerHealth.transform.position, gameObject, AttackType.Standard);
            }
        }

        // 4. 【重要】在所有操作完成后，通知AI主控制器，攻击流程结束
        if (enemyAI != null)
        {
            enemyAI.ResumeNormalBehavior();
        }

        // 5. 最后，销毁自己
        Destroy(gameObject);
    }

    // 在场景中绘制辅助线，方便调试
    void OnDrawGizmosSelected()
    {
        if (enemyData != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, enemyData.armingRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.explosionRadius);
        }
    }
}