// PlayerBeamController.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerBeamController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform target;
    private GameObject attacker;
    private WeaponStatBlock stats;

    private GameObject activeImpactVfxInstance;
    private float tickTimer;
    private int damagePerTick;

    // 由 WeaponPart 调用
    public void Initialize(WeaponStatBlock stats, GameObject attacker, Transform target)
    {
        this.lineRenderer = GetComponent<LineRenderer>();
        this.stats = stats;
        this.attacker = attacker;
        this.target = target;

        this.damagePerTick = Mathf.CeilToInt((float)stats.beamDamagePerSecond / stats.beamDamageTickRate);
    }

    void Update()
    {
        // 如果目标丢失，WeaponPart会销毁我们，所以这里只需要保证目标有效时才工作
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            // 安全起见，如果目标丢失也自我销毁
            Destroy(gameObject);
            return;
        }

        // 1. 更新视觉
        Vector3 startPoint = transform.position;
        Transform aimPoint = target.Find("AimTargetPoint");
        Vector3 endPoint = (aimPoint != null) ? aimPoint.position : target.position;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // 2. 更新命中特效
        if (stats.beamImpactVfxPrefab != null)
        {
            if (activeImpactVfxInstance == null)
                activeImpactVfxInstance = Instantiate(stats.beamImpactVfxPrefab, endPoint, Quaternion.identity);
            activeImpactVfxInstance.transform.position = endPoint;
        }

        // 3. 造成伤害
        tickTimer += Time.deltaTime;
        if (tickTimer >= (1f / stats.beamDamageTickRate))
        {
            tickTimer = 0f;
            Health enemyHealth = target.GetComponentInParent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damagePerTick, endPoint, attacker, AttackType.Standard);
            }
        }
    }

    void OnDestroy()
    {
        if (activeImpactVfxInstance != null) Destroy(activeImpactVfxInstance);
    }
}