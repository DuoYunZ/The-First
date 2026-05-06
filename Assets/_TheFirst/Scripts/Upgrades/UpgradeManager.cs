using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("UI引用")]
    public GameObject upgradePanel;
    public Transform cardContainer;
    [Tooltip("升级面板标题文字")]
    public TMPro.TextMeshProUGUI titleText;

    [Header("宝箱多选特效")]
    [Tooltip("可选2张时的彩带特效")]
    public GameObject confetti2;
    [Tooltip("可选3张时的彩带特效")]
    public GameObject confetti3;

    [Header("卡片预制件库 (按品质)")]
    public GameObject commonCardPrefab;
    public GameObject uncommonCardPrefab;
    public GameObject rareCardPrefab;
    public GameObject epicCardPrefab;
    public GameObject unlockCardPrefab;

    [Header("升级数据库")]
    public UpgradeDatabase upgradeDatabase;

    [Header("动画设置")]
    [Tooltip("每张卡片出现的间隔时间（秒）")]
    public float delayBetweenCards = 0.2f;
    [Tooltip("升级特效播放时间（秒，特效播完后才弹出卡片）")]
    public float levelUpVfxDelay = 1.0f;
    [Tooltip("升级特效期间的慢动作倍率")]
    public float levelUpSlowMotion = 0.0f;

    [Header("宝石镶嵌系统")]
    [Tooltip("宝石镶嵌动画覆盖层")]
    public GemEmbedOverlay gemEmbedOverlay;
    // 记录玩家已拥有的技能节点及其当前等级
    private Dictionary<SkillTreeNodeData, int> ownedUpgrades = new Dictionary<SkillTreeNodeData, int>();

    // 用于存储本次为玩家提供的三个“升级机会”
    private List<SkillTreeNodeData> offeredUpgrades = new List<SkillTreeNodeData>();
    private List<UpgradeCardUI> activeCardUIs = new List<UpgradeCardUI>();

    // === 宝石镶嵌追踪 ===
    private Dictionary<WeaponStatBlock, int> weaponGemCounts = new Dictionary<WeaponStatBlock, int>();
    private List<WeaponStatBlock> pendingUltimateUnlocks = new List<WeaponStatBlock>();
    public const int GEM_SLOT_COUNT = 5;

    // === 宝箱多选系统 ===
    /// <summary>
    /// 宝箱选卡剩余可选次数（>0 时选完一张后不关面板，继续选择下一张）
    /// </summary>
    private int remainingTreasurePicks = 0;

    // === 角色专属技能卡系统 ===
    /// <summary>
    /// 本局已激活的角色技能标识符集合（抽到卡片后加入）
    /// </summary>
    private HashSet<string> activeCharacterSkills = new HashSet<string>();

    /// <summary>
    /// 本局可用的角色技能卡池（局外解锁的 layer 2+ 节点关联的卡片）
    /// </summary>
    private List<SkillTreeNodeData> characterCardPool = new List<SkillTreeNodeData>();

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

        // 初始化角色专属卡池
        InitCharacterCardPool();
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
        // 启动协程：先播放升级特效，再显示卡片
        StartCoroutine(LevelUpSequence(newLevel));
    }

    /// <summary>
    /// 升级流程协程：慢动作 + 特效播放 + 卡片选择
    /// </summary>
    private IEnumerator LevelUpSequence(int newLevel)
    {
        // 1. 进入慢动作（让特效看起来更华丽）
        Time.timeScale = levelUpSlowMotion;

        // 2. 等待特效播放（使用 unscaledTime 确保不受慢动作影响）
        yield return new WaitForSecondsRealtime(levelUpVfxDelay);

        // 3. 完全暂停，开始显示卡片
        Time.timeScale = 0f;
        offeredUpgrades.Clear();

        // 0. 【宝石系统】优先检查是否有武器需要注入大招解锁卡
        if (pendingUltimateUnlocks.Count > 0)
        {
            WeaponStatBlock ultimateWeapon = pendingUltimateUnlocks[0];
            pendingUltimateUnlocks.RemoveAt(0);
            SkillTreeNodeData ultimateNode = CreateUltimateUnlockNode(ultimateWeapon);
            offeredUpgrades.Add(ultimateNode);
        }

        // 1. 优先检查融合 (宝箱逻辑通常不在这里，升级界面通常不直接给超武，除非你的设计允许)
        // (保持你原有的逻辑，如果这是你想保留的“升级直接送超武”机制)
        FusionRecipeSO fusionRecipe = null;
        if (WeaponController.Instance != null)
        {
            fusionRecipe = WeaponController.Instance.CheckForAvailableFusion();
        }

        if (fusionRecipe != null)
        {
            SkillTreeNodeData evoNode = CreateFusionNode(fusionRecipe);
            offeredUpgrades.Add(evoNode);
            Debug.Log($"[UpgradeManager] 发现融合配方: {fusionRecipe.resultWeapon.weaponName}");
        }

        // --- 【三轨道系统】混合卡池抽卡逻辑（补足至3张） ---
        // 大招卡/融合卡已经占用了一些位置，只需补足剩余
        int slotsToFill = 3 - offeredUpgrades.Count;
        if (slotsToFill > 0)
        {
            // 武器解锁轨道：解锁新武器
            // 被动轨道：被动道具、精通卡、天赋、消耗品
            // 武器技能轨道：已拥有武器的技能树节点（按链路依次解锁）

            // A. 获取所有可用的武器解锁卡
            List<SkillTreeNodeData> validWeapons = GenerateWeaponNodes();

            // B. 获取所有可用的被动升级
            List<SkillTreeNodeData> validPassives = new List<SkillTreeNodeData>();

            // 从 PlayerStats 获取真实的被动道具持有状态
            int currentUniquePassiveCount = 0;
            int maxPassiveSlots = 6;
            if (PlayerStats.Instance != null)
            {
                currentUniquePassiveCount = PlayerStats.Instance.activePassiveItems.Count;
            }

            if (upgradeDatabase.passiveUpgrades != null)
            {
                foreach (var node in upgradeDatabase.passiveUpgrades)
                {
                    bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));
                    bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel;

                    // 【图鉴解锁过滤】未解锁的被动道具不进入升级卡池
                    if (!prerequisitesMet || !notMaxed || !IsPassiveNodeUnlocked(node)) continue;

                    // 【槽位上限过滤】已有6种不同的被动道具时，只允许已拥有（可升级）的道具出现
                    if (currentUniquePassiveCount >= maxPassiveSlots)
                    {
                        // 检查这个道具是否已被玩家持有（可以升级）
                        bool alreadyOwned = ownedUpgrades.ContainsKey(node);
                        if (!alreadyOwned) continue; // 新道具不再出现
                    }

                    validPassives.Add(node);
                }
            }

            // C. 获取所有已拥有武器的可用技能树节点
            List<SkillTreeNodeData> validWeaponSkills = new List<SkillTreeNodeData>();
            if (WeaponController.Instance != null)
            {
                foreach (var owned in WeaponController.Instance.ownedWeapons)
                {
                    if (owned.weaponPartInstance != null && owned.stats != null)
                    {
                        var nodes = GetAvailableWeaponSkillNodes(owned.stats);
                        validWeaponSkills.AddRange(nodes);
                    }
                }
            }

            // 打乱列表
            var shuffledWeapons = validWeapons.OrderBy(a => Random.value).ToList();
            var shuffledPassives = validPassives.OrderBy(a => Random.value).ToList();
            var shuffledSkills = validWeaponSkills.OrderBy(a => Random.value).ToList();

            Debug.Log($"[UpgradeManager] 卡池状态 - 武器解锁:{shuffledWeapons.Count} 被动:{shuffledPassives.Count} 武器技能:{shuffledSkills.Count} 需补:{slotsToFill}张");

            // E. 获取可用的角色专属卡
            List<SkillTreeNodeData> validCharCards = GetAvailableCharacterCards();

            // 优先把分支卡（精准斩击/敏捷猎手）排到最前面
            validCharCards.Sort((a, b) =>
            {
                bool aIsBranch = IsBranchMechanicCard(a);
                bool bIsBranch = IsBranchMechanicCard(b);
                if (aIsBranch && !bIsBranch) return -1;
                if (!aIsBranch && bIsBranch) return 1;
                return Random.value > 0.5f ? 1 : -1; // 非分支卡随机排序
            });

            // 角色卡每次升级最多出现 1 张
            bool charCardUsed = false;

            // 每5级保底出现角色卡
            bool forceCharCard = (newLevel % 5 == 0) && validCharCards.Count > 0;
            if (forceCharCard)
            {
                SkillTreeNodeData charCard = validCharCards[0];
                offeredUpgrades.Add(charCard);
                slotsToFill--;
                charCardUsed = true;
                Debug.Log($"[UpgradeManager] 每5级保底角色卡: {charCard.skillName}");
            }

            // 如果已经用了角色卡，清空池子防止 for 循环再抽到
            var shuffledCharCards = charCardUsed
                ? new List<SkillTreeNodeData>()
                : validCharCards;

            // D. 抽取剩余张数的卡（确保不重复）
            for (int i = 0; i < slotsToFill; i++)
            {
                // 权重：30% 武器技能树、20% 角色卡、25% 武器解锁、25% 被动
                float roll = Random.value;

                bool hasWeapon = shuffledWeapons.Count > 0;
                bool hasPassive = shuffledPassives.Count > 0;
                bool hasSkill = shuffledSkills.Count > 0;
                bool hasCharCard = shuffledCharCards.Count > 0;

                SkillTreeNodeData pickedNode = null;

                if (roll < 0.30f && hasSkill)
                {
                    pickedNode = shuffledSkills[0];
                    shuffledSkills.RemoveAt(0);
                }
                else if (roll < 0.50f && hasCharCard && !charCardUsed)
                {
                    // 20% 概率抽角色卡（每次升级最多1张）
                    pickedNode = shuffledCharCards[0];
                    shuffledCharCards.Clear(); // 清空防止再抽
                    charCardUsed = true;
                }
                else if (roll < 0.75f && hasWeapon)
                {
                    pickedNode = shuffledWeapons[0];
                    shuffledWeapons.RemoveAt(0);
                }
                else if (hasPassive)
                {
                    pickedNode = shuffledPassives[0];
                    shuffledPassives.RemoveAt(0);
                }
                // 保底：哪个池子有就给哪个
                else if (hasSkill)
                {
                    pickedNode = shuffledSkills[0];
                    shuffledSkills.RemoveAt(0);
                }
                else if (hasCharCard)
                {
                    pickedNode = shuffledCharCards[0];
                    shuffledCharCards.RemoveAt(0);
                }
                else if (hasWeapon)
                {
                    pickedNode = shuffledWeapons[0];
                    shuffledWeapons.RemoveAt(0);
                }
                else if (hasPassive)
                {
                    pickedNode = shuffledPassives[0];
                    shuffledPassives.RemoveAt(0);
                }

                // 去重检查：避免同一张卡出现多次
                if (pickedNode != null && !offeredUpgrades.Contains(pickedNode))
                {
                    offeredUpgrades.Add(pickedNode);
                    Debug.Log($"[UpgradeManager] 第{i+1}张卡: {pickedNode.skillName} (roll={roll:F2})");
                }
                else if (pickedNode != null)
                {
                    Debug.Log($"[UpgradeManager] 第{i+1}张卡重复跳过: {pickedNode.skillName}，尝试从其他池补充");
                    // 重复了，尝试从其他池子补一张不重复的
                    SkillTreeNodeData fallback = null;
                    fallback = fallback ?? shuffledCharCards.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledPassives.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledWeapons.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledSkills.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    if (fallback != null)
                    {
                        offeredUpgrades.Add(fallback);
                        shuffledPassives.Remove(fallback);
                        shuffledWeapons.Remove(fallback);
                        shuffledSkills.Remove(fallback);
                        Debug.Log($"[UpgradeManager] 补发: {fallback.skillName}");
                    }
                }
            }
        }

        // ... 后续 UI 刷新逻辑保持不变 ...
        if (offeredUpgrades.Count == 0)
        {
            Time.timeScale = 1f;
            yield break;
        }

        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        // 普通升级：标题还原，关闭彩带
        SetUpgradePanelTitle(1);
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    private SkillTreeNodeData CreateFusionNode(FusionRecipeSO recipe)
    {
        // 在内存中创建一个临时的 ScriptableObject 实例
        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();

        // 显示结果武器的名字和图标 (例如 "炼狱风暴")
        node.skillName = recipe.resultWeapon.weaponName;
        node.skillIcon = recipe.resultWeapon.weaponIcon; // 或者是 recipe.fusionIcon

        // 创建一个选项
        UpgradeOption option = new UpgradeOption();
        option.description = recipe.description; // "融合！烈焰与疾风的结合..."
        option.rarity = Rarity.Epic; // 融合通常是史诗级的金色
        option.effects = new List<UpgradeEffect>();

        // 创建一个特殊的 Effect
        UpgradeEffect effect = new UpgradeEffect();
        // 我们需要一种 ActionType 来告诉系统“执行融合”
        // 既然你之前没有 EvolveWeapon 枚举，我们就用 ModifyStat + 特殊值来标记，
        // 或者最好去加一个 EffectActionType.FuseWeapon

        // 假设我们在 UpgradeEffect.cs 里加了 FuseWeapon (强烈建议加一个)
        effect.actionType = EffectActionType.EvolveWeapon; // 暂时复用 EvolveWeapon

        // 这里稍微 hack 一下：
        // 我们需要把 recipe 传给 OnUpgradeOptionSelected
        // 但 UpgradeEffect 没有 FusionRecipeSO 字段。
        // 既然这是临时的，我们可以把 recipe.resultWeapon 放在 weaponToUnlock 里
        // 然后在 OnUpgradeOptionSelected 里通过 CheckForAvailableFusion 再次确认
        effect.weaponToUnlock = recipe.resultWeapon;

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
        List<SkillTreeNodeData> availableNodes = new List<SkillTreeNodeData>();

        // --- 1. 获取通用被动技能 (原逻辑) ---
        // 以前是遍历 allUpgrades，现在遍历 passiveUpgrades
        if (upgradeDatabase.passiveUpgrades != null)
        {
            foreach (var node in upgradeDatabase.passiveUpgrades)
            {
                // 检查前置条件
                bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));
                // 检查等级上限
                bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel;

                if (prerequisitesMet && notMaxed)
                {
                    availableNodes.Add(node);
                }
            }
        }

        // --- 2. 获取武器升级 (新逻辑 - 动态生成节点) ---
        // 这里调用我们之前讨论的 GenerateWeaponNodes 方法
        availableNodes.AddRange(GenerateWeaponNodes());

        return availableNodes;
    }
    private List<SkillTreeNodeData> GenerateWeaponNodes()
    {
        List<SkillTreeNodeData> nodes = new List<SkillTreeNodeData>();

        if (WeaponController.Instance == null) return nodes;

        // [排查日志 1] 打印当前存档里所有的解锁物品，看看 "Molotov" 到底在不在里面
        if (PlayerProgressManager.Instance != null)
        {
            string allUnlocked = string.Join(", ", PlayerProgressManager.Instance.unlockedItems);
            // 【修改】去掉判空，强制打印，如果是空的就显示 "无"
            Debug.Log($"[UpgradeManager排查] 当前存档已解锁物品: {(string.IsNullOrEmpty(allUnlocked) ? "无 (列表为空)" : allUnlocked)}");
        }

        // --- 1. 获取当前武器数量和上限 ---
        int currentWeaponCount = WeaponController.Instance.ownedWeapons.Count;
        int maxWeaponSlots = 6;

        // 获取超武列表
        HashSet<WeaponStatBlock> evolutionOnlyWeapons = new HashSet<WeaponStatBlock>();
        if (WeaponController.Instance.fusionRecipes != null)
        {
            foreach (var recipe in WeaponController.Instance.fusionRecipes)
            {
                if (recipe.resultWeapon != null) evolutionOnlyWeapons.Add(recipe.resultWeapon);
            }
        }

        // 遍历数据库
        foreach (var chain in upgradeDatabase.weaponChains)
        {
            if (chain.targetWeapon == null) continue;

            // 过滤掉超武
            bool isEvoWeapon = evolutionOnlyWeapons.Contains(chain.targetWeapon);

            // 过滤掉黑名单
            if (WeaponController.Instance.banList.Contains(chain.targetWeapon)) continue;

            // 获取拥有状态
            var ownedWeapon = WeaponController.Instance.ownedWeapons
                .FirstOrDefault(w => w.stats == chain.targetWeapon);

            int currentLevel = (ownedWeapon != null) ? ownedWeapon.currentLevel : 0;
            int maxLevel = chain.targetWeapon.maxLevel;
            int dynamicMaxLevel = (ownedWeapon != null && ownedWeapon.weaponPartInstance != null)
                                    ? ownedWeapon.weaponPartInstance.maxLevel
                                    : chain.targetWeapon.maxLevel;

            // ---------------------------------------------------------
            // 情况 A: 尚未拥有 -> 提供解锁选项
            // ---------------------------------------------------------
            if (ownedWeapon == null)
            {
                // [排查日志 2] 针对燃烧瓶的专项检查
                // 如果名字里包含 Molotov 或 燃烧，就打印详细日志
                bool isTargetDebug = chain.weaponName.Contains("Molotov") || chain.weaponName.Contains("燃烧");

                // 1. 检查格子
                if (currentWeaponCount >= maxWeaponSlots)
                {
                    if (isTargetDebug) Debug.Log($"[UpgradeManager排查] 燃烧瓶被跳过：武器槽已满 ({currentWeaponCount}/{maxWeaponSlots})");
                    continue;
                }

                // 2. 检查超武
                if (isEvoWeapon) continue;

                // =========================================================
                // 3. 解锁资格检查 (带日志)
                // =========================================================
                bool isUnlocked = chain.isDefaultUnlocked;

                string wID = chain.targetWeapon.weaponID;
                string wName = chain.targetWeapon.weaponName;

                if (!isUnlocked && PlayerProgressManager.Instance != null)
                {
                    // A. 检查 ID
                    bool hasID = !string.IsNullOrEmpty(wID) && PlayerProgressManager.Instance.unlockedItems.Contains(wID);
                    // B. 检查 Name
                    bool hasName = PlayerProgressManager.Instance.unlockedItems.Contains(wName);

                    if (isTargetDebug)
                    {
                        Debug.Log($"[UpgradeManager排查] 燃烧瓶解锁判定:\n" +
                                  $"  - 目标ID: '{wID}'\n" +
                                  $"  - 目标Name: '{wName}'\n" +
                                  $"  - IsDefault: {chain.isDefaultUnlocked}\n" +
                                  $"  - 存档含ID?: {hasID}\n" +
                                  $"  - 存档含Name?: {hasName}");
                    }

                    if (hasID || hasName)
                    {
                        isUnlocked = true;
                    }
                }

                // 最终判定
                if (!isUnlocked)
                {
                    if (isTargetDebug) Debug.Log($"[UpgradeManager排查] 燃烧瓶最终判定: 【未解锁】，因此不生成卡片。");
                    continue;
                }

                if (isTargetDebug) Debug.Log($"[UpgradeManager排查] 燃烧瓶最终判定: 【已解锁】，生成解锁卡片！");
                // =========================================================

                SkillTreeNodeData unlockNode = ScriptableObject.CreateInstance<SkillTreeNodeData>();
                unlockNode.skillName = $"解锁 {chain.weaponName}";
                unlockNode.skillIcon = chain.icon;
                unlockNode.associatedWeapon = chain.targetWeapon;
                unlockNode.possibleOptions = new List<UpgradeOption> { chain.unlockOption };
                nodes.Add(unlockNode);
            }
            // 【已移除情况B和C】武器升级和进化现在通过武器自身经验条处理
        }

        return nodes;
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
        // --- 1. 标记位：这次操作是否是解锁武器？ ---
        bool isUnlockOperation = false;

        foreach (UpgradeEffect effect in chosenOption.effects)
        {
            // 【修复关键点】在这里定义 appliedLocally 变量，默认是 false
            bool appliedLocally = false;

            if (sourceNode.associatedWeapon != null && WeaponController.Instance != null)
            {
                // 尝试在背包里找到这把武器的实例
                var weaponWrapper = WeaponController.Instance.ownedWeapons
                    .FirstOrDefault(w => w.stats == sourceNode.associatedWeapon);

                if (weaponWrapper != null && weaponWrapper.weaponPartInstance != null)
                {
                    WeaponPart part = weaponWrapper.weaponPartInstance;

                    // 处理数值 (百分比转小数)
                    float val = effect.value;
                    if (effect.modType == ModifierType.Percentage) val /= 100f;

                    // =========================================================
                    // 【核心修复】拦截所有武器属性，存入局部变量
                    // =========================================================
                    switch (effect.statToModify)
                    {
                        case UpgradeType.WeaponDamage:
                            part.localDamageBonus += val;
                            appliedLocally = true;
                            Debug.Log($"[局部升级] {sourceNode.associatedWeapon.weaponName} 伤害 +{val:P0}");
                            break;

                        case UpgradeType.WeaponFireRate:
                            // 假设冷却缩减是正数 (如 0.1 代表 -10% CD)
                            part.localFireRateBonus += val;
                            appliedLocally = true;
                            Debug.Log($"[局部升级] {sourceNode.associatedWeapon.weaponName} 冷却缩减 +{val:P0}");
                            break;

                        case UpgradeType.OrbitalSpeed:      // 轨道速度
                        case UpgradeType.WeaponProjectileSpeed: // 或者子弹速度
                            part.localSpeedBonus += val;
                            appliedLocally = true;
                            Debug.Log($"[局部升级] {sourceNode.associatedWeapon.weaponName} 速度 +{val:P0}");
                            break;

                        case UpgradeType.WeaponDuration:
                            part.localDurationBonus += val;
                            appliedLocally = true;
                            break;

                        case UpgradeType.CritRate:
                            part.localCritRateBonus += val;
                            appliedLocally = true;
                            break;

                        case UpgradeType.CritDamage:
                            part.localCritDamageBonus += val;
                            appliedLocally = true;
                            break;

                        case UpgradeType.OrbitalCount:
                            part.localOrbitalCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            break;
                        case UpgradeType.AddProjectile:
                            // 根据武器类型分配到不同的字段
                            if (part.StatBlock != null && part.StatBlock.behavior == WeaponBehaviorType.Landmine)
                            {
                                part.localMineCountBonus += Mathf.RoundToInt(effect.value);
                                Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 地雷数量+{effect.value}，当前额外数: {part.localMineCountBonus}</color>");
                            }
                            else
                            {
                                part.localOrbitalCountBonus += Mathf.RoundToInt(effect.value);
                            }
                            appliedLocally = true;
                            break;
                        case UpgradeType.PierceCount:
                            part.localPierceCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 穿透 +{effect.value}，当前总穿透加成: {part.localPierceCountBonus}</color>");
                            break;
                        case UpgradeType.SlashCount:
                            part.localSlashCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            // 添加日志，确保存钱成功
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 刀光数量 +{effect.value}。当前总局部加成: {part.localSlashCountBonus}</color>");
                            break;

                        case UpgradeType.BurstCount:
                            part.localBurstCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 连射次数 +{effect.value}。当前总加成: {part.localBurstCountBonus}</color>");
                            break;

                        case UpgradeType.AoeRadius:
                            part.localAreaBonus += effect.value / 100f; // 假设 effect.value 是百分比 (80 代表 80%)，这里转为 0.8
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 范围加成 +{effect.value}%。当前总加成: {part.localAreaBonus:P0}</color>");
                            break;

                        case UpgradeType.SubProjectileCount:
                            // 这里我们假设 SubProjectileCount 直接增加 WeaponStatBlock 里的 subProjectileCount
                            // 但 WeaponStatBlock 是 ScriptableObject，运行时修改会保存。
                            // 所以我们需要在 WeaponPart 里用一个局部变量来存，或者 WeaponPart 动态覆盖 stat。
                            // 目前 WeaponPart 还没有 localSubProjectileCount。
                            // 让我们先简单处理：直接修改 part.StatBlock 的内存副本（如果是实例化出来的）
                            // 但通常 SO 是全局的。
                            // 更好的做法是：WeaponPart 维护 localSubProjectileCount，Projectile 发射时读取。
                            // 我们已经在 WeaponPart 看到 localPierceCountBonus 等。
                            // 让我添加 localSubProjectileCountBonus 到 WeaponPart (下一频及)。
                            // 这里先写上逻辑占位，等下修改 WeaponPart。
                            part.localSubProjectileCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 分裂数量 +{effect.value}</color>");
                            break;
                            
                        case UpgradeType.SubProjectile:
                            // 开启分裂模式。这通常意味着设置 subProjectilePrefab。
                            // 我们可以在 WeaponPart 里存一个 overrideSubProjectilePrefab
                            // 或者用一个 bool 标记 "enableSplit"
                            // 假设 effect.value > 0 代表开启
                            if (effect.value > 0)
                            {
                                part.isSubProjectileEnabled = true;
                            }
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 开启分裂效果</color>");
                            break;

                        case UpgradeType.IgnitionChance:
                            part.localIgnitionChanceBonus += effect.value / 100f; // 100 代表 +100% 概率
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 点燃概率 +{effect.value}%。当前总加成: {part.localIgnitionChanceBonus:P0}</color>");
                            break;

                        case UpgradeType.BurnDuration:
                            part.localBurnDurationBonus += effect.value; // 直接加秒数 (如 6 代表 +6秒)
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 燃烧时长 +{effect.value}秒。当前总加成: {part.localBurnDurationBonus}s</color>");
                            break;

                        case UpgradeType.MaxHealthBurn:
                            part.localMaxHealthBurnPercent += effect.value / 100f; // 1 代表 1%/跳
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 猛烈燃烧 +{effect.value}% 最大生命值/跳</color>");
                            break;

                        case UpgradeType.FreezeChance:
                            part.localFreezeChanceBonus += effect.value / 100f; // 30 代表 +30% 冰冻概率
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 冰冻概率 +{effect.value}%。当前总加成: {part.localFreezeChanceBonus:P0}</color>");
                            break;

                        case UpgradeType.SubProjectileDamageBonus:
                            part.localSubProjectileDamageBonus += effect.value / 100f; // 80 代表 +80% 伤害
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 分裂子弹伤害 +{effect.value}%</color>");
                            break;

                        case UpgradeType.SubProjectileInherit:
                            part.subProjectileInheritEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 分裂子弹继承母弹属性（穿透/冰冻）</color>");
                            break;

                        // === 雷击类 ===
                        case UpgradeType.LightningRepeatCount:
                            part.localLightningRepeatCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 连续雷击 +{effect.value}。当前总次数: {part.localLightningRepeatCount}</color>");
                            break;

                        case UpgradeType.StunDuration:
                            part.localStunDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 眩晕时间 +{effect.value}秒</color>");
                            break;

                        case UpgradeType.MagneticStormBurst:
                            part.isMagneticStormEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 磁暴已开启</color>");
                            break;

                        case UpgradeType.ElectricField:
                            part.isElectricFieldEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 电磁场已开启</color>");
                            break;

                        case UpgradeType.ElectricFieldDamage:
                            part.localElectricFieldDamageBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 电磁场伤害 +{effect.value}%</color>");
                            break;

                        case UpgradeType.ElectricFieldDuration:
                            part.localElectricFieldDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 电磁场持续时间 +{effect.value}秒</color>");
                            break;

                        case UpgradeType.OnKillChainLightning:
                            part.isOnKillChainEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=yellow>[升级生效] {sourceNode.associatedWeapon.weaponName} 击杀连锁雷击已开启</color>");
                            break;

                        // === 飓风术类 ===
                        case UpgradeType.KnockbackForce:
                            part.localKnockbackBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 击退力度 +{effect.value}%</color>");
                            break;

                        case UpgradeType.VacuumPull:
                            part.isVacuumPullEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 真空牵引已开启</color>");
                            break;

                        case UpgradeType.VacuumDamage:
                            part.localVacuumDamageBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 真空伤害 +{effect.value}%</color>");
                            break;

                        case UpgradeType.WindReturn:
                            part.isWindReturnEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 风力回旋已开启</color>");
                            break;

                        case UpgradeType.Turbulence:
                            part.localTurbulenceLevel = Mathf.Max(part.localTurbulenceLevel, 1);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 乱流已开启</color>");
                            break;

                        case UpgradeType.TurbulenceIntensify:
                            part.localTurbulenceLevel += 1;
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 乱流加剧 Lv.{part.localTurbulenceLevel}</color>");
                            break;

                        // === 榴弹类 ===
                        case UpgradeType.GrenadeBounce:
                            part.localBounceCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=orange>[升级生效] {sourceNode.associatedWeapon.weaponName} 弹跳 +{Mathf.RoundToInt(effect.value)}，当前弹跳: {part.localBounceCount}</color>");
                            break;

                        case UpgradeType.Stun:
                            part.localStunDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=orange>[升级生效] {sourceNode.associatedWeapon.weaponName} 眩晕 +{effect.value}秒，当前: {part.localStunDurationBonus}秒</color>");
                            break;

                        // === 闪电链类 ===
                        case UpgradeType.ChainCount:
                            part.localChainCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 弹射次数 +{Mathf.RoundToInt(effect.value)}，当前: {part.localChainCountBonus}</color>");
                            break;

                        case UpgradeType.IonExplosion:
                            part.localIonExplosionEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 离子爆破已开启</color>");
                            break;

                        case UpgradeType.IonExplosionDamage:
                            part.localIonExplosionDamageBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 离子爆破伤害 +{effect.value}%</color>");
                            break;

                        case UpgradeType.IonExplosionRadius:
                            part.localIonExplosionRadiusBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=cyan>[升级生效] {sourceNode.associatedWeapon.weaponName} 离子爆破范围 +{effect.value}%</color>");
                            break;

                        // === 冰霜新星类 ===
                        case UpgradeType.FrostNovaExtraCast:
                            part.localFrostNovaExtraCast += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 额外释放 +{Mathf.RoundToInt(effect.value)}，当前: {part.localFrostNovaExtraCast}</color>");
                            break;

                        case UpgradeType.FreezeDuration:
                            part.localFreezeDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 冻结时间 +{effect.value}秒，当前: {part.localFreezeDurationBonus}秒</color>");
                            break;

                        case UpgradeType.FrostNovaCenterDamage:
                            part.localFrostNovaCenterDmg = true;
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 寒霜之心已开启</color>");
                            break;

                        case UpgradeType.AbsoluteZero:
                            part.localAbsoluteZero = true;
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 绝对零度已开启</color>");
                            break;

                        // === 冰霜融合类 ===
                        case UpgradeType.FrostBite:
                            part.localFrostBite = true;
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 刺骨寒霜已开启</color>");
                            break;

                        case UpgradeType.IceCrystalShatter:
                            part.localIceCrystalShatter += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 冰晶碎裂 +{Mathf.RoundToInt(effect.value)}，当前: {part.localIceCrystalShatter}</color>");
                            break;

                        case UpgradeType.CooldownReduction:
                            part.localCooldownReduction += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log($"<color=#88DDFF>[升级生效] {sourceNode.associatedWeapon.weaponName} 冷却缩减 +{effect.value}%</color>");
                            break;

                        // === 环绕武器类 ===
                        case UpgradeType.OrbitalAbsorbProjectiles:
                            part.isOrbitalAbsorbEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 动能吸附已开启</color>");
                            break;

                        case UpgradeType.OrbitalExpansionBreathing:
                            part.isOrbitalBreathingEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 引力呼吸已开启</color>");
                            break;

                        case UpgradeType.OrbitalReleaseExplosion:
                            part.isOrbitalReleaseEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 充能释放已开启</color>");
                            break;

                        // === 地雷类 ===
                        case UpgradeType.LandmineEnergyRecovery:
                            part.isMineEnergyRecovery = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 能量回收已开启</color>");
                            break;

                        case UpgradeType.LandmineStun:
                            part.isMineStun = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 震撼弹片已开启</color>");
                            break;

                        case UpgradeType.LandmineGravityTrap:
                            part.isMineGravityTrap = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 引力陷阱已开启</color>");
                            break;

                        case UpgradeType.LandmineBlackHole:
                            part.isMineBlackHole = true;
                            appliedLocally = true;
                            Debug.Log($"<color=white>[升级生效] {sourceNode.associatedWeapon.weaponName} 引力黑洞已开启</color>");
                            break;

                        case UpgradeType.FusionNapalm:
                            part.isMineFusionNapalm = true;
                            appliedLocally = true;
                            Debug.Log($"<color=orange>[升级生效] {sourceNode.associatedWeapon.weaponName} 凝固汽油弹已开启</color>");
                            break;

                        // === Aura辅助型光环类（value 直接为实际数值） ===
                        case UpgradeType.AuraHealingPulse:
                            part.auraHealAmount = Mathf.Max(part.auraHealAmount, effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 生命脉动 回血={effect.value}</color>");
                            break;

                        case UpgradeType.AuraSluggishField:
                            part.auraSlowPercent = Mathf.Max(part.auraSlowPercent, effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 迟缓力场 减速={effect.value}%</color>");
                            break;

                        case UpgradeType.AuraFragileMark:
                            part.auraFragilePercent = Mathf.Max(part.auraFragilePercent, effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=green>[升级生效] {sourceNode.associatedWeapon.weaponName} 脆弱印记 增伤={effect.value}%</color>");
                            break;

                        // === 灵能飞刀类 ===
                        case UpgradeType.DaggerDamageBoost:
                        {
                            // value=1: 伤害+30%速度-15%, value=2: 伤害+60%速度-25%
                            float dmgBonus = effect.value >= 2 ? 60f : 30f;
                            float spdPenalty = effect.value >= 2 ? 25f : 15f;
                            part.daggerDamageBoost = Mathf.Max(part.daggerDamageBoost, dmgBonus);
                            part.daggerSpeedPenalty = Mathf.Max(part.daggerSpeedPenalty, spdPenalty);
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 烈焰增幅{(effect.value >= 2 ? "II" : "I")} 伤害+{dmgBonus}% 速度-{spdPenalty}%</color>");
                            break;
                        }
                        case UpgradeType.DaggerExtraCount:
                        {
                            // value=1: +1刀伤害-15%, value=2: +2刀伤害-25%（叠加）
                            int extraCount = effect.value >= 2 ? 2 : 1;
                            float dmgPenalty = effect.value >= 2 ? 25f : 15f;
                            part.daggerExtraCount += extraCount;
                            part.daggerCountDmgPenalty += dmgPenalty;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 多重飞刀{(effect.value >= 2 ? "II" : "I")} +{extraCount}刀 伤害-{dmgPenalty}% (当前共+{part.daggerExtraCount}刀, 总惩罚-{part.daggerCountDmgPenalty}%)</color>");
                            break;
                        }
                        case UpgradeType.DaggerSpeedBoost:
                        {
                            // value=1: 速度x1.3间隔-20%, value=2: 速度x1.6间隔-35%
                            float spdMult = effect.value >= 2 ? 1.6f : 1.3f;
                            float intervalReduce = effect.value >= 2 ? 35f : 20f;
                            part.daggerSpeedBoost = Mathf.Max(part.daggerSpeedBoost, spdMult);
                            part.daggerIntervalReduction = Mathf.Max(part.daggerIntervalReduction, intervalReduce);
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 焰舞加速{(effect.value >= 2 ? "II" : "I")} 速度x{spdMult} 间隔-{intervalReduce}%</color>");
                            break;
                        }
                        case UpgradeType.DaggerHoming:
                            part.daggerHomingUpgrade = true;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 锁魂追击 索敌+50%/锁定+2秒/半径-50%</color>");
                            break;
                        case UpgradeType.DaggerClone:
                            part.daggerCloneUpgrade = true;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 刃影分身 1%概率生成分身/半径-50%</color>");
                            break;
                        case UpgradeType.DaggerIgnite:
                            part.daggerIgniteUpgrade = true;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 灵能烙印 飞刀可点燃敌人</color>");
                            break;
                        case UpgradeType.DaggerLifeSteal:
                            part.daggerLifeStealUpgrade = true;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 灵魂收割 击杀回2%最大HP</color>");
                            break;
                        case UpgradeType.DaggerChainExplosion:
                            part.daggerChainExplosion = true;
                            appliedLocally = true;
                            Debug.Log($"<color=red>[升级生效] 连锁灵刃 命中点燃敌人触发爆破</color>");
                            break;

                        // === 镭射核心类 ===
                        case UpgradeType.LaserRefraction:
                            part.localLaserRefractionCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log($"<color=#FF4444>[升级生效] {sourceNode.associatedWeapon.weaponName} 棱镜折射 +{Mathf.RoundToInt(effect.value)}，当前折射数: {part.localLaserRefractionCount}</color>");
                            break;

                        case UpgradeType.LaserFocusBonus:
                            part.localLaserFocusBonus += effect.value / 100f; // 5 代表 +5% 每层
                            appliedLocally = true;
                            Debug.Log($"<color=#FF4444>[升级生效] {sourceNode.associatedWeapon.weaponName} 聚焦强化 +{effect.value}%/层</color>");
                            break;

                        case UpgradeType.LaserMeltdown:
                            part.localLaserMeltdownEnabled = true;
                            appliedLocally = true;
                            Debug.Log($"<color=#FF4444>[升级生效] {sourceNode.associatedWeapon.weaponName} 核心熔毁已开启（过热变为灼烧区域）</color>");
                            break;
                        
                    }
                }
            }

            // 如果没有局部应用（说明是通用属性，比如加血上限），则应用到全局 PlayerStats
            if (!appliedLocally && PlayerStats.Instance != null && effect.actionType == EffectActionType.ModifyStat)
            {
                PlayerStats.Instance.ApplyEffect(effect);
            }
            // 处理特殊操作类型
            else if (effect.actionType == EffectActionType.UnlockWeapon)
            {
                isUnlockOperation = true;
                if (effect.weaponToUnlock != null && WeaponController.Instance != null)
                {
                    WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock);
                }
            }
            else if (effect.actionType == EffectActionType.UnlockShield)
            {
                if (effect.shieldToUnlock != null && PlayerShield.Instance != null) { PlayerShield.Instance.EquipShield(effect.shieldToUnlock); }
            }
            else if (effect.actionType == EffectActionType.EvolveWeapon)
            {
                if (WeaponController.Instance != null && effect.weaponToUnlock != null)
                {
                    // 1. 尝试融合 (保持不变)
                    var recipe = WeaponController.Instance.fusionRecipes.FirstOrDefault(r => r.resultWeapon == effect.weaponToUnlock);
                    if (recipe != null)
                    {
                        WeaponController.Instance.PerformFusion(recipe);
                        Debug.Log($"<color=gold>[UpgradeManager] 融合进化成功！获得了: {effect.weaponToUnlock.weaponName}</color>");
                    }
                    else
                    {
                        // 2. 【核心修改】单体进化逻辑
                        // 不再自己处理数据替换，而是委托给 WeaponController 彻底换枪

                        // 找到是谁进化成这个新武器
                        var oldWeaponWrapper = WeaponController.Instance.ownedWeapons
                            .FirstOrDefault(w => w.stats.evolutionTarget == effect.weaponToUnlock);

                        if (oldWeaponWrapper != null)
                        {
                            // 调用我们刚才写的新方法
                            WeaponController.Instance.EvolveWeapon(oldWeaponWrapper.stats, effect.weaponToUnlock);
                        }
                        else
                        {
                            // 保底：找不到旧的直接给新的
                            WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock);
                        }
                    }
                }
            }
            // 【宝石系统】处理大招解锁效果
            else if (effect.actionType == EffectActionType.UnlockUltimate)
            {
                if (effect.weaponToUnlock != null && WeaponController.Instance != null)
                {
                    var targetWrapper = WeaponController.Instance.ownedWeapons
                        .FirstOrDefault(w => w.stats == effect.weaponToUnlock);
                    if (targetWrapper != null && targetWrapper.weaponPartInstance != null)
                    {
                        WeaponPart part = targetWrapper.weaponPartInstance;
                        part.isUltimateUnlocked = true;
                        part.currentEnergy = part.StatBlock.maxEnergy;
                        part.OnEnergyChanged?.Invoke(part.currentEnergy, part.StatBlock.maxEnergy);
                        part.OnEnergyFull?.Invoke(part);
                    }
                }
            }
            // 【角色技能卡】处理激活角色技能效果
            else if (effect.actionType == EffectActionType.ActivateCharSkill)
            {
                if (!string.IsNullOrEmpty(effect.skillIdentifier))
                {
                    activeCharacterSkills.Add(effect.skillIdentifier);
                    Debug.Log($"<color=magenta>[角色卡生效] 激活技能: {effect.skillIdentifier}</color>");

                    // 同时应用对应技能树节点上的属性加成（伤害/移速/护甲等）
                    ApplyCharacterNodeEffectsForSkill(effect.skillIdentifier);
                }
            }
        }

        // --- 2. 只有当【不是】解锁操作时，才去增加武器等级 ---
        if (!isUnlockOperation && WeaponController.Instance != null)
        {
            foreach (var ownedWrapper in WeaponController.Instance.ownedWeapons)
            {
                bool matchFound = false;
                if (sourceNode.associatedWeapon != null && sourceNode.associatedWeapon == ownedWrapper.stats) matchFound = true;
                else if (sourceNode.skillName.Contains(ownedWrapper.stats.weaponName)) matchFound = true;

                if (matchFound)
                {
                    int dynamicMaxLevel = ownedWrapper.stats.maxLevel;
                    if (ownedWrapper.weaponPartInstance != null)
                    {
                        dynamicMaxLevel = ownedWrapper.weaponPartInstance.maxLevel;
                    }

                    // 使用动态上限进行判断
                    if (ownedWrapper.currentLevel < dynamicMaxLevel)
                    {
                        ownedWrapper.currentLevel++;
                        if (ownedWrapper.weaponPartInstance != null)
                        {
                            ownedWrapper.weaponPartInstance.currentLevel = ownedWrapper.currentLevel;
                        }
                    }
                    break;
                }
            }
        }

        // 3. 更新升级记录
        if (ownedUpgrades.ContainsKey(sourceNode)) { ownedUpgrades[sourceNode]++; }
        else { ownedUpgrades.Add(sourceNode, 1); }

        // 4. 刷新状态
        if (WeaponController.Instance != null) { WeaponController.Instance.RefreshAllWeaponStates(); }
        if (PassiveItemsUI.Instance != null) { PassiveItemsUI.Instance.UpdateIcons(); }

        // === 5. 【宝石系统】追踪选择的武器技能 ===
        // 宝石飞入动画已集成到卡片UI中（UpgradeCardUI.PlayGemEmbedThenDismiss），
        // 这里只负责数据层的宝石计数和大招解锁检测
        WeaponStatBlock gemWeapon = sourceNode.associatedWeapon;
        if (gemWeapon != null)
        {
            if (!weaponGemCounts.ContainsKey(gemWeapon))
                weaponGemCounts[gemWeapon] = 0;
            weaponGemCounts[gemWeapon]++;

            int totalGems = weaponGemCounts[gemWeapon];

            // 检查是否达到5颗 → 解锁大招
            if (totalGems >= GEM_SLOT_COUNT)
            {
                var weaponWrapper = WeaponController.Instance != null
                    ? WeaponController.Instance.ownedWeapons.FirstOrDefault(w => w.stats == gemWeapon)
                    : null;
                bool alreadyUnlocked = weaponWrapper?.weaponPartInstance?.isUltimateUnlocked ?? false;
                if (!alreadyUnlocked && !pendingUltimateUnlocks.Contains(gemWeapon))
                {
                    pendingUltimateUnlocks.Add(gemWeapon);
                }
            }
        }

        // 6. 检查宝箱多选：是否还有剩余可选次数
        if (remainingTreasurePicks > 0)
        {
            remainingTreasurePicks--;
            Debug.Log($"<color=yellow>[宝箱] 还可以再选 {remainingTreasurePicks + 1} 张卡</color>");
            // 只更新标题文字提示剩余可选数，彩带保持不变
            if (titleText != null)
            {
                if (remainingTreasurePicks > 0)
                    titleText.text = $"还可选择{remainingTreasurePicks + 1}项";
                else
                    titleText.text = "选择最后一项";
            }
            // 不关闭面板，不恢复时间，等待玩家继续选择
            return;
        }

        // 没有剩余次数，关闭面板恢复游戏
        if (confetti2 != null) confetti2.SetActive(false);
        if (confetti3 != null) confetti3.SetActive(false);
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


    // ========================================================================
    //                    武器技能树 + 宝石镶嵌系统
    // ========================================================================
    #region 武器技能树与宝石系统

    /// <summary>
    /// 获取指定武器当前可用的技能树节点
    /// </summary>
    public List<SkillTreeNodeData> GetAvailableWeaponSkillNodes(WeaponStatBlock weaponStats)
    {
        List<SkillTreeNodeData> result = new List<SkillTreeNodeData>();
        if (upgradeDatabase.weaponSkillNodes == null) return result;

        foreach (var node in upgradeDatabase.weaponSkillNodes)
        {
            if (node == null) continue;
            if (node.associatedWeapon != weaponStats) continue;
            if (ownedUpgrades.ContainsKey(node) && ownedUpgrades[node] >= node.maxLevel) continue;

            if (node.prerequisites != null && node.prerequisites.Count > 0)
            {
                if (!node.prerequisites.All(p => p != null && ownedUpgrades.ContainsKey(p))) continue;
            }
            if (node.mutuallyExclusive != null && node.mutuallyExclusive.Count > 0)
            {
                if (node.mutuallyExclusive.Any(m => m != null && ownedUpgrades.ContainsKey(m))) continue;
            }
            if (node.requiredWeapons != null && node.requiredWeapons.Count > 0)
            {
                if (!node.requiredWeapons.All(rw => rw != null && WeaponController.Instance.ownedWeapons.Any(ow => ow.stats == rw))) continue;
            }
            result.Add(node);
        }
        return result;
    }

    public bool HasSkillNode(SkillTreeNodeData node)
    {
        return node != null && ownedUpgrades.ContainsKey(node);
    }

    // === 宝石系统辅助方法 ===

    public int GetGemCountForWeapon(WeaponStatBlock weapon)
    {
        if (weapon == null) return 0;
        return weaponGemCounts.ContainsKey(weapon) ? weaponGemCounts[weapon] : 0;
    }

    /// <summary>
    /// 创建大招解锁卡片节点
    /// </summary>
    private SkillTreeNodeData CreateUltimateUnlockNode(WeaponStatBlock weapon)
    {
        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();
        string weaponName = !string.IsNullOrEmpty(weapon.weaponID)
            ? LocalizationManager.T("weapon." + weapon.weaponID)
            : weapon.weaponName;

        node.skillName = weaponName;
        node.skillIcon = weapon.weaponIcon;
        node.associatedWeapon = weapon;

        UpgradeOption option = new UpgradeOption();
        option.description = !string.IsNullOrEmpty(weapon.ultimateDescription)
            ? weapon.ultimateDescription : weaponName;
        option.rarity = Rarity.Epic;

        UpgradeEffect effect = new UpgradeEffect();
        effect.actionType = EffectActionType.UnlockUltimate;
        effect.weaponToUnlock = weapon;

        option.effects = new List<UpgradeEffect> { effect };
        node.possibleOptions = new List<UpgradeOption> { option };
        return node;
    }

    /// <summary>
    /// 获取补充卡片
    /// </summary>
    private List<SkillTreeNodeData> GetFillerCards(int count)
    {
        List<SkillTreeNodeData> pool = new List<SkillTreeNodeData>();
        if (upgradeDatabase.passiveUpgrades != null)
        {
            foreach (var node in upgradeDatabase.passiveUpgrades)
            {
                bool ok = (node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p)));
                bool notMax = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel;

                // 【图鉴解锁过滤】未解锁的被动道具不进入卡池
                if (ok && notMax && IsPassiveNodeUnlocked(node)) pool.Add(node);
            }
        }
        if (WeaponController.Instance != null)
        {
            foreach (var owned in WeaponController.Instance.ownedWeapons)
            {
                if (owned.weaponPartInstance != null && owned.stats != null)
                    pool.AddRange(GetAvailableWeaponSkillNodes(owned.stats));
            }
        }
        var shuffled = pool.OrderBy(a => Random.value).ToList();
        return shuffled.Take(count).ToList();
    }
    // ============================================================
    // 宝箱系统：仅被动道具的升级选卡
    // ============================================================

    /// <summary>
    /// 查询是否处于宝箱多选模式（选完一张后还能继续选）
    /// UpgradeCardUI 在判断是否淡出其他卡片时使用
    /// </summary>
    public bool HasRemainingTreasurePicks() => remainingTreasurePicks > 0;

    /// <summary>
    /// 外部触发仅包含被动道具的升级选卡（宝箱拾取使用）
    /// </summary>
    /// <param name="allowedPicks">本次可选卡片数量（1=标准，2=双倍，3=全选），默认1张</param>
    public void TriggerPassiveOnlyUpgrade(int allowedPicks = 1)
    {
        // 设置剩余可选次数（减1是因为第一次选择不消耗此计数）
        remainingTreasurePicks = Mathf.Max(0, allowedPicks - 1);
        Debug.Log($"<color=yellow>[宝箱选卡] 触发被动道具选卡，可选 {allowedPicks} 张</color>");
        StartCoroutine(PassiveOnlyUpgradeSequence());
    }

    /// <summary>
    /// 仅被动道具的升级选卡流程
    /// </summary>
    private IEnumerator PassiveOnlyUpgradeSequence()
    {
        // 1. 进入慢动作
        Time.timeScale = levelUpSlowMotion;

        // 2. 等待特效
        yield return new WaitForSecondsRealtime(levelUpVfxDelay);

        // 3. 完全暂停
        Time.timeScale = 0f;
        offeredUpgrades.Clear();

        // 4. 仅从被动道具池中抽取3张卡
        List<SkillTreeNodeData> validPassives = new List<SkillTreeNodeData>();

        // 从 PlayerStats 获取真实的被动道具持有数量
        int currentUniquePassiveCount = 0;
        int maxPassiveSlots = 6;
        if (PlayerStats.Instance != null)
        {
            currentUniquePassiveCount = PlayerStats.Instance.activePassiveItems.Count;
        }

        if (upgradeDatabase.passiveUpgrades != null)
        {
            foreach (var node in upgradeDatabase.passiveUpgrades)
            {
                bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));
                bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel;

                // 【图鉴解锁过滤】未解锁的被动道具不进入宝箱卡池
                if (!prerequisitesMet || !notMaxed || !IsPassiveNodeUnlocked(node)) continue;

                // 【槽位上限过滤】已有6种不同的被动道具时，只允许已拥有（可升级）的道具出现
                if (currentUniquePassiveCount >= maxPassiveSlots)
                {
                    bool alreadyOwned = ownedUpgrades.ContainsKey(node);
                    if (!alreadyOwned) continue; // 新道具不再出现
                }

                validPassives.Add(node);
            }
        }

        // 打乱并取前3个
        var shuffledPassives = validPassives.OrderBy(a => Random.value).ToList();
        int slotsToFill = Mathf.Min(3, shuffledPassives.Count);
        for (int i = 0; i < slotsToFill; i++)
        {
            offeredUpgrades.Add(shuffledPassives[i]);
        }

        Debug.Log($"[UpgradeManager] 宝箱选卡 - 可用被动道具:{validPassives.Count}张, 提供:{offeredUpgrades.Count}张");

        // 5. 如果没有可用的被动道具，直接恢复游戏
        if (offeredUpgrades.Count == 0)
        {
            Debug.LogWarning("[UpgradeManager] 宝箱选卡：没有可用的被动道具！");
            Time.timeScale = 1f;
            yield break;
        }

        // 6. 显示卡片UI
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        // 宝箱选卡：设置标题和彩带特效
        int totalPicks = remainingTreasurePicks + 1; // 当前可选总数
        SetUpgradePanelTitle(totalPicks);
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    /// <summary>
    /// 根据可选张数设置面板标题文字和彩带特效
    /// </summary>
    private void SetUpgradePanelTitle(int allowedPicks)
    {
        // 设置标题文字
        if (titleText != null)
        {
            switch (allowedPicks)
            {
                case 3:
                    titleText.text = "选择三项升级";
                    break;
                case 2:
                    titleText.text = "选择两项升级";
                    break;
                default:
                    titleText.text = "选择一项升级";
                    break;
            }
        }

        // 激活/关闭彩带特效（使用Unscaled Time，不受暂停影响）
        if (confetti2 != null)
        {
            confetti2.SetActive(allowedPicks == 2);
            if (allowedPicks == 2) SetParticlesUnscaled(confetti2);
        }
        if (confetti3 != null)
        {
            confetti3.SetActive(allowedPicks == 3);
            if (allowedPicks == 3) SetParticlesUnscaled(confetti3);
        }
    }

    /// <summary>
    /// 设置目标物体上所有 ParticleImage 使用不受 Time.timeScale 影响的时间
    /// </summary>
    private void SetParticlesUnscaled(GameObject target)
    {
        var particles = target.GetComponentsInChildren<AssetKits.ParticleImage.ParticleImage>(true);
        foreach (var pi in particles)
        {
            pi.timeScale = AssetKits.ParticleImage.Enumerations.TimeScale.Unscaled;
        }
    }

    /// <summary>
    /// 检查被动道具节点是否已通过图鉴解锁
    /// 如果节点没有关联 PassiveItemData，或者道具是默认解锁的，则视为已解锁
    /// </summary>
    private bool IsPassiveNodeUnlocked(SkillTreeNodeData node)
    {
        if (node == null || node.possibleOptions == null) return true;

        // 遍历节点的所有选项，查找关联的 PassiveItemData
        foreach (var option in node.possibleOptions)
        {
            if (option.effects == null) continue;
            foreach (var effect in option.effects)
            {
                PassiveItemData passiveData = effect.passiveItemData;
                if (passiveData == null) continue;

                // 默认解锁的道具直接通过
                if (passiveData.isDefaultUnlocked) return true;

                // 需要成就解锁的道具：检查当前进度
                if (!string.IsNullOrEmpty(passiveData.unlockStatKey) && passiveData.unlockThreshold > 0)
                {
                    if (PlayerProgressManager.Instance != null)
                    {
                        int currentVal = 0;
                        if (PlayerProgressManager.Instance.achievementStats.ContainsKey(passiveData.unlockStatKey))
                        {
                            currentVal = PlayerProgressManager.Instance.achievementStats[passiveData.unlockStatKey];
                        }
                        // 未达到阈值 → 未解锁
                        if (currentVal < passiveData.unlockThreshold) return false;
                    }
                    else
                    {
                        // PlayerProgressManager 不存在时，无法判断，保守返回 false
                        return false;
                    }
                }
                else
                {
                    // 没有设置 unlockStatKey 且 isDefaultUnlocked 为 false → 未解锁
                    return false;
                }
            }
        }

        // 没有找到任何 PassiveItemData → 视为通用节点，允许入池
        return true;
    }

    #endregion

    #region === 角色专属技能卡系统 ===

    /// <summary>
    /// 初始化角色专属卡池：读取当前角色已解锁的 layer 2+ 节点，收集关联的卡片
    /// </summary>
    private void InitCharacterCardPool()
    {
        characterCardPool.Clear();
        activeCharacterSkills.Clear();

        if (PlayerProgressManager.Instance == null || DataManager.Instance == null) return;

        CharacterData charData = DataManager.Instance.selectedCharacter;
        if (charData == null || charData.characterSkillNodes == null) return;

        foreach (var node in charData.characterSkillNodes)
        {
            if (node == null) continue;
            // 只有 layer 2+ 且已解锁且配置了关联卡片的节点才加入卡池
            if (node.layer >= 2
                && node.linkedUpgradeNode != null
                && PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node))
            {
                characterCardPool.Add(node.linkedUpgradeNode);
                Debug.Log($"[角色卡池] 加入: {node.linkedUpgradeNode.skillName} (来自节点: {node.nodeName})");
            }
        }

        Debug.Log($"[角色卡池] 初始化完成，当前角色({charData.characterName})共 {characterCardPool.Count} 张角色卡");
    }

    /// <summary>
    /// 获取本局可用的角色卡（排除已激活的一次性卡）
    /// </summary>
    private List<SkillTreeNodeData> GetAvailableCharacterCards()
    {
        List<SkillTreeNodeData> available = new List<SkillTreeNodeData>();

        foreach (var card in characterCardPool)
        {
            if (card == null) continue;

            // 一次性卡片：已激活则不再出现
            if (card.isOneTimeOnly && ownedUpgrades.ContainsKey(card)) continue;

            // 跳过已通过 ForceActivateCharacterSkill 自动激活的技能卡
            // （如法师的 IcePath/FirePath 分支选择卡，战斗开始时已自动生效）
            if (card.possibleOptions != null && card.possibleOptions.Count > 0)
            {
                bool alreadyForceActivated = false;
                foreach (var option in card.possibleOptions)
                {
                    if (option.effects == null) continue;
                    foreach (var eff in option.effects)
                    {
                        if (eff.actionType == EffectActionType.ActivateCharSkill
                            && activeCharacterSkills.Contains(eff.skillIdentifier))
                        {
                            alreadyForceActivated = true;
                            break;
                        }
                    }
                    if (alreadyForceActivated) break;
                }
                if (alreadyForceActivated) continue;
            }

            // 检查是否已达最大等级
            if (ownedUpgrades.ContainsKey(card) && ownedUpgrades[card] >= card.maxLevel) continue;

            // 组合技卡片：必须同时装备所有指定武器才出现
            if (card.requiredWeapons != null && card.requiredWeapons.Count > 0)
            {
                if (WeaponController.Instance == null) continue;
                bool hasAll = true;
                foreach (var rw in card.requiredWeapons)
                {
                    if (rw == null) continue;
                    bool found = false;
                    foreach (var ow in WeaponController.Instance.ownedWeapons)
                    {
                        if (ow.stats == rw || (ow.weaponPartInstance != null && ow.weaponPartInstance.StatBlock == rw))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) { hasAll = false; break; }
                }
                if (!hasAll) continue;
            }

            available.Add(card);
        }

        return available;
    }

    /// <summary>
    /// 查询本局是否已激活某个角色技能（战斗系统使用）
    /// </summary>
    public bool HasActiveCharacterSkill(string skillIdentifier)
    {
        if (string.IsNullOrEmpty(skillIdentifier)) return false;
        return activeCharacterSkills.Contains(skillIdentifier);
    }

    /// <summary>
    /// 强制激活一个角色技能（供战斗初始化使用，跳过抽卡流程）
    /// 用于 IcePath/FirePath 等分支选择技能的自动激活
    /// </summary>
    public void ForceActivateCharacterSkill(string skillIdentifier)
    {
        if (string.IsNullOrEmpty(skillIdentifier)) return;
        if (!activeCharacterSkills.Contains(skillIdentifier))
        {
            activeCharacterSkills.Add(skillIdentifier);
            Debug.Log($"<color=magenta>[角色技能] 强制激活: {skillIdentifier}</color>");
        }
    }

    /// <summary>
    /// 判断卡片是否为分支机制卡（精准斩击/敏捷猎手），用于优先排序
    /// </summary>
    private bool IsBranchMechanicCard(SkillTreeNodeData card)
    {
        if (card == null || card.possibleOptions == null) return false;
        foreach (var option in card.possibleOptions)
        {
            if (option.effects == null) continue;
            foreach (var effect in option.effects)
            {
                if (effect.actionType == EffectActionType.ActivateCharSkill
                    && (effect.skillIdentifier == "PrecisionSlash" || effect.skillIdentifier == "AgileHunter"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 抽到角色卡时，查找对应的 CharacterSkillNode 并应用其属性效果
    /// </summary>
    private void ApplyCharacterNodeEffectsForSkill(string skillIdentifier)
    {
        // 角色卡的效果只是「激活某个机制」（如残影连斩、影分身等）
        // 机制激活通过 HasActiveCharacterSkill(skillIdentifier) 查询
        // 节点上的 PermanentUpgradeEffect（如 DamagePercent）是技能树的永久属性
        // 已在 RecalculateCharacterBonuses 中处理，不应在此重复应用

        Debug.Log($"<color=magenta>[角色卡] 激活技能: {skillIdentifier} （仅激活机制，不叠加属性）</color>");

        // 技能已通过 ActivateCharacterSkill() 注册到 activeCharacterSkills 中
        // 战斗代码通过 HasActiveCharacterSkill() 检查是否启用
    }

    #endregion
}
