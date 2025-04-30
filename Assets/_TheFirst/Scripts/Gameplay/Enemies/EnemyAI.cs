using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI 设置")]
    [Tooltip("敌人移动速度")]
    public float moveSpeed = 3f;
    public int touchDamage = 5;

    private Transform playerTransform = null;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        // 不查找玩家
    }

    void FixedUpdate() // 只处理物理移动
    {
        // 获取或等待玩家引用
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null) { return; }
            }
            else { return; }
        }

        // 计算方向并移动
        Vector3 directionToPlayer = (playerTransform.position - rb.position);
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();
        Vector3 nextPosition = rb.position + directionToPlayer * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    } // <--- FixedUpdate() 函数到此结束


    // vvv--- OnTriggerEnter 必须放在类级别，不能在 FixedUpdate 内部 ---vvv
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Enemy OnTriggerEnter with: {other.name} (Tag: {other.tag})"); // 1. 碰撞日志

        // 2. 查找 Health 组件 (向上查找)
        Health playerHealth = other.GetComponentInParent<Health>();

        // 3. 检查 Health 组件是否存在，并且 Health 组件所在的 GameObject 的 Tag 是 "Player"
        if (playerHealth != null && playerHealth.CompareTag("Player")) // <-- 检查 playerHealth 的 Tag
        {
            Debug.Log($"Player Health component found on {playerHealth.gameObject.name}! Dealing {touchDamage} damage."); // 4. 成功日志
            playerHealth.TakeDamage(touchDamage); // 5. 造成伤害
            Debug.Log($"Destroying enemy {gameObject.name} after dealing damage."); // 6. 销毁日志
            Destroy(gameObject); // 7. 销毁自己
        }
        // 可以保留之前的 else if / else 日志用于调试
        else if (playerHealth != null)
        {
            Debug.LogWarning($"Found Health on {playerHealth.gameObject.name} but tag is '{playerHealth.tag}', not 'Player'.");
        }
        else
        {
            Debug.Log($"No Health component found in parents of {other.name}.");
        }
    }
    // ^^^--- OnTriggerEnter 结束 ---^^^

} // <--- EnemyAI 类结束