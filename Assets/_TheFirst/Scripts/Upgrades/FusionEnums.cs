/// <summary>
/// 融合条件类型
/// </summary>
public enum ConditionType
{
    Weapon,     // 需要拥有特定武器
    Passive,    // 需要拥有特定被动道具
    Talent      // 需要拥有特定天赋
}

/// <summary>
/// 融合类型
/// </summary>
public enum FusionType
{
    Replace,    // 替换触发武器
    Merge,      // 两武器融合消失
    Upgrade     // 升级并解锁新武器到卡池
}
