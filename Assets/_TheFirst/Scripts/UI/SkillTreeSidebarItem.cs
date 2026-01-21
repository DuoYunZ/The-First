using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillTreeSidebarItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 组件")]
    public Image iconImage;
    public GameObject selectionHighlight; // 选中状态的高亮框
    public GameObject lockOverlay;        // 未解锁时的灰色遮罩

    public WeaponSkillTree MyTreeData { get; private set; }
    private SkillTreeUIManager manager;

    public void Setup(WeaponSkillTree treeData, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        MyTreeData = treeData;
        manager = uiManager;

        // 1. 设置图标
        if (treeData.associatedWeapon != null)
        {
            iconImage.sprite = treeData.associatedWeapon.weaponIcon;
        }

        // 2. 设置选中高亮
        if (selectionHighlight) selectionHighlight.SetActive(isSelected);

        // 3. 【核心修改】处理解锁状态和剪影
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

            // 选项 A：如果你想在黑色剪影上还盖一个锁的图标，就保留这行
            //if (lockOverlay) lockOverlay.SetActive(true);

            // 选项 B：如果你觉得黑色剪影就够了，不需要那个灰色的六边形底图了，就改成 false
             if (lockOverlay) lockOverlay.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.SelectWeaponTree(MyTreeData);
        }
    }
}