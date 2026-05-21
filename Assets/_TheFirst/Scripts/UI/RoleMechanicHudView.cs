using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoleMechanicHudView : MonoBehaviour
{
    [Header("Core")]
    public TextMeshProUGUI labelText;
    public Image fillImage;
    public Image fillGlowImage;

    [Header("Sword Focus")]
    public GameObject stackPipRoot;
    public Image[] stackPips;
    public Color activePipColor = new Color(1f, 0.78f, 0.22f, 1f);
    public Color inactivePipColor = new Color(0.18f, 0.12f, 0.07f, 0.82f);

    [Header("Role Colors")]
    public bool applyRoleColorToFill = true;
    public Color swordsmanColor = new Color(1f, 0.55f, 0.18f, 1f);
    public Color mageColor = new Color(0.28f, 0.72f, 1f, 1f);
    public Color engineerColor = new Color(0.42f, 0.95f, 0.76f, 1f);

    public TextMeshProUGUI LabelText => labelText;
    public Image FillImage => fillImage;

    public void ConfigureForRole(string roleName, Color fallbackColor)
    {
        Color roleColor = ResolveRoleColor(roleName, fallbackColor);
        if (applyRoleColorToFill)
        {
            if (fillImage != null) fillImage.color = roleColor;
            if (fillGlowImage != null)
            {
                fillGlowImage.color = new Color(roleColor.r, roleColor.g, roleColor.b, fillGlowImage.color.a);
            }
        }
    }

    public void SetValue(string label, float fillAmount, int stacks, int maxStacks, bool showStackPips)
    {
        if (labelText != null) labelText.text = label;

        float clampedFill = Mathf.Clamp01(fillAmount);
        if (fillImage != null) fillImage.fillAmount = clampedFill;
        if (fillGlowImage != null) fillGlowImage.fillAmount = clampedFill;

        if (stackPipRoot != null) stackPipRoot.SetActive(showStackPips);
        if (!showStackPips || stackPips == null) return;

        int safeMax = Mathf.Max(0, maxStacks);
        for (int i = 0; i < stackPips.Length; i++)
        {
            Image pip = stackPips[i];
            if (pip == null) continue;

            bool visible = i < safeMax;
            pip.gameObject.SetActive(visible);
            if (!visible) continue;

            pip.color = i < stacks ? activePipColor : inactivePipColor;
        }
    }

    private Color ResolveRoleColor(string roleName, Color fallbackColor)
    {
        switch (roleName)
        {
            case "Swordsman":
                return swordsmanColor;
            case "Mage":
                return mageColor;
            case "Engineer":
                return engineerColor;
            default:
                return fallbackColor;
        }
    }
}
