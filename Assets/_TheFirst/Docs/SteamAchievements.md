# Steam Achievements Setup

The runtime achievement system is code-only and auto-starts before the first scene.

## Local catalog

Edit `Assets/_TheFirst/Resources/Achievements/achievements.json`.

- `id`: local achievement id saved in `playerProgress.json`.
- `steamId`: Steamworks achievement API name. Keep this identical to the API Name in Steamworks App Admin.
- `statKey`: key updated through `PlayerProgressManager.AddStat` or `IncreaseAchievementStat`.
- `threshold`: unlock value for that stat.
- `steamStatId`: optional Steamworks stat API name. Leave empty unless the Steam dashboard also defines a matching stat.

## Steamworks.NET

1. Install Steamworks.NET in the Unity project.
2. Add its `SteamManager` to the first boot scene or keep an equivalent initializer alive before gameplay.
3. Create every `steamId` from `achievements.json` in Steamworks App Admin.
4. For local editor testing, place `steam_appid.txt` next to the editor/player executable as required by Steamworks.NET.

Without Steamworks.NET the game still records and unlocks achievements locally. When Steam becomes available, the manager pushes already unlocked local achievements to Steam.
