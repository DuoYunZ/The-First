using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // ȷ���� Rigidbody
public class EnemyAI : MonoBehaviour
{
    [Header("AI ����")]
    [Tooltip("�����ƶ��ٶ�")]
    public float moveSpeed = 3f;
    private Transform playerTransform = null;
    private Rigidbody rb;
    private bool canMove = false; // �Ƿ���Կ�ʼ�ƶ� (��ֹ Start ʱ��һ�û�ҵ�)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        // ��������������
    }

    void FixedUpdate() // �� FixedUpdate ���ƶ� Rigidbody
    {
        // �����û��������ã����Դ� GameManager ��ȡ
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat) // ֻ��ս��ģʽ����
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null)
                {
                    // �ȴ� GameManager ׼����
                    return; // �˳� FixedUpdate
                }
            }
            else
            {
                // GameManager �����û���ս��״̬
                return; // �˳� FixedUpdate
            }
        }

            // --- ���㳯����ҵķ��� ---
            Vector3 directionToPlayer = (playerTransform.position - rb.position); // ʹ�� rb.position ��׼ȷ
        directionToPlayer.y = 0; // ���� Y ����죬ֻ��ˮƽ���ƶ�
        directionToPlayer.Normalize(); // ��ȡ��λ��������

        // --- �ƶ����� ---
        // ʹ�� MovePosition ͨ����ֱ������ velocity ���ȶ���������������ײʱ
        Vector3 nextPosition = rb.position + directionToPlayer * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);

        // (��ѡ) �õ����Ӿ��ϳ������ (�����Ҫ�Ļ�)
        // transform.LookAt(playerTransform.position); // ����ܻᵼ������תԼ�������Ķ����������������ת�����о�û��

    }
}