using UnityEngine;

/// <summary>
/// 关卡数据 ScriptableObject — 定义一个可选关卡的所有信息
/// 配合 LevelRegionButton 使用，每个区域Image挂一个 LevelRegionButton 引用一个 LevelData
/// </summary>
[CreateAssetMenu(fileName = "NewLevelData", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("关卡显示名称（中文）")]
    public string levelName;

    [Tooltip("关卡显示名称（英文，留空则回退中文）")]
    public string levelNameEN;

    [Tooltip("要加载的场景名称（必须与 Build Settings 中的名称一致）")]
    public string sceneName;

    [Header("显示设置")]
    [Tooltip("关卡图标/缩略图（用于确认弹窗中显示）")]
    public Sprite levelIcon;

    [Tooltip("关卡简介描述（中文）")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("关卡简介描述（英文）")]
    [TextArea(2, 4)]
    public string descriptionEN;

    [Tooltip("推荐等级（0 = 不显示）")]
    public int recommendedLevel = 0;

    [Header("解锁条件")]
    [Tooltip("是否默认解锁")]
    public bool isUnlockedByDefault = true;

    [Tooltip("解锁所需的通关关卡ID（留空则不需要前置关卡）")]
    public string requiredClearedLevelID;

    /// <summary>
    /// 获取本地化的关卡名称
    /// </summary>
    public string LocalizedName
    {
        get
        {
            if (LocalizationManager.CurrentLanguage == SystemLanguage.English
                && !string.IsNullOrEmpty(levelNameEN))
            {
                return levelNameEN;
            }
            return levelName;
        }
    }

    /// <summary>
    /// 获取本地化的关卡描述
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
