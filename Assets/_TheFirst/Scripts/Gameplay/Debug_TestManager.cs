using UnityEngine;
using System.Collections.Generic;

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

    private Transform playerTransform;
    private EnemySpawner enemySpawner;

    void Start()
    {
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
}