using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PassiveItemsUI : MonoBehaviour
{
    public static PassiveItemsUI Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("放置图标的父容器 (Horizontal Layout Group)")]
    public Transform iconContainer;
    [Tooltip("图标预制件 (可以直接复用 WeaponIconPrefab)")]
    public GameObject iconPrefab;

    [Header("颜色设置")]
    public Color emptySlotColor = new Color(0, 0, 0, 0.5f);
    public Color equippedSlotColor = new Color(0.1f, 0.5f, 0.8f, 1f); // 蓝色调，区别于武器

    [Header("设置")]
    public int maxIcons = 6;

    private class PassiveSlot
    {
        public Image backgroundImage;
        public Image iconImage;
    }

    private List<PassiveSlot> slots = new List<PassiveSlot>();

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        InitializeSlots();
        // 订阅 PlayerStats 的变化 (如果你有做事件)，或者在 Update/Enable 中刷新
        // 暂时我们手动调用 UpdateIcons
        UpdateIcons();
    }

    private void InitializeSlots()
    {
        // 清理旧的子物体
        foreach (Transform child in iconContainer) Destroy(child.gameObject);
        slots.Clear();

        for (int i = 0; i < maxIcons; i++)
        {
            GameObject go = Instantiate(iconPrefab, iconContainer);

            // 获取组件 (假设 prefab 结构是 BG -> Icon)
            Image bg = go.GetComponent<Image>();
            Image icon = null;
            if (go.transform.childCount > 0)
                icon = go.transform.GetChild(0).GetComponent<Image>();

            if (bg != null && icon != null)
            {
                bg.color = emptySlotColor;
                icon.enabled = false;
                slots.Add(new PassiveSlot { backgroundImage = bg, iconImage = icon });
            }
        }
    }

    public void UpdateIcons()
    {
        if (PlayerStats.Instance == null) return;

        var items = PlayerStats.Instance.activePassiveItems;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                // 有道具
                PassiveSlot slot = slots[i];
                var itemData = items[i].data;

                slot.backgroundImage.color = equippedSlotColor;
                slot.iconImage.sprite = itemData.icon;
                slot.iconImage.enabled = true;
                slot.iconImage.color = Color.white;
            }
            else
            {
                // 空槽
                PassiveSlot slot = slots[i];
                slot.backgroundImage.color = emptySlotColor;
                slot.iconImage.enabled = false;
            }
        }
    }
}