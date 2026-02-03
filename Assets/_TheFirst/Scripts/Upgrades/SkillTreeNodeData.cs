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
    public List<SkillTreeNodeData> prerequisites;
    public int maxLevel = 1;

    [Header("【核心修改】升级选项池")]
    [Tooltip("当玩家升级此技能时，会从这个选项池中随机抽取一项，显示在卡片上")]
    public List<UpgradeOption> possibleOptions;
}