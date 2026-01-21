using UnityEngine;

[CreateAssetMenu(fileName = "NewFusionRecipe", menuName = "Weapons/Fusion Recipe")]
public class FusionRecipeSO : ScriptableObject
{
    [Header("融合配方")]
    [Tooltip("需要的第一个满级武器")]
    public WeaponStatBlock weaponA;

    [Tooltip("需要的第二个满级武器")]
    public WeaponStatBlock weaponB;

    [Header("融合结果")]
    [Tooltip("融合后诞生的超武数据")]
    public WeaponStatBlock resultWeapon;

    [Tooltip("融合时显示的描述文本")]
    [TextArea] public string description;

    [Tooltip("融合成功时的UI图标")]
    public Sprite fusionIcon;
}