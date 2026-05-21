using System;
using System.Collections.Generic;
using UnityEngine;

public static class WeaponBuildTagUtility
{
    public static bool HasTag(WeaponStatBlock weapon, WeaponBuildTag tag)
    {
        return GetTags(weapon).Contains(tag);
    }

    public static HashSet<WeaponBuildTag> GetTags(WeaponStatBlock weapon)
    {
        HashSet<WeaponBuildTag> tags = new HashSet<WeaponBuildTag>();
        if (weapon == null) return tags;

        if (weapon.buildTags != null)
        {
            foreach (WeaponBuildTag tag in weapon.buildTags)
            {
                tags.Add(tag);
            }
        }

        AddInferredTags(weapon, tags);
        return tags;
    }

    public static bool IsSlashWeapon(WeaponStatBlock weapon)
    {
        return HasTag(weapon, WeaponBuildTag.Slash);
    }

    public static bool IsMechanicalWeapon(WeaponStatBlock weapon)
    {
        HashSet<WeaponBuildTag> tags = GetTags(weapon);
        return tags.Contains(WeaponBuildTag.Mechanical) ||
               tags.Contains(WeaponBuildTag.Deployable);
    }

    public static bool IsGuardianWeapon(WeaponStatBlock weapon)
    {
        HashSet<WeaponBuildTag> tags = GetTags(weapon);
        return tags.Contains(WeaponBuildTag.Guardian) ||
               tags.Contains(WeaponBuildTag.Aura);
    }

    public static bool IsElementalWeapon(WeaponStatBlock weapon)
    {
        return !string.IsNullOrEmpty(GetPrimaryElementFamily(weapon));
    }

    public static int GetElementFamilyWeight(WeaponStatBlock weapon)
    {
        if (!IsElementalWeapon(weapon)) return 0;

        HashSet<WeaponBuildTag> tags = GetTags(weapon);
        if (tags.Contains(WeaponBuildTag.Slash) ||
            tags.Contains(WeaponBuildTag.Mechanical) ||
            tags.Contains(WeaponBuildTag.Guardian))
        {
            return 2;
        }

        return 1;
    }

    public static string GetPrimaryElementFamily(WeaponStatBlock weapon)
    {
        HashSet<WeaponBuildTag> tags = GetTags(weapon);
        if (tags.Contains(WeaponBuildTag.Fire)) return "Fire";
        if (tags.Contains(WeaponBuildTag.Ice)) return "Ice";
        if (tags.Contains(WeaponBuildTag.Lightning)) return "Thunder";
        if (tags.Contains(WeaponBuildTag.Wind)) return "Wind";
        return "";
    }

    private static void AddInferredTags(WeaponStatBlock weapon, HashSet<WeaponBuildTag> tags)
    {
        string id = weapon.weaponID ?? "";
        string assetName = weapon.name ?? "";
        string displayName = weapon.weaponName ?? "";
        string searchableName = $"{id} {assetName} {displayName}";
        WeaponBehaviorType behavior = weapon.behavior;

        if (ContainsAny(searchableName, "Blade", "Slash", "Scythe", "Sword", "Katana", "Saber"))
        {
            tags.Add(WeaponBuildTag.Slash);
        }

        if (ContainsAny(searchableName, "Fire", "Flame", "Inferno", "Meteor", "Napalm"))
        {
            tags.Add(WeaponBuildTag.Fire);
            tags.Add(WeaponBuildTag.Spell);
        }

        if (ContainsAny(searchableName, "Ice", "Frost", "Crystal", "Blizzard", "Hail"))
        {
            tags.Add(WeaponBuildTag.Ice);
            tags.Add(WeaponBuildTag.Spell);
            tags.Add(WeaponBuildTag.Control);
        }

        if (ContainsAny(searchableName, "Lightning", "Thunder", "Chain", "Volt", "Storm"))
        {
            tags.Add(WeaponBuildTag.Lightning);
            tags.Add(WeaponBuildTag.Spell);
        }

        if (ContainsAny(searchableName, "Hurricane", "Wind", "Tornado", "Gale"))
        {
            tags.Add(WeaponBuildTag.Wind);
            tags.Add(WeaponBuildTag.Spell);
            tags.Add(WeaponBuildTag.Control);
        }

        if (ContainsAny(searchableName, "Landmine", "Mine", "Grenade", "Laser", "Beam", "Turret", "Drone", "Mech", "Mortar", "Gear", "Tank", "Cannon") ||
            behavior == WeaponBehaviorType.Landmine ||
            behavior == WeaponBehaviorType.Beam ||
            behavior == WeaponBehaviorType.LaserCore ||
            behavior == WeaponBehaviorType.SummonDrone ||
            behavior == WeaponBehaviorType.Funnel ||
            behavior == WeaponBehaviorType.SuperMech)
        {
            tags.Add(WeaponBuildTag.Mechanical);
        }

        if (behavior == WeaponBehaviorType.Landmine ||
            behavior == WeaponBehaviorType.PersistentAOE ||
            behavior == WeaponBehaviorType.CreateAndForget ||
            ContainsAny(searchableName, "Landmine", "Mine", "Turret", "Mortar", "Trap"))
        {
            tags.Add(WeaponBuildTag.Deployable);
        }

        if (behavior == WeaponBehaviorType.Orbital ||
            behavior == WeaponBehaviorType.Aura ||
            ContainsAny(searchableName, "Orbit", "Aura", "Shield", "Bulwark", "Wisp", "Charm"))
        {
            tags.Add(WeaponBuildTag.Guardian);
        }

        if (behavior == WeaponBehaviorType.Aura)
        {
            tags.Add(WeaponBuildTag.Aura);
        }

        if (behavior == WeaponBehaviorType.Standard ||
            behavior == WeaponBehaviorType.Pierce ||
            behavior == WeaponBehaviorType.ParabolicAOE ||
            behavior == WeaponBehaviorType.Chain ||
            behavior == WeaponBehaviorType.Boomerang ||
            behavior == WeaponBehaviorType.FlyingDagger ||
            behavior == WeaponBehaviorType.FrostNova)
        {
            tags.Add(WeaponBuildTag.Projectile);
        }
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        if (string.IsNullOrEmpty(source)) return false;
        foreach (string token in tokens)
        {
            if (!string.IsNullOrEmpty(token) &&
                source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
