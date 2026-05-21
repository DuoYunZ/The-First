using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 图鉴侧边栏条目 — 支持武器和被动道具两种数据类型
/// </summary>
public class SkillTreeSidebarItem : MonoBehaviour, IPointerClickHandler
{
    public void Setup(FusionRecipeSO recipe, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Fusion;
        MyTreeData = null;
        MyPassiveData = null;
        MyFusionRecipe = recipe;
        MyWeaponFusionRecipe = null;
        MyEvolutionRecipe = null;
        manager = uiManager;

        WeaponStatBlock result = recipe != null ? recipe.resultWeapon : null;
        Sprite icon = recipe != null && recipe.fusionIcon != null ? recipe.fusionIcon : result != null ? result.weaponIcon : null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = isUnlocked
                ? result != null ? GetWeaponDisplayName(result) : recipe != null ? recipe.name : "\u878d\u5408"
                : "\u672a\u77e5\u8fdb\u5316";
        }

        if (typeBadgeText != null) typeBadgeText.gameObject.SetActive(false);
        if (statusText != null) statusText.text = isUnlocked ? "\u878d\u5408" : "\u6b66\u5668 Lv.5";

        if (backgroundImage != null)
        {
            Sprite bg = fusionBgSprite != null ? fusionBgSprite : passiveBgSprite;
            if (bg != null) backgroundImage.sprite = bg;
        }

        if (highlightImage != null)
        {
            Sprite highlight = fusionHighlightSprite != null ? fusionHighlightSprite : passiveHighlightSprite;
            if (highlight != null) highlightImage.sprite = highlight;
        }

        ApplyVisualState(isUnlocked, isSelected);
    }

    public void Setup(WeaponFusionRecipeSO recipe, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Fusion;
        MyTreeData = null;
        MyPassiveData = null;
        MyFusionRecipe = null;
        MyWeaponFusionRecipe = recipe;
        MyEvolutionRecipe = null;
        manager = uiManager;

        WeaponStatBlock result = recipe != null ? recipe.resultWeapon : null;
        Sprite icon = recipe != null && recipe.cardIcon != null
            ? recipe.cardIcon
            : result != null ? result.weaponIcon : recipe != null && recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = isUnlocked
                ? recipe != null && !string.IsNullOrEmpty(recipe.recipeName)
                    ? recipe.recipeName
                    : result != null ? GetWeaponDisplayName(result) : recipe != null ? recipe.name : "\u878d\u5408"
                : "\u672a\u77e5\u8fdb\u5316";
        }

        if (typeBadgeText != null) typeBadgeText.gameObject.SetActive(false);
        if (statusText != null)
        {
            int revealLevel = recipe != null ? Mathf.Max(1, recipe.codexRevealWeaponLevel) : 5;
            statusText.text = isUnlocked ? "\u914d\u65b9" : $"\u6b66\u5668 Lv.{revealLevel}";
        }

        if (backgroundImage != null)
        {
            Sprite bg = fusionBgSprite != null ? fusionBgSprite : passiveBgSprite;
            if (bg != null) backgroundImage.sprite = bg;
        }

        if (highlightImage != null)
        {
            Sprite highlight = fusionHighlightSprite != null ? fusionHighlightSprite : passiveHighlightSprite;
            if (highlight != null) highlightImage.sprite = highlight;
        }

        ApplyVisualState(isUnlocked, isSelected);
    }

    public void Setup(EvolutionRecipeSO recipe, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Fusion;
        MyTreeData = null;
        MyPassiveData = null;
        MyFusionRecipe = null;
        MyWeaponFusionRecipe = null;
        MyEvolutionRecipe = recipe;
        manager = uiManager;

        WeaponStatBlock result = recipe != null ? recipe.ResultWeapon : null;
        Sprite icon = recipe != null && recipe.cardIcon != null
            ? recipe.cardIcon
            : result != null ? result.weaponIcon : recipe != null && recipe.MainWeapon != null ? recipe.MainWeapon.weaponIcon : null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = isUnlocked && recipe != null
                ? recipe.DisplayName
                : "\u672a\u77e5\u8fdb\u5316";
        }

        if (typeBadgeText != null) typeBadgeText.gameObject.SetActive(false);
        if (statusText != null)
        {
            int revealLevel = recipe != null ? Mathf.Max(1, recipe.codexRevealWeaponLevel) : 5;
            statusText.text = isUnlocked ? "\u5143\u7d20\u8fdb\u5316" : $"\u6b66\u5668 Lv.{revealLevel}";
        }

        if (backgroundImage != null)
        {
            Sprite bg = fusionBgSprite != null ? fusionBgSprite : passiveBgSprite;
            if (bg != null) backgroundImage.sprite = bg;
        }

        if (highlightImage != null)
        {
            Sprite highlight = fusionHighlightSprite != null ? fusionHighlightSprite : passiveHighlightSprite;
            if (highlight != null) highlightImage.sprite = highlight;
        }

        ApplyVisualState(isUnlocked, isSelected);
    }

    /// <summary>
    /// 图鉴条目类型
    /// </summary>
    public enum CodexEntryType { Weapon, Passive, Fusion }

    [Header("UI 组件")]
    public Image iconImage;
    public Image backgroundImage;             // 底图 (不同类型显示不同背景)
    public Image highlightImage;              // 选中高亮的 Image 组件
    public GameObject selectionHighlight;      // 选中状态的高亮框
    public GameObject lockOverlay;             // 未解锁时的灰色遮罩
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI typeBadgeText;

    [Header("底图配置")]
    [Tooltip("武器类条目使用的底图 Sprite")]
    public Sprite weaponBgSprite;
    [Tooltip("被动道具类条目使用的底图 Sprite")]
    public Sprite passiveBgSprite;
    public Sprite fusionBgSprite;
    [Tooltip("鏈В閿佹潯鐩娇鐢ㄧ殑搴曞浘 Sprite")]
    public Sprite lockedBgSprite;

    [Header("高亮配置")]
    [Tooltip("武器类条目选中时的高亮 Sprite")]
    public Sprite weaponHighlightSprite;
    [Tooltip("被动道具类条目选中时的高亮 Sprite")]
    public Sprite passiveHighlightSprite;
    public Sprite fusionHighlightSprite;

    // --- 数据引用 ---
    public CodexEntryType EntryType { get; private set; }
    public WeaponSkillTree MyTreeData { get; private set; }
    public PassiveItemData MyPassiveData { get; private set; }
    public FusionRecipeSO MyFusionRecipe { get; private set; }
    public WeaponFusionRecipeSO MyWeaponFusionRecipe { get; private set; }
    public EvolutionRecipeSO MyEvolutionRecipe { get; private set; }
    private SkillTreeUIManager manager;

    /// <summary>
    /// 设置武器类型的图鉴条目
    /// </summary>
    public void Setup(WeaponSkillTree treeData, SkillTreeUIManager uiManager, bool isUnlocked, bool isSelected)
    {
        EntryType = CodexEntryType.Weapon;
        MyTreeData = treeData;
        MyPassiveData = null;
        MyFusionRecipe = null;
        MyWeaponFusionRecipe = null;
        MyEvolutionRecipe = null;
        manager = uiManager;

        // 设置图标
        if (iconImage != null && treeData.associatedWeapon != null)
        {
            iconImage.sprite = treeData.associatedWeapon.weaponIcon;
            iconImage.enabled = treeData.associatedWeapon.weaponIcon != null;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (nameText != null)
        {
            string weaponName = treeData.associatedWeapon != null ? treeData.associatedWeapon.weaponName : treeData.name;
            nameText.text = LanguageTable.LocalizeWeaponName(weaponName, LocalizationManager.CurrentLanguage);
        }

        if (typeBadgeText != null) typeBadgeText.gameObject.SetActive(false);
        if (statusText != null) statusText.text = isUnlocked ? GetWeaponStatusText(treeData) : GetLockedStatusText(treeData.unlockStatKey, treeData.unlockThreshold);

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
        MyFusionRecipe = null;
        MyWeaponFusionRecipe = null;
        MyEvolutionRecipe = null;
        manager = uiManager;

        // 设置图标
        if (iconImage != null && passiveData.icon != null)
        {
            iconImage.sprite = passiveData.icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (nameText != null) nameText.text = passiveData.itemName;
        if (typeBadgeText != null) typeBadgeText.gameObject.SetActive(false);
        if (statusText != null) statusText.text = isUnlocked ? GetPassiveStatusText(passiveData) : GetLockedStatusText(passiveData.unlockStatKey, passiveData.unlockThreshold);

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
            if (iconImage != null) iconImage.color = Color.white;
            if (lockOverlay) lockOverlay.SetActive(false);
        }
        else
        {
            // 未解锁：变成纯黑剪影
            if (iconImage != null) iconImage.color = new Color(0.05f, 0.04f, 0.035f, 0.82f);
            if (lockOverlay) lockOverlay.SetActive(true);
        }

        if (backgroundImage != null)
        {
            bool hasDemoSprites = weaponBgSprite != null || passiveBgSprite != null || fusionBgSprite != null || lockedBgSprite != null;
            if (hasDemoSprites)
            {
                Sprite stateSprite = EntryType == CodexEntryType.Weapon
                    ? weaponBgSprite
                    : EntryType == CodexEntryType.Fusion ? fusionBgSprite : passiveBgSprite;
                if (EntryType == CodexEntryType.Fusion && stateSprite == null) stateSprite = passiveBgSprite;
                if (!isUnlocked && lockedBgSprite != null) stateSprite = lockedBgSprite;
                if (stateSprite != null) backgroundImage.sprite = stateSprite;

                Color spriteTint = isUnlocked ? Color.white : new Color(0.76f, 0.72f, 0.66f, 1f);
                if (isSelected) spriteTint = Color.Lerp(spriteTint, new Color(1f, 0.86f, 0.56f, 1f), 0.08f);
                backgroundImage.color = spriteTint;
            }
            else
            {
                Color baseColor = EntryType == CodexEntryType.Weapon
                    ? new Color(0.68f, 0.31f, 0.08f, 0.96f)
                    : EntryType == CodexEntryType.Fusion
                        ? new Color(0.38f, 0.22f, 0.52f, 0.96f)
                        : new Color(0.25f, 0.43f, 0.14f, 0.96f);
                if (!isUnlocked) baseColor = new Color(0.17f, 0.13f, 0.10f, 0.96f);
                if (isSelected) baseColor = Color.Lerp(baseColor, new Color(1f, 0.62f, 0.16f, 1f), 0.35f);
                backgroundImage.color = baseColor;
            }
        }

        if (highlightImage != null)
        {
            highlightImage.enabled = isSelected;
            bool hasHighlightSprite = weaponHighlightSprite != null || passiveHighlightSprite != null || fusionHighlightSprite != null;
            highlightImage.color = hasHighlightSprite
                ? new Color(1f, 1f, 1f, isSelected ? 1f : 0f)
                : new Color(1f, 0.73f, 0.18f, isSelected ? 0.95f : 0f);
        }

        if (nameText != null)
        {
            nameText.color = isUnlocked ? new Color(1f, 0.91f, 0.68f, 1f) : new Color(0.58f, 0.50f, 0.41f, 1f);
        }

        if (statusText != null)
        {
            statusText.color = isUnlocked ? new Color(1f, 0.72f, 0.22f, 1f) : new Color(0.86f, 0.52f, 0.18f, 1f);
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
            case CodexEntryType.Fusion:
                if (MyFusionRecipe != null)
                    manager.SelectFusionEntry(MyFusionRecipe);
                else if (MyWeaponFusionRecipe != null)
                    manager.SelectFusionEntry(MyWeaponFusionRecipe);
                else if (MyEvolutionRecipe != null)
                    manager.SelectFusionEntry(MyEvolutionRecipe);
                break;
        }
    }

    private string GetWeaponStatusText(WeaponSkillTree treeData)
    {
        if (treeData == null || treeData.associatedWeapon == null) return "\u5df2\u89e3\u9501";

        WeaponStatBlock weapon = treeData.associatedWeapon;
        int damage = Mathf.Max(weapon.baseDirectDamage, weapon.baseAoeDamage);
        return $"{GetWeaponFamilyLabel(weapon)}  {damage}";
    }

    private string GetWeaponDisplayName(WeaponStatBlock weapon)
    {
        if (weapon == null) return "\u672a\u914d\u7f6e";

        string weaponName = !string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponName : weapon.name;
        return LanguageTable.LocalizeWeaponName(weaponName, LocalizationManager.CurrentLanguage);
    }

    private string GetPassiveStatusText(PassiveItemData passiveData)
    {
        if (passiveData == null) return "\u5df2\u89e3\u9501";
        return passiveData.isTriggerPassive ? "\u89e6\u53d1\u578b" : $"\u6700\u9ad8 Lv.{passiveData.EffectiveMaxLevel}";
    }

    private string GetLockedStatusText(string statKey, int threshold)
    {
        if (threshold <= 0) return "\u5f85\u89e3\u9501";

        int currentValue = 0;
        if (PlayerProgressManager.Instance != null && !string.IsNullOrEmpty(statKey))
        {
            var stats = PlayerProgressManager.Instance.achievementStats;
            if (stats != null) stats.TryGetValue(statKey, out currentValue);
        }

        return $"{currentValue}/{threshold}";
    }

    private string GetWeaponRoleLabel(WeaponBehaviorType behavior)
    {
        switch (behavior)
        {
            case WeaponBehaviorType.MeleeAOE: return "\u65a9\u51fb";
            case WeaponBehaviorType.Standard: return "\u5f39\u9053";
            case WeaponBehaviorType.Pierce: return "\u7a7f\u900f";
            case WeaponBehaviorType.ParabolicAOE: return "\u7206\u70b8";
            case WeaponBehaviorType.Chain: return "\u8fde\u9501";
            case WeaponBehaviorType.Orbital: return "\u73af\u7ed5";
            case WeaponBehaviorType.PersistentAOE: return "\u573a\u5730";
            case WeaponBehaviorType.SummonDrone: return "\u53ec\u5524";
            case WeaponBehaviorType.Beam: return "\u5149\u675f";
            case WeaponBehaviorType.Landmine: return "\u9677\u9631";
            case WeaponBehaviorType.FlyingDagger: return "\u8ffd\u8e2a";
            case WeaponBehaviorType.FrostNova: return "\u63a7\u573a";
            case WeaponBehaviorType.LaserCore: return "\u805a\u7126";
            default: return "\u6b66\u5668";
        }
    }

    private string GetWeaponFamilyLabel(WeaponStatBlock weapon)
    {
        if (weapon == null) return "\u6b66\u5668";
        if (WeaponBuildTagUtility.IsSlashWeapon(weapon)) return "\u65a9\u51fb";
        if (WeaponBuildTagUtility.IsMechanicalWeapon(weapon)) return "\u5de5\u7a0b";
        if (WeaponBuildTagUtility.IsElementalWeapon(weapon)) return "\u6cd5\u672f";
        if (WeaponBuildTagUtility.IsGuardianWeapon(weapon)) return "\u5b88\u62a4";
        return GetWeaponRoleLabel(weapon.behavior);
    }
}
