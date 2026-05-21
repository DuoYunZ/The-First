using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Debug_TestManager : MonoBehaviour
{
    [Header("敌人预制件列表 (用于生成)")]
    public List<EnemyType> enemyTypes;

    [Header("武器数据列表 (用于直接给予)")]
    public List<WeaponStatBlock> weaponStatBlocks;

    [Header("护盾数据列表 (用于直接给予)")]
    public List<ShieldData> shieldDataAssets;

    [Header("【新增】被动道具列表 (用于直接给予)")]
    public List<PassiveItemData> passiveItems; // <--- 新增列表

    [Header("升级节点列表 (用于强制升级)")]
    public List<SkillTreeNodeData> allSkillNodes;

    [Header("调试数值")]
    [Tooltip("每次点击“加经验”按钮时给予的经验值")]
    public int xpToAdd = 20;

    [Header("Build Test Kits")]
    [Tooltip("Play mode debug panel for deterministic build testing.")]
    public bool showBuildTestGui = true;

    private Transform playerTransform;
    private EnemySpawner enemySpawner;

#if UNITY_EDITOR
    private static readonly string[] BuildTestWeaponPaths =
    {
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Blade.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Fireball.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_FrostNova.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_IceShard.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Grenade.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_ChainLightning.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_LightningStrike.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Landmine.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Orbit.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_Laser_Tank.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/SO_SuperMech.asset",
        "Assets/_TheFirst/GameData/SO_Weapon/discard/SO_FlameTurret.asset"
    };

    private static readonly string[] BuildTestPassivePaths =
    {
        "Assets/_TheFirst/Prefabs/Passive Item Data/SwordmasterSoul_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/ArcaneMastery_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/ElementalResonance_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/MechanicalResonance_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/ATK_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/CD_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Fanwei_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Duration_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Pierce_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Cili_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Speed_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/HP_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Armor_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/Lucky_Data.asset",
        "Assets/_TheFirst/Prefabs/Passive Item Data/XPGain_Data.asset"
    };
#endif

    void OnValidate()
    {
#if UNITY_EDITOR
        EnsureBuildTestAssets();
#endif
    }

    void Start()
    {
#if UNITY_EDITOR
        EnsureBuildTestAssets();
#endif

        enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner == null)
        {
            Debug.LogError("Debug_TestManager 未能在场景中找到 EnemySpawner！");
        }

        Invoke("GetPlayerReference", 0.2f);
    }

    void GetPlayerReference()
    {
        if (GameManager.Instance != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
    }

    void OnGUI()
    {
        if (!showBuildTestGui || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 120, 260, 390), GUI.skin.box);
        GUILayout.Label("Build Test Kits");

        if (GUILayout.Button("Sword: Blade + Soul Lv3"))
        {
            GiveSwordmasterTestKit();
        }

        if (GUILayout.Button("Arcane: Mastery Lv5"))
        {
            GiveArcaneTestKit();
        }

        if (GUILayout.Button("Elemental: Fire/Ice/Thunder + Resonance Lv3"))
        {
            GiveElementalTestKit();
        }

        if (GUILayout.Button("Mechanical: Mine/Orbit/Laser + Resonance Lv3"))
        {
            GiveMechanicalTestKit();
        }

        if (GUILayout.Button("Engineer: Role Prototype Kit"))
        {
            GiveEngineerPrototypeKit();
        }

        if (GUILayout.Button("Old Passives: Milestone Kit"))
        {
            GiveOldPassiveMilestoneKit();
        }

        if (GUILayout.Button("All Build Passives +1"))
        {
            GiveAllBuildPassivesOnce();
        }

        if (GUILayout.Button("Add XP Once"))
        {
            AddExperience();
        }

        if (GUILayout.Button("Spawn Enemy Pack"))
        {
            SpawnEnemyPack();
        }

        if (GUILayout.Button("Log Player Build Stats"))
        {
            LogCurrentBuildStats("Manual check");
        }

        GUILayout.EndArea();
    }

    public void GiveSwordmasterTestKit()
    {
        GiveWeaponByAssetName("SO_Blade");
        GivePassiveByAssetName("SwordmasterSoul_Data", 3);
        LogCurrentBuildStats("Swordmaster kit");
    }

    public void GiveArcaneTestKit()
    {
        GivePassiveByAssetName("ArcaneMastery_Data", 5);
        LogCurrentBuildStats("Arcane kit");
    }

    public void GiveElementalTestKit()
    {
        GiveWeaponByAssetName("SO_Fireball");
        GiveWeaponByAssetName("SO_FrostNova");
        GiveWeaponByAssetName("SO_ChainLightning");
        GivePassiveByAssetName("ElementalResonance_Data", 3);
        LogCurrentBuildStats("Elemental kit");
    }

    public void GiveMechanicalTestKit()
    {
        GiveWeaponByAssetName("SO_Landmine");
        GiveWeaponByAssetName("SO_Orbit");
        GiveWeaponByAssetName("SO_Laser_Tank");
        GivePassiveByAssetName("MechanicalResonance_Data", 3);
        LogCurrentBuildStats("Mechanical kit");
    }

    public void GiveEngineerPrototypeKit()
    {
        SelectEngineerForNextRun();

        GiveWeaponByAssetName("SO_Landmine");
        GiveWeaponByAssetName("SO_Orbit");
        GiveWeaponByAssetName("SO_Laser_Tank");
        GiveWeaponByAssetName("SO_FlameTurret");
        GivePassiveByAssetName("MechanicalResonance_Data", 3);

        ForceActivateCharacterSkill("EngineerFortress");
        ForceActivateCharacterSkill("Engineer_Fortress_Minefield");
        ForceActivateCharacterSkill("Engineer_Fortress_AutoTurret");
        ForceActivateCharacterSkill("Engineer_Overclock_LaserGrid");
        ForceActivateCharacterSkill("Engineer_Overclock_RotorArray");
        ForceActivateCharacterSkill("Engineer_Talent_AssemblyLine");

        if (WeaponController.Instance != null)
        {
            WeaponController.Instance.RefreshAllWeaponStates();
        }

        LogCurrentBuildStats("Engineer prototype kit");
    }

    public void GiveAllBuildPassivesOnce()
    {
        GivePassiveByAssetName("SwordmasterSoul_Data", 1);
        GivePassiveByAssetName("ArcaneMastery_Data", 1);
        GivePassiveByAssetName("ElementalResonance_Data", 1);
        GivePassiveByAssetName("MechanicalResonance_Data", 1);
        LogCurrentBuildStats("All build passives +1");
    }

    public void GiveOldPassiveMilestoneKit()
    {
        GiveWeaponByAssetName("SO_Fireball");
        GiveWeaponByAssetName("SO_IceShard");
        GiveWeaponByAssetName("SO_Grenade");
        GiveWeaponByAssetName("SO_Orbit");

        GivePassiveByAssetName("CD_Data", 5);
        GivePassiveByAssetName("Fanwei_Data", 5);
        GivePassiveByAssetName("Duration_Data", 5);
        GivePassiveByAssetName("Pierce_Data", 3);
        GivePassiveByAssetName("Cili_Data", 5);
        GivePassiveByAssetName("Speed_Data", 5);
        LogCurrentBuildStats("Old passive milestone kit");
    }

    public void SpawnEnemyPack()
    {
        if (enemyTypes == null || enemyTypes.Count == 0)
        {
            Debug.LogError("Debug_TestManager: enemyTypes is empty.");
            return;
        }

        for (int i = 0; i < 10; i++)
        {
            SpawnEnemy(i % enemyTypes.Count);
        }
    }

    // --- 公共方法，将由UI按钮调用 ---

    /// <summary>
    /// 生成一个指定的敌人
    /// </summary>
    public void SpawnEnemy(int enemyIndex)
    {
        if (enemySpawner == null)
        {
            Debug.LogError("EnemySpawner 引用为空，无法生成敌人！");
            return;
        }
        if (enemyIndex < 0 || enemyIndex >= enemyTypes.Count)
        {
            Debug.LogError($"无效的敌人索引: {enemyIndex}");
            return;
        }

        // 直接命令 EnemySpawner 使用我们选择的 EnemyType 来生成敌人
        enemySpawner.Debug_SpawnSingleEnemy(enemyTypes[enemyIndex]);

    }

    /// <summary>
    /// 【新增】为玩家增加经验值，用于快速触发升级
    /// </summary>
    public void AddExperience()
    {
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.AddExperience(xpToAdd);
        }
        else
        {
            Debug.LogError("PlayerLevelManager 未找到！");
        }
    }

    /// <summary>
    /// 【新增】直接给予玩家一件指定的武器
    /// </summary>
    public void GiveWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weaponStatBlocks.Count)
        {
            Debug.LogError($"无效的武器索引: {weaponIndex}");
            return;
        }

        if (WeaponController.Instance != null)
        {
            WeaponController.Instance.AddNewWeapon(weaponStatBlocks[weaponIndex]);
        }
        else
        {
            Debug.LogError("WeaponController 未找到！");
        }
    }
    public void GivePassiveItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= passiveItems.Count)
        {
            Debug.LogError($"无效的被动道具索引: {itemIndex}");
            return;
        }

        if (PlayerStats.Instance != null)
        {
            // 直接调用 PlayerStats 的装备方法
            PlayerStats.Instance.EquipOrUpgradePassiveItem(passiveItems[itemIndex]);
            // 顺便刷新一下UI，确保图标立刻出现
            if (PassiveItemsUI.Instance != null)
            {
                PassiveItemsUI.Instance.UpdateIcons();
            }
        }
        else
        {
            Debug.LogError("PlayerStats 未找到！");
        }
    }
    public void ForceRandomUpgrade(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= allSkillNodes.Count)
        {
            Debug.LogError($"无效的技能节点索引: {nodeIndex}");
            return;
        }

        SkillTreeNodeData nodeToGrant = allSkillNodes[nodeIndex];

        if (UpgradeManager.Instance != null && PlayerStats.Instance != null)
        {
            // 1. 进行一次真实的随机抽取
            float playerLuck = PlayerStats.Instance.luck;
            UpgradeOption chosenOption = RaritySystem.GetRandomOptionByRarity(nodeToGrant.possibleOptions, playerLuck);

            if (chosenOption != null)
            {
                // 2. 将抽到的结果应用到游戏中
                UpgradeManager.Instance.OnUpgradeOptionSelected(nodeToGrant, chosenOption);

                // 3. 【新增反馈】将结果记录到我们的调试UI上
                if (Debug_UpgradeStats.Instance != null)
                {
                    // 构造一条清晰的反馈信息
                    string message = $"<color=yellow>强制升级【{nodeToGrant.skillName}】:</color>\n" +
                                     $"品质: <b>{chosenOption.rarity}</b>, 效果: <i>{chosenOption.description}</i>";

                    Debug_UpgradeStats.Instance.SetLastUpgradeMessage(message);
                    Debug_UpgradeStats.Instance.LogRarity(chosenOption.rarity); // 同时计入概率统计
                }

            }
            else
            {
                if (Debug_UpgradeStats.Instance != null)
                {
                    Debug_UpgradeStats.Instance.SetLastUpgradeMessage($"<color=red>为【{nodeToGrant.skillName}】抽卡失败！</color>");
                }
            }
        }
    }
    public void GiveShield(int shieldIndex)
    {
        if (shieldIndex < 0 || shieldIndex >= shieldDataAssets.Count)
        {
            Debug.LogError($"无效的护盾索引: {shieldIndex}");
            return;
        }

        if (PlayerShield.Instance != null)
        {
            // 调用 PlayerShield 的 EquipShield 方法
            PlayerShield.Instance.EquipShield(shieldDataAssets[shieldIndex]);
        }
        else
        {
            Debug.LogError("PlayerShield 未找到！");
        }
    }

    public void TestFusion()
    {
        if (WeaponController.Instance != null)
        {
            var recipe = WeaponController.Instance.CheckForAvailableFusion();
            if (recipe != null)
            {
                WeaponController.Instance.PerformFusion(recipe);
            }
            else
            {
            }
        }
    }

    private void ForceActivateCharacterSkill(string skillIdentifier)
    {
        if (UpgradeManager.Instance == null)
        {
            Debug.LogWarning($"[BuildTest] UpgradeManager not ready, cannot activate {skillIdentifier}.");
            return;
        }

        UpgradeManager.Instance.ForceActivateCharacterSkill(skillIdentifier);
    }

    private void SelectEngineerForNextRun()
    {
#if UNITY_EDITOR
        CharacterData engineer = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_TheFirst/GameData/Character/Role03_Data.asset");
        if (engineer == null)
        {
            Debug.LogWarning("[BuildTest] Engineer CharacterData not generated yet. Run Tools/TheFirst/Generate Engineer Prototype.");
            return;
        }

        if (DataManager.Instance != null)
        {
            DataManager.Instance.selectedCharacter = engineer;
            DataManager.Instance.selectedCharacterID = engineer.characterID;
            if (DataManager.Instance.allCharacters != null && !DataManager.Instance.allCharacters.Contains(engineer))
            {
                DataManager.Instance.allCharacters.Add(engineer);
            }
            Debug.Log("[BuildTest] Selected Pumpkin Engineer for the next combat spawn.");
        }
#endif
    }

    private void GiveWeaponByAssetName(string assetName)
    {
        WeaponStatBlock weapon = FindWeapon(assetName);
        if (weapon == null)
        {
            Debug.LogError($"Debug_TestManager: weapon not found in test list: {assetName}");
            return;
        }

        if (WeaponController.Instance == null)
        {
            Debug.LogError("WeaponController 未找到！");
            return;
        }

        WeaponController.Instance.AddNewWeapon(weapon);
    }

    private void GivePassiveByAssetName(string assetName, int levels)
    {
        PassiveItemData passive = FindPassive(assetName);
        if (passive == null)
        {
            Debug.LogError($"Debug_TestManager: passive not found in test list: {assetName}");
            return;
        }

        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PlayerStats 未找到！");
            return;
        }

        int count = Mathf.Max(1, levels);
        for (int i = 0; i < count; i++)
        {
            PlayerStats.Instance.EquipOrUpgradePassiveItem(passive);
        }

        if (PassiveItemsUI.Instance != null)
        {
            PassiveItemsUI.Instance.UpdateIcons();
        }
    }

    private WeaponStatBlock FindWeapon(string assetName)
    {
        if (weaponStatBlocks == null) return null;
        foreach (var weapon in weaponStatBlocks)
        {
            if (weapon != null && weapon.name == assetName) return weapon;
        }
        return null;
    }

    private PassiveItemData FindPassive(string assetName)
    {
        if (passiveItems == null) return null;
        foreach (var passive in passiveItems)
        {
            if (passive != null && passive.name == assetName) return passive;
        }
        return null;
    }

    private void LogCurrentBuildStats(string label)
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[BuildTest] {label}: PlayerStats not ready.");
            return;
        }

        PlayerStats stats = PlayerStats.Instance;
        Debug.Log($"<color=cyan>[BuildTest] {label}</color> " +
                  $"DMG={stats.damageMultiplier:F2}, Crit={stats.critRate:P0}/{stats.critDamage:P0}, Proj+={stats.bonusProjectileCount}, Pierce+={stats.bonusPierceCount}, Slash+={stats.bonusSlashCount}, " +
                  $"Arcane={stats.arcaneMasteryChance:P0}, Freeze={stats.globalFreezeChance:P0}, Thunder={stats.thunderWillChance:P0}, " +
                  $"AoE={stats.aoeRadiusMultiplier:F2}/{stats.aoeDamageMultiplier:F2}, Duration={stats.durationMultiplier:F2}, FireRate={stats.fireRateMultiplier:F2}, Speed={stats.moveSpeedMultiplier:F2}, " +
                  $"Orbital+={stats.bonusOrbitalCount}, Pickup={stats.pickupRadiusMultiplier:F2}, XP={stats.experienceGainMultiplier:F2}, Luck={stats.luck:F2}, Armor={stats.armor:F1}, HP+={stats.bonusMaxHealth}, DashBlast={stats.dashExplosionLevel}");
    }

#if UNITY_EDITOR
    private void EnsureBuildTestAssets()
    {
        if (weaponStatBlocks == null) weaponStatBlocks = new List<WeaponStatBlock>();
        if (passiveItems == null) passiveItems = new List<PassiveItemData>();

        bool changed = false;
        foreach (string path in BuildTestWeaponPaths)
        {
            changed |= AppendAssetIfMissing(weaponStatBlocks, path);
        }

        foreach (string path in BuildTestPassivePaths)
        {
            changed |= AppendAssetIfMissing(passiveItems, path);
        }

        if (changed && !Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private bool AppendAssetIfMissing<T>(List<T> list, string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) return false;

        foreach (var existing in list)
        {
            if (existing == asset) return false;
        }

        list.Add(asset);
        return true;
    }
#endif
}
