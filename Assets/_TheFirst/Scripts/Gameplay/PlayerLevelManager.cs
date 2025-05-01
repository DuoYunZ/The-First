using UnityEngine;
using UnityEngine.Events;

// 挂载在 MechRoot 上
public class PlayerLevelManager : MonoBehaviour
{
    [Header("等级与经验")]
    public int currentLevel = 1;
    public int currentExperience = 0;
    public int experienceToNextLevel = 10; // 升到下一级所需的经验

    [Header("升级事件")]
    public UnityEvent OnLevelUp; // 升级时触发 (用于打开升级选择界面等)

    // (可选) 用于计算下一级所需经验的曲线或公式参数
    // public AnimationCurve xpCurve;
    // public float baseXp = 10;
    // public float levelMultiplier = 1.5f;

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
        currentLevel++;
        currentExperience -= experienceToNextLevel; // 减去当前等级所需的经验

        // 计算升到再下一级所需的经验 (这里用一个简单的示例公式)
        experienceToNextLevel = CalculateNextLevelXP(currentLevel);

        Debug.Log($"--- LEVEL UP! --- 达到等级 {currentLevel}. 下一级需要 {experienceToNextLevel} XP.");

        // 触发升级事件
        OnLevelUp?.Invoke();

        // (可选) 可以在这里添加升级特效、音效等
    }

    /// <summary>
    /// 计算升到指定等级所需的经验值 (示例)
    /// </summary>
    private int CalculateNextLevelXP(int level)
    {
        // 简单的线性增长 + 固定基础值 (你可以自定义更复杂的公式)
        return 10 + (level - 1) * 5;
        // 或者指数增长: return Mathf.RoundToInt(baseXp * Mathf.Pow(levelMultiplier, level - 1));
    }

    // (可选) 获取当前等级等信息的方法
    public int GetLevel() => currentLevel;
    public int GetCurrentXP() => currentExperience;
    public int GetXPToNextLevel() => experienceToNextLevel;
    public float GetXPPercentage() => (float)currentExperience / experienceToNextLevel;
}