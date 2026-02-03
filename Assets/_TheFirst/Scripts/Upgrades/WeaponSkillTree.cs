using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillTree_", menuName = "Skill Tree/Weapon Skill Tree")]
public class WeaponSkillTree : ScriptableObject
{
    [Header("关联武器")]
    [Tooltip("这棵技能树属于哪一把武器")]
    public WeaponStatBlock associatedWeapon;

    [Header("解锁条件 (Unlock Condition)")]
    [Tooltip("默认是否已解锁 (初始武器勾选此项)")]
    public bool isDefaultUnlocked = false;

    [Tooltip("解锁所需的统计键值 (对应 PlayerProgressManager 中的 achievementStats Key)")]
    public string unlockStatKey = "Ignite_Count"; // 例如 "Ignite_Count"

    [Tooltip("解锁所需的目标数量")]
    public int unlockThreshold = 1000;

    [TextArea]
    [Tooltip("未解锁时显示的描述文本")]
    public string lockedDescription = "造成 1000 点燃烧伤害以解锁";

    [Header("技能节点")]
    [Tooltip("将这把武器的所有技能节点资产都拖到这里")]
    public List<WeaponUpgradeNode> allNodesInTree;
}