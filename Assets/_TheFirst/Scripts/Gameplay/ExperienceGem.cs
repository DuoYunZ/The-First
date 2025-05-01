using UnityEngine;
using UnityEngine.Events;
public class ExperienceGem : MonoBehaviour
{
    [Header("����ֵ����")]
    [Tooltip("�˱�ʯ�ṩ�ľ���ֵ")]
    public int experienceAmount = 1;

    [Header("ʰȡ����")]
    [Tooltip("��ҿ��������پ����ڿ�ʼ������")]
    public float magnetRadius = 4f;
    [Tooltip("������ҵ��ٶ�")]
    public float collectionSpeed = 8f;

    private Transform playerTransform; // ��� (MechRoot) �� Transform
    private bool isCollecting = false; // �Ƿ����ڱ��������
    private PlayerLevelManager foundLevelManager; // �����ҵ��� Level Manager


    void Start()
    {
       
    }

    void Update()
    {
        // �����û�ҵ���ң���������� Build ģʽ�±�����ˣ��ͳ������»�ȡ
        if (playerTransform == null || foundLevelManager == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
                // ���Դ��ҵ��� playerTransform (MechRoot) ��ȡ LevelManager
                foundLevelManager = playerTransform.GetComponent<PlayerLevelManager>();
                if (foundLevelManager == null)
                {
                    Debug.LogError("����� Transform ��δ�ҵ� PlayerLevelManager ���!", playerTransform);
                    // ������ȷʵû������ű�����ʯ���޷���ʰȡ
                }
            }
            if (playerTransform == null || foundLevelManager == null) return; // �����δ�ҵ�����ִ�к����߼�
        }

        // --- ������������ ---
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position); // ����������������

        // ��������ռ�״̬������ҽ����������뾶����ʼ�ռ�
        if (!isCollecting && distanceToPlayer <= magnetRadius)
        {
            isCollecting = true;
            // (��ѡ) ��������������������������ʯ�� Rigidbody �Ļ�
            // Rigidbody rb = GetComponent<Rigidbody>();
            // if (rb != null) { rb.isKinematic = true; rb.velocity = Vector3.zero; }
        }

        // ��������ռ�״̬����������
        if (isCollecting)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, collectionSpeed * Time.deltaTime);
            // ���ǳ��ӽ�ʱֱ���ռ�����ֹ��͸�򶶶�
            if (distanceToPlayer < 0.5f)
            { // �������������ֵ
                Collect(); // ֱ�ӵ����ռ����������� OnTriggerEnter
            }
        }
    }

        // --- ��������� ---
        void OnTriggerEnter(Collider other)
        {
            // �����봥�������Ƿ�����ҵ� LevelManager (�����Ӷ���)
            // ע�⣺����� other ������ ChassisCore �������
            if (foundLevelManager != null && other.GetComponentInParent<PlayerLevelManager>() == foundLevelManager)
            {
                Collect(); // ����ͳһ��ʰȡ�߼�
            }
        }

        // ʰȡ�߼�
        void Collect()
        {
            if (foundLevelManager != null)
            {
                foundLevelManager.AddExperience(experienceAmount);
                Debug.Log($"���鱦ʯ�ѱ�ʰȡ�������� {experienceAmount} XP"); // ʹ��������־
            }
            else
            {
                // �������� Update ���Ѿ�ȷ�� foundLevelManager ���� null ��
                Debug.LogWarning("�����ռ����飬��δ���ҵ� PlayerLevelManager ���ã�");
            }

            // ��������
            Destroy(gameObject);
        }
    }