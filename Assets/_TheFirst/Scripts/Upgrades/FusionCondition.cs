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
    
    [Header("被动/天赋条件")]
    [Tooltip("需要的被动道具ID")]
    public string requiredPassiveId;
    [Tooltip("需要的天赋ID")]
    public string requiredTalentId;
}
