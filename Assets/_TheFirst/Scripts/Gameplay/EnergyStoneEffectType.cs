// EnergyStoneEffectType.cs
// 这个枚举定义了所有能量石可能拥有的 *基础* 效果类型。

public enum EnergyStoneEffectType
{
    // 元素类
    ApplyBurn,       // 施加燃烧 (来自烈焰石)
    ApplySlow,       // 施加减速 (来自寒冰石)
    ApplyStun,       // 施加眩晕 (来自大地石)
    ApplyChain,      // 施加连锁 (来自雷电石)
    ApplyKnockback,  // 施加击退 (来自风暴石)
    ApplyWeaken,     // 增加防御 
    ApplyCorrode,    // 施加腐蚀 (来自剧毒石)
    ApplyMagnet,

    // 机制类
    AddHoming,       // 附加追踪
    AddPierce,       // 增加穿透
    AddExplosion,    // 命中爆炸
    AddLeech,        // 增加吸血
    AddSplit,        // 子弹分裂

    // 纯数值类 (这些可以通过 "value" 字段来强化)
    ModifyDamage,    // 改变伤害
    ModifyFireRate,  // 改变射速
    ModifyScale,     // 改变体积/范围
    ModifySpeed      // 改变子弹速度
}