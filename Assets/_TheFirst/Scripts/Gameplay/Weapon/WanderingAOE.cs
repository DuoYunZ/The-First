using UnityEngine;

public class WanderingAOE : MonoBehaviour
{
    [Header("核心设置")]
    public Transform target; // 追踪目标
    public float wanderRadius = 7f; // 游荡半径
    public float moveSpeed = 6f; // 移动速度
    public float turnSpeed = 5f; // 转向平滑度

    [Header("防卡死设置")]
    public float reachThreshold = 0.5f; // 到达判定距离
    public float changeDestInterval = 3.0f; // 强制换点间隔

    private Vector3 currentDestination;
    private float changeTimer;
    private Rigidbody rb;
    private bool isReturning = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();        

        // 2. 锁定目标 (优先找 WeaponController，它是玩家根物体)
        if (target == null)
        {
            if (WeaponController.Instance != null)
                target = WeaponController.Instance.transform;
            else if (GameManager.Instance != null)
                target = GameManager.Instance.playerTransform;
            else
                target = transform; // 保底
        }

        PickNewDestination();
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // --- 逻辑判定 ---
        float distToPlayer = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.position.x, 0, target.position.z));
        float distToDest = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentDestination.x, 0, currentDestination.z));

        // 1. 边界检查：飞出去了吗？
        if (distToPlayer > wanderRadius)
        {
            isReturning = true;
            currentDestination = target.position; // 强制设为回圆心
        }

        // 2. 状态切换
        if (isReturning)
        {
            // 返航模式：必须回到离圆心很近的地方 (半径的 20%) 才会解除，防止在边界反复横跳
            if (distToPlayer < wanderRadius * 0.2f)
            {
                isReturning = false;
                PickNewDestination();
            }
            // 否则保持返航状态，继续飞向 currentDestination (玩家位置)
            currentDestination = target.position;
        }
        else
        {
            // 游荡模式：到了目的地，或者时间到了，就换点
            changeTimer -= Time.fixedDeltaTime;
            if (distToDest < reachThreshold || changeTimer <= 0)
            {
                PickNewDestination();
            }
        }

        // --- 移动执行 ---
        MoveKinematic();
    }

    void PickNewDestination()
    {
        // 随机取点
        Vector2 randomPoint = Random.insideUnitCircle * wanderRadius;
        currentDestination = target.position + new Vector3(randomPoint.x, 0, randomPoint.y);
        changeTimer = changeDestInterval;
    }

    void MoveKinematic()
    {
        // 计算方向 (忽略高度差)
        Vector3 dir = (currentDestination - transform.position);
        dir.y = 0;

        if (dir.sqrMagnitude > 0.001f)
        {
            // 1. 平滑转向
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }

        // 2. 强制位移 (不使用力，直接修改位置，最稳)
        // 始终朝向自己前方移动
        Vector3 moveStep = transform.forward * moveSpeed * Time.fixedDeltaTime;

        if (rb != null)
            rb.MovePosition(rb.position + moveStep);
        else
            transform.position += moveStep;
    }

    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = isReturning ? Color.red : Color.green;
            Gizmos.DrawWireSphere(target.position, wanderRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawSphere(currentDestination, 0.5f);
        }
    }
}