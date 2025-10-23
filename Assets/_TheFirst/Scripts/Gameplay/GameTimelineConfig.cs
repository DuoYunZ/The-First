using UnityEngine;
using System.Collections.Generic;

// 这个 [System.Serializable] 类不需要是单独的文件，它会内嵌在 Config 中
[System.Serializable]
public class TimelineEvent
{
    [Tooltip("仅用于调试，方便识别")]
    public string eventName;

    [Tooltip("要生成的波次配置 (WaveConfig)")]
    public WaveConfig waveToSpawn;

    [Header("触发时间设置")]
    [Tooltip("勾选此项，将在下面的最小/最大时间之间随机触发")]
    public bool useRandomTimeRange;

    [Tooltip("固定的触发时间 (秒)")]
    public float fixedTriggerTime;

    [Tooltip("随机触发的最小时间 (秒)")]
    public float minTriggerTime;
    [Tooltip("随机触发的最大时间 (秒)")]
    public float maxTriggerTime;
}


[CreateAssetMenu(fileName = "GameTimeline_Main", menuName = "Game/Game Timeline Config")]
public class GameTimelineConfig : ScriptableObject
{
    [Tooltip("此关卡的总时长 (秒)，例如 20 * 60 = 1200")]
    public float totalGameDuration = 1200f;

    [Tooltip("关卡的时间轴事件列表")]
    public List<TimelineEvent> timelineEvents;
}