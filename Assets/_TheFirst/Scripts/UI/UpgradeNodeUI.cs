using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeNodeUI : MonoBehaviour
{
    [Header("UI 引用")]
    public Image iconImage;
    public Image backgroundImage;
    public Button purchaseButton;

    public TextMeshProUGUI nodeNameText;
    public TextMeshProUGUI nodeCostText;

    [Header("状态颜色")]
    public Color lockedColor = Color.gray;
    public Color unlockedColor = Color.yellow;
    public Color canPurchaseColor = Color.white;

    // 内部数据
    public WeaponUpgradeNode nodeData { get; private set; }
    private SkillTreeUIManager uiManager;

    public void Initialize(WeaponUpgradeNode data, SkillTreeUIManager manager)
    {
        this.nodeData = data;
        this.uiManager = manager;

        iconImage.sprite = nodeData.icon;

        if (nodeNameText != null) nodeNameText.text = nodeData.upgradeName;
        if (nodeCostText != null) nodeCostText.text = nodeData.cost.ToString();
        if (iconImage != null) iconImage.sprite = data.icon;

        // 绑定按钮点击事件
        purchaseButton.onClick.AddListener(OnNodeClicked);

        // 之后我们会在这里更新节点的视觉状态（锁定、可购买等）
        UpdateNodeState();
    }

    public void UpdateNodeState()
    {
        if (nodeData == null || PlayerProgressManager.Instance == null) return;

        // 检查1：该节点是否已经被解锁
        if (PlayerProgressManager.Instance.IsNodeUnlocked(nodeData))
        {
            backgroundImage.color = unlockedColor;
            purchaseButton.interactable = false; // 已解锁的不能再购买
            return; // 结束逻辑
        }

        // 检查2：前置节点是否已解锁
        // (如果前置节点为null，说明是初始节点，默认视为已解锁)
        bool prerequisiteUnlocked = nodeData.prerequisiteNode == null || PlayerProgressManager.Instance.IsNodeUnlocked(nodeData.prerequisiteNode);

        if (prerequisiteUnlocked)
        {
            // 检查3：金币是否足够
            if (PlayerProgressManager.Instance.CanAfford(nodeData.cost))
            {
                backgroundImage.color = canPurchaseColor;
                purchaseButton.interactable = true; // 前置已解，金币足够，可以购买
            }
            else
            {
                backgroundImage.color = canPurchaseColor; // 颜色一样，但按钮不可交互
                purchaseButton.interactable = false; // 金币不足
            }
        }
        else
        {
            // 前置节点未解锁
            backgroundImage.color = lockedColor;
            purchaseButton.interactable = false;
        }
    }

    private void OnNodeClicked()
    {
        // 当节点被点击时，通知UI管理器
        if (uiManager != null)
        {
            uiManager.OnNodeSelected(this, nodeData);
        }
    }
}