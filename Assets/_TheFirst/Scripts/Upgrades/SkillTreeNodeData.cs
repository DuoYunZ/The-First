using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSkillNode", menuName = "Game/Skill Tree Node")]
public class SkillTreeNodeData : ScriptableObject
{
    [Header("节点基础信息")]
    public string skillName;
    public Sprite skillIcon;

    [Header("关联武器")]
    [Tooltip("如果这是一个特定武器的升级，请在这里指定对应的武器数据。如果这是一个通用升级（如移速），请将此项留空(None)。")]
    public WeaponStatBlock associatedWeapon;

    [Header("技能树规则")]
    [Tooltip("前置节点：必须全部拥有后，该节点才能出现在候选池中")]
    public List<SkillTreeNodeData> prerequisites;

    [Tooltip("互斥节点：如果玩家已拥有列表中任一节点，该节点将不再出现")]
    public List<SkillTreeNodeData> mutuallyExclusive;

    [Tooltip("最大等级（技能树卡片通常为1，被动道具可为多级）")]
    public int maxLevel = 1;

    [Tooltip("是否为武器技能树节点（区别于通用被动升级）")]
    public bool isWeaponSkillTreeNode = false;

    [Tooltip("该节点是否仅允许获取一次（默认true，适用于技能树唯一节点）")]
    public bool isOneTimeOnly = true;

    [Header("【核心修改】升级选项池")]
    [Tooltip("当玩家升级此技能时，会从这个选项池中随机抽取一项，显示在卡片上")]
    public List<UpgradeOption> possibleOptions;

    [Header("编辑器可视化")]
    [HideInInspector] public Vector2 graphPosition; // 技能树编辑器中的节点位置
}