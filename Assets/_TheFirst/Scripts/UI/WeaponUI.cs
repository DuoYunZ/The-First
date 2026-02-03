// WeaponUI.cs
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.UI;

public class WeaponUI : MonoBehaviour
{
    public static WeaponUI Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("放置图标的父容器 (Horizontal Layout Group)")]
    public Transform iconContainer;
    [Tooltip("图标预制件 (包含 Image 组件)")]
    public GameObject iconPrefab;

    [Header("颜色设置")]
    [Tooltip("没有武器时的底框颜色 (例如：半透明黑)")]
    public Color emptySlotColor = new Color(0, 0, 0, 0.5f);

    [Tooltip("获得武器后的底框颜色 (例如：橙色/棕色)")]
    public Color equippedSlotColor = new Color(0.6f, 0.3f, 0.1f, 1f); // 参考你截图的颜色

    [Header("设置")]
    public int maxIcons = 6; // 最大显示数量

    private class WeaponSlot
    {
        public Image backgroundImage; // 底框
        public Image iconImage;       // 武器图标
    }

    // 缓存生成的图标对象
    private List<WeaponSlot> slots = new List<WeaponSlot>();

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        // 初始化生成空的槽位
        InitializeSlots();
        // 开局刷新一次 (显示初始刀光)
        UpdateWeaponIcons();
    }

    private void InitializeSlots()
    {
        foreach (Transform child in iconContainer) Destroy(child.gameObject);
        slots.Clear();

        for (int i = 0; i < maxIcons; i++)
        {
            GameObject go = Instantiate(iconPrefab, iconContainer);

            // 1. 获取底框 (Prefab 根节点上的 Image)
            Image bg = go.GetComponent<Image>();

            // 2. 获取图标 (Prefab 第一个子节点上的 Image)
            // 注意：我们假设 Prefab 结构是 Root(背景) -> Child(图标)
            Image icon = null;
            if (go.transform.childCount > 0)
            {
                icon = go.transform.GetChild(0).GetComponent<Image>();
            }

            if (bg != null && icon != null)
            {
                // 初始化状态
                bg.color = emptySlotColor;
                icon.sprite = null;
                icon.enabled = false; // 隐藏图标，只留底框

                // 保存引用
                slots.Add(new WeaponSlot { backgroundImage = bg, iconImage = icon });
            }
            else
            {
                Debug.LogError("WeaponIconPrefab 结构不对！请确保根节点有Image，且有一个带Image的子节点。");
            }
        }
    }

    /// <summary>
    /// 刷新所有武器图标 (逻辑参考 FusionUIManager)
    /// </summary>
    public void UpdateWeaponIcons()
    {
        var controller = WeaponController.Instance;
        if (controller == null) return;

        List<Sprite> iconsToShow = new List<Sprite>();

        // A. 自带刀光
        if (controller.builtInBladeWeapon != null &&
             controller.builtInBladeWeapon.StatBlock != null &&
             controller.builtInBladeWeapon.isActiveAndEnabled)
        {
            iconsToShow.Add(controller.builtInBladeWeapon.StatBlock.weaponIcon);
        }

        // B. 动态武器 (带去重)
        foreach (var owned in controller.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && owned.weaponPartInstance.StatBlock != null)
            {
                // 去重逻辑
                if (controller.builtInBladeWeapon != null &&
                    controller.builtInBladeWeapon.StatBlock != null &&
                    owned.weaponPartInstance.StatBlock == controller.builtInBladeWeapon.StatBlock)
                {
                    continue;
                }
                iconsToShow.Add(owned.weaponPartInstance.StatBlock.weaponIcon);
            }
        }

        // C. 更新 UI
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];

            if (i < iconsToShow.Count)
            {
                // --- 有武器状态 ---
                // 1. 底框变色 (变成“已装备”颜色)
                slot.backgroundImage.color = equippedSlotColor;

                // 2. 显示图标
                slot.iconImage.sprite = iconsToShow[i];
                slot.iconImage.enabled = true;
                slot.iconImage.color = Color.white; // 确保图标本身不透明
            }
            else
            {
                // --- 空槽状态 ---
                // 1. 底框变回默认色
                slot.backgroundImage.color = emptySlotColor;

                // 2. 隐藏图标
                slot.iconImage.sprite = null;
                slot.iconImage.enabled = false;
            }
        }
    }
}