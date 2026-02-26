using UnityEngine;

/// <summary>
/// 武器融合配方ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "FusionRecipe", menuName = "Weapons/FusionRecipe")]
public class WeaponFusionRecipeSO : ScriptableObject
{
    [Header("配方标识")]
    public string recipeId;
    public string recipeName;
    [TextArea]
    public string description;
    
    [Header("触发条件")]
    [Tooltip("经验满时触发检测的武器")]
    public WeaponStatBlock triggerWeapon;
    [Tooltip("触发武器需要的阶段")]
    public WeaponStage requiredStage = WeaponStage.Branched;
    
    [Header("组合条件")]
    [Tooltip("需要满足的所有条件")]
    public FusionCondition[] conditions;
    
    [Header("进化结果")]
    public FusionType fusionType;
    [Tooltip("融合后的武器")]
    public WeaponStatBlock resultWeapon;
    
    [Header("额外解锁")]
    [Tooltip("融合时解锁到卡池的武器ID")]
    public string[] unlockToPool;
    
    [Header("UI显示")]
    public Sprite cardIcon;
    public Color cardColor = Color.yellow;
}
