using UnityEngine;
using System.Collections.Generic;

public class BattleStatisticsManager : MonoBehaviour
{
    public static BattleStatisticsManager Instance { get; private set; }

    // --- 统计数据 ---
    public float StartTime { get; private set; }
    public int TotalKills { get; private set; }
    public int TotalGoldEarned { get; private set; }

    // 字典：武器名称 -> 造成的总伤害
    public Dictionary<string, int> WeaponDamageStats = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        StartTime = Time.time;
    }

    public float GetSurvivalTime()
    {
        return Time.time - StartTime;
    }

    // 1. 记录击杀 (在 Health.Die 调用)
    public void AddKill()
    {
        TotalKills++;
    }

    // 2. 记录金币 (在 GoldPickup.Collect 调用)
    public void AddGold(int amount)
    {
        TotalGoldEarned += amount;
    }

    // 3. 记录伤害 (核心：在 WeaponPart/Projectile 造成伤害时调用)
    public void AddDamage(string weaponName, int damage)
    {
        if (string.IsNullOrEmpty(weaponName)) return;

        if (!WeaponDamageStats.ContainsKey(weaponName))
        {
            WeaponDamageStats[weaponName] = 0;
        }
        WeaponDamageStats[weaponName] += damage;
    }
}