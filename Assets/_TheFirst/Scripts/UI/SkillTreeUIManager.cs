using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 图鉴系统 UI 管理器 (由原 SkillTree 改造而来)
/// 左侧展示武器 + 被动道具图标列表
/// 右侧根据条目类型显示不同内容：
///   - 武器(已解锁): 图标 + 名称 + 属性面板
///   - 被动道具(已解锁): 图标 + 名称 + 描述文本
///   - 任意(未解锁): 解锁条件 + 进度条
/// </summary>
public class SkillTreeUIManager : MonoBehaviour
{
    // =========================================================
    //  数据引用
    // =========================================================

    [Header("武器数据")]
    [Tooltip("把游戏里所有的武器技能树配置都拖到这里")]
    public List<WeaponSkillTree> allWeaponTrees;

    [Header("被动道具数据")]
    [Tooltip("把游戏里所有的被动道具配置都拖到这里")]
    public List<PassiveItemData> allPassiveItems;

    // =========================================================
    //  左侧列表 (Sidebar)
    // =========================================================

    [Header("左侧列表组件 (Sidebar)")]
    public Transform sidebarContent;      // 左侧 ScrollView 的 Content 父节点
    public GameObject sidebarItemPrefab;  // 需要挂载 SkillTreeSidebarItem 脚本的预制体

    // =========================================================
    //  右侧 - 锁定状态界面
    // =========================================================

    [Header("右侧 - 锁定状态界面 (Locked View)")]
    public GameObject lockedViewRoot;           // 锁定界面的父物体
    public TextMeshProUGUI lockConditionText;   // 显示解锁条件描述
    public Slider lockProgressBar;              // 解锁进度条
    public TextMeshProUGUI lockProgressText;    // 进度数值 "500/1000"
    public Image lockedWeaponIcon;              // 锁定界面显示的大图标

    // =========================================================
    //  右侧 - 武器属性展示视图
    // =========================================================

    [Header("右侧 - 武器属性展示 (Weapon Stats View)")]
    public GameObject weaponStatsViewRoot;         // 武器属性界面的父物体
    public Image weaponStatsIcon;                  // 大图标
    public TextMeshProUGUI weaponStatsName;        // 武器名称
    public Transform weaponStatsContainer;         // 放属性条目的容器
    public GameObject weaponStatItemPrefab;        // 单个属性条目预制体 (挂 CodexStatSlot)

    [Header("武器属性图标 (拖入对应 Sprite)")]
    [Tooltip("伤害图标")]
    public Sprite iconDamage;
    [Tooltip("射速/冷却图标")]
    public Sprite iconFireRate;
    [Tooltip("范围图标")]
    public Sprite iconRange;
    [Tooltip("弹数图标")]
    public Sprite iconProjectile;

    // =========================================================
    //  右侧 - 被动道具描述视图
    // =========================================================

    [Header("右侧 - 被动道具描述 (Passive Description View)")]
    public GameObject passiveDescViewRoot;          // 被动描述界面的父物体
    public Image passiveDescIcon;                   // 大图标
    public TextMeshProUGUI passiveDescName;         // 道具名称
    public TextMeshProUGUI passiveDescText;         // 详细描述文本

    // =========================================================
    //  通用 UI
    // =========================================================

    [Header("通用 UI")]
    public Button closeButton;

    // =========================================================
    //  【已关闭】原技能树升级相关 (保留引用以便日后恢复)
    // =========================================================

    [Header("【已关闭】原升级系统引用 (暂不使用)")]
    public GameObject unlockedViewRoot;            // 原解锁界面的父物体 (暂时不用)
    public Transform hexNodesContainer;
    public GameObject upgradeNodePrefab;
    public Transform skillDescriptionContainer;
    public GameObject skillDescriptionPrefab;

    // =========================================================
    //  运行时状态
    // =========================================================

    /// <summary>
    /// 当前选中的条目类型
    /// </summary>
    private enum SelectedEntryType { None, Weapon, Passive }
    private SelectedEntryType currentEntryType = SelectedEntryType.None;

    private WeaponSkillTree currentSelectedTree;
    private PassiveItemData currentSelectedPassive;
    private List<SkillTreeSidebarItem> sidebarItems = new List<SkillTreeSidebarItem>();
    private List<GameObject> activeStatSlots = new List<GameObject>();

    // =========================================================
    //  生命周期
    // =========================================================

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    // =========================================================
    //  外部调用入口
    // =========================================================

    public bool IsPanelOpen()
    {
        return gameObject.activeSelf;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // 暂停游戏

        // 1. 生成左侧导航栏（武器 + 被动道具）
        GenerateSidebar();

        // 2. 默认选中逻辑
        if (currentEntryType == SelectedEntryType.Weapon && currentSelectedTree != null
            && allWeaponTrees.Contains(currentSelectedTree))
        {
            // 恢复上次选中的武器
            SelectWeaponEntry(currentSelectedTree);
        }
        else if (currentEntryType == SelectedEntryType.Passive && currentSelectedPassive != null
            && allPassiveItems.Contains(currentSelectedPassive))
        {
            // 恢复上次选中的被动道具
            SelectPassiveEntry(currentSelectedPassive);
        }
        else
        {
            // 默认选第一个武器
            if (allWeaponTrees.Count > 0)
            {
                SelectWeaponEntry(allWeaponTrees[0]);
            }
            else if (allPassiveItems != null && allPassiveItems.Count > 0)
            {
                SelectPassiveEntry(allPassiveItems[0]);
            }
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏
    }

    // =========================================================
    //  左侧导航栏 (Sidebar)
    // =========================================================

    private void GenerateSidebar()
    {
        // 1. 清理旧列表
        foreach (Transform child in sidebarContent) Destroy(child.gameObject);
        sidebarItems.Clear();

        // 2. 生成武器条目
        foreach (var tree in allWeaponTrees)
        {
            if (tree == null) continue;
            CreateSidebarItem_Weapon(tree);
        }

        // 3. 生成被动道具条目
        if (allPassiveItems != null)
        {
            foreach (var passive in allPassiveItems)
            {
                if (passive == null) continue;
                CreateSidebarItem_Passive(passive);
            }
        }
    }

    /// <summary>
    /// 创建武器类型的侧边栏条目
    /// </summary>
    private void CreateSidebarItem_Weapon(WeaponSkillTree tree)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();

        if (script != null)
        {
            bool isUnlocked = CheckWeaponUnlocked(tree);
            bool isSelected = (currentEntryType == SelectedEntryType.Weapon && currentSelectedTree == tree);
            script.Setup(tree, this, isUnlocked, isSelected);
            sidebarItems.Add(script);
        }
    }

    /// <summary>
    /// 创建被动道具类型的侧边栏条目
    /// </summary>
    private void CreateSidebarItem_Passive(PassiveItemData passive)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();

        if (script != null)
        {
            bool isUnlocked = CheckPassiveUnlocked(passive);
            bool isSelected = (currentEntryType == SelectedEntryType.Passive && currentSelectedPassive == passive);
            script.Setup(passive, this, isUnlocked, isSelected);
            sidebarItems.Add(script);
        }
    }

    /// <summary>
    /// 刷新左侧所有条目的视觉状态（高亮、解锁）
    /// </summary>
    private void RefreshSidebarVisuals()
    {
        foreach (var item in sidebarItems)
        {
            if (item == null) continue;

            switch (item.EntryType)
            {
                case SkillTreeSidebarItem.CodexEntryType.Weapon:
                    {
                        bool isSelected = (currentEntryType == SelectedEntryType.Weapon
                                           && item.MyTreeData == currentSelectedTree);
                        bool isUnlocked = CheckWeaponUnlocked(item.MyTreeData);
                        item.Setup(item.MyTreeData, this, isUnlocked, isSelected);
                    }
                    break;

                case SkillTreeSidebarItem.CodexEntryType.Passive:
                    {
                        bool isSelected = (currentEntryType == SelectedEntryType.Passive
                                           && item.MyPassiveData == currentSelectedPassive);
                        bool isUnlocked = CheckPassiveUnlocked(item.MyPassiveData);
                        item.Setup(item.MyPassiveData, this, isUnlocked, isSelected);
                    }
                    break;
            }
        }
    }

    // =========================================================
    //  核心选择逻辑
    // =========================================================

    /// <summary>
    /// 选中一个武器条目（由侧边栏点击调用）
    /// </summary>
    public void SelectWeaponEntry(WeaponSkillTree tree)
    {
        currentEntryType = SelectedEntryType.Weapon;
        currentSelectedTree = tree;
        currentSelectedPassive = null;

        // 刷新左侧高亮
        RefreshSidebarVisuals();

        // 根据解锁状态切换右侧视图
        if (CheckWeaponUnlocked(tree))
        {
            ShowWeaponStatsView(tree);
        }
        else
        {
            ShowLockedView_Weapon(tree);
        }
    }

    /// <summary>
    /// 选中一个被动道具条目（由侧边栏点击调用）
    /// </summary>
    public void SelectPassiveEntry(PassiveItemData passive)
    {
        currentEntryType = SelectedEntryType.Passive;
        currentSelectedPassive = passive;
        currentSelectedTree = null;

        // 刷新左侧高亮
        RefreshSidebarVisuals();

        // 根据解锁状态切换右侧视图
        if (CheckPassiveUnlocked(passive))
        {
            ShowPassiveDescView(passive);
        }
        else
        {
            ShowLockedView_Passive(passive);
        }
    }

    // =========================================================
    //  解锁检查
    // =========================================================

    /// <summary>
    /// 检查武器是否已解锁（复用原有逻辑）
    /// </summary>
    private bool CheckWeaponUnlocked(WeaponSkillTree tree)
    {
        // 1. 默认解锁
        if (tree.isDefaultUnlocked) return true;

        if (PlayerProgressManager.Instance == null) return false;

        // 2. 检查白名单 (unlockedItems)
        string id = tree.associatedWeapon.weaponID;
        if (!string.IsNullOrEmpty(id) && PlayerProgressManager.Instance.unlockedItems.Contains(id)) return true;

        // 兼容检查 Name
        if (PlayerProgressManager.Instance.unlockedItems.Contains(tree.associatedWeapon.weaponName)) return true;

        // 3. 检查成就进度
        if (!string.IsNullOrEmpty(tree.unlockStatKey))
        {
            if (PlayerProgressManager.Instance.achievementStats.ContainsKey(tree.unlockStatKey))
            {
                int currentVal = PlayerProgressManager.Instance.achievementStats[tree.unlockStatKey];
                if (currentVal >= tree.unlockThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 检查被动道具是否已解锁
    /// </summary>
    private bool CheckPassiveUnlocked(PassiveItemData item)
    {
        if (item == null) return false;

        // 1. 默认解锁
        if (item.isDefaultUnlocked) return true;

        if (PlayerProgressManager.Instance == null) return false;

        // 2. 检查白名单 (用 SO 的 name 作为 ID)
        string id = item.name;
        if (PlayerProgressManager.Instance.unlockedItems.Contains(id)) return true;

        // 也检查 itemName (兼容)
        if (!string.IsNullOrEmpty(item.itemName)
            && PlayerProgressManager.Instance.unlockedItems.Contains(item.itemName)) return true;

        // 3. 检查成就统计
        if (!string.IsNullOrEmpty(item.unlockStatKey))
        {
            if (PlayerProgressManager.Instance.achievementStats.ContainsKey(item.unlockStatKey))
            {
                int currentVal = PlayerProgressManager.Instance.achievementStats[item.unlockStatKey];
                if (currentVal >= item.unlockThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    //  右侧视图切换辅助
    // =========================================================

    /// <summary>
    /// 隐藏所有右侧视图
    /// </summary>
    private void HideAllViews()
    {
        if (lockedViewRoot) lockedViewRoot.SetActive(false);
        if (weaponStatsViewRoot) weaponStatsViewRoot.SetActive(false);
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(false);

        // 同时隐藏旧的升级视图 (已关闭的功能)
        if (unlockedViewRoot) unlockedViewRoot.SetActive(false);

        // 【修复】切换视图时彻底清除武器属性条目，防止残留重叠
        ClearStatSlots();

        // 【修复】主动隐藏武器大图标和名称，防止非武器视图切换时残留
        if (weaponStatsIcon) weaponStatsIcon.gameObject.SetActive(false);
        if (weaponStatsName) weaponStatsName.gameObject.SetActive(false);
    }

    // =========================================================
    //  视图 A: 武器属性展示 (已解锁的武器)
    // =========================================================

    private void ShowWeaponStatsView(WeaponSkillTree tree)
    {
        HideAllViews();
        if (weaponStatsViewRoot) weaponStatsViewRoot.SetActive(true);

        var weapon = tree.associatedWeapon;
        if (weapon == null) return;

        // 设置大图标和名称（先激活再赋值）
        if (weaponStatsIcon != null)
        {
            weaponStatsIcon.gameObject.SetActive(true);
            weaponStatsIcon.sprite = weapon.weaponIcon;
        }
        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            // 使用本地化名称
            string localizedName = LanguageTable.LocalizeWeaponName(weapon.weaponName, LocalizationManager.CurrentLanguage);
            weaponStatsName.text = localizedName;
        }

        // 清除旧属性条目
        ClearStatSlots();

        // 生成4个核心属性条目
        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            // 1. 伤害 — 取直击伤害和范围伤害中较大的那个
            int damage = Mathf.Max(weapon.baseDirectDamage, weapon.baseAoeDamage);
            CreateStatSlot(iconDamage, damage.ToString());

            // 2. 射速 — 转换为冷却时间显示
            float cooldown = weapon.baseFireRate > 0 ? (1f / weapon.baseFireRate) : 0f;
            CreateStatSlot(iconFireRate, cooldown.ToString("F1") + "S");

            // 3. 范围
            float range = weapon.baseAoeRadius;
            CreateStatSlot(iconRange, range.ToString("F0") + "M");

            // 4. 穿透 — 基础穿透次数
            int pierce = weapon.basePierceCount;
            CreateStatSlot(iconProjectile, pierce.ToString());
        }
    }

    /// <summary>
    /// 创建单个属性条目
    /// </summary>
    private void CreateStatSlot(Sprite icon, string value)
    {
        GameObject slotObj = Instantiate(weaponStatItemPrefab, weaponStatsContainer);
        CodexStatSlot slot = slotObj.GetComponent<CodexStatSlot>();
        if (slot != null)
        {
            slot.Setup(icon, value);
        }
        activeStatSlots.Add(slotObj);
    }

    /// <summary>
    /// 清除所有属性条目
    /// </summary>
    private void ClearStatSlots()
    {
        foreach (var slot in activeStatSlots)
        {
            if (slot != null) Destroy(slot);
        }
        activeStatSlots.Clear();

        // 同时清除容器中的残留子物体
        if (weaponStatsContainer != null)
        {
            foreach (Transform child in weaponStatsContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    // =========================================================
    //  视图 B: 被动道具描述 (已解锁的被动道具)
    // =========================================================

    private void ShowPassiveDescView(PassiveItemData passive)
    {
        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);

        if (passive == null) return;

        // 设置大图标
        if (passiveDescIcon != null)
        {
            passiveDescIcon.sprite = passive.icon;
            passiveDescIcon.enabled = (passive.icon != null);
        }

        // 设置名称
        if (passiveDescName != null)
        {
            passiveDescName.text = passive.itemName;
        }

        // 设置描述文本
        if (passiveDescText != null)
        {
            passiveDescText.text = passive.description;
        }
    }

    // =========================================================
    //  视图 C: 锁定状态 (武器)
    // =========================================================

    private void ShowLockedView_Weapon(WeaponSkillTree tree)
    {
        HideAllViews();
        if (lockedViewRoot) lockedViewRoot.SetActive(true);

        // 解锁条件描述
        if (lockConditionText != null)
        {
            lockConditionText.text = tree.lockedDescription;
        }

        // 图标 (显示为锁定状态的武器图标)
        if (lockedWeaponIcon && tree.associatedWeapon != null)
        {
            lockedWeaponIcon.sprite = tree.associatedWeapon.weaponIcon;
        }

        // 进度条
        UpdateProgressBar(tree.unlockStatKey, tree.unlockThreshold);
    }

    // =========================================================
    //  视图 D: 锁定状态 (被动道具)
    // =========================================================

    private void ShowLockedView_Passive(PassiveItemData passive)
    {
        HideAllViews();
        if (lockedViewRoot) lockedViewRoot.SetActive(true);

        // 解锁条件描述
        if (lockConditionText != null)
        {
            lockConditionText.text = passive.lockedDescription;
        }

        // 图标
        if (lockedWeaponIcon && passive.icon != null)
        {
            lockedWeaponIcon.sprite = passive.icon;
        }

        // 进度条
        UpdateProgressBar(passive.unlockStatKey, passive.unlockThreshold);
    }

    /// <summary>
    /// 更新进度条显示（武器和被动道具通用）
    /// </summary>
    private void UpdateProgressBar(string statKey, int threshold)
    {
        int currentVal = 0;
        int targetVal = threshold;

        if (PlayerProgressManager.Instance != null && !string.IsNullOrEmpty(statKey))
        {
            if (PlayerProgressManager.Instance.achievementStats.ContainsKey(statKey))
            {
                currentVal = PlayerProgressManager.Instance.achievementStats[statKey];
            }
        }

        if (lockProgressBar != null)
        {
            lockProgressBar.maxValue = targetVal > 0 ? targetVal : 1; // 防除以0
            lockProgressBar.value = currentVal;
        }
        if (lockProgressText != null)
        {
            lockProgressText.text = $"{currentVal} / {targetVal}";
        }
    }

    // =========================================================
    //  【兼容保留】旧方法签名 (防止外部引用报错)
    // =========================================================

    /// <summary>
    /// 【已关闭】原技能树选择方法，现重定向到武器图鉴
    /// </summary>
    public void SelectWeaponTree(WeaponSkillTree tree)
    {
        SelectWeaponEntry(tree);
    }

    // =========================================================
    //  【已关闭】以下为原升级购买逻辑的空壳方法
    //  保留方法签名以防止 UpgradeNodeUI 等外部脚本编译报错
    //  恢复金币升级功能时，在此处补回具体逻辑
    // =========================================================

    /// <summary>
    /// 【已关闭】节点被点击时的购买回调（当前不执行任何操作）
    /// </summary>
    public void OnNodeSelected(UpgradeNodeUI selectedNodeUI, WeaponUpgradeNode selectedNodeData)
    {
        // 图鉴模式下不执行购买逻辑
        Debug.Log("[图鉴模式] 升级购买功能已暂时关闭。");
    }

    /// <summary>
    /// 【已关闭】刷新所有节点状态（当前不执行任何操作）
    /// </summary>
    public void RefreshAllNodeStates()
    {
        // 图鉴模式下不执行节点状态刷新
    }
}