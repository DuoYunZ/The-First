using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MetaUpgradeNodeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("数据绑定")]
    public MetaUpgradeSO upgradeData; // 【拖入】对应的 SO 文件 (比如 HP_Upgrade)

    [Header("UI 组件引用")]
    public Button myButton;
    public Image iconImage;

    // 如果你有等级文本显示在图标旁 (比如 "Lv.3")
    public TextMeshProUGUI levelText;

    // 引用主 UI (用于刷新提示框)
    private MetaUpgradeUI mainUI;

    void Start()
    {
        mainUI = GetComponentInParent<MetaUpgradeUI>();

        if (myButton == null) myButton = GetComponent<Button>();

        // 绑定点击事件
        myButton.onClick.AddListener(OnClick);

        // 初始化显示
        RefreshState();
    }

    public void RefreshState()
    {
        if (upgradeData == null) return;

        if (iconImage != null) iconImage.sprite = upgradeData.icon;

        int currentLevel = MetaUpgradeManager.Instance.GetLevel(upgradeData);
        bool isMax = currentLevel >= upgradeData.maxLevel;

        // 更新等级文本
        if (levelText != null) levelText.text = $"{currentLevel}/{upgradeData.maxLevel}";

        // 如果想根据解锁状态改变颜色 (比如未解锁变灰)
        // myButton.interactable = ... (如果有前置条件)
    }

    void OnClick()
    {
        // 尝试购买
        if (MetaUpgradeManager.Instance.TryPurchaseUpgrade(upgradeData))
        {
            // 购买成功，刷新自己和整个界面
            RefreshState();
            mainUI.RefreshGoldDisplay(); // 刷新金币文字
            mainUI.ShowTooltip(upgradeData); // 刷新提示框内容
        }
    }

    // --- 鼠标悬停显示信息 ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mainUI != null) mainUI.ShowTooltip(upgradeData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 可选：鼠标移开时隐藏提示，或者保持显示最后选中的
        // if (mainUI != null) mainUI.HideTooltip();
    }
}