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
    [Tooltip("触发武器需要达到的最低等级。0表示沿用阶段/满级规则。")]
    public int requiredWeaponLevel = 5;
    
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

    [Header("图鉴显示")]
    [Tooltip("仅用于图鉴展示的设计位，不会在局内触发实际进化。")]
    public bool codexOnly = false;
    [Tooltip("主武器达到该等级后，图鉴中把黑色未知图标翻成彩色。")]
    public int codexRevealWeaponLevel = 5;
    [Tooltip("未显色前隐藏结果名称，显示为未知进化。")]
    public bool hideResultUntilRevealed = true;
}
