using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlamethrowerTurret : MonoBehaviour
{
    [Header("视觉组件")]
    public Transform turretHead;       // 炮塔头部（用于旋转瞄准）
    public ParticleSystem flameVfx;    // 火焰粒子特效
    public GameObject spawnVfx;        // 生成时的特效（可选）
    public GameObject despawnVfx;      // 消失时的特效（可选）
    public Animator animator;          // 动画控制器（如有：Play("Spawn"), Play("Despawn")）

    [Header("配置参数")]
    public float rotateSpeed = 5f;     // 炮塔转速
    public float flameAngle = 45f;     // 喷火角度（扇形一半）
    public float flameRange = 6f;      // 喷火距离

    // --- 运行时数据 ---
    private int damagePerTick;
    private float duration;
    private float tickInterval = 0.2f; // 喷火伤害频率
    private GameObject owner;          // 伤害来源（玩家）
    private string weaponName;         // 用于统计伤害

    private bool isFiring = false;
    private float tickTimer = 0f;
    private Transform currentTarget;

    // 初始化方法（由 WeaponPart 调用）
    public void Initialize(int damage, float lifetime, GameObject ownerObj, float range, float interval, string name)
    {
        this.damagePerTick = damage;
        this.duration = lifetime;
        this.owner = ownerObj;
        this.tickInterval = interval;
        this.weaponName = name;

        // 1. 接收来自 ScriptableObject 的射程
        this.flameRange = range;       
        

        StartCoroutine(LifeCycleRoutine());
    }

    private IEnumerator LifeCycleRoutine()
    {
        // =================================================
        // 阶段 1: 出生 (Spawn)
        // =================================================

        // 强制清除所有残留粒子，防止复用对象池时带出来的脏数据
        if (flameVfx != null)
        {
            flameVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (spawnVfx != null) Instantiate(spawnVfx, transform.position, Quaternion.identity);

        if (animator != null)
        {
            animator.Play("Spawn");
            // 等待动画播完（这里假设是1秒，你可以根据实际情况调整）
            yield return new WaitForSeconds(1.0f);
        }

        // =================================================
        // 阶段 2: 攻击 (Attack)
        // =================================================
        isFiring = true;

        if (flameVfx != null)
        {
            flameVfx.Play();
        }

        float activeTimer = duration;
        while (activeTimer > 0)
        {
            activeTimer -= Time.deltaTime;
            HandleTargetingAndDamage(); // 这里面有旋转逻辑
            yield return null;
        }

        // =================================================
        // 阶段 3: 消失 (Despawn)
        // =================================================
        isFiring = false;

        // 【核心修改】Despawn 时，不仅停止发射，还要清除残留粒子
        // 这样塔缩回去的时候，火会瞬间消失，不会飘在空中穿帮
        if (flameVfx != null)
        {
            // StopEmittingAndClear = 停止发射并清除所有当前粒子
            flameVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (animator != null)
        {
            animator.Play("Despawn");
            yield return new WaitForSeconds(1.0f);
        }

        if (despawnVfx != null) Instantiate(despawnVfx, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    private void HandleTargetingAndDamage()
    {
        // --- A. 寻找目标 ---
        if (currentTarget == null || IsTargetInvalid(currentTarget))
        {
            currentTarget = FindNearestEnemy();
        }

        // --- B. 旋转炮塔 ---
        if (currentTarget != null && turretHead != null)
        {
            Vector3 dir = currentTarget.position - turretHead.position;
            dir.y = 0; // 仅水平旋转
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                turretHead.rotation = Quaternion.Slerp(turretHead.rotation, targetRot, Time.deltaTime * rotateSpeed);
            }
        }

        // --- C. 造成伤害 (扇形检测) ---
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0)
        {
            tickTimer = tickInterval;
            ApplyFlameDamage();
        }
    }

    private void ApplyFlameDamage()
    {
        // 使用 OverlapSphere 获取范围内所有敌人
        Collider[] hits = Physics.OverlapSphere(transform.position, flameRange, LayerMask.GetMask("Enemies")); // 需确保层级正确

        Vector3 forward = turretHead != null ? turretHead.forward : transform.forward;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            // 计算角度：如果 目标在前方 +/- (flameAngle/2) 度内
            if (Vector3.Angle(forward, dirToTarget) < flameAngle / 2)
            {
                Health h = hit.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    // 造成伤害
                    h.TakeDamage(damagePerTick, hit.transform.position, owner, AttackType.Standard, null, null, weaponName);
                }
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, flameRange * 1.5f, LayerMask.GetMask("Enemies"));
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist) { minDist = d; nearest = hit.transform; }
            }
        }
        return nearest;
    }

    private bool IsTargetInvalid(Transform t)
    {
        if (t == null) return true;
        if (!t.gameObject.activeInHierarchy) return true;
        // 如果你的敌人有 Health 且会死亡，最好检查 IsDead
        var h = t.GetComponentInParent<Health>();
        return h != null && h.IsDead;
    }

    // 辅助显示范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, flameRange);

        if (turretHead != null)
        {
            Vector3 left = Quaternion.Euler(0, -flameAngle / 2, 0) * turretHead.forward * flameRange;
            Vector3 right = Quaternion.Euler(0, flameAngle / 2, 0) * turretHead.forward * flameRange;
            Gizmos.DrawLine(turretHead.position, turretHead.position + left);
            Gizmos.DrawLine(turretHead.position, turretHead.position + right);
        }
    }
}