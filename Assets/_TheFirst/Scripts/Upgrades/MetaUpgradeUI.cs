using UnityEngine;
using TMPro;

public class MetaUpgradeUI : MonoBehaviour
{
    [Header("全局信息")]
    public TextMeshProUGUI goldText;

    [Header("详情提示框 (Tooltip)")]
    public GameObject tooltipPanel; // 右侧或下方的详情面板
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDesc;
    public TextMeshProUGUI tooltipCost;
    public TextMeshProUGUI tooltipCurrentBonus;

    private void OnEnable()
    {
        // 面板打开时，刷新所有子按钮的状态
        RefreshAllButtons();
        RefreshGoldDisplay();

        // 默认隐藏提示框
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void RefreshGoldDisplay()
    {
        if (MetaUpgradeManager.Instance != null && goldText != null)
        {
            goldText.text = MetaUpgradeManager.Instance.GetPlayerGold().ToString();
        }
    }

    public void RefreshAllButtons()
    {
        // 找到所有子物体里的按钮并刷新它们
        var buttons = GetComponentsInChildren<MetaUpgradeNodeButton>();
        foreach (var btn in buttons)
        {
            btn.RefreshState();
        }
    }

    // --- 显示详情 (由按钮调用) ---
    public void ShowTooltip(MetaUpgradeSO data)
    {
        if (tooltipPanel == null) return;

        tooltipPanel.SetActive(true);

        int level = MetaUpgradeManager.Instance.GetLevel(data);
        int cost = data.GetCost(level);
        bool isMax = level >= data.maxLevel;

        if (tooltipName) tooltipName.text = data.upgradeName;

        // 描述生成
        float curBonus = data.GetTotalBonus(level);
        float nextBonus = data.GetTotalBonus(level + 1);

        string desc = data.description + "\n";
        if (isMax)
        {
            desc += $"<color=yellow>已满级 (加成: {curBonus:F1})</color>";
            if (tooltipCost) tooltipCost.text = "---";
        }
        else
        {
            desc += $"当前: {curBonus:F1} <color=green>-> 下一级: {nextBonus:F1}</color>";
            if (tooltipCost)
            {
                int playerGold = MetaUpgradeManager.Instance.GetPlayerGold();
                string color = playerGold >= cost ? "white" : "red";
                tooltipCost.text = $"费用: <color={color}>{cost}</color>";
            }
        }

        if (tooltipDesc) tooltipDesc.text = desc;
    }
}