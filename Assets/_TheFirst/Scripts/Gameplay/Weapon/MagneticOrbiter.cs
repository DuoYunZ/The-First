using UnityEngine;
using System.Collections.Generic;

public class MagneticOrbiter : MonoBehaviour
{
    [Header("磁暴属性")]
    public int damagePerSecond = 20;
    public float damageInterval = 0.2f;
    public float pullRadius = 5f;
    public float pullSpeed = 10f;

    [Header("闪电特效")]
    [Tooltip("闪电链条的特效 (连线)")]
    public GameObject chainLightningVfxPrefab;
    [Tooltip("闪电命中敌人的特效 (火花)")]
    public GameObject impactVfxPrefab; // <--- 【新增】受击特效字段

    public int maxChainTargets = 3;
    public float chainRange = 6f;

    [Header("层级设置")]
    public LayerMask enemyLayer;

    private float damageTimer;
    private WeaponPart ownerWeapon;

    public void Initialize(int baseDamage, WeaponPart weapon)
    {
        this.damagePerSecond = baseDamage;
        this.ownerWeapon = weapon;
    }

    void Update()
    {
        PullEnemies();

        damageTimer += Time.deltaTime;
        if (damageTimer >= damageInterval)
        {
            damageTimer = 0f;
            ZapEnemies();
        }
    }

    void PullEnemies()
    {
        // ... (吸附逻辑保持不变，不需要改) ...
        Collider[] enemies = Physics.OverlapSphere(transform.position, pullRadius, enemyLayer);
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                Vector3 targetPos = transform.position;
                Vector3 enemyPos = col.transform.position;
                targetPos.y = enemyPos.y;

                if (Vector3.Distance(enemyPos, targetPos) > 1.5f)
                {
                    col.transform.position = Vector3.MoveTowards(enemyPos, targetPos, pullSpeed * Time.deltaTime);
                }
            }
        }
    }

    void ZapEnemies()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, pullRadius, enemyLayer);
        if (enemies.Length == 0) return;

        Transform bestTarget = null;
        float minDist = float.MaxValue;

        foreach (var col in enemies)
        {
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minDist)
            {
                minDist = d;
                bestTarget = col.transform;
            }
        }

        if (bestTarget != null)
        {
            Health targetHealth = bestTarget.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                // 1. 造成主伤害
                targetHealth.TakeDamage(damagePerSecond, bestTarget.position, ownerWeapon != null ? ownerWeapon.gameObject : gameObject, AttackType.Standard);

                // --- 【核心修改】将特效挂载到敌人身上 (bestTarget) ---
                if (impactVfxPrefab != null)
                {
                    // 第四个参数 bestTarget 表示将特效设为敌人的子物体
                    Instantiate(impactVfxPrefab, bestTarget.position, Quaternion.identity, bestTarget);
                }
                // ------------------------------------------------

                // 2. 触发连锁闪电
                if (ownerWeapon != null)
                {
                    ownerWeapon.ChainLightningFromTarget(
                        bestTarget,
                        maxChainTargets,
                        Mathf.RoundToInt(damagePerSecond * 0.5f),
                        chainRange,
                        chainLightningVfxPrefab,
                        impactVfxPrefab
                    );
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}