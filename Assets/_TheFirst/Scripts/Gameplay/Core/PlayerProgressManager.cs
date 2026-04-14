using UnityEngine;
using System.Collections.Generic;
using System.IO; // Required for file operations
using System.Linq; // Required for HashSet to List conversion


public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    // 定义物品解锁事件
    public static event System.Action<string> OnItemUnlocked;

    [Header("全局配置引用")]   
    public List<WeaponSkillTree> allSkillTrees;

    [Header("玩家数据")]
    public int startingGold = 1000; // 给予一些初始金币用于测试
    public int currentGold;

    public Dictionary<string, int> progressStats = new Dictionary<string, int>();
    public Dictionary<string, int> achievementStats = new Dictionary<string, int>();

    public List<string> unlockedItems = new List<string>();
    // 缓存存档中读取的角色选择（解决 Awake 时序问题，DataManager 可能晚于 PPM 初始化）
    [HideInInspector] public string savedSelectedCharacterID;
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
    public float permanentDamagePercentBonus = 0f;
    public float permanentFireRateBonus = 0f;

    [Header("角色技能树永久加成")]
    public int permanentMaxHealthBonus = 0;
    public float permanentArmorBonus = 0f;
    public float permanentMoveSpeedBonus = 0f;
    public float permanentCooldownReduction = 0f;
    public float permanentEnergyGainBonus = 0f;
    public float permanentLifeStealPercent = 0f;
    public float permanentCharDamagePercentBonus = 0f; // 角色技能树的攻击力百分比加成（独立于武器技能树）

    [System.Serializable]
    private class SaveData
    {
        public int savedGold;
        public List<string> savedUnlockedNodeIDs;
        public int savedFlatDamageBonus;
        public int savedMeleeAoeFlatDamageBonus;
        public float savedDamagePercentBonus;
        public float savedFireRateBonus;

        // --- 【移除】角色技能树属性不再保存数值，改为从已解锁节点动态计算 ---
        // 删除 savedMaxHealthBonus, savedArmorBonus 等字段

        // --- 【新增】保存物品解锁进度 ---
        public List<string> savedUnlockedItems;

        // --- 【新增】保存成就计数 (字典拆分为两个List保存) ---
        public List<string> savedStatKeys;
        public List<int> savedStatValues;

        // --- 【新增】保存当前选中的角色ID ---
        public string savedSelectedCharacterID;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        ApplyNodeEffects(node);

        // --- [MODIFIED] ---
        // After applying effects, we immediately save the progress.
        SaveGame();
    }



    private void ApplyNodeEffects(WeaponUpgradeNode node)
    {
        foreach (var effect in node.effects)
        {
            switch (effect.upgradeType)
            {
                case PermanentUpgradeType.FlatDamage:
                    permanentFlatDamageBonus += (int)effect.value;
                    break;

                case PermanentUpgradeType.MeleeAoeFlatDamage:
                    permanentMeleeAoeFlatDamageBonus += (int)effect.value;
                    break;

                case PermanentUpgradeType.DamagePercent:
                    permanentDamagePercentBonus += effect.value;
                    break;

                case PermanentUpgradeType.FireRatePercent:
                    permanentFireRateBonus += effect.value;
                    break;

                // --- 在这里处理“刃气”等机制性解锁 ---
                case PermanentUpgradeType.UnlockBladeEnergyProjectile:
                    // 这种“开关”型的解锁，我们已经在 PlayerProgressManager 中通过 IsNodeUnlocked(node) 记录了
                    // 战斗逻辑脚本可以直接查询 PlayerProgressManager.Instance.IsNodeUnlocked(...)
                    break;

                    // ... 其他 case ...

                // --- 角色技能树属性 ---
                case PermanentUpgradeType.MaxHealthFlat:
                    permanentMaxHealthBonus += (int)effect.value;
                    break;
                case PermanentUpgradeType.ArmorFlat:
                    permanentArmorBonus += effect.value;
                    break;
                case PermanentUpgradeType.MoveSpeedPercent:
                    permanentMoveSpeedBonus += effect.value;
                    break;
                case PermanentUpgradeType.CooldownReductionPercent:
                    permanentCooldownReduction += effect.value;
                    break;
                case PermanentUpgradeType.EnergyGainPercent:
                    permanentEnergyGainBonus += effect.value;
                    break;
                case PermanentUpgradeType.LifeStealPercent:
                    permanentLifeStealPercent += effect.value;
                    break;
            }
        }
    }

    // --- 角色技能树节点管理 ---

    /// <summary>
    /// 检查角色技能节点是否已解锁
    /// </summary>
    public bool IsCharacterNodeUnlocked(CharacterSkillNode node)
    {
        if (node == null) return false;
        return unlockedNodeIDs.Contains(node.name);
    }

    /// <summary>
    /// 解锁角色技能节点
    /// </summary>
    public void UnlockCharacterNode(CharacterSkillNode node)
    {
        if (node == null || IsCharacterNodeUnlocked(node)) return;

        unlockedNodeIDs.Add(node.name);
        
        // 【修改】不再直接累加效果，而是重新计算当前角色的全部加成
        // 获取当前角色数据
        CharacterData currentChar = null;
        if (DataManager.Instance != null)
        {
            currentChar = DataManager.Instance.selectedCharacter;
        }
        RecalculateCharacterBonuses(currentChar);

        SaveGame();
    }

    private void ApplyCharacterNodeEffect(PermanentUpgradeEffect effect)
    {
        switch (effect.upgradeType)
        {
            case PermanentUpgradeType.DamagePercent:
                permanentCharDamagePercentBonus += effect.value; // 累加到角色技能树专属字段
                break;
            case PermanentUpgradeType.MaxHealthFlat:
                permanentMaxHealthBonus += (int)effect.value;
                break;
            case PermanentUpgradeType.ArmorFlat:
                permanentArmorBonus += effect.value;
                break;
            case PermanentUpgradeType.MoveSpeedPercent:
                permanentMoveSpeedBonus += effect.value;
                break;
            case PermanentUpgradeType.CooldownReductionPercent:
                permanentCooldownReduction += effect.value;
                break;
            case PermanentUpgradeType.EnergyGainPercent:
                permanentEnergyGainBonus += effect.value;
                break;
            case PermanentUpgradeType.LifeStealPercent:
                permanentLifeStealPercent += effect.value;
                break;
        }
    }

    /// <summary>
    /// 【核心新增】根据指定角色的已解锁节点重新计算角色技能树加成
    /// 解决了不同角色共享同一组属性的问题
    /// </summary>
    public void RecalculateCharacterBonuses(CharacterData forCharacter)
    {
        // 清零角色技能树属性
        permanentMaxHealthBonus = 0;
        permanentArmorBonus = 0f;
        permanentMoveSpeedBonus = 0f;
        permanentCooldownReduction = 0f;
        permanentEnergyGainBonus = 0f;
        permanentLifeStealPercent = 0f;
        permanentCharDamagePercentBonus = 0f; // 清零角色技能树攻击力加成

        if (forCharacter == null || forCharacter.characterSkillNodes == null) return;

        // 只累加当前角色已解锁节点的效果
        foreach (var node in forCharacter.characterSkillNodes)
        {
            if (node != null && IsCharacterNodeUnlocked(node))
            {
                foreach (var effect in node.effects)
                {
                    ApplyCharacterNodeEffect(effect);
                }
            }
        }
    }

    /// <summary>
    /// 获取指定层级已解锁的节点数量
    /// </summary>
    public int GetUnlockedCountInLayer(CharacterData charData, int layer)
    {
        if (charData == null || charData.characterSkillNodes == null) return 0;
        int count = 0;
        foreach (var node in charData.characterSkillNodes)
        {
            if (node != null && node.layer == layer && IsCharacterNodeUnlocked(node))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 获取指定层级的总节点数
    /// </summary>
    public int GetTotalCountInLayer(CharacterData charData, int layer)
    {
        if (charData == null || charData.characterSkillNodes == null) return 0;
        int count = 0;
        foreach (var node in charData.characterSkillNodes)
        {
            if (node != null && node.layer == layer)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 检查某节点是否满足解锁条件（前置层级要求）
    /// 第1层：无前置
    /// 第2-3层：前一层解锁2个以上
    /// 第4层（天赋）：前一层全部解锁
    /// </summary>
    public bool CanUnlockCharacterNode(CharacterData charData, CharacterSkillNode node)
    {
        if (node == null) return false;
        if (IsCharacterNodeUnlocked(node)) return false; // 已解锁

        // 无前置节点 → 可直接解锁
        if (node.prerequisites == null || node.prerequisites.Count == 0)
            return true;

        // 所有前置节点必须已解锁
        foreach (var prereq in node.prerequisites)
        {
            if (prereq == null) continue;
            if (!IsCharacterNodeUnlocked(prereq))
                return false;
        }
        return true;
    }

    public void AddStat(string statKey, int amount)
    {
        // 1. 增加数值
        if (!achievementStats.ContainsKey(statKey)) achievementStats[statKey] = 0;
        achievementStats[statKey] += amount;

        // 【关键调试】看看有没有真的在涨
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
            SaveGame(); // 记得保存！

            // 触发解锁事件
            OnItemUnlocked?.Invoke(itemName);
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

        // --- 【修改】角色技能树属性不再保存数值，从已解锁节点动态计算 ---

        // --- 【新增】保存新数据 ---
        data.savedUnlockedItems = this.unlockedItems;

        // 把字典拆开存
        data.savedStatKeys = new List<string>(this.achievementStats.Keys);
        data.savedStatValues = new List<int>(this.achievementStats.Values);

        // --- 【新增】保存当前选中的角色 ---
        if (DataManager.Instance != null)
            data.savedSelectedCharacterID = DataManager.Instance.selectedCharacterID;
        // -------------------------

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSaveFilePath(), json);

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

            // --- 【修改】角色技能树属性不从存档加载，改为动态计算 ---
            // 先清零，后续由 RecalculateCharacterBonuses 根据当前角色重新计算
            this.permanentMaxHealthBonus = 0;
            this.permanentArmorBonus = 0f;
            this.permanentMoveSpeedBonus = 0f;
            this.permanentCooldownReduction = 0f;
            this.permanentEnergyGainBonus = 0f;
            this.permanentLifeStealPercent = 0f;
            this.permanentCharDamagePercentBonus = 0f; // 角色技能树攻击力加成也动态计算

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

            // --- 【新增】恢复上次选中的角色 ---
            // 先存到自身字段，因为 DataManager 可能尚未初始化
            this.savedSelectedCharacterID = data.savedSelectedCharacterID;
            if (DataManager.Instance != null && !string.IsNullOrEmpty(data.savedSelectedCharacterID))
            {
                DataManager.Instance.selectedCharacterID = data.savedSelectedCharacterID;
            }

        }
        else
        {
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

        // 角色技能树属性
        permanentMaxHealthBonus = 0;
        permanentArmorBonus = 0f;
        permanentMoveSpeedBonus = 0f;
        permanentCooldownReduction = 0f;
        permanentEnergyGainBonus = 0f;
        permanentLifeStealPercent = 0f;
        permanentCharDamagePercentBonus = 0f;

        // 【新增】清除角色解锁和成就数据
        unlockedItems.Clear();
        achievementStats.Clear();
        savedSelectedCharacterID = null;
    }

    private void ValidateRetroactiveUnlocks()
    {
        if (allSkillTrees == null) return;

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
        }
        else
        {
        }

        // 删除文件后，立刻将内存中的数据也重置
        ResetProgressToDefault();

        // 同步清除 DataManager 中的角色选择
        if (DataManager.Instance != null)
        {
            DataManager.Instance.selectedCharacterID = null;
            DataManager.Instance.selectedCharacter = null;
        }

        // 如果UI在主菜单可见，立即更新
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }
}