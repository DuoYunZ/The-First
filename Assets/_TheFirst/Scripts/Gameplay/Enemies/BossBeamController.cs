// --- BossBeamController.cs (Raycast伤害与视觉统一最终版) ---
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BossBeamController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform target;
    private GameObject attacker;

    // 特效与伤害相关
    private GameObject impactVfxPrefab;
    private GameObject impactEffectInstance; // 【修正】命中特效实例变量
    private int damagePerTick;
    private float tickInterval;
    private float tickTimer;

    // 光束行为相关
    private Vector3 currentBeamDirection;
    private float beamTurnSpeed;
    private float maxDistance;
    public LayerMask hitLayers; // 【重要】射线可以命中的层

    // 地面印记相关变量
    private GameObject scorchMarkPrefab;
    private float scorchMarkInterval;
    private LayerMask groundLayer;
    private float scorchMarkTimer;


    public void Initialize(WeaponStatBlock data, GameObject attacker, Transform target)
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.target = target;
        this.attacker = attacker;

        this.impactVfxPrefab = data.beamImpactVfxPrefab;
        this.tickInterval = (data.beamDamageTickRate > 0) ? (1f / data.beamDamageTickRate) : 0f;
        this.damagePerTick = (this.tickInterval > 0) ? Mathf.CeilToInt((float)data.beamDamagePerSecond / data.beamDamageTickRate) : 0;
        this.beamTurnSpeed = data.beamTurnSpeed;
        this.maxDistance = data.beamMaxDistance;
        this.scorchMarkPrefab = data.scorchMarkPrefab;
        this.scorchMarkInterval = data.scorchMarkInterval;
        this.groundLayer = data.beamScorchMarkGroundLayer;

        if (target != null)
        {
            this.currentBeamDirection = (target.position - transform.position).normalized;
        }
        else
        {
            this.currentBeamDirection = transform.forward;
        }

        // 【修正】在初始化时，提前生成命中特效实例并隐藏
        if (this.impactVfxPrefab != null)
        {
            impactEffectInstance = Instantiate(this.impactVfxPrefab);
            impactEffectInstance.SetActive(false);
        }

        lineRenderer.enabled = true;
    }

    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        // 1. 平滑更新光束的追踪方向
        Vector3 desiredDirection = (target.position - transform.position).normalized;
        currentBeamDirection = Vector3.Slerp(currentBeamDirection, desiredDirection, beamTurnSpeed * Time.deltaTime);

        Vector3 startPoint = transform.position;
        Vector3 endPoint;

        // 2. 进行射线检测，处理所有命中逻辑
        RaycastHit hit;
        if (Physics.Raycast(transform.position, currentBeamDirection, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide))
        {
            endPoint = hit.point;

            // 【修正】更新命中特效的位置并显示
            if (impactEffectInstance != null)
            {
                impactEffectInstance.SetActive(true);
                impactEffectInstance.transform.position = hit.point;
                impactEffectInstance.transform.rotation = Quaternion.LookRotation(hit.normal);
            }

            // 伤害判定逻辑
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                Health playerHealth = hit.collider.GetComponentInParent<Health>();
                if (playerHealth != null && hit.collider.CompareTag("Player"))
                {
                    playerHealth.TakeDamage(damagePerTick, hit.point, attacker, AttackType.Standard);
                }
            }

            // 地面印记逻辑
            if (scorchMarkPrefab != null && groundLayer == (groundLayer | (1 << hit.collider.gameObject.layer)))
            {
                scorchMarkTimer += Time.deltaTime;
                if (scorchMarkTimer >= scorchMarkInterval)
                {
                    scorchMarkTimer = 0f;
                    Instantiate(scorchMarkPrefab, hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
                }
            }
        }
        else
        {
            endPoint = transform.position + currentBeamDirection * maxDistance;

            // 【修正】如果没命中，则隐藏特效
            if (impactEffectInstance != null)
            {
                impactEffectInstance.SetActive(false);
            }
        }

        // 3. 更新LineRenderer的视觉表现
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
    }

    void OnDestroy()
    {
        // 【修正】在销毁时，清理特效实例，防止内存泄漏
        if (impactEffectInstance != null)
        {
            Destroy(impactEffectInstance);
        }
    }

    // OnDestroy 和 OnTriggerStay 不再需要
}