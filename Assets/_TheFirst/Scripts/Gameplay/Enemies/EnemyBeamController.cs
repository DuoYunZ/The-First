// EnemyBeamController.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EnemyBeamController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform playerTarget;
    private GameObject impactVfxPrefab;
    private GameObject activeImpactVfxInstance;
    private GameObject attacker;
    private int damagePerTick;
    private float tickInterval;
    private AttackType attackType;
    private float tickTimer;

    public void Initialize(EnemyAttackData data, GameObject attacker, Transform target)
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.playerTarget = target;
        this.attacker = attacker;
        this.impactVfxPrefab = data.beamImpactVfxPrefab; // 只使用常规命中特效
        this.damagePerTick = Mathf.CeilToInt((float)data.beamDamagePerSecond / data.beamDamageTickRate);
        this.tickInterval = 1f / data.beamDamageTickRate;
        this.attackType = data.attackType;
    }

    void Update()
    {
        if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy)
        {
            Destroy(gameObject); // 如果目标丢失，光束立即消失
            return;
        }

        // 更新视觉
        Vector3 startPoint = transform.position;
        Vector3 endPoint = playerTarget.position;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // 更新命中特效
        if (impactVfxPrefab != null)
        {
            if (activeImpactVfxInstance == null) activeImpactVfxInstance = Instantiate(impactVfxPrefab, endPoint, Quaternion.identity);
            activeImpactVfxInstance.transform.position = endPoint;
        }

        // 造成伤害
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            Health playerHealth = playerTarget.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damagePerTick, endPoint, attacker, attackType, null, attacker.GetComponent<EnemyBeamAttack>());
            }
        }
    }

    void OnDestroy()
    {
        if (activeImpactVfxInstance != null) Destroy(activeImpactVfxInstance);
    }
}