using UnityEngine;

/// <summary>
/// 融合条件配置
/// </summary>
[System.Serializable]
public class FusionCondition
{
    [Header("条件类型")]
    public ConditionType type;
    
    [Header("武器条件")]
    [Tooltip("需要拥有的武器")]
    public WeaponStatBlock requiredWeapon;
    [Tooltip("武器需要达到的阶段")]
    public WeaponStage requiredWeaponStage = WeaponStage.Base;
    [Tooltip("武器需要达到的最低等级。0表示只检查阶段。")]
    public int requiredWeaponLevel = 5;
    
    [Header("被动/天赋条件")]
    [Tooltip("需要的被动道具资源。优先使用该引用；为空时回退到 requiredPassiveId。")]
    public PassiveItemData requiredPassiveItem;
    [Tooltip("需要的被动道具ID")]
    public string requiredPassiveId;
    [Tooltip("Required passive item level. 1 means owning the passive is enough.")]
    public int requiredPassiveLevel = PassiveItemData.PassiveCapstoneLevel;
    [Tooltip("需要的天赋ID")]
    public string requiredTalentId;
}
