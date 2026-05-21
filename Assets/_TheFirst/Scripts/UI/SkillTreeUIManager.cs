using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SkillTreeUIManager : MonoBehaviour
{
    [Header("Weapon Data")]
    public List<WeaponSkillTree> allWeaponTrees;

    [Header("Passive Data")]
    public List<PassiveItemData> allPassiveItems;

    [Header("Fusion Data")]
    public List<FusionRecipeSO> allFusionRecipes;
    public List<WeaponFusionRecipeSO> allWeaponFusionRecipes;
    public List<EvolutionRecipeSO> allEvolutionRecipes;
    public List<SO_ComboUltimate> allComboUltimates;

    [Header("Sidebar")]
    public Transform sidebarContent;
    public GameObject sidebarItemPrefab;

    [Header("Locked View")]
    public GameObject lockedViewRoot;
    public TextMeshProUGUI lockConditionText;
    public Slider lockProgressBar;
    public TextMeshProUGUI lockProgressText;
    public Image lockedWeaponIcon;

    [Header("Weapon Stats View")]
    public GameObject weaponStatsViewRoot;
    public Image weaponStatsIcon;
    public TextMeshProUGUI weaponStatsName;
    public Transform weaponStatsContainer;
    public GameObject weaponStatItemPrefab;

    [Header("Weapon Stat Icons")]
    public Sprite iconDamage;
    public Sprite iconFireRate;
    public Sprite iconRange;
    public Sprite iconProjectile;

    [Header("Passive Description View")]
    public GameObject passiveDescViewRoot;
    public Image passiveDescIcon;
    public TextMeshProUGUI passiveDescName;
    public TextMeshProUGUI passiveDescText;

    [Header("Common UI")]
    public Button closeButton;

    [Header("Font")]
    [Tooltip("Chinese UI font. Recommended: Assets/_TheFirst/Art/Fonts/けいなんポップ体.asset")]
    public TMP_FontAsset chineseUIFont;

    [Header("Legacy Skill Tree References")]
    public GameObject unlockedViewRoot;
    public Transform hexNodesContainer;
    public GameObject upgradeNodePrefab;
    public Transform skillDescriptionContainer;
    public GameObject skillDescriptionPrefab;

    private enum SelectedEntryType { None, Weapon, Passive, Fusion }
    private enum CodexCategory { Weapons, Passives, Fusion, Monsters }
    private enum WeaponBuildFamily { Slash, Spell, Engineering, Guardian, Hybrid }

    private sealed class RuntimeCodexTab
    {
        public CodexCategory category;
        public Image background;
        public TextMeshProUGUI label;
        public Color activeColor;
        public Color inactiveColor;
    }

    private sealed class PassiveEvolutionDesign
    {
        public UpgradeType passiveType;
        public string resultName;
        public string description;

        public PassiveEvolutionDesign(UpgradeType passiveType, string resultName, string description)
        {
            this.passiveType = passiveType;
            this.resultName = resultName;
            this.description = description;
        }
    }

    private sealed class FusionConditionVisual
    {
        public Sprite icon;
        public bool isMet;
        public string fallbackText;

        public FusionConditionVisual(Sprite icon, bool isMet, string fallbackText = null)
        {
            this.icon = icon;
            this.isMet = isMet;
            this.fallbackText = fallbackText;
        }
    }

    private SelectedEntryType currentEntryType = SelectedEntryType.None;
    private CodexCategory currentCodexCategory = CodexCategory.Weapons;
    private WeaponSkillTree currentSelectedTree;
    private PassiveItemData currentSelectedPassive;
    private FusionRecipeSO currentSelectedFusionRecipe;
    private WeaponFusionRecipeSO currentSelectedWeaponFusionRecipe;
    private EvolutionRecipeSO currentSelectedEvolutionRecipe;
    private readonly List<SkillTreeSidebarItem> sidebarItems = new List<SkillTreeSidebarItem>();
    private readonly List<RuntimeCodexTab> codexTabs = new List<RuntimeCodexTab>();
    private readonly List<GameObject> activeStatSlots = new List<GameObject>();
    private GameObject runtimeSidebarItemPrefab;
    private GameObject runtimeStatItemPrefab;
    private TextMeshProUGUI codexDetailBodyText;
    private TextMeshProUGUI codexHeroSummaryText;
    private TextMeshProUGUI codexTagsText;
    private TextMeshProUGUI codexCollectionText;
    private TextMeshProUGUI codexRecommendedTitleText;
    private Transform codexRecommendationContainer;
    private Transform lockConditionIconContainer;
    [Header("Runtime Layout")]
    public bool useVampireStyleCodexLayout = true;

    [Header("Runtime Art Variant")]
    [Tooltip("Use the atlas-sliced codex UI art under Resources/UI/DemoCodexCutout. Keep disabled until the sliced book atlas preserves the reference aspect ratio.")]
    public bool useCutoutCodexArt = false;

    private const string DemoCodexSpritePath = "UI/DemoCodex/";
    private const string CutoutCodexSpritePath = "UI/DemoCodexCutout/";
    private const bool ForceProgrammaticCodexArt = true;
    private const string PopUIFontAssetGuid = "491b2d422301899469285199dbc67db4";

    private bool UseCutoutCodexArt => !ForceProgrammaticCodexArt && useCutoutCodexArt;
    private bool UseVampireStyleCodexLayout => useVampireStyleCodexLayout;
    private bool IsEnglishLanguage => LocalizationManager.CurrentLanguage == SystemLanguage.English;

    private string L(string zh, string en)
    {
        return IsEnglishLanguage ? en : zh;
    }

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    private void EnsureCodexData()
    {
        if (allWeaponTrees == null) allWeaponTrees = new List<WeaponSkillTree>();
        if (allPassiveItems == null) allPassiveItems = new List<PassiveItemData>();
        if (allFusionRecipes == null) allFusionRecipes = new List<FusionRecipeSO>();
        if (allWeaponFusionRecipes == null) allWeaponFusionRecipes = new List<WeaponFusionRecipeSO>();
        if (allEvolutionRecipes == null) allEvolutionRecipes = new List<EvolutionRecipeSO>();
        if (allComboUltimates == null) allComboUltimates = new List<SO_ComboUltimate>();

#if UNITY_EDITOR
        LoadEditorAssetsIntoList("Assets/_TheFirst/Prefabs/Passive Item Data", allPassiveItems);
        LoadEditorAssetsIntoList("Assets/_TheFirst/Prefabs/Skill Tree", allWeaponTrees);
        LoadEditorAssetsIntoList("Assets/_TheFirst/GameData", allFusionRecipes);
        LoadEditorAssetsIntoList("Assets/_TheFirst/Prefabs/Energy Stone", allEvolutionRecipes);
        LoadEditorAssetsIntoList("Assets/_TheFirst/GameData", allEvolutionRecipes);
        LoadEditorAssetsIntoList("Assets/_TheFirst/GameData", allWeaponFusionRecipes);
        LoadEditorAssetsIntoList("Assets/_TheFirst/GameData", allComboUltimates);

        if (!HasWeaponTree("LightningStrike"))
        {
            WeaponSkillTree lightningTree = AssetDatabase.LoadAssetAtPath<WeaponSkillTree>(
                "Assets/_TheFirst/Prefabs/Skill Tree/LightningStrike_SkillTree.asset");
            if (lightningTree != null) allWeaponTrees.Add(lightningTree);
        }
#endif
        EnsureFutureWeaponCodexEntries();
        EnsurePassiveEvolutionCodexEntries();
    }

#if UNITY_EDITOR
    private void LoadEditorAssetsIntoList<T>(string folder, List<T> target) where T : UnityEngine.Object
    {
        if (target == null) return;

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && !target.Contains(asset)) target.Add(asset);
        }
    }
#endif

    private void EnsureFutureWeaponCodexEntries()
    {
        AddFutureWeaponCodexEntry("MoonSlash", "月牙斩", WeaponBehaviorType.MeleeAOE,
            FindWeaponIcon("Blade", "Hurricane"), 28, 0, 2.8f, 0.75f, 1, 1, 1200,
            "未来解锁：斩击流进阶武器。宽月牙刀光向前推进，适合刀光数量、剑圣之魂和风系连携。",
            "宽幅远程刀光。每第3次攻击释放双月牙；刀光数量越高，屏幕前方越像扇形清场。",
            "斩击扩展 / 刀光数量 / 剑圣之魂 / 风刃连携");

        AddFutureWeaponCodexEntry("CrossBlade", "十字回斩", WeaponBehaviorType.MeleeAOE,
            FindWeaponIcon("Blade", "Orbit"), 20, 0, 2.4f, 0.95f, 2, 1, 1100,
            "未来解锁：斩击流控制武器。前后十字刀光保护近身区域，适合守护环绕流混搭。",
            "前后交错的十字斩。升级后追加侧向回斩，适合喜欢贴脸穿怪、绕圈清场的剑士 build。",
            "斩击防线 / 贴身生存 / 守护混搭");

        AddFutureWeaponCodexEntry("PumpkinScythe", "南瓜巨镰", WeaponBehaviorType.MeleeAOE,
            FindWeaponIcon("Blade", "Landmine"), 52, 0, 3.2f, 1.45f, 1, 2, 1400,
            "未来解锁：斩击流重武器。慢速大范围收割，适合暴击、处决和剑圣之魂。",
            "低频高伤的巨镰横扫。命中低生命敌人会触发小范围处决爆裂，形成斩击流的后期爆发路线。",
            "重斩 / 暴击处决 / 低频高伤");

        AddFutureWeaponCodexEntry("CandyMeteor", "糖果陨星", WeaponBehaviorType.ParabolicAOE,
            FindWeaponIcon("Fireball", "Grenade"), 24, 42, 3.8f, 1.05f, 1, 0, 1000,
            "未来解锁：法术流火系武器。延迟落点爆炸，适合火系连携和元素共鸣。",
            "锁定敌群中心落下糖果陨星。落地前有短延迟，升级后留下焦糖燃烧区，鼓励控场后爆破。",
            "火系法术 / 延迟爆破 / 燃烧地面");

        AddFutureWeaponCodexEntry("FrostMirror", "冰镜术", WeaponBehaviorType.CreateAndForget,
            FindWeaponIcon("IceShard", "FrostNova"), 18, 24, 2.6f, 1.2f, 3, 1, 950,
            "未来解锁：法术流冰系武器。生成冰镜折射碎片，适合冰冻、弹数和元素共鸣。",
            "短暂召唤冰镜，向多个方向折射冰片。冰冻敌人会被后续碎片优先追击，形成冰系控场核心。",
            "冰系法术 / 折射弹幕 / 冰冻连锁");

        AddFutureWeaponCodexEntry("VineHex", "藤蔓咒缚", WeaponBehaviorType.PersistentAOE,
            FindWeaponIcon("Aura", "FrostNova"), 10, 16, 2.8f, 1.4f, 1, 0, 950,
            "未来解锁：法术流控制武器。藤蔓区域缠绕敌人，适合腐蚀、减速和持续时间。",
            "在敌群脚下生成藤蔓圈，持续拉扯并减速。后期可转为腐蚀咒地，给法师提供非冰系控场路线。",
            "控制法术 / 持续区域 / 腐蚀路线");

        AddFutureWeaponCodexEntry("PumpkinMortar", "南瓜迫击炮", WeaponBehaviorType.ParabolicAOE,
            FindWeaponIcon("Grenade", "Landmine"), 30, 48, 4.2f, 0.85f, 1, 0, 1100,
            "未来解锁：机械工程流爆破武器。远距离抛射，适合地雷、喷火塔和机械共鸣。",
            "高抛南瓜炮弹轰击远处敌群。升级后弹坑可变成临时陷阱，和地雷/喷火塔组成阵地流。",
            "工程爆破 / 远程炮击 / 阵地联动");

        AddFutureWeaponCodexEntry("GearSaw", "齿轮锯轮", WeaponBehaviorType.Boomerang,
            FindWeaponIcon("FlameDagger", "Blade"), 18, 0, 2.2f, 0.9f, 1, 999, 900,
            "未来解锁：机械工程流弹跳武器。锯轮往返切割，适合持续时间、穿透和机械共鸣。",
            "发射可回收锯轮，穿过敌人后回到玩家身边。升级后可在墙边/敌群间弹跳，形成工程流近中距离输出。",
            "工程投射 / 往返切割 / 穿透持续");

        AddFutureWeaponCodexEntry("SyrupSprayer", "糖浆喷射器", WeaponBehaviorType.Beam,
            FindWeaponIcon("Landmine", "Fireball"), 8, 18, 1.7f, 1.1f, 1, 0, 900,
            "未来解锁：机械工程流控场武器。扇形喷射糖浆，适合减速、燃烧和喷火塔路线。",
            "向近距离喷出糖浆束，造成持续减速。遇到火系效果会变成焦糖火线，是工程+火法的桥接武器。",
            "工程控场 / 减速喷射 / 火系桥接");

        AddFutureWeaponCodexEntry("CandyBulwark", "糖盾壁垒", WeaponBehaviorType.Orbital,
            FindWeaponIcon("Orbit", "Aura"), 14, 0, 1.6f, 0f, 1, 1, 1000,
            "未来解锁：守护环绕流武器。糖盾环绕吸收压力，适合环绕数量、持续时间和回血。",
            "数枚糖盾围绕玩家，命中敌人时短暂停顿形成防线。升级后破盾爆裂，可保护低机动 build。",
            "守护环绕 / 贴身防线 / 破盾爆裂");

        AddFutureWeaponCodexEntry("LanternWisp", "灯笼精灵", WeaponBehaviorType.SummonDrone,
            FindWeaponIcon("Aura", "ChainLightning"), 12, 18, 2.4f, 0.75f, 2, 1, 950,
            "未来解锁：守护支援武器。灯笼精灵自动游走攻击，适合元素共鸣和生存辅助。",
            "召唤绕身灯笼精灵，优先攻击靠近玩家的敌人。可分支成火灯、冰灯或雷灯，补足守护流的元素触发。",
            "守护召唤 / 自动拦截 / 元素分支");

        AddFutureWeaponCodexEntry("MirrorCharm", "镜面护符", WeaponBehaviorType.Aura,
            FindWeaponIcon("Aura", "Orbit"), 6, 10, 2.0f, 0f, 1, 0, 1000,
            "未来解锁：守护反击武器。周期性展开反射护符，适合护甲、反伤和环绕流。",
            "每隔数秒展开镜面护符，反弹近身伤害并向外释放碎光。适合作为后期高压波次的防御答案。",
            "守护反击 / 护甲反伤 / 生存核心");
    }

    private void AddFutureWeaponCodexEntry(
        string id,
        string displayName,
        WeaponBehaviorType behavior,
        Sprite icon,
        int directDamage,
        int areaDamage,
        float areaRadius,
        float cooldownSeconds,
        int projectileCount,
        int pierceCount,
        int maxEnergy,
        string lockedDescription,
        string designNote,
        string synergyLine)
    {
        if (HasWeaponTree(id)) return;

        WeaponStatBlock weapon = ScriptableObject.CreateInstance<WeaponStatBlock>();
        weapon.name = "Future_" + id;
        weapon.weaponID = id;
        weapon.weaponName = displayName;
        weapon.weaponIcon = icon;
        weapon.behavior = behavior;
        weapon.weaponGlowColor = GetFutureWeaponGlowColor(behavior);
        weapon.baseFireRate = cooldownSeconds > 0f ? 1f / cooldownSeconds : 0f;
        weapon.projectileCount = Mathf.Max(1, projectileCount);
        weapon.basePierceCount = Mathf.Max(0, pierceCount);
        weapon.baseDirectDamage = Mathf.Max(0, directDamage);
        weapon.baseAoeDamage = Mathf.Max(0, areaDamage);
        weapon.baseAoeRadius = Mathf.Max(0f, areaRadius);
        weapon.baseProjectileLifetime = 5f;
        weapon.baseLaunchForce = 20f;
        weapon.maxLevel = 8;
        weapon.usesProficiency = true;
        weapon.xpSource = WeaponXpSource.DamageDealt;
        weapon.xpGainFactor = 1f;
        weapon.damageGrowthPerLevel = 0.12f;
        weapon.cooldownGrowthPerLevel = 0.04f;
        weapon.areaGrowthPerLevel = 0.08f;
        weapon.usesEnergy = true;
        weapon.maxEnergy = Mathf.Max(600, maxEnergy);
        weapon.energyGainPerDamage = 1f;
        weapon.ultimateDamage = Mathf.Max(directDamage, areaDamage) * 4;
        weapon.ultimateRadius = Mathf.Max(4f, areaRadius + 2f);
        weapon.ultimateDescription = designNote;
        weapon.ultimateDescriptionEN = synergyLine;

        if (behavior == WeaponBehaviorType.Orbital)
        {
            weapon.baseOrbitalCount = Mathf.Max(1, projectileCount);
            weapon.baseOrbitalRadius = 3f;
            weapon.baseOrbitalSpeed = 90f;
            weapon.baseDuration = 12f;
        }
        else if (behavior == WeaponBehaviorType.PersistentAOE || behavior == WeaponBehaviorType.Aura)
        {
            weapon.baseAreaDuration = 5f;
            weapon.baseAreaTickInterval = 0.5f;
            weapon.baseAreaDamagePerTick = Mathf.Max(1, areaDamage / 4);
            weapon.baseDuration = 10f;
        }
        else if (behavior == WeaponBehaviorType.MeleeAOE)
        {
            weapon.multiHitCount = Mathf.Max(1, projectileCount);
            weapon.attackAngle = 110f;
            weapon.baseStunChance = 0.08f;
        }

        WeaponSkillTree tree = ScriptableObject.CreateInstance<WeaponSkillTree>();
        tree.name = id + "_FutureSkillTree";
        tree.associatedWeapon = weapon;
        tree.isDefaultUnlocked = false;
        tree.unlockStatKey = "FutureWeapon_" + id;
        tree.unlockThreshold = 0;
        tree.lockedDescription = lockedDescription;
        tree.allNodesInTree = new List<WeaponUpgradeNode>();

        allWeaponTrees.Add(tree);
    }

    private Sprite FindWeaponIcon(params string[] weaponIds)
    {
        if (weaponIds == null || allWeaponTrees == null) return null;

        foreach (string weaponId in weaponIds)
        {
            if (string.IsNullOrEmpty(weaponId)) continue;
            foreach (WeaponSkillTree tree in allWeaponTrees)
            {
                WeaponStatBlock weapon = tree != null ? tree.associatedWeapon : null;
                if (weapon == null || weapon.weaponIcon == null) continue;
                if (weapon.weaponID == weaponId) return weapon.weaponIcon;
            }
        }

        foreach (WeaponSkillTree tree in allWeaponTrees)
        {
            WeaponStatBlock weapon = tree != null ? tree.associatedWeapon : null;
            if (weapon != null && weapon.weaponIcon != null) return weapon.weaponIcon;
        }

        return null;
    }

    private Color GetFutureWeaponGlowColor(WeaponBehaviorType behavior)
    {
        switch (behavior)
        {
            case WeaponBehaviorType.MeleeAOE: return new Color(1f, 0.48f, 0.14f, 1.8f);
            case WeaponBehaviorType.ParabolicAOE:
            case WeaponBehaviorType.CreateAndForget:
            case WeaponBehaviorType.PersistentAOE: return new Color(0.45f, 0.75f, 1f, 1.6f);
            case WeaponBehaviorType.Landmine:
            case WeaponBehaviorType.Beam:
            case WeaponBehaviorType.Boomerang: return new Color(1f, 0.76f, 0.2f, 1.6f);
            case WeaponBehaviorType.Orbital:
            case WeaponBehaviorType.Aura:
            case WeaponBehaviorType.SummonDrone: return new Color(0.55f, 1f, 0.58f, 1.4f);
            default: return new Color(1f, 0.6f, 0.25f, 1.4f);
        }
    }

    private bool HasWeaponTree(string weaponId)
    {
        if (allWeaponTrees == null || string.IsNullOrEmpty(weaponId)) return false;

        foreach (WeaponSkillTree tree in allWeaponTrees)
        {
            if (tree == null || tree.associatedWeapon == null) continue;
            if (tree.associatedWeapon.weaponID == weaponId) return true;
        }

        return false;
    }

    private void EnsurePassiveEvolutionCodexEntries()
    {
        if (allWeaponTrees == null || allPassiveItems == null || allWeaponFusionRecipes == null) return;

        HashSet<string> existingKeys = new HashSet<string>();
        foreach (WeaponFusionRecipeSO recipe in allWeaponFusionRecipes)
        {
            if (recipe == null) continue;
            if (!string.IsNullOrEmpty(recipe.recipeId)) existingKeys.Add(recipe.recipeId);
            PassiveItemData passive = GetFirstPassiveCondition(recipe);
            if (recipe.triggerWeapon != null && passive != null)
            {
                existingKeys.Add(BuildPassiveEvolutionRecipeId(recipe.triggerWeapon, passive));
            }
        }

        foreach (WeaponSkillTree tree in allWeaponTrees)
        {
            WeaponStatBlock weapon = tree != null ? tree.associatedWeapon : null;
            if (weapon == null || IsFutureCodexWeapon(weapon)) continue;
            if (!DemoContentGate.IsWeaponAllowed(weapon)) continue;

            PassiveEvolutionDesign design = GetPassiveEvolutionDesign(weapon);
            PassiveItemData passive = PickPassiveEvolutionItemForWeapon(weapon, design);
            if (passive == null) continue;

            string recipeId = BuildPassiveEvolutionRecipeId(weapon, passive);
            if (!existingKeys.Add(recipeId)) continue;

            WeaponFusionRecipeSO recipe = ScriptableObject.CreateInstance<WeaponFusionRecipeSO>();
            recipe.name = "CodexOnly_" + recipeId;
            recipe.recipeId = recipeId;
            recipe.recipeName = BuildPassiveEvolutionName(weapon, passive, design);
            recipe.description = BuildPassiveEvolutionDescription(weapon, passive, design);
            recipe.triggerWeapon = weapon;
            recipe.requiredStage = WeaponStage.Base;
            recipe.requiredWeaponLevel = 5;
            recipe.conditions = new[]
            {
                new FusionCondition
                {
                    type = ConditionType.Passive,
                    requiredPassiveItem = passive,
                    requiredPassiveId = passive.itemName,
                    requiredPassiveLevel = PassiveItemData.PassiveCapstoneLevel
                }
            };
            recipe.fusionType = FusionType.Upgrade;
            recipe.resultWeapon = CreateCodexOnlyEvolutionWeapon(weapon, passive, design);
            recipe.cardIcon = recipe.resultWeapon != null ? recipe.resultWeapon.weaponIcon : weapon.weaponIcon;
            recipe.cardColor = weapon.weaponGlowColor;
            recipe.codexOnly = true;
            recipe.codexRevealWeaponLevel = 5;
            recipe.hideResultUntilRevealed = true;
            allWeaponFusionRecipes.Add(recipe);
        }
    }

    private PassiveItemData PickPassiveEvolutionItemForWeapon(WeaponStatBlock weapon, PassiveEvolutionDesign design)
    {
        if (weapon == null) return null;

        if (design != null)
        {
            PassiveItemData designedPassive = FindPassiveByTypeForEvolution(design.passiveType);
            if (designedPassive != null) return designedPassive;
        }

        PassiveItemData passive = null;
        if (WeaponBuildTagUtility.IsSlashWeapon(weapon))
        {
            passive = FindPassiveByTypeForEvolution(UpgradeType.SwordmasterSoul);
        }
        else if (WeaponBuildTagUtility.IsMechanicalWeapon(weapon))
        {
            passive = FindPassiveByTypeForEvolution(UpgradeType.MechanicalResonance);
        }
        else if (WeaponBuildTagUtility.IsGuardianWeapon(weapon))
        {
            passive = FindPassiveByTypeForEvolution(UpgradeType.Armor);
        }
        else if (WeaponBuildTagUtility.IsElementalWeapon(weapon))
        {
            passive = HasWeaponTag(weapon, WeaponBuildTag.Lightning)
                ? FindPassiveByTypeForEvolution(UpgradeType.ThunderWill)
                : FindPassiveByTypeForEvolution(UpgradeType.ElementalResonance);
        }

        if (passive == null)
        {
            switch (weapon.behavior)
            {
                case WeaponBehaviorType.ParabolicAOE:
                case WeaponBehaviorType.PersistentAOE:
                case WeaponBehaviorType.FrostNova:
                    passive = FindPassiveByTypeForEvolution(UpgradeType.AoeRadius);
                    break;
                case WeaponBehaviorType.Orbital:
                case WeaponBehaviorType.Aura:
                    passive = FindPassiveByTypeForEvolution(UpgradeType.WeaponDuration);
                    break;
                default:
                    passive = FindPassiveByTypeForEvolution(UpgradeType.WeaponDamage);
                    break;
            }
        }

        return passive != null && passive.EffectiveMaxLevel >= PassiveItemData.PassiveCapstoneLevel ? passive : FindPassiveByTypeForEvolution(UpgradeType.WeaponDamage);
    }

    private PassiveEvolutionDesign GetPassiveEvolutionDesign(WeaponStatBlock weapon)
    {
        if (weapon == null) return null;

        string id = !string.IsNullOrEmpty(weapon.weaponID) ? weapon.weaponID : weapon.name;
        string weaponName = !string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponName : weapon.name;

        switch (id)
        {
            case "Blade":
                return new PassiveEvolutionDesign(UpgradeType.SwordmasterSoul, "万刃剑阵", "斩击与剑圣之魂共鸣。进化后斩击不再只是单次挥砍，而是在玩家周围形成连续剑阵，适合剑士后期清潮。");
            case "Fireball":
                return new PassiveEvolutionDesign(UpgradeType.ArcaneMastery, "爆炎星核", "火焰球与奥术精通共鸣。进化后火球爆炸会追加奥术裂变，形成更密集的连环爆破。");
            case "IceShard":
                return new PassiveEvolutionDesign(UpgradeType.ElementalResonance, "极寒晶雨", "冰锥术与元素共鸣共鸣。进化后冰锥分裂成持续落下的寒晶雨，增强控场和范围压制。");
            case "LightningStrike":
                return new PassiveEvolutionDesign(UpgradeType.ThunderWill, "天罚雷柱", "雷击与雷鸣意志共鸣。进化后落雷会留下短暂雷柱，持续惩罚高密度敌群。");
            case "ChainLightning":
                return new PassiveEvolutionDesign(UpgradeType.ThunderWill, "雷暴网络", "闪电链与雷鸣意志共鸣。进化后连锁会在敌群之间织成雷网，适合处理潮水式推进。");
            case "Hurricane":
                return new PassiveEvolutionDesign(UpgradeType.MoveSpeed, "风暴回廊", "小龙卷与双筒跑鞋共鸣。进化后龙卷围绕玩家路径形成风道，兼顾机动和清线。");
            case "Grenade":
                return new PassiveEvolutionDesign(UpgradeType.AoeRadius, "南瓜重炮", "榴弹与甜筒望远镜共鸣。进化后爆炸范围扩大，并在中心留下二段冲击。");
            case "Landmine":
                return new PassiveEvolutionDesign(UpgradeType.MechanicalResonance, "自动雷场", "地雷与机械共鸣共鸣。进化后地雷会自动布成阵地，让工程流获得稳定防线。");
            case "Orbit":
                return new PassiveEvolutionDesign(UpgradeType.Armor, "磁暴岩盾", "大地岩盾与西瓜装甲共鸣。进化后环绕护盾会释放磁暴脉冲，强化贴身生存。");
            case "SupportAura":
                return new PassiveEvolutionDesign(UpgradeType.KillHeal, "生命圣域", "光环与灵魂汲取共鸣。进化后光环会周期治疗，并在击杀节奏中扩大安全区。");
            case "FlameDagger":
                return new PassiveEvolutionDesign(UpgradeType.Luck, "追魂灵刃", "灵能飞刃与幸运四叶草共鸣。进化后飞刃更容易锁定关键目标，并触发额外追击。");
            case "FrostNova":
                return new PassiveEvolutionDesign(UpgradeType.WeaponDuration, "永冻结界", "冰霜新星与永恒之球共鸣。进化后新星残留冻结结界，压住敌潮推进。");
            case "SuperMech":
                return new PassiveEvolutionDesign(UpgradeType.MechanicalResonance, "南瓜巨神兵", "巨型机器人与机械共鸣共鸣。进化后机器人进入超频形态，强化工程流终局幻想。");
        }

        if (!string.IsNullOrEmpty(weaponName))
        {
            if (weaponName.Contains("Beam") || weaponName.Contains("镭") || weaponName.Contains("光束"))
            {
                return new PassiveEvolutionDesign(UpgradeType.WeaponFireRate, "棱镜核心", "光束武器与时光曲奇共鸣。进化后光束获得更稳定的聚焦窗口。");
            }

            if (weaponName.Contains("飞刀") || weaponName.Contains("灵刃"))
            {
                return new PassiveEvolutionDesign(UpgradeType.Luck, "追魂灵刃", "飞刀武器与幸运四叶草共鸣。进化后飞刀获得更强锁定和额外追击。");
            }
        }

        return null;
    }

    private PassiveItemData FindPassiveByTypeForEvolution(UpgradeType statType)
    {
        if (allPassiveItems == null) return null;
        foreach (PassiveItemData passive in allPassiveItems)
        {
            if (passive == null || passive.statType != statType) continue;
            if (!DemoContentGate.IsPassiveAllowed(passive)) continue;
            if (passive.EffectiveMaxLevel < PassiveItemData.PassiveCapstoneLevel) continue;
            return passive;
        }

        return null;
    }

    private static bool HasWeaponTag(WeaponStatBlock weapon, WeaponBuildTag tag)
    {
        return weapon != null && weapon.buildTags != null && weapon.buildTags.Contains(tag);
    }

    private string BuildPassiveEvolutionRecipeId(WeaponStatBlock weapon, PassiveItemData passive)
    {
        string weaponId = weapon != null && !string.IsNullOrEmpty(weapon.weaponID) ? weapon.weaponID : weapon != null ? weapon.name : "Weapon";
        string passiveId = passive != null && !string.IsNullOrEmpty(passive.itemName) ? passive.itemName : passive != null ? passive.name : "Passive";
        return "PassiveEvolution_" + weaponId + "_" + passiveId;
    }

    private string BuildPassiveEvolutionName(WeaponStatBlock weapon, PassiveItemData passive, PassiveEvolutionDesign design)
    {
        if (design != null && !string.IsNullOrEmpty(design.resultName)) return design.resultName;
        string suffix = GetPassiveEvolutionSuffix(passive);
        return $"{GetWeaponDisplayName(weapon)}·{suffix}";
    }

    private string BuildPassiveEvolutionDescription(WeaponStatBlock weapon, PassiveItemData passive, PassiveEvolutionDesign design)
    {
        string weaponName = GetWeaponDisplayName(weapon);
        string passiveName = passive != null ? passive.itemName : "\u6307\u5b9a\u9053\u5177";
        string resultName = BuildPassiveEvolutionName(weapon, passive, design);
        string designText = design != null && !string.IsNullOrEmpty(design.description)
            ? design.description
            : $"{weaponName} \u4e0e {passiveName} \u5171\u9e23\uff0c\u8fdb\u5316\u4e3a {resultName}\u3002";
        return $"{designText}\n\n\u8fdb\u5316\u95e8\u69db: {weaponName} Lv.5 + {passiveName} Lv.{PassiveItemData.PassiveCapstoneLevel}\u3002";
    }

    private string GetPassiveEvolutionSuffix(PassiveItemData passive)
    {
        if (passive == null) return "\u8fdb\u5316";
        switch (passive.statType)
        {
            case UpgradeType.SwordmasterSoul: return "\u5251\u5723";
            case UpgradeType.ElementalResonance: return "\u5143\u7d20";
            case UpgradeType.ArcaneMastery: return "\u5965\u672f";
            case UpgradeType.MechanicalResonance: return "\u8d85\u9891";
            case UpgradeType.ThunderWill: return "\u96f7\u9e23";
            case UpgradeType.Armor: return "\u5b88\u62a4";
            case UpgradeType.AoeRadius: return "\u5de8\u578b";
            case UpgradeType.WeaponDuration: return "\u6c38\u6052";
            case UpgradeType.WeaponDamage: return "\u5f3a\u653b";
            default: return "\u8fdb\u5316";
        }
    }

    private WeaponStatBlock CreateCodexOnlyEvolutionWeapon(WeaponStatBlock weapon, PassiveItemData passive, PassiveEvolutionDesign design)
    {
        if (weapon == null) return null;

        WeaponStatBlock result = ScriptableObject.CreateInstance<WeaponStatBlock>();
        result.name = "CodexOnly_Result_" + BuildPassiveEvolutionRecipeId(weapon, passive);
        result.weaponID = "CodexOnly_" + (!string.IsNullOrEmpty(weapon.weaponID) ? weapon.weaponID : weapon.name);
        result.weaponName = BuildPassiveEvolutionName(weapon, passive, design);
        result.weaponIcon = weapon.weaponIcon;
        result.weaponGlowColor = weapon.weaponGlowColor;
        result.behavior = weapon.behavior;
        result.baseDirectDamage = weapon.baseDirectDamage;
        result.baseAoeDamage = weapon.baseAoeDamage;
        result.baseAoeRadius = weapon.baseAoeRadius;
        result.baseFireRate = weapon.baseFireRate;
        result.basePierceCount = weapon.basePierceCount;
        result.projectileCount = weapon.projectileCount;
        result.maxLevel = 1;
        if (weapon.buildTags != null) result.buildTags = new List<WeaponBuildTag>(weapon.buildTags);
        return result;
    }

    private Sprite LoadDemoUISprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        if (UseCutoutCodexArt)
        {
            Sprite cutoutSprite = Resources.Load<Sprite>(CutoutCodexSpritePath + spriteName);
            if (cutoutSprite != null) return cutoutSprite;
        }

        return Resources.Load<Sprite>(DemoCodexSpritePath + spriteName);
    }

    private bool HasCutoutUISprite(string spriteName)
    {
        return UseCutoutCodexArt && !string.IsNullOrEmpty(spriteName) && Resources.Load<Sprite>(CutoutCodexSpritePath + spriteName) != null;
    }

    private bool ApplyDemoSprite(Image image, string spriteName, Color tint, bool preserveAspect = false)
    {
        if (image == null) return false;

        Sprite sprite = LoadDemoUISprite(spriteName);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.color = tint;
            return true;
        }

        image.color = tint;
        return false;
    }

    private void EnsurePolishedCodexLayout()
    {
        if (UseVampireStyleCodexLayout)
        {
            EnsureVampireStyleCodexLayout();
            return;
        }

        Transform oldRuntime = transform.Find("Runtime_CodexBook");
        if (oldRuntime != null) Destroy(oldRuntime.gameObject);

        Transform oldBackdrop = transform.Find("Runtime_CodexBackdrop");
        if (oldBackdrop != null) Destroy(oldBackdrop.gameObject);

        GameObject backdrop = CreateUIObject("Runtime_CodexBackdrop", transform);
        StretchToParent(backdrop.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.04f, 0.025f, 0.015f, 0.68f);
        backdropImage.raycastTarget = true;

        GameObject book = CreateUIObject("Runtime_CodexBook", transform);
        RectTransform bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.anchoredPosition = Vector2.zero;
        bookRect.sizeDelta = new Vector2(1560f, 860f);
        Image bookImage = book.AddComponent<Image>();
        ApplyDemoSprite(bookImage, "codex_book_frame", Color.white);
        Shadow bookShadow = book.AddComponent<Shadow>();
        bookShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        bookShadow.effectDistance = new Vector2(0f, -10f);

        if (!UseCutoutCodexArt)
        {
            GameObject spine = CreatePanel("Runtime_CodexSpine", book.transform, new Color(0.18f, 0.10f, 0.07f, 0.88f));
            ApplyDemoSprite(spine.GetComponent<Image>(), "codex_spine", Color.white);
            RectTransform spineRect = spine.GetComponent<RectTransform>();
            spineRect.anchorMin = new Vector2(0.5f, 0f);
            spineRect.anchorMax = new Vector2(0.5f, 1f);
            spineRect.pivot = new Vector2(0.5f, 0.5f);
            spineRect.anchoredPosition = Vector2.zero;
            spineRect.sizeDelta = new Vector2(44f, -70f);
        }

        GameObject leftPage = UseCutoutCodexArt
            ? CreateUIObject("Runtime_CodexLeftPage", book.transform)
            : CreatePanel("Runtime_CodexLeftPage", book.transform, new Color(0.96f, 0.80f, 0.52f, 1f));
        Image leftPageImage = leftPage.GetComponent<Image>();
        if (leftPageImage != null) ApplyDemoSprite(leftPageImage, "codex_page_left", Color.white);
        RectTransform leftPageRect = leftPage.GetComponent<RectTransform>();
        if (UseCutoutCodexArt)
        {
            leftPageRect.anchorMin = new Vector2(0f, 0.5f);
            leftPageRect.anchorMax = new Vector2(0f, 0.5f);
            leftPageRect.pivot = new Vector2(0f, 0.5f);
            leftPageRect.anchoredPosition = new Vector2(24f, 0f);
            leftPageRect.sizeDelta = new Vector2(960f, 790f);
        }
        else
        {
            leftPageRect.anchorMin = new Vector2(0.5f, 0.5f);
            leftPageRect.anchorMax = new Vector2(0.5f, 0.5f);
            leftPageRect.pivot = new Vector2(1f, 0.5f);
            leftPageRect.anchoredPosition = new Vector2(-22f, 0f);
            leftPageRect.sizeDelta = new Vector2(720f, 790f);
        }

        GameObject rightPage = UseCutoutCodexArt
            ? CreateUIObject("Runtime_CodexRightPage", book.transform)
            : CreatePanel("Runtime_CodexRightPage", book.transform, new Color(0.98f, 0.84f, 0.57f, 1f));
        Image rightPageImage = rightPage.GetComponent<Image>();
        if (rightPageImage != null) ApplyDemoSprite(rightPageImage, "codex_page_right", Color.white);
        RectTransform rightPageRect = rightPage.GetComponent<RectTransform>();
        if (UseCutoutCodexArt)
        {
            rightPageRect.anchorMin = new Vector2(1f, 0.5f);
            rightPageRect.anchorMax = new Vector2(1f, 0.5f);
            rightPageRect.pivot = new Vector2(1f, 0.5f);
            rightPageRect.anchoredPosition = new Vector2(-24f, 0f);
            rightPageRect.sizeDelta = new Vector2(540f, 790f);
        }
        else
        {
            rightPageRect.anchorMin = new Vector2(0.5f, 0.5f);
            rightPageRect.anchorMax = new Vector2(0.5f, 0.5f);
            rightPageRect.pivot = new Vector2(0f, 0.5f);
            rightPageRect.anchoredPosition = new Vector2(22f, 0f);
            rightPageRect.sizeDelta = new Vector2(720f, 790f);
        }

        TextMeshProUGUI sectionTitle = CreateText("Runtime_CodexTitle", leftPage.transform, "\u6536\u85cf\u76ee\u5f55", 30f, FontStyles.Bold, new Color(0.27f, 0.12f, 0.05f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(132f, -50f), new Vector2(260f, 54f));
        ConfigureTextFit(sectionTitle, 20f, 30f, 1);
        sectionTitle.gameObject.SetActive(false);
        if (!UseCutoutCodexArt) CreateCodexTabs(leftPage.transform);
        CreateRuntimeSidebar(leftPage.transform);
        CreateRuntimeFooter(leftPage.transform);
        CreateRuntimeDetailPage(rightPage.transform);
        CreateRuntimeCloseButton(book.transform);
        CreateRuntimeBookHeader(book.transform);

        if (unlockedViewRoot != null) unlockedViewRoot.SetActive(false);
    }

    private void EnsureVampireStyleCodexLayout()
    {
        Transform oldRuntime = transform.Find("Runtime_CodexBook");
        if (oldRuntime != null) Destroy(oldRuntime.gameObject);

        Transform oldBackdrop = transform.Find("Runtime_CodexBackdrop");
        if (oldBackdrop != null) Destroy(oldBackdrop.gameObject);

        codexTabs.Clear();

        GameObject backdrop = CreateUIObject("Runtime_CodexBackdrop", transform);
        StretchToParent(backdrop.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.035f, 0.025f, 0.02f, 0.70f);
        backdropImage.raycastTarget = true;

        GameObject panel = CreatePanel("Runtime_CodexBook", transform, new Color(0.29f, 0.28f, 0.39f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(880f, 820f);

        Outline outerLine = panel.AddComponent<Outline>();
        outerLine.effectColor = new Color(0.96f, 0.68f, 0.22f, 1f);
        outerLine.effectDistance = new Vector2(3f, -3f);

        Shadow panelShadow = panel.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        panelShadow.effectDistance = new Vector2(0f, -8f);

        GameObject header = CreatePanel("Runtime_CodexHeader", panel.transform, new Color(0.50f, 0.06f, 0.12f, 1f));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 68f);

        codexCollectionText = CreateText("Runtime_CollectionText", header.transform, "", 34f, FontStyles.Bold, new Color(0.94f, 0.91f, 0.86f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-110f, -8f));
        ConfigureTextFit(codexCollectionText, 24f, 34f, 1);

        CreateVampireStyleSidebar(panel.transform);
        CreateVampireStyleDetailBar(panel.transform);
        CreateRuntimeCloseButton(panel.transform);

        if (unlockedViewRoot != null) unlockedViewRoot.SetActive(false);
    }

    private void CreateVampireStyleSidebar(Transform parent)
    {
        GameObject scroll = CreateUIObject("Runtime_CodexScroll", parent);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(30f, 174f);
        scrollRect.offsetMax = new Vector2(-48f, -82f);

        ScrollRect scrollView = scroll.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollView.scrollSensitivity = 36f;

        GameObject viewport = CreatePanel("Viewport", scroll.transform, new Color(0.12f, 0.11f, 0.17f, 0.58f));
        StretchToParent(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollView.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 560f);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.cellSize = new Vector2(72f, 72f);
        grid.spacing = new Vector2(18f, 16f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.content = contentRect;
        sidebarContent = contentRect;

        CreateVampireStyleScrollbar(scroll.transform, scrollView);

        runtimeSidebarItemPrefab = CreateVampireStyleSidebarItemPrefab(parent);
        sidebarItemPrefab = runtimeSidebarItemPrefab;
    }

    private void CreateVampireStyleScrollbar(Transform parent, ScrollRect scrollView)
    {
        GameObject bar = CreatePanel("Runtime_CodexScrollbar", parent, new Color(0.18f, 0.16f, 0.24f, 0.95f));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = new Vector2(22f, 0f);
        barRect.sizeDelta = new Vector2(14f, 0f);

        GameObject slidingArea = CreateUIObject("Sliding Area", bar.transform);
        StretchToParent(slidingArea.GetComponent<RectTransform>(), 2f, 2f, 4f, 4f);

        GameObject handle = CreatePanel("Handle", slidingArea.transform, new Color(0.96f, 0.70f, 0.24f, 1f));
        Image handleImage = handle.GetComponent<Image>();
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        StretchToParent(handleRect, 0f, 0f, 0f, 0f);

        Scrollbar scrollbar = bar.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollView.verticalScrollbar = scrollbar;
        scrollView.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    private void CreateVampireStyleDetailBar(Transform parent)
    {
        GameObject detail = CreatePanel("Runtime_DetailRoot", parent, new Color(0.55f, 0.52f, 0.49f, 1f));
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0f);
        detailRect.pivot = new Vector2(0.5f, 0f);
        detailRect.anchoredPosition = new Vector2(0f, 20f);
        detailRect.sizeDelta = new Vector2(-36f, 136f);

        Outline outline = detail.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.67f, 0.20f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        weaponStatsViewRoot = detail;
        passiveDescViewRoot = detail;

        GameObject iconFrame = CreatePanel("Runtime_DetailIconFrame", detail.transform, new Color(0.04f, 0.035f, 0.035f, 1f));
        RectTransform iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0.5f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(70f, 0f);
        iconFrameRect.sizeDelta = new Vector2(78f, 78f);
        Outline iconOutline = iconFrame.AddComponent<Outline>();
        iconOutline.effectColor = new Color(0.96f, 0.78f, 0.22f, 1f);
        iconOutline.effectDistance = new Vector2(2f, -2f);

        GameObject iconObj = CreateUIObject("Runtime_DetailIcon", iconFrame.transform);
        StretchToParent(iconObj.GetComponent<RectTransform>(), 12f, 12f, 12f, 12f);
        weaponStatsIcon = iconObj.AddComponent<Image>();
        weaponStatsIcon.preserveAspect = true;
        passiveDescIcon = weaponStatsIcon;

        weaponStatsName = CreateText("Runtime_DetailTitle", detail.transform, "", 24f, FontStyles.Bold, new Color(1f, 0.83f, 0.28f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -34f), new Vector2(-144f, 34f));
        ConfigureTextFit(weaponStatsName, 18f, 24f, 1);
        passiveDescName = weaponStatsName;

        codexDetailBodyText = CreateText("Runtime_DetailBody", detail.transform, "", 21f, FontStyles.Bold, new Color(0.96f, 0.94f, 0.90f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -70f), new Vector2(-144f, 42f));
        ConfigureTextFit(codexDetailBodyText, 16f, 21f, 2);
        passiveDescText = codexDetailBodyText;

        codexTagsText = CreateText("Runtime_DetailFooter", detail.transform, "", 18f, FontStyles.Bold, new Color(1f, 0.58f, 0.12f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(96f, 28f), new Vector2(-144f, 28f));
        ConfigureTextFit(codexTagsText, 14f, 18f, 1);

        codexHeroSummaryText = null;
        weaponStatsContainer = null;
        weaponStatItemPrefab = null;
        runtimeStatItemPrefab = null;
        codexRecommendationContainer = null;
        codexRecommendedTitleText = null;
    }

    private void CreateRuntimeFooter(Transform parent)
    {
        GameObject footer = UseCutoutCodexArt
            ? CreateUIObject("Runtime_CodexFooter", parent)
            : CreatePanel("Runtime_CodexFooter", parent, new Color(0.30f, 0.18f, 0.10f, 0.96f));
        Image footerImage = footer.GetComponent<Image>();
        if (footerImage != null)
        {
            ApplyDemoSprite(footerImage, "codex_footer_bar", Color.white);
            if (footerImage.sprite == null) footerImage.color = new Color(0.30f, 0.18f, 0.10f, 0.96f);
            footerImage.raycastTarget = false;
        }

        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(0f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0.5f);
        footerRect.anchoredPosition = UseCutoutCodexArt ? new Vector2(486f, 59f) : new Vector2(360f, 54f);
        footerRect.sizeDelta = UseCutoutCodexArt ? new Vector2(820f, 58f) : new Vector2(600f, 58f);

        codexCollectionText = CreateText("Runtime_CollectionText", footer.transform, "", 22f, FontStyles.Bold, new Color(1f, 0.86f, 0.58f, 1f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, UseCutoutCodexArt ? new Vector2(-96f, -10f) : new Vector2(-64f, -10f));
        ConfigureTextFit(codexCollectionText, 16f, 22f, 1);
    }

    private void CreateRuntimeBookHeader(Transform parent)
    {
        Color plaqueColor = new Color(0.45f, 0.22f, 0.08f, 1f);
        GameObject plaque = CreatePanel("Runtime_CodexHeaderPlaque", parent, plaqueColor);
        Image plaqueImage = plaque.GetComponent<Image>();
        if (!ApplyDemoSprite(plaqueImage, "codex_header_plaque", Color.white))
        {
            ApplyDemoSprite(plaqueImage, "codex_tab", plaqueColor);
            Outline outline = plaque.AddComponent<Outline>();
            outline.effectColor = new Color(0.14f, 0.06f, 0.02f, 0.92f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        Shadow plaqueShadow = plaque.AddComponent<Shadow>();
        plaqueShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        plaqueShadow.effectDistance = new Vector2(0f, -5f);

        RectTransform plaqueRect = plaque.GetComponent<RectTransform>();
        plaqueRect.anchorMin = new Vector2(0.5f, 1f);
        plaqueRect.anchorMax = new Vector2(0.5f, 1f);
        plaqueRect.pivot = new Vector2(0.5f, 0.5f);
        plaqueRect.anchoredPosition = UseCutoutCodexArt ? new Vector2(-192f, -9f) : new Vector2(0f, -8f);
        plaqueRect.sizeDelta = new Vector2(380f, 78f);

        if (UseCutoutCodexArt)
        {
            return;
        }

        GameObject pumpkin = CreateUIObject("PumpkinBadge", plaque.transform);
        RectTransform pumpkinRect = pumpkin.GetComponent<RectTransform>();
        pumpkinRect.anchorMin = new Vector2(0f, 0.5f);
        pumpkinRect.anchorMax = new Vector2(0f, 0.5f);
        pumpkinRect.pivot = new Vector2(0.5f, 0.5f);
        pumpkinRect.anchoredPosition = new Vector2(47f, 2f);
        pumpkinRect.sizeDelta = new Vector2(72f, 72f);
        Image pumpkinImage = pumpkin.AddComponent<Image>();
        bool hasPumpkinBadge = ApplyDemoSprite(pumpkinImage, "codex_pumpkin_badge", Color.white, true);
        pumpkinImage.raycastTarget = false;
        pumpkin.SetActive(hasPumpkinBadge);

        TextMeshProUGUI title = CreateText("Title", plaque.transform, "\u5357\u74dc\u56fe\u9274", 34f, FontStyles.Bold, new Color(1f, 0.86f, 0.57f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), hasPumpkinBadge ? new Vector2(38f, 1f) : Vector2.zero, hasPumpkinBadge ? new Vector2(-96f, -12f) : new Vector2(-40f, -12f));
        ConfigureTextFit(title, 22f, 34f, 1);
    }

    private void CreateCodexTabs(Transform parent)
    {
        codexTabs.Clear();

        GameObject tabRow = CreateUIObject("Runtime_CodexTabs", parent);
        RectTransform tabRect = tabRow.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f);
        tabRect.anchorMax = new Vector2(1f, 1f);
        tabRect.pivot = new Vector2(0.5f, 1f);
        tabRect.anchoredPosition = UseCutoutCodexArt ? new Vector2(12f, -74f) : new Vector2(0f, -58f);
        tabRect.sizeDelta = UseCutoutCodexArt ? new Vector2(-84f, 64f) : new Vector2(-76f, 58f);
        HorizontalLayoutGroup layout = tabRow.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = UseCutoutCodexArt ? 8f : 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        CreateVisualTab(tabRow.transform, "\u6b66\u5668", "codex_tab_weapons", UseCutoutCodexArt ? 230f : 164f, new Color(0.76f, 0.24f, 0.05f, 1f), CodexCategory.Weapons);
        CreateVisualTab(tabRow.transform, "\u88ab\u52a8", "codex_tab_passives", UseCutoutCodexArt ? 220f : 156f, new Color(0.23f, 0.38f, 0.10f, 1f), CodexCategory.Passives);
        CreateVisualTab(tabRow.transform, "\u878d\u5408", "codex_tab_fusion", UseCutoutCodexArt ? 190f : 136f, new Color(0.39f, 0.24f, 0.52f, 0.94f), CodexCategory.Fusion);
        CreateVisualTab(tabRow.transform, "\u602a\u7269", "codex_tab_monsters", UseCutoutCodexArt ? 190f : 136f, new Color(0.47f, 0.30f, 0.18f, 0.94f), CodexCategory.Monsters);
        UpdateCodexTabVisuals();
    }

    private void CreateVisualTab(Transform parent, string text, string spriteName, float width, Color color, CodexCategory category)
    {
        GameObject tab = CreatePanel("Runtime_Tab", parent, color);
        bool hasReferenceSprite = HasCutoutUISprite(spriteName);
        Image tabImage = tab.GetComponent<Image>();
        ApplyDemoSprite(tabImage, hasReferenceSprite ? spriteName : "codex_tab", hasReferenceSprite ? Color.white : color);
        LayoutElement layout = tab.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = UseCutoutCodexArt ? 64f : 58f;
        Button button = tab.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = tabImage;
        CodexCategory selectedCategory = category;
        button.onClick.AddListener(() => SelectCodexCategory(selectedCategory));

        TextMeshProUGUI label = CreateText("Label", tab.transform, text, UseCutoutCodexArt ? 22f : 20f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, -2f));
        label.gameObject.SetActive(!hasReferenceSprite);
        ConfigureTextFit(label, 15f, UseCutoutCodexArt ? 22f : 20f, 1);

        codexTabs.Add(new RuntimeCodexTab
        {
            category = category,
            background = tabImage,
            label = label,
            activeColor = color,
            inactiveColor = Color.Lerp(color, new Color(0.08f, 0.05f, 0.03f, 1f), 0.56f)
        });
    }

    private void CreateRuntimeSidebar(Transform parent)
    {
        GameObject scroll = CreateUIObject("Runtime_CodexScroll", parent);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = UseCutoutCodexArt ? new Vector2(72f, 176f) : new Vector2(46f, 118f);
        scrollRect.offsetMax = UseCutoutCodexArt ? new Vector2(-58f, -124f) : new Vector2(-46f, -132f);

        ScrollRect scrollView = scroll.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollView.scrollSensitivity = 28f;

        GameObject viewport = CreatePanel("Viewport", scroll.transform, new Color(0.39f, 0.20f, 0.10f, 0.18f));
        StretchToParent(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollView.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, UseCutoutCodexArt ? 720f : 680f);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.padding = UseCutoutCodexArt ? new RectOffset(10, 10, 8, 20) : new RectOffset(18, 18, 8, 20);
        grid.cellSize = UseCutoutCodexArt ? new Vector2(126f, 146f) : new Vector2(116f, 140f);
        grid.spacing = UseCutoutCodexArt ? new Vector2(34f, 22f) : new Vector2(28f, 20f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.content = contentRect;
        sidebarContent = contentRect;

        runtimeSidebarItemPrefab = CreateRuntimeSidebarItemPrefab(parent);
        sidebarItemPrefab = runtimeSidebarItemPrefab;
    }

    private void CreateRuntimeDetailPage(Transform parent)
    {
        GameObject detailRoot = CreateUIObject("Runtime_DetailRoot", parent);
        StretchToParent(detailRoot.GetComponent<RectTransform>(), 34f, 40f, 34f, 42f);
        weaponStatsViewRoot = detailRoot;
        passiveDescViewRoot = detailRoot;

        GameObject hero = CreatePanel("Runtime_HeroFrame", detailRoot.transform, new Color(0.22f, 0.13f, 0.09f, 0.92f));
        ApplyDemoSprite(hero.GetComponent<Image>(), "codex_detail_panel", Color.white);
        RectTransform heroRect = hero.GetComponent<RectTransform>();
        heroRect.anchorMin = new Vector2(0f, 1f);
        heroRect.anchorMax = new Vector2(1f, 1f);
        heroRect.pivot = new Vector2(0.5f, 1f);
        heroRect.anchoredPosition = Vector2.zero;
        heroRect.sizeDelta = new Vector2(0f, 172f);

        GameObject iconFrame = CreatePanel("Runtime_DetailIconFrame", hero.transform, new Color(0.91f, 0.43f, 0.08f, 0.95f));
        ApplyDemoSprite(iconFrame.GetComponent<Image>(), "codex_icon_frame", Color.white);
        RectTransform iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(24f, 0f);
        iconFrameRect.sizeDelta = new Vector2(124f, 124f);

        GameObject icon = CreateUIObject("Runtime_DetailIcon", iconFrame.transform);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        StretchToParent(iconRect, 16f, 16f, 16f, 16f);
        weaponStatsIcon = icon.AddComponent<Image>();
        weaponStatsIcon.preserveAspect = true;
        passiveDescIcon = weaponStatsIcon;

        weaponStatsName = CreateText("Runtime_DetailTitle", hero.transform, "", 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(100f, -46f), new Vector2(-216f, 56f));
        ConfigureTextFit(weaponStatsName, 22f, 34f, 1);
        passiveDescName = weaponStatsName;

        codexHeroSummaryText = CreateText("Runtime_DetailSummary", hero.transform, "", 21f, FontStyles.Bold, new Color(1f, 0.88f, 0.66f, 1f), TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(100f, -108f), new Vector2(-216f, 72f));
        ConfigureTextFit(codexHeroSummaryText, 16f, 21f, 3);

        codexTagsText = CreateText("Runtime_DetailTags", detailRoot.transform, "", 20f, FontStyles.Bold, new Color(0.45f, 0.22f, 0.09f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -202f), new Vector2(-20f, 36f));
        ConfigureTextFit(codexTagsText, 15f, 20f, 1);

        codexDetailBodyText = CreateText("Runtime_DetailBody", detailRoot.transform, "", 21f, FontStyles.Normal, new Color(0.26f, 0.13f, 0.06f, 1f), TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -316f), new Vector2(-20f, 200f));
        ConfigureTextFit(codexDetailBodyText, 16f, 21f, 7);
        passiveDescText = codexDetailBodyText;

        GameObject stats = CreateUIObject("Runtime_StatsGrid", detailRoot.transform);
        RectTransform statsRect = stats.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 0f);
        statsRect.pivot = new Vector2(0.5f, 0f);
        statsRect.anchoredPosition = new Vector2(0f, 126f);
        statsRect.sizeDelta = new Vector2(-20f, 116f);
        GridLayoutGroup statsGrid = stats.AddComponent<GridLayoutGroup>();
        statsGrid.cellSize = UseCutoutCodexArt ? new Vector2(214f, 50f) : new Vector2(292f, 56f);
        statsGrid.spacing = UseCutoutCodexArt ? new Vector2(14f, 12f) : new Vector2(16f, 10f);
        statsGrid.childAlignment = TextAnchor.MiddleCenter;
        statsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        statsGrid.constraintCount = 2;
        weaponStatsContainer = stats.transform;

        codexRecommendationContainer = CreateUIObject("Runtime_Recommendations", detailRoot.transform).transform;
        RectTransform recRect = codexRecommendationContainer.GetComponent<RectTransform>();
        recRect.anchorMin = new Vector2(0f, 0f);
        recRect.anchorMax = new Vector2(1f, 0f);
        recRect.pivot = new Vector2(0.5f, 0f);
        recRect.anchoredPosition = new Vector2(0f, 36f);
        recRect.sizeDelta = new Vector2(-20f, 60f);
        HorizontalLayoutGroup recLayout = codexRecommendationContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        recLayout.spacing = 10f;
        recLayout.childAlignment = TextAnchor.MiddleLeft;
        recLayout.childControlWidth = false;
        recLayout.childControlHeight = false;

        codexRecommendedTitleText = CreateText("Runtime_RecommendationTitle", detailRoot.transform, "\u63a8\u8350", 18f, FontStyles.Bold, new Color(0.43f, 0.20f, 0.08f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 108f), new Vector2(-20f, 26f));
        ConfigureTextFit(codexRecommendedTitleText, 14f, 18f, 1);

        runtimeStatItemPrefab = CreateRuntimeStatItemPrefab(parent);
        weaponStatItemPrefab = runtimeStatItemPrefab;

        CreateRuntimeLockedView(parent);
    }

    private void CreateRuntimeLockedView(Transform parent)
    {
        GameObject lockedRoot = CreateUIObject("Runtime_LockedView", parent);
        StretchToParent(lockedRoot.GetComponent<RectTransform>(), 40f, 40f, 38f, 42f);
        lockedViewRoot = lockedRoot;
        lockedRoot.SetActive(false);

        GameObject lockIconObj = CreatePanel("Runtime_LockedIconFrame", lockedRoot.transform, new Color(0.18f, 0.13f, 0.10f, 0.92f));
        ApplyDemoSprite(lockIconObj.GetComponent<Image>(), "codex_icon_frame", Color.white);
        RectTransform iconRect = lockIconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -84f);
        iconRect.sizeDelta = new Vector2(180f, 180f);

        GameObject icon = CreateUIObject("Runtime_LockedIcon", lockIconObj.transform);
        StretchToParent(icon.GetComponent<RectTransform>(), 28f, 28f, 28f, 28f);
        lockedWeaponIcon = icon.AddComponent<Image>();
        lockedWeaponIcon.preserveAspect = true;
        lockedWeaponIcon.color = new Color(0f, 0f, 0f, 0.86f);

        lockConditionText = CreateText("Runtime_LockCondition", lockedRoot.transform, "", 30f, FontStyles.Bold, new Color(0.27f, 0.12f, 0.05f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 18f), new Vector2(-80f, 150f));

        lockConditionIconContainer = CreateUIObject("Runtime_LockConditionIcons", lockedRoot.transform).transform;
        RectTransform conditionRect = lockConditionIconContainer.GetComponent<RectTransform>();
        conditionRect.anchorMin = new Vector2(0f, 0.5f);
        conditionRect.anchorMax = new Vector2(1f, 0.5f);
        conditionRect.pivot = new Vector2(0.5f, 0.5f);
        conditionRect.anchoredPosition = new Vector2(0f, 18f);
        conditionRect.sizeDelta = new Vector2(-80f, 150f);
        HorizontalLayoutGroup conditionLayout = lockConditionIconContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        conditionLayout.spacing = 18f;
        conditionLayout.childAlignment = TextAnchor.MiddleCenter;
        conditionLayout.childControlWidth = false;
        conditionLayout.childControlHeight = false;
        conditionLayout.childForceExpandWidth = false;
        conditionLayout.childForceExpandHeight = false;
        lockConditionIconContainer.gameObject.SetActive(false);

        lockProgressText = CreateText("Runtime_LockProgress", lockedRoot.transform, "", 32f, FontStyles.Bold, new Color(0.88f, 0.36f, 0.08f, 1f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(300f, 58f));
    }

    private void CreateRuntimeCloseButton(Transform parent)
    {
        GameObject closeObj = CreatePanel("Runtime_CloseButton", parent, new Color(0.78f, 0.16f, 0.12f, 1f));
        ApplyDemoSprite(closeObj.GetComponent<Image>(), "codex_close_button", Color.white);
        RectTransform rect = closeObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-34f, -34f);
        rect.sizeDelta = new Vector2(64f, 64f);
        Button button = closeObj.AddComponent<Button>();
        button.onClick.AddListener(ClosePanel);
        closeButton = button;
    }

    private GameObject CreateVampireStyleSidebarItemPrefab(Transform parent)
    {
        GameObject item = CreatePanel("Runtime_SidebarItemPrefab", parent, new Color(1f, 1f, 1f, 0f));
        item.SetActive(false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(72f, 72f);
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.preferredWidth = 72f;
        layout.preferredHeight = 72f;
        item.AddComponent<Button>();

        GameObject cardBg = CreatePanel("CardBackground", item.transform, new Color(0.04f, 0.035f, 0.035f, 1f));
        Image cardImage = cardBg.GetComponent<Image>();
        cardImage.raycastTarget = false;
        StretchToParent(cardBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Outline cardOutline = cardBg.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.96f, 0.77f, 0.22f, 1f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        GameObject highlight = CreatePanel("Highlight", item.transform, new Color(1f, 0.78f, 0.18f, 0.34f));
        Image highlightImage = highlight.GetComponent<Image>();
        highlightImage.raycastTarget = false;
        StretchToParent(highlight.GetComponent<RectTransform>(), -4f, -4f, -4f, -4f);
        highlight.SetActive(false);

        GameObject iconObj = CreateUIObject("Icon", item.transform);
        StretchToParent(iconObj.GetComponent<RectTransform>(), 11f, 11f, 11f, 11f);
        Image icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject lockOverlayObj = CreatePanel("LockOverlay", item.transform, new Color(0f, 0f, 0f, 0.26f));
        Image overlayImage = lockOverlayObj.GetComponent<Image>();
        overlayImage.raycastTarget = false;
        StretchToParent(lockOverlayObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        lockOverlayObj.SetActive(false);

        SkillTreeSidebarItem sidebarItem = item.AddComponent<SkillTreeSidebarItem>();
        sidebarItem.iconImage = icon;
        sidebarItem.backgroundImage = cardImage;
        sidebarItem.highlightImage = highlightImage;
        sidebarItem.selectionHighlight = highlight;
        sidebarItem.lockOverlay = lockOverlayObj;
        sidebarItem.nameText = null;
        sidebarItem.statusText = null;
        sidebarItem.typeBadgeText = null;
        sidebarItem.weaponBgSprite = null;
        sidebarItem.passiveBgSprite = null;
        sidebarItem.fusionBgSprite = null;
        sidebarItem.lockedBgSprite = null;
        sidebarItem.weaponHighlightSprite = null;
        sidebarItem.passiveHighlightSprite = null;
        sidebarItem.fusionHighlightSprite = null;
        return item;
    }

    private GameObject CreateRuntimeSidebarItemPrefab(Transform parent)
    {
        GameObject item = CreatePanel("Runtime_SidebarItemPrefab", parent, new Color(1f, 1f, 1f, 0f));
        item.SetActive(false);
        Vector2 itemSize = UseCutoutCodexArt ? new Vector2(126f, 146f) : new Vector2(116f, 140f);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = itemSize;
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.preferredWidth = itemSize.x;
        layout.preferredHeight = itemSize.y;
        item.AddComponent<Button>();

        GameObject highlight = CreatePanel("Highlight", item.transform, Color.white);
        ApplyDemoSprite(highlight.GetComponent<Image>(), "codex_card_selected", Color.white);
        highlight.GetComponent<Image>().raycastTarget = false;
        StretchToParent(highlight.GetComponent<RectTransform>(), -6f, -6f, -6f, -6f);
        highlight.SetActive(false);

        GameObject cardBg = CreatePanel("CardBackground", item.transform, new Color(0.48f, 0.22f, 0.08f, 1f));
        ApplyDemoSprite(cardBg.GetComponent<Image>(), "codex_card_weapon", Color.white);
        cardBg.GetComponent<Image>().raycastTarget = false;
        StretchToParent(cardBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject iconObj = CreateUIObject("Icon", item.transform);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = UseCutoutCodexArt ? new Vector2(0f, -18f) : new Vector2(0f, -16f);
        iconRect.sizeDelta = UseCutoutCodexArt ? new Vector2(70f, 70f) : new Vector2(62f, 62f);
        Image icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;

        TextMeshProUGUI name = CreateText("Name", item.transform, "", 15f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), UseCutoutCodexArt ? new Vector2(0f, 46f) : new Vector2(0f, 42f), new Vector2(-12f, 34f));
        ConfigureTextFit(name, 10f, 15f, 2);
        TextMeshProUGUI status = CreateText("Status", item.transform, "", 13f, FontStyles.Bold, new Color(1f, 0.72f, 0.22f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), UseCutoutCodexArt ? new Vector2(0f, 15f) : new Vector2(0f, 13f), new Vector2(-12f, 26f));
        ConfigureTextFit(status, 9f, 13f, 1);
        TextMeshProUGUI badge = CreateText("Badge", item.transform, "", 12f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(13f, -15f), new Vector2(24f, 20f));
        ConfigureTextFit(badge, 9f, 12f, 1);

        GameObject lockOverlayObj = CreatePanel("LockOverlay", item.transform, new Color(0f, 0f, 0f, 0.22f));
        lockOverlayObj.GetComponent<Image>().raycastTarget = false;
        StretchToParent(lockOverlayObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        lockOverlayObj.SetActive(false);

        SkillTreeSidebarItem sidebarItem = item.AddComponent<SkillTreeSidebarItem>();
        sidebarItem.iconImage = icon;
        sidebarItem.backgroundImage = cardBg.GetComponent<Image>();
        sidebarItem.highlightImage = highlight.GetComponent<Image>();
        sidebarItem.selectionHighlight = highlight;
        sidebarItem.lockOverlay = lockOverlayObj;
        sidebarItem.nameText = name;
        sidebarItem.statusText = status;
        sidebarItem.typeBadgeText = badge;
        sidebarItem.weaponBgSprite = LoadDemoUISprite("codex_card_weapon");
        sidebarItem.passiveBgSprite = LoadDemoUISprite("codex_card_passive");
        sidebarItem.fusionBgSprite = LoadDemoUISprite("codex_card_passive");
        sidebarItem.lockedBgSprite = LoadDemoUISprite("codex_card_locked");
        sidebarItem.weaponHighlightSprite = LoadDemoUISprite("codex_card_selected");
        sidebarItem.passiveHighlightSprite = LoadDemoUISprite("codex_card_selected");
        sidebarItem.fusionHighlightSprite = LoadDemoUISprite("codex_card_selected");
        return item;
    }

    private GameObject CreateRuntimeStatItemPrefab(Transform parent)
    {
        GameObject item = CreatePanel("Runtime_StatItemPrefab", parent, new Color(0.30f, 0.16f, 0.08f, 0.92f));
        ApplyDemoSprite(item.GetComponent<Image>(), "codex_stat_slot", Color.white);
        item.SetActive(false);
        Vector2 statSize = UseCutoutCodexArt ? new Vector2(214f, 50f) : new Vector2(292f, 56f);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = statSize;

        GameObject iconObj = CreateUIObject("Icon", item.transform);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = UseCutoutCodexArt ? new Vector2(28f, 0f) : new Vector2(34f, 0f);
        iconRect.sizeDelta = UseCutoutCodexArt ? new Vector2(28f, 28f) : new Vector2(30f, 30f);
        Image statIcon = iconObj.AddComponent<Image>();
        statIcon.preserveAspect = true;
        statIcon.raycastTarget = false;

        TextMeshProUGUI label = CreateText("Label", item.transform, "", 16f, FontStyles.Bold, new Color(0.95f, 0.72f, 0.32f, 1f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(0f, 1f), UseCutoutCodexArt ? new Vector2(94f, 0f) : new Vector2(112f, 0f), UseCutoutCodexArt ? new Vector2(88f, -4f) : new Vector2(132f, -4f));
        TextMeshProUGUI value = CreateText("Value", item.transform, "", 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineRight,
            new Vector2(1f, 0f), new Vector2(1f, 1f), UseCutoutCodexArt ? new Vector2(-48f, 0f) : new Vector2(-58f, 0f), UseCutoutCodexArt ? new Vector2(80f, -4f) : new Vector2(90f, -4f));
        ConfigureTextFit(label, 12f, 16f, 1);
        ConfigureTextFit(value, 16f, 22f, 1);

        CodexStatSlot slot = item.AddComponent<CodexStatSlot>();
        slot.statIcon = statIcon;
        slot.labelText = label;
        slot.valueText = value;
        return item;
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(objectName, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return go;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string text,
        float size,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        ApplyUIFont(label);
        return label;
    }

    private void ApplyUIFont(TextMeshProUGUI label)
    {
        if (label == null) return;

        TMP_FontAsset font = ResolveChineseUIFontByGuid();
        if (font == null) return;

        label.font = font;
        if (font.material != null) label.fontSharedMaterial = font.material;
    }

    private TMP_FontAsset ResolveChineseUIFontByGuid()
    {
        if (chineseUIFont != null) return chineseUIFont;

#if UNITY_EDITOR
        string fontAssetPath = AssetDatabase.GUIDToAssetPath(PopUIFontAssetGuid);
        chineseUIFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
#endif
        return chineseUIFont;
    }

    private TMP_FontAsset ResolveChineseUIFont()
    {
        if (chineseUIFont != null) return chineseUIFont;

        chineseUIFont = Resources.Load<TMP_FontAsset>("UI/Fonts/けいなんポップ体");
#if UNITY_EDITOR
        if (chineseUIFont == null)
        {
            chineseUIFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_TheFirst/Art/Fonts/けいなんポップ体.asset");
        }
#endif
        return chineseUIFont;
    }

    private void ConfigureTextFit(TextMeshProUGUI label, float minSize, float maxSize, int maxLines = 0)
    {
        if (label == null) return;

        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (maxLines > 0) label.maxVisibleLines = maxLines;
    }

    private void StretchToParent(RectTransform rect, float left, float right, float top, float bottom)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public bool IsPanelOpen()
    {
        return gameObject.activeSelf;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        EnsureCodexData();
        EnsurePolishedCodexLayout();
        GenerateSidebar();
        SelectDefaultEntryForCurrentCategory();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    private void SelectCodexCategory(CodexCategory category)
    {
        currentCodexCategory = category;
        GenerateSidebar();
        SelectDefaultEntryForCurrentCategory();
    }

    private void SelectDefaultEntryForCurrentCategory()
    {
        if (UseVampireStyleCodexLayout)
        {
            SelectDefaultVampireStyleEntry();
            return;
        }

        switch (currentCodexCategory)
        {
            case CodexCategory.Weapons:
                if (currentSelectedTree != null && allWeaponTrees != null && allWeaponTrees.Contains(currentSelectedTree))
                {
                    SelectWeaponEntry(currentSelectedTree);
                    return;
                }

                if (allWeaponTrees != null)
                {
                    foreach (var tree in allWeaponTrees)
                    {
                        if (tree == null) continue;
                        SelectWeaponEntry(tree);
                        return;
                    }
                }

                ShowCategoryPlaceholder("\u6682\u65e0\u6b66\u5668\u6761\u76ee", "\u6b66\u5668\u56fe\u9274\u6570\u636e\u672a\u63a5\u5165", "\u8bf7\u5728 Weapon Skill Tree \u8d44\u4ea7\u4e2d\u914d\u7f6e\u56fe\u9274\u6761\u76ee\u3002");
                break;
            case CodexCategory.Passives:
                if (currentSelectedPassive != null && allPassiveItems != null && allPassiveItems.Contains(currentSelectedPassive))
                {
                    SelectPassiveEntry(currentSelectedPassive);
                    return;
                }

                if (allPassiveItems != null)
                {
                    foreach (var passive in allPassiveItems)
                    {
                        if (passive == null) continue;
                        SelectPassiveEntry(passive);
                        return;
                    }
                }

                ShowCategoryPlaceholder("\u6682\u65e0\u88ab\u52a8\u6761\u76ee", "\u88ab\u52a8\u56fe\u9274\u6570\u636e\u672a\u63a5\u5165", "\u8bf7\u5728 Passive Item Data \u8d44\u4ea7\u4e2d\u914d\u7f6e\u56fe\u9274\u6761\u76ee\u3002");
                break;
            case CodexCategory.Fusion:
                if (currentSelectedFusionRecipe != null && allFusionRecipes != null && allFusionRecipes.Contains(currentSelectedFusionRecipe))
                {
                    SelectFusionEntry(currentSelectedFusionRecipe);
                    return;
                }

                if (currentSelectedWeaponFusionRecipe != null && allWeaponFusionRecipes != null && allWeaponFusionRecipes.Contains(currentSelectedWeaponFusionRecipe))
                {
                    SelectFusionEntry(currentSelectedWeaponFusionRecipe);
                    return;
                }

                if (currentSelectedEvolutionRecipe != null && allEvolutionRecipes != null && allEvolutionRecipes.Contains(currentSelectedEvolutionRecipe))
                {
                    SelectFusionEntry(currentSelectedEvolutionRecipe);
                    return;
                }

                if (TrySelectFirstFusionEntry()) return;

                ShowCategoryPlaceholder("\u878d\u5408\u914d\u65b9", "\u878d\u5408\u6570\u636e\u672a\u63a5\u5165", "\u5f53\u524d\u6ca1\u6709\u53ef\u8bfb\u53d6\u7684\u878d\u5408\u914d\u65b9\u8d44\u4ea7\u3002");
                break;
            case CodexCategory.Monsters:
                ShowCategoryPlaceholder("\u602a\u7269\u56fe\u9274", "\u602a\u7269\u6570\u636e\u672a\u63a5\u5165", "\u602a\u7269\u56fe\u9274\u9700\u8981\u72ec\u7acb\u7684\u602a\u7269\u6570\u636e\u8d44\u4ea7\uff0c\u4e0d\u4ece\u6218\u6597\u5237\u602a\u903b\u8f91\u4e2d\u76f4\u63a5\u62c6\u6570\u636e\u3002");
                break;
        }
    }

    private void SelectDefaultVampireStyleEntry()
    {
        if (currentSelectedTree != null && allWeaponTrees != null && allWeaponTrees.Contains(currentSelectedTree))
        {
            SelectWeaponEntry(currentSelectedTree);
            return;
        }

        if (currentSelectedPassive != null && allPassiveItems != null && allPassiveItems.Contains(currentSelectedPassive))
        {
            SelectPassiveEntry(currentSelectedPassive);
            return;
        }

        if (currentSelectedFusionRecipe != null && allFusionRecipes != null && allFusionRecipes.Contains(currentSelectedFusionRecipe))
        {
            SelectFusionEntry(currentSelectedFusionRecipe);
            return;
        }

        if (currentSelectedWeaponFusionRecipe != null && allWeaponFusionRecipes != null && allWeaponFusionRecipes.Contains(currentSelectedWeaponFusionRecipe))
        {
            SelectFusionEntry(currentSelectedWeaponFusionRecipe);
            return;
        }

        if (currentSelectedEvolutionRecipe != null && allEvolutionRecipes != null && allEvolutionRecipes.Contains(currentSelectedEvolutionRecipe))
        {
            SelectFusionEntry(currentSelectedEvolutionRecipe);
            return;
        }

        if (allWeaponTrees != null)
        {
            foreach (var tree in allWeaponTrees)
            {
                if (tree == null) continue;
                SelectWeaponEntry(tree);
                return;
            }
        }

        if (allPassiveItems != null)
        {
            foreach (var passive in allPassiveItems)
            {
                if (passive == null) continue;
                SelectPassiveEntry(passive);
                return;
            }
        }

        if (TrySelectFirstFusionEntry()) return;

        ShowCategoryPlaceholder("暂无条目", "图鉴数据未接入", "");
    }

    private bool TrySelectFirstFusionEntry()
    {
        HashSet<string> shownResults = new HashSet<string>();

        if (allWeaponFusionRecipes != null)
        {
            foreach (var recipe in allWeaponFusionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownResults, recipe.resultWeapon)) continue;
                SelectFusionEntry(recipe);
                return true;
            }
        }

        if (allFusionRecipes != null)
        {
            foreach (var recipe in allFusionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownResults, recipe.resultWeapon)) continue;
                SelectFusionEntry(recipe);
                return true;
            }
        }

        if (allEvolutionRecipes != null)
        {
            foreach (var recipe in allEvolutionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownResults, recipe.ResultWeapon)) continue;
                SelectFusionEntry(recipe);
                return true;
            }
        }

        return false;
    }

    private void UpdateCodexTabVisuals()
    {
        foreach (RuntimeCodexTab tab in codexTabs)
        {
            if (tab == null) continue;

            bool isActive = tab.category == currentCodexCategory;
            if (tab.background != null)
            {
                tab.background.color = isActive ? tab.activeColor : tab.inactiveColor;
            }

            if (tab.label != null)
            {
                tab.label.color = isActive
                    ? Color.white
                    : new Color(0.74f, 0.66f, 0.52f, 1f);
                tab.label.fontStyle = FontStyles.Bold;
            }
        }
    }

    private void GenerateSidebar()
    {
        if (sidebarContent == null || sidebarItemPrefab == null) return;

        if (UseVampireStyleCodexLayout)
        {
            GenerateVampireStyleSidebar();
            return;
        }

        foreach (Transform child in sidebarContent)
        {
            Destroy(child.gameObject);
        }
        sidebarItems.Clear();

        switch (currentCodexCategory)
        {
            case CodexCategory.Weapons:
                if (allWeaponTrees != null)
                {
                    foreach (var tree in allWeaponTrees)
                    {
                        if (tree != null) CreateSidebarItemWeapon(tree);
                    }
                }
                break;
            case CodexCategory.Passives:
                if (allPassiveItems != null)
                {
                    foreach (var passive in allPassiveItems)
                    {
                        if (passive != null) CreateSidebarItemPassive(passive);
                    }
                }
                break;
            case CodexCategory.Fusion:
                CreateFusionSidebarItems();
                if (sidebarContent.childCount == 0) CreateSidebarPlaceholderCard("\u878d\u5408\u914d\u65b9", "\u5f85\u63a5\u5165");
                break;
            case CodexCategory.Monsters:
                CreateSidebarPlaceholderCard("\u602a\u7269\u56fe\u9274", "\u5f85\u63a5\u5165");
                break;
        }

        UpdateCodexCollectionText();
        UpdateCodexTabVisuals();
    }

    private void GenerateVampireStyleSidebar()
    {
        foreach (Transform child in sidebarContent)
        {
            Destroy(child.gameObject);
        }
        sidebarItems.Clear();

        if (allWeaponTrees != null)
        {
            foreach (var tree in allWeaponTrees)
            {
                if (tree != null) CreateSidebarItemWeapon(tree);
            }
        }

        if (allPassiveItems != null)
        {
            foreach (var passive in allPassiveItems)
            {
                if (passive != null) CreateSidebarItemPassive(passive);
            }
        }

        CreateFusionSidebarItems();

        if (sidebarContent.childCount == 0) CreateSidebarPlaceholderCard("?", "待接入");
        UpdateCodexCollectionText();
    }

    private void CreateFusionSidebarItems()
    {
        HashSet<string> shownFusionResults = new HashSet<string>();

        if (allWeaponFusionRecipes != null)
        {
            foreach (var recipe in allWeaponFusionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownFusionResults, recipe.resultWeapon)) continue;
                CreateSidebarItemFusion(recipe);
            }
        }

        if (allFusionRecipes != null)
        {
            foreach (var recipe in allFusionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownFusionResults, recipe.resultWeapon)) continue;
                CreateSidebarItemFusion(recipe);
            }
        }

        if (allEvolutionRecipes != null)
        {
            foreach (var recipe in allEvolutionRecipes)
            {
                if (recipe == null || ShouldSkipDuplicateFusionResult(shownFusionResults, recipe.ResultWeapon)) continue;
                CreateSidebarItemFusion(recipe);
            }
        }
    }

    private bool ShouldSkipDuplicateFusionResult(HashSet<string> shownResults, WeaponStatBlock resultWeapon)
    {
        if (shownResults == null || resultWeapon == null) return false;

        string key = GetFusionResultKey(resultWeapon);
        if (string.IsNullOrEmpty(key)) return false;
        if (shownResults.Contains(key)) return true;

        shownResults.Add(key);
        return false;
    }

    private string GetFusionResultKey(WeaponStatBlock weapon)
    {
        if (weapon == null) return string.Empty;
        if (!string.IsNullOrEmpty(weapon.weaponID)) return "id:" + weapon.weaponID.ToLowerInvariant();
        if (!string.IsNullOrEmpty(weapon.weaponName)) return "name:" + weapon.weaponName.ToLowerInvariant();
        return "asset:" + weapon.GetInstanceID();
    }

    private void CreateSidebarItemWeapon(WeaponSkillTree tree)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        itemObj.SetActive(true);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();
        if (script == null) return;

        bool isUnlocked = CheckWeaponUnlocked(tree);
        bool isSelected = currentEntryType == SelectedEntryType.Weapon && currentSelectedTree == tree;
        script.Setup(tree, this, isUnlocked, isSelected);
        sidebarItems.Add(script);
    }

    private void CreateSidebarItemPassive(PassiveItemData passive)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        itemObj.SetActive(true);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();
        if (script == null) return;

        bool isUnlocked = CheckPassiveUnlocked(passive);
        bool isSelected = currentEntryType == SelectedEntryType.Passive && currentSelectedPassive == passive;
        script.Setup(passive, this, isUnlocked, isSelected);
        sidebarItems.Add(script);
    }

    private void CreateSidebarItemFusion(FusionRecipeSO recipe)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        itemObj.SetActive(true);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();
        if (script == null) return;

        bool isSelected = currentEntryType == SelectedEntryType.Fusion && currentSelectedFusionRecipe == recipe;
        script.Setup(recipe, this, CheckFusionRevealed(recipe), isSelected);
        sidebarItems.Add(script);
    }

    private void CreateSidebarItemFusion(WeaponFusionRecipeSO recipe)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        itemObj.SetActive(true);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();
        if (script == null) return;

        bool isSelected = currentEntryType == SelectedEntryType.Fusion && currentSelectedWeaponFusionRecipe == recipe;
        script.Setup(recipe, this, CheckFusionRevealed(recipe), isSelected);
        sidebarItems.Add(script);
    }

    private void CreateSidebarItemFusion(EvolutionRecipeSO recipe)
    {
        GameObject itemObj = Instantiate(sidebarItemPrefab, sidebarContent);
        itemObj.SetActive(true);
        SkillTreeSidebarItem script = itemObj.GetComponent<SkillTreeSidebarItem>();
        if (script == null) return;

        bool isSelected = currentEntryType == SelectedEntryType.Fusion && currentSelectedEvolutionRecipe == recipe;
        script.Setup(recipe, this, CheckFusionRevealed(recipe), isSelected);
        sidebarItems.Add(script);
    }

    private void CreateSidebarPlaceholderCard(string title, string status)
    {
        GameObject item = CreatePanel("Runtime_PlaceholderCard", sidebarContent, new Color(1f, 1f, 1f, 0f));
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.sizeDelta = UseCutoutCodexArt ? new Vector2(126f, 146f) : new Vector2(116f, 140f);
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.preferredWidth = itemRect.sizeDelta.x;
        layout.preferredHeight = itemRect.sizeDelta.y;

        GameObject cardBg = CreatePanel("CardBackground", item.transform, new Color(0.16f, 0.12f, 0.09f, 0.92f));
        ApplyDemoSprite(cardBg.GetComponent<Image>(), "codex_card_locked", Color.white);
        cardBg.GetComponent<Image>().raycastTarget = false;
        StretchToParent(cardBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        TextMeshProUGUI titleText = CreateText("Title", item.transform, title, 15f, FontStyles.Bold, new Color(1f, 0.86f, 0.58f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 18f), new Vector2(-12f, 48f));
        ConfigureTextFit(titleText, 10f, 15f, 2);

        TextMeshProUGUI statusText = CreateText("Status", item.transform, status, 13f, FontStyles.Bold, new Color(0.86f, 0.52f, 0.18f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 26f), new Vector2(-12f, 26f));
        ConfigureTextFit(statusText, 9f, 13f, 1);
    }

    private void RefreshSidebarVisuals()
    {
        foreach (var item in sidebarItems)
        {
            if (item == null) continue;

            if (item.EntryType == SkillTreeSidebarItem.CodexEntryType.Weapon)
            {
                bool isSelected = currentEntryType == SelectedEntryType.Weapon && item.MyTreeData == currentSelectedTree;
                item.Setup(item.MyTreeData, this, CheckWeaponUnlocked(item.MyTreeData), isSelected);
            }
            else
            {
                if (item.EntryType == SkillTreeSidebarItem.CodexEntryType.Passive)
                {
                    bool isSelected = currentEntryType == SelectedEntryType.Passive && item.MyPassiveData == currentSelectedPassive;
                    item.Setup(item.MyPassiveData, this, CheckPassiveUnlocked(item.MyPassiveData), isSelected);
                }
                else if (item.MyFusionRecipe != null)
                {
                    bool isSelected = currentEntryType == SelectedEntryType.Fusion && item.MyFusionRecipe == currentSelectedFusionRecipe;
                    item.Setup(item.MyFusionRecipe, this, CheckFusionRevealed(item.MyFusionRecipe), isSelected);
                }
                else if (item.MyWeaponFusionRecipe != null)
                {
                    bool isSelected = currentEntryType == SelectedEntryType.Fusion && item.MyWeaponFusionRecipe == currentSelectedWeaponFusionRecipe;
                    item.Setup(item.MyWeaponFusionRecipe, this, CheckFusionRevealed(item.MyWeaponFusionRecipe), isSelected);
                }
                else if (item.MyEvolutionRecipe != null)
                {
                    bool isSelected = currentEntryType == SelectedEntryType.Fusion && item.MyEvolutionRecipe == currentSelectedEvolutionRecipe;
                    item.Setup(item.MyEvolutionRecipe, this, CheckFusionRevealed(item.MyEvolutionRecipe), isSelected);
                }
            }
        }
    }

    public void SelectWeaponEntry(WeaponSkillTree tree)
    {
        if (tree == null) return;

        currentEntryType = SelectedEntryType.Weapon;
        currentCodexCategory = CodexCategory.Weapons;
        currentSelectedTree = tree;
        currentSelectedPassive = null;
        currentSelectedFusionRecipe = null;
        currentSelectedWeaponFusionRecipe = null;
        currentSelectedEvolutionRecipe = null;

        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();

        if (CheckWeaponUnlocked(tree))
        {
            ShowWeaponStatsView(tree);
        }
        else
        {
            ShowLockedViewWeapon(tree);
        }
    }

    public void SelectPassiveEntry(PassiveItemData passive)
    {
        if (passive == null) return;

        currentEntryType = SelectedEntryType.Passive;
        currentCodexCategory = CodexCategory.Passives;
        currentSelectedPassive = passive;
        currentSelectedTree = null;
        currentSelectedFusionRecipe = null;
        currentSelectedWeaponFusionRecipe = null;
        currentSelectedEvolutionRecipe = null;

        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();

        if (CheckPassiveUnlocked(passive))
        {
            ShowPassiveDescView(passive);
        }
        else
        {
            ShowLockedViewPassive(passive);
        }
    }

    public void SelectFusionEntry(FusionRecipeSO recipe)
    {
        if (recipe == null) return;

        currentEntryType = SelectedEntryType.Fusion;
        currentCodexCategory = CodexCategory.Fusion;
        currentSelectedTree = null;
        currentSelectedPassive = null;
        currentSelectedFusionRecipe = recipe;
        currentSelectedWeaponFusionRecipe = null;
        currentSelectedEvolutionRecipe = null;

        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();
        ShowFusionUnlockView(recipe);
    }

    public void SelectFusionEntry(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null) return;

        currentEntryType = SelectedEntryType.Fusion;
        currentCodexCategory = CodexCategory.Fusion;
        currentSelectedTree = null;
        currentSelectedPassive = null;
        currentSelectedFusionRecipe = null;
        currentSelectedWeaponFusionRecipe = recipe;
        currentSelectedEvolutionRecipe = null;

        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();
        ShowFusionUnlockView(recipe);
    }

    public void SelectFusionEntry(EvolutionRecipeSO recipe)
    {
        if (recipe == null) return;

        currentEntryType = SelectedEntryType.Fusion;
        currentCodexCategory = CodexCategory.Fusion;
        currentSelectedTree = null;
        currentSelectedPassive = null;
        currentSelectedFusionRecipe = null;
        currentSelectedWeaponFusionRecipe = null;
        currentSelectedEvolutionRecipe = recipe;

        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();
        ShowFusionUnlockView(recipe);
    }

    private bool CheckWeaponUnlocked(WeaponSkillTree tree)
    {
        if (tree == null) return false;
        if (IsFutureCodexWeapon(tree)) return false;
        if (tree.isDefaultUnlocked) return true;
        if (PlayerProgressManager.Instance == null) return false;

        if (!string.IsNullOrEmpty(tree.unlockStatKey) && tree.unlockThreshold > 0)
        {
            return HasProgressStatReached(tree.unlockStatKey, tree.unlockThreshold);
        }

        var unlockedItems = PlayerProgressManager.Instance.unlockedItems;
        WeaponStatBlock weapon = tree.associatedWeapon;
        if (unlockedItems != null && weapon != null)
        {
            if (!string.IsNullOrEmpty(weapon.weaponID) && unlockedItems.Contains(weapon.weaponID)) return true;
            if (!string.IsNullOrEmpty(weapon.weaponName) && unlockedItems.Contains(weapon.weaponName)) return true;
        }

        return HasProgressStatReached(tree.unlockStatKey, tree.unlockThreshold);
    }

    private bool IsFutureCodexWeapon(WeaponSkillTree tree)
    {
        return tree != null
            && !string.IsNullOrEmpty(tree.unlockStatKey)
            && tree.unlockStatKey.StartsWith("FutureWeapon_");
    }

    private bool IsFutureCodexWeapon(WeaponStatBlock weapon)
    {
        return weapon != null
            && !string.IsNullOrEmpty(weapon.name)
            && weapon.name.StartsWith("Future_");
    }

    private bool CheckPassiveUnlocked(PassiveItemData item)
    {
        if (item == null) return false;
        if (item.isDefaultUnlocked) return true;
        if (PlayerProgressManager.Instance == null) return false;

        if (!string.IsNullOrEmpty(item.unlockStatKey) && item.unlockThreshold > 0)
        {
            return HasProgressStatReached(item.unlockStatKey, item.unlockThreshold);
        }

        var unlockedItems = PlayerProgressManager.Instance.unlockedItems;
        if (unlockedItems != null)
        {
            if (unlockedItems.Contains(item.name)) return true;
            if (!string.IsNullOrEmpty(item.itemName) && unlockedItems.Contains(item.itemName)) return true;
        }

        return HasProgressStatReached(item.unlockStatKey, item.unlockThreshold);
    }

    private bool CheckFusionRevealed(FusionRecipeSO recipe)
    {
        if (recipe == null) return false;
        return CheckWeaponCodexKnown(recipe.resultWeapon);
    }

    private bool CheckFusionRevealed(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null) return false;
        return CheckWeaponCodexKnown(recipe.resultWeapon);
    }

    private bool CheckFusionRevealed(EvolutionRecipeSO recipe)
    {
        if (recipe == null) return false;
        return CheckWeaponCodexKnown(recipe.ResultWeapon);
    }

    private bool CheckWeaponCodexKnown(WeaponStatBlock weapon)
    {
        if (weapon == null) return false;
        if (PlayerProgressManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(weapon.weaponID) && PlayerProgressManager.Instance.IsItemUnlocked(weapon.weaponID)) return true;
            if (!string.IsNullOrEmpty(weapon.weaponName) && PlayerProgressManager.Instance.IsItemUnlocked(weapon.weaponName)) return true;
        }

        return HasWeaponReachedLevel(weapon, 1);
    }

    private bool HasWeaponReachedLevel(WeaponStatBlock weapon, int level)
    {
        if (weapon == null) return false;
        int targetLevel = Mathf.Max(1, level);

        if (WeaponController.Instance != null && WeaponController.Instance.ownedWeapons != null)
        {
            foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
            {
                if (owned == null) continue;
                bool matches = owned.InheritsSkillSource(weapon) ||
                               owned.stats == weapon ||
                               (owned.weaponPartInstance != null && owned.weaponPartInstance.StatBlock == weapon);
                if (matches && owned.currentLevel >= targetLevel) return true;
            }
        }

        if (PlayerProgressManager.Instance == null) return false;
        return PlayerProgressManager.Instance.GetAchievementStat(PlayerProgressManager.GetWeaponLevelStatKey(weapon)) >= targetLevel;
    }

    private WeaponStatBlock GetPrimaryFusionWeapon(FusionRecipeSO recipe)
    {
        if (recipe == null) return null;
        if (recipe.weaponA != null) return recipe.weaponA;
        return recipe.weaponB;
    }

    private bool IsRecipeTriggerMet(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null) return false;
        int requiredLevel = recipe.requiredWeaponLevel > 0
            ? recipe.requiredWeaponLevel
            : Mathf.Max(1, recipe.codexRevealWeaponLevel);
        return HasWeaponReachedLevel(recipe.triggerWeapon, requiredLevel);
    }

    private bool IsFusionConditionMet(FusionCondition condition)
    {
        if (condition == null) return true;

        switch (condition.type)
        {
            case ConditionType.Weapon:
                return HasWeaponReachedLevel(condition.requiredWeapon, Mathf.Max(1, condition.requiredWeaponLevel));
            case ConditionType.Passive:
                PassiveItemData passive = GetConditionPassive(condition);
                return HasPassiveReachedLevel(passive, condition.requiredPassiveId, condition.requiredPassiveLevel);
            case ConditionType.Talent:
                return UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill(condition.requiredTalentId);
            default:
                return false;
        }
    }

    private bool HasPassiveReachedLevel(PassiveItemData passive, string passiveId, int level)
    {
        int targetLevel = GetEffectivePassiveRequiredLevel(passive, level);
        if (PlayerStats.Instance != null && PlayerStats.Instance.activePassiveItems != null)
        {
            foreach (RuntimePassiveItem item in PlayerStats.Instance.activePassiveItems)
            {
                if (item == null || item.data == null) continue;
                bool matches = item.data == passive ||
                               string.Equals(item.data.name, passiveId, System.StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.data.itemName, passiveId, System.StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.data.statType.ToString(), passiveId, System.StringComparison.OrdinalIgnoreCase);
                if (matches && item.currentLevel >= targetLevel) return true;
            }
        }

        if (PlayerProgressManager.Instance == null) return false;
        if (passive != null && PlayerProgressManager.Instance.GetAchievementStat(PlayerProgressManager.GetPassiveLevelStatKey(passive)) >= targetLevel) return true;

        PassiveItemData idPassive = passive != null ? passive : FindPassiveByIdentifier(passiveId);
        if (idPassive == null) return false;

        targetLevel = GetEffectivePassiveRequiredLevel(idPassive, level);
        return PlayerProgressManager.Instance.GetAchievementStat(PlayerProgressManager.GetPassiveLevelStatKey(idPassive)) >= targetLevel;
    }

    private int GetEffectivePassiveRequiredLevel(PassiveItemData passive, int level)
    {
        int targetLevel = Mathf.Max(1, level);
        if (passive != null)
        {
            targetLevel = Mathf.Min(targetLevel, passive.EffectiveMaxLevel);
        }
        return targetLevel;
    }

    private bool HasProgressStatReached(string statKey, int threshold)
    {
        if (PlayerProgressManager.Instance == null || string.IsNullOrEmpty(statKey)) return false;
        var stats = PlayerProgressManager.Instance.achievementStats;
        return stats != null && stats.TryGetValue(statKey, out int currentVal) && currentVal >= threshold;
    }

    private void HideAllViews()
    {
        if (lockedViewRoot) lockedViewRoot.SetActive(false);
        if (weaponStatsViewRoot) weaponStatsViewRoot.SetActive(false);
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(false);
        if (unlockedViewRoot) unlockedViewRoot.SetActive(false);

        ClearStatSlots();
        ClearRecommendations();

        if (weaponStatsIcon) weaponStatsIcon.gameObject.SetActive(false);
        if (weaponStatsName) weaponStatsName.gameObject.SetActive(false);
        if (codexHeroSummaryText) codexHeroSummaryText.text = "";
        if (codexDetailBodyText) codexDetailBodyText.text = "";
        if (codexTagsText) codexTagsText.text = "";
        if (codexRecommendedTitleText) codexRecommendedTitleText.gameObject.SetActive(false);
    }

    private void ShowCategoryPlaceholder(string title, string summary, string body)
    {
        currentEntryType = SelectedEntryType.None;
        currentSelectedTree = null;
        currentSelectedPassive = null;
        currentSelectedFusionRecipe = null;
        currentSelectedWeaponFusionRecipe = null;
        currentSelectedEvolutionRecipe = null;

        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);
        RefreshSidebarVisuals();
        UpdateCodexTabVisuals();

        if (weaponStatsIcon != null)
        {
            weaponStatsIcon.gameObject.SetActive(false);
            weaponStatsIcon.enabled = false;
        }

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = title;
        }

        if (codexHeroSummaryText != null) codexHeroSummaryText.text = summary;
        if (codexTagsText != null) codexTagsText.text = "\u5f85\u63a5\u5165 | Codex";
        if (codexDetailBodyText != null) codexDetailBodyText.text = body;
    }

    private void ShowVampireStyleEntry(Sprite icon, string title, string description, string footer, bool bright)
    {
        HideAllViews();
        if (weaponStatsViewRoot) weaponStatsViewRoot.SetActive(true);

        if (weaponStatsIcon != null)
        {
            weaponStatsIcon.gameObject.SetActive(true);
            weaponStatsIcon.sprite = icon;
            weaponStatsIcon.enabled = icon != null;
            weaponStatsIcon.color = bright ? Color.white : GetSilhouetteColor();
        }

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = title;
        }

        if (codexDetailBodyText != null)
        {
            codexDetailBodyText.text = CompactLine(description, "");
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = CompactLine(footer, "");
        }
    }

    private void ShowVampireStyleWeaponView(WeaponSkillTree tree, bool unlocked)
    {
        WeaponStatBlock weapon = tree != null ? tree.associatedWeapon : null;
        string weaponName = weapon != null ? GetWeaponDisplayName(weapon) : tree != null ? tree.name : "???";

        if (!unlocked)
        {
            bool future = IsFutureCodexWeapon(tree);
            string title = future ? L("未来解锁", "Future Unlock") : L("未解锁", "Locked");
            string description = future
                ? L("未来版本武器，暂不进入当前局内卡池。", "Future weapon. Not in the current run pool.")
                : tree != null && !string.IsNullOrEmpty(tree.lockedDescription)
                    ? tree.lockedDescription
                    : L("尚未发现。", "Not discovered yet.");
            string footer = BuildProgressFooter(tree != null ? tree.unlockStatKey : "", tree != null ? tree.unlockThreshold : 0);
            ShowVampireStyleEntry(weapon != null ? weapon.weaponIcon : null, title, description, footer, false);
            return;
        }

        ShowVampireStyleEntry(
            weapon != null ? weapon.weaponIcon : null,
            weaponName,
            BuildVampireStyleWeaponDescription(weapon),
            BuildWeaponIgnoresLine(weapon),
            true);
    }

    private void ShowVampireStylePassiveView(PassiveItemData passive, bool unlocked)
    {
        if (passive == null)
        {
            ShowVampireStyleEntry(null, "???", L("尚未发现。", "Not discovered yet."), "", false);
            return;
        }

        if (!unlocked)
        {
            ShowVampireStyleEntry(
                passive.icon,
                L("未解锁", "Locked"),
                passive.lockedDescription,
                BuildProgressFooter(passive.unlockStatKey, passive.unlockThreshold),
                false);
            return;
        }

        string description = !string.IsNullOrEmpty(passive.description)
            ? passive.description
            : IsEnglishLanguage
                ? $"{GetPassiveStatLabel(passive.statType)} +{FormatPassiveValue(passive.statType, passive.valuePerLevel)} per level."
                : $"每级 {GetPassiveStatLabel(passive.statType)} +{FormatPassiveValue(passive.statType, passive.valuePerLevel)}。";
        string footer = IsEnglishLanguage ? $"Max Level: {passive.EffectiveMaxLevel}" : $"最高等级: {passive.EffectiveMaxLevel}";
        ShowVampireStyleEntry(passive.icon, passive.itemName, description, footer, true);
    }

    private void ShowVampireStyleFusionView(FusionRecipeSO recipe)
    {
        if (recipe == null) return;

        WeaponStatBlock result = recipe.resultWeapon;
        bool revealed = CheckFusionRevealed(recipe);
        string weaponA = GetWeaponDisplayName(recipe.weaponA);
        string weaponB = GetWeaponDisplayName(recipe.weaponB);
        string resultName = GetWeaponDisplayName(result);
        string title = revealed ? resultName : L("未知进化", "Unknown Evolution");
        string description = revealed
            ? CompactLine(recipe.description, L($"{weaponA} 与 {weaponB} 融合进化。", $"Evolves from {weaponA} and {weaponB}."))
            : L("获得该进化后显示名称和图标。", "Name and icon appear after this evolution is unlocked.");
        string footer = revealed
            ? FormatRequirement($"{weaponA} Lv.5 + {weaponB} Lv.5")
            : BuildFusionProgressFooter(
                CountMetConditions(new List<FusionConditionVisual>
                {
                    new FusionConditionVisual(null, HasWeaponReachedLevel(recipe.weaponA, 5)),
                    new FusionConditionVisual(null, HasWeaponReachedLevel(recipe.weaponB, 5))
                }),
                2);
        Sprite icon = recipe.fusionIcon != null ? recipe.fusionIcon : result != null ? result.weaponIcon : null;
        ShowVampireStyleEntry(icon, title, description, footer, revealed);
    }

    private void ShowVampireStyleFusionView(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null) return;

        bool revealed = CheckFusionRevealed(recipe);
        WeaponStatBlock result = recipe.resultWeapon;
        string triggerName = GetWeaponDisplayName(recipe.triggerWeapon);
        string resultName = GetWeaponDisplayName(result);
        string title = revealed
            ? !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : resultName
            : L("未知进化", "Unknown Evolution");
        int triggerLevel = recipe.requiredWeaponLevel > 0
            ? recipe.requiredWeaponLevel
            : Mathf.Max(1, recipe.codexRevealWeaponLevel);
        string conditionLine = BuildFusionConditionLine(recipe.conditions);
        string description = revealed
            ? CompactLine(recipe.description, L($"{triggerName} 进化为 {resultName}。", $"Evolves {triggerName} into {resultName}."))
            : L("获得该进化后显示名称和图标。", "Name and icon appear after this evolution is unlocked.");
        List<FusionConditionVisual> conditionVisuals = BuildWeaponFusionConditionVisuals(recipe, triggerLevel);
        string footer = revealed
            ? FormatRequirement($"{triggerName} Lv.{triggerLevel} + {conditionLine}")
            : BuildFusionProgressFooter(CountMetConditions(conditionVisuals), Mathf.Max(1, conditionVisuals.Count));
        Sprite icon = recipe.cardIcon != null
            ? recipe.cardIcon
            : result != null ? result.weaponIcon : recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null;
        ShowVampireStyleEntry(icon, title, description, footer, revealed);
    }

    private void ShowVampireStyleFusionView(EvolutionRecipeSO recipe)
    {
        if (recipe == null) return;

        bool revealed = CheckFusionRevealed(recipe);
        WeaponStatBlock result = recipe.ResultWeapon;
        string weaponName = GetWeaponDisplayName(recipe.MainWeapon);
        string stoneName = GetStoneTypeLabel(recipe.requiredStoneType);
        string resultName = result != null ? GetWeaponDisplayName(result) : recipe.DisplayName;
        int requiredLevel = Mathf.Max(1, recipe.requiredWeaponLevel);
        string title = revealed ? (!string.IsNullOrEmpty(recipe.DisplayName) ? recipe.DisplayName : resultName) : L("未知进化", "Unknown Evolution");
        string description = revealed
            ? CompactLine(recipe.description, L($"{weaponName} 与 {stoneName} 元素进化。", $"Evolves {weaponName} with {stoneName}."))
            : L("获得该进化后显示名称和图标。", "Name and icon appear after this evolution is unlocked.");
        string footer = revealed
            ? FormatRequirement($"{weaponName} Lv.{requiredLevel} + {stoneName}")
            : BuildFusionProgressFooter(HasWeaponReachedLevel(recipe.MainWeapon, requiredLevel) ? 1 : 0, 2);
        Sprite icon = recipe.cardIcon != null
            ? recipe.cardIcon
            : result != null ? result.weaponIcon : recipe.MainWeapon != null ? recipe.MainWeapon.weaponIcon : null;
        ShowVampireStyleEntry(icon, title, description, footer, revealed);
    }

    private List<FusionConditionVisual> BuildWeaponFusionConditionVisuals(WeaponFusionRecipeSO recipe, int triggerLevel)
    {
        List<FusionConditionVisual> conditions = new List<FusionConditionVisual>();
        if (recipe == null) return conditions;

        conditions.Add(new FusionConditionVisual(null, HasWeaponReachedLevel(recipe.triggerWeapon, Mathf.Max(1, triggerLevel))));
        if (recipe.conditions != null)
        {
            foreach (FusionCondition condition in recipe.conditions)
            {
                if (condition == null) continue;
                conditions.Add(new FusionConditionVisual(null, IsFusionConditionMet(condition)));
            }
        }
        return conditions;
    }

    private string BuildVampireStyleWeaponDescription(WeaponStatBlock weapon)
    {
        if (weapon == null) return L("无数据。", "No data.");

        switch (weapon.behavior)
        {
            case WeaponBehaviorType.MeleeAOE: return L("挥砍附近敌人。", "Attacks nearby enemies with a sweeping slash.");
            case WeaponBehaviorType.Standard: return L("向敌人发射弹道。", "Fires projectiles at enemies.");
            case WeaponBehaviorType.Pierce: return L("发射可穿透敌人的弹道。", "Fires projectiles that pass through enemies.");
            case WeaponBehaviorType.ParabolicAOE: return L("向敌群抛射爆炸物。", "Throws an explosive projectile at enemy groups.");
            case WeaponBehaviorType.Chain: return L("命中后跳转到附近目标。", "Strikes enemies and jumps to nearby targets.");
            case WeaponBehaviorType.Orbital: return L("环绕角色并攻击近身敌人。", "Orbits the character and hits nearby enemies.");
            case WeaponBehaviorType.PersistentAOE: return L("制造持续伤害区域。", "Creates an area that damages enemies over time.");
            case WeaponBehaviorType.SummonDrone: return L("召唤自动攻击的伙伴。", "Summons an ally that attacks automatically.");
            case WeaponBehaviorType.Beam: return L("持续引导光束灼烧敌人。", "Channels a beam that burns through enemies.");
            case WeaponBehaviorType.Funnel: return L("部署自动开火的浮游炮。", "Deploys floating guns that fire automatically.");
            case WeaponBehaviorType.SuperMech: return L("召唤强力机械伙伴。", "Summons a powerful mechanical ally.");
            case WeaponBehaviorType.Landmine: return L("放置接近后爆炸的陷阱。", "Drops traps that explode when enemies approach.");
            case WeaponBehaviorType.Boomerang: return L("投出会返回的武器。", "Throws a weapon that returns after hitting enemies.");
            case WeaponBehaviorType.Aura: return L("在角色周围生成光环。", "Creates an aura around the character.");
            case WeaponBehaviorType.CreateAndForget: return L("生成独立法术效果。", "Creates an independent spell effect.");
            case WeaponBehaviorType.FlyingDagger: return L("召唤追踪目标的飞刃。", "Summons blades that chase targets.");
            case WeaponBehaviorType.FrostNova: return L("在敌人脚下爆发冰霜并控场。", "Bursts frost under enemies and controls groups.");
            case WeaponBehaviorType.LaserCore: return L("聚焦光束灼烧目标。", "Focuses a burning beam on a target.");
            default: return L("自动攻击敌人。", "Attacks enemies automatically.");
        }
    }

    private string BuildWeaponIgnoresLine(WeaponStatBlock weapon)
    {
        if (weapon == null) return L("不受影响: 无", "Ignores: None");

        List<string> ignores = new List<string>();
        switch (weapon.behavior)
        {
            case WeaponBehaviorType.MeleeAOE:
                ignores.Add(L("弹道速度", "Projectile Speed"));
                ignores.Add(L("持续时间", "Duration"));
                break;
            case WeaponBehaviorType.Beam:
            case WeaponBehaviorType.LaserCore:
                ignores.Add(L("弹道速度", "Projectile Speed"));
                ignores.Add(L("穿透", "Pierce"));
                break;
            case WeaponBehaviorType.Orbital:
            case WeaponBehaviorType.Aura:
                ignores.Add(L("弹道速度", "Projectile Speed"));
                break;
            case WeaponBehaviorType.PersistentAOE:
            case WeaponBehaviorType.Landmine:
                ignores.Add(L("穿透", "Pierce"));
                break;
            case WeaponBehaviorType.SummonDrone:
            case WeaponBehaviorType.SuperMech:
                ignores.Add(L("穿透", "Pierce"));
                ignores.Add(L("范围", "Area"));
                break;
        }

        string prefix = L("不受影响", "Ignores");
        string none = L("无", "None");
        return ignores.Count > 0 ? $"{prefix}: {string.Join(", ", ignores)}." : $"{prefix}: {none}.";
    }

    private string BuildProgressFooter(string statKey, int threshold)
    {
        if (threshold <= 0) return L("已锁定。", "Locked.");

        int current = 0;
        if (PlayerProgressManager.Instance != null && !string.IsNullOrEmpty(statKey))
        {
            var stats = PlayerProgressManager.Instance.achievementStats;
            if (stats != null) stats.TryGetValue(statKey, out current);
        }

        return $"{L("进度", "Progress")}: {Mathf.Clamp(current, 0, threshold)} / {threshold}";
    }

    private string BuildFusionProgressFooter(int current, int target)
    {
        int safeTarget = Mathf.Max(1, target);
        return $"{L("进度", "Progress")}: {Mathf.Clamp(current, 0, safeTarget)} / {safeTarget}";
    }

    private string FormatRequirement(string requirement)
    {
        return $"{L("需求", "Requires")}: {requirement}";
    }

    private string CompactLine(string text, string fallback)
    {
        string value = string.IsNullOrWhiteSpace(text) ? fallback : text;
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private void ShowWeaponStatsView(WeaponSkillTree tree)
    {
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStyleWeaponView(tree, true);
            return;
        }

        HideAllViews();
        if (weaponStatsViewRoot) weaponStatsViewRoot.SetActive(true);

        WeaponStatBlock weapon = tree.associatedWeapon;
        if (weapon == null) return;

        if (weaponStatsIcon != null)
        {
            weaponStatsIcon.gameObject.SetActive(true);
            weaponStatsIcon.sprite = weapon.weaponIcon;
            weaponStatsIcon.enabled = weapon.weaponIcon != null;
            weaponStatsIcon.color = Color.white;
        }

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            string localizedName = LanguageTable.LocalizeWeaponName(weapon.weaponName, LocalizationManager.CurrentLanguage);
            weaponStatsName.text = localizedName;
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = BuildWeaponCodexSummary(weapon);
        }

        if (codexDetailBodyText != null)
        {
            codexDetailBodyText.text = BuildWeaponDetailBody(weapon);
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = GetWeaponTagLine(weapon);
        }

        ClearRecommendations();

        if (weaponStatsContainer == null || weaponStatItemPrefab == null) return;

        int damage = Mathf.Max(weapon.baseDirectDamage, weapon.baseAoeDamage);
        CreateStatSlot(iconDamage, "DMG", damage.ToString());

        float cooldown = weapon.baseFireRate > 0f ? 1f / weapon.baseFireRate : 0f;
        CreateStatSlot(iconFireRate, "CD", cooldown.ToString("F1") + "S");

        CreateStatSlot(iconRange, "AREA", weapon.baseAoeRadius.ToString("F0") + "M");
        CreateStatSlot(iconProjectile, "PIERCE", weapon.basePierceCount.ToString());
    }

    private void ShowLockedFusionView(WeaponStatBlock clueWeapon, int revealLevel)
    {
        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);

        string clueName = GetWeaponDisplayName(clueWeapon);
        int targetLevel = Mathf.Max(1, revealLevel);

        if (weaponStatsIcon != null)
        {
            weaponStatsIcon.gameObject.SetActive(true);
            weaponStatsIcon.sprite = clueWeapon != null ? clueWeapon.weaponIcon : null;
            weaponStatsIcon.enabled = weaponStatsIcon.sprite != null;
            weaponStatsIcon.color = new Color(0.05f, 0.04f, 0.035f, 0.86f);
        }

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = "\u672a\u77e5\u8fdb\u5316";
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = $"{clueName} Lv.{targetLevel}\n\u8fbe\u5230\u7b49\u7ea7\u540e\u663e\u793a\u914d\u65b9\u989c\u8272";
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = "\u672a\u77e5 | \u8fdb\u5316 | \u56fe\u9274\u7ebf\u7d22";
        }

        if (codexDetailBodyText != null)
        {
            codexDetailBodyText.text = "\u8fd9\u662f\u4e00\u6761\u5c1a\u672a\u8bc6\u522b\u7684\u8fdb\u5316\u7ebf\u7d22\u3002\u5c06\u5bf9\u5e94\u4e3b\u6b66\u5668\u63d0\u5347\u5230\u6307\u5b9a\u7b49\u7ea7\u540e\uff0c\u56fe\u6807\u4f1a\u4ece\u9ed1\u5f71\u53d8\u4e3a\u5f69\u8272\uff0c\u5e76\u663e\u793a\u9700\u8981\u7684\u9053\u5177\u6216\u5143\u7d20\u6761\u4ef6\u3002";
        }

        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            CreateStatSlot(null, "\u7ebf\u7d22", clueName);
            CreateStatSlot(null, "\u9700\u6c42", $"Lv.{targetLevel}");
            CreateStatSlot(null, "\u72b6\u6001", "\u672a\u53d1\u73b0");
        }
    }

    private void ShowFusionUnlockView(FusionRecipeSO recipe)
    {
        if (recipe == null) return;
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStyleFusionView(recipe);
            return;
        }

        List<FusionConditionVisual> conditions = new List<FusionConditionVisual>
        {
            new FusionConditionVisual(recipe.weaponA != null ? recipe.weaponA.weaponIcon : null, HasWeaponReachedLevel(recipe.weaponA, 5)),
            new FusionConditionVisual(recipe.weaponB != null ? recipe.weaponB.weaponIcon : null, HasWeaponReachedLevel(recipe.weaponB, 5))
        };
        int current = CountMetConditions(conditions);
        int target = conditions.Count;

        Sprite icon = recipe.fusionIcon != null
            ? recipe.fusionIcon
            : recipe.resultWeapon != null ? recipe.resultWeapon.weaponIcon : null;
        ShowFusionUnlockView(icon, current >= target, conditions, current, target);
    }

    private void ShowFusionUnlockView(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null) return;
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStyleFusionView(recipe);
            return;
        }

        List<FusionConditionVisual> conditions = new List<FusionConditionVisual>();
        int current = 0;
        int target = 0;

        int triggerLevel = recipe.requiredWeaponLevel > 0
            ? recipe.requiredWeaponLevel
            : Mathf.Max(1, recipe.codexRevealWeaponLevel);
        conditions.Add(new FusionConditionVisual(
            recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null,
            HasWeaponReachedLevel(recipe.triggerWeapon, triggerLevel)));

        if (recipe.conditions != null)
        {
            foreach (FusionCondition condition in recipe.conditions)
            {
                if (condition == null) continue;
                conditions.Add(BuildFusionConditionVisual(condition));
            }
        }

        current = CountMetConditions(conditions);
        target = conditions.Count;
        Sprite icon = recipe.cardIcon != null
            ? recipe.cardIcon
            : recipe.resultWeapon != null
                ? recipe.resultWeapon.weaponIcon
                : recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null;
        ShowFusionUnlockView(icon, current >= Mathf.Max(1, target), conditions, current, Mathf.Max(1, target));
    }

    private void ShowFusionUnlockView(EvolutionRecipeSO recipe)
    {
        if (recipe == null) return;
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStyleFusionView(recipe);
            return;
        }

        int requiredLevel = Mathf.Max(1, recipe.requiredWeaponLevel);
        bool weaponMet = HasWeaponReachedLevel(recipe.MainWeapon, requiredLevel);
        List<FusionConditionVisual> conditions = new List<FusionConditionVisual>
        {
            new FusionConditionVisual(recipe.MainWeapon != null ? recipe.MainWeapon.weaponIcon : null, weaponMet),
            new FusionConditionVisual(recipe.cardIcon, weaponMet, GetStoneTypeLabel(recipe.requiredStoneType))
        };
        int current = CountMetConditions(conditions);
        int target = conditions.Count;
        Sprite icon = recipe.cardIcon != null
            ? recipe.cardIcon
            : recipe.ResultWeapon != null
                ? recipe.ResultWeapon.weaponIcon
                : recipe.MainWeapon != null ? recipe.MainWeapon.weaponIcon : null;

        ShowFusionUnlockView(icon, current >= target, conditions, current, target);
    }

    private void ShowFusionUnlockView(Sprite icon, bool unlocked, List<FusionConditionVisual> conditions, int current, int target)
    {
        HideAllViews();
        if (lockedViewRoot) lockedViewRoot.SetActive(true);

        if (lockedWeaponIcon != null)
        {
            lockedWeaponIcon.sprite = icon;
            lockedWeaponIcon.enabled = icon != null;
            lockedWeaponIcon.color = unlocked ? Color.white : GetSilhouetteColor();
        }

        if (lockConditionText != null)
        {
            lockConditionText.text = "";
            lockConditionText.gameObject.SetActive(false);
        }
        PopulateLockConditionIcons(conditions);

        int safeTarget = Mathf.Max(1, target);
        int safeCurrent = Mathf.Clamp(current, 0, safeTarget);
        if (lockProgressBar != null)
        {
            lockProgressBar.gameObject.SetActive(true);
            lockProgressBar.maxValue = safeTarget;
            lockProgressBar.value = safeCurrent;
        }

        if (lockProgressText != null)
        {
            lockProgressText.gameObject.SetActive(true);
            lockProgressText.text = $"{safeCurrent} / {safeTarget}";
        }
    }

    private int CountMetConditions(List<FusionConditionVisual> conditions)
    {
        if (conditions == null) return 0;

        int count = 0;
        foreach (FusionConditionVisual condition in conditions)
        {
            if (condition != null && condition.isMet) count++;
        }

        return count;
    }

    private FusionConditionVisual BuildFusionConditionVisual(FusionCondition condition)
    {
        if (condition == null) return new FusionConditionVisual(null, false, "?");

        bool isMet = IsFusionConditionMet(condition);
        switch (condition.type)
        {
            case ConditionType.Weapon:
                return new FusionConditionVisual(
                    condition.requiredWeapon != null ? condition.requiredWeapon.weaponIcon : null,
                    isMet,
                    "\u6b66");
            case ConditionType.Passive:
                PassiveItemData passive = GetConditionPassive(condition);
                return new FusionConditionVisual(
                    passive != null ? passive.icon : null,
                    isMet,
                    "\u9053");
            case ConditionType.Talent:
                return new FusionConditionVisual(null, isMet, "\u5929");
            default:
                return new FusionConditionVisual(null, isMet, "?");
        }
    }

    private void PopulateLockConditionIcons(List<FusionConditionVisual> conditions)
    {
        if (lockConditionIconContainer == null) return;

        foreach (Transform child in lockConditionIconContainer)
        {
            Destroy(child.gameObject);
        }

        bool hasConditions = conditions != null && conditions.Count > 0;
        lockConditionIconContainer.gameObject.SetActive(hasConditions);
        if (!hasConditions) return;

        for (int i = 0; i < conditions.Count; i++)
        {
            if (i > 0) CreateLockConditionPlus();
            CreateLockConditionIcon(conditions[i]);
        }
    }

    private void HideLockConditionIcons()
    {
        if (lockConditionIconContainer == null) return;

        foreach (Transform child in lockConditionIconContainer)
        {
            Destroy(child.gameObject);
        }

        lockConditionIconContainer.gameObject.SetActive(false);
    }

    private void CreateLockConditionIcon(FusionConditionVisual condition)
    {
        if (lockConditionIconContainer == null) return;

        bool isMet = condition != null && condition.isMet;
        Sprite sprite = condition != null ? condition.icon : null;
        string fallbackText = condition != null ? condition.fallbackText : null;

        GameObject frame = CreatePanel("Runtime_LockConditionIcon", lockConditionIconContainer, new Color(0.30f, 0.16f, 0.08f, 0.94f));
        ApplyDemoSprite(frame.GetComponent<Image>(), "codex_icon_frame", Color.white);
        Image frameImage = frame.GetComponent<Image>();
        if (frameImage != null)
        {
            frameImage.color = isMet ? Color.white : new Color(0.44f, 0.35f, 0.25f, 1f);
            frameImage.raycastTarget = false;
        }

        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(92f, 92f);
        LayoutElement frameLayout = frame.AddComponent<LayoutElement>();
        frameLayout.preferredWidth = 92f;
        frameLayout.preferredHeight = 92f;
        frameLayout.flexibleWidth = 0f;
        frameLayout.flexibleHeight = 0f;

        GameObject iconObj = CreateUIObject("Icon", frame.transform);
        StretchToParent(iconObj.GetComponent<RectTransform>(), 13f, 13f, 13f, 13f);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
        icon.color = isMet ? Color.white : GetSilhouetteColor();
        icon.raycastTarget = false;

        if (sprite == null && !string.IsNullOrEmpty(fallbackText))
        {
            string shortText = fallbackText.Length > 1 ? fallbackText.Substring(0, 1) : fallbackText;
            TextMeshProUGUI fallback = CreateText("Fallback", frame.transform, shortText, 34f, FontStyles.Bold,
                isMet ? new Color(1f, 0.82f, 0.34f, 1f) : GetSilhouetteColor(),
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-18f, -18f));
            ConfigureTextFit(fallback, 22f, 34f, 1);
        }
    }

    private void CreateLockConditionPlus()
    {
        if (lockConditionIconContainer == null) return;

        GameObject plusObj = CreateUIObject("Runtime_LockConditionPlus", lockConditionIconContainer);
        RectTransform rect = plusObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(42f, 92f);
        LayoutElement layout = plusObj.AddComponent<LayoutElement>();
        layout.preferredWidth = 42f;
        layout.preferredHeight = 92f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        TextMeshProUGUI plus = plusObj.AddComponent<TextMeshProUGUI>();
        plus.text = "+";
        plus.fontSize = 38f;
        plus.fontStyle = FontStyles.Bold;
        plus.color = new Color(0.46f, 0.20f, 0.07f, 1f);
        plus.alignment = TextAlignmentOptions.Center;
        plus.raycastTarget = false;
        ApplyUIFont(plus);
        ConfigureTextFit(plus, 24f, 38f, 1);
    }

    private string GetSimpleFusionConditionText(FusionCondition condition)
    {
        if (condition == null) return "\u672a\u914d\u7f6e";

        switch (condition.type)
        {
            case ConditionType.Weapon:
                int weaponLevel = Mathf.Max(1, condition.requiredWeaponLevel);
                return $"{GetWeaponDisplayName(condition.requiredWeapon)} Lv.{weaponLevel}";
            case ConditionType.Passive:
                PassiveItemData passive = GetConditionPassive(condition);
                string passiveName = passive != null ? passive.itemName : condition.requiredPassiveId;
                if (string.IsNullOrEmpty(passiveName)) passiveName = "\u672a\u914d\u7f6e\u9053\u5177";
                return $"{passiveName} Lv.{GetEffectivePassiveRequiredLevel(passive, condition.requiredPassiveLevel)}";
            case ConditionType.Talent:
                return string.IsNullOrEmpty(condition.requiredTalentId) ? "\u672a\u914d\u7f6e\u5929\u8d4b" : condition.requiredTalentId;
            default:
                return "\u672a\u914d\u7f6e";
        }
    }

    private void SetDetailHeroIcon(Sprite icon, bool isBright)
    {
        if (weaponStatsIcon == null) return;
        weaponStatsIcon.gameObject.SetActive(true);
        weaponStatsIcon.sprite = icon;
        weaponStatsIcon.enabled = icon != null;
        weaponStatsIcon.color = isBright ? Color.white : GetSilhouetteColor();
    }

    private static Color GetSilhouetteColor()
    {
        return new Color(0.05f, 0.04f, 0.035f, 0.86f);
    }

    private void ShowFusionDescView(FusionRecipeSO recipe)
    {
        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);
        if (recipe == null) return;

        WeaponStatBlock result = recipe.resultWeapon;
        string weaponA = GetWeaponDisplayName(recipe.weaponA);
        string weaponB = GetWeaponDisplayName(recipe.weaponB);
        string resultName = GetWeaponDisplayName(result);
        string title = !string.IsNullOrEmpty(resultName) ? resultName : recipe.name;
        bool isRevealed = CheckFusionRevealed(recipe);

        Sprite icon = recipe.fusionIcon != null ? recipe.fusionIcon : result != null ? result.weaponIcon : null;
        SetDetailHeroIcon(icon, isRevealed);

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = isRevealed ? title : "\u672a\u77e5\u8fdb\u5316";
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = isRevealed
                ? $"\u914d\u65b9: {weaponA} + {weaponB}\n\u7ed3\u679c: {title}"
                : $"\u672a\u77e5\u8fdb\u5316\n{weaponA} Lv.5 / {weaponB} Lv.5";
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = "\u878d\u5408 | \u8d85\u6b66 | \u56fe\u9274";
        }

        if (codexDetailBodyText != null)
        {
            codexDetailBodyText.text = !isRevealed
                ? "\u8fd9\u662f\u4e00\u6761\u672a\u89e3\u5bc6\u7684\u8fdb\u5316\u7ebf\u7d22\u3002\u4e0a\u65b9\u662f\u8fdb\u5316\u7ed3\u679c\u526a\u5f71\uff0c\u4e0b\u65b9\u6761\u4ef6\u56fe\u6807\u8fbe\u6210\u540e\u4f1a\u4ece\u9ed1\u8272\u53d8\u4eae\u3002"
                : string.IsNullOrEmpty(recipe.description)
                ? $"\u9700\u8981 {weaponA} \u548c {weaponB} \u540c\u65f6\u8fbe\u6210\u6761\u4ef6\u540e\u878d\u5408\u3002"
                : recipe.description;
        }

        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            CreateStatSlot(null, "\u6750\u6599A", weaponA);
            CreateStatSlot(null, "\u6750\u6599B", weaponB);
            CreateStatSlot(null, "\u7ed3\u679c", isRevealed ? title : "\u672a\u77e5");
            CreateStatSlot(null, "\u7c7b\u578b", "\u878d\u5408");
        }

        CreateRecommendationIcon(recipe.weaponA != null ? recipe.weaponA.weaponIcon : null, weaponA, HasWeaponReachedLevel(recipe.weaponA, 5));
        CreateRecommendationIcon(recipe.weaponB != null ? recipe.weaponB.weaponIcon : null, weaponB, HasWeaponReachedLevel(recipe.weaponB, 5));
        if (codexRecommendedTitleText != null) codexRecommendedTitleText.text = "\u8fdb\u5316\u6761\u4ef6";
        if (codexRecommendedTitleText != null) codexRecommendedTitleText.gameObject.SetActive(true);
    }

    private void ShowFusionDescView(WeaponFusionRecipeSO recipe)
    {
        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);
        if (recipe == null) return;

        WeaponStatBlock result = recipe.resultWeapon;
        string triggerName = GetWeaponDisplayName(recipe.triggerWeapon);
        string resultName = GetWeaponDisplayName(result);
        string title = !string.IsNullOrEmpty(recipe.recipeName)
            ? recipe.recipeName
            : !string.IsNullOrEmpty(resultName) ? resultName : recipe.name;
        string triggerRequirement = recipe.requiredWeaponLevel > 0
            ? $"Lv.{recipe.requiredWeaponLevel}"
            : recipe.requiredStage.ToString();
        string conditionLine = BuildFusionConditionLine(recipe.conditions);
        bool isRevealed = CheckFusionRevealed(recipe);
        bool triggerMet = IsRecipeTriggerMet(recipe);

        Sprite icon = recipe.cardIcon != null ? recipe.cardIcon : result != null ? result.weaponIcon : recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null;
        SetDetailHeroIcon(icon, isRevealed);

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = isRevealed ? title : "\u672a\u77e5\u8fdb\u5316";
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = isRevealed
                ? $"\u914d\u65b9: {triggerName} + {conditionLine}\n\u9700\u6c42: {triggerName} {triggerRequirement}"
                : $"\u672a\u77e5\u8fdb\u5316\n{triggerName} {triggerRequirement}";
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = recipe.codexOnly
                ? "\u8bbe\u8ba1\u4f4d | \u6b66\u5668+\u9053\u5177 | \u8fdb\u5316"
                : $"\u878d\u5408 | {recipe.fusionType} | \u7ed3\u679c: {resultName}";
        }

        if (codexDetailBodyText != null)
        {
            string body = !isRevealed
                ? "\u8fd9\u662f\u4e00\u6761\u672a\u89e3\u5bc6\u7684\u8fdb\u5316\u7ebf\u7d22\u3002\u4e0a\u65b9\u662f\u8fdb\u5316\u7ed3\u679c\u526a\u5f71\uff0c\u4e0b\u65b9\u662f\u8fdb\u5316\u6761\u4ef6\u3002\u8fbe\u6210\u7684\u6761\u4ef6\u4f1a\u4ece\u9ed1\u8272\u53d8\u4eae\u3002"
                : string.IsNullOrEmpty(recipe.description)
                ? $"\u89e6\u53d1\u6b66\u5668\u8fbe\u5230 {recipe.requiredStage} \u540e\uff0c\u68c0\u67e5\u7ec4\u5408\u6761\u4ef6\u5e76\u878d\u5408\u4e3a {resultName}\u3002"
                : recipe.description;
            if (recipe.requiredWeaponLevel > 0)
            {
                body += $"\n\n\u8fdb\u5316\u95e8\u69db: {triggerName} Lv.{recipe.requiredWeaponLevel} + {conditionLine}\u3002";
            }
            codexDetailBodyText.text = body;
        }

        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            CreateStatSlot(null, "\u89e6\u53d1", triggerName);
            CreateStatSlot(null, "\u6b66\u5668\u9700\u6c42", triggerRequirement);
            CreateFusionConditionStatSlots(recipe.conditions);
            CreateStatSlot(null, "\u7ed3\u679c", isRevealed ? resultName : "\u672a\u77e5");
        }

        CreateRecommendationIcon(recipe.triggerWeapon != null ? recipe.triggerWeapon.weaponIcon : null, triggerName, triggerMet);
        CreateFusionConditionIcons(recipe.conditions);
        if (!recipe.codexOnly) CreateRecommendationIcon(result != null ? result.weaponIcon : null, resultName);
        if (codexRecommendedTitleText != null) codexRecommendedTitleText.text = "\u8fdb\u5316\u6761\u4ef6";
        if (codexRecommendedTitleText != null) codexRecommendedTitleText.gameObject.SetActive(true);
    }

    private void ShowFusionDescView(EvolutionRecipeSO recipe)
    {
        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);
        if (recipe == null) return;

        WeaponStatBlock mainWeapon = recipe.MainWeapon;
        WeaponStatBlock result = recipe.ResultWeapon;
        string weaponName = GetWeaponDisplayName(mainWeapon);
        string resultName = result != null ? GetWeaponDisplayName(result) : recipe.DisplayName;
        string stoneName = GetStoneTypeLabel(recipe.requiredStoneType);
        string title = !string.IsNullOrEmpty(recipe.DisplayName) ? recipe.DisplayName : resultName;
        int requiredLevel = Mathf.Max(1, recipe.requiredWeaponLevel);
        bool isRevealed = CheckFusionRevealed(recipe);
        bool weaponMet = HasWeaponReachedLevel(mainWeapon, requiredLevel);

        Sprite icon = recipe.cardIcon != null ? recipe.cardIcon : result != null ? result.weaponIcon : mainWeapon != null ? mainWeapon.weaponIcon : null;
        SetDetailHeroIcon(icon, isRevealed);

        if (weaponStatsName != null)
        {
            weaponStatsName.gameObject.SetActive(true);
            weaponStatsName.text = isRevealed ? title : "\u672a\u77e5\u8fdb\u5316";
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = isRevealed
                ? $"\u914d\u65b9: {weaponName} + {stoneName}\n\u9700\u6c42: {weaponName} Lv.{requiredLevel}"
                : $"\u672a\u77e5\u8fdb\u5316\n{weaponName} Lv.{requiredLevel}";
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = "\u804c\u4e1a\u7279\u5316 | \u5143\u7d20\u8fdb\u5316 | \u56fe\u9274";
        }

        if (codexDetailBodyText != null)
        {
            codexDetailBodyText.text = !isRevealed
                ? "\u8fd9\u662f\u804c\u4e1a\u6216\u5143\u7d20\u7279\u5316\u8fdb\u5316\u7ebf\u7d22\u3002\u4e0a\u65b9\u4fdd\u7559\u8fdb\u5316\u7ed3\u679c\u526a\u5f71\uff0c\u4e0b\u65b9\u6761\u4ef6\u56fe\u6807\u8fbe\u6210\u540e\u4f1a\u4ece\u9ed1\u8272\u53d8\u4eae\u3002"
                : string.IsNullOrEmpty(recipe.description)
                ? $"{weaponName} \u8fbe\u5230 Lv.{requiredLevel} \u540e\uff0c\u4e0e {stoneName} \u5143\u7d20\u7ed3\u5408\uff0c\u8fdb\u5316\u4e3a {resultName}\u3002"
                : recipe.description;
        }

        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            CreateStatSlot(null, "\u4e3b\u6b66\u5668", weaponName);
            CreateStatSlot(null, "\u6b66\u5668\u9700\u6c42", $"Lv.{requiredLevel}");
            CreateStatSlot(null, "\u5143\u7d20", stoneName);
            CreateStatSlot(null, "\u7ed3\u679c", isRevealed ? resultName : "\u672a\u77e5");
        }

        CreateRecommendationIcon(mainWeapon != null ? mainWeapon.weaponIcon : null, weaponName, weaponMet);
        CreateRecommendationIcon(recipe.cardIcon, stoneName, isRevealed, stoneName);
        if (codexRecommendedTitleText != null)
        {
            codexRecommendedTitleText.text = "\u8fdb\u5316\u6761\u4ef6";
            codexRecommendedTitleText.gameObject.SetActive(true);
        }
    }

    private void CreateStatSlot(Sprite icon, string value)
    {
        CreateStatSlot(icon, "", value);
    }

    private void CreateStatSlot(Sprite icon, string label, string value)
    {
        GameObject slotObj = Instantiate(weaponStatItemPrefab, weaponStatsContainer);
        slotObj.SetActive(true);
        CodexStatSlot slot = slotObj.GetComponent<CodexStatSlot>();
        if (slot != null)
        {
            slot.Setup(icon, label, value);
        }
        activeStatSlots.Add(slotObj);
    }

    private string GetWeaponDisplayName(WeaponStatBlock weapon)
    {
        if (weapon == null) return "\u672a\u914d\u7f6e";

        string weaponName = !string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponName : weapon.name;
        return LanguageTable.LocalizeWeaponName(weaponName, LocalizationManager.CurrentLanguage);
    }

    private string BuildFusionConditionLine(FusionCondition[] conditions)
    {
        if (conditions == null || conditions.Length == 0) return "\u65e0\u989d\u5916\u6761\u4ef6";

        List<string> names = new List<string>();
        foreach (FusionCondition condition in conditions)
        {
            if (condition == null) continue;
            names.Add(GetFusionConditionName(condition, true));
        }

        return names.Count > 0 ? string.Join(" + ", names) : "\u65e0\u989d\u5916\u6761\u4ef6";
    }

    private void CreateFusionConditionStatSlots(FusionCondition[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            CreateStatSlot(null, "\u6761\u4ef6", "\u65e0");
            return;
        }

        foreach (FusionCondition condition in conditions)
        {
            if (condition == null) continue;

            string label = "\u6761\u4ef6";
            string value = GetFusionConditionName(condition, false);
            switch (condition.type)
            {
                case ConditionType.Weapon:
                    label = "\u6b66\u5668\u6761\u4ef6";
                    if (condition.requiredWeaponLevel > 0) value += $" Lv.{condition.requiredWeaponLevel}";
                    else value += $" {condition.requiredWeaponStage}";
                    break;
                case ConditionType.Passive:
                    label = "\u9053\u5177\u6761\u4ef6";
                    value += $" Lv.{GetEffectivePassiveRequiredLevel(GetConditionPassive(condition), condition.requiredPassiveLevel)}";
                    break;
                case ConditionType.Talent:
                    label = "\u5929\u8d4b\u6761\u4ef6";
                    break;
            }

            CreateStatSlot(null, label, value);
        }
    }

    private void CreateFusionConditionIcons(FusionCondition[] conditions)
    {
        if (conditions == null) return;

        foreach (FusionCondition condition in conditions)
        {
            if (condition == null) continue;
            CreateRecommendationIcon(GetFusionConditionIcon(condition), GetFusionConditionName(condition, false), IsFusionConditionMet(condition));
        }
    }

    private Sprite GetFusionConditionIcon(FusionCondition condition)
    {
        if (condition == null) return null;
        if (condition.type == ConditionType.Weapon)
        {
            return condition.requiredWeapon != null ? condition.requiredWeapon.weaponIcon : null;
        }

        if (condition.type == ConditionType.Passive)
        {
            PassiveItemData passive = GetConditionPassive(condition);
            return passive != null ? passive.icon : null;
        }

        return null;
    }

    private string GetFusionConditionName(FusionCondition condition, bool includeLevel)
    {
        if (condition == null) return "\u672a\u914d\u7f6e";

        switch (condition.type)
        {
            case ConditionType.Weapon:
                return GetWeaponDisplayName(condition.requiredWeapon);
            case ConditionType.Passive:
                PassiveItemData passive = GetConditionPassive(condition);
                string passiveName = passive != null ? passive.itemName : condition.requiredPassiveId;
                if (string.IsNullOrEmpty(passiveName)) passiveName = "\u672a\u914d\u7f6e\u9053\u5177";
                return includeLevel ? $"{passiveName} Lv.{GetEffectivePassiveRequiredLevel(passive, condition.requiredPassiveLevel)}" : passiveName;
            case ConditionType.Talent:
                return string.IsNullOrEmpty(condition.requiredTalentId) ? "\u672a\u914d\u7f6e\u5929\u8d4b" : condition.requiredTalentId;
            default:
                return "\u672a\u914d\u7f6e";
        }
    }

    private PassiveItemData GetFirstPassiveCondition(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null || recipe.conditions == null) return null;
        foreach (FusionCondition condition in recipe.conditions)
        {
            if (condition == null || condition.type != ConditionType.Passive) continue;
            PassiveItemData passive = GetConditionPassive(condition);
            if (passive != null) return passive;
        }

        return null;
    }

    private PassiveItemData GetConditionPassive(FusionCondition condition)
    {
        if (condition == null) return null;
        if (condition.requiredPassiveItem != null) return condition.requiredPassiveItem;
        return FindPassiveByIdentifier(condition.requiredPassiveId);
    }

    private PassiveItemData FindPassiveByIdentifier(string passiveId)
    {
        if (string.IsNullOrEmpty(passiveId) || allPassiveItems == null) return null;

        foreach (PassiveItemData passive in allPassiveItems)
        {
            if (passive == null) continue;
            if (string.Equals(passive.name, passiveId, System.StringComparison.OrdinalIgnoreCase)) return passive;
            if (string.Equals(passive.itemName, passiveId, System.StringComparison.OrdinalIgnoreCase)) return passive;
            if (string.Equals(passive.statType.ToString(), passiveId, System.StringComparison.OrdinalIgnoreCase)) return passive;
        }

        return null;
    }

    private string GetStoneTypeLabel(EnergyStoneEffectType type)
    {
        switch (type)
        {
            case EnergyStoneEffectType.ApplyBurn: return "\u706b";
            case EnergyStoneEffectType.ApplySlow: return "\u51b0";
            case EnergyStoneEffectType.ApplyStun: return "\u5730";
            case EnergyStoneEffectType.ApplyChain: return "\u96f7";
            case EnergyStoneEffectType.ApplyKnockback: return "\u98ce";
            case EnergyStoneEffectType.ApplyWeaken: return "\u865a\u5f31";
            case EnergyStoneEffectType.ApplyCorrode: return "\u6bd2";
            case EnergyStoneEffectType.ApplyMagnet: return "\u78c1";
            default: return type.ToString();
        }
    }

    private string BuildWeaponCodexSummary(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";

        string cadence = weapon.baseFireRate > 0f
            ? $"\u6bcf {1f / weapon.baseFireRate:F1}s \u89e6\u53d1"
            : "\u88ab\u52a8\u89e6\u53d1";
        string status = IsFutureCodexWeapon(weapon) ? "\u672a\u6765\u89e3\u9501\n" : "";
        return $"{status}{cadence}";
    }

    private string BuildWeaponDetailBody(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";

        List<string> lines = new List<string>();
        if (IsFutureCodexWeapon(weapon))
        {
            lines.Add("\u72b6\u6001: \u672a\u6765\u7248\u672c\u6b66\u5668\uff0c\u6682\u4e0d\u8fdb\u5165 Demo \u5c40\u5185\u5361\u6c60\u3002");
        }
        lines.Add(weapon.baseFireRate > 0f
            ? $"\u8282\u594f: \u6bcf {1f / weapon.baseFireRate:F1}s \u81ea\u52a8\u89e6\u53d1\uff0c\u9002\u5408\u548c\u51b7\u5374\u3001\u6570\u91cf\u3001\u8303\u56f4\u7c7b\u88ab\u52a8\u642d\u914d\u3002"
            : "\u8282\u594f: \u88ab\u52a8\u5e38\u9a7b\u578b\uff0c\u4e3b\u8981\u9760\u8d70\u4f4d\u548c\u8303\u56f4\u63a7\u5236\u63d0\u5347\u4f53\u9a8c\u3002");

        string fusionHint = GetWeaponFusionHintLine(weapon);
        if (!string.IsNullOrEmpty(fusionHint)) lines.Add(fusionHint);

        if (IsFutureCodexWeapon(weapon) && !string.IsNullOrEmpty(weapon.ultimateDescription))
        {
            lines.Add($"\u6982\u5ff5\u73a9\u6cd5: {weapon.ultimateDescription}");
        }

        if (weapon.projectileCount > 1) lines.Add($"\u6210\u957f\u770b\u70b9: \u521d\u59cb\u5f39\u6570 {weapon.projectileCount}\uff0c\u589e\u52a0\u6570\u91cf\u540e\u53d8\u5316\u4f1a\u975e\u5e38\u76f4\u89c2\u3002");
        if (weapon.baseChainCount > 0) lines.Add($"\u6210\u957f\u770b\u70b9: \u57fa\u7840\u8fde\u9501 {weapon.baseChainCount}\uff0c\u9002\u5408\u7528\u6765\u5904\u7406\u5bc6\u96c6\u602a\u6f6e\u3002");
        if (weapon.baseOrbitalCount > 0) lines.Add($"\u6210\u957f\u770b\u70b9: \u57fa\u7840\u73af\u7ed5 {weapon.baseOrbitalCount}\uff0c\u8d34\u8eab\u751f\u5b58\u611f\u5f3a\u3002");
        if (weapon.baseSlowPercentage > 0f || weapon.baseFreezeChance > 0f) lines.Add("\u989d\u5916\u4ef7\u503c: \u63a7\u573a\u80fd\u51cf\u5c11\u538b\u529b\uff0c\u4e0d\u53ea\u662f\u6e05\u602a\u4f24\u5bb3\u3002");
        if (weapon.ignitionChance > 0f || weapon.nativeBurn) lines.Add("\u5143\u7d20: \u706b\u7cfb\uff0c\u53ef\u4ee5\u8fdb\u5165\u5143\u7d20\u5171\u9e23 Build\u3002");
        if (weapon.nativeLightningChance > 0f || weapon.nativeElectrify || weapon.weaponID == "LightningStrike") lines.Add("\u5143\u7d20: \u96f7\u7cfb\uff0c\u5728\u9ad8\u5bc6\u5ea6\u6218\u6597\u4e2d\u6536\u76ca\u66f4\u9ad8\u3002");
        if (weapon.evolutionTarget != null) lines.Add($"\u8fdb\u5316: \u53ef\u8fdb\u5316\u4e3a {weapon.evolutionTarget.weaponName}\u3002");

        return string.Join("\n", lines);
    }

    private string GetWeaponRoleLabel(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";

        string id = weapon.weaponID ?? "";
        if (id == "LightningStrike") return "\u843d\u96f7\u7206\u53d1";
        if (id.Contains("ChainLightning")) return "\u8fde\u9501\u6e05\u602a";

        return GetWeaponRoleLabel(weapon.behavior);
    }

    private string GetWeaponRoleLabel(WeaponBehaviorType behavior)
    {
        switch (behavior)
        {
            case WeaponBehaviorType.MeleeAOE: return "\u8fd1\u8eab\u65a9\u51fb";
            case WeaponBehaviorType.Standard: return "\u76f4\u7ebf\u5f39\u9053";
            case WeaponBehaviorType.Pierce: return "\u7a7f\u900f\u5f39\u9053";
            case WeaponBehaviorType.ParabolicAOE: return "\u629b\u5c04\u7206\u70b8";
            case WeaponBehaviorType.Chain: return "\u8fde\u9501\u6e05\u602a";
            case WeaponBehaviorType.Orbital: return "\u73af\u7ed5\u62a4\u8eab";
            case WeaponBehaviorType.PersistentAOE: return "\u6301\u7eed\u533a\u57df";
            case WeaponBehaviorType.SummonDrone: return "\u53ec\u5524\u7269";
            case WeaponBehaviorType.Beam: return "\u6301\u7eed\u5149\u675f";
            case WeaponBehaviorType.Funnel: return "\u6d6e\u6e38\u70ae";
            case WeaponBehaviorType.SuperMech: return "\u7ec8\u5c40\u53ec\u5524";
            case WeaponBehaviorType.Landmine: return "\u5e03\u7f6e\u9677\u9631";
            case WeaponBehaviorType.Boomerang: return "\u5f80\u8fd4\u6295\u5c04";
            case WeaponBehaviorType.Aura: return "\u8f85\u52a9\u5149\u73af";
            case WeaponBehaviorType.CreateAndForget: return "\u72ec\u7acb\u9020\u7269";
            case WeaponBehaviorType.FlyingDagger: return "\u8ffd\u8e2a\u98de\u5200";
            case WeaponBehaviorType.FrostNova: return "\u63a7\u573a\u7206\u53d1";
            case WeaponBehaviorType.LaserCore: return "\u805a\u7126\u707c\u70e7";
            default: return behavior.ToString();
        }
    }

    private WeaponBuildFamily GetWeaponBuildFamily(WeaponStatBlock weapon)
    {
        if (weapon == null) return WeaponBuildFamily.Hybrid;

        HashSet<WeaponBuildTag> tags = WeaponBuildTagUtility.GetTags(weapon);
        if (tags.Contains(WeaponBuildTag.Slash)) return WeaponBuildFamily.Slash;
        if (tags.Contains(WeaponBuildTag.Mechanical) || tags.Contains(WeaponBuildTag.Deployable)) return WeaponBuildFamily.Engineering;
        if (tags.Contains(WeaponBuildTag.Spell) || WeaponBuildTagUtility.IsElementalWeapon(weapon)) return WeaponBuildFamily.Spell;
        if (tags.Contains(WeaponBuildTag.Guardian) || tags.Contains(WeaponBuildTag.Aura)) return WeaponBuildFamily.Guardian;

        return WeaponBuildFamily.Hybrid;
    }

    private string GetWeaponFamilyLabel(WeaponStatBlock weapon)
    {
        switch (GetWeaponBuildFamily(weapon))
        {
            case WeaponBuildFamily.Slash: return "\u65a9\u51fb\u6d41";
            case WeaponBuildFamily.Spell: return "\u6cd5\u672f\u6d41";
            case WeaponBuildFamily.Engineering: return "\u673a\u68b0\u5de5\u7a0b\u6d41";
            case WeaponBuildFamily.Guardian: return "\u5b88\u62a4\u73af\u7ed5\u6d41";
            default: return "\u6df7\u5408\u6d41";
        }
    }

    private string GetWeaponBuildAdvice(WeaponStatBlock weapon)
    {
        switch (GetWeaponBuildFamily(weapon))
        {
            case WeaponBuildFamily.Slash:
                return "\u4f18\u5148\u627e\u65a9\u51fb\u6280\u80fd\u3001\u5200\u5149\u6570\u91cf\u548c\u5251\u5723\u4e4b\u9b42\uff0c\u8ba9\u653b\u51fb\u5f62\u6001\u53d1\u751f\u76f4\u89c2\u53d8\u5316\u3002";
            case WeaponBuildFamily.Spell:
                return "\u4f18\u5148\u627e\u5176\u4ed6\u5143\u7d20\u6cd5\u672f\u548c\u5143\u7d20\u5171\u9e23\uff0c\u8ba9\u89e6\u53d1\u7387\u3001\u8303\u56f4\u548c\u989d\u5916\u8109\u51b2\u4e00\u8d77\u6210\u957f\u3002";
            case WeaponBuildFamily.Engineering:
                return "\u4f18\u5148\u627e\u5730\u96f7\u3001\u70ae\u5854\u3001\u5149\u675f\u548c\u673a\u68b0\u5171\u9e23\uff0c\u628a\u5355\u4e2a\u88c5\u7f6e\u53d8\u6210\u9635\u5730\u538b\u529b\u3002";
            case WeaponBuildFamily.Guardian:
                return "\u4f18\u5148\u627e\u73af\u7ed5\u6570\u91cf\u3001\u6301\u7eed\u65f6\u95f4\u548c\u751f\u5b58\u88ab\u52a8\uff0c\u628a\u8d34\u8eab\u9632\u7ebf\u505a\u539a\u3002";
            default:
                return "\u4f18\u5148\u627e\u80fd\u89e6\u53d1\u878d\u5408\u6216\u8865\u8db3\u77ed\u677f\u7684\u642d\u914d\uff0c\u4e0d\u53ea\u662f\u5806\u653b\u51fb\u529b\u3002";
        }
    }

    private string GetWeaponElementLabel(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";

        string family = WeaponBuildTagUtility.GetPrimaryElementFamily(weapon);
        if (!string.IsNullOrEmpty(family)) return GetElementLabelFromFamily(family);

        string id = weapon.weaponID ?? "";
        if (id.Contains("Fire") || id.Contains("Flame") || weapon.ignitionChance > 0f || weapon.nativeBurn) return "\u706b\u7cfb";
        if (id.Contains("Ice") || id.Contains("Frost") || weapon.baseFreezeChance > 0f) return "\u51b0\u7cfb";
        if (id.Contains("Lightning") || id.Contains("Thunder") || weapon.nativeLightningChance > 0f || weapon.nativeElectrify || id == "LightningStrike") return "\u96f7\u7cfb";
        if (id.Contains("Hurricane") || id.Contains("Wind")) return "\u98ce\u7cfb";
        if (id.Contains("Corrode") || weapon.nativeCorrode) return "\u8150\u8680";
        return "";
    }

    private string GetElementLabelFromFamily(string family)
    {
        switch (family)
        {
            case "Fire": return "\u706b\u7cfb";
            case "Ice": return "\u51b0\u7cfb";
            case "Thunder": return "\u96f7\u7cfb";
            case "Wind": return "\u98ce\u7cfb";
            case "Corrode": return "\u8150\u8680";
            default: return "";
        }
    }

    private string GetWeaponFusionHintLine(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";

        List<string> partners = new List<string>();
        AddFusionPartnerNames(weapon, partners, 3);
        if (partners.Count <= 0) return "";

        return $"\u8054\u52a8\u65b9\u5411: \u53ef\u4ee5\u7559\u610f {string.Join("\u3001", partners)} \u7b49\u7ec4\u5408\u3002";
    }

    private bool HasAnyFusionRecommendation(WeaponStatBlock weapon)
    {
        List<string> names = new List<string>();
        AddFusionPartnerNames(weapon, names, 1);
        return names.Count > 0;
    }

    private void AddFusionPartnerNames(WeaponStatBlock weapon, List<string> names, int maxCount)
    {
        if (weapon == null || names == null || names.Count >= maxCount) return;

        if (allComboUltimates != null)
        {
            foreach (SO_ComboUltimate combo in allComboUltimates)
            {
                if (combo == null) continue;

                WeaponStatBlock partner = null;
                if (IsSameWeapon(combo.weaponA, weapon)) partner = combo.weaponB;
                else if (IsSameWeapon(combo.weaponB, weapon)) partner = combo.weaponA;

                AddFusionPartnerName(partner, names, maxCount);
                if (names.Count >= maxCount) return;
            }
        }

        if (allFusionRecipes != null)
        {
            foreach (FusionRecipeSO recipe in allFusionRecipes)
            {
                if (recipe == null) continue;

                WeaponStatBlock partner = null;
                if (IsSameWeapon(recipe.weaponA, weapon)) partner = recipe.weaponB;
                else if (IsSameWeapon(recipe.weaponB, weapon)) partner = recipe.weaponA;

                AddFusionPartnerName(partner, names, maxCount);
                if (names.Count >= maxCount) return;
            }
        }

        if (allWeaponFusionRecipes == null) return;
        foreach (WeaponFusionRecipeSO recipe in allWeaponFusionRecipes)
        {
            if (recipe == null) continue;

            if (IsSameWeapon(recipe.triggerWeapon, weapon) && recipe.conditions != null)
            {
                foreach (FusionCondition condition in recipe.conditions)
                {
                    AddFusionPartnerName(condition != null ? condition.requiredWeapon : null, names, maxCount);
                    if (names.Count >= maxCount) return;
                }
            }
            else if (recipe.conditions != null)
            {
                foreach (FusionCondition condition in recipe.conditions)
                {
                    if (condition == null || !IsSameWeapon(condition.requiredWeapon, weapon)) continue;
                    AddFusionPartnerName(recipe.triggerWeapon, names, maxCount);
                    if (names.Count >= maxCount) return;
                }
            }
        }
    }

    private void AddFusionPartnerName(WeaponStatBlock partner, List<string> names, int maxCount)
    {
        if (partner == null || names == null || names.Count >= maxCount) return;
        if (!DemoContentGate.IsWeaponAllowed(partner)) return;

        string partnerName = GetWeaponDisplayName(partner);
        if (string.IsNullOrEmpty(partnerName) || names.Contains(partnerName)) return;
        names.Add(partnerName);
    }

    private bool IsWeaponBuildPartnerRecommended(WeaponStatBlock weapon, WeaponStatBlock candidate)
    {
        if (weapon == null || candidate == null) return false;

        WeaponBuildFamily family = GetWeaponBuildFamily(weapon);
        WeaponBuildFamily candidateFamily = GetWeaponBuildFamily(candidate);
        if (family != candidateFamily)
        {
            return false;
        }

        if (family == WeaponBuildFamily.Spell)
        {
            string element = GetWeaponElementLabel(weapon);
            string candidateElement = GetWeaponElementLabel(candidate);
            return string.IsNullOrEmpty(element)
                || string.IsNullOrEmpty(candidateElement)
                || element != candidateElement;
        }

        return family == WeaponBuildFamily.Engineering || family == WeaponBuildFamily.Guardian;
    }

    private bool IsSameWeapon(WeaponStatBlock a, WeaponStatBlock b)
    {
        if (a == null || b == null) return false;
        if (a == b) return true;

        if (!string.IsNullOrEmpty(a.weaponID) && !string.IsNullOrEmpty(b.weaponID))
        {
            return a.weaponID == b.weaponID;
        }

        return a.name == b.name;
    }

    private string GetWeaponTagLine(WeaponStatBlock weapon)
    {
        List<string> tags = new List<string>();
        if (IsFutureCodexWeapon(weapon)) tags.Add("\u672a\u6765\u89e3\u9501");
        tags.Add(GetWeaponFamilyLabel(weapon));
        tags.Add(GetWeaponRoleLabel(weapon));

        string elementLabel = GetWeaponElementLabel(weapon);
        if (!string.IsNullOrEmpty(elementLabel)) tags.Add(elementLabel);
        if (HasAnyFusionRecommendation(weapon)) tags.Add("\u53ef\u8054\u52a8/\u878d\u5408");
        if (weapon.evolutionTarget != null) tags.Add($"\u8fdb\u5316: {weapon.evolutionTarget.weaponName}");

        return tags.Count > 0
            ? string.Join("  /  ", tags)
            : "\u6df7\u5408\u6d41  /  \u7a33\u5b9a\u57fa\u7840\u8f93\u51fa";
    }

    private void ClearStatSlots()
    {
        foreach (var slot in activeStatSlots)
        {
            if (slot != null) Destroy(slot);
        }
        activeStatSlots.Clear();

        if (weaponStatsContainer == null) return;

        foreach (Transform child in weaponStatsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void ClearRecommendations()
    {
        if (codexRecommendedTitleText != null) codexRecommendedTitleText.gameObject.SetActive(false);
        if (codexRecommendationContainer == null) return;

        foreach (Transform child in codexRecommendationContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void PopulateWeaponRecommendations(WeaponStatBlock weapon)
    {
        if (weapon == null || codexRecommendationContainer == null) return;

        HashSet<string> used = new HashSet<string>();
        int count = 0;
        count = AddFusionRecommendations(weapon, used, count, 5);
        count = AddCorePassiveRecommendations(weapon, used, count, 5);
        count = AddSameFamilyWeaponRecommendations(weapon, used, count, 5);

        if (codexRecommendedTitleText != null)
        {
            codexRecommendedTitleText.text = "\u63a8\u8350\u8054\u52a8";
            codexRecommendedTitleText.gameObject.SetActive(count > 0);
        }
    }

    private void PopulatePassiveRecommendations(PassiveItemData passive)
    {
        if (passive == null || codexRecommendationContainer == null) return;

        HashSet<string> used = new HashSet<string>();
        int count = 0;
        if (passive.requiredWeapon != null && DemoContentGate.IsWeaponAllowed(passive.requiredWeapon))
        {
            if (AddRecommendationIconIfNew(used, passive.requiredWeapon, passive.requiredWeapon.weaponIcon, passive.requiredWeapon.weaponName))
            {
                count++;
            }
        }

        if (allWeaponTrees != null)
        {
            foreach (WeaponSkillTree tree in allWeaponTrees)
            {
                if (tree == null || tree.associatedWeapon == null) continue;
                if (!IsPassiveRecommendedForWeapon(passive, tree.associatedWeapon)) continue;
                if (AddRecommendationIconIfNew(used, tree.associatedWeapon, tree.associatedWeapon.weaponIcon, tree.associatedWeapon.weaponName))
                {
                    count++;
                }
                if (count >= 5) break;
            }
        }

        if (codexRecommendedTitleText != null)
        {
            codexRecommendedTitleText.text = "\u63a8\u8350\u9002\u914d";
            codexRecommendedTitleText.gameObject.SetActive(count > 0);
        }
    }

    private int AddFusionRecommendations(WeaponStatBlock weapon, HashSet<string> used, int count, int maxCount)
    {
        if (weapon == null || count >= maxCount) return count;

        if (allComboUltimates != null)
        {
            foreach (SO_ComboUltimate combo in allComboUltimates)
            {
                if (combo == null) continue;

                WeaponStatBlock partner = null;
                if (IsSameWeapon(combo.weaponA, weapon)) partner = combo.weaponB;
                else if (IsSameWeapon(combo.weaponB, weapon)) partner = combo.weaponA;

                if (partner == null || !DemoContentGate.IsWeaponAllowed(partner)) continue;
                if (AddRecommendationIconIfNew(used, partner, partner.weaponIcon, GetWeaponDisplayName(partner))) count++;
                if (count >= maxCount) return count;
            }
        }

        if (allFusionRecipes != null)
        {
            foreach (FusionRecipeSO recipe in allFusionRecipes)
            {
                if (recipe == null) continue;

                WeaponStatBlock partner = null;
                if (IsSameWeapon(recipe.weaponA, weapon)) partner = recipe.weaponB;
                else if (IsSameWeapon(recipe.weaponB, weapon)) partner = recipe.weaponA;

                if (partner == null || !DemoContentGate.IsWeaponAllowed(partner)) continue;
                if (AddRecommendationIconIfNew(used, partner, partner.weaponIcon, GetWeaponDisplayName(partner))) count++;
                if (count >= maxCount) return count;
            }
        }

        if (allWeaponFusionRecipes != null)
        {
            foreach (WeaponFusionRecipeSO recipe in allWeaponFusionRecipes)
            {
                if (recipe == null) continue;

                if (IsSameWeapon(recipe.triggerWeapon, weapon) && recipe.conditions != null)
                {
                    foreach (FusionCondition condition in recipe.conditions)
                    {
                        if (condition == null || condition.requiredWeapon == null) continue;
                        if (!DemoContentGate.IsWeaponAllowed(condition.requiredWeapon)) continue;
                        if (AddRecommendationIconIfNew(used, condition.requiredWeapon, condition.requiredWeapon.weaponIcon, GetWeaponDisplayName(condition.requiredWeapon))) count++;
                        if (count >= maxCount) return count;
                    }
                }
                else if (recipe.conditions != null)
                {
                    foreach (FusionCondition condition in recipe.conditions)
                    {
                        if (condition == null || !IsSameWeapon(condition.requiredWeapon, weapon) || recipe.triggerWeapon == null) continue;
                        if (!DemoContentGate.IsWeaponAllowed(recipe.triggerWeapon)) continue;
                        if (AddRecommendationIconIfNew(used, recipe.triggerWeapon, recipe.triggerWeapon.weaponIcon, GetWeaponDisplayName(recipe.triggerWeapon))) count++;
                        if (count >= maxCount) return count;
                    }
                }
            }
        }

        return count;
    }

    private int AddCorePassiveRecommendations(WeaponStatBlock weapon, HashSet<string> used, int count, int maxCount)
    {
        if (weapon == null || allPassiveItems == null || count >= maxCount) return count;

        switch (GetWeaponBuildFamily(weapon))
        {
            case WeaponBuildFamily.Slash:
                count = AddPassiveRecommendationByType(UpgradeType.SwordmasterSoul, used, count, maxCount);
                count = AddPassiveRecommendationByType(UpgradeType.SlashCount, used, count, maxCount);
                break;
            case WeaponBuildFamily.Spell:
                count = AddPassiveRecommendationByType(UpgradeType.ElementalResonance, used, count, maxCount);
                count = AddPassiveRecommendationByType(UpgradeType.ArcaneMastery, used, count, maxCount);
                break;
            case WeaponBuildFamily.Engineering:
                count = AddPassiveRecommendationByType(UpgradeType.MechanicalResonance, used, count, maxCount);
                count = AddPassiveRecommendationByType(UpgradeType.WeaponDuration, used, count, maxCount);
                break;
            case WeaponBuildFamily.Guardian:
                count = AddPassiveRecommendationByType(UpgradeType.OrbitalCount, used, count, maxCount);
                count = AddPassiveRecommendationByType(UpgradeType.WeaponDuration, used, count, maxCount);
                count = AddPassiveRecommendationByType(UpgradeType.KillHeal, used, count, maxCount);
                break;
        }

        foreach (PassiveItemData passive in allPassiveItems)
        {
            if (count >= maxCount) break;
            if (passive == null || !IsPassiveRecommendedForWeapon(passive, weapon)) continue;
            if (AddRecommendationIconIfNew(used, passive, passive.icon, passive.itemName)) count++;
        }

        return count;
    }

    private int AddPassiveRecommendationByType(UpgradeType statType, HashSet<string> used, int count, int maxCount)
    {
        if (allPassiveItems == null || count >= maxCount) return count;

        foreach (PassiveItemData passive in allPassiveItems)
        {
            if (passive == null || passive.statType != statType) continue;
            if (!DemoContentGate.IsPassiveAllowed(passive)) continue;
            if (AddRecommendationIconIfNew(used, passive, passive.icon, passive.itemName)) count++;
            break;
        }

        return count;
    }

    private int AddSameFamilyWeaponRecommendations(WeaponStatBlock weapon, HashSet<string> used, int count, int maxCount)
    {
        if (weapon == null || allWeaponTrees == null || count >= maxCount) return count;

        foreach (WeaponSkillTree tree in allWeaponTrees)
        {
            if (tree == null || tree.associatedWeapon == null) continue;
            WeaponStatBlock candidate = tree.associatedWeapon;
            if (IsSameWeapon(candidate, weapon) || !DemoContentGate.IsWeaponAllowed(candidate)) continue;
            if (!IsWeaponBuildPartnerRecommended(weapon, candidate)) continue;

            if (AddRecommendationIconIfNew(used, candidate, candidate.weaponIcon, GetWeaponDisplayName(candidate))) count++;
            if (count >= maxCount) break;
        }

        return count;
    }

    private bool AddRecommendationIconIfNew(HashSet<string> used, WeaponStatBlock weapon, Sprite sprite, string label)
    {
        if (weapon == null) return false;
        string key = "W:" + (!string.IsNullOrEmpty(weapon.weaponID) ? weapon.weaponID : weapon.name);
        if (!used.Add(key)) return false;

        CreateRecommendationIcon(sprite, label);
        return true;
    }

    private bool AddRecommendationIconIfNew(HashSet<string> used, PassiveItemData passive, Sprite sprite, string label)
    {
        if (passive == null) return false;
        string key = "P:" + (!string.IsNullOrEmpty(passive.itemName) ? passive.itemName : passive.name);
        if (!used.Add(key)) return false;

        CreateRecommendationIcon(sprite, label);
        return true;
    }

    private void CreateRecommendationIcon(Sprite sprite, string label, bool isBright = true, string fallbackText = null)
    {
        if (codexRecommendationContainer == null) return;

        GameObject frame = CreatePanel("Runtime_Recommendation", codexRecommendationContainer, new Color(0.30f, 0.16f, 0.08f, 0.90f));
        if (!string.IsNullOrEmpty(label)) frame.name = $"Runtime_Recommendation_{label}";
        ApplyDemoSprite(frame.GetComponent<Image>(), "codex_recommend_frame", Color.white);
        Image frameImage = frame.GetComponent<Image>();
        if (frameImage != null && !isBright)
        {
            frameImage.color = new Color(0.45f, 0.39f, 0.31f, 1f);
        }
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(60f, 60f);
        LayoutElement layout = frame.AddComponent<LayoutElement>();
        layout.preferredWidth = 60f;
        layout.preferredHeight = 60f;

        GameObject iconObj = CreateUIObject("Icon", frame.transform);
        StretchToParent(iconObj.GetComponent<RectTransform>(), 7f, 7f, 7f, 7f);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
        icon.color = isBright ? Color.white : GetSilhouetteColor();
        icon.raycastTarget = false;

        if (sprite == null && !string.IsNullOrEmpty(fallbackText))
        {
            string shortText = fallbackText.Length > 1 ? fallbackText.Substring(0, 1) : fallbackText;
            TextMeshProUGUI fallback = CreateText("Fallback", frame.transform, shortText, 26f, FontStyles.Bold,
                isBright ? new Color(1f, 0.86f, 0.42f, 1f) : new Color(0.05f, 0.04f, 0.035f, 0.92f),
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, -12f));
            ConfigureTextFit(fallback, 18f, 26f, 1);
        }
    }

    private bool IsPassiveRecommendedForWeapon(PassiveItemData passive, WeaponStatBlock weapon)
    {
        if (passive == null || weapon == null) return false;
        if (!DemoContentGate.IsPassiveAllowed(passive)) return false;
        if (IsSameWeapon(passive.requiredWeapon, weapon)) return true;

        WeaponBuildFamily family = GetWeaponBuildFamily(weapon);
        string element = GetWeaponElementLabel(weapon);

        switch (passive.statType)
        {
            case UpgradeType.WeaponDamage:
            case UpgradeType.WeaponFireRate:
            case UpgradeType.Luck:
                return false;
            case UpgradeType.AddProjectile:
                return family == WeaponBuildFamily.Spell
                    && (weapon.behavior == WeaponBehaviorType.Standard
                    || weapon.behavior == WeaponBehaviorType.Pierce
                    || weapon.behavior == WeaponBehaviorType.FlyingDagger
                    || weapon.projectileCount > 1);
            case UpgradeType.AoeRadius:
            case UpgradeType.AoeDamage:
                return (family == WeaponBuildFamily.Spell || family == WeaponBuildFamily.Engineering || family == WeaponBuildFamily.Guardian)
                    && (weapon.baseAoeRadius > 0f
                    || weapon.behavior == WeaponBehaviorType.ParabolicAOE
                    || weapon.behavior == WeaponBehaviorType.PersistentAOE
                    || weapon.behavior == WeaponBehaviorType.FrostNova);
            case UpgradeType.PierceCount:
                return family == WeaponBuildFamily.Spell
                    && (weapon.behavior == WeaponBehaviorType.Pierce || weapon.basePierceCount > 0);
            case UpgradeType.SlashCount:
            case UpgradeType.SwordmasterSoul:
                return family == WeaponBuildFamily.Slash;
            case UpgradeType.OrbitalCount:
            case UpgradeType.WeaponDuration:
                return family == WeaponBuildFamily.Guardian
                    || weapon.behavior == WeaponBehaviorType.Orbital
                    || weapon.behavior == WeaponBehaviorType.PersistentAOE
                    || weapon.behavior == WeaponBehaviorType.Aura
                    || weapon.behavior == WeaponBehaviorType.Landmine
                    || weapon.behavior == WeaponBehaviorType.Beam
                    || weapon.behavior == WeaponBehaviorType.LaserCore;
            case UpgradeType.ArcaneMastery:
                return family == WeaponBuildFamily.Spell;
            case UpgradeType.ElementalResonance:
                return family == WeaponBuildFamily.Spell && !string.IsNullOrEmpty(element);
            case UpgradeType.MechanicalResonance:
                return family == WeaponBuildFamily.Engineering;
            case UpgradeType.GlobalFreezeChance:
                return element == "\u51b0\u7cfb";
            case UpgradeType.ThunderWill:
                return element == "\u96f7\u7cfb";
            case UpgradeType.FlameTrail:
                return element == "\u706b\u7cfb";
            case UpgradeType.MaxHealth:
            case UpgradeType.Armor:
            case UpgradeType.KillHeal:
            case UpgradeType.LifeStealPassive:
            case UpgradeType.ThornsDamage:
                return family == WeaponBuildFamily.Guardian || family == WeaponBuildFamily.Slash;
            default:
                return false;
        }
    }

    private void ShowPassiveDescView(PassiveItemData passive)
    {
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStylePassiveView(passive, true);
            return;
        }

        HideAllViews();
        if (passiveDescViewRoot) passiveDescViewRoot.SetActive(true);
        if (passive == null) return;

        if (passiveDescIcon != null)
        {
            passiveDescIcon.gameObject.SetActive(true);
            passiveDescIcon.sprite = passive.icon;
            passiveDescIcon.enabled = passive.icon != null;
        }

        if (passiveDescName != null)
        {
            passiveDescName.gameObject.SetActive(true);
            passiveDescName.text = passive.itemName;
        }

        if (codexHeroSummaryText != null)
        {
            codexHeroSummaryText.text = BuildPassiveHeroSummary(passive);
        }

        if (passiveDescText != null)
        {
            passiveDescText.text = BuildPassiveCodexDescription(passive);
        }

        if (codexTagsText != null)
        {
            codexTagsText.text = BuildPassiveTagLine(passive);
        }

        if (weaponStatsContainer != null && weaponStatItemPrefab != null)
        {
            CreateStatSlot(null, "\u7c7b\u578b", passive.isTriggerPassive ? "\u89e6\u53d1" : "\u5c5e\u6027");
            CreateStatSlot(null, "\u6bcf\u7ea7", FormatPassiveValue(passive.statType, passive.valuePerLevel));
            int effectiveMaxLevel = passive.EffectiveMaxLevel;
            CreateStatSlot(null, "\u6ee1\u7ea7", FormatPassiveValue(passive.statType, passive.valuePerLevel * effectiveMaxLevel));
            CreateStatSlot(null, "\u4e0a\u9650", $"Lv.{effectiveMaxLevel}");
        }

        ClearRecommendations();
    }

    private string BuildPassiveCodexDescription(PassiveItemData passive)
    {
        if (passive == null) return "";

        string statName = GetPassiveStatLabel(passive.statType);
        string perLevel = FormatPassiveValue(passive.statType, passive.valuePerLevel);
        int effectiveMaxLevel = passive.EffectiveMaxLevel;
        string maxValue = FormatPassiveValue(passive.statType, passive.valuePerLevel * effectiveMaxLevel);

        List<string> lines = new List<string>();
        if (!string.IsNullOrEmpty(passive.description)) lines.Add(passive.description);
        lines.Add($"\u6210\u957f: \u6bcf\u7ea7 {statName} +{perLevel}, \u6700\u9ad8 {effectiveMaxLevel} \u7ea7\u7d2f\u8ba1 +{maxValue}");

        string level3 = passive.GetMilestoneUnlockDescription(2, 3);
        if (!string.IsNullOrEmpty(level3)) lines.Add($"3\u7ea7\u8282\u70b9: {level3}");

        string maxMilestone = passive.GetMilestoneUnlockDescription(Mathf.Max(0, effectiveMaxLevel - 1), effectiveMaxLevel);
        if (!string.IsNullOrEmpty(maxMilestone)) lines.Add($"\u6ee1\u7ea7\u8282\u70b9: {maxMilestone}");

        if (passive.requiredWeapon != null)
        {
            lines.Add($"Build\u6761\u4ef6: \u62e5\u6709 {passive.requiredWeapon.weaponName} \u540e\u8fdb\u5165\u5361\u6c60");
        }

        if (passive.isTriggerPassive)
        {
            lines.Add("\u7c7b\u578b: \u89e6\u53d1\u578b\u88ab\u52a8\uff0c\u4f1a\u6539\u53d8\u6218\u6597\u8868\u73b0\u800c\u4e0d\u53ea\u662f\u6570\u503c\u3002");
        }

        return string.Join("\n", lines);
    }

    private string BuildPassiveHeroSummary(PassiveItemData passive)
    {
        if (passive == null) return "";

        string typeLabel = passive.isTriggerPassive ? "\u89e6\u53d1\u578b" : "\u6210\u957f\u578b";
        string perLevel = FormatPassiveValue(passive.statType, passive.valuePerLevel);
        return $"\u7c7b\u578b: {typeLabel}\n\u6bcf\u7ea7: {GetPassiveStatLabel(passive.statType)} +{perLevel}";
    }

    private string BuildPassiveTagLine(PassiveItemData passive)
    {
        if (passive == null) return "";

        List<string> tags = new List<string>();
        tags.Add(passive.isTriggerPassive ? "\u89e6\u53d1\u578b" : "\u5c5e\u6027\u578b");
        tags.Add(GetPassiveStatLabel(passive.statType));
        tags.Add($"\u6700\u9ad8 Lv.{passive.EffectiveMaxLevel}");
        if (passive.requiredWeapon != null) tags.Add($"\u9700\u8981 {passive.requiredWeapon.weaponName}");

        return string.Join("  /  ", tags);
    }

    private void UpdateCodexCollectionText()
    {
        if (codexCollectionText == null) return;

        int total = 0;
        int unlocked = 0;

        if (allWeaponTrees != null)
        {
            foreach (WeaponSkillTree tree in allWeaponTrees)
            {
                if (tree == null) continue;
                total++;
                if (CheckWeaponUnlocked(tree)) unlocked++;
            }
        }

        if (allPassiveItems != null)
        {
            foreach (PassiveItemData passive in allPassiveItems)
            {
                if (passive == null) continue;
                total++;
                if (CheckPassiveUnlocked(passive)) unlocked++;
            }
        }

        if (UseVampireStyleCodexLayout)
        {
            HashSet<string> countedFusionResults = new HashSet<string>();

            if (allWeaponFusionRecipes != null)
            {
                foreach (WeaponFusionRecipeSO recipe in allWeaponFusionRecipes)
                {
                    if (recipe == null || ShouldSkipDuplicateFusionResult(countedFusionResults, recipe.resultWeapon)) continue;
                    total++;
                    if (CheckFusionRevealed(recipe)) unlocked++;
                }
            }

            if (allFusionRecipes != null)
            {
                foreach (FusionRecipeSO recipe in allFusionRecipes)
                {
                    if (recipe == null || ShouldSkipDuplicateFusionResult(countedFusionResults, recipe.resultWeapon)) continue;
                    total++;
                    if (CheckFusionRevealed(recipe)) unlocked++;
                }
            }

            if (allEvolutionRecipes != null)
            {
                foreach (EvolutionRecipeSO recipe in allEvolutionRecipes)
                {
                    if (recipe == null || ShouldSkipDuplicateFusionResult(countedFusionResults, recipe.ResultWeapon)) continue;
                    total++;
                    if (CheckFusionRevealed(recipe)) unlocked++;
                }
            }

            codexCollectionText.text = IsEnglishLanguage
                ? $"Collected: {unlocked} of {total}"
                : $"收集: {unlocked} / {total}";
            return;
        }

        codexCollectionText.text = $"\u6536\u96c6 {unlocked}/{total}";
    }

    private string GetPassiveStatLabel(UpgradeType statType)
    {
        switch (statType)
        {
            case UpgradeType.WeaponDamage: return "\u6b66\u5668\u4f24\u5bb3";
            case UpgradeType.AoeDamage: return "\u8303\u56f4\u4f24\u5bb3";
            case UpgradeType.AoeRadius: return "\u8303\u56f4";
            case UpgradeType.WeaponFireRate: return "\u51b7\u5374\u7f29\u77ed";
            case UpgradeType.WeaponProjectileSpeed: return "\u5f39\u9053\u901f\u5ea6";
            case UpgradeType.AddProjectile: return "\u5f39\u6570";
            case UpgradeType.PierceCount: return "\u7a7f\u900f";
            case UpgradeType.SlashCount: return "\u5200\u5149\u6570\u91cf";
            case UpgradeType.OrbitalCount: return "\u73af\u7ed5\u6570\u91cf";
            case UpgradeType.WeaponDuration: return "\u6301\u7eed\u65f6\u95f4";
            case UpgradeType.PickupRadius: return "\u62fe\u53d6\u8303\u56f4";
            case UpgradeType.MoveSpeed: return "\u79fb\u52a8\u901f\u5ea6";
            case UpgradeType.MaxHealth: return "\u751f\u547d\u4e0a\u9650";
            case UpgradeType.Armor: return "\u62a4\u7532";
            case UpgradeType.Luck: return "\u5e78\u8fd0";
            case UpgradeType.ExperienceGain: return "\u7ecf\u9a8c\u83b7\u53d6";
            case UpgradeType.BerserkerHeart: return "\u4f4e\u8840\u589e\u4f24";
            case UpgradeType.FlameTrail: return "\u71c3\u70e7\u8f68\u8ff9";
            case UpgradeType.ThornsDamage: return "\u53d7\u51fb\u53cd\u4f24";
            case UpgradeType.KillHeal: return "\u51fb\u6740\u56de\u8840";
            case UpgradeType.GlobalFreezeChance: return "\u5168\u5c40\u51b0\u51bb";
            case UpgradeType.ThunderWill: return "\u51fb\u6740\u96f7\u51fb";
            case UpgradeType.LifeStealPassive: return "\u5438\u8840";
            case UpgradeType.DashExplosion: return "\u51b2\u523a\u51b2\u51fb";
            case UpgradeType.SwordmasterSoul: return "\u65a9\u51fb\u653b\u901f";
            case UpgradeType.ArcaneMastery: return "\u5965\u672f\u7206\u53d1";
            case UpgradeType.ElementalResonance: return "\u5143\u7d20\u6d41\u6d3e";
            case UpgradeType.MechanicalResonance: return "\u673a\u68b0\u6d41\u6d3e";
            default: return statType.ToString();
        }
    }

    private string FormatPassiveValue(UpgradeType statType, float value)
    {
        bool flatValue = statType == UpgradeType.MaxHealth
                      || statType == UpgradeType.Armor
                      || statType == UpgradeType.PierceCount
                      || statType == UpgradeType.SlashCount
                      || statType == UpgradeType.OrbitalCount
                      || statType == UpgradeType.Revival
                      || statType == UpgradeType.KillHeal
                      || statType == UpgradeType.SwordmasterSoul
                      || statType == UpgradeType.ElementalResonance
                      || statType == UpgradeType.MechanicalResonance
                      || statType == UpgradeType.DashExplosion
                      || statType == UpgradeType.ThunderWill;

        if (flatValue) return value.ToString("0.#");
        return $"{value * 100f:0.#}%";
    }

    private void ShowLockedViewWeapon(WeaponSkillTree tree)
    {
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStyleWeaponView(tree, false);
            return;
        }

        HideAllViews();
        if (lockedViewRoot) lockedViewRoot.SetActive(true);
        if (tree == null) return;

        HideLockConditionIcons();
        bool isFutureWeapon = IsFutureCodexWeapon(tree);

        if (lockConditionText != null)
        {
            lockConditionText.gameObject.SetActive(true);
            lockConditionText.text = isFutureWeapon ? "\u672a\u6765\u89e3\u9501" : tree.lockedDescription;
        }
        if (lockedWeaponIcon != null && tree.associatedWeapon != null)
        {
            lockedWeaponIcon.sprite = tree.associatedWeapon.weaponIcon;
            lockedWeaponIcon.enabled = tree.associatedWeapon.weaponIcon != null;
            lockedWeaponIcon.color = GetSilhouetteColor();
        }

        UpdateProgressBar(tree.unlockStatKey, tree.unlockThreshold);
    }

    private void ShowLockedViewPassive(PassiveItemData passive)
    {
        if (UseVampireStyleCodexLayout)
        {
            ShowVampireStylePassiveView(passive, false);
            return;
        }

        HideAllViews();
        if (lockedViewRoot) lockedViewRoot.SetActive(true);
        if (passive == null) return;

        HideLockConditionIcons();
        if (lockConditionText != null)
        {
            lockConditionText.gameObject.SetActive(true);
            lockConditionText.text = passive.lockedDescription;
        }
        if (lockedWeaponIcon != null)
        {
            lockedWeaponIcon.sprite = passive.icon;
            lockedWeaponIcon.enabled = passive.icon != null;
            lockedWeaponIcon.color = GetSilhouetteColor();
        }

        UpdateProgressBar(passive.unlockStatKey, passive.unlockThreshold);
    }

    private void UpdateProgressBar(string statKey, int threshold)
    {
        if (threshold <= 0)
        {
            if (lockProgressBar != null) lockProgressBar.gameObject.SetActive(false);
            if (lockProgressText != null) lockProgressText.gameObject.SetActive(false);
            return;
        }

        if (lockProgressBar != null) lockProgressBar.gameObject.SetActive(true);
        if (lockProgressText != null) lockProgressText.gameObject.SetActive(true);

        int currentVal = 0;
        int targetVal = threshold;

        if (PlayerProgressManager.Instance != null && !string.IsNullOrEmpty(statKey))
        {
            var stats = PlayerProgressManager.Instance.achievementStats;
            if (stats != null && stats.TryGetValue(statKey, out int value))
            {
                currentVal = value;
            }
        }

        if (lockProgressBar != null)
        {
            lockProgressBar.maxValue = targetVal;
            lockProgressBar.value = currentVal;
        }

        if (lockProgressText != null)
        {
            lockProgressText.text = $"{currentVal} / {targetVal}";
        }
    }

    public void SelectWeaponTree(WeaponSkillTree tree)
    {
        SelectWeaponEntry(tree);
    }

    public void OnNodeSelected(UpgradeNodeUI selectedNodeUI, WeaponUpgradeNode selectedNodeData)
    {
        Debug.Log("[Codex] Legacy upgrade purchase UI is disabled in codex mode.");
    }

    public void RefreshAllNodeStates()
    {
    }
}
