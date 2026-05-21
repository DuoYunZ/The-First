using System;
using System.Reflection;
using UnityEngine;

public sealed class SteamAchievementPlatform : IAchievementPlatform
{
    private Type steamUserStatsType;
    private Type steamManagerType;
    private MethodInfo requestCurrentStatsMethod;
    private MethodInfo setAchievementMethod;
    private MethodInfo getAchievementMethod;
    private MethodInfo setStatIntMethod;
    private MethodInfo storeStatsMethod;
    private PropertyInfo steamManagerInitializedProperty;
    private FieldInfo steamManagerInitializedField;
    private bool requestedStats;
    private bool warnedMissingSteamworks;
    private bool warnedUnavailable;

    public bool IsAvailable { get; private set; }

    public void Initialize()
    {
        ResolveSteamTypes();
        RefreshAvailability();
    }

    public bool RefreshAvailability()
    {
        if (steamUserStatsType == null)
        {
            ResolveSteamTypes();
        }

        if (steamUserStatsType == null)
        {
            if (!warnedMissingSteamworks)
            {
                Debug.Log("[Achievements] Steamworks.NET not found. Steam achievement sync is disabled until the Steamworks package and SteamManager are added.");
                warnedMissingSteamworks = true;
            }

            IsAvailable = false;
            return false;
        }

        if (!IsSteamManagerInitialized())
        {
            if (!warnedUnavailable)
            {
                Debug.Log("[Achievements] Steamworks found, but SteamManager is not initialized yet. Local achievements will sync when Steam is ready.");
                warnedUnavailable = true;
            }

            IsAvailable = false;
            return false;
        }

        IsAvailable = true;
        warnedUnavailable = false;

        if (!requestedStats)
        {
            TryInvokeBool(requestCurrentStatsMethod);
            requestedStats = true;
        }

        return true;
    }

    public bool UnlockAchievement(string platformAchievementId)
    {
        if (string.IsNullOrWhiteSpace(platformAchievementId) || !RefreshAvailability())
        {
            return false;
        }

        if (IsAchievementAlreadyUnlocked(platformAchievementId))
        {
            return true;
        }

        bool unlocked = TryInvokeBool(setAchievementMethod, platformAchievementId);
        if (unlocked)
        {
            Flush();
        }

        return unlocked;
    }

    public bool SetStat(string platformStatId, int value)
    {
        if (string.IsNullOrWhiteSpace(platformStatId) || !RefreshAvailability() || setStatIntMethod == null)
        {
            return false;
        }

        return TryInvokeBool(setStatIntMethod, platformStatId, value);
    }

    public void Flush()
    {
        if (!RefreshAvailability())
        {
            return;
        }

        TryInvokeBool(storeStatsMethod);
    }

    private void ResolveSteamTypes()
    {
        steamUserStatsType = FindType("Steamworks.SteamUserStats");
        steamManagerType = FindType("SteamManager");

        if (steamUserStatsType != null)
        {
            requestCurrentStatsMethod = steamUserStatsType.GetMethod("RequestCurrentStats", BindingFlags.Public | BindingFlags.Static);
            setAchievementMethod = steamUserStatsType.GetMethod("SetAchievement", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            getAchievementMethod = FindGetAchievementMethod(steamUserStatsType);
            setStatIntMethod = steamUserStatsType.GetMethod("SetStat", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null);
            storeStatsMethod = steamUserStatsType.GetMethod("StoreStats", BindingFlags.Public | BindingFlags.Static);
        }

        if (steamManagerType != null)
        {
            steamManagerInitializedProperty = steamManagerType.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Static);
            steamManagerInitializedField = steamManagerType.GetField("Initialized", BindingFlags.Public | BindingFlags.Static);
        }
    }

    private static MethodInfo FindGetAchievementMethod(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "GetAchievement")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(bool).MakeByRefType())
            {
                return method;
            }
        }

        return null;
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private bool IsSteamManagerInitialized()
    {
        if (steamManagerType == null)
        {
            return true;
        }

        try
        {
            if (steamManagerInitializedProperty != null)
            {
                return (bool)steamManagerInitializedProperty.GetValue(null);
            }

            if (steamManagerInitializedField != null)
            {
                return (bool)steamManagerInitializedField.GetValue(null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Achievements] Failed to read SteamManager.Initialized: {ex.Message}");
            return false;
        }

        return true;
    }

    private bool IsAchievementAlreadyUnlocked(string platformAchievementId)
    {
        if (getAchievementMethod == null)
        {
            return false;
        }

        try
        {
            object[] args = { platformAchievementId, false };
            bool success = (bool)getAchievementMethod.Invoke(null, args);
            return success && args[1] is bool unlocked && unlocked;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Achievements] Steam GetAchievement failed for '{platformAchievementId}': {ex.Message}");
            return false;
        }
    }

    private static bool TryInvokeBool(MethodInfo method, params object[] args)
    {
        if (method == null)
        {
            return false;
        }

        try
        {
            object result = method.Invoke(null, args);
            return result is bool boolResult ? boolResult : true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Achievements] Steam call '{method.Name}' failed: {ex.Message}");
            return false;
        }
    }
}
