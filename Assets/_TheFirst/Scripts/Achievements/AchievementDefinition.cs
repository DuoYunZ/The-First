using System;
using System.Collections.Generic;

[Serializable]
public class AchievementDefinition
{
    public string id;
    public string displayName;
    public string description;
    public string statKey;
    public int threshold = 1;
    public string steamId;
    public string steamStatId;
    public bool hidden;
    public bool enabled = true;

    public bool IsValid => !string.IsNullOrWhiteSpace(id);
    public bool IsStatDriven => !string.IsNullOrWhiteSpace(statKey) && threshold > 0;
    public string PlatformAchievementId => string.IsNullOrWhiteSpace(steamId) ? id : steamId;
    public string PlatformStatId => string.IsNullOrWhiteSpace(steamStatId) ? statKey : steamStatId;
    public bool HasPlatformStat => !string.IsNullOrWhiteSpace(steamStatId);
}

[Serializable]
public class AchievementCatalogData
{
    public List<AchievementDefinition> achievements = new List<AchievementDefinition>();
}
