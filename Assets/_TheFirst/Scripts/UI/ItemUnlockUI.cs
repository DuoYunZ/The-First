using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemUnlockUI : MonoBehaviour
{
    [Header("目标物品")]
    public string targetItemName = "Molotov"; // 物品ID
    public string statKey = "Ignite_Count"; // 监听的统计数据
    public int requiredCount = 100; // 目标数量

    [Header("UI组件")]
    public GameObject lockedPanel;   // 没解锁时显示
    public GameObject unlockedPanel; // 解锁后显示
    public Slider progressSlider;
    public TextMeshProUGUI progressText; // 显示 "50/100"

    void OnEnable() // 每次打开界面时刷新
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (PlayerProgressManager.Instance == null) return;

        // 1. 检查是否已解锁
        bool isUnlocked = PlayerProgressManager.Instance.unlockedItems.Contains(targetItemName);

        if (isUnlocked)
        {
            if (lockedPanel) lockedPanel.SetActive(false);
            if (unlockedPanel) unlockedPanel.SetActive(true);
        }
        else
        {
            if (lockedPanel) lockedPanel.SetActive(true);
            if (unlockedPanel) unlockedPanel.SetActive(false);

            // 2. 更新进度条
            int current = PlayerProgressManager.Instance.GetStat(statKey);

            if (progressSlider)
            {
                progressSlider.maxValue = requiredCount;
                progressSlider.value = current;
            }
            if (progressText)
            {
                progressText.text = $"{current} / {requiredCount}";
            }
        }
    }
}