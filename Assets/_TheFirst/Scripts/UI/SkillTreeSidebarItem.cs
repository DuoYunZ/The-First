using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 图鉴侧边栏条目 — 支持武器和被动道具两种数据类型
/// </summary>
public class SkillTreeSidebarItem : MonoBehaviour, IPointerClickHandler
{
    /// <summary>
    /// 图鉴条目类型
    /// </summary>
    public enum CodexEntryType { Weapon, Passive }

    [Header("UI 组件")]
    public Image iconImage;
    public Image backgroundImage;             // 底图 (不同类型显示不同背景)
    public Image highlightImage;              // 选中高亮的 Image 组件
    public GameObject selectionHighlight;      // 选中状态的高亮框
    public GameObject lockOverlay;             // 未解锁时的灰色遮罩

    [Header("底图配置")]
    [Tooltip("武器类条目使用的底图 Sprite")]
    public Sprite weaponBgSprite;
    [Tooltip("被动道具类条目使用的底图 Sprite")]
    public Sprite passiveBgSprite;

    [Header("高亮配置")]
    [Tooltip("武器类条目选中时的高亮 Sprite")]
    public Sprite weaponHighlightSprite;
    [Tooltip("被动道具类条目选中时的高亮 Sprite")]
    public Sprite passiveHighlightSprite;

    // --- 数据引用 ---
    public CodexEntryType EntryType { get; private set; }
    public WeaponSkillTree MyTreeData { get; private set; }
    public PassiveItemData MyPassiveData { get; private set; }
    private SkillTreeUIManager manager;

    /// <summary>
    /// 设置武器类型的图鉴条目
    /// </summary>
    public void Setup(WeaponSkillTree treeData, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Weapon;
        MyTreeData = treeData;
        MyPassiveData = null;
        manager = uiManager;

        // 设置图标
        if (treeData.associatedWeapon != null)
        {
            iconImage.sprite = treeData.associatedWeapon.weaponIcon;
        }

        // 设置武器类底图
        if (backgroundImage != null && weaponBgSprite != null)
        {
            backgroundImage.sprite = weaponBgSprite;
        }

        // 设置武器类高亮
        if (highlightImage != null && weaponHighlightSprite != null)
        {
            highlightImage.sprite = weaponHighlightSprite;
        }

        // 应用通用视觉状态
        ApplyVisualState(isUnlocked, isSelected);
    }

    /// <summary>
    /// 设置被动道具类型的图鉴条目
    /// </summary>
    public void Setup(PassiveItemData passiveData, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Passive;
        MyPassiveData = passiveData;
        MyTreeData = null;
        manager = uiManager;

        // 设置图标
        if (passiveData.icon != null)
        {
            iconImage.sprite = passiveData.icon;
        }

        // 设置被动道具类底图
        if (backgroundImage != null && passiveBgSprite != null)
        {
            backgroundImage.sprite = passiveBgSprite;
        }

        // 设置被动道具类高亮
        if (highlightImage != null && passiveHighlightSprite != null)
        {
            highlightImage.sprite = passiveHighlightSprite;
        }

        // 应用通用视觉状态
        ApplyVisualState(isUnlocked, isSelected);
    }

    /// <summary>
    /// 通用的视觉状态设置 (解锁/锁定、选中高亮)
    /// </summary>
    private void ApplyVisualState(bool isUnlocked, bool isSelected)
    {
        // 选中高亮
        if (selectionHighlight) selectionHighlight.SetActive(isSelected);

        if (isUnlocked)
        {
            // 已解锁：显示原色，隐藏锁图标
            iconImage.color = Color.white;
            if (lockOverlay) lockOverlay.SetActive(false);
        }
        else
        {
            // 未解锁：变成纯黑剪影
            iconImage.color = Color.black;
            if (lockOverlay) lockOverlay.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null) return;

        // 根据条目类型调用不同的选择方法
        switch (EntryType)
        {
            case CodexEntryType.Weapon:
                if (MyTreeData != null)
                    manager.SelectWeaponEntry(MyTreeData);
                break;
            case CodexEntryType.Passive:
                if (MyPassiveData != null)
                    manager.SelectPassiveEntry(MyPassiveData);
                break;
        }
    }
}