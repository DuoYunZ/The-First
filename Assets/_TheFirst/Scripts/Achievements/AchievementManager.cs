using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-950)]
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    [SerializeField] private string catalogResourcePath = "Achievements/achievements";
    [SerializeField] private float platformRetryInterval = 5f;
    [SerializeField] private bool logUnlocks = true;

    private readonly List<AchievementDefinition> definitions = new List<AchievementDefinition>();
    private readonly Dictionary<string, AchievementDefinition> definitionsById = new Dictionary<string, AchievementDefinition>();
    private readonly Dictionary<string, List<AchievementDefinition>> definitionsByStatKey = new Dictionary<string, List<AchievementDefinition>>();
    private IAchievementPlatform platform;
    private bool platformSyncPending = true;
    private float nextPlatformSyncTime;

    public IReadOnlyList<AchievementDefinition> Definitions => definitions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("[AchievementManager]");
        managerObject.AddComponent<AchievementManager>();
        Object.DontDestroyOnLoad(managerObject);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCatalog();
        platform = new SteamAchievementPlatform();
        platform.Initialize();
    }

    private void Start()
    {
        SyncFromProgress(PlayerProgressManager.Instance);
    }

    private void Update()
    {
        if (!platformSyncPending || Time.unscaledTime < nextPlatformSyncTime)
        {
            return;
        }

        TrySyncPlatformState();
    }

    private void OnApplicationQuit()
    {
        platform?.Flush();
    }

    public void NotifyStatChanged(string statKey, int currentValue)
    {
        if (string.IsNullOrWhiteSpace(statKey))
        {
            return;
        }

        if (definitionsByStatKey.TryGetValue(statKey, out List<AchievementDefinition> candidates))
        {
            foreach (AchievementDefinition definition in candidates)
            {
                Evaluate(definition, currentValue);
                SyncSteamStat(definition, currentValue);
            }
        }
    }

    public void SyncFromProgress(PlayerProgressManager progress)
    {
        if (progress == null)
        {
            platformSyncPending = true;
            SchedulePlatformRetry();
            return;
        }

        foreach (KeyValuePair<string, int> stat in progress.achievementStats)
        {
            NotifyStatChanged(stat.Key, stat.Value);
        }

        platformSyncPending = true;
        TrySyncPlatformState();
    }

    public bool UnlockAchievement(string achievementId)
    {
        if (string.IsNullOrWhiteSpace(achievementId))
        {
            return false;
        }

        if (!definitionsById.TryGetValue(achievementId, out AchievementDefinition definition))
        {
            definition = new AchievementDefinition { id = achievementId, steamId = achievementId, enabled = true };
        }

        return Unlock(definition);
    }

    private void LoadCatalog()
    {
        definitions.Clear();
        definitionsById.Clear();
        definitionsByStatKey.Clear();

        List<AchievementDefinition> loadedDefinitions = AchievementCatalogLoader.Load(catalogResourcePath);
        foreach (AchievementDefinition definition in loadedDefinitions)
        {
            if (definition == null || !definition.enabled || !definition.IsValid)
            {
                continue;
            }

            if (definitionsById.ContainsKey(definition.id))
            {
                Debug.LogWarning($"[Achievements] Duplicate achievement id '{definition.id}' ignored.");
                continue;
            }

            definitions.Add(definition);
            definitionsById.Add(definition.id, definition);

            if (definition.IsStatDriven)
            {
                if (!definitionsByStatKey.TryGetValue(definition.statKey, out List<AchievementDefinition> statDefinitions))
                {
                    statDefinitions = new List<AchievementDefinition>();
                    definitionsByStatKey.Add(definition.statKey, statDefinitions);
                }

                statDefinitions.Add(definition);
            }
        }
    }

    private void Evaluate(AchievementDefinition definition, int currentValue)
    {
        if (definition == null || !definition.enabled || !definition.IsStatDriven)
        {
            return;
        }

        if (currentValue >= definition.threshold)
        {
            Unlock(definition);
        }
    }

    private bool Unlock(AchievementDefinition definition)
    {
        if (definition == null || !definition.IsValid)
        {
            return false;
        }

        bool newlyUnlocked = false;
        if (PlayerProgressManager.Instance != null)
        {
            newlyUnlocked = PlayerProgressManager.Instance.MarkAchievementUnlocked(definition.id);
        }

        bool pushedToPlatform = PushToPlatform(definition);
        if (!pushedToPlatform)
        {
            platformSyncPending = true;
            SchedulePlatformRetry();
        }

        if (newlyUnlocked && logUnlocks)
        {
            Debug.Log($"[Achievements] Unlocked {definition.id}: {definition.displayName}");
        }

        return newlyUnlocked || pushedToPlatform;
    }

    private void SyncSteamStat(AchievementDefinition definition, int value)
    {
        if (definition == null || !definition.HasPlatformStat || string.IsNullOrWhiteSpace(definition.PlatformStatId))
        {
            return;
        }

        if (platform == null || !platform.SetStat(definition.PlatformStatId, value))
        {
            platformSyncPending = true;
            SchedulePlatformRetry();
        }
    }

    private bool PushToPlatform(AchievementDefinition definition)
    {
        if (platform == null || definition == null || string.IsNullOrWhiteSpace(definition.PlatformAchievementId))
        {
            return false;
        }

        return platform.UnlockAchievement(definition.PlatformAchievementId);
    }

    private void TrySyncPlatformState()
    {
        if (platform == null || !platform.RefreshAvailability())
        {
            platformSyncPending = true;
            SchedulePlatformRetry();
            return;
        }

        PlayerProgressManager progress = PlayerProgressManager.Instance;
        if (progress == null)
        {
            platformSyncPending = true;
            SchedulePlatformRetry();
            return;
        }

        foreach (string achievementId in progress.GetUnlockedAchievementIds())
        {
            if (definitionsById.TryGetValue(achievementId, out AchievementDefinition definition))
            {
                PushToPlatform(definition);
            }
            else
            {
                platform.UnlockAchievement(achievementId);
            }
        }

        foreach (KeyValuePair<string, int> stat in progress.achievementStats)
        {
            if (!definitionsByStatKey.TryGetValue(stat.Key, out List<AchievementDefinition> statDefinitions))
            {
                continue;
            }

            foreach (AchievementDefinition definition in statDefinitions)
            {
                SyncSteamStat(definition, stat.Value);
            }
        }

        platform.Flush();
        platformSyncPending = false;
    }

    private void SchedulePlatformRetry()
    {
        nextPlatformSyncTime = Time.unscaledTime + Mathf.Max(1f, platformRetryInterval);
    }
}
