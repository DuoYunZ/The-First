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

    [Header("英文本地化")]
    [Tooltip("英文角色名称（留空则在英文模式下回退显示中文）")]
    public string characterNameEN;
    [Tooltip("英文描述（留空则在英文模式下回退显示中文）")]
    [TextArea(3, 5)]
    public string descriptionEN;

    [Header("战斗设置")]
    [Tooltip("这个角色在战斗中实际使用的机甲核心预制件")]
    public GameObject chassisPrefab;

    [Tooltip("这个角色的初始武器列表")]
    public List<WeaponStatBlock> initialWeapons;

    [Header("分支初始武器（可选）")]
    [Tooltip("当玩家选择了特定分支后，用此武器替换初始武器列表中的第一个武器")]
    public WeaponStatBlock alternateStartWeapon;

    [Tooltip("触发替换的机制分支ID（如 IcePath）。留空则不使用替换功能")]
    public string alternateStartMechanicID;

    [Tooltip("法师特性：初始武器自动解锁大招（无需5颗宝石）")]
    public bool autoUnlockInitialUltimate = false;
    [Tooltip("角色唯一标识，用于存档（如 Role01）")]
    public string characterID;
    [Tooltip("是否默认解锁（第一个角色应勾选）")]
    public bool isDefaultUnlocked = false;
    [Tooltip("解锁所需金币数（默认解锁的角色可设为0）")]
    public int unlockCost = 0;

    [Header("角色技能树")]
    [Tooltip("这个角色的所有技能树节点（按层级顺序排列）")]
    public List<CharacterSkillNode> characterSkillNodes;

    /// <summary>
    /// 获取本地化后的角色名称
    /// </summary>
    public string LocalizedName
    {
        get
        {
            if (LocalizationManager.CurrentLanguage == SystemLanguage.English
                && !string.IsNullOrEmpty(characterNameEN))
            {
                return characterNameEN;
            }
            return characterName;
        }
    }

    /// <summary>
    /// 获取本地化后的角色描述
    /// </summary>
    public string LocalizedDescription
    {
        get
        {
            if (LocalizationManager.CurrentLanguage == SystemLanguage.English
                && !string.IsNullOrEmpty(descriptionEN))
            {
                return descriptionEN;
            }
            return description;
        }
    }
}