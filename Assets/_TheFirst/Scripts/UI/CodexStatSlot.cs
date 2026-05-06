using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 图鉴武器属性条目 — 挂在每个属性条目预制体上
/// 每个条目显示一个属性图标和对应的数值文本
/// </summary>
public class CodexStatSlot : MonoBehaviour
{
    [Header("UI 组件")]
    public Image statIcon;            // 属性图标 (如剑、弓等)
    public TextMeshProUGUI valueText; // 数值文本 (如 "28", "0.3S")

    /// <summary>
    /// 设置属性条目的显示内容
    /// </summary>
    public void Setup(Sprite icon, string value)
    {
        if (statIcon != null)
        {
            statIcon.sprite = icon;
            statIcon.enabled = (icon != null);
        }
        if (valueText != null)
        {
            valueText.text = value;
        }
    }
}
