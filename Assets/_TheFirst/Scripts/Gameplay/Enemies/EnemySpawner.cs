using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    [Tooltip("要生成的敌人预设")]
    public GameObject enemyPrefab;
    [Tooltip("每秒生成多少个敌人")]
    public float spawnRate = 0.5f; // 每 2 秒生成一个
    [Tooltip("距离玩家生成的最小半径")]
    public float spawnRadiusMin = 10f;
    [Tooltip("距离玩家生成的最大半径")]
    public float spawnRadiusMax = 15f;

    private float spawnCooldown = 0f;
    private Transform playerTransform = null; // 初始化为 null


    void Start()
    {
        
    }

    void OnEnable()
    {
        // 可以在这里尝试获取一次玩家，或者在 Update 中获取
        // 如果在这里获取，需要确保 GameManager.Instance 已经可用
        if (GameManager.Instance != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
        // 重置冷却计时器可能更适合放在这里
        if (spawnRate > 0) spawnCooldown = 1f / spawnRate;
        else spawnCooldown = float.MaxValue;

        Debug.Log("EnemySpawner Enabled.");
    }
    void Update()
    {
        // 如果还没有玩家引用，并且当前是战斗状态，尝试从 GameManager 获取
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
            {
                playerTransform = GameManager.Instance.playerTransform;
                if (playerTransform == null)
                {
                    // GameManager 存在且是战斗状态，但 playerTransform 还是 null?
                    // 可能 MechRoot 还没完全准备好，或者 GameManager 逻辑有误
                    // 暂时等待下一帧
                    return;
                }
                Debug.Log("EnemySpawner found player via GameManager in Update.");
            }
            else
            {
                // GameManager 不可用或不是战斗状态，不执行
                return;
            }
        }
        // --- 后续逻辑不变 ---
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
        // 加一个额外的安全检查
        if (playerTransform == null) return;
        // 计算随机生成位置
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad; // 随机角度 (弧度)
        float randomDistance = Random.Range(spawnRadiusMin, spawnRadiusMax); // 随机距离

        // 计算方向向量 (在 XZ 平面)
        Vector3 spawnDirection = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle));
        // 计算最终生成点 (玩家位置 + 方向 * 距离)
        Vector3 spawnPosition = playerTransform.position + spawnDirection * randomDistance;

        // 确保 Y 轴高度与玩家一致 (或者设为 0，取决于你的地面)
        spawnPosition.y = playerTransform.position.y; // 或者 spawnPosition.y = 0;

        // 实例化敌人
        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity); // 使用默认旋转
        // Debug.Log("Spawned Enemy at: " + spawnPosition);
    }
    // OnDisable 时清除引用是个好习惯
    void OnDisable()
    {
        playerTransform = null;
        Debug.Log("EnemySpawner Disabled, player reference cleared.");
    }

    // (可选) 提供停止和开始生成的方法，供 GameManager 调用
    public void StartSpawning() { enabled = true; /* 可能需要重置 cooldown? */ }
    public void StopSpawning() { enabled = false; }
}