using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class Debug_UpgradeStats : MonoBehaviour
{
    public static Debug_UpgradeStats Instance { get; private set; }

    public TextMeshProUGUI statsDisplayText; // 用于显示统计结果的UI文本

    private Dictionary<Rarity, int> rarityCounts = new Dictionary<Rarity, int>();
    private int totalCount = 0;
    private string lastUpgradeLogged = ""; // 【新增】用于存储上一条升级信息

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始化计数器
        rarityCounts[Rarity.Common] = 0;
        rarityCounts[Rarity.Uncommon] = 0;
        rarityCounts[Rarity.Rare] = 0;
        rarityCounts[Rarity.Epic] = 0;
    }

    /// <summary>
    /// 记录一次抽卡结果的品质
    /// </summary>
    public void LogRarity(Rarity rarity)
    {
        totalCount++;
        rarityCounts[rarity]++;
        UpdateDisplayText();
    }

    /// <summary>
    /// 【新增】记录并临时显示上一次获得的具体升级描述
    /// </summary>
    public void SetLastUpgradeMessage(string message)
    {
        lastUpgradeLogged = message;
        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        if (statsDisplayText == null) return;

        float commonPct = (totalCount > 0) ? (float)rarityCounts[Rarity.Common] / totalCount * 100 : 0;
        float uncommonPct = (totalCount > 0) ? (float)rarityCounts[Rarity.Uncommon] / totalCount * 100 : 0;
        float rarePct = (totalCount > 0) ? (float)rarityCounts[Rarity.Rare] / totalCount * 100 : 0;
        float epicPct = (totalCount > 0) ? (float)rarityCounts[Rarity.Epic] / totalCount * 100 : 0;

        // 【修改】在显示内容中加入“最近获得”
        statsDisplayText.text = "<b>--- 最近获得 ---</b>\n" +
                                $"{lastUpgradeLogged}\n\n" +
                                "<b>--- 升级品质概率统计 (总计: " + totalCount + ") ---</b>\n" +
                                $"<color=white>普通 (白): {rarityCounts[Rarity.Common]}次 ({commonPct:F1}%)</color>\n" +
                                $"<color=blue>稀有 (蓝): {rarityCounts[Rarity.Uncommon]}次 ({uncommonPct:F1}%)</color>\n" +
                                $"<color=purple>史诗 (紫): {rarityCounts[Rarity.Rare]}次 ({rarePct:F1}%)</color>\n" +
                                $"<color=orange>传说 (橙): {rarityCounts[Rarity.Epic]}次 ({epicPct:F1}%)</color>";
    }
}