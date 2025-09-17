using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillTree_", menuName = "Skill Tree/Weapon Skill Tree")]
public class WeaponSkillTree : ScriptableObject
{
    [Header("关联武器")]
    [Tooltip("这棵技能树属于哪一把武器")]
    public WeaponStatBlock associatedWeapon;

    [Header("技能节点")]
    [Tooltip("将这把武器的所有技能节点资产都拖到这里")]
    public List<WeaponUpgradeNode> allNodesInTree;
}