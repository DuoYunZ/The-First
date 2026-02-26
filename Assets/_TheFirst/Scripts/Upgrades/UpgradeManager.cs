using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

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
        else
        {
            // --- 【双轨道系统】混合卡池抽卡逻辑 ---
            // 武器轨道：武器通过自身经验升级/分支/进化，此处仅提供武器解锁卡
            // 人物轨道：被动道具、精通卡、天赋、消耗品

            // A. 获取所有可用的武器解锁卡（不再包含升级/进化）
            List<SkillTreeNodeData> validWeapons = GenerateWeaponNodes();

            // B. 获取所有可用的被动升级
            List<SkillTreeNodeData> validPassives = new List<SkillTreeNodeData>();
            int currentPassiveCount = ownedUpgrades.Count;
            int maxPassiveSlots = 6;

            if (upgradeDatabase.passiveUpgrades != null)
            {
                foreach (var node in upgradeDatabase.passiveUpgrades)
                {
                    bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));
                    bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < node.maxLevel;
                    if (prerequisitesMet && notMaxed) validPassives.Add(node);
                }
            }

            // 打乱列表
            var shuffledWeapons = validWeapons.OrderBy(a => Random.value).ToList();
            var shuffledPassives = validPassives.OrderBy(a => Random.value).ToList();

            // C. 抽取 3 张卡
            int slotsToFill = 3;

            for (int i = 0; i < slotsToFill; i++)
            {
                // 判定权重：如果 随机数 < 0.6 (60%) 且 还有武器可升，就给武器
                // 否则给被动。如果没有被动了，也得给武器。
                bool wantWeapon = Random.value < 0.6f;

                // 确保池子里有东西
                bool hasWeapon = shuffledWeapons.Count > 0;
                bool hasPassive = shuffledPassives.Count > 0;

                SkillTreeNodeData pickedNode = null;

                if (wantWeapon && hasWeapon)
                {
                    pickedNode = shuffledWeapons[0];
                    shuffledWeapons.RemoveAt(0);
                }
                else if (hasPassive)
                {
                    pickedNode = shuffledPassives[0];
                    shuffledPassives.RemoveAt(0);
                }
                else if (hasWeapon) // 没被动了，只能给武器
                {
                    pickedNode = shuffledWeapons[0];
                    shuffledWeapons.RemoveAt(0);
                }

                if (pickedNode != null)
                {
                    offeredUpgrades.Add(pickedNode);
                }
            }
        }

        // ... 后续 UI 刷新逻辑保持不变 ...
        if (offeredUpgrades.Count == 0)
        {
            Time.timeScale = 1f;
            return;
        }

        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
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
                        case UpgradeType.AddProjectile:
                        case UpgradeType.PierceCount:
                            part.localOrbitalCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
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

        // 4. 刷新状态并关闭面板
        if (WeaponController.Instance != null) { WeaponController.Instance.RefreshAllWeaponStates(); }
        if (PassiveItemsUI.Instance != null) { PassiveItemsUI.Instance.UpdateIcons(); }
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

    // ========================================================================================
    //                        【新增】武器技能树系统 (Weapon Skill Tree)
    // ========================================================================================
    #region 武器技能树

    // 当前正在升级的武器引用
    private WeaponPart currentUpgradingWeapon;

    /// <summary>
    /// 当武器经验满升级时调用。暂停游戏，弹出该武器的技能树卡片3选1。
    /// </summary>
    public void HandleWeaponLevelUp(WeaponPart weapon)
    {
        if (weapon == null || upgradeDatabase == null) return;

        currentUpgradingWeapon = weapon;

        // 1. 获取该武器可用的技能树节点
        List<SkillTreeNodeData> availableNodes = GetAvailableWeaponSkillNodes(weapon.StatBlock);

        if (availableNodes.Count == 0)
        {
            Debug.Log($"<color=yellow>[技能树] {weapon.StatBlock.weaponName} 没有可用的技能树卡片，跳过选择。</color>");
            currentUpgradingWeapon = null;
            return;
        }

        // 2. 暂停游戏
        Time.timeScale = 0f;

        // 3. 从可用节点中随机抽取最多3个
        var shuffled = availableNodes.OrderBy(a => Random.value).ToList();
        offeredUpgrades.Clear();
        for (int i = 0; i < Mathf.Min(3, shuffled.Count); i++)
        {
            offeredUpgrades.Add(shuffled[i]);
        }

        // 4. 展示卡片UI
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());

        Debug.Log($"<color=cyan>[技能树] {weapon.StatBlock.weaponName} 升级！展示 {offeredUpgrades.Count} 张技能卡片。</color>");
    }

    /// <summary>
    /// 获取指定武器当前可用的技能树节点（已筛选前置/互斥/唯一性）。
    /// </summary>
    public List<SkillTreeNodeData> GetAvailableWeaponSkillNodes(WeaponStatBlock weaponStats)
    {
        List<SkillTreeNodeData> result = new List<SkillTreeNodeData>();

        if (upgradeDatabase.weaponSkillNodes == null) return result;

        foreach (var node in upgradeDatabase.weaponSkillNodes)
        {
            if (node == null) continue;

            // A. 必须是该武器的节点
            if (node.associatedWeapon != weaponStats) continue;

            // B. 唯一性检查：已拥有则跳过（maxLevel通常为1）
            if (ownedUpgrades.ContainsKey(node) && ownedUpgrades[node] >= node.maxLevel)
                continue;

            // C. 前置检查：所有前置节点必须已拥有 (AND 逻辑)
            if (node.prerequisites != null && node.prerequisites.Count > 0)
            {
                bool allPrereqMet = node.prerequisites.All(p => p != null && ownedUpgrades.ContainsKey(p));
                if (!allPrereqMet) continue;
            }

            // D. 互斥检查：如果玩家已拥有列表中任一节点，则剔除
            if (node.mutuallyExclusive != null && node.mutuallyExclusive.Count > 0)
            {
                bool hasConflict = node.mutuallyExclusive.Any(m => m != null && ownedUpgrades.ContainsKey(m));
                if (hasConflict) continue;
            }

            result.Add(node);
        }

        return result;
    }

    /// <summary>
    /// 检查玩家是否已拥有某个技能节点
    /// </summary>
    public bool HasSkillNode(SkillTreeNodeData node)
    {
        return node != null && ownedUpgrades.ContainsKey(node);
    }

    #endregion
}