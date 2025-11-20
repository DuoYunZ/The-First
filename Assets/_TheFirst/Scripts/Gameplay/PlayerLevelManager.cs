using System;
using UnityEngine;
using UnityEngine.Events;

// 挂载在 MechRoot 上
public class PlayerLevelManager : MonoBehaviour
{
    #region Singleton Pattern
    // 1. 创建一个公共的、静态的、只读的实例属性
    public static PlayerLevelManager Instance { get; private set; }

    // 2. 在 Awake 方法中设置这个实例
    private void Awake()
    {
        // 如果还没有实例，那么将这个对象设为实例
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果你需要这个管理器跨场景存在，可以取消这行注释
        }
        // 如果实例已存在，并且不是当前这个对象，则销毁当前这个多余的对象
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    public enum LevelingScheme
    {
        Quadratic,      // 二次方增长 (推荐：适合大多数肉鸽，后期平滑变难)
        Exponential,    // 指数增长 (极难：后期几乎升不动)
        Linear,         // 线性 (简单：后期升级太快，不推荐)
        CustomCurve     // 自定义曲线 (最灵活：在 Inspector 面板画线)
    }

    [Header("等级与经验")]
    public int currentLevel = 1;
    public int currentExperience = 0;
    public int experienceToNextLevel = 10; // 升到下一级所需的经验

    public event Action<int> OnLevelUp;

    [Header("经验曲线配置")]
    [Tooltip("选择经验增长的计算公式")]
    public LevelingScheme levelingScheme = LevelingScheme.Quadratic;

    [Header("参数设置 (仅对非曲线模式有效)")]
    [Tooltip("基础经验值 (1级升2级大概需要多少)")]
    public int baseXp = 10;

    [Tooltip("线性增长系数 (影响前期节奏)")]
    public float linearFactor = 10f;

    [Tooltip("二次方/指数增长系数 (影响后期节奏，数值越大约难)")]
    public float powerFactor = 1.5f;

    [Header("自定义曲线 (仅 CustomCurve 模式有效)")]
    [Tooltip("X轴=等级, Y轴=所需经验。请将X轴范围设为 1~100 (或你的最大等级)")]
    public AnimationCurve xpCurve = new AnimationCurve(new Keyframe(1, 10), new Keyframe(100, 10000));

    private void Start()
    {
        // 初始化第一级所需的经验
        experienceToNextLevel = CalculateNextLevelXP(currentLevel);
    }

    /// <summary>
    /// 增加经验值。
    /// </summary>
    /// <param name="amount">增加的经验数量</param>
    public void AddExperience(int amount)
    {
        currentExperience += amount;
        Debug.Log($"获得 {amount} XP. 当前: {currentExperience} / {experienceToNextLevel}");

        // 检查是否升级
        while (currentExperience >= experienceToNextLevel) // 使用 while 以处理一次获得很多经验连升多级的情况
        {
            LevelUp();
        }

        // (可选) 在这里更新经验条 UI
        // UpdateXpUI();
    }

    /// <summary>
    /// 处理升级逻辑。
    /// </summary>
    private void LevelUp()
    {
        // --- 日志 1 ---
        Debug.Log("<color=lime>-- CHECKPOINT 1: LevelUp() method in PlayerLevelManager was called! --</color>");

        currentLevel++;
        currentExperience -= experienceToNextLevel; // 减去当前等级所需的经验

        // 计算升到再下一级所需的经验 (这里用一个简单的示例公式)
        experienceToNextLevel = CalculateNextLevelXP(currentLevel);

        Debug.Log($"--- LEVEL UP! --- 达到等级 {currentLevel}. 下一级需要 {experienceToNextLevel} XP.");

        // 触发升级事件
        OnLevelUp?.Invoke(currentLevel); // 触发升级事件，通知 UpgradeManager

        // (可选) 可以在这里添加升级特效、音效等
    }

    /// <summary>
    /// 计算升到指定等级所需的经验值 (示例)
    /// </summary>
    private int CalculateNextLevelXP(int level)
    {
        float nextReq = 0;

        switch (levelingScheme)
        {
            case LevelingScheme.Linear:
                // 公式: Base + Level * Linear
                // 缺点: 后期太容易升级
                nextReq = baseXp + (level * linearFactor);
                break;

            case LevelingScheme.Quadratic:
                // 公式: Base + Level * Linear + Level^2 * Power
                // 优点: 完美契合割草游戏。前期线性，后期随着杀怪数平方级增长，难度适中。
                // 示例: Lvl 10=160xp, Lvl 50=4260xp (假设 param=1.5)
                nextReq = baseXp + (level * linearFactor) + (Mathf.Pow(level, 2) * powerFactor);
                break;

            case LevelingScheme.Exponential:
                // 公式: Base * Power^(Level-1)
                // 缺点: 极容易导致数值溢出或后期完全升不动
                nextReq = baseXp * Mathf.Pow(powerFactor, level - 1);
                break;

            case LevelingScheme.CustomCurve:
                // 直接从图表中读取
                nextReq = xpCurve.Evaluate(level);
                break;
        }

        return Mathf.Max(10, Mathf.FloorToInt(nextReq)); // 确保至少需要10点，防止负数或0
    }

    // (可选) 获取当前等级等信息的方法
    public float GetXPProgressNormalized()
    {
        if (experienceToNextLevel == 0) return 1f;
        return (float)currentExperience / experienceToNextLevel;
    }
    public int GetLevel() => currentLevel;
    public int GetCurrentXP() => currentExperience;
    public int GetXPToNextLevel() => experienceToNextLevel;
    public float GetXPPercentage() => (float)currentExperience / experienceToNextLevel;
}