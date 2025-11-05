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
        List<SkillTreeNodeData> availableUpgrades = GetAvailableUpgrades();
        if (availableUpgrades.Count == 0) return;
        Time.timeScale = 0f;

        offeredUpgrades.Clear();
        var shuffledUpgrades = availableUpgrades.OrderBy(a => Random.value).ToList();
        int count = Mathf.Min(3, shuffledUpgrades.Count);
        for (int i = 0; i < count; i++)
        {
            offeredUpgrades.Add(shuffledUpgrades[i]);
        }

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }
        activeCardUIs.Clear();
        upgradePanel.SetActive(true);

        StartCoroutine(ShowCardsSequentially());
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
        // 1. 应用卡片效果 (保持不变)
        //    【重要】当 effect.statToModify 是 BoomerangStackUpgrade 时，
        //    PlayerStats.ApplyEffect 内部会自动处理回旋镖的升级逻辑。
        foreach (UpgradeEffect effect in chosenOption.effects)
        {
            if (effect.actionType == EffectActionType.ModifyStat)
            {
                if (PlayerStats.Instance != null) { PlayerStats.Instance.ApplyEffect(effect); } // <-- 这里会处理 BoomerangStackUpgrade
                else { Debug.LogError("PlayerStats Instance not found!"); }
            }
            else if (effect.actionType == EffectActionType.UnlockWeapon)
            {
                if (effect.weaponToUnlock != null && WeaponController.Instance != null) { WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock); }
            }
            else if (effect.actionType == EffectActionType.UnlockShield)
            {
                if (effect.shieldToUnlock != null && PlayerShield.Instance != null) { PlayerShield.Instance.EquipShield(effect.shieldToUnlock); }
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