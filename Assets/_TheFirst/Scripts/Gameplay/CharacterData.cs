using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("角色基本信息")]
    public string characterName;
    [TextArea(3, 5)]
    public string description;
    public Sprite characterIcon; // 用于UI选择界面
    public GameObject characterPreviewPrefab; // 用于UI选择界面的3D预览模型

    [Header("战斗设置")]
    [Tooltip("这个角色在战斗中实际使用的机甲核心预制件")]
    public GameObject chassisPrefab;

    [Tooltip("这个角色的初始武器列表")]
    public List<WeaponStatBlock> initialWeapons;

    [Header("解锁设置")]
    [Tooltip("角色唯一标识，用于存档（如 Role01）")]
    public string characterID;
    [Tooltip("是否默认解锁（第一个角色应勾选）")]
    public bool isDefaultUnlocked = false;
    [Tooltip("解锁所需金币数（默认解锁的角色可设为0）")]
    public int unlockCost = 0;

    [Header("角色技能树")]
    [Tooltip("这个角色的所有技能树节点（按层级顺序排列）")]
    public List<CharacterSkillNode> characterSkillNodes;
}