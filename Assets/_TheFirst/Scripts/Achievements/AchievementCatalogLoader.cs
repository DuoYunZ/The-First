using System.Collections.Generic;
using UnityEngine;

public static class AchievementCatalogLoader
{
    public static List<AchievementDefinition> Load(string resourcePath)
    {
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>(resourcePath);
            if (catalogAsset != null)
            {
                AchievementCatalogData catalog = JsonUtility.FromJson<AchievementCatalogData>(catalogAsset.text);
                if (catalog != null && catalog.achievements != null && catalog.achievements.Count > 0)
                {
                    return catalog.achievements;
                }
            }
        }

        return CreateFallbackCatalog();
    }

    private static List<AchievementDefinition> CreateFallbackCatalog()
    {
        return new List<AchievementDefinition>
        {
            Create("ACH_FIRST_BLOOD", "First Blood", "Defeat your first enemy.", "Kill_Count", 1),
            Create("ACH_100_KILLS", "Scrap Storm", "Defeat 100 enemies.", "Kill_Count", 100),
            Create("ACH_FIRST_DASH", "Boost Online", "Dash once.", "Dash_Count", 1),
            Create("ACH_100_DASHES", "Afterburner", "Dash 100 times.", "Dash_Count", 100),
            Create("ACH_FIRST_CLEAR", "First Clear", "Win a run.", "Victory_Count", 1),
            Create("ACH_FIRST_DEATH", "Rebuilt", "Fall in battle once.", "Death_Count", 1),
            Create("ACH_FIRST_IGNITE", "Spark", "Ignite an enemy.", "Ignite_Count", 1),
            Create("ACH_1000_IGNITES", "Wildfire", "Ignite enemies 1000 times.", "Ignite_Count", 1000),
            Create("ACH_LEVEL_10", "Growing Core", "Reach level 10.", "Max_Level_Reached", 10)
        };
    }

    private static AchievementDefinition Create(string id, string displayName, string description, string statKey, int threshold)
    {
        return new AchievementDefinition
        {
            id = id,
            steamId = id,
            displayName = displayName,
            description = description,
            statKey = statKey,
            threshold = threshold,
            enabled = true
        };
    }
}
