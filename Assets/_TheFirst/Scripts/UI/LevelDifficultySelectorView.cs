using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelDifficultySelectorView : MonoBehaviour
{
    [Header("Bindings")]
    public Button normalButton;
    public Button hardButton;
    public TextMeshProUGUI normalText;
    public TextMeshProUGUI hardText;
    public TextMeshProUGUI hintText;

    [Header("Optional Visuals")]
    public Image normalBackground;
    public Image hardBackground;
    public GameObject normalSelectedRoot;
    public GameObject hardSelectedRoot;
    public GameObject hardLockedRoot;

    [Header("Scripted Colors")]
    public bool applyScriptedButtonColors = true;
    public Color selectedColor = new Color(0.93f, 0.55f, 0.16f, 1f);
    public Color normalColor = new Color(0.24f, 0.15f, 0.08f, 0.95f);
    public Color lockedColor = new Color(0.19f, 0.18f, 0.17f, 0.9f);

    public void ApplyVisualState(bool hardSelected, bool hardUnlocked)
    {
        bool normalSelected = !hardSelected;

        if (normalSelectedRoot != null) normalSelectedRoot.SetActive(normalSelected);
        if (hardSelectedRoot != null) hardSelectedRoot.SetActive(hardSelected && hardUnlocked);
        if (hardLockedRoot != null) hardLockedRoot.SetActive(!hardUnlocked);

        if (!applyScriptedButtonColors) return;

        if (normalBackground != null)
        {
            normalBackground.color = normalSelected ? selectedColor : normalColor;
        }

        if (hardBackground != null)
        {
            hardBackground.color = !hardUnlocked
                ? lockedColor
                : hardSelected ? selectedColor : normalColor;
        }
    }
}
