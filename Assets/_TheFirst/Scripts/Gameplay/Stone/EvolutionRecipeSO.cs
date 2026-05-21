using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Recipe_FireStorm", menuName = "Weapons/Evolution Recipe")]
public class EvolutionRecipeSO : ScriptableObject
{
    [Header("旧能量石进化配方兼容")]
    [Tooltip("核心主武器。旧资产字段名为 baseWeapon。")]
    public WeaponStatBlock baseWeapon;
    [Tooltip("需要的能量石元素。旧资产字段名为 requiredStoneType。")]
    public EnergyStoneEffectType requiredStoneType;
    [Tooltip("进化后的武器。旧资产字段名为 evolvedWeapon。")]
    public WeaponStatBlock evolvedWeapon;
    [Tooltip("进化显示名。旧资产字段名为 evolutionName。")]
    public string evolutionName;

    [Header("进化结果 (终极技能)")]
    public WeaponStatBlock resultWeapon; // 进化后的武器 (烈焰风暴)

    [Header("合成条件")]
    [Tooltip("核心主武器 (例如火球术)")]
    public WeaponStatBlock mainWeapon;
    [Tooltip("主武器需要的等级")]
    public int mainWeaponLevelReq = 10;

    [Tooltip("辅助武器/被动 (例如燃烧瓶)")]
    public WeaponStatBlock subItem;
    [Tooltip("辅助武器需要的等级")]
    public int subItemLevelReq = 5;

    [Tooltip("特殊道具 (例如火焰之心 - 宝箱掉落)")]
    public PassiveItemSO specialItem; // 假设你有一个针对被动/特殊道具的SO

    [Header("图鉴/解锁")]
    [Tooltip("主武器需要达到的等级。默认5级用于图鉴追求和进化门槛。")]
    public int requiredWeaponLevel = 5;
    [Tooltip("主武器达到该等级后，图鉴中把黑色未知图标翻成彩色。")]
    public int codexRevealWeaponLevel = 5;
    public Sprite cardIcon;
    public Color cardColor = Color.cyan;

    [Header("进化描述")]
    [TextArea] public string description = "烈焰与燃油的终极融合。";

    public WeaponStatBlock MainWeapon => mainWeapon != null ? mainWeapon : baseWeapon;
    public WeaponStatBlock ResultWeapon => resultWeapon != null ? resultWeapon : evolvedWeapon;
    public string DisplayName => !string.IsNullOrEmpty(evolutionName)
        ? evolutionName
        : ResultWeapon != null && !string.IsNullOrEmpty(ResultWeapon.weaponName)
            ? ResultWeapon.weaponName
            : name;
}
