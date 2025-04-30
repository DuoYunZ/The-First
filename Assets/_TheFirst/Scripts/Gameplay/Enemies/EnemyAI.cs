using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI ����")]
    [Tooltip("�����ƶ��ٶ�")]
    public float moveSpeed = 3f;
    public int touchDamage = 5;

    private Transform playerTransform = null;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        // ���������
    }

    void FixedUpdate() // ֻ���������ƶ�
    {
        // ��ȡ��ȴ��������
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null) { return; }
            }
            else { return; }
        }

        // ���㷽���ƶ�
        Vector3 directionToPlayer = (playerTransform.position - rb.position);
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();
        Vector3 nextPosition = rb.position + directionToPlayer * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    } // <--- FixedUpdate() �������˽���


    // vvv--- OnTriggerEnter ��������༶�𣬲����� FixedUpdate �ڲ� ---vvv
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Enemy OnTriggerEnter with: {other.name} (Tag: {other.tag})"); // 1. ��ײ��־

        // 2. ���� Health ��� (���ϲ���)
        Health playerHealth = other.GetComponentInParent<Health>();

        // 3. ��� Health ����Ƿ���ڣ����� Health ������ڵ� GameObject �� Tag �� "Player"
        if (playerHealth != null && playerHealth.CompareTag("Player")) // <-- ��� playerHealth �� Tag
        {
            Debug.Log($"Player Health component found on {playerHealth.gameObject.name}! Dealing {touchDamage} damage."); // 4. �ɹ���־
            playerHealth.TakeDamage(touchDamage); // 5. ����˺�
            Debug.Log($"Destroying enemy {gameObject.name} after dealing damage."); // 6. ������־
            Destroy(gameObject); // 7. �����Լ�
        }
        // ���Ա���֮ǰ�� else if / else ��־���ڵ���
        else if (playerHealth != null)
        {
            Debug.LogWarning($"Found Health on {playerHealth.gameObject.name} but tag is '{playerHealth.tag}', not 'Player'.");
        }
        else
        {
            Debug.Log($"No Health component found in parents of {other.name}.");
        }
    }
    // ^^^--- OnTriggerEnter ���� ---^^^

} // <--- EnemyAI �����