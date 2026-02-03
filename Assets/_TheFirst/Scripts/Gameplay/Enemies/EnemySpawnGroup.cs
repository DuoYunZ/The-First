// 你可以创建一个新的 C# 脚本 EnemySpawnGroup.cs
// 或者如果它只被 WaveConfig 使用，也可以将其定义在 WaveConfig.cs 文件内部（但作为独立类）
using UnityEngine;

[System.Serializable] // 使其可以在 WaveConfig 的 Inspector 中被编辑
public class EnemySpawnGroup
{
    public enum FormationType
    {
        None, // 默认的随机生成
        Line, // 水平线
        V_Shape,
        Grid
    }

    [Header("阵型设置 (Formation)")]
    [Tooltip("选择此组敌人生成的阵型")]
    public FormationType formation = FormationType.None;
    [Tooltip("阵型中敌人之间的间距")]
    public float formationSpacing = 2f;
    [Tooltip("V字形的深度因子 (0.5表示每隔1单位间距，也前进0.5单位)")]
    public float vShapeDepthFactor = 0.5f;

    [Tooltip("网格阵型的列数。行数将自动计算。")]
    public int gridColumns = 5; // <--- 2. 在这里添加这一行

    [Tooltip("阵型偏移量(沿前进方向)，用于创建交错线")]
    public float formationOffset = 0f; // <--- 在这里添加这一行

    [Tooltip("要生成的敌人类型 (引用 EnemyType ScriptableObject)")]
    public EnemyType enemyType;

    [Tooltip("生成此类型敌人的数量")]
    public int count = 1;

    [Tooltip("（可选）此组敌人是否为精英怪")]
    public bool isElite = false;

    [Tooltip("在本波中，此组敌人在上一组敌人开始生成后延迟多少秒开始生成")]
    public float delayAfterPreviousGroupStarts = 0f;

    [Tooltip("在此组内部，每个敌人之间的生成间隔时间。如果为0，则使用 EnemySpawner 的全局默认间隔。")]
    public float spawnIntervalWithinGroup = 0.2f;

    [Tooltip("（可选）此组敌人生成的方向提示")]
    public SpawnDirectionHint directionHint = SpawnDirectionHint.Random;

    [Header("爆发生成覆盖 (可选)")]
    [Tooltip("勾选此项，为此组敌人启用独立的爆发生成设置，忽略 EnemySpawner 上的全局设置。")]
    public bool overrideSpawnerBurstSettings = false;

    [Tooltip("【独立设置】当这组敌人的数量超过此值时，启用爆发生成模式。")]
    public int burstSpawnThreshold = 20;

    [Tooltip("【独立设置】在爆发模式下，陆续生成完这一整组敌人所用的总时间（秒）。")]
    public float burstSpawnTotalDuration = 1.5f;

    [Header("自定义属性覆盖 (可选)")]
    [Tooltip("勾选此项，以启用下面的自定义属性，它会忽略'Is Elite'的设置。")]
    public bool overrideStats = false;

    [Tooltip("在此处设置的属性，将覆盖上面 EnemyType 中的基础值。")]
    public EnemyStatOverrides statOverrides;
    // --- 未来可扩展字段 (怪物生成多样性) ---
    // [Tooltip("（可选）指定此组敌人必须从哪个出生点生成")]
    // public Transform specificSpawnPointOverride;
}

// (可选) 如果要用到方向提示，可以定义一个枚举
public enum SpawnDirectionHint
{
    Random,
    North,      // 北 (0°)
    Northeast,  // 东北 (45°)
    East,       // 东 (90°)
    Southeast,  // 东南 (135°)
    South,      // 南 (180°)
    Southwest,  // 西南 (225°)
    West,       // 西 (270°)
    Northwest,   // 西北 (315°)
    AllSides
    }