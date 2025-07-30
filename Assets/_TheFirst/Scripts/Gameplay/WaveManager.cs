// 修改你现有的 WaveManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic; // 需要这个


public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("所有波次配置")]
    [Tooltip("按顺序拖入所有 WaveConfig ScriptableObject 资源")]
    public List<WaveConfig> allWaveConfigurations; // <--- 新增：取代之前的公式计算

    [Header("通用波次控制")]
    public int currentWaveIndex = -1; // 从-1开始，这样第一波是索引0
    [Tooltip("每波之间的默认准备时间 (秒)，会被 WaveConfig 中的 customTimeUntilNextWave 覆盖")]
    public float defaultTimeBetweenWaves = 10f;

    private float currentWaveActiveTimer; // 当前波次已激活的时间或剩余时间
   
    [Header("敌人属性成长因子 (全局)")]
    public float healthGrowthFactor = 0.1f;
    public float damageGrowthFactor = 0.05f;
    public float speedGrowthFactor = 0.02f;

    [Header("引用")]
    public EnemySpawner enemySpawner;

    private Health bossInstanceHealth = null; // 用于存储当前场上Boss的Health组件引用
    private int enemiesRemainingInWave = 0;
    private bool waveIsCurrentlySpawning = false;
    private float nextWaveCountdownTimer;

    public enum WaveState { StartingGame, SpawningEnemies, WaitingForAllEnemiesCleared, WaveCooldown }
    public WaveState currentState = WaveState.StartingGame;

    // ================== 【修改点 1】无尽模式配置 ==================
    [Header("无尽模式配置")]
    [Tooltip("当所有预设波次完成后，是否开启无尽模式")]
    public bool enableEndlessMode = true;

    [Tooltip("无尽模式下，每波之间的准备时间")]
    public float endlessModeTimeBetweenWaves = 15f;

    [Tooltip("无尽模式的基础敌人数量")]
    public int endlessBaseEnemyCount = 10;

    [Tooltip("无尽模式下，每波增加的敌人数量")]
    public int endlessEnemyCountIncreasePerWave = 2;

    [Tooltip("用于在无尽模式中随机生成敌人的【EnemyType】资产列表")]
    public List<EnemyType> endlessModeEnemyPool; // <-- 类型已修正为 List<EnemyType>

    // --- 私有变量 ---
    private bool isInEndlessMode = false;
    private int endlessWaveCount = 0; // 用于计算无尽模式的难度

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (Instance == this) // 确保单例设置正确后
        {
            Debug.Log($"UIManager.Instance in WaveManager.Start(): {UIManager.Instance}"); // <--- 添加这行
        }
        if (enemySpawner == null) { Debug.LogError("EnemySpawner 未在 WaveManager 中设置!", this); enabled = false; return; }
        if (allWaveConfigurations == null || allWaveConfigurations.Count == 0) { Debug.LogError("allWaveConfigurations 列表为空!", this); enabled = false; return; }

        nextWaveCountdownTimer = 3f; // 游戏开始后短暂延迟
        Debug.Log("WaveManager 初始化，等待游戏开始波次序列。");
    }

    void Update()
    {
        switch (currentState)
        {
            case WaveState.StartingGame:
                if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat)
                {
                    nextWaveCountdownTimer -= Time.deltaTime;
                    UIManager.Instance?.UpdateNextWaveTimer(nextWaveCountdownTimer);
                    if (nextWaveCountdownTimer <= 0)
                    {
                        StartNextWave();
                    }
                }
                break;

            case WaveState.SpawningEnemies:
                if (!waveIsCurrentlySpawning)
                {
                    currentState = WaveState.WaitingForAllEnemiesCleared;
                }
                break;

            case WaveState.WaitingForAllEnemiesCleared:
                bool waveCleared = false;
                // 无尽模式和普通模式共用清空逻辑
                if (isInEndlessMode || allWaveConfigurations[currentWaveIndex].waveType != WaveType.Boss)
                {
                    if (enemiesRemainingInWave <= 0) waveCleared = true;
                }
                // Boss 波次特殊逻辑
                else if (allWaveConfigurations[currentWaveIndex].waveType == WaveType.Boss)
                {
                    if (bossInstanceHealth == null || bossInstanceHealth.IsDead) waveCleared = true;
                }

                if (waveCleared)
                {
                    EndCurrentWave();
                }
                break;

            case WaveState.WaveCooldown:
                nextWaveCountdownTimer -= Time.deltaTime;
                UIManager.Instance?.UpdateNextWaveTimer(nextWaveCountdownTimer);
                if (nextWaveCountdownTimer <= 0)
                {
                    StartNextWave();
                }
                break;
        }
    }

    void StartNextWave()
    {
        // 如果已进入无尽模式，直接调用无尽波次生成逻辑
        if (isInEndlessMode)
        {
            StartEndlessWave();
            return;
        }


        currentWaveIndex++; // 移动到下一波的索引

        // 检查是否完成所有预设波次
        if (currentWaveIndex >= allWaveConfigurations.Count)
        {
            if (enableEndlessMode)
            {
                Debug.Log("<color=magenta>所有配置波次已完成！正在启动无尽模式...</color>");
                isInEndlessMode = true;
                StartEndlessWave(); // 第一次启动无尽模式
            }
            else
            {
                Debug.Log("所有配置的波次已完成! 游戏胜利！");
                // 在这里可以触发游戏胜利的UI或逻辑
                enabled = false; // 禁用 WaveManager
            }
            return;
        }

        WaveConfig currentConfig = allWaveConfigurations[currentWaveIndex];
        Debug.Log($"开始波次: {currentWaveIndex + 1} ({currentConfig.waveName}) - 类型: {currentConfig.waveType}");
        bossInstanceHealth = null;

        currentState = WaveState.SpawningEnemies;
        waveIsCurrentlySpawning = true;
        enemiesRemainingInWave = currentConfig.GetTotalEnemiesInWave();
        UIManager.Instance?.UpdateWaveNumber(currentWaveIndex + 1, currentConfig.waveName, currentConfig.waveType);

        if (currentConfig.maxWaveDuration > 0)
        {
            currentWaveActiveTimer = currentConfig.maxWaveDuration;
            Debug.Log($"Wave {currentWaveIndex + 1} has a max duration of {currentWaveActiveTimer} seconds.");
        }
        else
        {
            currentWaveActiveTimer = float.MaxValue; // 表示没有时间限制 (或设为-1等特殊值)
        }
        UIManager.Instance.UpdateEnemiesRemaining(enemiesRemainingInWave);

        // 将整个 WaveConfig 或其核心部分传递给 EnemySpawner
        enemySpawner.InstructToSpawnWaveConfig(
            currentConfig,
            currentWaveIndex + 1, // 传递实际波数 (1-based) 用于成长计算
            healthGrowthFactor,
            damageGrowthFactor,
            speedGrowthFactor
        );
        if (currentConfig.waveType == WaveType.Boss)
        {
            Debug.Log("这是一个Boss波次！准备战斗！");
            // 在这里播放Boss战音乐、显示Boss血条UI等
            // UIManager.Instance.ShowBossHealthBar();
        }
        else if (currentConfig.waveType == WaveType.Reward)
        {
            Debug.Log("奖励波次！快打破宝箱！");
            // 在这里可以播放轻松的背景音乐、显示特殊的UI提示等
            // UIManager.Instance.ShowMessage("奖励波次！", 3f);
        }
    }

    public void NotifySpawnerFinishedCurrentWave()
    {
        waveIsCurrentlySpawning = false;
    }
    public void AnEnemyFailedToSpawn()
    {
        if (currentState == WaveState.WaitingForAllEnemiesCleared ||
            currentState == WaveState.SpawningEnemies) // 即使正在生成时，如果一个失败了也应该计数
        {
            enemiesRemainingInWave--;
            if (enemiesRemainingInWave < 0) enemiesRemainingInWave = 0;

            Debug.LogWarning($"[WaveManager] 一个敌人未能生成。剩余敌人计数已调整为: {enemiesRemainingInWave}");
            UIManager.Instance?.UpdateEnemiesRemaining(enemiesRemainingInWave); // 更新UI
        }
    }

    void EndCurrentWave()
    {
        if (isInEndlessMode)
        {
            Debug.Log($"无尽波次 {endlessWaveCount} 已清除!");
            currentState = WaveState.WaveCooldown;
            nextWaveCountdownTimer = endlessModeTimeBetweenWaves;
        }
        else
        {
            WaveConfig currentConfig = allWaveConfigurations[currentWaveIndex];
            Debug.Log($"波次 {currentWaveIndex + 1} ({currentConfig.waveName}) 已清除!");
            currentState = WaveState.WaveCooldown;
            nextWaveCountdownTimer = currentConfig.customTimeUntilNextWave > 0 ? currentConfig.customTimeUntilNextWave : defaultTimeBetweenWaves;
        }
    }

    public void EnemyDefeated()
    {
        // 如果一个波次是激活状态（正在生成 或 等待被清空），那么就应该计数这次击杀。
        if (currentState == WaveState.SpawningEnemies || currentState == WaveState.WaitingForAllEnemiesCleared)
        {
            enemiesRemainingInWave--;
            if (enemiesRemainingInWave < 0)
            {
                enemiesRemainingInWave = 0; // 确保不会是负数
            }

            Debug.Log($"[WaveManager] 一个敌人被击败。波次 {currentWaveIndex + 1} 剩余敌人: {enemiesRemainingInWave}");
            UIManager.Instance?.UpdateEnemiesRemaining(enemiesRemainingInWave);
        }
        else
        {
            // 这个日志很有用，可以帮助我们发现是否在不应该的时候（如冷却期间）有敌人死亡
            Debug.LogWarning($"[WaveManager] EnemyDefeated 在一个意外的状态({currentState})下被调用，此次击杀未被计入波次总数。");
        }
    }

    public void ResetWaveSystem()
    {
        isInEndlessMode = false;
        endlessWaveCount = 0;
        currentWaveIndex = -1;
        enemiesRemainingInWave = 0;
        waveIsCurrentlySpawning = false;
        nextWaveCountdownTimer = 3f;
        currentState = WaveState.StartingGame;
        StopAllCoroutines();
        if (enemySpawner != null) enemySpawner.StopAndClearSpawning();
        Debug.Log("WaveManager 已重置。");
    }
    public void RegisterBossInstance(Health bossHealth)
    {
        bossInstanceHealth = bossHealth;
        if (bossHealth != null)
        {
            Debug.Log($"WaveManager 已注册Boss: {bossHealth.gameObject.name}");
            // 在这里将Boss的Health组件关联到UI的Boss血条上
            // UIManager.Instance.LinkBossHealthBar(bossHealth);
        }
    }
    void StartEndlessWave()
    {
        endlessWaveCount++;
        Debug.Log($"<color=yellow>开始无尽波次: {endlessWaveCount}</color>");

        // 1. 动态创建一个临时的WaveConfig
        WaveConfig endlessConfig = ScriptableObject.CreateInstance<WaveConfig>();
        endlessConfig.waveName = $"无尽波次 {endlessWaveCount}";
        endlessConfig.waveType = WaveType.Normal;

        // 2. 动态创建 EnemySpawnGroup 列表
        List<EnemySpawnGroup> groupsForThisWave = new List<EnemySpawnGroup>();
        int totalEnemiesForThisWave = endlessBaseEnemyCount + (endlessWaveCount * endlessEnemyCountIncreasePerWave);

        // 3. 填充列表：为每个要生成的敌人创建一个独立的 EnemySpawnGroup
        //    这是一种简单的实现，可以让敌人种类非常随机。
        for (int i = 0; i < totalEnemiesForThisWave; i++)
        {
            EnemyType randomEnemyType = endlessModeEnemyPool[Random.Range(0, endlessModeEnemyPool.Count)];

            EnemySpawnGroup newGroup = new EnemySpawnGroup
            {
                enemyType = randomEnemyType,
                count = 1, // 每次只生成1个，但我们循环创建了 N 个组
                spawnIntervalWithinGroup = 0.1f, // 可以设置一个小的默认值，让生成更平滑
                delayAfterPreviousGroupStarts = Random.Range(0.1f, 0.5f) // 随机的小延迟，避免所有敌人在同一点生成
            };
            groupsForThisWave.Add(newGroup);
        }
        endlessConfig.enemyGroups = groupsForThisWave;

        // 4. 设置状态和UI
        currentState = WaveState.SpawningEnemies;
        waveIsCurrentlySpawning = true;
        enemiesRemainingInWave = totalEnemiesForThisWave;
        UIManager.Instance?.UpdateWaveNumber(allWaveConfigurations.Count + endlessWaveCount, endlessConfig.waveName, endlessConfig.waveType);
        UIManager.Instance.UpdateEnemiesRemaining(enemiesRemainingInWave);

        // 5. 让 Spawner 开始生成
        int effectiveWaveNumber = allWaveConfigurations.Count + endlessWaveCount;
        enemySpawner.InstructToSpawnWaveConfig(
            endlessConfig,
            effectiveWaveNumber,
            healthGrowthFactor,
            damageGrowthFactor,
            speedGrowthFactor
        );
    }
}