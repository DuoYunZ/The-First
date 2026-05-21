using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Game/Upgrade Database")]
public class UpgradeDatabase : ScriptableObject
{
    [Header("通用被动技能 (移速/磁铁等) - 保持原样")]
    public List<SkillTreeNodeData> passiveUpgrades;

    [Header("武器升级链 (解锁用)")]
    public List<WeaponUpgradeChainSO> weaponChains;

    [Header("武器技能树节点 (新系统)")]
    [Tooltip("按武器分组的技能树节点。武器经验升级时，会从这里筛选该武器可用的卡片。")]
    public List<SkillTreeNodeData> weaponSkillNodes;

    [Header("流派终局奖励")]
    [Tooltip("机械共鸣达到终局条件时自动授予的武器。")]
    public WeaponStatBlock mechanicalCapstoneWeapon;
}
