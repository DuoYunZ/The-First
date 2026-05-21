using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeaponFusionManager : MonoBehaviour
{
    public static WeaponFusionManager Instance { get; private set; }
    private const int DefaultEvolutionWeaponLevel = 5;

    [Header("Recipe Library")]
    [Tooltip("All weapon evolution/fusion recipes.")]
    public List<WeaponFusionRecipeSO> allRecipes = new List<WeaponFusionRecipeSO>();

    public static WeaponFusionManager EnsureInstance()
    {
        if (Instance != null)
        {
#if UNITY_EDITOR
            Instance.LoadEditorRecipesIfEmpty();
#endif
            return Instance;
        }

        WeaponFusionManager existing = FindFirstObjectByType<WeaponFusionManager>();
        if (existing != null)
        {
            Instance = existing;
#if UNITY_EDITOR
            existing.LoadEditorRecipesIfEmpty();
#endif
            return existing;
        }

        GameObject runtimeObject = new GameObject("WeaponFusionManager_Runtime");
        return runtimeObject.AddComponent<WeaponFusionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
#if UNITY_EDITOR
        LoadEditorRecipesIfEmpty();
#endif
    }

#if UNITY_EDITOR
    private void LoadEditorRecipesIfEmpty()
    {
        if (allRecipes == null)
        {
            allRecipes = new List<WeaponFusionRecipeSO>();
        }

        string[] guids = AssetDatabase.FindAssets("t:WeaponFusionRecipeSO", new[] { "Assets/_TheFirst/GameData" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponFusionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<WeaponFusionRecipeSO>(path);
            if (recipe != null && !allRecipes.Contains(recipe))
            {
                allRecipes.Add(recipe);
            }
        }
    }
#endif

    public List<WeaponFusionRecipeSO> GetAvailableFusions(WeaponPart weapon)
    {
        List<WeaponFusionRecipeSO> available = new List<WeaponFusionRecipeSO>();
        if (weapon == null || weapon.StatBlock == null) return available;

        foreach (WeaponFusionRecipeSO recipe in allRecipes)
        {
            if (CanTriggerRecipe(weapon, recipe))
            {
                available.Add(recipe);
            }
        }

        return available;
    }

    public WeaponFusionRecipeSO FindAvailableRecipeByResult(WeaponStatBlock resultWeapon)
    {
        if (resultWeapon == null || WeaponController.Instance == null) return null;

        foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
        {
            if (owned?.weaponPartInstance == null) continue;

            List<WeaponFusionRecipeSO> recipes = GetAvailableFusions(owned.weaponPartInstance);
            foreach (WeaponFusionRecipeSO recipe in recipes)
            {
                if (recipe != null && recipe.resultWeapon == resultWeapon)
                {
                    return recipe;
                }
            }
        }

        return null;
    }

    private bool CanTriggerRecipe(WeaponPart weapon, WeaponFusionRecipeSO recipe)
    {
        if (weapon == null || recipe == null) return false;
        if (recipe.codexOnly) return false;
        if (IsWeaponAlreadyOwned(recipe.resultWeapon)) return false;

        OwnedWeapon triggerOwned = FindOwnedWeaponForPart(weapon);
        if (triggerOwned != null)
        {
            if (!MatchesWeaponSource(triggerOwned, recipe.triggerWeapon)) return false;
            if (!IsOwnedWeaponReady(triggerOwned, recipe.requiredStage, recipe.requiredWeaponLevel)) return false;
        }
        else
        {
            if (recipe.triggerWeapon != weapon.StatBlock) return false;
            if (!IsWeaponReady(weapon, recipe.requiredStage, recipe.requiredWeaponLevel)) return false;
        }

        if (recipe.conditions != null)
        {
            foreach (FusionCondition cond in recipe.conditions)
            {
                if (!CheckCondition(cond)) return false;
            }
        }

        return true;
    }

    private bool CheckCondition(FusionCondition cond)
    {
        if (cond == null) return true;

        switch (cond.type)
        {
            case ConditionType.Weapon:
                return HasWeapon(cond.requiredWeapon, cond.requiredWeaponStage, cond.requiredWeaponLevel);
            case ConditionType.Passive:
                return HasPassive(cond);
            case ConditionType.Talent:
                return HasTalent(cond.requiredTalentId);
            default:
                return false;
        }
    }

    private bool HasWeapon(WeaponStatBlock wsb, WeaponStage stage, int requiredLevel)
    {
        if (wsb == null || WeaponController.Instance == null) return false;

        foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
        {
            if (owned == null || owned.weaponPartInstance == null) continue;
            if (MatchesWeaponSource(owned, wsb))
            {
                if (IsOwnedWeaponReady(owned, stage, requiredLevel)) return true;
            }
        }

        return false;
    }

    private bool IsWeaponReady(WeaponPart weapon, WeaponStage stage, int requiredLevel)
    {
        if (weapon == null) return false;
        OwnedWeapon owned = WeaponController.Instance != null
            ? WeaponController.Instance.ownedWeapons.Find(w => w != null && w.weaponPartInstance == weapon)
            : null;

        if (owned != null) return IsOwnedWeaponReady(owned, stage, requiredLevel);

        int maxLevel = Mathf.Max(1, weapon.maxLevel);
        if (weapon.StatBlock != null) maxLevel = Mathf.Max(maxLevel, weapon.StatBlock.maxLevel);
        int targetLevel = Mathf.Min(NormalizeRequiredWeaponLevel(requiredLevel), maxLevel);
        if (weapon.currentLevel < targetLevel) return false;
        if (stage == WeaponStage.Base) return true;
        if (weapon.currentStage >= stage) return true;
        return weapon.currentLevel >= maxLevel;
    }

    private bool IsOwnedWeaponReady(OwnedWeapon owned, WeaponStage stage, int requiredLevel)
    {
        if (owned == null || owned.weaponPartInstance == null) return false;
        int maxLevel = Mathf.Max(1, owned.stats != null ? owned.stats.maxLevel : owned.weaponPartInstance.maxLevel);
        if (owned.weaponPartInstance != null) maxLevel = Mathf.Max(maxLevel, owned.weaponPartInstance.maxLevel);
        int targetLevel = Mathf.Min(NormalizeRequiredWeaponLevel(requiredLevel), maxLevel);
        if (owned.currentLevel < targetLevel) return false;
        if (stage == WeaponStage.Base) return true;
        if (owned.weaponPartInstance.currentStage >= stage) return true;

        return owned.currentLevel >= maxLevel;
    }

    private int NormalizeRequiredWeaponLevel(int requiredLevel)
    {
        return requiredLevel > 0 ? requiredLevel : DefaultEvolutionWeaponLevel;
    }

    private OwnedWeapon FindOwnedWeaponForPart(WeaponPart weapon)
    {
        if (weapon == null || WeaponController.Instance == null) return null;
        return WeaponController.Instance.ownedWeapons.Find(w => w != null && w.weaponPartInstance == weapon);
    }

    private bool MatchesWeaponSource(OwnedWeapon owned, WeaponStatBlock source)
    {
        if (owned == null || source == null) return false;
        if (owned.InheritsSkillSource(source)) return true;
        if (owned.stats == source) return true;
        if (owned.weaponPartInstance != null && owned.weaponPartInstance.StatBlock == source) return true;

        string sourceId = source.weaponID;
        if (!string.IsNullOrEmpty(sourceId))
        {
            if (owned.stats != null && string.Equals(owned.stats.weaponID, sourceId, System.StringComparison.OrdinalIgnoreCase)) return true;
            WeaponStatBlock partStats = owned.weaponPartInstance != null ? owned.weaponPartInstance.StatBlock : null;
            if (partStats != null && string.Equals(partStats.weaponID, sourceId, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        string sourceName = source.weaponName;
        if (!string.IsNullOrEmpty(sourceName))
        {
            if (owned.stats != null && string.Equals(owned.stats.weaponName, sourceName, System.StringComparison.OrdinalIgnoreCase)) return true;
            WeaponStatBlock partStats = owned.weaponPartInstance != null ? owned.weaponPartInstance.StatBlock : null;
            if (partStats != null && string.Equals(partStats.weaponName, sourceName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private bool IsWeaponAlreadyOwned(WeaponStatBlock source)
    {
        if (source == null || WeaponController.Instance == null) return false;

        foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
        {
            if (MatchesWeaponSource(owned, source)) return true;
        }

        return false;
    }

    private bool HasPassive(FusionCondition condition)
    {
        if (condition == null) return true;
        return HasPassive(condition.requiredPassiveItem, condition.requiredPassiveId, condition.requiredPassiveLevel);
    }

    private bool HasPassive(PassiveItemData requiredPassive, string passiveId, int requiredLevel)
    {
        if (requiredPassive == null && string.IsNullOrEmpty(passiveId)) return false;
        if (PlayerStats.Instance == null || PlayerStats.Instance.activePassiveItems == null) return false;

        foreach (RuntimePassiveItem item in PlayerStats.Instance.activePassiveItems)
        {
            if (item == null || item.data == null || item.currentLevel <= 0) continue;

            PassiveItemData data = item.data;
            bool matches = data == requiredPassive ||
                           string.Equals(data.name, passiveId, System.StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(data.itemName, passiveId, System.StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(data.statType.ToString(), passiveId, System.StringComparison.OrdinalIgnoreCase);
            int targetLevel = GetEffectivePassiveRequiredLevel(requiredPassive != null ? requiredPassive : data, requiredLevel);
            if (matches && item.currentLevel >= targetLevel) return true;
        }

        return false;
    }

    private int GetEffectivePassiveRequiredLevel(PassiveItemData passive, int requiredLevel)
    {
        int targetLevel = requiredLevel > 0 ? requiredLevel : PassiveItemData.PassiveCapstoneLevel;
        if (passive != null)
        {
            targetLevel = Mathf.Min(targetLevel, passive.EffectiveMaxLevel);
        }
        return targetLevel;
    }

    private bool HasTalent(string talentId)
    {
        if (string.IsNullOrEmpty(talentId)) return false;
        return UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill(talentId);
    }
}
