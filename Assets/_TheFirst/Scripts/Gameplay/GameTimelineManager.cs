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

    [Header("波次提前完成设置")]
    [Tooltip("勾选此项，当前波敌人全部被消灭后立即进入下一波")]
    public bool advanceWaveOnClear = true;
    [Tooltip("提前进入下一波前的短暂延迟（秒），给玩家喘息时间")]
    public float advanceDelay = 1.0f;

    [Header("调试显示")]
    [Tooltip("勾选此项在左上角显示当前波次调试信息")]
    public bool showDebugUI = true;

    // --- 内部运行时列表 ---
    private class PendingEvent
    {
        public float triggerTime;
        public WaveConfig waveConfig;
    }
    private List<PendingEvent> pendingEvents = new List<PendingEvent>();
    private bool gameFinished = false;

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
        if (timelineConfig == null || enemySpawner == null || gameTimer == null)
        {
            Debug.LogError("GameTimelineManager 缺少关键引用 (Timeline, Spawner, or Timer)！", this);
            enabled = false;
            return;
        }
        InitializeTimeline();
    }

    void InitializeTimeline()
    {
        pendingEvents.Clear();
        totalKills = 0;
        aliveEnemyCount = 0;
        currentWaveIndex = 0;
        gameFinished = false;
        isAdvancing = false;
        allWavesFired = false;

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

        // 设置 GameTimer 的倒计时总时长
        if (gameTimer != null)
        {
            gameTimer.SetTotalDuration(timelineConfig.totalGameDuration);
        }
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
    }

    void FireEvent(PendingEvent evt)
    {
        if (evt.waveConfig == null) return;

        currentWaveIndex++;
        currentWaveName = evt.waveConfig.waveName;
        waveSpawnedTotal = evt.waveConfig.GetTotalEnemiesInWave();
        aliveEnemyCount += waveSpawnedTotal; // 累加（可能上一波还没打完）

        // --- 动态计算属性成长 ---
        int effectiveWaveNumber = 1 + (int)(gameTimer.GetElapsedTime() / 60f);

        // 【重要】我们复用 EnemySpawner 的方法，传入计算好的成长值
        enemySpawner.InstructToSpawnWaveConfig(
            evt.waveConfig,
            effectiveWaveNumber,
            healthGrowthFactorPerMinute,
            damageGrowthFactorPerMinute,
            speedGrowthFactorPerMinute
        );
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
        }
        isAdvancing = false;
    }

    void GameWin()
    {
        gameFinished = true;
        // 停止所有刷怪
        enemySpawner.StopAndClearSpawning();

        // 触发胜利结算界面
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleVictory();
        }
    }


    // --- 公共方法，用于替换 WaveManager 的功能 ---

    public void EnemyDefeated()
    {
        totalKills++;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);

        // 胜利判定：所有波次已触发 + 场上敌人全部被消灭 = 胜利
        if (allWavesFired && aliveEnemyCount <= 0 && !gameFinished)
        {
            GameWin();
            return;
        }

        // 当场上敌人全部被消灭，且还有后续波次，立即推进
        if (advanceWaveOnClear && aliveEnemyCount <= 0 && pendingEvents.Count > 0 && !isAdvancing)
        {
            AdvanceToNextWave();
        }
    }

    public void AnEnemyFailedToSpawn()
    {
        // 生成失败的敌人也要从存活数中扣除，避免永远无法"清完"
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
        Debug.LogWarning("[GameTimelineManager] 一个敌人未能生成。");
    }

    // --- 调试 UI ---
    void OnGUI()
    {
        if (!showDebugUI || gameFinished) return;

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