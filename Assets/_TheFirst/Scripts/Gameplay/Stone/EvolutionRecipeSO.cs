using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Recipe_FireStorm", menuName = "Weapons/Evolution Recipe")]
public class EvolutionRecipeSO : ScriptableObject
{
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

    [Header("进化描述")]
    [TextArea] public string description = "烈焰与燃油的终极融合。";
}