using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single stat row used by the codex detail page.
/// Existing prefabs can keep using only valueText; labelText is optional.
/// </summary>
public class CodexStatSlot : MonoBehaviour
{
    [Header("UI Components")]
    public Image statIcon;
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI valueText;

    public void Setup(Sprite icon, string value)
    {
        Setup(icon, "", value);
    }

    public void Setup(Sprite icon, string label, string value)
    {
        if (statIcon != null)
        {
            statIcon.sprite = icon;
            statIcon.enabled = icon != null;
        }

        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = new Color(0.95f, 0.72f, 0.32f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (valueText != null)
        {
            valueText.text = labelText == null && !string.IsNullOrEmpty(label)
                ? $"{label}\n{value}"
                : value;
            valueText.color = Color.white;
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
}
