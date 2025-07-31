// ReflectedBeam.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ReflectedBeam : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform enemyTarget;
    private EnemyBeamController sourceBeam;
    private GameObject impactVfxPrefab;
    private GameObject activeImpactVfxInstance;
    private GameObject playerAttacker;
    private int damagePerTick;
    private float tickInterval;
    private float tickTimer;
    //private float remainingDuration;

    // 由 PlayerShield 调用
    public void Initialize(EnemyAttackData data, GameObject player, Transform target, EnemyBeamController sourceBeam)
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.enemyTarget = target;
        this.sourceBeam = sourceBeam; // 存储原始光束的控制器引用
        this.playerAttacker = player;
        this.impactVfxPrefab = data.beamImpactVfxPrefab;
        this.damagePerTick = Mathf.CeilToInt((float)data.beamDamagePerSecond / data.beamDamageTickRate);
        this.tickInterval = 1f / data.beamDamageTickRate;
    }

    void Update()
    {
        // 如果原始攻击者、当前目标死亡，或持续时间耗尽，则光束立即消失
        if (sourceBeam == null || enemyTarget == null || !enemyTarget.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        //remainingDuration -= Time.deltaTime;

        // 更新视觉
        Vector3 startPoint = transform.position;
        Transform aimPoint = enemyTarget.Find("AimTargetPoint");
        Vector3 endPoint = (aimPoint != null) ? aimPoint.position : enemyTarget.position; // 找到就用，找不到就用根坐标
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // ... (命中特效和伤害逻辑与 PlayerBeamController 类似)
        if (impactVfxPrefab != null)
        {
            if (activeImpactVfxInstance == null) activeImpactVfxInstance = Instantiate(impactVfxPrefab, endPoint, Quaternion.identity);
            activeImpactVfxInstance.transform.position = endPoint;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            Health enemyHealth = enemyTarget.GetComponentInParent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damagePerTick, endPoint, playerAttacker, AttackType.Standard);
            }
        }
    }

    void OnDestroy()
    {
        if (activeImpactVfxInstance != null) Destroy(activeImpactVfxInstance);
    }
}