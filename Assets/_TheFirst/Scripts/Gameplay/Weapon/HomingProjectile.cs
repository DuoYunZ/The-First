using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingProjectile : MonoBehaviour
{
    [Header("追踪设置")]
    public float turnSpeed = 40f; // 【改动】必须很大，因为延迟过后需要急转弯
    public float searchRadius = 30f;
    public float speed = 20f;

    [Header("弹道控制 (核心)")]
    [Tooltip("子弹发射后多少秒才开始追踪？(建议 0.2 - 0.5)")]
    public float homingDelay = 0.25f; // 【新增】延迟追踪时间

    [Header("部位瞄准")]
    public string targetPartName = "AimTargetPoint";
    public LayerMask enemyLayer;

    private Transform target;
    private Rigidbody rb;
    private Projectile baseProjectile;
    private float startTime; // 记录发射时间

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        baseProjectile = GetComponent<Projectile>();
        startTime = Time.time; // 记录出生时间

        if (baseProjectile != null)
        {
            this.speed = baseProjectile.speed;
            baseProjectile.enabled = false;
        }

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;

        FindNearestTarget();

        // 给初始速度一个推力，保证在延迟期间它是飞行的
        rb.velocity = transform.forward * speed;
    }

    void FixedUpdate()
    {
        // 1. 如果还在延迟期，只做直线飞行，不追踪
        if (Time.time < startTime + homingDelay)
        {
            rb.velocity = transform.forward * speed;
            return;
        }

        // --- 以下是原本的追踪逻辑 ---

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            FindNearestTarget();
            if (target == null)
            {
                rb.velocity = transform.forward * speed;
                return;
            }
        }

        Vector3 aimPos = target.position;
        Transform aimPoint = target.Find(targetPartName);

        if (aimPoint != null) aimPos = aimPoint.position;
        else aimPos += Vector3.up * 1.0f;

        Vector3 direction = (aimPos - transform.position).normalized;

        // 转弯
        Vector3 newDirection = Vector3.RotateTowards(transform.forward, direction, turnSpeed * Time.fixedDeltaTime, 0.0f);

        transform.rotation = Quaternion.LookRotation(newDirection);
        rb.velocity = transform.forward * speed;
    }

    void FindNearestTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, searchRadius, enemyLayer);
        float minDist = float.MaxValue;
        Transform bestTarget = null;

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                float d = Vector3.Distance(transform.position, col.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    bestTarget = col.transform;
                }
            }
        }
        target = bestTarget;
    }
}