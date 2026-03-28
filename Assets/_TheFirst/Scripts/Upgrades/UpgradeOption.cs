using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class UpgradeOption
{
    [Header("选项信息")]
    [Tooltip("这个升级选项在卡片上显示的描述（中文）。例如：'地狱火：伤害+10%，范围+15%'")]
    [TextArea]
    public string description;

    [Tooltip("英文描述（可选）。留空则在英文模式下回退显示中文。")]
    [TextArea]
    public string descriptionEN;

    [Tooltip("这个选项的品质，将决定卡片的背景样式和出现概率")]
    public Rarity rarity;

    [Header("具体效果列表")]
    [Tooltip("当选择此项时，应用的所有具体效果")]
    public List<UpgradeEffect> effects;

    /// <summary>
    /// 获取本地化后的描述文本
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