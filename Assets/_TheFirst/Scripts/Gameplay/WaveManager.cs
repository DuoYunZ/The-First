// --- WaveManager.cs (最终完整修正版) ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("所有波次配置")]
    public List<WaveConfig> allWaveConfigurations;

    [Header("通用波次控制")]
    public int currentWaveIndex = -1;
    public float defaultTimeBetweenWaves = 10f;
    private float currentWaveActiveTimer;

    [Header("敌人属性成长因子 (全局)")]
    public float healthGrowthFactor = 0.1f;
    public float damageGrowthFactor = 0.05f;
    public float speedGrowthFactor = 0.02f;

    [Header("引用")]
    public EnemySpawner enemySpawner;

    private Health bossInstanceHealth = null;
    private int enemiesRemainingInWave = 0;
    private bool waveIsCurrentlySpawning = false;
    private float nextWaveCountdownTimer;

    public enum WaveState { StartingGame, SpawningEnemies, WaitingForAllEnemiesCleared, WaveCooldown }
    private WaveState _currentState = WaveState.StartingGame;

    public WaveState currentState
    {
        get { return _currentState; }
        private set
        {
            if (_currentState != value)
            {
                Debug.Log($"<color=yellow>WaveManager STATE CHANGE: From '{_currentState}' TO '{value}'</color>");
                _currentState = value;
            }
        }
    }

    [Header("无尽模式配置")]
    public bool enableEndlessMode = true;
    public float endlessModeTimeBetweenWaves = 15f;
    public int endlessBaseEnemyCount = 10;
    public int endlessEnemyCountIncreasePerWave = 2;
    public List<EnemyType> endlessModeEnemyPool;

    private bool isInEndlessMode = false;
    private int endlessWaveCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (enemySpawner == null) { Debug.LogError("EnemySpawner 未在 WaveManager 中设置!", this); enabled = false; return; }
        if (allWaveConfigurations == null || allWaveConfigurations.Count == 0) { Debug.LogError("allWaveConfigurations 列表为空!", this); enabled = false; return; }

        nextWaveCountdownTimer = 3f;
        currentState = WaveState.StartingGame;
        Debug.Log("WaveManager 初始化，等待游戏开始波次序列。");
    }

    // vvv --- 【核心修正区域】 --- vvv
    // 我们将 Update 和 CheckForWaveCompletion 方法更新为最终的正确逻辑
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

            // 在“正在生成”和“等待清空”两个状态下，我们都调用同一个检查方法
            // 这样能确保计时器从波次一开始就计时
            case WaveState.SpawningEnemies:
            case WaveState.WaitingForAllEnemiesCleared:
                CheckForWaveCompletion();
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

    private void CheckForWaveCompletion()
    {
        if (currentState == WaveState.SpawningEnemies && !waveIsCurrentlySpawning)
        {
            currentState = WaveState.WaitingForAllEnemiesCleared;
        }

        bool waveCleared = false;

        // vvv --- 【核心修正区域】 --- vvv
        if (isInEndlessMode)
        {
            // 如果是无尽模式，只检查敌人是否清空
            if (enemiesRemainingInWave <= 0 && !waveIsCurrentlySpawning)
            {
                waveCleared = true;
            }
        }
        else
        {
            // 如果是预设波次，执行我们之前的双重检查（时间和敌人数量）
            // 增加一个安全检查，确保 currentWaveIndex 仍在有效范围内
            if (currentWaveIndex < 0 || currentWaveIndex >= allWaveConfigurations.Count)
            {
                // 如果索引无效，为防止卡死，直接结束波次 (这是一种保护机制)
                waveCleared = true;
                Debug.LogError($"检测到无效的 currentWaveIndex ({currentWaveIndex})，强制结束波次！");
                return;
            }

            if (currentWaveActiveTimer > 0)
            {
                currentWaveActiveTimer -= Time.deltaTime;
            }

            WaveConfig currentConfig = allWaveConfigurations[currentWaveIndex];

            if (currentConfig.maxWaveDuration > 0 && currentWaveActiveTimer <= 0)
            {
                Debug.Log($"<color=orange>波次 {currentWaveIndex + 1} 时间耗尽！强制进入下一波。</color>");
                waveCleared = true;
            }

            if (!waveCleared)
            {
                if (currentConfig.waveType != WaveType.Boss)
                {
                    if (enemiesRemainingInWave <= 0 && !waveIsCurrentlySpawning) waveCleared = true;
                }
                else
                {
                    if (bossInstanceHealth == null || bossInstanceHealth.IsDead) waveCleared = true;
                }
            }
        }
        // ^^^ --- 修正结束 --- ^^^

        if (waveCleared)
        {
            EndCurrentWave();
        }
    }


    void StartNextWave()
    {
        if (isInEndlessMode)
        {
            StartEndlessWave();
            return;
        }

        currentWaveIndex++;

        if (currentWaveIndex >= allWaveConfigurations.Count)
        {
            if (enableEndlessMode)
            {
                Debug.Log("<color=magenta>所有配置波次已完成！正在启动无尽模式...</color>");
                isInEndlessMode = true;
                StartEndlessWave();
            }
            else
            {
                Debug.Log("所有配置的波次已完成! 游戏胜利！");
                enabled = false;
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
            // 使用一个很大的值来代表无限时间
            currentWaveActiveTimer = 9999f;
        }
        UIManager.Instance.UpdateEnemiesRemaining(enemiesRemainingInWave);

        enemySpawner.InstructToSpawnWaveConfig(
            currentConfig,
            currentWaveIndex + 1,
            healthGrowthFactor,
            damageGrowthFactor,
            speedGrowthFactor
        );

        if (currentConfig.waveType == WaveType.Boss)
        {
            Debug.Log("这是一个Boss波次！准备战斗！");
        }
        else if (currentConfig.waveType == WaveType.Reward)
        {
            Debug.Log("奖励波次！快打破宝箱！");
        }
    }

    public void NotifySpawnerFinishedCurrentWave()
    {
        Debug.Log("接收到 EnemySpawner 的生成完毕通知。");
        waveIsCurrentlySpawning = false;
    }

    public void AnEnemyFailedToSpawn()
    {
        if (currentState == WaveState.WaitingForAllEnemiesCleared || currentState == WaveState.SpawningEnemies)
        {
            enemiesRemainingInWave--;
            if (enemiesRemainingInWave < 0) enemiesRemainingInWave = 0;
            Debug.LogWarning($"[WaveManager] 一个敌人未能生成。剩余敌人计数已调整为: {enemiesRemainingInWave}");
            UIManager.Instance?.UpdateEnemiesRemaining(enemiesRemainingInWave);
        }
    }

    void EndCurrentWave()
    {
        // 停止所有敌人的生成，以防有延迟生成的敌人组在波次结束后还生成
        enemySpawner.StopAndClearSpawning();

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
        if (currentState == WaveState.SpawningEnemies || currentState == WaveState.WaitingForAllEnemiesCleared)
        {
            enemiesRemainingInWave--;
            if (enemiesRemainingInWave < 0)
            {
                enemiesRemainingInWave = 0;
            }
            Debug.Log($"[WaveManager] 一个敌人被击败。波次 {currentWaveIndex + 1} 剩余敌人: {enemiesRemainingInWave}");
            UIManager.Instance?.UpdateEnemiesRemaining(enemiesRemainingInWave);
        }
        else
        {
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
        }
    }

    void StartEndlessWave()
    {
        endlessWaveCount++;
        Debug.Log($"<color=yellow>开始无尽波次: {endlessWaveCount}</color>");

        WaveConfig endlessConfig = ScriptableObject.CreateInstance<WaveConfig>();
        endlessConfig.waveName = $"无尽波次 {endlessWaveCount}";
        endlessConfig.waveType = WaveType.Normal;

        List<EnemySpawnGroup> groupsForThisWave = new List<EnemySpawnGroup>();
        int totalEnemiesForThisWave = endlessBaseEnemyCount + (endlessWaveCount * endlessEnemyCountIncreasePerWave);

        for (int i = 0; i < totalEnemiesForThisWave; i++)
        {
            EnemyType randomEnemyType = endlessModeEnemyPool[Random.Range(0, endlessModeEnemyPool.Count)];
            EnemySpawnGroup newGroup = new EnemySpawnGroup
            {
                enemyType = randomEnemyType,
                count = 1,
                spawnIntervalWithinGroup = 0.1f,
                delayAfterPreviousGroupStarts = Random.Range(0.1f, 0.5f)
            };
            groupsForThisWave.Add(newGroup);
        }
        endlessConfig.enemyGroups = groupsForThisWave;

        currentState = WaveState.SpawningEnemies;
        waveIsCurrentlySpawning = true;
        enemiesRemainingInWave = totalEnemiesForThisWave;
        UIManager.Instance?.UpdateWaveNumber(allWaveConfigurations.Count + endlessWaveCount, endlessConfig.waveName, endlessConfig.waveType);
        UIManager.Instance.UpdateEnemiesRemaining(enemiesRemainingInWave);

        int effectiveWaveNumber = allWaveConfigurations.Count + endlessWaveCount;
        enemySpawner.InstructToSpawnWaveConfig(
            endlessConfig,
            effectiveWaveNumber,
            healthGrowthFactor,
            damageGrowthFactor,
            speedGrowthFactor
        );
    }

    public void RegisterDebugEnemy()
    {
        enemiesRemainingInWave++;
        UIManager.Instance?.UpdateEnemiesRemaining(enemiesRemainingInWave);
        Debug.Log($"[调试] 一个新的敌人已被注册到WaveManager，当前剩余敌人: {enemiesRemainingInWave}");
    }
}