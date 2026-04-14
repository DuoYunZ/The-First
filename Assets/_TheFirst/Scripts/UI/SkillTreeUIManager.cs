using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class SkillTreeUIManager : MonoBehaviour
{
    [Header("全局数据")]
    [Tooltip("把游戏里所有的武器技能树配置都拖到这里 (火球, 飞刀, 燃烧瓶...)")]
    public List<WeaponSkillTree> allWeaponTrees;

    [Header("左侧列表组件 (Sidebar)")]
    public Transform sidebarContent;      // 左侧 ScrollView 的 Content 父节点
    public GameObject sidebarItemPrefab;  // 需要挂载 SkillTreeSidebarItem 脚本的预制体

    [Header("右侧 - 锁定状态界面 (Locked View)")]
    public GameObject lockedViewRoot;           // 锁定界面的父物体
    public TextMeshProUGUI lockConditionText;   // 显示解锁条件描述
    public Slider lockProgressBar;              // 解锁进度条
    public TextMeshProUGUI lockProgressText;    // 进度数值 "500/1000"
    public Image lockedWeaponIcon;              // (可选) 锁定界面显示的大图标

    [Header("右侧 - 已解锁界面 (Unlocked View)")]
    public GameObject unlockedViewRoot;         // 解锁界面的父物体
    public Transform hexNodesContainer;         // 放六边形技能节点的地方
    public GameObject upgradeNodePrefab;        // 六边形节点预制体

    [Header("右侧 - 已获技能描述列表")]
    public Transform skillDescriptionContainer; // 右侧放文字条目的 Content
    public GameObject skillDescriptionPrefab;   // 文字条目预制体 (Text 或包含 Text 的 Panel)
    public Color unlockedDescColor = Color.yellow;
    public Color lockedDescColor = Color.gray;

    [Header("通用 UI")]
    public Button closeButton;

    // --- 运行时状态 ---
    private WeaponSkillTree currentSelectedTree;
    private List<SkillTreeSidebarItem> sidebarItems = new List<SkillTreeSidebarItem>();
    private List<GameObject> activeHexNodes = new List<GameObject>();
    private List<GameObject> activeDescItems = new List<GameObject>();
    private List<SkillDescriptionItem> activeSkillItems = new List<SkillDescriptionItem>();

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    // --- 外部调用入口 ---
    public bool IsPanelOpen()
    {
        return gameObject.activeSelf;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // 暂停游戏

        // 1. 生成左侧导航栏
        GenerateSidebar();

        // 2. 默认逻辑：
        // 如果之前没有选中过，或者选中的树不在列表里，默认选第一个
        if (currentSelectedTree == null || !allWeaponTrees.Contains(currentSelectedTree))
        {
            if (allWeaponTrees.Count > 0)
            {
                SelectWeaponTree(allWeaponTrees[0]);
            }
        }
        else
        {
            // 如果有上次选中的，重新刷新一下它的显示（防止数据变了）
            SelectWeaponTree(currentSelectedTree);
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏
    }

    // =========================================================
    //  左侧导航栏逻辑 (Sidebar)
    // =========================================================

    private void GenerateSidebar()
    {
        // 1. 清理旧列表
        foreach (Transform child in sidebarContent) Destroy(child.gameObject);
        sidebarItems.Clear();

        // 2. 遍历所有武器树生成按钮
        foreach (var tree in allWeaponTrees)
        {
            if (tree == null) continue;

            GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
            SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();

            if (script != null)
            {
                bool isUnlocked = CheckIfUnlocked(tree);
                bool isSelected = (currentSelectedTree == tree);

                script.Setup(tree, this, isUnlocked, isSelected);
                sidebarItems.Add(script);
            }
        }
    }

    private void RefreshSidebarVisuals()
    {
        // 简单高效的做法：重新 Setup 所有 Item 的高亮状态
        foreach (var item in sidebarItems)
        {
            bool isSelected = (item.MyTreeData == currentSelectedTree);
            bool isUnlocked = CheckIfUnlocked(item.MyTreeData);
            item.Setup(item.MyTreeData, this, isUnlocked, isSelected);
        }
    }

    // =========================================================
    //  核心切换逻辑 (Switching Views)
    // =========================================================

    public void SelectWeaponTree(WeaponSkillTree tree)
    {
        currentSelectedTree = tree;

        // 1. 刷新左侧按钮的高亮
        RefreshSidebarVisuals();

        // 2. 根据解锁状态切换右侧界面
        if (CheckIfUnlocked(tree))
        {
            ShowUnlockedView(tree);
        }
        else
        {
            ShowLockedView(tree);
        }
    }
    private void GenerateUnlockedSkillsList()
    {
        // [调试] 打印日志看看有没有运行到这里
        // 1. 检查容器
        if (skillDescriptionContainer == null)
        {
            Debug.LogError("出错：Skill Description Container 没赋值！请在 Inspector 里拖拽 SkillDescriptionList 物体。");
            return;
        }

        // 2. 清理旧列表
        foreach (Transform child in skillDescriptionContainer)
        {
            Destroy(child.gameObject);
        }

        // 3. 检查数据
        if (currentSelectedTree == null)
        {
            Debug.LogWarning("出错：currentSelectedTree 是空的！");
            return;
        }
        if (currentSelectedTree.allNodesInTree == null || currentSelectedTree.allNodesInTree.Count == 0)
        {
            Debug.LogWarning($"注意：武器 {currentSelectedTree.name} 的 allNodesInTree 列表是空的，没东西可显示。");
            return;
        }

        activeSkillItems.Clear();

        foreach (WeaponUpgradeNode nodeData in currentSelectedTree.allNodesInTree)
        {
            GameObject displayGO = Instantiate(skillDescriptionPrefab, skillDescriptionContainer);
            SkillDescriptionItem itemScript = displayGO.GetComponent<SkillDescriptionItem>();

            if (itemScript != null)
            {
                // 【新增】加入列表
                activeSkillItems.Add(itemScript);

                itemScript.Setup(nodeData, () =>
                {
                    RefreshAllNodeStates();
                });
            }
        }

        // 【新增】生成完立马刷新一次，把该锁的锁住
        RefreshAllNodeStates();
    }
    private bool CheckIfUnlocked(WeaponSkillTree tree)
    {
        // 1. 默认解锁
        if (tree.isDefaultUnlocked) return true;

        if (PlayerProgressManager.Instance == null) return false;

        // 2. 检查“白名单” (unlockedItems)
        // 优先检查 ID (兼容多语言)
        string id = tree.associatedWeapon.weaponID;
        if (!string.IsNullOrEmpty(id) && PlayerProgressManager.Instance.unlockedItems.Contains(id)) return true;

        // 兼容检查 Name (防止旧存档是用名字存的)
        if (PlayerProgressManager.Instance.unlockedItems.Contains(tree.associatedWeapon.weaponName)) return true;

        // 3. 【核心修复】检查“成就进度”是否达标
        // 即使没有明确 Unlock，只要进度满了 (比如 1/1)，就视为解锁！
        if (!string.IsNullOrEmpty(tree.unlockStatKey))
        {
            if (PlayerProgressManager.Instance.achievementStats.ContainsKey(tree.unlockStatKey))
            {
                int currentVal = PlayerProgressManager.Instance.achievementStats[tree.unlockStatKey];

                // 如果当前进度 >= 目标阈值，放行！
                if (currentVal >= tree.unlockThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    //  视图 A: 锁定状态 (Locked View)
    // =========================================================

    private void ShowLockedView(WeaponSkillTree tree)
    {
        if (lockedViewRoot) lockedViewRoot.SetActive(true);
        if (unlockedViewRoot) unlockedViewRoot.SetActive(false);

        // 1. 设置描述文本 (需要在 WeaponSkillTree 中加 lockedDescription 字段)
        if (lockConditionText != null)
        {
            // 确保你已经在 SO 里填了字，不然这里会显示空
            lockConditionText.text = tree.lockedDescription;
        }

        // 2. 设置图标
        if (lockedWeaponIcon && tree.associatedWeapon != null)
        {
            lockedWeaponIcon.sprite = tree.associatedWeapon.weaponIcon;
        }

        // 3. 设置进度条
        int currentVal = 0;
        int targetVal = tree.unlockThreshold;

        if (PlayerProgressManager.Instance != null && !string.IsNullOrEmpty(tree.unlockStatKey))
        {
            if (PlayerProgressManager.Instance.achievementStats.ContainsKey(tree.unlockStatKey))
            {
                currentVal = PlayerProgressManager.Instance.achievementStats[tree.unlockStatKey];
            }
        }

        if (lockProgressBar != null)
        {
            lockProgressBar.maxValue = targetVal > 0 ? targetVal : 1; // 防止除以0
            lockProgressBar.value = currentVal;
        }
        if (lockProgressText != null)
        {
            lockProgressText.text = $"{currentVal} / {targetVal}";
        }
    }

    // =========================================================
    //  视图 B: 已解锁状态 (Unlocked View - Hex Nodes)
    // =========================================================

    private void ShowUnlockedView(WeaponSkillTree tree)
    {
        // 1. 切换界面显示
        if (lockedViewRoot) lockedViewRoot.SetActive(false);
        if (unlockedViewRoot) unlockedViewRoot.SetActive(true);

        // 2. 生成六边形 (如果你保留了的话)
        //GenerateNodes(tree);

        // 3. 【核心检查】这一行必须有！！而且名字要和你写的生成方法一致！
        // 建议统一改名为 GenerateUnlockedSkillsList
        GenerateUnlockedSkillsList();
    }

    private void GenerateNodes(WeaponSkillTree tree)
    {
        // 1. 清理旧节点
        foreach (var node in activeHexNodes) Destroy(node);
        activeHexNodes.Clear();

        if (tree == null || tree.allNodesInTree == null) return;

        // 【核心逻辑】追踪“上一个节点”是否已解锁
        // 第一个节点的前置默认视为已解锁，否则没人能买第一个
        bool isPreviousUnlocked = true;

        // 2. 遍历生成
        foreach (WeaponUpgradeNode nodeData in tree.allNodesInTree)
        {
            GameObject nodeGO = Instantiate(upgradeNodePrefab, hexNodesContainer);
            UpgradeNodeUI nodeUI = nodeGO.GetComponent<UpgradeNodeUI>();

            // 先初始化基础显示
            nodeUI.Initialize(nodeData, this);

            // 检查当前这个节点是否已购买
            bool isCurrentNodeUnlocked = PlayerProgressManager.Instance.IsNodeUnlocked(nodeData);

            if (isCurrentNodeUnlocked)
            {
                // 情况 A: 已经买过了
                // 保持原样 (Initialize里通常会处理已购买的绿色状态)
                // 它的下一个节点有资格被购买
                isPreviousUnlocked = true;
            }
            else
            {
                // 情况 B: 还没买
                if (isPreviousUnlocked)
                {
                    // 前一个买过了 -> 我是下一个待买的
                    // 允许点击，显示正常
                    if (nodeUI.purchaseButton) nodeUI.purchaseButton.interactable = true;

                    // 【关键】因为我还没买，所以我的下一个节点绝对不能买
                    isPreviousUnlocked = false;
                }
                else
                {
                    // 前一个都没买 -> 我处于“被锁住”状态
                    // 禁用按钮
                    if (nodeUI.purchaseButton) nodeUI.purchaseButton.interactable = false;

                    // 可选：在这里给图标加个灰色遮罩，或者把透明度调低，让它看起来像“不可用”
                    // var canvasGroup = nodeGO.GetComponent<CanvasGroup>();
                    // if(canvasGroup) canvasGroup.alpha = 0.5f;

                    // 传递锁定状态给下一个
                    isPreviousUnlocked = false;
                }
            }

            activeHexNodes.Add(nodeGO);
        }
    }

    private void GenerateSkillDescriptionList(WeaponSkillTree tree)
    {
        // 1. 清理旧描述
        foreach (var item in activeDescItems) Destroy(item);
        activeDescItems.Clear();

        if (skillDescriptionContainer == null || skillDescriptionPrefab == null) return;

        // 2. 生成描述条目
        foreach (WeaponUpgradeNode nodeData in tree.allNodesInTree)
        {
            GameObject itemGO = Instantiate(skillDescriptionPrefab, skillDescriptionContainer);
            TextMeshProUGUI textComp = itemGO.GetComponentInChildren<TextMeshProUGUI>();
            Image bg = itemGO.GetComponent<Image>();

            if (textComp != null)
            {
                textComp.text = nodeData.description;
            }

            // 根据该节点是否已购买，设置颜色
            bool isPurchased = PlayerProgressManager.Instance.IsNodeUnlocked(nodeData);

            if (textComp != null) textComp.color = isPurchased ? unlockedDescColor : lockedDescColor;
            if (bg != null) bg.color = isPurchased ? Color.white : new Color(1, 1, 1, 0.2f); // 简单的透明度变化

            activeDescItems.Add(itemGO);
        }
    }

    // =========================================================
    //  节点交互逻辑 (购买)
    // =========================================================

    public void OnNodeSelected(UpgradeNodeUI selectedNodeUI, WeaponUpgradeNode selectedNodeData)
    {
        // 这里的逻辑和你之前的一样，只要按钮可点击就代表钱够且前置已满足
        if (selectedNodeUI.purchaseButton.interactable)
        {
            // 1. 扣钱
            PlayerProgressManager.Instance.SpendGold(selectedNodeData.cost);

            // 2. 解锁
            PlayerProgressManager.Instance.UnlockNode(selectedNodeData);

            // 3. 刷新界面 (刷新所有节点的状态)
            RefreshAllNodeStates();

            // 4. 刷新右侧的描述列表颜色
            GenerateSkillDescriptionList(currentSelectedTree);
        }
    }

    public void RefreshAllNodeStates()
    {
        // 1. 链式顺序锁定逻辑
        bool isPreviousUnlocked = true; // 列表第一个默认允许

        // 【核心修改】遍历长条列表，而不是六边形
        foreach (SkillDescriptionItem item in activeSkillItems)
        {
            if (item == null || item.NodeData == null) continue;

            // 获取按钮和画布组 (假设你在 SkillDescriptionItem 里公开了它们)
            // 如果没公开，请去 SkillDescriptionItem 把 myButton 和 canvasGroup 改成 public
            Button btn = item.myButton;
            // 建议给预制体根物体加个 CanvasGroup 组件来控制半透明
            CanvasGroup cg = item.GetComponent<CanvasGroup>();

            // 检查当前节点是否已购买
            bool isCurrentUnlocked = PlayerProgressManager.Instance.IsNodeUnlocked(item.NodeData);

            if (isCurrentUnlocked)
            {
                // 自己解锁了 -> 下一个可以买
                isPreviousUnlocked = true;

                // 既然买了，按钮通常就禁用了 (或者显示已购买状态，这在 item.Setup 里应该处理过)
                if (cg) cg.alpha = 1f;
            }
            else
            {
                // 自己没解锁
                if (isPreviousUnlocked)
                {
                    // 前一个是通的 -> 我是“待买节点”
                    // 允许交互 (具体钱够不够，由 Item 自己的逻辑决定，这里只管顺序)
                    if (btn) btn.interactable = true; // 这里只是解开“顺序锁”，钱够不够的锁由 Item 自己管
                    if (cg) cg.alpha = 1f;

                    // 重新触发一次 Item 自身的状态刷新，确保金币判断正确
                    // (这需要你在 SkillDescriptionItem 里有个 public void RefreshState() 方法)
                    // item.RefreshState(); 

                    isPreviousUnlocked = false; // 链条中断
                }
                else
                {
                    // 前一个没通 -> 强制锁死
                    if (btn) btn.interactable = false;

                    // 【视觉反馈】变半透明，一眼看出被锁了
                    if (cg) cg.alpha = 0.4f;

                    isPreviousUnlocked = false;
                }
            }
        }
    }
}