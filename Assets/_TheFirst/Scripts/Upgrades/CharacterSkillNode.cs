using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色技能树节点 — 用于角色局外永久属性提升
/// 每个角色拥有多个节点，按前置关系线性解锁
/// </summary>
[CreateAssetMenu(fileName = "NewCharSkillNode", menuName = "Game/Character Skill Node")]
public class CharacterSkillNode : ScriptableObject
{
    [Header("节点信息")]
    [Tooltip("节点显示名称（如：攻击力 I）")]
    public string nodeName;

    [TextArea(2, 4)]
    [Tooltip("节点描述（如：攻击力 +5%）")]
    public string description;

    public Sprite icon;

    [Header("层级与费用")]
    [Tooltip("所在层级（1=基础层, 2=进阶层, 3=精通层, 4=天赋层）")]
    [Range(1, 4)]
    public int layer = 1;

    [Tooltip("解锁所需金币")]
    public int cost = 50;

    [Header("前置节点")]
    [Tooltip("必须先解锁这些节点才能解锁当前节点（留空 = 无前置，可直接解锁）")]
    public List<CharacterSkillNode> prerequisites;

    [Header("升级效果")]
    [Tooltip("解锁此节点后获得的永久效果")]
    public List<PermanentUpgradeEffect> effects;
}
