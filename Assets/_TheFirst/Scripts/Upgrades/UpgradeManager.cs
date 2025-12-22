using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("UI引用")]
    public GameObject upgradePanel;
    public Transform cardContainer;

    [Header("卡片预制件库 (按品质)")]
    public GameObject commonCardPrefab;
    public GameObject uncommonCardPrefab;
    public GameObject rareCardPrefab;
    public GameObject epicCardPrefab;
    public GameObject unlockCardPrefab;

    [Header("升级数据库")]
    public UpgradeDatabase upgradeDatabase;

    [Header("动画设置")] // 【新增】
    [Tooltip("每张卡片出现的间隔时间（秒）")]
    public float delayBetweenCards = 0.2f; // 【新增】
    // 记录玩家已拥有的技能节点及其当前等级
    private Dictionary<SkillTreeNodeData, int> ownedUpgrades = new Dictionary<SkillTreeNodeData, int>();

    // 用于存储本次为玩家提供的三个“升级机会”
    private List<SkillTreeNodeData> offeredUpgrades = new List<SkillTreeNodeData>();
    private List<UpgradeCardUI> activeCardUIs = new List<UpgradeCardUI>(); // 【新增】用于存储当前卡片实例

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp += HandlePlayerLevelUp;
        }
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp -= HandlePlayerLevelUp;
        }
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        Time.timeScale = 0f;
        offeredUpgrades.Clear();

        // --- 【核心修改】步骤 1: 优先检查进化 ---
        EvolutionRecipeSO evolutionRecipe = null;
        if (WeaponController.Instance != null)
        {
            evolutionRecipe = WeaponController.Instance.GetPendingEvolution();
        }

        if (evolutionRecipe != null)
        {
            // 如果有进化配方，我们创建一个临时的“进化节点”
            // 这样 UI 就能显示它，而不需要我们在数据库里预先配置
            SkillTreeNodeData evoNode = CreateEvolutionNode(evolutionRecipe);
            offeredUpgrades.Add(evoNode);

            // 策略选择：
            // A. 进化时刻只显示这一张卡 (强制进化，类吸血鬼幸存者宝箱) -> 直接跳过后续抽卡
            // B. 进化卡混在普通卡里 -> 继续下面的抽卡逻辑 (需注意 count - 1)

            // 这里演示方案 A (更有仪式感):
            Debug.Log($"[UpgradeManager] 发现进化配方: {evolutionRecipe.evolutionName}");
        }
        else
        {
            // --- 如果没有进化，才执行常规抽卡 ---
            List<SkillTreeNodeData> availableUpgrades = GetAvailableUpgrades();
            if (availableUpgrades.Count > 0)
            {
                var shuffledUpgrades = availableUpgrades.OrderBy(a => Random.value).ToList();
                int count = Mathf.Min(3, shuffledUpgrades.Count);
                for (int i = 0; i < count; i++)
                {
                    offeredUpgrades.Add(shuffledUpgrades[i]);
                }
            }
        }
        // ----------------------------------------

        if (offeredUpgrades.Count == 0)
        {
            Time.timeScale = 1f; // 没东西升了，直接恢复
            return;
        }

        // 刷新 UI
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    private SkillTreeNodeData CreateEvolutionNode(EvolutionRecipeSO recipe)
    {
        // 在内存中创建一个临时的 ScriptableObject 实例
        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();

        node.skillName = recipe.evolutionName; // "疾风之刃"
        node.skillIcon = recipe.evolvedWeapon.weaponIcon; // 用新武器的图标

        // 创建一个选项
        UpgradeOption option = new UpgradeOption();
        option.description = recipe.description; // "进化！发射穿透风刃..."
        option.rarity = Rarity.Epic; // 进化通常是史诗级的金色
        option.effects = new List<UpgradeEffect>();

        // 这里我们需要一种新的 ActionType 来告诉系统“这是进化”
        // 但为了不改动太多底层代码，我们可以复用 'UnlockWeapon' 
        // 并把 evolvedWeapon 传进去。
        // *或者* 最好是在 UpgradeEffect.cs 里加一个 'EvolveWeapon' 类型。
        // 为了稳健，我们假设你还没有加那个枚举，我们先用 UnlockWeapon 变通一下，
        // 或者我们直接在 ApplyEffect 里通过名字判断。

        // 推荐方案：去 UpgradeEffect.cs 加一个 EffectActionType.EvolveWeapon
        // 这里假设你已经加了 (稍后步骤会提示你加)
        UpgradeEffect effect = new UpgradeEffect();
        effect.actionType = EffectActionType.EvolveWeapon; // <--- 需新增枚举
        effect.weaponToUnlock = recipe.baseWeapon; // 这里的逻辑是：我们要进化的是 BaseWeapon
                                                   // 或者我们可以存 evolvedWeapon，这取决于 WeaponController 怎么处理

        // 实际上 WeaponController.TryUpgradeWeapon 需要的是“基础武器的名字”
        // 既然 UpgradeEffect 没有 string 字段，我们可以借用 weaponToUnlock.weaponName
        effect.weaponToUnlock = recipe.baseWeapon;

        option.effects.Add(effect);

        node.possibleOptions = new List<UpgradeOption> { option };

        return node;
    }

    private IEnumerator ShowCardsSequentially()
    {
        foreach (var upgradeNode in offeredUpgrades)
        {
            // --- 这部分逻辑与您原来的一致 ---
            float playerLuck = PlayerStats.Instance != null ? PlayerStats.Instance.luck : 1.0f;
            UpgradeOption chosenOption = RaritySystem.GetRandomOptionByRarity(upgradeNode.possibleOptions, playerLuck);

            if (chosenOption == null) continue;

            GameObject prefabToInstantiate = GetPrefabForOption(chosenOption);
            GameObject cardGO = Instantiate(prefabToInstantiate, cardContainer);
            var cardUI = cardGO.GetComponent<UpgradeCardUI>();
            // --- 逻辑结束 ---

            if (cardUI != null)
            {
                // 1. 先设置卡片数据
                cardUI.Setup(upgradeNode, chosenOption);

                // 2. 再调用Show()方法来触发Animator动画
                cardUI.Show();

                // 3. 将实例化的卡片UI存入列表
                activeCardUIs.Add(cardUI);
            }

            // 【关键】等待指定的时间，再进行下一次循环
            yield return new WaitForSecondsRealtime(delayBetweenCards);
        }
    }
    private List<SkillTreeNodeData> GetAvailableUpgrades()
    {
        // 获取玩家当前拥有的所有武器的 StatBlock
        HashSet<WeaponStatBlock> ownedWeaponStats = new HashSet<WeaponStatBlock>();
        if (WeaponController.Instance != null)
        {
            foreach (var ownedWeapon in WeaponController.Instance.ownedWeapons)
            {
                ownedWeaponStats.Add(ownedWeapon.stats);
            }
        }

        List<SkillTreeNodeData> availableNodes = new List<SkillTreeNodeData>();
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            // --- 【核心修改】 ---
            // 检查1：如果这是一个“解锁武器”的节点，先判断是否已拥有该武器
            bool isUnlockWeaponNode = node.possibleOptions.Any(opt => opt.effects.Any(eff => eff.actionType == EffectActionType.UnlockWeapon));
            if (isUnlockWeaponNode)
            {
                var weaponToUnlock = node.possibleOptions.First().effects.First().weaponToUnlock;
                if (ownedWeaponStats.Contains(weaponToUnlock))
                {
                    continue; // 如果已经拥有这把武器，则直接跳过这个节点
                }
            }
            // --- 修改结束 ---

            // 检查2：前置条件是否满足 (逻辑不变)
            bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));

            if (prerequisitesMet)
            {
                // 检查3：是否达到最大等级 (逻辑不变)
                if (!ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel)
                {
                    availableNodes.Add(node);
                }
            }
        }
        return availableNodes;
    }

    private GameObject GetPrefabForOption(UpgradeOption option)
    {
        // 检查是否有解锁武器的效果，这类效果优先使用专属卡片样式
        if (option.effects.Any(e => e.actionType == EffectActionType.UnlockWeapon))
        {
            return unlockCardPrefab;
        }

        switch (option.rarity)
        {
            case Rarity.Common: return commonCardPrefab;
            case Rarity.Uncommon: return uncommonCardPrefab;
            case Rarity.Rare: return rareCardPrefab;
            case Rarity.Epic: return epicCardPrefab;
            default: return commonCardPrefab;
        }
    }

    /// <summary>
    /// 由卡片UI在被点击后调用
    /// </summary>
    public void OnUpgradeOptionSelected(SkillTreeNodeData sourceNode, UpgradeOption chosenOption)
    {
        foreach (UpgradeEffect effect in chosenOption.effects)
        {
            if (effect.actionType == EffectActionType.ModifyStat)
            {
                if (PlayerStats.Instance != null) { PlayerStats.Instance.ApplyEffect(effect); }
            }
            else if (effect.actionType == EffectActionType.UnlockWeapon)
            {
                if (effect.weaponToUnlock != null && WeaponController.Instance != null) { WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock); }
            }
            else if (effect.actionType == EffectActionType.UnlockShield)
            {
                if (effect.shieldToUnlock != null && PlayerShield.Instance != null) { PlayerShield.Instance.EquipShield(effect.shieldToUnlock); }
            }
            else if (effect.actionType == EffectActionType.EvolveWeapon)
            {
                if (WeaponController.Instance != null && effect.weaponToUnlock != null)
                {
                    WeaponController.Instance.TryUpgradeWeapon(effect.weaponToUnlock.weaponName);
                }
            }
        }

        if (WeaponController.Instance != null)
        {
            // 遍历玩家手里的所有武器
            foreach (var ownedWrapper in WeaponController.Instance.ownedWeapons)
            {
                bool matchFound = false;

                // 判定方式 A: 检查 ScriptableObject 里的 "Associated Weapon" 字段 (最准确)
                // (注意：你需要确保 SkillTreeNodeData 脚本里有 associatedWeapon 这个 public 字段)
                if (sourceNode.associatedWeapon != null && sourceNode.associatedWeapon == ownedWrapper.stats)
                {
                    matchFound = true;
                }
                // 判定方式 B: 如果没配字段，回退到名字匹配 (例如 "手雷Lv5" 包含 "手雷")
                else if (sourceNode.skillName.Contains(ownedWrapper.stats.weaponName))
                {
                    matchFound = true;
                }

                if (matchFound)
                {
                    // 只有当武器没满级时才加
                    if (ownedWrapper.currentLevel < ownedWrapper.stats.maxLevel)
                    {
                        ownedWrapper.currentLevel++; // 1. 提升数据层级

                        // 2. 同步给场景里的实体 WeaponPart
                        if (ownedWrapper.weaponPartInstance != null)
                        {
                            ownedWrapper.weaponPartInstance.currentLevel = ownedWrapper.currentLevel;
                        }

                        Debug.Log($"<color=green>[UpgradeManager] 武器同步升级: '{ownedWrapper.stats.weaponName}' 升到了 Lv.{ownedWrapper.currentLevel}</color>");
                    }
                    else
                    {
                        // 如果已经是满级(Lv.5)，再升级可能是单纯加属性，这里不报错
                        Debug.Log($"[UpgradeManager] '{ownedWrapper.stats.weaponName}' 已达等级上限，仅应用属性加成。");
                    }

                    // 找到一个匹配的就退出循环
                    break;
                }
            }
        }

        // 2. 更新升级记录 (保持不变)
        if (ownedUpgrades.ContainsKey(sourceNode)) { ownedUpgrades[sourceNode]++; }
        else { ownedUpgrades.Add(sourceNode, 1); }
        Debug.Log($"升级 '{sourceNode.skillName}' 已应用，当前等级: {ownedUpgrades[sourceNode]}");       

        // 4. 刷新状态并关闭面板 (保持不变)
        // 刷新仍然重要，因为它会应用 PlayerStats 中可能改变的基础伤害/射速等
        if (WeaponController.Instance != null) { WeaponController.Instance.RefreshAllWeaponStates(); }
        if (upgradePanel != null) upgradePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ForceGrantUpgrade(SkillTreeNodeData nodeToGrant)
    {
        if (nodeToGrant == null) return;

        // 1. 从这个节点中，随机抽取一个最高品质的效果来应用
        // (在调试时，我们通常希望测试最强的效果)
        var bestOption = nodeToGrant.possibleOptions.OrderByDescending(opt => opt.rarity).FirstOrDefault();

        if (bestOption != null)
        {
            // 2. 直接调用我们已有的“应用效果”的逻辑
            OnUpgradeOptionSelected(nodeToGrant, bestOption);
        }
        else
        {
            Debug.LogError($"技能节点 '{nodeToGrant.skillName}' 中没有任何可用的升级选项！");
        }
    }
}