using UnityEngine;
using System.Collections.Generic;
using System.IO; // Required for file operations
using System.Linq; // Required for HashSet to List conversion


public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    [Header("全局配置引用")]   
    public List<WeaponSkillTree> allSkillTrees;

    [Header("玩家数据")]
    public int startingGold = 1000; // 给予一些初始金币用于测试
    public int currentGold;

    public Dictionary<string, int> progressStats = new Dictionary<string, int>();
    public Dictionary<string, int> achievementStats = new Dictionary<string, int>();

    public List<string> unlockedItems = new List<string>();
    // 使用一个HashSet来存储已解锁节点的ID (也就是它们的ScriptableObject文件名)
    // HashSet的查找速度非常快
    private HashSet<string> unlockedNodeIDs = new HashSet<string>();

    public bool IsNodeUnlockedRaw(string nodeID)
    {
        return unlockedNodeIDs.Contains(nodeID);
    }

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

        // --- 【新增】保存物品解锁进度 ---
        public List<string> savedUnlockedItems;

        // --- 【新增】保存成就计数 (字典拆分为两个List保存) ---
        public List<string> savedStatKeys;
        public List<int> savedStatValues;
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
        ValidateRetroactiveUnlocks();
        // 游戏一开始，就更新一次金币显示

        int currentIgnite = achievementStats.ContainsKey("Ignite_Count") ? achievementStats["Ignite_Count"] : 0;
        Debug.Log($"[存档检查] 游戏启动。当前 Ignite_Count: {currentIgnite}");
        Debug.Log($"[存档检查] 当前 unlockedItems 数量: {unlockedItems.Count}");

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

    public void AddStat(string statKey, int amount)
    {
        // 1. 增加数值
        if (!achievementStats.ContainsKey(statKey)) achievementStats[statKey] = 0;
        achievementStats[statKey] += amount;

        // 【关键调试】看看有没有真的在涨
        Debug.Log($"[统计] {statKey}: {achievementStats[statKey]}");

        // 2. 【核心】检查是否达到解锁条件
        CheckUnlocks(statKey);

        // 3. 【核心】保存！如果不保存，下次进游戏进度就丢了
        // 如果觉得每次加都保存太耗性能，可以放到 CheckUnlocks 里去保存，或者关卡结束保存
        // 但为了测试，先在这里保存
        // SaveGame(); 
    }

    public void IncreaseAchievementStat(string statKey, int amount = 1)
    {
        // 1. 更新字典数据
        if (achievementStats.ContainsKey(statKey))
        {
            achievementStats[statKey] += amount;
        }
        else
        {
            achievementStats.Add(statKey, amount);
        }

        Debug.Log($"[成就记录] {statKey} 进度增加! 当前总计: {achievementStats[statKey]}");

        // 2. 【关键】立即保存进度，确保解锁条件被写入磁盘
        SaveGame();
    }
    public int GetStat(string key)
    {
        return progressStats.ContainsKey(key) ? progressStats[key] : 0;
    }
    private void CheckUnlocks(string changedStatKey)
    {
        if (allSkillTrees == null) return;

        foreach (var tree in allSkillTrees)
        {
            // 1. 如果这个树已经解锁了，跳过
            // (注意：这里假设 weaponID 是解锁凭证)
            string id = tree.associatedWeapon.weaponID;
            if (unlockedItems.Contains(id)) continue;

            // 2. 检查这个树是否关心当前变化的 StatKey
            // 比如 ignite_count 变了，我们只检查关心点燃的树
            if (tree.unlockStatKey == changedStatKey)
            {
                // 3. 检查数值是否达标 (直接读配置里的 Threshold!)
                int currentVal = achievementStats.ContainsKey(changedStatKey) ? achievementStats[changedStatKey] : 0;

                if (currentVal >= tree.unlockThreshold)
                {
                    Debug.Log($"[成就达成] {tree.name} 条件满足！自动解锁: {id}");
                    UnlockItem(id);
                }
            }
        }
    }

    public void UnlockItem(string itemName)
    {
        if (!unlockedItems.Contains(itemName))
        {
            unlockedItems.Add(itemName);
            Debug.Log($"<color=yellow>新物品解锁: {itemName}!</color>");
            SaveGame(); // 记得保存！
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

        data.savedGold = this.currentGold;
        data.savedUnlockedNodeIDs = this.unlockedNodeIDs.ToList();
        data.savedFlatDamageBonus = this.permanentFlatDamageBonus;
        data.savedMeleeAoeFlatDamageBonus = this.permanentMeleeAoeFlatDamageBonus;
        data.savedDamagePercentBonus = this.permanentDamagePercentBonus;
        data.savedFireRateBonus = this.permanentFireRateBonus;

        // --- 【新增】保存新数据 ---
        data.savedUnlockedItems = this.unlockedItems;

        // 把字典拆开存
        data.savedStatKeys = new List<string>(this.achievementStats.Keys);
        data.savedStatValues = new List<int>(this.achievementStats.Values);
        // -------------------------

        string json = JsonUtility.ToJson(data, true);
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

            this.currentGold = data.savedGold;
            this.unlockedNodeIDs = new HashSet<string>(data.savedUnlockedNodeIDs);
            this.permanentFlatDamageBonus = data.savedFlatDamageBonus;
            this.permanentMeleeAoeFlatDamageBonus = data.savedMeleeAoeFlatDamageBonus;
            this.permanentDamagePercentBonus = data.savedDamagePercentBonus;
            this.permanentFireRateBonus = data.savedFireRateBonus;

            // --- 【新增】读取物品解锁 ---
            if (data.savedUnlockedItems != null)
            {
                this.unlockedItems = data.savedUnlockedItems;
            }
            else
            {
                this.unlockedItems = new List<string>();
            }

            // --- 【新增】读取成就计数 (组装回字典) ---
            this.achievementStats = new Dictionary<string, int>();
            if (data.savedStatKeys != null && data.savedStatValues != null)
            {
                // 确保 key 和 value 数量一致，防止报错
                int count = Mathf.Min(data.savedStatKeys.Count, data.savedStatValues.Count);
                for (int i = 0; i < count; i++)
                {
                    this.achievementStats[data.savedStatKeys[i]] = data.savedStatValues[i];
                }
            }
            // ----------------------------------------

            Debug.Log("<color=yellow>游戏进度已从 " + path + " 加载。</color>");
        }
        else
        {
            Debug.Log("未找到存档文件，将以初始状态开始游戏。");
            ResetProgressToDefault(); // 建议加上这个初始化调用
        }
        ValidateRetroactiveUnlocks();
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

    private void ValidateRetroactiveUnlocks()
    {
        if (allSkillTrees == null) return;

        Debug.Log("[存档体检] 开始根据 SkillTree 配置进行全量检查...");

        foreach (var tree in allSkillTrees)
        {
            // 1. 跳过默认解锁的
            if (tree.isDefaultUnlocked) continue;

            string id = tree.associatedWeapon.weaponID;

            // 2. 如果已经解锁了，跳过
            if (unlockedItems.Contains(id)) continue;

            // 3. 检查条件是否存在
            if (string.IsNullOrEmpty(tree.unlockStatKey)) continue;

            // 4. 获取当前进度
            int currentVal = 0;
            if (achievementStats.ContainsKey(tree.unlockStatKey))
            {
                currentVal = achievementStats[tree.unlockStatKey];
            }

            // 5. 【核心】直接对比配置里的 Threshold
            if (currentVal >= tree.unlockThreshold)
            {
                Debug.LogWarning($"[自动修复] 发现 {id} 条件已达标 ({currentVal}/{tree.unlockThreshold}) 但未解锁，正在补票...");
                UnlockItem(id);
            }
        }
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