using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("��������")]
    [Tooltip("Ҫ���ɵĵ���Ԥ��")]
    public GameObject enemyPrefab;
    [Tooltip("ÿ�����ɶ��ٸ�����")]
    public float spawnRate = 0.5f; // ÿ 2 ������һ��
    [Tooltip("����������ɵ���С�뾶")]
    public float spawnRadiusMin = 10f;
    [Tooltip("����������ɵ����뾶")]
    public float spawnRadiusMax = 15f;

    private float spawnCooldown = 0f;
    private Transform playerTransform = null; // ��ʼ��Ϊ null


    void Start()
    {
        
    }

    void OnEnable()
    {
        // ���������ﳢ�Ի�ȡһ����ң������� Update �л�ȡ
        // ����������ȡ����Ҫȷ�� GameManager.Instance �Ѿ�����
        if (GameManager.Instance != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
        // ������ȴ��ʱ�����ܸ��ʺϷ�������
        if (spawnRate > 0) spawnCooldown = 1f / spawnRate;
        else spawnCooldown = float.MaxValue;

        Debug.Log("EnemySpawner Enabled.");
    }
    void Update()
    {
        // �����û��������ã����ҵ�ǰ��ս��״̬�����Դ� GameManager ��ȡ
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null)
                {
                    // GameManager ��������ս��״̬���� playerTransform ���� null?
                    // ���� MechRoot ��û��ȫ׼���ã����� GameManager �߼�����
                    // ��ʱ�ȴ���һ֡
                    return;
                }
                Debug.Log("EnemySpawner found player via GameManager in Update.");
            }
            else
            {
                // GameManager �����û���ս��״̬����ִ��
                return;
            }
        }
        // --- �����߼����� ---
        if (enemyPrefab == null) return;
        spawnCooldown -= Time.deltaTime;
        if (spawnCooldown <= 0f)
        {
            SpawnEnemy();
            if (spawnRate > 0) spawnCooldown = 1f / spawnRate;
            else spawnCooldown = float.MaxValue;
        }
    }

    void SpawnEnemy()
    {
        // ��һ������İ�ȫ���
        if (playerTransform == null) return;
        // �����������λ��
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad; // ����Ƕ� (����)
        float randomDistance = Random.Range(spawnRadiusMin, spawnRadiusMax); // �������

        // ���㷽������ (�� XZ ƽ��)
        Vector3 spawnDirection = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle));
        // �����������ɵ� (���λ�� + ���� * ����)
        Vector3 spawnPosition = playerTransform.position + spawnDirection * randomDistance;

        // ȷ�� Y ��߶������һ�� (������Ϊ 0��ȡ������ĵ���)
        spawnPosition.y = playerTransform.position.y; // ���� spawnPosition.y = 0;

        // ʵ��������
        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity); // ʹ��Ĭ����ת
        // Debug.Log("Spawned Enemy at: " + spawnPosition);
    }
    // OnDisable ʱ��������Ǹ���ϰ��
    void OnDisable()
    {
        playerTransform = null;
        Debug.Log("EnemySpawner Disabled, player reference cleared.");
    }

    // (��ѡ) �ṩֹͣ�Ϳ�ʼ���ɵķ������� GameManager ����
    public void StartSpawning() { enabled = true; /* ������Ҫ���� cooldown? */ }
    public void StopSpawning() { enabled = false; }
}