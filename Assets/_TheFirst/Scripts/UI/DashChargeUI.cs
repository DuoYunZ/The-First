using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Dash 充能格 UI 显示
/// 根据 MechController 的充能格数动态创建/更新 UI 图标
/// 充满为亮色，耗尽为灰色，充能中显示填充进度
/// </summary>
public class DashChargeUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家角色上的 MechController（不设置则自动查找）")]
    public MechController mechController;

    [Header("UI 设置")]
    [Tooltip("充能格图标的父容器（HorizontalLayoutGroup）")]
    public Transform chargeContainer;
    [Tooltip("单个充能格的预制件（需要包含两个 Image: 背景底图 + 填充图）")]
    public GameObject chargeIconPrefab;

    [Header("颜色设置")]
    [Tooltip("充能格满时的颜色")]
    public Color chargedColor = new Color(0f, 0.85f, 1f, 1f); // 亮青色
    [Tooltip("充能格空时的颜色")]
    public Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.6f); // 灰色
    [Tooltip("正在充能时的填充颜色")]
    public Color chargingColor = new Color(0f, 0.5f, 0.8f, 0.8f); // 深青色

    // 运行时生成的图标列表
    private List<Image> fillImages = new List<Image>();
    private List<Image> bgImages = new List<Image>();
    private int lastChargeCount = -1;

    void Start()
    {
        TryFindMechController();
    }

    /// <summary>
    /// 尝试查找 MechController，支持跨场景动态生成的玩家
    /// </summary>
    void TryFindMechController()
    {
        if (mechController != null) return;

        // 方案1: 通过 GameManager 的 playerTransform
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            mechController = GameManager.Instance.playerTransform.GetComponent<MechController>();
            if (mechController == null)
            {
                // 可能在父级或子级
                mechController = GameManager.Instance.playerTransform.GetComponentInChildren<MechController>();
            }
        }

        // 方案2: 直接在场景中查找（处理跨场景改名的情况）
        if (mechController == null)
        {
            mechController = FindObjectOfType<MechController>();
        }

        if (mechController != null)
        {
            BuildChargeIcons(mechController.MaxDashCharges);
        }
    }

    void Update()
    {
        // 如果还没找到，持续尝试（处理玩家延迟生成的情况）
        if (mechController == null)
        {
            TryFindMechController();
            return;
        }

        // 如果充能格数变了（角色切换等），重建 UI
        if (mechController.MaxDashCharges != lastChargeCount)
        {
            BuildChargeIcons(mechController.MaxDashCharges);
        }

        UpdateChargeDisplay();
    }

    /// <summary>
    /// 根据格数动态生成 UI 图标
    /// </summary>
    void BuildChargeIcons(int count)
    {
        lastChargeCount = count;

        // 清除旧图标
        fillImages.Clear();
        bgImages.Clear();
        if (chargeContainer != null)
        {
            for (int i = chargeContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(chargeContainer.GetChild(i).gameObject);
            }
        }

        if (chargeIconPrefab == null || chargeContainer == null) return;

        // 生成新图标
        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(chargeIconPrefab, chargeContainer);
            icon.name = $"DashCharge_{i}";

            // 预制件结构要求：
            // - 根物体有 Image（背景底图）
            // - 子物体 "Fill" 有 Image（填充图，使用 Filled 模式）
            Image bg = icon.GetComponent<Image>();
            Image fill = icon.transform.Find("Fill")?.GetComponent<Image>();

            if (bg != null) bgImages.Add(bg);
            if (fill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImages.Add(fill);
            }
        }
    }

    /// <summary>
    /// 每帧更新充能格的视觉状态
    /// </summary>
    void UpdateChargeDisplay()
    {
        int currentCharges = mechController.CurrentDashCharges;
        int maxCharges = mechController.MaxDashCharges;

        for (int i = 0; i < fillImages.Count && i < maxCharges; i++)
        {
            float progress = mechController.GetChargeProgress(i);

            if (progress >= 1f)
            {
                // 已充满：亮色，完全填充
                fillImages[i].fillAmount = 1f;
                fillImages[i].color = chargedColor;
                if (i < bgImages.Count) bgImages[i].color = Color.white;
            }
            else if (progress > 0f)
            {
                // 正在充能：显示填充进度
                fillImages[i].fillAmount = progress;
                fillImages[i].color = chargingColor;
                if (i < bgImages.Count) bgImages[i].color = emptyColor;
            }
            else
            {
                // 完全耗尽：灰色
                fillImages[i].fillAmount = 0f;
                fillImages[i].color = chargingColor;
                if (i < bgImages.Count) bgImages[i].color = emptyColor;
            }
        }
    }
}
