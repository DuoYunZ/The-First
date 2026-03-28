using UnityEngine;
using System.Collections;

/// <summary>
/// 挂在榴弹实例上的独立飞行脚本（由 EnemyGrenadeAttack 动态添加）
/// 榴弹会独立飞行，即使投掷者死亡也能完成爆炸
/// </summary>
public class GrenadeFlyer : MonoBehaviour
{
    // 飞行参数（由 EnemyGrenadeAttack 在 Initialize 时设置）
    private Vector3 startPos;
    private Vector3 targetPos;
    private float arcHeight;
    private float flightDuration;
    private float explosionRadius;
    private int explosionDamage;
    private GameObject explosionVfxPrefab;
    private GameObject attackerRef; // 攻击来源引用

    private float elapsed = 0f;
    private bool hasExploded = false;

    /// <summary>
    /// 初始化榴弹飞行参数
    /// </summary>
    public void Initialize(Vector3 from, Vector3 to, float height, float duration,
                           float radius, int damage, GameObject vfxPrefab, GameObject attacker)
    {
        startPos = from;
        targetPos = to;
        arcHeight = height;
        flightDuration = duration;
        explosionRadius = radius;
        explosionDamage = damage;
        explosionVfxPrefab = vfxPrefab;
        attackerRef = attacker;
    }

    void Update()
    {
        if (hasExploded) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);

        // 水平线性插值
        Vector3 horizontalPos = Vector3.Lerp(startPos, targetPos, t);

        // 垂直抛物线：y = 4h * t * (1-t)，最高点在 t=0.5
        float yOffset = 4f * arcHeight * t * (1f - t);
        transform.position = new Vector3(horizontalPos.x, horizontalPos.y + yOffset, horizontalPos.z);

        // 让榴弹朝飞行方向旋转
        if (t < 0.99f)
        {
            float nextT = Mathf.Clamp01(t + 0.02f);
            Vector3 nextHoriz = Vector3.Lerp(startPos, targetPos, nextT);
            float nextY = 4f * arcHeight * nextT * (1f - nextT);
            Vector3 nextPos = new Vector3(nextHoriz.x, nextHoriz.y + nextY, nextHoriz.z);
            Vector3 flyDir = nextPos - transform.position;
            if (flyDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flyDir);
            }
        }

        // 到达目标：爆炸
        if (t >= 1f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 播放爆炸特效
        if (explosionVfxPrefab != null)
        {
            Instantiate(explosionVfxPrefab, targetPos, Quaternion.identity);
        }

        // 范围伤害：直接检测玩家距离，只造成一次伤害
        Transform playerTransform = null;
        if (GameManager.Instance != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(playerTransform.position, targetPos);
            if (dist <= explosionRadius)
            {
                Health playerHealth = playerTransform.GetComponentInParent<Health>();
                if (playerHealth == null)
                {
                    playerHealth = playerTransform.GetComponent<Health>();
                }
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(explosionDamage, targetPos, attackerRef, AttackType.Standard);
                }
            }
        }

        // 销毁自身
        Destroy(gameObject);
    }
}
