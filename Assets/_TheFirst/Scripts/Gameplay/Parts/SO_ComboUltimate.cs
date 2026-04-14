using UnityEngine;

/// <summary>
/// 连携技配置 - 定义两把武器满能量时可以释放的连携技
/// </summary>
[CreateAssetMenu(fileName = "ComboUltimate_New", menuName = "Weapons/Combo Ultimate")]
public class SO_ComboUltimate : ScriptableObject
{
    [Header("连携武器配置")]
    [Tooltip("参与连携的武器A")]
    public WeaponStatBlock weaponA;
    [Tooltip("参与连携的武器B")]
    public WeaponStatBlock weaponB;

    [Header("连携技信息")]
    [Tooltip("连携技名称")]
    public string comboName;
    [Tooltip("连携技图标")]
    public Sprite comboIcon;
    [Tooltip("连携技描述")]
    [TextArea(2, 4)]
    public string comboDescription;
    [Tooltip("连携技描述（英文）")]
    [TextArea(2, 4)]
    public string comboDescriptionEN;

    [Header("连携技效果")]
    [Tooltip("连携技效果预制件")]
    public GameObject comboEffectPrefab;
    [Tooltip("连携技伤害")]
    public int comboDamage = 200;
    [Tooltip("连携技范围")]
    public float comboRadius = 8f;

    /// <summary>
    /// 检查两把武器是否匹配此连携技（不分顺序）
    /// </summary>
    public bool MatchesWeapons(WeaponStatBlock a, WeaponStatBlock b)
    {
        if (a == null || b == null) return false;
        return (a == weaponA && b == weaponB) || (a == weaponB && b == weaponA);
    }
}
