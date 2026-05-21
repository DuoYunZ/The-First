using System;
using System.Collections.Generic;

/// <summary>
/// Central switch for the current public demo content set.
/// Disable DemoModeEnabled when the full roster/maps should be exposed again.
/// </summary>
public static class DemoContentGate
{
    public static readonly bool DemoModeEnabled = true;
    public static readonly bool DisableUltimateSystemInDemo = true;
    public const string IntroClearStatKey = "Demo_Intro10_Clear";
    public const string HardClearStatKey = "Demo_Hard20_Clear";
    public const string HardUnlockItemId = "Demo_Hard20_Unlocked";
    public const string MageCharacterId = "Role02";
    public const string LightningStrikeWeaponId = "LightningStrike";
    public const string ChainLightningWeaponId = "ChainLightning";
    public const string FrostNovaWeaponId = "FrostNova";
    public const string FlameDaggerWeaponId = "FlameDagger";
    public const string ArcaneMasteryPassiveName = "奥术精通";
    public const string ElementalResonancePassiveName = "元素共鸣";
    public const string MechanicalResonancePassiveName = "机械共鸣";
    public const string ExperienceGainPassiveName = "经验磁铁";

    private static readonly HashSet<string> DemoCharacterIds = new HashSet<string>
    {
        "Role01",
        "Role02"
    };

    private static readonly HashSet<string> DemoSceneNames = new HashSet<string>
    {
        "CombatArena01"
    };

    private static readonly HashSet<string> DemoBlockedWeaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SuperMech"
    };

    private static readonly HashSet<string> DemoBlockedWeaponAssetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SO_Laser_Tank",
        "SO_SuperMech"
    };

    public static bool IsCharacterAllowed(CharacterData character)
    {
        if (!DemoModeEnabled) return true;
        if (character == null) return false;
        return DemoCharacterIds.Contains(character.characterID);
    }

    public static bool IsLevelAllowed(LevelData level)
    {
        if (!DemoModeEnabled) return true;
        if (level == null) return false;
        return DemoSceneNames.Contains(level.sceneName);
    }

    public static bool IsSceneAllowed(string sceneName)
    {
        if (!DemoModeEnabled) return true;
        return !string.IsNullOrEmpty(sceneName) && DemoSceneNames.Contains(sceneName);
    }

    public static bool IsWeaponAllowed(WeaponStatBlock weapon)
    {
        if (!DemoModeEnabled) return true;
        if (weapon == null) return false;
        if (string.IsNullOrWhiteSpace(weapon.weaponID)) return false;
        if (DemoBlockedWeaponIds.Contains(weapon.weaponID)) return false;
        if (!string.IsNullOrEmpty(weapon.name) && DemoBlockedWeaponAssetNames.Contains(weapon.name)) return false;
        return true;
    }

    public static bool IsPassiveAllowed(PassiveItemData passiveData)
    {
        if (!DemoModeEnabled) return true;
        if (passiveData == null) return true;
        return passiveData.statType != UpgradeType.MechanicalResonance;
    }

    public static string GetDemoFallbackScene()
    {
        foreach (var sceneName in DemoSceneNames)
        {
            return sceneName;
        }
        return string.Empty;
    }

    public static bool IsHardTimelineName(string timelineName)
    {
        return !string.IsNullOrEmpty(timelineName) && timelineName.Contains("Hard20");
    }
}
