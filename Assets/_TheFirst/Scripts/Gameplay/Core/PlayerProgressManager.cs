using UnityEngine;
using System.Collections.Generic;
using System.IO; // Required for file operations
using System.Linq; // Required for HashSet to List conversion


public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    [Header("玩家数据")]
    public int startingGold = 1000; // 给予一些初始金币用于测试
    public int currentGold;

    // 使用一个HashSet来存储已解锁节点的ID (也就是它们的ScriptableObject文件名)
    // HashSet的查找速度非常快
    private HashSet<string> unlockedNodeIDs = new HashSet<string>();

    [Header("永久属性加成 (由技能树解锁)")]
    public int permanentFlatDamageBonus = 0;

    public int permanentMeleeAoeFlatDamageBonus = 0;

    public float permanentDamagePercentBonus = 0f; // 0.1 代表 +10%

    public float permanentFireRateBonus = 0f;      // 0.1 代表 +10%

    [System.Serializable]
    private class SaveData
    {
        public int savedGold;
        public List<string> savedUnlockedNodeIDs;
        public int savedFlatDamageBonus;
        public int savedMeleeAoeFlatDamageBonus;
        public float savedDamagePercentBonus;
        public float savedFireRateBonus;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("<color=green>PlayerProgressManager AWAKE! Instance is now SET.</color>", this);
            DontDestroyOnLoad(gameObject);

            // --- [MODIFIED] ---
            // Now we call LoadGame() here.
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 游戏一开始，就更新一次金币显示
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }
    // --- 金币管理 ---
    public bool CanAfford(int amount)
    {
        return currentGold >= amount;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        Debug.Log($"获得了 {amount} 金币，当前总计: {currentGold}");

        // 【修改】在金币增加时，更新UI显示
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }

    public void SpendGold(int amount)
    {
        currentGold -= amount;

        // 【修改】在金币花费时，更新UI显示
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }

    // --- 技能节点管理 ---
    public bool IsNodeUnlocked(WeaponUpgradeNode node)
    {
        if (node == null) return false;
        return unlockedNodeIDs.Contains(node.name);
    }

    public void UnlockNode(WeaponUpgradeNode node)
    {
        if (node == null || IsNodeUnlocked(node)) return;

        unlockedNodeIDs.Add(node.name);
        Debug.Log($"<color=yellow>已解锁新技能: {node.upgradeName}</color>");

        ApplyNodeEffects(node);

        // --- [MODIFIED] ---
        // After applying effects, we immediately save the progress.
        SaveGame();
    }



    private void ApplyNodeEffects(WeaponUpgradeNode node)
    {
        Debug.Log($"正在应用节点 '{node.upgradeName}' 的永久效果...");
        foreach (var effect in node.effects)
        {
            switch (effect.upgradeType)
            {
                case PermanentUpgradeType.FlatDamage:
                    permanentFlatDamageBonus += (int)effect.value;
                    Debug.Log($"永久固定伤害加成增加: +{(int)effect.value}，当前总计: {permanentFlatDamageBonus}");
                    break;

                case PermanentUpgradeType.MeleeAoeFlatDamage:
                    permanentMeleeAoeFlatDamageBonus += (int)effect.value;
                    Debug.Log($"永久近战范围伤害加成增加: +{(int)effect.value}，当前总计: {permanentMeleeAoeFlatDamageBonus}");
                    break;

                case PermanentUpgradeType.DamagePercent:
                    permanentDamagePercentBonus += effect.value;
                    Debug.Log($"永久百分比伤害加成增加: +{effect.value * 100}%，当前总计: {permanentDamagePercentBonus * 100}%");
                    break;

                case PermanentUpgradeType.FireRatePercent:
                    permanentFireRateBonus += effect.value;
                    Debug.Log($"永久射速加成增加: +{effect.value * 100}%，当前总计: {permanentFireRateBonus * 100}%");
                    break;

                // --- 在这里处理“刃气”等机制性解锁 ---
                case PermanentUpgradeType.UnlockBladeEnergyProjectile:
                    // 这种“开关”型的解锁，我们已经在 PlayerProgressManager 中通过 IsNodeUnlocked(node) 记录了
                    // 战斗逻辑脚本可以直接查询 PlayerProgressManager.Instance.IsNodeUnlocked(...)
                    Debug.Log("机制解锁：刃气斩！");
                    break;

                    // ... 其他 case ...
            }
        }
    }
    private string GetSaveFilePath()
    {
        // Application.persistentDataPath is a reliable, writeable directory on all platforms.
        return Path.Combine(Application.persistentDataPath, "playerProgress.json");
    }

    public void SaveGame()
    {
        SaveData data = new SaveData();

        // Populate the SaveData object from the current manager state.
        data.savedGold = this.currentGold;
        // JsonUtility cannot serialize HashSets, so we convert it to a List first.
        data.savedUnlockedNodeIDs = this.unlockedNodeIDs.ToList();
        data.savedFlatDamageBonus = this.permanentFlatDamageBonus;
        data.savedMeleeAoeFlatDamageBonus = this.permanentMeleeAoeFlatDamageBonus;
        data.savedDamagePercentBonus = this.permanentDamagePercentBonus;
        data.savedFireRateBonus = this.permanentFireRateBonus;

        string json = JsonUtility.ToJson(data, true); // 'true' for pretty formatting
        File.WriteAllText(GetSaveFilePath(), json);

        Debug.Log("<color=green>游戏进度已保存到: " + GetSaveFilePath() + "</color>");
    }

    public void LoadGame()
    {
        string path = GetSaveFilePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Restore the manager's state from the loaded SaveData object.
            this.currentGold = data.savedGold;
            // Convert the loaded List back into a HashSet for fast lookups.
            this.unlockedNodeIDs = new HashSet<string>(data.savedUnlockedNodeIDs);
            this.permanentFlatDamageBonus = data.savedFlatDamageBonus;
            this.permanentMeleeAoeFlatDamageBonus = data.savedMeleeAoeFlatDamageBonus;
            this.permanentDamagePercentBonus = data.savedDamagePercentBonus;
            this.permanentFireRateBonus = data.savedFireRateBonus;

            Debug.Log("<color=yellow>游戏进度已从 " + path + " 加载。</color>");
        }
        else
        {
            Debug.Log("未找到存档文件，将以初始状态开始游戏。");
            // No action needed, the manager will just use its default values.
        }
    }
    private void ResetProgressToDefault()
    {
        currentGold = startingGold;
        unlockedNodeIDs.Clear();
        permanentFlatDamageBonus = 0;
        permanentMeleeAoeFlatDamageBonus = 0;
        permanentDamagePercentBonus = 0f;
        permanentFireRateBonus = 0f;
    }
    public void ClearSaveData()
    {
        string path = GetSaveFilePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("<color=red>存档文件已删除: " + path + "</color>");
        }
        else
        {
            Debug.Log("无需删除，存档文件不存在。");
        }

        // 删除文件后，立刻将内存中的数据也重置
        ResetProgressToDefault();

        // （可选）如果UI在主菜单可见，立即更新
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }
}