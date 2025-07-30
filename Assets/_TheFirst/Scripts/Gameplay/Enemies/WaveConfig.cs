// 创建新脚本: Assets/Scripts/GameManagement/WaveConfig.cs (或你喜欢的路径)
using UnityEngine;
using System.Collections.Generic;

// --- 为了“特殊波次”提前定义的枚举 ---
public enum WaveType
{
    Normal,     // 普通波次
    Boss,       // Boss 波次
    Reward,     // 奖励波次
    // 你可以根据需要添加更多类型，例如 Horde (尸潮), TimedSurvival (限时生存)等
}

[CreateAssetMenu(fileName = "WaveConfig_01", menuName = "Game/Wave Configuration")]
public class WaveConfig : ScriptableObject
{
    [Header("波次基础信息")]
    [Tooltip("波次名称 (可选，用于显示或调试)")]
    public string waveName = "Standard Wave";

    [Tooltip("波次类型 (用于特殊逻辑)")]
    public WaveType waveType = WaveType.Normal;

    // public int waveNumber; // 波次编号可以由其在 WaveManager 列表中的顺序决定，不一定需要显式指定

    [Header("敌人构成 (核心)")]
    [Tooltip("定义了这一波中会按顺序生成的不同敌人组")]
    public List<EnemySpawnGroup> enemyGroups;

    [Header("波次特定参数 (可选覆盖)")]
    [Tooltip("（可选）此波次结束后到下一波次的自定义等待时间。如果为0，则使用 WaveManager 的默认设置。")]
    public float customTimeUntilNextWave = 0f;

    [Header("波次持续时间 (可选)")]
    [Tooltip("此波次的最大持续时间 (秒)。如果为0或负数，则表示没有时间限制，必须清空所有敌人才结束。")]
    public float maxWaveDuration = 0f;

    // --- 未来可扩展字段 (用于更精细控制或特殊波次) ---
    // [Tooltip("（可选）此波次是否有时间限制 (秒)，0表示无限制")]
    // public float timeLimitForWave = 0f;
    // [Tooltip("（可选）此波次特定的全局生命值乘数 (会覆盖WaveManager的成长计算，或者在其基础上再乘)")]
    // public float healthMultiplierOverride = 1f;
    // [Tooltip("（可选）此波次特定的全局伤害乘数")]
    // public float damageMultiplierOverride = 1f;
    // [Tooltip("（可选）此波次特定的全局速度乘数")]
    // public float speedMultiplierOverride = 1f;


    /// <summary>
    /// 计算并返回此波次中所有敌人组的总敌人数量。
    /// </summary>
    public int GetTotalEnemiesInWave()
    {
        int total = 0;
        if (enemyGroups != null)
        {
            foreach (var group in enemyGroups)
            {
                total += group.count;
            }
        }
        return total;
    }
}