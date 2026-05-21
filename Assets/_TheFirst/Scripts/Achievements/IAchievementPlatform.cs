public interface IAchievementPlatform
{
    bool IsAvailable { get; }
    void Initialize();
    bool RefreshAvailability();
    bool UnlockAchievement(string platformAchievementId);
    bool SetStat(string platformStatId, int value);
    void Flush();
}
