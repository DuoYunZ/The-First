using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class StraightMoverAI : MonoBehaviour
{
    private float moveSpeed;
    private Vector3 moveDirection;
    private Rigidbody rb;
    private int touchDamage = 5;

    private bool isStunned = false;
    private Vector3 savedVelocity;

    private Animator animator;

    [Header("伤害设置")]
    [Tooltip("怪物每次造成伤害后的冷却时间（秒）")]
    public float damageCooldown = 1.0f;
    private bool canDealDamage = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        // 锁定Y轴位置和所有旋转，我们只通过脚本控制速度
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 由 EnemySpawner 调用的初始化方法
    /// </summary>
    public void Initialize(float speed, float lifetime, Vector3 direction, int damage)
    {
        this.moveSpeed = speed;
        this.moveDirection = direction.normalized;
        this.touchDamage = damage;

        // 立即设置固定的移动速度
        rb.velocity = this.moveDirection * this.moveSpeed;

        // 让怪物面朝移动方向
        if (this.moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(this.moveDirection);
        }

        if (animator != null)
        {
            animator.SetBool("isMoving", true);
        }

        // 在 'lifetime' 秒后自动销毁
        Destroy(gameObject, lifetime);
    }

    public void SetStunned(bool stunned)
    {
        isStunned = stunned;
        if (rb == null) return;

        if (stunned)
        {
            // 保存当前速度并停止
            savedVelocity = rb.velocity;
            rb.velocity = Vector3.zero;
        }
        else
        {
            // 仅在 Rigidbody 停止时才恢复速度（防止覆盖其他物理交互）
            if (rb.velocity.sqrMagnitude < 0.1f)
            {
                rb.velocity = savedVelocity;
            }
        }
        if (animator != null)
        {
            // 如果眩晕，设置 "isMoving" 为 false (播放待机)
            // 如果眩晕结束，设置 "isMoving" 为 true (恢复跑步)
            animator.SetBool("isMoving", !stunned);
        }
    }
    // --- 碰撞伤害逻辑 ---
    void OnTriggerStay(Collider other)
    {
        if (isStunned) return;

        if (canDealDamage && other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(touchDamage, transform.position, this.gameObject, AttackType.Standard);
                canDealDamage = false;
                StartCoroutine(DamageCooldownRoutine());
            }
        }
    }

    IEnumerator DamageCooldownRoutine()
    {
        yield return new WaitForSeconds(damageCooldown);
        canDealDamage = true;
    }
}