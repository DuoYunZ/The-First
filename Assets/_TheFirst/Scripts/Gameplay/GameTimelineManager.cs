using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 用于排序
using UnityEngine.SceneManagement; // 用于结束游戏

public class GameTimelineManager : MonoBehaviour
{
    public static GameTimelineManager Instance { get; private set; }

    [Header("时间轴配置")]
    [Tooltip("拖入定义了20分钟关卡事件的 ScriptableObject")]
    public GameTimelineConfig timelineConfig;

    [Header("Demo Timeline Overrides")]
    public bool autoSelectDemoTimeline = true;
    public GameTimelineConfig demoIntroTimelineConfig;
    public GameTimelineConfig demoHardTimelineConfig;
    public bool useBuildTest60Timeline = false;
    public GameTimelineConfig demoBuildTest60TimelineConfig;
    public bool useDebugTimelineOverride = false;
    public GameTimelineConfig debugTimelineOverrideConfig;
    public float demoIntroExperienceMultiplier = 1f;
    public float demoHardExperienceMultiplier = 2f;
    public float demoBuildTest60ExperienceMultiplier = 1.35f;

    [Header("Demo Surprise Events")]
    public bool enableDemoSurpriseEvents = true;
    public bool demoSurpriseHardOnly = true;
    public int demoSurpriseMaxEvents = 4;
    public float demoSurpriseFirstTime = 260f;
    public float demoSurpriseLastTime = 1080f;
    public float demoSurpriseMinInterval = 120f;
    public float demoSurpriseMaxInterval = 180f;
    public List<WaveConfig> demoSurpriseWaves = new List<WaveConfig>();

    [Header("游戏状态")]
    public int totalKills = 0; // 可以用来统计击杀数

    [Header("引用")]
    [Tooltip("必须引用场景中的 EnemySpawner")]
    public EnemySpawner enemySpawner;
    [Tooltip("必须引用场景中的 GameTimer")]
    public GameTimer gameTimer;

    [Header("敌人属性成长因子 (全局)")]
    [Tooltip("每分钟敌人增加的生命值百分比")]
    public float healthGrowthFactorPerMinute = 0.1f;
    [Tooltip("每分钟敌人增加的伤害百分比")]
    public float damageGrowthFactorPerMinute = 0.05f;
    [Tooltip("每分钟敌人增加的速度百分比")]
    public float speedGrowthFactorPerMinute = 0.02f;

    [Header("Demo Late Pressure")]
    public bool enableLateHealthRamp = true;
    public float lateHealthRampStartTime = 240f;
    public float lateHealthMultiplierPerMinute = 0.3f;
    public float lateHealthMultiplierMax = 2.4f;

    [Header("Post 3 Minute Enemy Count Ramp")]
    public bool enablePostThreeMinuteCountRamp = true;
    public float countRampStartTime = 180f;
    public float countRampBaseMultiplier = 1.35f;
    public float countRampMultiplierPerMinute = 0.12f;
    public float countRampMaxMultiplier = 2f;

    [Header("Continuous Pressure Spawning")]
    public bool enableContinuousPressure = true;
    public float pressureStartTime = 12f;
    public float pressureCheckInterval = 1.25f;
    public int pressureBaseTargetAlive = 10;
    public int pressureTargetAliveIncreasePerMinute = 3;
    public int pressureTargetAliveMax = 45;
    [Range(0.1f, 1f)] public float pressureRefillThreshold = 0.55f;
    [Range(1f, 2f)] public float pressureHardCapMultiplier = 1.25f;
    public int pressureMaxBatchSize = 4;
    public float pressureSpawnIntervalWithinBatch = 0.18f;
    public float pressureEnemyUnlockLookAhead = 20f;
    public bool pressureDebugLogs = false;

    [Header("波次提前完成设置")]
    [Tooltip("勾选此项，当前波敌人全部被消灭后立即进入下一波")]
    public bool advanceWaveOnClear = true;
    [Tooltip("提前进入下一波前的短暂延迟（秒），给玩家喘息时间")]
    public float advanceDelay = 0.25f;

    [Header("调试显示")]
    [Tooltip("勾选此项在左上角显示当前波次调试信息")]
    public bool showDebugUI = false;
    private const bool AllowOnGuiDebug = false;

    // --- 内部运行时列表 ---
    private class PendingEvent
    {
        public float triggerTime;
        public WaveConfig waveConfig;
    }

    private class PressureSpawnSource
    {
        public float triggerTime;
        public EnemySpawnGroup group;
    }

    private List<PendingEvent> pendingEvents = new List<PendingEvent>();
    private readonly List<PressureSpawnSource> pressureSpawnSources = new List<PressureSpawnSource>();
    private bool gameFinished = false;
    private int removedWithoutKillCount = 0;
    private float nextDemoSurpriseTime = -1f;
    private int demoSurpriseEventsTriggered = 0;
    private WaveConfig lastDemoSurpriseWave;
    private float pressureCheckTimer = 0f;
    private int pressureAliveEnemyCount = 0;
    private int pressureSpawnedTotal = 0;
    private float lastMajorWaveTriggerTime = -999f;

    // --- 波次追踪 ---
    private int currentWaveIndex = 0;    // 当前已触发的波次编号（从1开始）
    private int totalWaveCount = 0;      // 总波次数量
    private string currentWaveName = ""; // 当前波次名称
    private int aliveEnemyCount = 0;     // 当前存活的敌人数量
    private int waveSpawnedTotal = 0;    // 当前波生成的总敌人数
    private bool isAdvancing = false;    // 是否正在提前推进
    private bool allWavesFired = false;  // 所有波次是否已全部触发

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        SelectDemoTimelineIfNeeded();

        if (timelineConfig == null || enemySpawner == null || gameTimer == null)
        {
            Debug.LogError("GameTimelineManager 缺少关键引用 (Timeline, Spawner, or Timer)！", this);
            enabled = false;
            return;
        }
        ApplyDemoTimelineExperienceModifier();
        InitializeTimeline();
    }

    private void SelectDemoTimelineIfNeeded()
    {
        if (useDebugTimelineOverride)
        {
            if (debugTimelineOverrideConfig != null)
            {
                timelineConfig = debugTimelineOverrideConfig;
                Debug.Log($"<color=cyan>[Timeline] Debug timeline override selected: {timelineConfig.name}</color>");
                return;
            }

            Debug.LogWarning("[Timeline] useDebugTimelineOverride is enabled, but debugTimelineOverrideConfig is not assigned.");
        }

        if (!autoSelectDemoTimeline || !DemoContentGate.DemoModeEnabled) return;
        if (!DemoContentGate.IsSceneAllowed(SceneManager.GetActiveScene().name)) return;

        if (useBuildTest60Timeline && demoBuildTest60TimelineConfig != null)
        {
            timelineConfig = demoBuildTest60TimelineConfig;
            advanceWaveOnClear = false;
            Debug.Log($"<color=cyan>[Timeline] Build test 60 timeline selected: {timelineConfig.name}</color>");
            return;
        }

        if (!DemoContentGate.EnableHardModeInDemo && DataManager.Instance != null)
        {
            DataManager.Instance.selectedDemoDifficulty = DemoDifficultySelection.Normal;
        }

        bool hardUnlocked = DemoContentGate.EnableHardModeInDemo
            && PlayerProgressManager.Instance != null
            && PlayerProgressManager.Instance.IsItemUnlocked(DemoContentGate.HardUnlockItemId);

        bool hardSelected = DemoContentGate.EnableHardModeInDemo
            && DataManager.Instance != null
            && DataManager.Instance.selectedDemoDifficulty == DemoDifficultySelection.Hard;

        GameTimelineConfig selected = hardUnlocked && hardSelected ? demoHardTimelineConfig : demoIntroTimelineConfig;
        if (selected != null)
        {
            timelineConfig = selected;
            Debug.Log($"<color=cyan>[Timeline] Demo timeline selected: {timelineConfig.name}</color>");
        }
    }

    public string GetActiveTimelineName()
    {
        return timelineConfig != null ? timelineConfig.name : string.Empty;
    }

    private void ApplyDemoTimelineExperienceModifier()
    {
        if (PlayerLevelManager.Instance == null) return;

        string activeTimelineName = GetActiveTimelineName();
        float multiplier = IsBuildTest60TimelineName(activeTimelineName)
            ? demoBuildTest60ExperienceMultiplier
            : DemoContentGate.IsHardTimelineName(activeTimelineName)
                ? demoHardExperienceMultiplier
                : demoIntroExperienceMultiplier;
        PlayerLevelManager.Instance.SetTimelineExperienceMultiplier(multiplier);
        Debug.Log($"<color=yellow>[Timeline] XP multiplier: {multiplier:F2}x</color>");
    }

    private void InitializeDemoSurpriseEvents()
    {
        demoSurpriseEventsTriggered = 0;
        lastDemoSurpriseWave = null;
        nextDemoSurpriseTime = -1f;

        if (!ShouldRunDemoSurpriseEvents()) return;

        float eventWindowEnd = GetDemoSurpriseWindowEnd();
        float firstTime = Mathf.Max(0f, demoSurpriseFirstTime);
        if (firstTime > eventWindowEnd) return;

        nextDemoSurpriseTime = firstTime;
        Debug.Log($"<color=magenta>[Timeline] Demo surprise events armed. First at {nextDemoSurpriseTime:F0}s.</color>");
    }

    private bool ShouldRunDemoSurpriseEvents()
    {
        if (!enableDemoSurpriseEvents) return false;
        if (!DemoContentGate.DemoModeEnabled) return false;
        if (demoSurpriseMaxEvents <= 0) return false;
        if (demoSurpriseWaves == null || demoSurpriseWaves.Count == 0) return false;
        string activeTimelineName = GetActiveTimelineName();
        if (demoSurpriseHardOnly
            && !DemoContentGate.IsHardTimelineName(activeTimelineName)
            && !IsBuildTest60TimelineName(activeTimelineName)) return false;
        return true;
    }

    private float GetDemoSurpriseWindowEnd()
    {
        float timelineEnd = timelineConfig != null ? timelineConfig.totalGameDuration : demoSurpriseLastTime;
        if (IsBuildTest60TimelineName(GetActiveTimelineName()))
        {
            return Mathf.Max(0f, timelineEnd - 180f);
        }
        return Mathf.Min(Mathf.Max(0f, demoSurpriseLastTime), Mathf.Max(0f, timelineEnd));
    }

    private bool IsBuildTest60TimelineName(string timelineName)
    {
        return !string.IsNullOrEmpty(timelineName) && timelineName.Contains("BuildTest60");
    }

    private void TryFireDemoSurpriseEvent(float elapsedTime)
    {
        if (nextDemoSurpriseTime < 0f || elapsedTime < nextDemoSurpriseTime) return;

        if (!ShouldRunDemoSurpriseEvents() || demoSurpriseEventsTriggered >= demoSurpriseMaxEvents)
        {
            nextDemoSurpriseTime = -1f;
            return;
        }

        if (elapsedTime > GetDemoSurpriseWindowEnd())
        {
            nextDemoSurpriseTime = -1f;
            return;
        }

        WaveConfig wave = PickDemoSurpriseWave();
        if (wave == null)
        {
            nextDemoSurpriseTime = -1f;
            return;
        }

        FireDemoSurpriseWave(wave);
        demoSurpriseEventsTriggered++;
        ScheduleNextDemoSurpriseEvent(elapsedTime);
    }

    private WaveConfig PickDemoSurpriseWave()
    {
        List<WaveConfig> candidates = demoSurpriseWaves
            .Where(wave => wave != null && wave != lastDemoSurpriseWave)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = demoSurpriseWaves
                .Where(wave => wave != null)
                .ToList();
        }

        if (candidates.Count == 0) return null;

        WaveConfig selected = candidates[Random.Range(0, candidates.Count)];
        lastDemoSurpriseWave = selected;
        return selected;
    }

    private void FireDemoSurpriseWave(WaveConfig wave)
    {
        float elapsedTime = gameTimer != null ? gameTimer.GetElapsedTime() : 0f;
        int effectiveWaveNumber = 1 + (int)(elapsedTime / 60f);
        float healthScaleMultiplier = GetLateHealthMultiplier(elapsedTime);
        float countMultiplier = GetSpawnCountMultiplier(elapsedTime);
        int spawned = wave.GetTotalEnemiesInWave(countMultiplier);
        aliveEnemyCount += spawned;
        Debug.Log($"<color=magenta>[Timeline] Demo surprise {demoSurpriseEventsTriggered + 1}/{demoSurpriseMaxEvents}: '{wave.waveName}' | extraSpawned={spawned} | alive={aliveEnemyCount}</color>");

        enemySpawner.InstructToSpawnWaveConfig(
            wave,
            effectiveWaveNumber,
            healthGrowthFactorPerMinute,
            damageGrowthFactorPerMinute,
            speedGrowthFactorPerMinute,
            healthScaleMultiplier,
            countMultiplier
        );
    }

    private void ScheduleNextDemoSurpriseEvent(float currentTime)
    {
        if (!ShouldRunDemoSurpriseEvents() || demoSurpriseEventsTriggered >= demoSurpriseMaxEvents)
        {
            nextDemoSurpriseTime = -1f;
            return;
        }

        float minInterval = Mathf.Max(1f, Mathf.Min(demoSurpriseMinInterval, demoSurpriseMaxInterval));
        float maxInterval = Mathf.Max(minInterval, demoSurpriseMaxInterval);
        float nextTime = currentTime + Random.Range(minInterval, maxInterval);

        nextDemoSurpriseTime = nextTime <= GetDemoSurpriseWindowEnd() ? nextTime : -1f;
    }

    void InitializeTimeline()
    {
        pendingEvents.Clear();
        totalKills = 0;
        aliveEnemyCount = 0;
        pressureAliveEnemyCount = 0;
        pressureSpawnedTotal = 0;
        pressureCheckTimer = Mathf.Max(0.1f, pressureCheckInterval);
        lastMajorWaveTriggerTime = -999f;
        currentWaveIndex = 0;
        gameFinished = false;
        removedWithoutKillCount = 0;
        isAdvancing = false;
        allWavesFired = false;
        InitializeDemoSurpriseEvents();

        // 准备所有待处理事件
        foreach (var evt in timelineConfig.timelineEvents)
        {
            PendingEvent pending = new PendingEvent();
            pending.waveConfig = evt.waveToSpawn;

            if (evt.useRandomTimeRange)
            {
                // 在游戏开始时就计算好随机时间
                pending.triggerTime = Random.Range(evt.minTriggerTime, evt.maxTriggerTime);
            }
            else
            {
                pending.triggerTime = evt.fixedTriggerTime;
            }
            pendingEvents.Add(pending);
        }

        // 【关键】按触发时间对列表进行排序
        pendingEvents = pendingEvents.OrderBy(e => e.triggerTime).ToList();
        totalWaveCount = pendingEvents.Count;
        BuildContinuousPressureSources();

        // 设置 GameTimer 的倒计时总时长
        if (gameTimer != null)
        {
            gameTimer.SetTotalDuration(timelineConfig.totalGameDuration);
        }
    }

    private void BuildContinuousPressureSources()
    {
        pressureSpawnSources.Clear();

        for (int eventIndex = 0; eventIndex < pendingEvents.Count; eventIndex++)
        {
            PendingEvent pending = pendingEvents[eventIndex];
            if (pending == null || pending.waveConfig == null || pending.waveConfig.enemyGroups == null) continue;

            for (int groupIndex = 0; groupIndex < pending.waveConfig.enemyGroups.Count; groupIndex++)
            {
                EnemySpawnGroup group = pending.waveConfig.enemyGroups[groupIndex];
                if (!IsEligiblePressureSpawnGroup(group)) continue;

                pressureSpawnSources.Add(new PressureSpawnSource
                {
                    triggerTime = pending.triggerTime,
                    group = group
                });
            }
        }

        if (pressureDebugLogs)
        {
            Debug.Log($"<color=cyan>[Timeline] Continuous pressure sources: {pressureSpawnSources.Count}</color>");
        }
    }

    private bool IsEligiblePressureSpawnGroup(EnemySpawnGroup group)
    {
        if (group == null || group.count <= 0) return false;
        if (group.enemyType == null || group.enemyType.enemyPrefab == null) return false;
        if (group.enemyType.isBoss) return false;
        if (group.enemyType.aiType != AIType.Chasing) return false;
        if (group.enemyType.isSuicideBomber) return false;
        if (group.isElite || group.overrideStats) return false;
        if (group.formation != EnemySpawnGroup.FormationType.None) return false;
        if (group.specialBehavior != EnemySpecialBehavior.None) return false;

        return true;
    }

    void Update()
    {
        if (gameFinished) return;

        float elapsedTime = gameTimer.GetElapsedTime();

        // 检查是否触发了下一个事件（按时间）
        if (pendingEvents.Count > 0 && elapsedTime >= pendingEvents[0].triggerTime)
        {
            FireEvent(pendingEvents[0]);
            pendingEvents.RemoveAt(0);

            // 标记所有波次是否已全部触发
            if (pendingEvents.Count == 0)
            {
                allWavesFired = true;
            }
        }

        TryFireDemoSurpriseEvent(elapsedTime);
        UpdateContinuousPressure(elapsedTime);
    }

    void FireEvent(PendingEvent evt)
    {
        if (evt.waveConfig == null) return;

        currentWaveIndex++;
        currentWaveName = evt.waveConfig.waveName;
        float currentElapsedTime = gameTimer != null ? gameTimer.GetElapsedTime() : 0f;
        lastMajorWaveTriggerTime = currentElapsedTime;
        float countMultiplier = GetSpawnCountMultiplier(currentElapsedTime);
        waveSpawnedTotal = evt.waveConfig.GetTotalEnemiesInWave(countMultiplier);
        aliveEnemyCount += waveSpawnedTotal; // 累加（可能上一波还没打完）

        Debug.Log($"<color=orange>[Timeline] 触发波次 {currentWaveIndex}/{totalWaveCount}: '{currentWaveName}' | 本波生成={waveSpawnedTotal} | 当前存活={aliveEnemyCount} | 剩余波次={pendingEvents.Count - 1}</color>");

        // --- 动态计算属性成长 ---
        float elapsedTime = gameTimer != null ? gameTimer.GetElapsedTime() : 0f;
        int effectiveWaveNumber = 1 + (int)(elapsedTime / 60f);
        float healthScaleMultiplier = GetLateHealthMultiplier(elapsedTime);

        // 【重要】我们复用 EnemySpawner 的方法，传入计算好的成长值
        enemySpawner.InstructToSpawnWaveConfig(
            evt.waveConfig,
            effectiveWaveNumber,
            healthGrowthFactorPerMinute,
            damageGrowthFactorPerMinute,
            speedGrowthFactorPerMinute,
            healthScaleMultiplier,
            countMultiplier
        );
    }

    private float GetLateHealthMultiplier(float elapsedTime)
    {
        if (!enableLateHealthRamp || lateHealthMultiplierPerMinute <= 0f || lateHealthMultiplierMax <= 1f)
        {
            return 1f;
        }

        float minutesAfterStart = Mathf.Max(0f, elapsedTime - Mathf.Max(0f, lateHealthRampStartTime)) / 60f;
        float multiplier = 1f + minutesAfterStart * lateHealthMultiplierPerMinute;
        return Mathf.Clamp(multiplier, 1f, lateHealthMultiplierMax);
    }

    private float GetSpawnCountMultiplier(float elapsedTime)
    {
        if (!enablePostThreeMinuteCountRamp || countRampMaxMultiplier <= 1f)
        {
            return 1f;
        }

        float rampStart = Mathf.Max(0f, countRampStartTime);
        if (elapsedTime < rampStart)
        {
            return 1f;
        }

        float minutesAfterStart = Mathf.Max(0f, elapsedTime - rampStart) / 60f;
        float multiplier = Mathf.Max(1f, countRampBaseMultiplier)
            + minutesAfterStart * Mathf.Max(0f, countRampMultiplierPerMinute);
        return Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, countRampMaxMultiplier));
    }

    private void UpdateContinuousPressure(float elapsedTime)
    {
        if (!enableContinuousPressure || enemySpawner == null) return;
        if (allWavesFired || pendingEvents.Count == 0) return;
        if (isAdvancing) return;
        if (pressureSpawnSources.Count == 0) return;
        if (elapsedTime < Mathf.Max(0f, pressureStartTime)) return;
        if (elapsedTime - lastMajorWaveTriggerTime < Mathf.Max(0.1f, pressureCheckInterval)) return;

        pressureCheckTimer -= Time.deltaTime;
        if (pressureCheckTimer > 0f) return;

        pressureCheckTimer = Mathf.Max(0.1f, pressureCheckInterval);

        int targetAlive = GetContinuousPressureTargetAlive(elapsedTime);
        int totalCombatEnemies = Mathf.Max(0, aliveEnemyCount) + Mathf.Max(0, pressureAliveEnemyCount);
        int refillAt = Mathf.Max(1, Mathf.FloorToInt(targetAlive * Mathf.Clamp01(pressureRefillThreshold)));
        if (totalCombatEnemies > refillAt) return;

        int hardCap = Mathf.Max(refillAt + 1, Mathf.CeilToInt(targetAlive * Mathf.Max(1f, pressureHardCapMultiplier)));
        int room = hardCap - totalCombatEnemies;
        if (room <= 0) return;

        PressureSpawnSource source = PickPressureSpawnSource(elapsedTime);
        if (source == null || source.group == null) return;

        int desired = Mathf.Max(1, targetAlive - totalCombatEnemies);
        int count = Mathf.Clamp(desired, 1, Mathf.Max(1, pressureMaxBatchSize));
        count = Mathf.Min(count, room);

        FirePressureSpawn(source.group, count, elapsedTime);
    }

    private int GetContinuousPressureTargetAlive(float elapsedTime)
    {
        float minutes = Mathf.Max(0f, elapsedTime) / 60f;
        int target = pressureBaseTargetAlive + Mathf.FloorToInt(minutes * Mathf.Max(0, pressureTargetAliveIncreasePerMinute));
        return Mathf.Clamp(target, 1, Mathf.Max(1, pressureTargetAliveMax));
    }

    private PressureSpawnSource PickPressureSpawnSource(float elapsedTime)
    {
        float unlockTime = elapsedTime + Mathf.Max(0f, pressureEnemyUnlockLookAhead);
        List<PressureSpawnSource> candidates = pressureSpawnSources
            .Where(source => source != null && source.group != null && source.triggerTime <= unlockTime)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = pressureSpawnSources
                .Where(source => source != null && source.group != null)
                .ToList();
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void FirePressureSpawn(EnemySpawnGroup sourceGroup, int count, float elapsedTime)
    {
        if (sourceGroup == null || count <= 0) return;

        WaveConfig pressureWave = ScriptableObject.CreateInstance<WaveConfig>();
        pressureWave.name = "Runtime_ContinuousPressure";
        pressureWave.hideFlags = HideFlags.DontSave;
        pressureWave.waveName = "Continuous Pressure";
        pressureWave.waveType = WaveType.Normal;
        pressureWave.enemyGroups = new List<EnemySpawnGroup>
        {
            CreatePressureSpawnGroup(sourceGroup, count)
        };

        pressureAliveEnemyCount += count;
        pressureSpawnedTotal += count;

        int effectiveWaveNumber = 1 + (int)(elapsedTime / 60f);
        enemySpawner.InstructToSpawnWaveConfig(
            pressureWave,
            effectiveWaveNumber,
            healthGrowthFactorPerMinute,
            damageGrowthFactorPerMinute,
            speedGrowthFactorPerMinute,
            GetLateHealthMultiplier(elapsedTime),
            1f
        );

        if (pressureDebugLogs)
        {
            string enemyName = sourceGroup.enemyType != null ? sourceGroup.enemyType.name : "Unknown";
            Debug.Log($"<color=cyan>[Timeline] Pressure spawn +{count} {enemyName} | main={aliveEnemyCount} pressure={pressureAliveEnemyCount}</color>");
        }
    }

    private EnemySpawnGroup CreatePressureSpawnGroup(EnemySpawnGroup sourceGroup, int count)
    {
        return new EnemySpawnGroup
        {
            enemyType = sourceGroup.enemyType,
            count = Mathf.Max(1, count),
            formation = EnemySpawnGroup.FormationType.None,
            spawnIntervalWithinGroup = Mathf.Max(0.05f, pressureSpawnIntervalWithinBatch),
            delayAfterPreviousGroupStarts = 0f,
            directionHint = SpawnDirectionHint.Random,
            overrideSpawnerBurstSettings = true,
            burstSpawnThreshold = 9999,
            burstSpawnTotalDuration = Mathf.Max(0.05f, pressureSpawnIntervalWithinBatch * Mathf.Max(1, count)),
            isPressureSpawn = true
        };
    }

    /// <summary>
    /// 提前触发下一波（当当前波敌人全部被消灭时）
    /// </summary>
    private void AdvanceToNextWave()
    {
        if (pendingEvents.Count == 0 || gameFinished || isAdvancing) return;

        isAdvancing = true;
        // 延迟一小段时间再推进，给玩家喘息
        StartCoroutine(AdvanceDelayRoutine());
    }

    private System.Collections.IEnumerator AdvanceDelayRoutine()
    {
        yield return new WaitForSeconds(advanceDelay);

        if (pendingEvents.Count > 0 && !gameFinished)
        {
            FireEvent(pendingEvents[0]);
            pendingEvents.RemoveAt(0);
            if (pendingEvents.Count == 0)
            {
                allWavesFired = true;
            }
        }
        isAdvancing = false;
    }

    // 记录最后被击杀的 Boss 信息（由 EnemyDefeated 传入）
    private Vector3 lastBossDeathPosition;
    private GameObject lastBossGameObject;

    void GameWin()
    {
        if (gameFinished) return;

        gameFinished = true;
        GameManager.Instance?.BeginVictoryPending();
        // 停止所有刷怪
        enemySpawner.StopAndClearSpawning();

        // 如果有 Boss 死亡表演系统，播放表演后再结算
        if (BossDeathCeremony.Instance != null && lastBossGameObject != null)
        {
            Debug.Log("<color=gold>[Timeline] 启动 Boss 死亡表演！</color>");
            BossDeathCeremony.Instance.StartCeremony(lastBossDeathPosition, lastBossGameObject);
            return;
        }

        // 没有表演系统，直接触发胜利结算界面
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleVictory();
        }
    }


    // --- 公共方法，用于替换 WaveManager 的功能 ---

    private bool TryCompleteGameIfReady(string reason)
    {
        if (!allWavesFired || gameFinished) return false;

        if (pressureAliveEnemyCount > 0)
        {
            pressureAliveEnemyCount = CountAlivePressureEnemyHealthObjects();
        }
        if (aliveEnemyCount > 0 || pressureAliveEnemyCount > 0) return false;

        Debug.Log($"<color=gold>[Timeline] Victory condition met ({reason}). kills={totalKills}, pressureSpawned={pressureSpawnedTotal}</color>");
        GameWin();
        return true;
    }

    public void EnemyDefeated()
    {
        totalKills++;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);

        if (allWavesFired && (aliveEnemyCount <= 100 || totalKills % 50 == 0))
        {
            ReconcileAliveEnemyCount("kill");
        }

        // 每50次击杀输出一次状态（减少日志量），或者当存活数很少时每次都输出
        if (totalKills % 50 == 0 || aliveEnemyCount <= 5)
        {
            Debug.Log($"<color=lime>[Timeline] 击杀#{totalKills} | 存活={aliveEnemyCount} | allWavesFired={allWavesFired} | gameFinished={gameFinished}</color>");
        }

        // 胜利判定：所有波次已触发 + 场上敌人全部被消灭 = 胜利
        if (TryCompleteGameIfReady("enemy-kill"))
        {
            return;
        }

        // 当场上敌人全部被消灭，且还有后续波次，立即推进
        if (advanceWaveOnClear && aliveEnemyCount <= 0 && pendingEvents.Count > 0 && !isAdvancing)
        {
            AdvanceToNextWave();
        }
    }

    /// <summary>
    /// 由 Health.Die() 在 Boss 死亡时调用，记录 Boss 信息供死亡表演使用
    /// </summary>
    public void RegisterBossDeath(Vector3 position, GameObject bossGO)
    {
        lastBossDeathPosition = position;
        lastBossGameObject = bossGO;
        Debug.Log($"<color=orange>[Timeline] Boss 死亡信息已注册: pos={position}</color>");
    }

    public void BossDefeated(Vector3 position, GameObject bossGO)
    {
        if (gameFinished) return;

        RegisterBossDeath(position, bossGO);
        totalKills++;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);

        Debug.Log($"<color=gold>[Timeline] Boss defeated; completing game immediately. trackedAlive={aliveEnemyCount}</color>");
        GameWin();
    }

    public void AnEnemyFailedToSpawn()
    {
        EnemiesFailedToSpawn(1);
    }

    public void EnemiesFailedToSpawn(int count)
    {
        if (count <= 0) return;

        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - count);
        Debug.LogWarning($"[GameTimelineManager] {count} enemies failed to spawn. alive={aliveEnemyCount}");

        if (allWavesFired)
        {
            ReconcileAliveEnemyCount("spawn-failed");
        }

        if (TryCompleteGameIfReady("spawn-failed"))
        {
            return;
        }
    }

    public void PressureEnemyDefeated()
    {
        pressureAliveEnemyCount = Mathf.Max(0, pressureAliveEnemyCount - 1);

        if (pressureDebugLogs && (pressureAliveEnemyCount <= 5 || pressureAliveEnemyCount % 10 == 0))
        {
            Debug.Log($"<color=cyan>[Timeline] Pressure enemy defeated. pressure={pressureAliveEnemyCount}, main={aliveEnemyCount}</color>");
        }

        TryCompleteGameIfReady("pressure-kill");
    }

    public void PressureEnemyFailedToSpawn()
    {
        PressureEnemiesFailedToSpawn(1);
    }

    public void PressureEnemiesFailedToSpawn(int count)
    {
        if (count <= 0) return;

        pressureAliveEnemyCount = Mathf.Max(0, pressureAliveEnemyCount - count);
        if (pressureDebugLogs)
        {
            Debug.LogWarning($"[GameTimelineManager] {count} pressure enemies failed to spawn. pressure={pressureAliveEnemyCount}");
        }

        TryCompleteGameIfReady("pressure-spawn-failed");
    }

    public void EnemyRemovedWithoutKill(string reason = "despawn")
    {
        if (gameFinished) return;

        removedWithoutKillCount++;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
        if (removedWithoutKillCount % 25 == 0 || aliveEnemyCount <= 5)
        {
            Debug.Log($"<color=yellow>[Timeline] Enemy removed without kill ({reason}) #{removedWithoutKillCount}. alive={aliveEnemyCount}</color>");
        }

        if (TryCompleteGameIfReady(reason))
        {
            return;
        }

        if (advanceWaveOnClear && aliveEnemyCount <= 0 && pendingEvents.Count > 0 && !isAdvancing)
        {
            AdvanceToNextWave();
        }
    }

    private void ReconcileAliveEnemyCount(string reason)
    {
        int sceneAlive = CountAliveEnemyHealthObjects();
        if (sceneAlive != aliveEnemyCount)
        {
            Debug.Log($"<color=yellow>[Timeline] Alive count reconciled ({reason}): tracked={aliveEnemyCount}, scene={sceneAlive}</color>");
            aliveEnemyCount = sceneAlive;
        }
    }

    private int CountAliveEnemyHealthObjects()
    {
        int count = 0;
        Health[] healthObjects = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < healthObjects.Length; i++)
        {
            Health health = healthObjects[i];
            if (health != null && !health.IsDead && health.gameObject.CompareTag("Enemy"))
            {
                if (health.GetComponent<PressureSpawnedEnemy>() != null) continue;
                count++;
            }
        }
        return count;
    }

    // --- 调试 UI ---
    private int CountAlivePressureEnemyHealthObjects()
    {
        int count = 0;
        Health[] healthObjects = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < healthObjects.Length; i++)
        {
            Health health = healthObjects[i];
            if (health != null
                && !health.IsDead
                && health.gameObject.CompareTag("Enemy")
                && health.GetComponent<PressureSpawnedEnemy>() != null)
            {
                count++;
            }
        }
        return count;
    }

    void OnGUI()
    {
        if (!AllowOnGuiDebug || !showDebugUI || gameFinished) return;

        // 左上角显示波次调试信息
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 18;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        style.padding = new RectOffset(10, 10, 5, 5);

        float elapsed = gameTimer != null ? gameTimer.GetElapsedTime() : 0;
        string timeStr = $"{(int)(elapsed / 60):00}:{(int)(elapsed % 60):00}";

        string info = $"波次: {currentWaveIndex}/{totalWaveCount}  [{currentWaveName}]\n" +
                      $"存活敌人: {aliveEnemyCount}  |  总击杀: {totalKills}\n" +
                      $"时间: {timeStr}  |  剩余波次: {pendingEvents.Count}";

        GUI.Box(new Rect(10, 10, 420, 80), info, style);
    }
}
