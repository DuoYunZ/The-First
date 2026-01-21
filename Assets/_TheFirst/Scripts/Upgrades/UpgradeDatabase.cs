using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Game/Upgrade Database")]
public class UpgradeDatabase : ScriptableObject
{
    [Header("通用被动技能 (移速/磁铁等) - 保持原样")]
    public List<SkillTreeNodeData> passiveUpgrades;

    [Header("武器升级链 (新系统)")]
    public List<WeaponUpgradeChainSO> weaponChains;
}