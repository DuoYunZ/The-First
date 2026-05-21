using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PinballAI : MonoBehaviour
{
    private Rigidbody rb;
    private float moveSpeed;
    private int touchDamage;
    private Vector3 lastVelocity;

    [Header("伤害设置")]
    public float damageCooldown = 1.0f; //
    private bool canDealDamage = true; //

    [Header("视口反弹 设置")]
    public float viewportPadding = 0.01f; //
    private Camera _mainCamera; //
    private float offScreenTimer = 0f; // [!] 用于2秒 计时

    [Header("反弹 随机化")]
    public float maxRandomBounceAngle = 15f; //

    [Header("朝向设置")]
    public float rotationSpeed = 10f; //

    [Header("反弹 冷却")]
    public float bounceCooldown = 0.2f; //
    private float lastBounceTime = -1f; //


    void Awake()
    {
        rb = GetComponent<Rigidbody>(); //
        rb.useGravity = false; //
        rb.isKinematic = false; //
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation; //

        // [!] 移除 col.isTrigger = true;

        _mainCamera = Camera.main; //
    }

    // --- (Initialize() 方法保持不变) ---
    public void Initialize(float speed, float lifetime, int damage) //
    {
        this.moveSpeed = speed;
        this.touchDamage = damage;
        Vector2 randomDir = Random.insideUnitCircle.normalized; //
        Vector3 initialVelocity = new Vector3(randomDir.x, 0, randomDir.y) * moveSpeed; //
        rb.velocity = initialVelocity; //
        lastVelocity = initialVelocity; //
        if (lifetime > 0) StartCoroutine(LifetimeDespawnRoutine(lifetime)); //
    }

    void FixedUpdate()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main; // 尝试重新获取
            if (_mainCamera == null) return;
        }

        // --- (朝向 逻辑 - 保持不变) ---
        if (rb.velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.velocity.normalized); //
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed); //
        }

        // --- vvv [ 核心修改 ] vvv ---

        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(transform.position); //

        // 1. (新) 出屏 2秒 回收逻辑
        // (检查是否在视口 0-1 范围之外)
        bool isOffScreen = viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.z < 0f || viewportPos.z > 1f;

        if (isOffScreen)
        {
            offScreenTimer += Time.fixedDeltaTime;

            if (offScreenTimer >= 2.0f) //
            {
                // 强制移回屏幕 中心
                Vector3 centerOfScreenWorld = _mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, viewportPos.z));
                Vector3 directionToCenter = (centerOfScreenWorld - transform.position).normalized;
                directionToCenter.y = 0;

                rb.velocity = directionToCenter * moveSpeed;
                lastVelocity = rb.velocity;
                offScreenTimer = 0f; // 重置
            }
        }
        else
        {
            offScreenTimer = 0f; // 在屏幕 内，重置计时器
        }

        // 2. (修改) 视口 边缘反弹 逻辑
        Vector3 reflectionNormal = Vector3.zero;
        if (viewportPos.x < viewportPadding) reflectionNormal = Vector3.right; //
        else if (viewportPos.x > 1f - viewportPadding) reflectionNormal = Vector3.left; //
        else if (viewportPos.z < viewportPadding) reflectionNormal = Vector3.forward; //
        else if (viewportPos.z > 1f - viewportPadding) reflectionNormal = Vector3.back; //

        if (reflectionNormal != Vector3.zero && Time.time > lastBounceTime + bounceCooldown) //
        {
            ApplyBounce(reflectionNormal); // [!] 调用新的反弹 方法
        }

        // 3. (保持不变) 速度维持
        if (rb.velocity.sqrMagnitude > 0.1f)
        {
            rb.velocity = rb.velocity.normalized * moveSpeed; //
            lastVelocity = rb.velocity; //
        }
        else if (Time.time > lastBounceTime + bounceCooldown)
        {
            rb.velocity = lastVelocity.normalized * moveSpeed; //
        }
        // --- ^^^ [ 核心修改 ] ^^^ ---
    }


    // --- vvv [ 核心修改 ] vvv ---
    // (重命名: OnTriggerEnter -> OnCollisionEnter)
    void OnCollisionEnter(Collision collision)
    {
        // 1. 检查是否是墙体，并且冷却 已过
        if (collision.gameObject.CompareTag("Wall") && Time.time > lastBounceTime + bounceCooldown) //
        {
            // 2. [新逻辑] 从物理碰撞中获取法线
            Vector3 normal = collision.contacts[0].normal;
            normal.y = 0;

            ApplyBounce(normal.normalized); // [!] 调用新的反弹 方法
        }
    }

    // (重命名: OnTriggerStay -> OnCollisionStay)
    void OnCollisionStay(Collision collision)
    {
        // (伤害玩家的逻辑 - 保持不变)
        if (canDealDamage && collision.gameObject.CompareTag("Player")) //
        {
            Health playerHealth = collision.gameObject.GetComponentInParent<Health>(); //
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(touchDamage, transform.position, this.gameObject, AttackType.Standard); //
                canDealDamage = false; //
                StartCoroutine(DamageCooldownRoutine()); //
            }
        }
    }
    // --- ^^^ [ 核心修改 ] ^^^ ---


    // --- vvv [ 核心新增 ] vvv ---
    /// <summary>
    /// (新增) 统一的反弹 方法
    /// </summary>
    void ApplyBounce(Vector3 normal)
    {
        lastBounceTime = Time.time; // [!] 重置冷却

        // (随机化 反弹 逻辑 - 保持不变)
        Vector3 perfectDir = Vector3.Reflect(lastVelocity, normal).normalized; //
        Quaternion randomRot = Quaternion.Euler(0, Random.Range(-maxRandomBounceAngle, maxRandomBounceAngle), 0); //
        Vector3 finalDir = randomRot * perfectDir; //

        rb.velocity = finalDir * moveSpeed; //
        lastVelocity = rb.velocity; //
    }
    // --- ^^^ [ 核心新增 ] ^^^ ---


    // --- (DamageCooldownRoutine ... 保持不变) ---
    IEnumerator DamageCooldownRoutine()
    {
        yield return new WaitForSeconds(damageCooldown); //
        canDealDamage = true; //
    }

    private IEnumerator LifetimeDespawnRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        Health health = GetComponent<Health>();
        if (health != null && !health.IsDead && gameObject.CompareTag("Enemy"))
        {
            GameTimelineManager.Instance?.EnemyRemovedWithoutKill("pinball-lifetime");
        }

        Destroy(gameObject);
    }
}
