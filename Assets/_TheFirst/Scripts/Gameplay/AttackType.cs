// AttackType.cs
public enum AttackType
{
    Standard,       // 标准攻击，造成普通伤害
    Reflectable,    // 可反弹的攻击，如激光
    Ignition,
    ShieldBreaking, // 可直接击破护盾的攻击，如冲撞
    Unblockable     // (未来扩展) 不可被护盾格挡的攻击
}