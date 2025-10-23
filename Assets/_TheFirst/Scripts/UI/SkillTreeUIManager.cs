using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI; // 【在这里新增这一行】

public class SkillTreeUIManager : MonoBehaviour
{
    [Header("UI 预制件和容器")]
    public GameObject upgradeNodePrefab;
    public Transform nodesContainer;

    [Header("已解锁列表")]
    public GameObject unlockedSkillDisplayPrefab; // 左侧的
    public Transform unlockedSkillsContainer;
    public Color unlockedItemColor = Color.yellow;
    public Color lockedItemColor = Color.gray;

    [Header("其他UI")]
    public Button closeButton;

    private WeaponSkillTree currentTree;

    private List<UpgradeNodeUI> instantiatedNodes = new List<UpgradeNodeUI>(); // 用于存储所有UI实例

    void Awake()
    {
        // 确保 closeButton 已经正确赋值，否则这里依然会报错
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
        else
        {
            Debug.LogError("SkillTreeUIManager: closeButton 未在Inspector中设置！", this);
        }

        // 【核心修正】删除或注释掉下面这行代码
        // gameObject.SetActive(false); 
    }

    public void OpenPanel(WeaponSkillTree skillTree)
    {
        Debug.Log("--- 2. OpenPanel 方法被成功调用 ---"); // <-- 新增
        this.currentTree = skillTree;

        Debug.Log("--- 3. 准备执行 gameObject.SetActive(true) ---"); // <-- 新增
        gameObject.SetActive(true);

        Debug.Log($"--- 4. SetActive(true) 执行完毕。面板 IsActive: {gameObject.activeSelf}, IsActiveInHierarchy: {gameObject.activeInHierarchy}"); // <-- 新增
        Time.timeScale = 0f; // 打开UI时暂停游戏

        Debug.Log("--- 5. Time.timeScale 已设置为 0 ---"); // <-- 新增

        GenerateNodes();
        GenerateUnlockedSkillsList(); // 【新增】生成左侧列表
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; // 关闭UI时恢复游戏
    }

    private void GenerateNodes()
    {
        foreach (Transform child in nodesContainer)
        {
            Destroy(child.gameObject);
        }
        instantiatedNodes.Clear();

        // 【新增】安全检查，防止 currentTree 或其节点列表为空时报错
        if (currentTree == null || currentTree.allNodesInTree == null)
        {
            Debug.LogError("无法生成节点，因为当前的SkillTree或其节点列表为空！");
            return;
        }

        // ... (实例化节点的循环)
        foreach (WeaponUpgradeNode nodeData in currentTree.allNodesInTree)
        {
            GameObject nodeGO = Instantiate(upgradeNodePrefab, nodesContainer);
            UpgradeNodeUI nodeUI = nodeGO.GetComponent<UpgradeNodeUI>();
            nodeUI.Initialize(nodeData, this); // Initialize会调用一次UpdateNodeState
            instantiatedNodes.Add(nodeUI); // 将实例存入列表
        }
    }

    // 当一个节点UI被点击时，由 UpgradeNodeUI 脚本调用
    public void OnNodeSelected(UpgradeNodeUI selectedNodeUI, WeaponUpgradeNode selectedNodeData)
    {     

        if (selectedNodeUI.purchaseButton.interactable)
        {
            Debug.Log("<color=red>OnNodeSelected: Attempting to use PlayerProgressManager.Instance...</color>", this);
            Debug.Log(PlayerProgressManager.Instance, this); // 这会直接打印出Instance是"Null"还是一个对象

            // 可以在这里加一个确认购买的弹窗，但我们先直接购买

            // 1. 消费金币
            PlayerProgressManager.Instance.SpendGold(selectedNodeData.cost);

            // 2. 解锁节点 (这会自动应用节点效果)
            PlayerProgressManager.Instance.UnlockNode(selectedNodeData);

            // 3. 购买成功后，刷新所有节点的UI状态
            RefreshAllNodeStates();

            GenerateUnlockedSkillsList();
        }
    }

    public void RefreshAllNodeStates()
    {
        foreach (UpgradeNodeUI nodeUI in instantiatedNodes)
        {
            nodeUI.UpdateNodeState();
        }
    }
    private void GenerateUnlockedSkillsList()
    {
        // 1. 清理旧列表
        foreach (Transform child in unlockedSkillsContainer)
        {
            Destroy(child.gameObject);
        }

        if (currentTree == null || currentTree.allNodesInTree == null) return;

        // 2. 遍历整个技能树的所有节点
        foreach (WeaponUpgradeNode nodeData in currentTree.allNodesInTree)
        {
            // 3. 实例化显示项
            GameObject displayGO = Instantiate(unlockedSkillDisplayPrefab, unlockedSkillsContainer);
            Image backgroundImage = displayGO.GetComponent<Image>();
            TextMeshProUGUI descriptionText = displayGO.GetComponentInChildren<TextMeshProUGUI>();

            if (descriptionText != null)
            {
                descriptionText.text = nodeData.description;
            }

            // 4. 根据解锁状态设置颜色
            if (PlayerProgressManager.Instance.IsNodeUnlocked(nodeData))
            {
                if (backgroundImage != null) backgroundImage.color = unlockedItemColor;
                if (descriptionText != null) descriptionText.color = Color.white; // 或者其他亮色
            }
            else
            {
                if (backgroundImage != null) backgroundImage.color = lockedItemColor;
                if (descriptionText != null) descriptionText.color = Color.gray;
            }
        }
    }
}