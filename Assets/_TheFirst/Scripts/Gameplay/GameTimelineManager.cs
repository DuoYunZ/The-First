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
    [Tooltip("每“分钟”敌人增加的生命值百分比")]
    public float healthGrowthFactorPerMinute = 0.1f;
    [Tooltip("每“分钟”敌人增加的伤害百分比")]
    public float damageGrowthFactorPerMinute = 0.05f;
    [Tooltip("每“分钟”敌人增加的速度百分比")]
    public float speedGrowthFactorPerMinute = 0.02f;

    // --- 内部运行时列表 ---
    private class PendingEvent
    {
        public float triggerTime;
        public WaveConfig waveConfig;
    }
    private List<PendingEvent> pendingEvents = new List<PendingEvent>();
    private bool gameFinished = false;

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
        gameFinished = false;

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

        Debug.Log($"时间轴已初始化，总计 {pendingEvents.Count} 个事件已排序。");
    }

    void Update()
    {
        if (gameFinished) return;

        float elapsedTime = gameTimer.GetElapsedTime();

        // 1. 检查是否触发了下一个事件
        if (pendingEvents.Count > 0 && elapsedTime >= pendingEvents[0].triggerTime)
        {
            FireEvent(pendingEvents[0]);
            pendingEvents.RemoveAt(0); // 移除已触发的事件
        }

        // 2. 检查游戏是否胜利 (时间到)
        if (elapsedTime >= timelineConfig.totalGameDuration)
        {
            GameWin();
        }
    }

    void FireEvent(PendingEvent evt)
    {
        if (evt.waveConfig == null) return;

        Debug.Log($"<color=cyan>时间轴事件触发: {evt.waveConfig.name} (时间: {gameTimer.GetElapsedTime():F1}s)</color>");

        // --- 动态计算属性成长 ---
        // 我们用 "分钟" 作为 "波数" 来计算成长
        int effectiveWaveNumber = 1 + (int)(gameTimer.GetElapsedTime() / 60f);

        // 【重要】我们复用 EnemySpawner 的方法，传入计算好的成长值
        enemySpawner.InstructToSpawnWaveConfig(
            evt.waveConfig,
            effectiveWaveNumber,
            healthGrowthFactorPerMinute,
            damageGrowthFactorPerMinute,
            speedGrowthFactorPerMinute
        );

        // 注意：我们不再关心这个波次何时“结束”，系统会继续按时间触发下一个事件。
        // WaveConfig 上的 "maxWaveDuration" 字段现在基本失效了。
    }

    void GameWin()
    {
        gameFinished = true;
        Debug.Log("<color=green>游戏胜利！时间已到！</color>");

        // 停止所有刷怪
        enemySpawner.StopAndClearSpawning();

        // (在这里添加您的胜利逻辑，例如显示胜利UI，保存数据，返回Hub)
        // 示例：2秒后返回Hub
        // Invoke("ReturnToHub", 2f);
    }

    // (可选) 返回Hub的方法
    // void ReturnToHub()
    // {
    //     SceneManager.LoadScene("HubScene");
    // }


    // --- 公共方法，用于替换 WaveManager 的功能 ---

    public void EnemyDefeated()
    {
        // 我们不再检查波次是否结束，只是简单计数
        totalKills++;
        // (您可以在此更新UI上的击杀数)
        // UIManager.Instance?.UpdateKills(totalKills);
    }

    public void AnEnemyFailedToSpawn()
    {
        // 在这个新系统下，一个敌人生成失败并不会影响波次逻辑
        Debug.LogWarning("[GameTimelineManager] 一个敌人未能生成。");
    }
}