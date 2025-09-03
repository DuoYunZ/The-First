// EnemyExplosionAttack.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))] // 确保有刚体
public class EnemyExplosionAttack : MonoBehaviour
{
    private Transform playerTarget;
    private EnemyAI enemyAI;
    private Health health;
    private Rigidbody rb;
    private EnemyType enemyData; // 用于存储从Spawner传入的数据

    private bool isAttacking = false;


    public void Initialize(EnemyType data)
    {
        this.enemyData = data;
    }

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();

        if (GameManager.Instance != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
        }

        // 这是一个安全检查，正常情况下 EnemySpawner 会调用 Initialize
        if (enemyData == null)
        {
            Debug.LogWarning($"'{gameObject.name}' 上的 EnemyExplosionAttack 没有被正确初始化 EnemyType 数据！", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (playerTarget == null || isAttacking || health.IsDead) return;

        // 【修改】现在使用 jumpTriggerRange 来触发攻击
        if (Vector3.Distance(transform.position, playerTarget.position) <= enemyData.jumpTriggerRange)
        {
            StartCoroutine(ArmAndExplodeSequence());
        }
    }

    IEnumerator ArmAndExplodeSequence()
    {
        isAttacking = true;
        enemyAI.enabled = false;
        rb.velocity = Vector3.zero;
        // 在跳跃期间禁用重力，因为我们将手动控制其轨迹
        rb.useGravity = false;

        // (可选) 可以在这里增加一个短暂的攻击前摇，比如蓄力动作
        // yield return new WaitForSeconds(0.5f);

        // --- 抛物线跳跃逻辑 ---
        if (enemyData.hasJumpAttack && playerTarget != null)
        {
            Vector3 startPoint = transform.position;
            Vector3 endPoint = playerTarget.position; // 锁定跳跃开始时的玩家位置

            float jumpDuration = enemyData.jumpAirTime;
            float timer = 0f;

            while (timer < jumpDuration)
            {
                // 如果中途玩家消失了，就中断跳跃
                if (playerTarget == null) yield break;

                // t 是从0到1的插值进度
                float t = timer / jumpDuration;

                // 计算水平位置的线性插值
                Vector3 horizontalPosition = Vector3.Lerp(startPoint, endPoint, t);

                // 计算垂直位置的抛物线运动 (先上升后下降)
                float verticalPosition = Mathf.Sin(t * Mathf.PI) * enemyData.jumpArcHeight;

                // 合成最终位置
                transform.position = new Vector3(horizontalPosition.x, startPoint.y + verticalPosition, horizontalPosition.z);

                timer += Time.deltaTime;
                yield return null; // 等待下一帧
            }
            // 确保精确落点
            transform.position = endPoint;
        }

        // --- 跳跃结束，开始地面预警和引爆 ---
        GameObject warningIndicator = null;
        if (enemyData.armingWarningPrefab != null)
        {
            warningIndicator = Instantiate(enemyData.armingWarningPrefab, transform.position, Quaternion.identity);
            warningIndicator.transform.localScale = Vector3.one * enemyData.explosionRadius * 2;
        }

        // 等待落地后的准备时间
        yield return new WaitForSeconds(enemyData.armingTime);

        if (warningIndicator != null) Destroy(warningIndicator);

        Explode();
    }

    private void Explode()
    {
        health.Die(false);

        // 2. 播放爆炸特效
        if (enemyData.explosionVfxPrefab != null)
        {
            Instantiate(enemyData.explosionVfxPrefab, transform.position, Quaternion.identity);
        }

        // 3. 进行范围伤害检测
        // 这部分代码现在可以安全执行，即使它导致玩家死亡
        Collider[] hits = Physics.OverlapSphere(transform.position, enemyData.explosionRadius, LayerMask.GetMask("Player"));
        foreach (Collider hit in hits)
        {
            Health playerHealth = hit.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(enemyData.explosionDamage, playerHealth.transform.position, gameObject, AttackType.Standard);
            }
        }

        // 4. 在完成所有操作后，手动销毁自己
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