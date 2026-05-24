using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public enum TreasureRewardKind
    {
        WeaponLevels,
        Evolution,
        Gold,
        BaseAttack
    }

    public struct TreasureSlotReward
    {
        public TreasureRewardKind kind;
        public WeaponStatBlock targetWeapon;
        public FusionRecipeSO legacyFusion;
        public WeaponFusionRecipeSO weaponFusion;
        public Sprite icon;
        public Sprite[] reelIcons;
        public string[] reelNames;
        public string resultText;
        public string detailText;
        public string[] reelDetails;
        public SkillTreeNodeData[] awardedNodes;
        public WeaponStatBlock[] reelLevelWeapons;
        public int evolutionReelIndex;
        public int startLevel;
        public int finalLevel;
        public int levelGain;
        public int goldReward;
        public int baseAttackBonusCount;
        public float baseAttackBonus;
        public bool[] reelBaseAttackBonuses;
        public bool jackpot;
        public bool evolved;
        public bool tryEvolutionAfterLevels;
    }

    private struct TreasureNodeCandidate
    {
        public OwnedWeapon target;
        public SkillTreeNodeData node;
    }

    [Header("Upgrade Manager")]
    public GameObject upgradePanel;
    public Transform cardContainer;
    [Tooltip("Runtime tuning value.")]
    public TMPro.TextMeshProUGUI titleText;

    [Header("Upgrade Manager")]
    [Tooltip("Runtime tuning value.")]
    public GameObject confetti2;
    [Tooltip("Runtime tuning value.")]
    public GameObject confetti3;

    [Header("Upgrade Manager")]
    [Tooltip("Runtime tuning value.")]
    [Range(0f, 1f)] public float treasureEvolutionChance = 0.35f;
    [Tooltip("Runtime tuning value.")]
    [Range(0f, 1f)] public float treasureDoubleLevelChance = 0.30f;
    [Tooltip("Runtime tuning value.")]
    [Range(0f, 1f)] public float treasureTripleLevelChance = 0.12f;
    [Tooltip("Runtime tuning value.")]
    public int treasureRerollBaseCost = 60;
    [Tooltip("Runtime tuning value.")]
    public int treasureRerollCostStep = 35;
    [Tooltip("Runtime tuning value.")]
    public float treasureRerollRewardChanceBoost = 0.35f;
    [Tooltip("Base damage multiplier granted by each Nothing pumpkin reel when no stronger treasure reward is available.")]
    public float treasurePumpkinBaseAttackBonus = 0.02f;
    [Tooltip("If enabled, normal level-up choices can offer evolution cards. Keep off for chest-driven evolution.")]
    public bool offerEvolutionCardsOnLevelUp = false;

    [Header("Upgrade Manager")]
    public GameObject commonCardPrefab;
    public GameObject uncommonCardPrefab;
    public GameObject rareCardPrefab;
    public GameObject epicCardPrefab;
    public GameObject unlockCardPrefab;

    [Header("Upgrade Manager")]
    public UpgradeDatabase upgradeDatabase;

    [Header("Upgrade Manager")]
    [Tooltip("Runtime tuning value.")]
    public float delayBetweenCards = 0.2f;
    [Tooltip("Runtime tuning value.")]
    public float levelUpVfxDelay = 1.0f;
    [Tooltip("Runtime tuning value.")]
    public float levelUpSlowMotion = 0.0f;

    [Header("Upgrade Manager")]
    [Tooltip("Runtime tuning value.")]
    public GemEmbedOverlay gemEmbedOverlay;
    // 閻犱焦婢樼紞宥夋偝閳轰緡鍟€鐎圭寮剁€氥垽寮垫径灞剧暠闁瑰灈鍋撻柤鍐测偓鐔肺濋柣鎰嚀瀵兘宕楃捄铏圭Ъ闁告挸绉堕悺鎴犵棯?
    private Dictionary<SkillTreeNodeData, int> ownedUpgrades = new Dictionary<SkillTreeNodeData, int>();

    // 闁活潿鍔嬬花顒傗偓娑櫭崑宥夊嫉椤掍緡鍋у☉鎾规鐢櫣鈧鍩栬ぐ浣圭瑹濞戞碍鐣卞☉鎾愁槷闁叉粓鍨惧鍐ㄧ３缂佺嫏鍕皻濞村吋鍩冮埀?
    private List<SkillTreeNodeData> offeredUpgrades = new List<SkillTreeNodeData>();
    private List<UpgradeCardUI> activeCardUIs = new List<UpgradeCardUI>();

    // === 閻庤绻勯悡鍫曟⒐鐠鸿櫣銈甸弶鈺佲偓鐔煎殝 ===
    private Dictionary<WeaponStatBlock, int> weaponGemCounts = new Dictionary<WeaponStatBlock, int>();
    private List<WeaponStatBlock> pendingUltimateUnlocks = new List<WeaponStatBlock>();
    public const int GEM_SLOT_COUNT = 5;
    [Header("Upgrade Manager")]
    [Tooltip("Runtime tuning value.")]
    public bool enableUltimateUnlockCards = false;

    // === 閻庤绻勯鍫熷緞濮樺灈鍋撴径宀勫厙缂?===
    /// <summary>
    /// 閻庤绻勯鍫ユ焻婢跺﹤骞㈤柛鎾櫃缂嶆垿宕ｉ鐐╁亾婢跺鍋ч柡浣稿簻缁?0 闁哄啫鐖奸埀顒€顦悾顒佺▔閳ь剙顕ｉ悩鍙夊€靛☉鎾崇Т閸櫻囨閵忊剝绶查柨娑樼灱閹撮绱掗鈶╁亾婢跺顏ュ☉鎾愁儎缁旀潙顕ｉ悪鍛
    /// </summary>
    private int remainingTreasurePicks = 0;

    // === 閻熸瑦甯熸竟濠冪▔閹惧磭娼ｉ柟鍨涘亾闁煎疇妫勫畷杈╁寲閼姐倗鍩?===
    /// <summary>
    /// 闁哄牜鍓欓惇顒€顔忛崣澶岃礋婵炶尪宕靛▓鎴犳喆閹烘洖顥忛柟鍨涘亾闁煎疇濮ら悥锝囨嫚閸℃瑯鍎婇梻鍡楁閹酣鏁嶉崼鐔封枙闁告帗婢樺畷閬嶆偋閸パ勫€甸柛鏃傚Т閸欏棝鏁?
    /// </summary>
    private HashSet<string> activeCharacterSkills = new HashSet<string>();
    private int levelUpsSinceCharacterCardOffer = 0;

    private struct OfferWeights
    {
        public float skill;
        public float character;
        public float weapon;
        public float passive;

        public OfferWeights(float skill, float character, float weapon, float passive)
        {
            this.skill = skill;
            this.character = character;
            this.weapon = weapon;
            this.passive = passive;
        }
    }

    /// <summary>
    /// 闁哄牜鍓欓惇顒勫矗椤栨粍鏆忛柣銊ュ椤鎳濋崣澶娢楅柤瀹犳瀹曞崬效閻欏懐绀勯悘鐐╁亾濠㈣埖鐗炶闂佸じ鑳跺▓?layer 2+ 闁煎搫鍊婚崑锝夊礂鐎圭姳绮撻柣銊ュ瀹曢亶鎮ч崶椋庣
    /// </summary>
    private List<SkillTreeNodeData> characterCardPool = new List<SkillTreeNodeData>();
    private readonly List<SkillTreeNodeData> runtimeRoleMilestoneCards = new List<SkillTreeNodeData>();
    private bool currentOfferHasCharacterMilestone;
    private int currentCharacterMilestoneLevel;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (DemoContentGate.DemoModeEnabled && DemoContentGate.DisableUltimateSystemInDemo)
        {
            enableUltimateUnlockCards = false;
            pendingUltimateUnlocks.Clear();
        }
    }

    void Start()
    {
        WeaponFusionManager.EnsureInstance();

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp += HandlePlayerLevelUp;
        }
        if (upgradePanel != null) upgradePanel.SetActive(false);

        // 闁告帗绻傞～鎰板礌閺嶎剦娼￠柤纭呭紦缁楁挾浠﹂悙鎻掑耿婵?
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
        // 闁告凹鍨版慨鈺呭础韫囨洍鏌ら柨娑欒壘閸樻盯骞橀鐔告澒闁告娲ㄦ鍥偋鐟欏嫭娅忛柨娑樿嫰閸熲偓闁哄嫬澧介妵姘跺础閿涘嫬顣?
        StartCoroutine(LevelUpSequence(newLevel));
    }

    /// <summary>
    /// 闁告娲ㄦ鍥规担琛℃煠闁告绻掗埢濂告晬濮橆厼寮柛鏂诲妺缂?+ 闁绘顫夐弲銉╁箻椤撶喐鏉?+ 闁告绱曟晶鏍焻婢跺顏?
    /// </summary>
    private IEnumerator LevelUpSequence(int newLevel)
    {
        // 1. 閺夆晜绋戦崣鍡涘箞閵忕姴袟濞达絾绮ｇ槐娆戞媼閳哄啫顥楅柡浣哥墢濠€鍛導闁垮闄嶉柡鍥ㄦ綑瀹曟洘绋夐弬銈囩
        Time.timeScale = levelUpSlowMotion;

        // 2. 缂佹稑顦欢鐔兼偋鐟欏嫭娅忛柟缁㈠幗閺備線鏁嶉崼婊冣枏闁?unscaledTime 缁绢収鍠曠换姘▔瀹ュ懎缍€闁逛究鍨规慨鈺傛媴濠婂啫顨涢柛婵嗙▌缁?
        yield return new WaitForSecondsRealtime(levelUpVfxDelay);

        // Pause gameplay while upgrade cards are shown.
        Time.timeScale = 0f;
        offeredUpgrades.Clear();
        currentOfferHasCharacterMilestone = false;
        currentCharacterMilestoneLevel = 0;

        // 0. 闁靛棙鍔曢悿鍌炴儗瀹曞洭鍏囩紓浣哄枂閳ь剚鍨崇槐顓㈠礂閸噥姊鹃柡灞诲劜濡叉悂宕ラ敂鑺ョ畳婵繐绠戝▍鎺楁閳ь剛鎲版担瑙勬殘闁稿繈鍎遍妵鍥箯濞戞簽鎺楁煥娴ｇ骞?
        if (enableUltimateUnlockCards && pendingUltimateUnlocks.Count > 0)
        {
            WeaponStatBlock ultimateWeapon = pendingUltimateUnlocks[0];
            pendingUltimateUnlocks.RemoveAt(0);
            SkillTreeNodeData ultimateNode = CreateUltimateUnlockNode(ultimateWeapon);
            offeredUpgrades.Add(ultimateNode);
        }

        // 1. 濞村吋锚閸樻稑螞閳ь剟寮婚妷銊р偓娲触?(閻庤绻勯鍫ユ焻閺勫繒甯嗛梺顐ｈ壘閻栬埖绋夊鍛含閺夆晜鐟╅崳鐑芥晬鐏炶棄纾崇紒鐙欏懏娅曢梻鍫涘灲閳ь剚鑹鹃悥鑸电▔瀹ュ洦绾柟鎭掑劤缁壆鎼鹃崨顕呭妳闁挎稑鐭傚▍搴ㄦ閻愭壆绋戦柣銊ュ椤旀洜鎷嬮垾鍐插笒閻?
        // (濞ｅ洦绻冪€垫梹鎷呴悩鎻掓枾闁哄牆顦卞▓鎴︽焻閺勫繒甯嗛柨娑樿嫰椤┭囧几濠婂棛绠归柡鍕靛灟缂嶆﹢骞嗛崗鍛闁伙絾鐟у▓鎴﹀灳濠婂啫纾崇紒鐙欏懏绾柟鎭掑劦閳ь兛娴囩粔鏉戭潰閿斿厜鍋撳┑鍥ㄧ皻闁?
        if (offerEvolutionCardsOnLevelUp)
        {
        FusionRecipeSO fusionRecipe = null;
        if (WeaponController.Instance != null)
        {
            fusionRecipe = WeaponController.Instance.CheckForAvailableFusion();
        }

        if (fusionRecipe != null)
        {
            SkillTreeNodeData evoNode = CreateFusionNode(fusionRecipe);
            offeredUpgrades.Add(evoNode);
            Debug.Log("[UpgradeManager] log.");
        }

        // --- 闁靛棙鍔掔粭浣规姜閵娾晙澹曠紒顖濆吹缁椽濡撮幋鐐剁┛闁告艾鐗嗗畷鍗炐ч悩鍐测枙闁告绻濋埀顒佹缁额偊鏁嶉崼锛鎼剧€圭姴娈?鐎殿喚濯寸槐?---
        // 濠㈠爢鍕彛闁?闁捐绉撮幃搴ㄥ础閳ュ啿鍤掔紓浣哥箰瀹曚即鎮介妸銈囧晩濞戞挴鍋撳ù婊勭◥缂嶅懐绱旈鍡欑闁告瑯浜〒鍓佹偘閵夈劌鍠曢柛鎾櫃缂?
        WeaponFusionManager fusionManager = WeaponFusionManager.EnsureInstance();
        if (fusionManager != null && WeaponController.Instance != null)
        {
            HashSet<WeaponStatBlock> addedEvolutionResults = new HashSet<WeaponStatBlock>();
            foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
            {
                if (owned?.weaponPartInstance == null) continue;

                List<WeaponFusionRecipeSO> availableFusions = fusionManager.GetAvailableFusions(owned.weaponPartInstance);
                foreach (WeaponFusionRecipeSO recipe in availableFusions)
                {
                    if (recipe == null || recipe.resultWeapon == null || addedEvolutionResults.Contains(recipe.resultWeapon)) continue;
                    offeredUpgrades.Add(CreateFusionNode(recipe));
                    addedEvolutionResults.Add(recipe.resultWeapon);
                    Debug.Log("[UpgradeManager] log.");
                    if (offeredUpgrades.Count >= 2) break;
                }

                if (offeredUpgrades.Count >= 2) break;
            }
        }

        }

        int slotsToFill = 3 - offeredUpgrades.Count;
        if (slotsToFill > 0)
        {
            // 婵繐绠戝▍鎺旀喆閿濆鏁氶弶鐐姂娴滈箖鏁嶅鎵挎帡鏌ㄦ担瑙勭厐婵繐绠戝▍?
            // 閻炴凹鍋勬慨鈺傛姜閵娾晙澹曢柨娑欎亢椤箓宕濋妸鈺€澹曢柛蹇旂仛閳ь兛鑳剁花鍧楁焻濮橆剙骞㈤柕鍡曠閵囧鎸х€ｃ劉鍋撴担鍦ラ柤鐗堫殔閹?
            // 婵繐绠戝▍鎺楀箮閳ь剟鎳楅崐鐔风紦闂侇剚鎼槐鏉款啅閸欏顏熼柡鍫濐槹椤掔喖宕抽妸褎鐣遍柟鍨涘亾闁煎疇濮ら悥鏌ユ嚍閸屾粌浠柨娑樼墛鐎垫粓鏌ч幑鎰唴濞撴碍绻冮鑲╂喆閿濆鏁氶柨?

            // A. 闁兼儳鍢茶ぐ鍥箥閳ь剟寮垫径濠傝闁活潿鍔庡▓鎴濐潰閿曗偓濞呮帞鎲撮敐澶嬫暁闁?
            List<SkillTreeNodeData> validWeapons = GenerateWeaponNodes();

            // B. 闁兼儳鍢茶ぐ鍥箥閳ь剟寮垫径濠傝闁活潿鍔庡▓鎴犳偖椤愩垹袟闁告娲ㄦ?
            List<SkillTreeNodeData> validPassives = new List<SkillTreeNodeData>();

            // 濞?PlayerStats 闁兼儳鍢茶ぐ鍥儑閻旈鏉介柣銊ュ椤箓宕濋妸鈺€澹曢柛蹇涙敱鐎垫棃寮垫径灞叫﹂柟?
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
                    int nodeMaxLevel = GetEffectiveUpgradeNodeMaxLevel(node);
                    bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < nodeMaxLevel;

                    // 闁靛棙鍔曞ù姗€鏌庣壕瀣撴帡鏌ㄦ担鐣岀畺婵犲鍊戦埀顒佸灦濠€顓犳喆閿濆鏁氶柣銊ュ椤箓宕濋妸鈺€澹曢柛蹇氭腹缁楀娼诲☉妯哄汲闁告娲ㄦ鍥础閳╁啰娼?
                    if (!prerequisitesMet || !notMaxed || !IsPassiveNodeUnlocked(node)) continue;

                    // 闁靛棙鍔栬潕濞达絽绉崇粭鍌炴⒔閹邦垳绠栨繝濞垮€戦埀顒佸灥閸戯繝寮?缂佸绉崇粭澶愬触瀹€鈧▓鎴犳偖椤愩垹袟闂侇剚鎸搁崣鍧楀籍鐠佸湱绀夐柛娆樹簻閸樻垹鎷嬬粙鍨殥闁规灚鍎插﹢渚€鏁嶉崼婵嗚闁告娲ㄦ鍥晬婢跺本鐣遍梺顒佹尭閸欏潡宕欓搹鐟扮疀
                    if (currentUniquePassiveCount >= maxPassiveSlots)
                    {
                        // 婵☆偀鍋撻柡灞诲劥缁绘牗绋夐鍫滃闁稿繘鏀卞Σ鎼佸触閿曗偓閸戯紕鎮銈呰礋閻庣鍩栫€垫棃寮垫径娑氱闁告瑯鍨禍鎺楀础閸モ晠鐛撻柨?
                        bool alreadyOwned = ownedUpgrades.ContainsKey(node);
                        if (!alreadyOwned) continue; // 闁哄倷鍗虫禍楣冨礂閾氬倻鐟濋柛鎰Т閸ゎ參鎮?
                    }

                    validPassives.Add(node);
                }
            }

            // C. 闁兼儳鍢茶ぐ鍥箥閳ь剟寮垫径濠傚殥闁规灚鍎插﹢浣割潰閿曗偓濞呮帡鎯冮崟顐㈣闁活潿鍔嶆俊褔鎳楅懞銉у煇闁煎搫鍊婚崑?
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

            // 闁瑰灚鎸风拹锟犲礆濡ゅ嫨鈧?
            var shuffledWeapons = validWeapons.OrderBy(a => Random.value).ToList();
            var shuffledPassives = validPassives.OrderBy(a => Random.value).ToList();
            var shuffledSkills = validWeaponSkills.OrderBy(a => Random.value).ToList();

            Debug.Log("[UpgradeManager] log.");

            // E. Prepare character milestone cards.
            List<SkillTreeNodeData> validCharCards = GetAvailableCharacterCards();
            bool allowCharacterCardsThisLevel = IsCharacterCardMilestoneLevel(newLevel);
            currentOfferHasCharacterMilestone = false;
            currentCharacterMilestoneLevel = 0;

            // 濞村吋锚閸樻盯骞庢繝鍌氱€婚柡鈧姘耿闁挎稑鐗忕花鍧楀礄閸℃ɑ鐒甸柛?闁轰礁绻戝畵搴ㄦ偖鎼淬垹顤侀柨娑橆槹鐢捇宕氶悧鍫熶粯闁告挸绉瑰?
            validCharCards.Sort((a, b) =>
            {
                bool aIsBranch = IsBranchMechanicCard(a);
                bool bIsBranch = IsBranchMechanicCard(b);
                if (aIsBranch && !bIsBranch) return -1;
                if (!aIsBranch && bIsBranch) return 1;
                return Random.value > 0.5f ? 1 : -1; // 闂傚牏鍋涢崹搴ㄥ绩椤栨艾骞㈤梻鍛箲濠р偓闁圭儤甯掔花?
            });

            // 閻熸瑦甯熸竟濠囧础閳╁啰妲ㄦ繛鍡忊偓鍐茬３缂佺嫏鍕粯濠㈣埖鑹鹃崵顓㈡偝?1 鐎?
            OfferWeights offerWeights = GetOfferWeightsForLevel(newLevel);
            Debug.Log("[UpgradeManager] log.");

            bool charCardUsed = false;

            bool forceCharCard = allowCharacterCardsThisLevel && validCharCards.Count > 0 && slotsToFill > 0;
            if (forceCharCard)
            {
                SkillTreeNodeData charCard = PickMilestoneCharacterCard(newLevel, validCharCards);
                offeredUpgrades.Add(charCard);
                slotsToFill--;
                charCardUsed = true;
                currentOfferHasCharacterMilestone = true;
                currentCharacterMilestoneLevel = newLevel;
                Debug.Log("[UpgradeManager] log.");
            }

            // Character cards only enter the offer pool on milestone levels.
            var shuffledCharCards = (!allowCharacterCardsThisLevel || charCardUsed)
                ? new List<SkillTreeNodeData>()
                : validCharCards;
            if (!allowCharacterCardsThisLevel)
            {
                offerWeights.character = 0f;
            }

            // D. 闁规儼妫勮ぐ鍥礈閳衡偓缂嶆垵顕ｉ悩铏闁汇劌瀚畷閬嶆晬閸懇鈧ɑ绌卞┑鍛憹闂佹彃绉撮ˇ鏌ユ晬?
            for (int i = 0; i < slotsToFill; i++)
            {
                // 闁哄鍟撮崳鎼佹晬?0% 婵繐绠戝▍鎺楀箮閳ь剟鎳楅懞銉у煇闁?0% 閻熸瑦甯熸竟濠囧础鎺抽埀?5% 婵繐绠戝▍鎺旀喆閿濆鏁氶柕?5% 閻炴凹鍋勬慨?
                float roll = Random.value;

                bool hasWeapon = shuffledWeapons.Count > 0;
                bool hasPassive = shuffledPassives.Count > 0;
                bool hasSkill = shuffledSkills.Count > 0;
                bool hasCharCard = shuffledCharCards.Count > 0;

                SkillTreeNodeData pickedNode = null;
                float skillCutoff = offerWeights.skill;
                float characterCutoff = skillCutoff + offerWeights.character;
                float weaponCutoff = characterCutoff + offerWeights.weapon;

                if (roll < skillCutoff && hasSkill)
                {
                    pickedNode = shuffledSkills[0];
                    shuffledSkills.RemoveAt(0);
                }
                else if (roll < characterCutoff && hasCharCard && !charCardUsed)
                {
                    // 20% 婵帒鍊诲濂稿箮閸婄噥娼￠柤瑙勫絻瀹曢亶鏁嶉崼鐔烘Ж婵炲棌鈧啿纾崇紒鐙欏嫭浠樺?鐎殿喚濯寸槐?
                    pickedNode = shuffledCharCards[0];
                    shuffledCharCards.Clear(); // 婵炴挸鎳愰埞鏍⒓閸欏鍓鹃柛鎰У婵?
                    charCardUsed = true;
                }
                else if (roll < weaponCutoff && hasWeapon)
                {
                    pickedNode = shuffledWeapons[0];
                    shuffledWeapons.RemoveAt(0);
                }
                else if (hasPassive)
                {
                    pickedNode = shuffledPassives[0];
                    shuffledPassives.RemoveAt(0);
                }
                // 濞ｅ洦绻傜花鎶芥晬濮橆剚鎲垮☉鎿冧簼閻粎鈧稒鍔栧﹢浣轰焊鏉堚晝鑸堕柛婵愪簷闁?
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

                // 闁告ê顭烽崳绋课涢埀顒勫蓟閵夘垳绐楅梺顒€鐏濋崢銈夊触鐏炶偐顏辩€殿喚濮村畷閬嶅礄閾忕懓绠涘鑸电椤?
                if (pickedNode != null && !offeredUpgrades.Contains(pickedNode))
                {
                    offeredUpgrades.Add(pickedNode);
                    Debug.Log("[UpgradeManager] log.");
                }
                else if (pickedNode != null)
                {
                    Debug.Log("[UpgradeManager] log.");
                    // 闂佹彃绉撮ˇ鍙夌閸☆厾绀夐悘蹇旂箚閻︻垱绂掓惔鈥冲緭濞寸姵鐗楅惈婊呪偓娑欏姌钘熷☉鎾亾鐎殿喚濮崇粭澶愭煂瀹ュ拋妲婚柣?
                    SkillTreeNodeData fallback = null;
                    fallback = fallback ?? shuffledCharCards.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledPassives.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledWeapons.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    fallback = fallback ?? shuffledSkills.FirstOrDefault(n => !offeredUpgrades.Contains(n));
                    if (fallback != null)
                    {
                        offeredUpgrades.Add(fallback);
                        shuffledCharCards.Remove(fallback);
                        shuffledPassives.Remove(fallback);
                        shuffledWeapons.Remove(fallback);
                        shuffledSkills.Remove(fallback);
                        if (validCharCards.Contains(fallback)) charCardUsed = true;
                        Debug.Log("[UpgradeManager] log.");
                    }
                }
            }

            levelUpsSinceCharacterCardOffer = charCardUsed ? 0 : levelUpsSinceCharacterCardOffer + 1;
        }

        // ... 闁告艾娴烽悽?UI 闁告帡鏀遍弻濠囨焻閺勫繒甯嗗ǎ鍥ㄧ箖鐎垫梹绋夊鍛秮 ...
        if (offeredUpgrades.Count == 0)
        {
            Time.timeScale = 1f;
            yield break;
        }

        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        SetUpgradePanelTitle(1);
        if (currentOfferHasCharacterMilestone && titleText != null)
        {
            titleText.text = $"Character Card Lv.{currentCharacterMilestoneLevel}";
        }
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    private OfferWeights GetOfferWeightsForLevel(int newLevel)
    {
        OfferWeights weights = new OfferWeights(0.30f, 0.20f, 0.25f, 0.25f);
        if (!DemoContentGate.DemoModeEnabled) return weights;

        int ownedWeaponCount = WeaponController.Instance != null ? WeaponController.Instance.ownedWeapons.Count : 1;
        if (newLevel <= 8 && ownedWeaponCount <= 2)
        {
            return new OfferWeights(0.25f, 0.15f, 0.40f, 0.20f);
        }

        if (newLevel >= 24)
        {
            return new OfferWeights(0.50f, 0.10f, 0.10f, 0.30f);
        }

        if (newLevel >= 14 || ownedWeaponCount >= 3)
        {
            return new OfferWeights(0.45f, 0.15f, 0.15f, 0.25f);
        }

        return weights;
    }

    private bool ShouldForceCharacterCard(int newLevel, int availableCharacterCardCount)
    {
        if (availableCharacterCardCount <= 0) return false;
        return IsCharacterCardMilestoneLevel(newLevel);
    }

    private bool IsCharacterCardMilestoneLevel(int newLevel)
    {
        return newLevel == 5 || newLevel == 10;
    }

    private SkillTreeNodeData PickMilestoneCharacterCard(int newLevel, List<SkillTreeNodeData> validCharCards)
    {
        if (validCharCards == null || validCharCards.Count == 0) return null;

        List<SkillTreeNodeData> authoredCards = validCharCards
            .Where(card => card != null && !IsRuntimeRoleMilestoneCard(card))
            .ToList();

        if (newLevel == 5)
        {
            SkillTreeNodeData authoredBranchCard = authoredCards.FirstOrDefault(IsBranchMechanicCard);
            if (authoredBranchCard != null) return authoredBranchCard;

            if (authoredCards.Count > 0) return authoredCards[0];

            SkillTreeNodeData roleIntroCard = runtimeRoleMilestoneCards.Count > 0 && validCharCards.Contains(runtimeRoleMilestoneCards[0])
                ? runtimeRoleMilestoneCards[0]
                : validCharCards.FirstOrDefault(IsRuntimeRoleMilestoneCard);
            if (roleIntroCard != null) return roleIntroCard;
        }

        if (newLevel == 10)
        {
            SkillTreeNodeData authoredNonBranchCard = authoredCards.FirstOrDefault(card => !IsBranchMechanicCard(card));
            if (authoredNonBranchCard != null) return authoredNonBranchCard;

            if (authoredCards.Count > 0) return authoredCards[0];

            SkillTreeNodeData rolePowerCard = runtimeRoleMilestoneCards.Count > 1 && validCharCards.Contains(runtimeRoleMilestoneCards[1])
                ? runtimeRoleMilestoneCards[1]
                : validCharCards.LastOrDefault(IsRuntimeRoleMilestoneCard);
            if (rolePowerCard != null) return rolePowerCard;
        }

        return validCharCards[0];
    }

    private bool IsRuntimeRoleMilestoneCard(SkillTreeNodeData card)
    {
        return runtimeRoleMilestoneCards.Contains(card);
    }

    private SkillTreeNodeData CreateFusionNode(FusionRecipeSO recipe)
    {
        // 闁革负鍔岄崬瀵糕偓娑櫭奸懙鎴﹀礆濞戞绱﹀☉鎾亾濞戞搩浜欐径宥夊籍閸撲焦鐣?ScriptableObject 閻庡湱鍋樼欢?
        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();

        // 闁哄嫬澧介妵姘辩磼閹惧浜慨婵撶畱濞呮帡鎯冮崟顐ｅ€抽悗娑欘殔閹蜂即宕堕悙顒傚灱 (濞撴艾顑呴々?"闁绘劘鍋愮€氳櫕顦版惔銏＄樄")
        node.skillName = recipe.resultWeapon.weaponName;
        node.skillIcon = recipe.resultWeapon.weaponIcon; // 闁瑰瓨鐗為埀顒€鎳忓Σ?recipe.fusionIcon
        node.associatedWeapon = recipe.resultWeapon;

        // 闁告帗绋戠紓鎾寸▔閳ь剚绋夐鍫氬亾婢舵劑鈧?
        UpgradeOption option = new UpgradeOption();
        option.description = recipe.description; // "闁捐绉撮幃搴ㄦ晬娴ｅ搫鍓甸柣鎺楊暒缁楀矂鎮挎ィ鍐炬闁汇劌瀚划銊╁触?.."
        option.rarity = Rarity.Epic; // 闁捐绉撮幃搴ㄦ焻濮橆剛鍩楅柡鍕靛灠瑜板墎鎷犲Δ鍐崜闁汇劌瀚伴崳楣冩嚌?
        option.effects = new List<UpgradeEffect>();

        // 闁告帗绋戠紓鎾寸▔閳ь剚绋夐鍡楊棗婵炲牆锕﹀▓?Effect
        UpgradeEffect effect = new UpgradeEffect();
        // 闁瑰瓨鍨冲鎴︽閳ь剛鎲版担椋庮伇缂?ActionType 闁哄鍎遍幉锛勬嫚婢跺矂鍏囩紓浣哄暱閳ь剚绮嶆晶鐣屾偘瀹€鍐偓娲触閸埃鍋?
        // 闁哄啨鍨婚崝褎鎷呴悩杈╊吅闁告挸绉甸惀鍛村嫉?EvolveWeapon 闁哄鐭俊鍥晬鐏炴儳鐏夊ù鐙€鍓欏銊╂偨?ModifyStat + 闁绘顫夐悾鈺呭磹閸忓吋闄嶉柡宥呮穿椤斿洭鏁?
        // 闁瑰瓨鐗為埀顒€鎳忓〒鑸电附閽樺绠甸柛鏃傚С缁斿瓨绋?EffectActionType.FuseWeapon

        // 闁稿娲╅鏇㈠箣閹存粍绮﹂柛?UpgradeEffect.cs 闂佹彃鑻慨鐐寸?FuseWeapon (鐎殿喛娅ｉ崕鎾愁嚈妤︽鍞撮柛鏃傚С缁斿瓨绋?
        effect.actionType = EffectActionType.EvolveWeapon; // 闁哄棗鍊瑰鍌涘緞瀹ュ洦鏆?EvolveWeapon

        // 閺夆晜鐟╅崳椋庣矙瀹ュ懍绨?hack 濞戞挴鍋撳☉鎾愁儜缁?
        // 闁瑰瓨鍨冲鎴︽閳ь剛鎲版担鐟拔?recipe 濞磋偐濮风划?OnUpgradeOptionSelected
        // 濞?UpgradeEffect 婵炲备鍓濆﹢?FusionRecipeSO 閻庢稒顨嗛宀勫Υ?
        // 闁哄啨鍨婚崝褎娼诲▎鎰﹀☉鎾崇摠濡炲倿鎯冮崟鍓佺闁瑰瓨鍨冲鎴﹀矗椤栨瑤绨伴柟?recipe.resultWeapon 闁衡偓閹勮含 weaponToUnlock 闂?
        // 闁绘帟娉涢幃妤呭捶?OnUpgradeOptionSelected 闂佹彃鐭傞埀顒佷亢缁?CheckForAvailableFusion 闁告劕绉甸鑲╂兜椤旀鍚?
        effect.weaponToUnlock = recipe.resultWeapon;

        option.effects.Add(effect);
        node.possibleOptions = new List<UpgradeOption> { option };

        return node;
    }

    private SkillTreeNodeData CreateFusionNode(WeaponFusionRecipeSO recipe)
    {
        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();
        node.skillName = recipe.resultWeapon != null && !string.IsNullOrEmpty(recipe.resultWeapon.weaponName)
            ? recipe.resultWeapon.weaponName
            : recipe.recipeName;
        node.skillIcon = recipe.resultWeapon != null && recipe.resultWeapon.weaponIcon != null
            ? recipe.resultWeapon.weaponIcon
            : recipe.cardIcon;
        node.associatedWeapon = recipe.resultWeapon;

        UpgradeOption option = new UpgradeOption();
        option.description = !string.IsNullOrEmpty(recipe.description) ? recipe.description : recipe.recipeName;
        option.rarity = Rarity.Epic;
        option.effects = new List<UpgradeEffect>();

        UpgradeEffect effect = new UpgradeEffect();
        effect.actionType = EffectActionType.EvolveWeapon;
        effect.weaponToUnlock = recipe.resultWeapon;
        option.effects.Add(effect);

        node.possibleOptions = new List<UpgradeOption> { option };
        return node;
    }

    private IEnumerator ShowCardsSequentially()
    {
        foreach (var upgradeNode in offeredUpgrades)
        {
            // --- 閺夆晜鐟╅崕鎾礆閸℃稈鍋撻弰蹇曞竼濞戞挸瀛╅崑宥夊储閻斿憡闄嶉柣銊ュ缁旀挳鎳?---
            float playerLuck = PlayerStats.Instance != null ? PlayerStats.Instance.luck : 1.0f;
            UpgradeOption chosenOption = RaritySystem.GetRandomOptionByRarity(upgradeNode.possibleOptions, playerLuck);

            if (chosenOption == null) continue;

            GameObject prefabToInstantiate = GetPrefabForOption(chosenOption);
            GameObject cardGO = Instantiate(prefabToInstantiate, cardContainer);
            var cardUI = cardGO.GetComponent<UpgradeCardUI>();
            // --- 闂侇偅妲掔欢顐ょ磼閹惧瓨灏?---

            if (cardUI != null)
            {
                // 1. 闁稿繐鐗愰鏇犵磾椤旂厧骞㈤柣妤€娲﹂弳鐔煎箲?
                cardUI.Setup(upgradeNode, chosenOption);

                // 2. 闁告劕绉烽惃鐔兼偨閳福ow()闁哄倽顫夌涵鍫曞级閵夈剱鏇㈠矗閹卞《imator闁告柣鍔庨弫?
                cardUI.Show();

                // 3. 閻忓繐妫楅悿鍕瑹鐎ｎ亜顕ч柣銊ュ瀹曢亶鎮ч崲顨悗娑櫭崣鍡涘礆濡ゅ嫨鈧?
                activeCardUIs.Add(cardUI);
            }

            // 闁靛棙鍔曢崣褔鏌ㄩ琛″亾閹寸姷鎼肩€垫澘鎳忕€垫氨鈧姘ㄥ▓鎴﹀籍閸洘锛熼柨娑樿嫰閸熲偓閺夆晜绋栭、鎴炵▔鐎ｂ晝顏辨繛鍡忊偓铏剷闁?
            yield return new WaitForSecondsRealtime(delayBetweenCards);
        }
    }
    private List<SkillTreeNodeData> GetAvailableUpgrades()
    {
        List<SkillTreeNodeData> availableNodes = new List<SkillTreeNodeData>();

        // --- 1. 闁兼儳鍢茶ぐ鍥焻濮樿鲸鏆忛悶姘煎亜婵晠骞庨埀顒勬嚄?(闁告鍠栭埀顒佹缁? ---
        // 濞寸姰鍎辨晶鐘诲及椤栫偘鎲鹃柛?allUpgrades闁挎稑鐬奸獮鍥捶閵娾晙鎲鹃柛?passiveUpgrades
        if (upgradeDatabase.passiveUpgrades != null)
        {
            foreach (var node in upgradeDatabase.passiveUpgrades)
            {
                // 婵☆偀鍋撻柡灞诲劚婢х姷绱旈鑺ヨ拫濞?
                bool prerequisitesMet = node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p));
                // 婵☆偀鍋撻柡灞诲劤閻℃垹鐥缁楀倿姊?
                int nodeMaxLevel = GetEffectiveUpgradeNodeMaxLevel(node);
                bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < nodeMaxLevel;

                if (prerequisitesMet && notMaxed && IsPassiveNodeUnlocked(node))
                {
                    availableNodes.Add(node);
                }
            }
        }

        // --- 2. 闁兼儳鍢茶ぐ鍥ь潰閿曗偓濞呮帡宕￠崶鈺呯崜 (闁哄倷鍗抽埀顒佹缁?- 闁告柣鍔嶉埀顑胯兌閺佹捇骞嬮幇顖毼濋柣? ---
        // 閺夆晜鐟╅崳椋庢嫬閸愵亝鏆忛柟瀛樺灣濠婃垶绋婄€ｎ亜顤呴悹浣靛姀椤旀垿鎯?GenerateWeaponNodes 闁哄倽顫夌涵?
        availableNodes.AddRange(GenerateWeaponNodes());

        return availableNodes;
    }
    private List<SkillTreeNodeData> GenerateWeaponNodes()
    {
        List<SkillTreeNodeData> nodes = new List<SkillTreeNodeData>();

        if (WeaponController.Instance == null) return nodes;

        // [闁圭儤甯楅悡锟犲籍閵夈儳绠?1] 闁瑰灚鎸稿畵鍐亹閹惧啿顤呴悗娑櫳戦妴鍌炴煂鐏炴儳顣查柡鍫濐槺濞堟垹鎲撮敐澶嬫暁闁绘せ鏅涢幖褔鏁嶅畝鈧﹢鍛存儑?"Molotov" 闁告帗婢樼花鎶藉捶閵娿倗鐟濋柛锔哄姂閸ｇ兘妫?
        if (PlayerProgressManager.Instance != null)
        {
            string allUnlocked = string.Join(", ", PlayerProgressManager.Instance.unlockedItems);
            // 闁靛棙鍔掗幈銊╁绩楠炲簱鍋撻幋婵嗙闁瑰搫顦崹鐣岀矚閻氬绀夌€殿喖鎼崺妤呭箥閹惧啿绁柨娑樿嫰椤┭囧几濠婂嫭笑缂佸矁娅ｅ▓鎴犱焊鏉堛劍鈻旂紒鈧?"闁?
            Debug.Log("[UpgradeManager] log.");
        }

        // --- 1. 闁兼儳鍢茶ぐ鍥亹閹惧啿顤呮慨婵撶畱濞呮帡寮导鏉戞闁告粌濂旂粭鍌炴⒔?---
        int currentWeaponCount = WeaponController.Instance.ownedWeapons.Count;
        int maxWeaponSlots = 6;

        // 闁兼儳鍢茶ぐ鍥╂惥閸涱噮鍔呴柛鎺擃殙閵?
        HashSet<WeaponStatBlock> evolutionOnlyWeapons = new HashSet<WeaponStatBlock>();
        if (WeaponController.Instance.fusionRecipes != null)
        {
            foreach (var recipe in WeaponController.Instance.fusionRecipes)
            {
                if (recipe.resultWeapon != null) evolutionOnlyWeapons.Add(recipe.resultWeapon);
            }
        }

        // 闂侇剙绉村濠氬极閻楀牆绁﹂幖?
        foreach (var chain in upgradeDatabase.weaponChains)
        {
            if (chain.targetWeapon == null) continue;
            if (!DemoContentGate.IsWeaponAllowed(chain.targetWeapon)) continue;

            // 閺夆晛娲﹂幎銈夊箳婢跺海孝婵?
            bool isEvoWeapon = evolutionOnlyWeapons.Contains(chain.targetWeapon);

            // 閺夆晛娲﹂幎銈夊箳婢舵劗鎷ㄩ柛姘Т瀹?
            if (WeaponController.Instance.banList.Contains(chain.targetWeapon)) continue;

            // 闁兼儳鍢茶ぐ鍥箯閵夛附绠掗柣妯垮煐閳?
            var ownedWeapon = WeaponController.Instance.ownedWeapons
                .FirstOrDefault(w => w.stats == chain.targetWeapon);

            int currentLevel = (ownedWeapon != null) ? ownedWeapon.currentLevel : 0;
            int maxLevel = chain.targetWeapon.maxLevel;
            int dynamicMaxLevel = (ownedWeapon != null && ownedWeapon.weaponPartInstance != null)
                                    ? ownedWeapon.weaponPartInstance.maxLevel
                                    : chain.targetWeapon.maxLevel;

            // ---------------------------------------------------------
            // 闁诡垰鎳庨崰?A: 閻忓繑纰嶅﹢顓㈠箯閵夛附绠?-> 闁圭粯鍔掔欢鐢垫喆閿濆鏁氶梺顐㈩樀閵?
            // ---------------------------------------------------------
            if (ownedWeapon == null)
            {
                // [闁圭儤甯楅悡锟犲籍閵夈儳绠?2] 闂佽棄鐗嗛顕€鎮ら崘顏勫姢闁烩€冲濞堟垶绋夐幘姹団偓宥呂涢埀顒勫蓟?
                // 濠碘€冲€归悘澶愬触瀹ュ懐鎽熼梺鎻掕嫰鐎垫﹢宕?Molotov 闁?闁绘洖鍟伴崕鎶芥晬鐏炶姤鐨戦柟鍨尭瀹撳啰鎷犻敂鍓х煄闁哄啨鍎辩换?
                bool isTargetDebug = chain.weaponName.Contains("Molotov");

                // 1. 婵☆偀鍋撻柡灞诲劜閻楀摜鈧?
                if (currentWeaponCount >= maxWeaponSlots)
                {
                    if (isTargetDebug) Debug.Log("[UpgradeManager] debug.");
                    continue;
                }

                // 2. 婵☆偀鍋撻柡灞诲劥缁夋潙顫?
                if (isEvoWeapon) continue;

                // =========================================================
                // 3. 閻熸瑱缍侀弨锝囨導閸曨剛澹愭俊顐熷亾闁?(閻㈩垽闄勫Λ鈺勭疀?
                // =========================================================
                bool isUnlocked = IsWeaponChainUnlocked(chain);

                // 闁哄牃鍋撶紓浣哥墕閸ㄧ晫鈧?
                if (!isUnlocked)
                {
                    if (isTargetDebug) Debug.Log("[UpgradeManager] debug.");
                    continue;
                }

                if (isTargetDebug) Debug.Log("[UpgradeManager] debug.");
                // =========================================================

                SkillTreeNodeData unlockNode = ScriptableObject.CreateInstance<SkillTreeNodeData>();
                unlockNode.skillName = $"Unlock {chain.weaponName}";
                unlockNode.skillIcon = chain.icon;
                unlockNode.associatedWeapon = chain.targetWeapon;
                unlockNode.possibleOptions = new List<UpgradeOption> { chain.unlockOption };
                nodes.Add(unlockNode);
            }
            // 闁靛棙鍔曢崙锛勭矓婵犳碍鐝熼柟顖氭噹閸犲瓓闁告粌鐡旈柕鍡樺灦椤掔喖宕抽妸銉ョ３缂佺嫏鍐╁閺夆晜绋戠€垫煡鎮抽弶鎸庤含闂侇偅淇虹换鍐潰閿曗偓濞呮帡鎳涢鍥叐缂備礁绻橀悰娆撳级閳ユ剚妲遍柣?
        }

        return nodes;
    }

    private bool IsWeaponChainUnlocked(WeaponUpgradeChainSO chain)
    {
        if (chain == null || chain.targetWeapon == null) return false;
        if (chain.isDefaultUnlocked) return true;
        if (PlayerProgressManager.Instance == null) return false;

        WeaponSkillTree progressTree = FindProgressSkillTreeForWeapon(chain.targetWeapon);
        if (progressTree != null)
        {
            if (progressTree.isDefaultUnlocked) return true;

            if (!string.IsNullOrEmpty(progressTree.unlockStatKey) && progressTree.unlockThreshold > 0)
            {
                return PlayerProgressManager.Instance.GetAchievementStat(progressTree.unlockStatKey) >= progressTree.unlockThreshold;
            }
        }

        string weaponID = chain.targetWeapon.weaponID;
        string weaponName = chain.targetWeapon.weaponName;
        return (!string.IsNullOrEmpty(weaponID) && PlayerProgressManager.Instance.IsItemUnlocked(weaponID))
            || (!string.IsNullOrEmpty(weaponName) && PlayerProgressManager.Instance.IsItemUnlocked(weaponName));
    }

    private WeaponSkillTree FindProgressSkillTreeForWeapon(WeaponStatBlock weapon)
    {
        if (weapon == null || PlayerProgressManager.Instance == null || PlayerProgressManager.Instance.allSkillTrees == null) return null;

        foreach (WeaponSkillTree tree in PlayerProgressManager.Instance.allSkillTrees)
        {
            if (tree == null || tree.associatedWeapon == null) continue;

            WeaponStatBlock candidate = tree.associatedWeapon;
            if (candidate == weapon) return tree;

            if (!string.IsNullOrEmpty(candidate.weaponID)
                && !string.IsNullOrEmpty(weapon.weaponID)
                && string.Equals(candidate.weaponID, weapon.weaponID, System.StringComparison.OrdinalIgnoreCase))
            {
                return tree;
            }
        }

        return null;
    }

    private GameObject GetPrefabForOption(UpgradeOption option)
    {
        // 婵☆偀鍋撻柡灞诲劜濡叉悂宕ラ敂鑺ョ畳閻熸瑱缍侀弨锝咁潰閿曗偓濞呮帡鎯冮崟顒佹珡闁哄绮ｇ槐婵囨交濞嗘垼顫﹂柡浣哥墛閻忓瀵煎Ο鍝勫弗濞达綀娉曢弫銈嗙▔閹惧磭娼ｉ柛妤嬬磿婢ф牠寮藉畡鎵
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
    /// 闁汇垹宕畷閬嶆偋閸㈩毌闁革负鍔忛～锕傛倷閻熸澘姣婇柛姘唉閻ㄧ喖鎮?
    /// </summary>
    public void OnUpgradeOptionSelected(SkillTreeNodeData sourceNode, UpgradeOption chosenOption)
    {
        // --- 1. 闁哄秴娲╅鍥ㄦ媴瀹ュ繒绐楅弶鈺傜懄椤愬ジ骞欏鍕▕闁哄嫷鍨伴幆渚€寮伴婢帡鏌ㄦ担璇″妳闁革綆鐓夌槐?---
        bool isUnlockOperation = false;

        foreach (UpgradeEffect effect in chosenOption.effects)
        {
            // 闁靛棙鍔掗幈銊﹀緞瀹ュ懎褰犻梺娆惧枤閸嬶綁濡撮幋婵囪含閺夆晜鐟╅崳椋庘偓瑙勭煯缁?appliedLocally 闁告瑦锕㈤崳娲晬瀹€鍕笡閻犱降鍊栧Σ?false
            bool appliedLocally = false;

            if (sourceNode.associatedWeapon != null && WeaponController.Instance != null)
            {
                // 閻忓繑绻嗛惁顖炲捶閵娿劌鍓归柛鏍ф嚇閸ｇ兘骞嶉幆褍鐓傞弶鈺傜懄婵＄顫㈤敃鈧▍鎺楁儍閸曨偆鏉藉〒?
                var weaponWrapper = WeaponController.Instance.ownedWeapons
                    .FirstOrDefault(w => w.InheritsSkillSource(sourceNode.associatedWeapon));

                if (weaponWrapper != null && weaponWrapper.weaponPartInstance != null)
                {
                    WeaponPart part = weaponWrapper.weaponPartInstance;

                    // 濠㈣泛瀚幃濠囧极閺夊簱鍋?(闁谎勫劤閸ㄥ骸袙閺冨洦绁悘蹇撶箲閺?
                    float val = effect.value;
                    if (effect.modType == ModifierType.Percentage) val /= 100f;

                    // =========================================================
                    // 闁靛棙鍔栭悧瀹犵疀閸愌勫弿濠㈣泛绉查埀顒佸灦鐎氥倝骞嬮鍛暡闁哄牆顦伴鐔煎闯閵娿儳娼ｉ柟顑秶绀夐悗娑櫭崣鍡欎沪閳ь剟鏌堥妸銉ョ秮闂?
                    // =========================================================
                    switch (effect.statToModify)
                    {
                        case UpgradeType.WeaponDamage:
                            part.localDamageBonus += val;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.WeaponFireRate:
                            // 闁稿娲╅鏇㈠礃瀹勬澘绁辩紓鍌楁櫅閸ｆ椽寮伴娑卞妧闁?(濠?0.1 濞寸媴缍€閵?-10% CD)
                            part.localFireRateBonus += val;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.OrbitalSpeed:      // 閺夌偑鍔戞禍楣冩焻閻斿嘲顔?
                        case UpgradeType.WeaponProjectileSpeed: // 闁瑰瓨鐗為埀顒€鎳庨悺娆忣嚕瑜版巻鍋撻悢宄邦唺
                            part.localSpeedBonus += val;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.BoomerangReturnDamage:
                            part.boomerangReturnDamageBonus += val;
                            appliedLocally = true;
                            break;

                        case UpgradeType.BoomerangReturnPulse:
                            part.boomerangReturnPulseEveryHits = 4;
                            part.boomerangReturnPulseDamageMultiplier = Mathf.Max(part.boomerangReturnPulseDamageMultiplier, val);
                            part.boomerangReturnPulseRadius = Mathf.Max(part.boomerangReturnPulseRadius, 2.2f);
                            appliedLocally = true;
                            break;

                        case UpgradeType.BoomerangRecallBurst:
                            part.boomerangRecallBurstDamageMultiplier = Mathf.Max(part.boomerangRecallBurstDamageMultiplier, val);
                            part.boomerangRecallBurstRadius = Mathf.Max(part.boomerangRecallBurstRadius, 2.8f);
                            appliedLocally = true;
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
                            // 闁哄秷顫夊畵浣割潰閿曗偓濞呮帞鐚剧拠鑼偓鐑藉礆閸℃稑甯抽柛鎺楊暒缁楀宕ュ畝鈧▓鎴犫偓娑欘殕椤?
                            if (part.StatBlock != null && part.StatBlock.behavior == WeaponBehaviorType.Landmine)
                            {
                                part.localMineCountBonus += Mathf.RoundToInt(effect.value);
                                Debug.Log("[UpgradeManager] log.");
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
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        case UpgradeType.SlashCount:
                            part.localSlashCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            // 婵烇綀顕ф慨鐐哄籍閵夈儳绠堕柨娑樼灱閳ユɑ绌卞┑鍡欐憼闂佸€熼哺閸ㄦ岸宕?
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.BurstCount:
                            part.localBurstCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.AoeRadius:
                            part.localAreaBonus += effect.value / 100f; // 闁稿娲╅?effect.value 闁哄嫷鍨冲▍銊╁礆閸℃妲?(80 濞寸媴缍€閵?80%)闁挎稑鐭佺换鏍煂瀹€鍐╃ギ濞?0.8
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.SubProjectileCount:
                            // 閺夆晜鐟╅崳鐑藉箣閹存粍绮﹂柛瀣穿椤?SubProjectileCount 闁烩晛鐡ㄧ敮瀛樻櫠閻愭彃顫?WeaponStatBlock 闂佹彃鐬煎▓?subProjectileCount
                            // 濞?WeaponStatBlock 闁?ScriptableObject闁挎稑鐭佺换宥囨偘鐏炵偓顦уǎ鍥跺枟閺佸吋瀵煎顐ょ閻庢稒菧閳?
                            // 闁圭鍋撳ù鐘劜閸ㄦ粍绂掗鍕粯閻熸洑绀佸﹢?WeaponPart 闂佹彃鐬奸弫銈嗙▔閳ь剚绋夐鍕拱闂侇喓鍔岃ぐ澶愭煂韫囨梹闄嶉悗娑欙公缁辨繈骞嬮弽顑藉亾?WeaponPart 闁告柣鍔嶉埀顑挎祰椤╊偊鎯?stat闁?
                            // 闁烩晩鍠栨晶?WeaponPart 閺夆晜蓱閻ュ懘寮?localSubProjectileCount闁?
                            // 閻犱讲鏅滈崹婊勭椤掆偓閸樻稓绮婚埀顒勫础閺囩儐妲遍柣鐐叉４缁变即鎯勭€涙ê澶嶅ǎ鍥跺枟閺?part.StatBlock 闁汇劌瀚崬瀵糕偓娑櫭竟鍥嫉椤掑﹦绀勫┑鈥冲€归悘澶愬及椤栨氨鏉藉〒姘儏鐎垫煡宕欓悜妯婚檷闁汇劌瀚哥槐?
                            // 濞达絽妫濋埀顒佽壘閻?SO 闁哄嫷鍨伴崣蹇曚沪閳ь剟鎯冮崟鈹惧亾?
                            // 闁哄洦娼欓妶浠嬫儍閸曨偂绮垫繛澶嬫礃濡叉悂鏁嶅鎭奱ponPart 缂備礁鐡ㄦ慨?localSubProjectileCount闁挎稑顒ojectile 闁告瑦鍨甸惃鐘诲籍閹偊鍤㈤柛娆愮墦閳?
                            // 闁瑰瓨鍨冲鎴濐啅閼碱剛鐥呴柛?WeaponPart 闁活亜顑呴崺?localPierceCountBonus 缂佹稑顦埀?
                            // 閻犱讲鏅滈崹婊兦庣拠鎻掝潱 localSubProjectileCountBonus 闁?WeaponPart (濞戞挸顑勭粩瀛橈紣閹存繂鎸?闁?
                            // 閺夆晜鐟╅崳鐑藉礂閸繂鏅稿☉鎾筹躬閳ь剚妲掔欢顐﹀础閻樿京绉撮柨娑樼灱閻℃垶绋夌€ｂ晜鍙忛柡鈧?WeaponPart闁?
                            part.localSubProjectileCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                            
                        case UpgradeType.SubProjectile:
                            // 鐎殿喒鍋撻柛姘煎灠閸ㄥ海鎲楅崒娴や礁顕ｈ箛瀣у亾閸屾繄绠归梺顐ｈ壘閻栧爼骞囪箛鎾村殑闁活偀鍋撻悹浣稿⒔閻?subProjectilePrefab闁?
                            // 闁瑰瓨鍨冲鎴﹀矗椤栨瑤绨伴柛?WeaponPart 闂佹彃鑻悺銊︾▔閳ь剚绋?overrideSubProjectilePrefab
                            // 闁瑰瓨鐗為埀顒€鎳愰弫銈嗙▔閳ь剚绋?bool 闁哄秴娲╅?"enableSplit"
                            // 闁稿娲╅?effect.value > 0 濞寸媴缍€閵嗗啫顕ｉ埀顒勫触?
                            if (effect.value > 0)
                            {
                                part.isSubProjectileEnabled = true;
                            }
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.IgnitionChance:
                            part.localIgnitionChanceBonus += effect.value / 100f; // 100 濞寸媴缍€閵?+100% 婵帒鍊诲?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.BurnDuration:
                            part.localBurnDurationBonus += effect.value; // 闁烩晛鐡ㄧ敮鎾礉閻樻娼￠柡?(濠?6 濞寸媴缍€閵?+6缂?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.MaxHealthBurn:
                            part.localMaxHealthBurnPercent += effect.value / 100f; // 1 濞寸媴缍€閵?1%/閻?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.FreezeChance:
                            part.localFreezeChanceBonus += effect.value / 100f; // 30 濞寸媴缍€閵?+30% 闁告劖婢橀崰鏇烆潡閸屾粌鑺?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.SubProjectileDamageBonus:
                            part.localSubProjectileDamageBonus += effect.value / 100f; // 80 濞寸媴缍€閵?+80% 濞寸鍊曢?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.SubProjectileInherit:
                            part.subProjectileInheritEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闂傚棗鍢查崵顔剧尵?===
                        case UpgradeType.LightningRepeatCount:
                            part.localLightningRepeatCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.StunDuration:
                            part.localStunDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.MagneticStormBurst:
                            part.isMagneticStormEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.ElectricField:
                            part.isElectricFieldEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.ElectricFieldDamage:
                            part.localElectricFieldDamageBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.ElectricFieldDuration:
                            part.localElectricFieldDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.OnKillChainLightning:
                            part.isOnKillChainEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 濡炲鎹囬ˉ鎾诲嫉椤栨粏顫?===
                        case UpgradeType.KnockbackForce:
                            part.localKnockbackBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.VacuumPull:
                            part.isVacuumPullEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.VacuumDamage:
                            part.localVacuumDamageBonus += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.WindReturn:
                            part.isWindReturnEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.Turbulence:
                            part.localTurbulenceLevel = Mathf.Max(part.localTurbulenceLevel, 1);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.TurbulenceIntensify:
                            part.localTurbulenceLevel += 1;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 婵帗娼欓懘濠勭尵?===
                        case UpgradeType.GrenadeBounce:
                            part.localBounceCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.Stun:
                            part.localStunDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闂傚偆浜為弫鎼佹煣閸撗嗩潶 ===
                        case UpgradeType.ChainCount:
                            part.localChainCountBonus += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.IonExplosion:
                            part.localIonExplosionEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.IonExplosionDamage:
                            part.localIonExplosionDamageBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.IonExplosionRadius:
                            part.localIonExplosionRadiusBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闁告劒鍗冲﹢婵嬪棘閻楀牊袝缂?===
                        case UpgradeType.FrostNovaExtraCast:
                            part.localFrostNovaExtraCast += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.FreezeDuration:
                            part.localFreezeDurationBonus += effect.value;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.FrostNovaCenterDamage:
                            part.localFrostNovaCenterDmg = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.AbsoluteZero:
                            part.localAbsoluteZero = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闁告劒鍗冲﹢婵嬫懚瀹ュ懏鍊ょ紒?===
                        case UpgradeType.FrostBite:
                            part.localFrostBite = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.IceCrystalShatter:
                            part.localIceCrystalShatter += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.CooldownReduction:
                            part.localCooldownReduction += effect.value / 100f;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闁绘粠鍨崇划顐㈩潰閿曗偓濞呮帞鐚?===
                        case UpgradeType.OrbitalAbsorbProjectiles:
                            part.isOrbitalAbsorbEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.OrbitalExpansionBreathing:
                            part.isOrbitalBreathingEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.OrbitalReleaseExplosion:
                            part.isOrbitalReleaseEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闁革缚鍗冲ù鍕尵?===
                        case UpgradeType.LandmineEnergyRecovery:
                            part.isMineEnergyRecovery = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.LandmineStun:
                            part.isMineStun = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.LandmineGravityTrap:
                            part.isMineGravityTrap = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.LandmineBlackHole:
                            part.isMineBlackHole = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.FusionNapalm:
                            part.isMineFusionNapalm = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === Aura閺夊牆鎳庢慨顏堝垂鐎ｎ亜甯ㄩ柣婊庡灣鐞氼偊鏁嶉崸纰糽ue 闁烩晛鐡ㄧ敮瀛樼▔閸濆嫮鏉介梻鍕噺閺嗙喖宕愮涵椋庣 ===
                        case UpgradeType.AuraHealingPulse:
                            part.auraHealAmount = Mathf.Max(part.auraHealAmount, effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.AuraSluggishField:
                            part.auraSlowPercent = Mathf.Max(part.auraSlowPercent, effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.AuraFragileMark:
                            part.auraFragilePercent = Mathf.Max(part.auraFragilePercent, effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闁诲繋绲婚崗妯活槹閻愭彃鐎紒?===
                        case UpgradeType.DaggerDamageBoost:
                        {
                            // value=1: 濞寸鍊曢?30%闂侇偆鍠庣€?15%, value=2: 濞寸鍊曢?60%闂侇偆鍠庣€?25%
                            float dmgBonus = effect.value >= 2 ? 60f : 30f;
                            float spdPenalty = effect.value >= 2 ? 25f : 15f;
                            part.daggerDamageBoost = Mathf.Max(part.daggerDamageBoost, dmgBonus);
                            part.daggerSpeedPenalty = Mathf.Max(part.daggerSpeedPenalty, spdPenalty);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        }
                        case UpgradeType.DaggerExtraCount:
                        {
                            // value=1: +1闁告巻鍋撳ù绗哄€曢?15%, value=2: +2闁告巻鍋撳ù绗哄€曢?25%闁挎稑鐗嗚ぐ鏃堝礉閻欏懐绀?
                            int extraCount = effect.value >= 2 ? 2 : 1;
                            float dmgPenalty = effect.value >= 2 ? 25f : 15f;
                            part.daggerExtraCount += extraCount;
                            part.daggerCountDmgPenalty += dmgPenalty;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        }
                        case UpgradeType.DaggerSpeedBoost:
                        {
                            // value=1: 闂侇偆鍠庣€圭1.3闂傚倹鎸冲▓?20%, value=2: 闂侇偆鍠庣€圭1.6闂傚倹鎸冲▓?35%
                            float spdMult = effect.value >= 2 ? 1.6f : 1.3f;
                            float intervalReduce = effect.value >= 2 ? 35f : 20f;
                            part.daggerSpeedBoost = Mathf.Max(part.daggerSpeedBoost, spdMult);
                            part.daggerIntervalReduction = Mathf.Max(part.daggerIntervalReduction, intervalReduce);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        }
                        case UpgradeType.DaggerHoming:
                            part.daggerHomingUpgrade = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        case UpgradeType.DaggerClone:
                            part.daggerCloneUpgrade = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        case UpgradeType.DaggerIgnite:
                            part.daggerIgniteUpgrade = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        case UpgradeType.DaggerLifeSteal:
                            part.daggerLifeStealUpgrade = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        case UpgradeType.DaggerChainExplosion:
                            part.daggerChainExplosion = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        // === 闂傗偓椤撶偟娈搁柡宥囶焾缁哄墽鐚?===
                        case UpgradeType.LaserRefraction:
                            part.localLaserRefractionCount += Mathf.RoundToInt(effect.value);
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.LaserFocusBonus:
                            part.localLaserFocusBonus += effect.value / 100f; // 5 濞寸媴缍€閵?+5% 婵絽绻愰惇?
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;

                        case UpgradeType.LaserMeltdown:
                            part.localLaserMeltdownEnabled = true;
                            appliedLocally = true;
                            Debug.Log("[UpgradeManager] log.");
                            break;
                        
                    }
                }
            }

            // 濠碘€冲€归悘澶娾柦閳╁啯绠掗悘鐐╁亾闂侇喓鍔岀花鏌ユ偨椤帞绀勯悹鍥х摠濡叉垿寮伴鐐╁亾濮樿鲸鏆忛悘鐐靛仦閳ь儸宥囩婵絾鏌ㄩ々褔宕濋悩渚敤濞戞挸锕娲晬婢舵稓绀夐柛鎺撶懃缁ㄦ煡鎮介妸銉ョ厒闁稿繈鍔岄惇?PlayerStats
            if (!appliedLocally && PlayerStats.Instance != null && effect.actionType == EffectActionType.ModifyStat)
            {
                PlayerStats.Instance.ApplyEffect(effect);
            }
            // 濠㈣泛瀚幃濠囨偋鐟欏嫮鏆曢柟鍨С缂嶆梻鐚剧拠鑼偓?
            else if (effect.actionType == EffectActionType.UnlockWeapon)
            {
                isUnlockOperation = true;
                if (effect.weaponToUnlock != null && WeaponController.Instance != null)
                {
                    WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock);
                    RegisterWeaponSkillGem(effect.weaponToUnlock, 1);
                }
            }
            else if (effect.actionType == EffectActionType.UnlockShield)
            {
                if (effect.shieldToUnlock != null && PlayerShield.Instance != null) { PlayerShield.Instance.EquipShield(effect.shieldToUnlock); }
            }
            else if (effect.actionType == EffectActionType.EvolveWeapon)
            {
                isUnlockOperation = true;
                if (WeaponController.Instance != null && effect.weaponToUnlock != null)
                {
                    // 1. 閻忓繑绻嗛惁顖炴懚瀹ュ懏鍊?(濞ｅ洦绻冪€垫梹绋夊鍛秮)
                    var recipe = WeaponController.Instance.fusionRecipes.FirstOrDefault(r => r.resultWeapon == effect.weaponToUnlock);
                    if (recipe != null)
                    {
                        WeaponController.Instance.PerformFusion(recipe);
                        Debug.Log("[UpgradeManager] log.");
                    }
                    else
                    {
                        // 2. 闁靛棙鍔栭悧瀹犵疀閸愌勫弿闁衡偓楠炲簱鍋撻幋婵嗙濞达絾鎹佺换姗€宕犻弽顓涘亾閺勫繒甯?
                        // 濞戞挸绉撮崯鈧柤濂変簻缁讳焦寰勯崟顓熷€為柡浣哄瀹撲線寮撮幐搴″簥闁挎稑鐭侀埀顒€鏈Σ鍛婃叏閺冣偓婢ь厾绱?WeaponController 鐟滄媽顕х花鎶藉箲閵忥紕浠?

                        // 闁瑰灚鍎抽崺宀勫及椤栨繄娈遍弶鈺傜☉鐎垫煡骞嬮幇顖滅濞戞搩浜濋弻濠傤潰閿曗偓濞?
                        WeaponFusionManager fusionManager = WeaponFusionManager.EnsureInstance();
                        WeaponFusionRecipeSO evolutionRecipe = fusionManager != null
                            ? fusionManager.FindAvailableRecipeByResult(effect.weaponToUnlock)
                            : null;
                        if (evolutionRecipe != null)
                        {
                            WeaponController.Instance.PerformFusion(evolutionRecipe);
                            Debug.Log("[UpgradeManager] log.");
                            continue;
                        }

                        var oldWeaponWrapper = WeaponController.Instance.ownedWeapons
                            .FirstOrDefault(w => w.stats != null && w.stats.evolutionTarget == effect.weaponToUnlock);

                        if (oldWeaponWrapper != null)
                        {
                            // 閻犲鍟伴弫銈夊箣閹存粍绮﹂柛鎺撶婢х娀宕樺▎鎴炵暠闁哄倻澧楅弻鐔封枖?
                            WeaponController.Instance.EvolveWeapon(oldWeaponWrapper.stats, effect.weaponToUnlock);
                        }
                        else
                        {
                            // 濞ｅ洦绻傜花鎶芥晬濮橆厼顥濆☉鎾崇Т閸╁矂寮濞堟垿鎯勭€涙ê澶嶇紓浣圭懄閺屽﹪鎯?
                            WeaponController.Instance.AddNewWeapon(effect.weaponToUnlock);
                        }
                    }
                }
            }
            // 闁靛棙鍔曢悿鍌炴儗瀹曞洭鍏囩紓浣哄枂閳ь剚鍨甸ˇ鈺呮偠閸℃ぜ浜ｉ柟閿嬬琚欓梺澶哥劍閺呫儵寮?
            else if (effect.actionType == EffectActionType.UnlockUltimate)
            {
                if (!enableUltimateUnlockCards) continue;
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
            // 闁靛棙鍔橀～妤呮嚌閸欏螚闁煎疇妫勫畷閬嶅Υ閹存繍妲遍柣鐐叉缁哄搫煤閺勫浚娼￠柤鐟板级婵⊙囨嚄閼恒儲娅忛柡?
            else if (effect.actionType == EffectActionType.ActivateCharSkill)
            {
                if (!string.IsNullOrEmpty(effect.skillIdentifier))
                {
                    activeCharacterSkills.Add(effect.skillIdentifier);
                    Debug.Log("[UpgradeManager] log.");

                    // 闁告艾鏈鍌涙償閺冨倹鏆忛悗鐢垫嚀缁ㄦ煡骞庨埀顒勬嚄閼恒儳鍩愰柤鍝勫€婚崑锝嗙▔婵犲嫭鐣遍悘鐐靛仦閳ь儸鍐潱闁瑰瓨鍔х槐娆愬閵堝拋鍞?缂佸顭烽埀?闁硅翰鍊楅弫宕囩驳婢舵稓绀?
                    ApplyCharacterNodeEffectsForSkill(effect.skillIdentifier);
                }
            }
        }

        // --- 2. 闁告瑯浜濆﹢浣姐亹閹炬墎鍋撻幇顏嗙憹闁哄嫷鍨埀顒佸灱琚欓梺澶哥劍閹奸攱鎷呭鍕槯闁挎稑鏈晶鐘诲储鐠囧樊鏉婚柛鏃傚У椤掔喖宕抽妸褏鎼肩紒?---
        if (!isUnlockOperation && WeaponController.Instance != null)
        {
            foreach (var ownedWrapper in WeaponController.Instance.ownedWeapons)
            {
                bool matchFound = false;
                if (sourceNode.associatedWeapon != null && ownedWrapper.InheritsSkillSource(sourceNode.associatedWeapon)) matchFound = true;
                else if (sourceNode.skillName.Contains(ownedWrapper.stats.weaponName)) matchFound = true;

                if (matchFound)
                {
                    int dynamicMaxLevel = ownedWrapper.stats.maxLevel;
                    if (ownedWrapper.weaponPartInstance != null)
                    {
                        dynamicMaxLevel = ownedWrapper.weaponPartInstance.maxLevel;
                    }

                    // 濞达綀娉曢弫銈夊礉閵婏腹鍋撴担椋庣憪闂傚嫭鍔樼换妯兼偘鐏炶棄鐏查柡?
                    if (ownedWrapper.currentLevel < dynamicMaxLevel)
                    {
                        ownedWrapper.currentLevel++;
                        if (ownedWrapper.weaponPartInstance != null)
                        {
                            ownedWrapper.weaponPartInstance.currentLevel = ownedWrapper.currentLevel;
                        }
                        PlayerProgressManager.Instance?.RecordWeaponLevelReached(ownedWrapper.stats, ownedWrapper.currentLevel);
                    }
                    break;
                }
            }
        }

        // 3. 闁哄洤鐡ㄩ弻濠囧础閸モ晠鐛撻悹浣规緲缂?
        if (ownedUpgrades.ContainsKey(sourceNode)) { ownedUpgrades[sourceNode]++; }
        else { ownedUpgrades.Add(sourceNode, 1); }

        // 4. 闁告帡鏀遍弻濠囨偐閼哥鍋?
        if (WeaponController.Instance != null) { WeaponController.Instance.RefreshAllWeaponStates(); }
        if (PassiveItemsUI.Instance != null) { PassiveItemsUI.Instance.UpdateIcons(); }

        // === 5. 闁靛棙鍔曢悿鍌炴儗瀹曞洭鍏囩紓浣哄枂閳ь剚鍨奸幏鐑界叒椤忓牃鍋撴径瀣仴闁汇劌瀚鐔煎闯閵婏箑螚闁?===
        RegisterWeaponSkillGem(sourceNode, chosenOption);

        // 6. 婵☆偀鍋撻柡灞诲劚閻ゅ倻绮婚崡鐑嗘▼闂侇偄顧€缁变即寮伴姘剨閺夆晜蓱濠€渚€宕滈埡鈧紞鎴﹀矗椤栫偐鍋撴径瀣靛仹闁?
        if (remainingTreasurePicks > 0)
        {
            remainingTreasurePicks--;
            Debug.Log("[UpgradeManager] log.");
            // 闁告瑯浜濆ú鍧楀棘閻楀牏鍨煎Λ鐗埳戦弸鍐偓娑欘殕瑜颁胶绮堥崫鍕挅濞达絾鐟ヨぐ鏌ユ焻婢跺娈堕柨娑樿嫰閸嶇數鏁敂璺ㄧ闁归晲妞掔粭澶愬矗?
            if (titleText != null)
            {
                if (remainingTreasurePicks > 0)
                    titleText.text = $"Choose {remainingTreasurePicks + 1} more";
                else
                    titleText.text = "Choose Reward";
            }
            // 濞戞挸绉撮崣褔姊婚銏℃〃闁哄灏呯槐婵囩▔瀹ュ棔鍒掑璺虹У濡炲倿姊绘潏鍓х缂佹稑顦欢鐔兼偝閳轰緡鍟€缂備綀鍛暰闂侇偄顦扮€?
            return;
        }

        // 婵炲备鍓濆﹢渚€宕滈埡鈧紞鎴濃枎閳╁啯娈堕柨娑樿嫰閸櫻囨⒒椤撱垺妗ㄩ柡澶嬪娴狀喗寰勫鍡欏煑闁?
        if (confetti2 != null) confetti2.SetActive(false);
        if (confetti3 != null) confetti3.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ForceGrantUpgrade(SkillTreeNodeData nodeToGrant)
    {
        if (nodeToGrant == null) return;

        // 1. 濞寸姴姘︾换鏍ㄧ▔椤忓洤螡闁绘劙鈧稖鍘柨娑樼焸濞堛垽寮甸悜妯衡枙闁告瑦鐗旂粩瀛樼▔椤忓懏浠樺Δ鍌浢幖褏鎷归妸褎鐣遍柡浣哥墛閻忓寮堕妷銉у畨闁?
        // (闁革负鍔忛惃鐔烘嫚閺囩喐顦ч柨娑樻湰閸ㄦ粍绂掗鍕ㄥ亾濮橆剛鍩楅悽顖氭湰濠€婊兠圭€ｎ厾妲搁柡鍫氬亾鐎殿喛娅ｅ▓鎴﹀极閸喓浜?
        var bestOption = nodeToGrant.possibleOptions.OrderByDescending(opt => opt.rarity).FirstOrDefault();

        if (bestOption != null)
        {
            // 2. 闁烩晛鐡ㄧ敮瀵告嫬閸愵亝鏆忛柟瀛樺灣濠婃垵顔忛崣澶嬬畳闁汇劌瀚ч埀顒佺矊缁ㄦ煡鎮介妸锔芥珡闁哄澹曢埀顒佺箘濞堟垿鏌呴弰蹇曞竼
            OnUpgradeOptionSelected(nodeToGrant, bestOption);
        }
        else
        {
            Debug.LogError("[UpgradeManager] error.");
        }
    }


    // ========================================================================
    //                    婵繐绠戝▍鎺楀箮閳ь剟鎳楅懞銉у煇 + 閻庤绻勯悡鍫曟⒐鐠鸿櫣銈电紒顖濆吹缁?
    // ========================================================================
    #region 婵繐绠戝▍鎺楀箮閳ь剟鎳楅懞銉у煇濞戞挸楠搁悿鍌炴儗瀹曞洭鍏囩紓?

    /// <summary>
    /// 闁兼儳鍢茶ぐ鍥箰閸パ呮毎婵繐绠戝▍鎺曘亹閹惧啿顤呴柛娆樺灣閺併倝鎯冮崟顒€螚闁煎疇濮ら悥鏌ユ嚍閸屾粌浠?
    /// </summary>
    public List<SkillTreeNodeData> GetAvailableWeaponSkillNodes(WeaponStatBlock weaponStats)
    {
        List<SkillTreeNodeData> result = new List<SkillTreeNodeData>();
        if (upgradeDatabase.weaponSkillNodes == null) return result;

        foreach (var node in upgradeDatabase.weaponSkillNodes)
        {
            if (node == null) continue;
            if (IsConsumedFusionSource(node.associatedWeapon)) continue;

            bool matchesWeapon = node.associatedWeapon == weaponStats;
            if (!matchesWeapon && WeaponController.Instance != null)
            {
                OwnedWeapon ownedWeapon = WeaponController.Instance.ownedWeapons
                    .FirstOrDefault(w => w != null && w.stats == weaponStats);
                matchesWeapon = ownedWeapon != null
                    && ShouldOfferInheritedWeaponSkillNodes(ownedWeapon)
                    && ownedWeapon.InheritsSkillSource(node.associatedWeapon);
            }
            if (!matchesWeapon) continue;
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

    private bool ShouldOfferInheritedWeaponSkillNodes(OwnedWeapon ownedWeapon)
    {
        if (ownedWeapon == null || ownedWeapon.weaponPartInstance == null) return true;
        return ownedWeapon.weaponPartInstance.currentStage < WeaponStage.Evolved;
    }

    private bool IsConsumedFusionSource(WeaponStatBlock weapon)
    {
        return weapon != null
            && WeaponController.Instance != null
            && WeaponController.Instance.banList != null
            && WeaponController.Instance.banList.Contains(weapon);
    }

    public bool HasSkillNode(SkillTreeNodeData node)
    {
        return node != null && ownedUpgrades.ContainsKey(node);
    }

    private int GetEffectiveUpgradeNodeMaxLevel(SkillTreeNodeData node)
    {
        if (node == null) return 0;
        PassiveItemData passive = GetPassiveItemDataFromNode(node);
        if (passive != null)
        {
            return Mathf.Max(1, passive.EffectiveMaxLevel);
        }

        return Mathf.Max(1, node.maxLevel);
    }

    private PassiveItemData GetPassiveItemDataFromNode(SkillTreeNodeData node)
    {
        if (node == null || node.possibleOptions == null) return null;
        foreach (UpgradeOption option in node.possibleOptions)
        {
            if (option == null || option.effects == null) continue;
            foreach (UpgradeEffect effect in option.effects)
            {
                if (effect != null && effect.passiveItemData != null)
                {
                    return effect.passiveItemData;
                }
            }
        }

        return null;
    }

    // === 閻庤绻勯悡鍓佸寲閼姐倗鍩犻弶鍫濇噹婵亪寮憴鍕€?===

    public int GetGemCountForWeapon(WeaponStatBlock weapon)
    {
        if (weapon == null) return 0;
        int trackedCount = CountExplicitWeaponGemGrants(weapon);
        int ownedNodeCount = CountOwnedWeaponSkillGemUpgrades(weapon);
        int runtimeLevel = GetRuntimeWeaponLevelForGems(weapon);
        return Mathf.Max(runtimeLevel, trackedCount + ownedNodeCount);
    }

    private int GetRuntimeWeaponLevelForGems(WeaponStatBlock weapon)
    {
        if (weapon == null || WeaponController.Instance == null) return 0;

        OwnedWeapon owned = WeaponController.Instance.ownedWeapons
            .FirstOrDefault(w => w != null && w.InheritsSkillSource(weapon));
        return owned != null ? Mathf.Max(0, owned.currentLevel) : 0;
    }

    private void RegisterWeaponSkillGem(SkillTreeNodeData sourceNode, UpgradeOption chosenOption)
    {
        if (sourceNode == null || !ShouldCountWeaponSkillGem(sourceNode, chosenOption)) return;
        QueueUltimateUnlockIfNeeded(sourceNode.associatedWeapon);
    }

    private void RegisterWeaponSkillGem(WeaponStatBlock weapon, int amount = 1)
    {
        if (weapon == null || amount <= 0) return;

        if (!weaponGemCounts.ContainsKey(weapon))
        {
            weaponGemCounts[weapon] = 0;
        }
        weaponGemCounts[weapon] += amount;

        QueueUltimateUnlockIfNeeded(weapon);
    }

    private int CountExplicitWeaponGemGrants(WeaponStatBlock weapon)
    {
        if (weapon == null || weaponGemCounts == null || weaponGemCounts.Count == 0) return 0;

        int count = 0;
        bool allowInheritedSources = ShouldCountInheritedGemSources(weapon);
        foreach (var pair in weaponGemCounts)
        {
            if (DoesWeaponMatchGemSource(weapon, pair.Key, allowInheritedSources))
            {
                count += Mathf.Max(0, pair.Value);
            }
        }

        return count;
    }

    private void QueueUltimateUnlockIfNeeded(WeaponStatBlock weapon)
    {
        if (weapon == null) return;
        if (!enableUltimateUnlockCards) return;

        int totalGems = GetGemCountForWeapon(weapon);
        if (totalGems < GEM_SLOT_COUNT) return;

        var weaponWrapper = WeaponController.Instance != null
            ? WeaponController.Instance.ownedWeapons.FirstOrDefault(w => w != null && w.InheritsSkillSource(weapon))
            : null;
        bool alreadyUnlocked = weaponWrapper?.weaponPartInstance?.isUltimateUnlocked ?? false;
        if (!alreadyUnlocked && !pendingUltimateUnlocks.Contains(weapon))
        {
            pendingUltimateUnlocks.Add(weapon);
        }
    }

    private int CountOwnedWeaponSkillGemUpgrades(WeaponStatBlock weapon)
    {
        if (weapon == null || ownedUpgrades == null || ownedUpgrades.Count == 0) return 0;

        int count = 0;
        bool allowInheritedSources = ShouldCountInheritedGemSources(weapon);
        foreach (var pair in ownedUpgrades)
        {
            SkillTreeNodeData node = pair.Key;
            if (!IsGemTrackedWeaponSkillNode(node)) continue;
            if (!DoesWeaponMatchGemSource(weapon, node.associatedWeapon, allowInheritedSources)) continue;

            count += Mathf.Max(0, pair.Value);
        }

        return count;
    }

    private bool IsGemTrackedWeaponSkillNode(SkillTreeNodeData node)
    {
        if (node == null || node.associatedWeapon == null) return false;
        if (node.possibleOptions != null && node.possibleOptions.Count > 0)
        {
            return node.possibleOptions.Any(option => ShouldCountWeaponSkillGem(node, option));
        }

        return node.isWeaponSkillTreeNode;
    }

    private bool ShouldCountWeaponSkillGem(SkillTreeNodeData node, UpgradeOption option)
    {
        if (node == null || node.associatedWeapon == null || option == null || option.effects == null) return false;

        bool hasStatUpgrade = false;
        foreach (UpgradeEffect effect in option.effects)
        {
            if (effect == null) continue;
            if (effect.actionType != EffectActionType.ModifyStat)
            {
                return false;
            }

            hasStatUpgrade = true;
        }

        return hasStatUpgrade;
    }

    private bool ShouldCountInheritedGemSources(WeaponStatBlock weapon)
    {
        if (weapon == null || WeaponController.Instance == null) return true;

        OwnedWeapon owned = WeaponController.Instance.ownedWeapons
            .FirstOrDefault(w => w != null && w.stats == weapon);
        return owned == null || ShouldOfferInheritedWeaponSkillNodes(owned);
    }

    private bool DoesWeaponMatchGemSource(WeaponStatBlock weapon, WeaponStatBlock source, bool allowInheritedSources = true)
    {
        if (weapon == null || source == null) return false;
        if (weapon == source) return true;
        if (!allowInheritedSources) return false;
        if (WeaponController.Instance == null) return false;

        OwnedWeapon ownedForWeapon = WeaponController.Instance.ownedWeapons
            .FirstOrDefault(w => w != null && w.InheritsSkillSource(weapon));
        if (ownedForWeapon != null && ownedForWeapon.InheritsSkillSource(source)) return true;

        OwnedWeapon ownedForSource = WeaponController.Instance.ownedWeapons
            .FirstOrDefault(w => w != null && w.InheritsSkillSource(source));
        return ownedForSource != null && ownedForSource.InheritsSkillSource(weapon);
    }

    /// <summary>
    /// 闁告帗绋戠紓鎾村緞瑜庣€氭垹鎲撮敐澶嬫暁闁告绱曟晶鏍嚍閸屾粌浠?
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
    /// 闁兼儳鍢茶ぐ鍥╂偘閵夈儱甯犻柛妤嬬磿婢?
    /// </summary>
    private List<SkillTreeNodeData> GetFillerCards(int count)
    {
        List<SkillTreeNodeData> pool = new List<SkillTreeNodeData>();
        if (upgradeDatabase.passiveUpgrades != null)
        {
            foreach (var node in upgradeDatabase.passiveUpgrades)
            {
                bool ok = (node.prerequisites == null || node.prerequisites.Count == 0 || node.prerequisites.All(p => ownedUpgrades.ContainsKey(p)));
                int nodeMaxLevel = GetEffectiveUpgradeNodeMaxLevel(node);
                bool notMax = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < nodeMaxLevel;

                // 闁靛棙鍔曞ù姗€鏌庣壕瀣撴帡鏌ㄦ担鐣岀畺婵犲鍊戦埀顒佸灦濠€顓犳喆閿濆鏁氶柣銊ュ椤箓宕濋妸鈺€澹曢柛蹇氭腹缁楀娼诲☉妯哄汲闁告せ鍓濋惈?
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
    // 閻庤绻勯鍫㈠寲閼姐倗鍩犻柨娑欑煯缁海鎮銏犘楅梺顒佹尭閸欏潡鎯冮崟顐㈢３缂佺嫏鍥ｅ亾婢跺﹤骞?
    // ============================================================

    public void TriggerTreasureSlotMachineReward()
    {
        StartCoroutine(TreasureSlotMachineSequence());
    }

    private IEnumerator TreasureSlotMachineSequence()
    {
        Time.timeScale = 0f;

        TreasureSlotReward reward = RollTreasureSlotReward(0);
        TreasureSlotReward lockedEvolutionReward = reward.kind == TreasureRewardKind.Evolution ? reward : default(TreasureSlotReward);
        TreasureSlotMachineUI slotUI = TreasureSlotMachineUI.GetOrCreate();
        yield return slotUI.Play(
            reward,
            rerollCount => RollTreasureSlotReward(rerollCount, lockedEvolutionReward),
            TrySpendTreasureRerollCost,
            GetTreasureRerollCost);

        ApplyTreasureSlotReward(slotUI.CurrentReward);
        yield return new WaitForSecondsRealtime(0.2f);

        slotUI.Hide();
        Time.timeScale = 1f;
    }

    public int GetTreasureRerollCost(int rerollIndex)
    {
        return Mathf.Max(0, treasureRerollBaseCost + Mathf.Max(0, rerollIndex - 1) * treasureRerollCostStep);
    }

    private bool TrySpendTreasureRerollCost(int rerollIndex)
    {
        int cost = GetTreasureRerollCost(rerollIndex);
        if (PlayerProgressManager.Instance == null || !PlayerProgressManager.Instance.CanAfford(cost)) return false;
        PlayerProgressManager.Instance.SpendGold(cost);
        return true;
    }

    private TreasureSlotReward RollTreasureSlotReward(int rerollCount)
    {
        return RollTreasureSlotReward(rerollCount, default(TreasureSlotReward));
    }

    private TreasureSlotReward RollTreasureSlotReward(int rerollCount, TreasureSlotReward lockedEvolutionReward)
    {
        float luck = PlayerStats.Instance != null ? Mathf.Max(0.1f, PlayerStats.Instance.luck) : 1f;
        float rerollBoost = 1f + Mathf.Max(0, rerollCount) * treasureRerollRewardChanceBoost;
        if (lockedEvolutionReward.kind == TreasureRewardKind.Evolution && lockedEvolutionReward.targetWeapon != null)
        {
            TreasureSlotReward preservedEvolution = lockedEvolutionReward;
            PopulateEvolutionSideReels(ref preservedEvolution, lockedEvolutionReward.targetWeapon.weaponName, luck, rerollBoost);
            return preservedEvolution;
        }

        TreasureSlotReward reward = BuildTreasureBaseAttackReward(3);

        if (TryGetAvailableTreasureEvolution(out FusionRecipeSO legacyFusion, out WeaponFusionRecipeSO weaponFusion, out WeaponStatBlock evolutionResult))
        {
            reward = BuildTreasureEvolutionReward(legacyFusion, weaponFusion, evolutionResult, luck, rerollBoost);
            return reward;
        }

        if (WeaponController.Instance == null)
        {
            reward = BuildTreasureBaseAttackReward(3);
            return reward;
        }

        List<OwnedWeapon> skillNodeTargets = WeaponController.Instance.ownedWeapons
            .Where(owned => owned != null
                && owned.stats != null
                && owned.weaponPartInstance != null
                && GetAvailableWeaponSkillNodes(owned.stats).Count > 0)
            .ToList();
        List<OwnedWeapon> upgradeable = skillNodeTargets.Count > 0
            ? skillNodeTargets
            : WeaponController.Instance.GetUpgradeableWeapons();
        if (upgradeable.Count == 0)
        {
            if (TryGetAvailableTreasureEvolution(out legacyFusion, out weaponFusion, out evolutionResult))
            {
                reward = BuildTreasureEvolutionReward(legacyFusion, weaponFusion, evolutionResult, luck, rerollBoost);
                return reward;
            }

            reward = BuildTreasureBaseAttackReward(3);
            return reward;
        }

        int gain = RollTreasureLevelGain(luck, rerollBoost);

        List<SkillTreeNodeData> awardedNodes = skillNodeTargets.Count > 0
            ? PickTreasureWeaponSkillNodesAcrossTargets(skillNodeTargets, gain)
            : new List<SkillTreeNodeData>();
        OwnedWeapon target = ResolveTreasureRewardTarget(upgradeable, awardedNodes);
        if (target == null)
        {
            target = upgradeable[Random.Range(0, upgradeable.Count)];
        }

        reward.kind = TreasureRewardKind.WeaponLevels;
        reward.targetWeapon = target.stats;
        reward.icon = target.stats != null ? target.stats.weaponIcon : null;
        int startLevel = target.currentLevel;
        int maxLevel = WeaponController.Instance.GetMaxLevel(target);
        int remainingLevels = Mathf.Max(0, maxLevel - startLevel);
        int appliedGain = awardedNodes.Count > 0
            ? awardedNodes.Count
            : Mathf.Clamp(gain, 1, Mathf.Max(1, remainingLevels));

        reward.startLevel = startLevel;
        reward.finalLevel = Mathf.Min(maxLevel, startLevel + appliedGain);
        reward.levelGain = appliedGain;
        reward.awardedNodes = awardedNodes.ToArray();
        reward.tryEvolutionAfterLevels = awardedNodes.Count > 0
            ? WillAnyTreasureAwardReachEvolutionLevel(awardedNodes)
            : gain > appliedGain || WillReachTreasureEvolutionLevel(target, reward.finalLevel);
        reward.jackpot = gain >= 3 || reward.tryEvolutionAfterLevels;
        reward.goldReward = 0;
        reward.reelBaseAttackBonuses = BuildBaseAttackReelFlags(appliedGain, reward.tryEvolutionAfterLevels ? 2 : -1);
        reward.baseAttackBonusCount = CountBaseAttackReels(reward.reelBaseAttackBonuses);
        reward.baseAttackBonus = GetTreasurePumpkinBaseAttackBonus() * reward.baseAttackBonusCount;
        string name = target.stats != null ? target.stats.weaponName : "Weapon";
        reward.resultText = appliedGain >= 2 ? $"{name} +{appliedGain}" : $"{name} +1";
        if (reward.finalLevel >= maxLevel) reward.resultText = $"{name} Max";
        reward.detailText = BuildWeaponLevelDetail(name, startLevel, reward.finalLevel, maxLevel, reward.tryEvolutionAfterLevels, awardedNodes);
        reward.reelDetails = BuildWeaponLevelReelDetails(name, startLevel, appliedGain, reward.tryEvolutionAfterLevels, awardedNodes, reward.reelBaseAttackBonuses);
        reward.reelIcons = BuildTreasureReelIcons(reward.icon, awardedNodes);
        reward.reelNames = BuildTreasureReelNames(name, awardedNodes);
        return reward;
    }

    private TreasureSlotReward BuildTreasureBaseAttackReward(int reelCount)
    {
        int count = Mathf.Clamp(reelCount, 1, 3);
        bool[] pumpkinReels = new bool[3];
        for (int i = 0; i < pumpkinReels.Length; i++)
        {
            pumpkinReels[i] = i < count;
        }

        float perReelBonus = GetTreasurePumpkinBaseAttackBonus();
        float totalBonus = perReelBonus * count;
        return new TreasureSlotReward
        {
            kind = TreasureRewardKind.BaseAttack,
            resultText = $"{GetBaseAttackLabel()} +{FormatPercent(totalBonus)}",
            detailText = BuildBaseAttackDetail(count, totalBonus),
            reelDetails = BuildBaseAttackReelDetails(pumpkinReels),
            reelBaseAttackBonuses = pumpkinReels,
            baseAttackBonusCount = count,
            baseAttackBonus = totalBonus,
            evolutionReelIndex = -1,
            levelGain = 0,
            goldReward = 0,
            jackpot = count >= 3
        };
    }

    private int RollTreasureLevelGain(float luck, float rerollBoost)
    {
        int gain = 1;
        float roll = Random.value;
        float tripleChance = Mathf.Clamp01(treasureTripleLevelChance * Mathf.Lerp(1f, luck, 0.4f) * rerollBoost);
        float doubleChance = Mathf.Clamp01(treasureDoubleLevelChance * Mathf.Lerp(1f, luck, 0.35f) * rerollBoost);
        if (roll < tripleChance)
        {
            gain = 3;
        }
        else if (roll < tripleChance + doubleChance)
        {
            gain = 2;
        }

        return gain;
    }

    private TreasureSlotReward BuildTreasureEvolutionReward(
        FusionRecipeSO legacyFusion,
        WeaponFusionRecipeSO weaponFusion,
        WeaponStatBlock evolutionResult,
        float luck,
        float rerollBoost)
    {
        string weaponName = evolutionResult != null ? evolutionResult.weaponName : "Evolution";
        TreasureSlotReward reward = new TreasureSlotReward
        {
            kind = TreasureRewardKind.Evolution,
            legacyFusion = legacyFusion,
            weaponFusion = weaponFusion,
            targetWeapon = evolutionResult,
            icon = evolutionResult != null ? evolutionResult.weaponIcon : null,
            evolved = true,
            jackpot = true,
            resultText = $"Evolution: {weaponName}",
            detailText = BuildEvolutionDetail(weaponName),
            evolutionReelIndex = 1,
            levelGain = 1,
            goldReward = 0
        };

        PopulateEvolutionSideReels(ref reward, weaponName, luck, rerollBoost);
        return reward;
    }

    private void PopulateEvolutionSideReels(ref TreasureSlotReward reward, string weaponName, float luck, float rerollBoost)
    {
        SkillTreeNodeData[] reelNodes = new SkillTreeNodeData[3];
        WeaponStatBlock[] reelLevelWeapons = new WeaponStatBlock[3];
        bool[] baseAttackReels = new bool[3];
        int totalRewardSlots = RollTreasureLevelGain(luck, rerollBoost);
        int sideRewardSlots = Mathf.Clamp(totalRewardSlots - 1, 0, 2);
        HashSet<WeaponStatBlock> fusionSources = BuildFusionSourceSet(reward);

        if (sideRewardSlots > 0 && WeaponController.Instance != null)
        {
            List<OwnedWeapon> skillNodeTargets = WeaponController.Instance.ownedWeapons
                .Where(owned => owned != null
                    && owned.stats != null
                    && owned.weaponPartInstance != null
                    && !ShouldExcludeTreasureSideTarget(owned, fusionSources)
                    && GetAvailableWeaponSkillNodes(owned.stats).Count > 0)
                .ToList();

            List<SkillTreeNodeData> sideNodes = skillNodeTargets.Count > 0
                ? PickTreasureWeaponSkillNodesAcrossTargets(skillNodeTargets, sideRewardSlots)
                : new List<SkillTreeNodeData>();

            List<int> sideIndices = new List<int> { 0, 2 }.OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < sideNodes.Count && i < sideIndices.Count; i++)
            {
                reelNodes[sideIndices[i]] = sideNodes[i];
            }

            int filledSideSlots = sideNodes.Count;
            int remainingSideSlots = Mathf.Clamp(sideRewardSlots - filledSideSlots, 0, 2);
            if (remainingSideSlots > 0)
            {
                List<OwnedWeapon> rawTargets = WeaponController.Instance.GetUpgradeableWeapons()
                    .Where(owned => owned != null
                        && owned.stats != null
                        && !ShouldExcludeTreasureSideTarget(owned, fusionSources))
                    .OrderBy(_ => Random.value)
                    .ToList();

                int rawTargetIndex = 0;
                for (int i = 0; i < sideIndices.Count && remainingSideSlots > 0; i++)
                {
                    int reelIndex = sideIndices[i];
                    if (reelNodes[reelIndex] != null) continue;
                    if (rawTargetIndex >= rawTargets.Count) break;

                    reelLevelWeapons[reelIndex] = rawTargets[rawTargetIndex].stats;
                    rawTargetIndex++;
                    remainingSideSlots--;
                }
            }
        }

        for (int i = 0; i < baseAttackReels.Length; i++)
        {
            bool isEvolution = i == reward.evolutionReelIndex;
            bool hasSideNode = reelNodes[i] != null;
            bool hasRawLevel = reelLevelWeapons[i] != null;
            baseAttackReels[i] = !isEvolution && !hasSideNode && !hasRawLevel;
        }

        reward.awardedNodes = reelNodes;
        reward.reelLevelWeapons = reelLevelWeapons;
        reward.reelBaseAttackBonuses = baseAttackReels;
        reward.levelGain = 1 + reelNodes.Count(node => node != null) + reelLevelWeapons.Count(weapon => weapon != null);
        reward.baseAttackBonusCount = CountBaseAttackReels(baseAttackReels);
        reward.baseAttackBonus = GetTreasurePumpkinBaseAttackBonus() * reward.baseAttackBonusCount;
        reward.jackpot = reward.levelGain >= 3 || reward.evolved;
        BuildEvolutionReels(ref reward, weaponName);
    }

    private HashSet<WeaponStatBlock> BuildFusionSourceSet(TreasureSlotReward reward)
    {
        HashSet<WeaponStatBlock> sources = new HashSet<WeaponStatBlock>();
        if (reward.legacyFusion != null)
        {
            if (reward.legacyFusion.weaponA != null) sources.Add(reward.legacyFusion.weaponA);
            if (reward.legacyFusion.weaponB != null) sources.Add(reward.legacyFusion.weaponB);
        }

        if (reward.weaponFusion != null)
        {
            if (reward.weaponFusion.triggerWeapon != null) sources.Add(reward.weaponFusion.triggerWeapon);
            if (reward.weaponFusion.conditions != null)
            {
                foreach (FusionCondition condition in reward.weaponFusion.conditions)
                {
                    if (condition != null && condition.type == ConditionType.Weapon && condition.requiredWeapon != null)
                    {
                        sources.Add(condition.requiredWeapon);
                    }
                }
            }
        }

        return sources;
    }

    private bool ShouldExcludeTreasureSideTarget(OwnedWeapon owned, HashSet<WeaponStatBlock> excludedSources)
    {
        if (owned == null || excludedSources == null || excludedSources.Count == 0) return false;
        foreach (WeaponStatBlock source in excludedSources)
        {
            if (owned.InheritsSkillSource(source)) return true;
        }

        return false;
    }

    private void BuildEvolutionReels(ref TreasureSlotReward reward, string weaponName)
    {
        Sprite[] icons = new Sprite[3];
        string[] names = new string[3];
        string[] details = new string[3];

        for (int i = 0; i < 3; i++)
        {
            SkillTreeNodeData sideNode = reward.awardedNodes != null && i < reward.awardedNodes.Length
                ? reward.awardedNodes[i]
                : null;

            if (i == reward.evolutionReelIndex)
            {
                icons[i] = reward.icon;
                names[i] = weaponName;
                details[i] = $"{weaponName}\nEvolution reward.";
            }
            else if (sideNode != null)
            {
                icons[i] = sideNode.skillIcon != null ? sideNode.skillIcon : reward.icon;
                names[i] = GetNodeDisplayName(sideNode);
                details[i] = $"{GetNodeDisplayName(sideNode)}\n{GetNodeDescription(sideNode)}";
            }
            else if (reward.reelLevelWeapons != null && i < reward.reelLevelWeapons.Length && reward.reelLevelWeapons[i] != null)
            {
                WeaponStatBlock sideWeapon = reward.reelLevelWeapons[i];
                icons[i] = sideWeapon.weaponIcon != null ? sideWeapon.weaponIcon : reward.icon;
                names[i] = !string.IsNullOrEmpty(sideWeapon.weaponName) ? sideWeapon.weaponName : "Weapon";

                OwnedWeapon owned = WeaponController.Instance != null
                    ? WeaponController.Instance.ownedWeapons.FirstOrDefault(w => w != null && w.InheritsSkillSource(sideWeapon))
                    : null;
                int from = owned != null ? owned.currentLevel : 0;
                details[i] = $"{names[i]}\nLv.{from} -> Lv.{from + 1}";
            }
            else
            {
                icons[i] = null;
                names[i] = GetPumpkinBlessingLabel();
                details[i] = BuildBaseAttackReelDetail();
            }
        }

        reward.reelIcons = icons;
        reward.reelNames = names;
        reward.reelDetails = details;
    }

    private string BuildEvolutionDetail(string weaponName)
    {
        return $"{weaponName}\nEvolution reward unlocked.";
    }

    private string[] BuildEvolutionReelDetails(string weaponName)
    {
        return new[]
        {
            "Locked reel.",
            $"{weaponName}\nEvolution reward.",
            "Locked reel."
        };
    }

    private string BuildWeaponLevelDetail(string weaponName, int startLevel, int finalLevel, int maxLevel, bool tryEvolutionAfterLevels, List<SkillTreeNodeData> awardedNodes)
    {
        string detail = $"{weaponName}\nLv.{startLevel} -> Lv.{finalLevel} / {maxLevel}";
        if (awardedNodes != null && awardedNodes.Count > 0)
        {
            detail += "\n" + string.Join("\n", awardedNodes.Select(node => "+ " + GetNodeDisplayName(node)));
        }
        else
        {
            detail += "\nWeapon level increased.";
        }
        if (tryEvolutionAfterLevels)
        {
            detail += "\nEvolution will be checked after this reward.";
        }
        return detail;
    }

    private string[] BuildWeaponLevelReelDetails(string weaponName, int startLevel, int appliedGain, bool tryEvolutionAfterLevels, List<SkillTreeNodeData> awardedNodes, bool[] baseAttackReels)
    {
        string[] details = new string[3];
        for (int i = 0; i < details.Length; i++)
        {
            if (awardedNodes != null && i < awardedNodes.Count)
            {
                SkillTreeNodeData node = awardedNodes[i];
                details[i] = $"{GetNodeDisplayName(node)}\n{GetNodeDescription(node)}";
            }
            else if (i < appliedGain)
            {
                int from = startLevel + i;
                int to = from + 1;
                details[i] = $"{weaponName}\nLv.{from} -> Lv.{to}";
            }
            else if (tryEvolutionAfterLevels && i == details.Length - 1)
            {
                details[i] = $"{weaponName}\nEvolution check.";
            }
            else if (baseAttackReels != null && i < baseAttackReels.Length && baseAttackReels[i])
            {
                details[i] = BuildBaseAttackReelDetail();
            }
            else
            {
                details[i] = $"{weaponName}\nLocked reel.";
            }
        }
        return details;
    }

    private List<SkillTreeNodeData> PickTreasureWeaponSkillNodes(WeaponStatBlock weaponStats, int requestedCount)
    {
        List<SkillTreeNodeData> picked = new List<SkillTreeNodeData>();
        if (weaponStats == null || upgradeDatabase == null || upgradeDatabase.weaponSkillNodes == null) return picked;

        HashSet<SkillTreeNodeData> simulatedOwned = new HashSet<SkillTreeNodeData>(ownedUpgrades.Keys);
        int targetCount = Mathf.Clamp(requestedCount, 1, 3);
        for (int i = 0; i < targetCount; i++)
        {
            List<SkillTreeNodeData> candidates = GetAvailableWeaponSkillNodesForTreasure(weaponStats, simulatedOwned)
                .Where(node => node != null && !picked.Contains(node))
                .OrderBy(_ => Random.value)
                .ToList();
            if (candidates.Count == 0) break;

            SkillTreeNodeData selected = candidates[0];
            picked.Add(selected);
            simulatedOwned.Add(selected);
        }

        return picked;
    }

    private List<SkillTreeNodeData> PickTreasureWeaponSkillNodesAcrossTargets(List<OwnedWeapon> targets, int requestedCount)
    {
        List<SkillTreeNodeData> picked = new List<SkillTreeNodeData>();
        if (targets == null || targets.Count == 0 || upgradeDatabase == null || upgradeDatabase.weaponSkillNodes == null) return picked;

        HashSet<SkillTreeNodeData> simulatedOwned = new HashSet<SkillTreeNodeData>(ownedUpgrades.Keys);
        HashSet<WeaponStatBlock> usedTargets = new HashSet<WeaponStatBlock>();
        int targetCount = Mathf.Clamp(requestedCount, 1, 3);

        for (int i = 0; i < targetCount; i++)
        {
            List<TreasureNodeCandidate> candidates = BuildTreasureNodeCandidates(targets, simulatedOwned, picked, usedTargets);
            if (candidates.Count == 0)
            {
                candidates = BuildTreasureNodeCandidates(targets, simulatedOwned, picked, null);
            }
            if (candidates.Count == 0) break;

            TreasureNodeCandidate selected = candidates[Random.Range(0, candidates.Count)];
            picked.Add(selected.node);
            simulatedOwned.Add(selected.node);
            if (selected.target != null && selected.target.stats != null)
            {
                usedTargets.Add(selected.target.stats);
            }
        }

        return picked;
    }

    private List<TreasureNodeCandidate> BuildTreasureNodeCandidates(
        List<OwnedWeapon> targets,
        HashSet<SkillTreeNodeData> simulatedOwned,
        List<SkillTreeNodeData> alreadyPicked,
        HashSet<WeaponStatBlock> excludedTargets)
    {
        List<TreasureNodeCandidate> candidates = new List<TreasureNodeCandidate>();
        foreach (OwnedWeapon target in targets.OrderBy(_ => Random.value))
        {
            if (target == null || target.stats == null) continue;
            if (excludedTargets != null && excludedTargets.Contains(target.stats)) continue;

            List<SkillTreeNodeData> nodes = GetAvailableWeaponSkillNodesForTreasure(target.stats, simulatedOwned)
                .Where(node => node != null && (alreadyPicked == null || !alreadyPicked.Contains(node)))
                .OrderBy(_ => Random.value)
                .ToList();

            foreach (SkillTreeNodeData node in nodes)
            {
                candidates.Add(new TreasureNodeCandidate
                {
                    target = target,
                    node = node
                });
            }
        }

        return candidates;
    }

    private OwnedWeapon ResolveTreasureRewardTarget(List<OwnedWeapon> fallbackTargets, List<SkillTreeNodeData> awardedNodes)
    {
        if (WeaponController.Instance != null && awardedNodes != null)
        {
            foreach (SkillTreeNodeData node in awardedNodes)
            {
                if (node == null || node.associatedWeapon == null) continue;
                OwnedWeapon owned = WeaponController.Instance.ownedWeapons
                    .FirstOrDefault(w => w != null && w.InheritsSkillSource(node.associatedWeapon));
                if (owned != null) return owned;
            }
        }

        return fallbackTargets != null && fallbackTargets.Count > 0 ? fallbackTargets[Random.Range(0, fallbackTargets.Count)] : null;
    }

    private bool WillAnyTreasureAwardReachEvolutionLevel(List<SkillTreeNodeData> awardedNodes)
    {
        if (WeaponController.Instance == null || awardedNodes == null || awardedNodes.Count == 0) return false;

        Dictionary<OwnedWeapon, int> gainsByWeapon = new Dictionary<OwnedWeapon, int>();
        foreach (SkillTreeNodeData node in awardedNodes)
        {
            if (node == null || node.associatedWeapon == null) continue;
            OwnedWeapon owned = WeaponController.Instance.ownedWeapons
                .FirstOrDefault(w => w != null && w.InheritsSkillSource(node.associatedWeapon));
            if (owned == null) continue;

            if (!gainsByWeapon.ContainsKey(owned)) gainsByWeapon[owned] = 0;
            gainsByWeapon[owned]++;
        }

        foreach (var pair in gainsByWeapon)
        {
            OwnedWeapon owned = pair.Key;
            int finalLevel = owned.currentLevel + pair.Value;
            if (WillReachTreasureEvolutionLevel(owned, finalLevel)) return true;
        }

        return false;
    }

    private bool WillReachTreasureEvolutionLevel(OwnedWeapon owned, int finalLevel)
    {
        if (owned == null || WeaponController.Instance == null) return false;
        int maxLevel = WeaponController.Instance.GetMaxLevel(owned);
        int evolutionLevel = Mathf.Min(GEM_SLOT_COUNT, Mathf.Max(1, maxLevel));
        return finalLevel >= evolutionLevel;
    }

    private List<SkillTreeNodeData> GetAvailableWeaponSkillNodesForTreasure(WeaponStatBlock weaponStats, HashSet<SkillTreeNodeData> simulatedOwned)
    {
        List<SkillTreeNodeData> result = new List<SkillTreeNodeData>();
        if (weaponStats == null || upgradeDatabase == null || upgradeDatabase.weaponSkillNodes == null) return result;

        foreach (SkillTreeNodeData node in upgradeDatabase.weaponSkillNodes)
        {
            if (node == null) continue;
            bool matchesWeapon = node.associatedWeapon == weaponStats;
            if (!matchesWeapon && WeaponController.Instance != null)
            {
                OwnedWeapon ownedWeapon = WeaponController.Instance.ownedWeapons
                    .FirstOrDefault(w => w != null && w.stats == weaponStats);
                matchesWeapon = ownedWeapon != null && ownedWeapon.InheritsSkillSource(node.associatedWeapon);
            }
            if (!matchesWeapon) continue;
            int nodeMaxLevel = GetEffectiveUpgradeNodeMaxLevel(node);
            if (ownedUpgrades.ContainsKey(node) && ownedUpgrades[node] >= nodeMaxLevel) continue;
            if (simulatedOwned.Contains(node) && nodeMaxLevel <= 1) continue;

            if (node.prerequisites != null && node.prerequisites.Count > 0)
            {
                if (!node.prerequisites.All(p => p != null && simulatedOwned.Contains(p))) continue;
            }
            if (node.mutuallyExclusive != null && node.mutuallyExclusive.Count > 0)
            {
                if (node.mutuallyExclusive.Any(m => m != null && simulatedOwned.Contains(m))) continue;
            }
            if (node.requiredWeapons != null && node.requiredWeapons.Count > 0)
            {
                if (WeaponController.Instance == null) continue;
                if (!node.requiredWeapons.All(rw => rw != null && WeaponController.Instance.ownedWeapons.Any(ow => ow != null && ow.stats == rw))) continue;
            }

            result.Add(node);
        }

        return result;
    }

    private Sprite[] BuildTreasureReelIcons(Sprite fallback, List<SkillTreeNodeData> awardedNodes)
    {
        Sprite[] icons = new Sprite[3];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = awardedNodes != null && i < awardedNodes.Count && awardedNodes[i] != null && awardedNodes[i].skillIcon != null
                ? awardedNodes[i].skillIcon
                : fallback;
        }
        return icons;
    }

    private string[] BuildTreasureReelNames(string fallback, List<SkillTreeNodeData> awardedNodes)
    {
        string[] names = new string[3];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = awardedNodes != null && i < awardedNodes.Count
                ? GetNodeDisplayName(awardedNodes[i])
                : fallback;
        }
        return names;
    }

    private Sprite[] BuildSameIconReels(Sprite icon)
    {
        return new[] { icon, icon, icon };
    }

    private string[] BuildSameNameReels(string name)
    {
        return new[] { name, name, name };
    }

    private string GetNodeDisplayName(SkillTreeNodeData node)
    {
        if (node == null) return "Unknown";
        return !string.IsNullOrEmpty(node.skillName) ? node.skillName : node.name;
    }

    private string GetNodeDescription(SkillTreeNodeData node)
    {
        if (node == null) return "No description.";
        UpgradeOption option = GetTreasureOption(node);
        if (option == null) return "No upgrade effect.";

        string desc = LocalizationManager.CurrentLanguage == SystemLanguage.English && !string.IsNullOrEmpty(option.descriptionEN)
            ? option.descriptionEN
            : option.description;
        return string.IsNullOrEmpty(desc) ? "No upgrade effect." : desc;
    }

    private UpgradeOption GetTreasureOption(SkillTreeNodeData node)
    {
        if (node == null || node.possibleOptions == null || node.possibleOptions.Count == 0) return null;
        return node.possibleOptions.OrderByDescending(opt => opt.rarity).FirstOrDefault();
    }

    private string[] BuildFallbackReelDetails()
    {
        return BuildBaseAttackReelDetails(new[] { true, true, true });
    }

    private bool[] BuildBaseAttackReelFlags(int rewardedSlots, int reservedSlot)
    {
        bool[] flags = new bool[3];
        int safeRewardedSlots = Mathf.Clamp(rewardedSlots, 0, flags.Length);
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = i >= safeRewardedSlots && i != reservedSlot;
        }

        return flags;
    }

    private int CountBaseAttackReels(bool[] flags)
    {
        return flags != null ? flags.Count(flag => flag) : 0;
    }

    private string[] BuildBaseAttackReelDetails(bool[] flags)
    {
        string[] details = new string[3];
        for (int i = 0; i < details.Length; i++)
        {
            details[i] = flags != null && i < flags.Length && flags[i]
                ? BuildBaseAttackReelDetail()
                : string.Empty;
        }

        return details;
    }

    private string BuildBaseAttackDetail(int count, float totalBonus)
    {
        return $"{GetPumpkinBlessingLabel()}\n{GetBaseAttackLabel()} +{FormatPercent(totalBonus)} ({count}x {FormatPercent(GetTreasurePumpkinBaseAttackBonus())})";
    }

    private string BuildBaseAttackReelDetail()
    {
        return $"{GetPumpkinBlessingLabel()}\n{GetBaseAttackLabel()} +{FormatPercent(GetTreasurePumpkinBaseAttackBonus())}";
    }

    private float GetTreasurePumpkinBaseAttackBonus()
    {
        return Mathf.Max(0f, treasurePumpkinBaseAttackBonus);
    }

    private string GetPumpkinBlessingLabel()
    {
        return "\u5357\u74dc\u795d\u798f";
    }

    private string GetBaseAttackLabel()
    {
        return "\u57fa\u7840\u653b\u51fb\u529b";
    }

    private string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private bool TryGetAvailableTreasureEvolution(out FusionRecipeSO legacyFusion, out WeaponFusionRecipeSO weaponFusion, out WeaponStatBlock resultWeapon)
    {
        legacyFusion = null;
        weaponFusion = null;
        resultWeapon = null;

        if (WeaponController.Instance == null) return false;

        legacyFusion = WeaponController.Instance.CheckForAvailableFusion();
        if (legacyFusion != null && legacyFusion.resultWeapon != null)
        {
            resultWeapon = legacyFusion.resultWeapon;
            return true;
        }

        WeaponFusionManager fusionManager = WeaponFusionManager.EnsureInstance();
        if (fusionManager == null) return false;
        foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
        {
            if (owned?.weaponPartInstance == null) continue;
            List<WeaponFusionRecipeSO> recipes = fusionManager.GetAvailableFusions(owned.weaponPartInstance);
            if (recipes.Count <= 0) continue;
            weaponFusion = recipes[Random.Range(0, recipes.Count)];
            resultWeapon = weaponFusion.resultWeapon;
            return resultWeapon != null;
        }

        return false;
    }

    private void ApplyTreasureSlotReward(TreasureSlotReward reward)
    {
        if (reward.kind == TreasureRewardKind.Evolution && WeaponController.Instance != null)
        {
            ApplyTreasureAwardedNodes(reward.awardedNodes);
            ApplyTreasureReelLevelRewards(reward.reelLevelWeapons);

            if (reward.legacyFusion != null)
            {
                WeaponController.Instance.PerformFusion(reward.legacyFusion);
            }
            else if (reward.weaponFusion != null)
            {
                WeaponController.Instance.PerformFusion(reward.weaponFusion);
            }
        }
        else if (reward.kind == TreasureRewardKind.WeaponLevels && WeaponController.Instance != null)
        {
            int applied = 0;
            if (reward.awardedNodes != null && reward.awardedNodes.Length > 0)
            {
                applied = ApplyTreasureAwardedNodes(reward.awardedNodes);
            }
            else
            {
                applied = WeaponController.Instance.GrantWeaponLevels(reward.targetWeapon, reward.levelGain);
                RegisterWeaponSkillGem(reward.targetWeapon, applied);
            }

            if (applied < reward.levelGain || reward.tryEvolutionAfterLevels)
            {
                TryGetAvailableTreasureEvolution(out FusionRecipeSO legacyFusion, out WeaponFusionRecipeSO weaponFusion, out _);
                if (legacyFusion != null)
                {
                    WeaponController.Instance.PerformFusion(legacyFusion);
                }
                else if (weaponFusion != null)
                {
                    WeaponController.Instance.PerformFusion(weaponFusion);
                }
            }
        }

        if (reward.goldReward > 0 && PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddGold(reward.goldReward);
            BattleStatisticsManager.Instance?.AddGold(reward.goldReward);
        }

        if (reward.baseAttackBonus > 0f && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddBaseDamageMultiplier(reward.baseAttackBonus);
        }
    }

    private int ApplyTreasureAwardedNodes(SkillTreeNodeData[] awardedNodes)
    {
        if (awardedNodes == null || awardedNodes.Length == 0) return 0;

        int applied = 0;
        foreach (SkillTreeNodeData node in awardedNodes)
        {
            if (node == null) continue;
            UpgradeOption option = GetTreasureOption(node);
            if (option != null)
            {
                OnUpgradeOptionSelected(node, option);
                applied++;
            }
        }

        return applied;
    }

    private int ApplyTreasureReelLevelRewards(WeaponStatBlock[] reelLevelWeapons)
    {
        if (reelLevelWeapons == null || reelLevelWeapons.Length == 0 || WeaponController.Instance == null) return 0;

        int appliedTotal = 0;
        foreach (WeaponStatBlock weapon in reelLevelWeapons)
        {
            if (weapon == null) continue;
            int applied = WeaponController.Instance.GrantWeaponLevels(weapon, 1);
            if (applied > 0)
            {
                RegisterWeaponSkillGem(weapon, applied);
                appliedTotal += applied;
            }
        }

        return appliedTotal;
    }

    /// <summary>
    /// 闁哄被鍎撮妤呭及椤栨碍鍎婂璺哄缁剛鈧绻勯鍫熷緞濮樺灈鍋撴径瀣嗕礁顕ｈ箛銉х闂侇偄顦悾顒佺▔閳ь剙顕ｉ悩鍙夊€甸弶鈺傤焾閸忔绱掕閻㈠鏌呮径娑氱
    /// UpgradeCardUI 闁革负鍔岄崹浠嬪棘椤撶喐笑闁告熬闄勭拹浼村礄閸濆嫬寰撳ù鐘崇墪瀹曢亶鎮ч崶銊︻槯濞达綀娉曢弫?
    /// </summary>
    public bool HasRemainingTreasurePicks() => remainingTreasurePicks > 0;

    /// <summary>
    /// 濠㈣埖鐗犻崕瀵告喆閿曗偓瑜板倹绂掗崨顓炵樁闁告凹鍋夐～锕傚礉閵娾晙澹曢柛蹇涱棑濞堟垿宕￠崶鈺呯崜闂侇偄顦畷閬嶆晬閸繄鏉虹紒鐘洪哺鐎ｂ偓闁告瑦鐗旀繛鍥偨椤帞绀?
    /// </summary>
    /// <param name="allowedPicks">闁哄牜鍓氶濂稿矗椤栫偐鍋撴径濠傚耿闁绘娲﹂弳鐔兼煂韫囥儳绀?=闁哄秴娲ら崳顖炴晬?=闁告瑥鑻埀顒€绋勭槐?=闁稿繈鍔戦埀顒€顧€缁辨岸鏁嶅畝鍕笡閻?鐎?/param>
    public void TriggerPassiveOnlyUpgrade(int allowedPicks = 1)
    {
        // 閻犱礁澧介悿鍡涘礈閳衡偓缂嶆垿宕ｉ鐐╁亾婢跺鍋ч柡浣稿簻缁辨瑩宕?闁哄嫷鍨板ú婊勭▔閾忚鍎戝☉鎾亾婵炲棴绻濋埀顒€顦扮€氥劍绋夊鍡櫺ラ柤鐗堫殕椤掓繄鎷嬮埄鍐╂闁?
        remainingTreasurePicks = Mathf.Max(0, allowedPicks - 1);
        Debug.Log("[UpgradeManager] log.");
        StartCoroutine(PassiveOnlyUpgradeSequence());
    }

    /// <summary>
    /// 濞寸姴鎳撻～锕傚礉閵娾晙澹曢柛蹇涱棑濞堟垿宕￠崶鈺呯崜闂侇偄顦畷鍗灻规担琛℃煠
    /// </summary>
    private IEnumerator PassiveOnlyUpgradeSequence()
    {
        // 1. 閺夆晜绋戦崣鍡涘箞閵忕姴袟濞?
        Time.timeScale = levelUpSlowMotion;

        // 2. 缂佹稑顦欢鐔兼偋鐟欏嫭娅?
        yield return new WaitForSecondsRealtime(levelUpVfxDelay);

        // 3. 閻庣懓鑻崣蹇涘汲閸屾矮绮?
        Time.timeScale = 0f;
        offeredUpgrades.Clear();

        // 4. 濞寸姴鎳嶇划鐘垫偖椤愩垹袟闂侇剚鎸搁崣鍨ч悩杈幀闁规儼妫勮ぐ?鐎殿喚濮村畷?
        List<SkillTreeNodeData> validPassives = new List<SkillTreeNodeData>();

        // 濞?PlayerStats 闁兼儳鍢茶ぐ鍥儑閻旈鏉介柣銊ュ椤箓宕濋妸鈺€澹曢柛蹇涙敱鐎垫棃寮垫径瀣闂?
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
                int nodeMaxLevel = GetEffectiveUpgradeNodeMaxLevel(node);
                bool notMaxed = !ownedUpgrades.ContainsKey(node) || ownedUpgrades[node] < nodeMaxLevel;

                // 闁靛棙鍔曞ù姗€鏌庣壕瀣撴帡鏌ㄦ担鐣岀畺婵犲鍊戦埀顒佸灦濠€顓犳喆閿濆鏁氶柣銊ュ椤箓宕濋妸鈺€澹曢柛蹇氭腹缁楀娼诲☉妯哄汲閻庤绻勯鍫ュ础閳╁啰娼?
                if (!prerequisitesMet || !notMaxed || !IsPassiveNodeUnlocked(node)) continue;

                // 闁靛棙鍔栬潕濞达絽绉崇粭鍌炴⒔閹邦垳绠栨繝濞垮€戦埀顒佸灥閸戯繝寮?缂佸绉崇粭澶愬触瀹€鈧▓鎴犳偖椤愩垹袟闂侇剚鎸搁崣鍧楀籍鐠佸湱绀夐柛娆樹簻閸樻垹鎷嬬粙鍨殥闁规灚鍎插﹢渚€鏁嶉崼婵嗚闁告娲ㄦ鍥晬婢跺本鐣遍梺顒佹尭閸欏潡宕欓搹鐟扮疀
                if (currentUniquePassiveCount >= maxPassiveSlots)
                {
                    bool alreadyOwned = ownedUpgrades.ContainsKey(node);
                    if (!alreadyOwned) continue; // 闁哄倷鍗虫禍楣冨礂閾氬倻鐟濋柛鎰Т閸ゎ參鎮?
                }

                validPassives.Add(node);
            }
        }

        // 闁瑰灚鎸风拹锟犵嵁鐠哄搫绲块柛?濞?
        var shuffledPassives = validPassives.OrderBy(a => Random.value).ToList();
        int slotsToFill = Mathf.Min(3, shuffledPassives.Count);
        for (int i = 0; i < slotsToFill; i++)
        {
            offeredUpgrades.Add(shuffledPassives[i]);
        }

        Debug.Log("[UpgradeManager] log.");

        // 5. 濠碘€冲€归悘澶娾柦閳╁啯绠掗柛娆樺灣閺併倝鎯冮崟顕呮蕉闁告柣鍔戞禍楣冨礂閸戙倗绀夐柣鈺佺摠鐢挳骞侀姀鐙€妲绘繛鎾虫啞閸?
        if (offeredUpgrades.Count == 0)
        {
            Debug.LogWarning("[UpgradeManager] warning.");
            Time.timeScale = 1f;
            yield break;
        }

        // 6. 闁哄嫬澧介妵姘跺础閿涘嫬顣籙I
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCardUIs.Clear();
        // 閻庤绻勯鍫ユ焻婢跺﹤骞㈤柨娑欎亢椤旀洜绱旈鐣屽灱濡増锚閹锋媽銇愰埡浣烘暔闁绘顫夐弲?
        int totalPicks = remainingTreasurePicks + 1; // 鐟滅増鎸告晶鐘诲矗椤栫偐鍋撴径瀣у亾缂佹ɑ娈?
        SetUpgradePanelTitle(totalPicks);
        upgradePanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    /// <summary>
    /// 闁哄秷顫夊畵渚€宕ｉ鐐╁亾婢跺﹦鐐婇柡浣瑰椤旀洜绱旈鈧浼村级閹稿海鍨煎Λ鐗埳戦弸鍐偓娑欘殔閹锋媽銇愰埡浣烘暔闁绘顫夐弲?
    /// </summary>
    private void SetUpgradePanelTitle(int allowedPicks)
    {
        // 閻犱礁澧介悿鍡涘冀閸ヮ剦鏆柡鍌氭搐閻?
        if (titleText != null)
        {
            switch (allowedPicks)
            {
                case 3:
                    titleText.text = "Choose Reward";
                    break;
                case 2:
                    titleText.text = "Choose Reward";
                    break;
                default:
                    titleText.text = "Choose Reward";
                    break;
            }
        }

        // 婵犵鍋撴繛?闁稿繑濞婂Λ纾嬨亹閳轰胶鏁ㄩ柣妤勵潐閺呫儵鏁嶉崼婊冣枏闁烩懇鏅硁scaled Time闁挎稑濂旂粭澶愬矗濡や焦鐣柛瀣矊婵傛牠宕蹇曠
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
    /// 閻犱礁澧介悿鍡涙儎椤旂晫鍨奸柣妞绘櫃缂嶅绋夋繝鍐暡闁?ParticleImage 濞达綀娉曢弫銈嗙▔瀹ュ懎缍€ Time.timeScale 鐟滄澘宕幖鐑芥儍閸曨剚顦ч梻?
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
    /// 婵☆偀鍋撻柡灞诲劥椤箓宕濋妸鈺€澹曢柛蹇曟焿婵☆參鎮欑憴鍕﹂柛姘剧畱閸戯繝鏌呭宕囩畺闁搞儵绠栨竟宀€鎲撮敐澶嬫暁
    /// 濠碘€冲€归悘澶愭嚍閸屾粌浠繛灞稿墲濠€渚€宕楃€圭姳绮?PassiveItemData闁挎稑鏈崹銊╂嚀閸涚繝澹曢柛蹇涙敱濡插憡顪€濡鍚囬悷娆欑秮閺€锝夋儍閸曞墎绀夐柛鎺撶懆椤绋夐崫鍕殥閻熸瑱缍侀弨?
    /// </summary>
    private bool IsPassiveNodeUnlocked(SkillTreeNodeData node)
    {
        if (node == null || node.possibleOptions == null) return true;

        // 闂侇剙绉村濠氭嚍閸屾粌浠柣銊ュ婢у秹寮垫径鎰ㄥ亾婢舵劑鈧秹鏁嶇仦鍓у弨闁瑰灚鍎抽崣褔鎳曢弮鍌涚暠 PassiveItemData
        foreach (var option in node.possibleOptions)
        {
            if (option.effects == null) continue;
            foreach (var effect in option.effects)
            {
                PassiveItemData passiveData = effect.passiveItemData;
                if (passiveData == null) continue;
                if (!DemoContentGate.IsPassiveAllowed(passiveData)) return false;

                // 濮掓稒顭堥鑽ゆ喆閿濆鏁氶柣銊ュ娴滈箖宕楁搴㈢函闁规亽鍎甸埀顒佷亢缁?
                if (passiveData.isDefaultUnlocked) return true;
                if (PlayerProgressManager.Instance != null
                    && (PlayerProgressManager.Instance.IsItemUnlocked(passiveData.itemName)
                        || PlayerProgressManager.Instance.IsItemUnlocked(passiveData.name)))
                {
                    return true;
                }

                // 闂傚洠鍋撻悷鏇氱劍閸ㄦ氨浜告潏顐嶆帡鏌ㄦ担鐑樼暠闂侇剚鎸搁崣鍧楁晬濮橆収姊鹃柡灞诲劚缂嶅宕滃鍫㈢閹?
                if (!string.IsNullOrEmpty(passiveData.unlockStatKey) && passiveData.unlockThreshold > 0)
                {
                    if (PlayerProgressManager.Instance != null)
                    {
                        int currentVal = 0;
                        if (PlayerProgressManager.Instance.achievementStats.ContainsKey(passiveData.unlockStatKey))
                        {
                            currentVal = PlayerProgressManager.Instance.achievementStats[passiveData.unlockStatKey];
                        }
                        // 闁哄牜浜ｉ幓顏堝礆娴煎瓨顫岄柛?闁?闁哄牜浜ｈ闂?
                        if (currentVal < passiveData.unlockThreshold) return false;
                    }
                    else
                    {
                        // PlayerProgressManager 濞戞挸绉撮悺銊╁捶閵婏附顦ч柨娑樻湰濡倕鈻旈弴鐐茬伈闁哄偆鍙忕槐婵囩┍濠靛棛鏆撻弶鈺傛煥濞?false
                        return false;
                    }
                }
                else
                {
                    // 婵炲备鍓濆﹢浣烘媼閸撗呮瀭 unlockStatKey 濞?isDefaultUnlocked 濞?false 闁?闁哄牜浜ｈ闂?
                    return false;
                }
            }
        }

        // 婵炲备鍓濆﹢渚€骞嶉幆褍鐓傚ù鐘侯唺缂?PassiveItemData 闁?閻熸瑥妫旂拹鐔兼焻濮樿鲸鏆忛柤鍝勫€婚崑锝夋晬鐏炶棄甯掗悹浣侯焾閸欏棗效?
        return true;
    }

    #endregion

    #region === 閻熸瑦甯熸竟濠冪▔閹惧磭娼ｉ柟鍨涘亾闁煎疇妫勫畷杈╁寲閼姐倗鍩?===

    /// <summary>
    /// 闁告帗绻傞～鎰板礌閺嶎剦娼￠柤纭呭紦缁楁挾浠﹂悙鎻掑耿婵湱濯寸槐鎵嫚鐠囨彃绲跨憸鐗堟尭婢х姷鎲撮幒鏇烆棌鐎规瓕灏闂佸じ鑳跺▓?layer 2+ 闁煎搫鍊婚崑锝夋晬鐏炵偓鏆梻鍡楁閸櫻囨嚂閺冨倹鐣遍柛妤嬬磿婢?
    /// </summary>
    private void InitCharacterCardPool()
    {
        characterCardPool.Clear();
        runtimeRoleMilestoneCards.Clear();
        activeCharacterSkills.Clear();

        if (PlayerProgressManager.Instance == null || DataManager.Instance == null) return;

        CharacterData charData = DataManager.Instance.selectedCharacter;
        if (charData == null || charData.characterSkillNodes == null) return;

        AddRuntimeRoleMilestoneCards(charData);

        foreach (var node in charData.characterSkillNodes)
        {
            if (node == null) continue;
            // 闁告瑯浜濆﹢?layer 2+ 濞戞挻鏌ㄩ崙锛勬喆閿濆鏁氬☉鎾存閸樸倗绱旈鑽ゅ晩闁稿繐鐤囨禒鍫ュ础閿涘嫬顣婚柣銊ュ婵☆參鎮欑憴鍕枀闁告梻濮撮崣鍡涘础閳╁啰娼?
            if (node.layer >= 2
                && node.linkedUpgradeNode != null
                && PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node))
            {
                characterCardPool.Add(node.linkedUpgradeNode);
                Debug.Log("[UpgradeManager] log.");
            }
        }

        Debug.Log("[UpgradeManager] log.");
    }

    private void AddRuntimeRoleMilestoneCards(CharacterData charData)
    {
        if (charData == null) return;

        if (charData.characterID == "Role02")
        {
            AddRuntimeRoleCard(
                "Mage_EnergyBloom",
                "Mana Surge",
                "Energy gain +50%. Press Ultimate at full energy to cast pulsing arcane bursts.",
                "Mana Surge",
                "Energy gain +50%. Press Ultimate at full energy to cast pulsing arcane bursts.",
                charData.characterIcon);
            AddRuntimeRoleCard(
                "Mage_ArcaneNova",
                "Arcane Nova",
                "Energy cast gains a third pulse and wider burst coverage.",
                "Arcane Nova",
                "Energy cast gains a third pulse and wider burst coverage.",
                charData.characterIcon);
        }
        else if (charData.characterID == "Role01")
        {
            AddRuntimeRoleCard(
                "Sword_BladeMomentum",
                "Blade Momentum",
                "Every 30 seconds without health damage grants Blade Focus. Each stack increases slash area; taking health damage removes one stack.",
                "Blade Momentum",
                "Every 30 seconds without health damage grants Blade Focus. Each stack increases slash area; taking health damage removes one stack.",
                charData.characterIcon);
            AddRuntimeRoleCard(
                "Sword_SurgeMastery",
                "Blade Surge",
                "Blade Focus stacks up to 3. The first two stacks grant +30% slash area each; the final stack grants +50%.",
                "Blade Surge",
                "Blade Focus stacks up to 3. The first two stacks grant +30% slash area each; the final stack grants +50%.",
                charData.characterIcon);
        }
        else if (charData.characterID == "Role03")
        {
            AddRuntimeRoleCard(
                "Engineer_ScrapRecycler",
                "Scrap Recycler",
                "Parts gained from enemies are doubled. Full parts auto-modify an engineering weapon.",
                "Scrap Recycler",
                "Parts gained from enemies are doubled. Full parts auto-modify an engineering weapon.",
                charData.characterIcon);
            AddRuntimeRoleCard(
                "Engineer_OverclockWorkshop",
                "Overclock Workshop",
                "Modifications require fewer parts and grant an extra weapon level.",
                "Overclock Workshop",
                "Modifications require fewer parts and grant an extra weapon level.",
                charData.characterIcon);
        }
    }

    private void AddRuntimeRoleCard(string skillId, string name, string description, string nameEN, string descriptionEN, Sprite icon)
    {
        if (skillId == "Sword_BladeMomentum")
        {
            description = "\u6bcf30\u79d2\u672a\u53d7\u4f24\u83b7\u5f971\u5c42\u5251\u52bf\u3002\u6bcf\u5c42\u63d0\u9ad8\u65a9\u51fb\u8303\u56f4\uff0c\u53d7\u4f24\u4f1a\u51cf\u5c111\u5c42\u3002";
            descriptionEN = "Every 30 seconds without health damage grants Blade Focus. Each stack increases slash area; taking health damage removes one stack.";
        }

        SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();
        node.skillName = LocalizationManager.CurrentLanguage == SystemLanguage.English ? nameEN : name;
        node.skillIcon = icon;
        node.maxLevel = 1;
        node.isOneTimeOnly = true;
        node.possibleOptions = new List<UpgradeOption>();

        UpgradeOption option = new UpgradeOption
        {
            description = description,
            descriptionEN = descriptionEN,
            rarity = Rarity.Epic,
            effects = new List<UpgradeEffect>
            {
                new UpgradeEffect
                {
                    actionType = EffectActionType.ActivateCharSkill,
                    skillIdentifier = skillId
                }
            }
        };
        node.possibleOptions.Add(option);

        runtimeRoleMilestoneCards.Add(node);
        characterCardPool.Add(node);
    }

    /// <summary>
    /// 闁兼儳鍢茶ぐ鍥嫉椤掆偓閻剟宕ｉ婊勬殢闁汇劌瀚～妤呮嚌閹绘帒骞㈤柨娑樼墛鐢捇姊介妶鍛殥婵犵鍋撴繛鑼跺吹濞堟垶绋夐埀顒€鈻庨埄鍐ｅ亾瑜嶅畷閬嶆晬?
    /// </summary>
    private List<SkillTreeNodeData> GetAvailableCharacterCards()
    {
        List<SkillTreeNodeData> available = new List<SkillTreeNodeData>();

        foreach (var card in characterCardPool)
        {
            if (card == null) continue;

            // 濞戞挴鍋撴繛鍡忓墲閳ь儸鍐ㄥ耿闁绘娴勭槐鏉款啅閸欏璐熸繛鑼额嚙閸垱绋夊鍛櫃闁告垼娅ｉ獮?
            if (card.isOneTimeOnly && ownedUpgrades.ContainsKey(card)) continue;

            // 閻犲搫鐤囩换鍐啅閺屻儮鍋撳宕囩畺 ForceActivateCharacterSkill 闁煎浜滄慨鈺佲攽閳ь剙煤閼姐倖鐣遍柟鍨涘亾闁煎疇妫勫畷?
            // 闁挎稑鐗嗛々褍鈻旈弴鐐电憥闁?IcePath/FirePath 闁告帒妫欓弫顕€鏌呮径瀣仴闁告せ妲勭槐婵嬪箣濡粯鐏嶇€殿喒鍋撳┑顔碱儐濡炲倸顔忛懠璺烘闁告柣鍔庨弫鎾诲极閸剛绀?
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

            // 婵☆偀鍋撻柡灞诲劜濡叉悂宕ラ敃鈧崙鈩冩綇閻愵剚浠樺鍫嗗懐鎼肩紒?
            if (ownedUpgrades.ContainsKey(card) && ownedUpgrades[card] >= GetEffectiveUpgradeNodeMaxLevel(card)) continue;

            // 缂備礁瀚幃搴ㄥ箮閳ь剟宕￠敍鍕暬闁挎稒鑹剧换鈧銈堫嚙閹捇寮幆閭︽濠㈣泛娲︽晶宥夊嫉婢跺鐦归悗瑙勭椤掔喖宕抽妸锕€顤呴柛鎴ｆ楠?
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
    /// 闁哄被鍎撮妤呭嫉椤掆偓閻剟寮伴姘剨鐎圭寮剁缓鍝劽虹紒妯煎帣濞戞搩浜ｉ～妤呮嚌閸欏螚闁艰櫕鏋荤槐娆撳箣濡粯鐏嶇紒顖濆吹缁儤鎷呯捄銊︽殢闁?
    /// </summary>
    public bool HasActiveCharacterSkill(string skillIdentifier)
    {
        if (string.IsNullOrEmpty(skillIdentifier)) return false;
        return activeCharacterSkills.Contains(skillIdentifier);
    }

    /// <summary>
    /// 鐎殿喖鎼崺妤€鈹戦埀顒€煤鐠佸磭顏卞☉鎿冧海椤鎳濋崣澶娢楅柤铏灮缁辨瑦绗熷☉娆忕仜闁哄倹顨呴崹鍨叏鐎ｎ亜顕уù锝堟硶閺併倝鏁嶅畝鍐劜閺夆晛娲︽繛濠囧础閳╁啰銈︾紒瀣儜缁?
    /// 闁活潿鍔嬬花?IcePath/FirePath 缂佹稑顦崹搴ㄥ绩椤栫偐鍋撴径瀣仴闁瑰灈鍋撻柤宕囨櫕濞堟垿鎳涢鍕楁繝纰樺亾婵?
    /// </summary>
    public void ForceActivateCharacterSkill(string skillIdentifier)
    {
        if (string.IsNullOrEmpty(skillIdentifier)) return;
        if (!activeCharacterSkills.Contains(skillIdentifier))
        {
            activeCharacterSkills.Add(skillIdentifier);
            Debug.Log("[UpgradeManager] log.");
        }
    }

    /// <summary>
    /// 闁告帇鍊栭弻鍥础閿涘嫬顣婚柡鍕靛灠閹焦绋夐崫鍕€婚柡鈧娑欑皻闁告帟娉涘畷閬嶆晬閸垻缈遍柛鎴濇閺屸偓闁?闁轰礁绻戝畵搴ㄦ偖鎼淬垹顤侀柨娑橆檧缁辨繈鎮介妸銈囪壘濞村吋锚閸樻盯骞掗幒鎴犵
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
    /// 闁规儼妫勯崺宀€鎲撮幒鏇烆棌闁告せ鍓濆鍌炴晬鐏炲墽鍙€闁瑰灚鍎抽顔芥償閺冨倹鐣?CharacterSkillNode 妤犵偠娉涚花鏌ユ偨閵娿儱寰撻悘鐐靛仦閳ь儸鍕珡闁?
    /// </summary>
    private void ApplyCharacterNodeEffectsForSkill(string skillIdentifier)
    {
        // 閻熸瑦甯熸竟濠囧础閿涘嫭鐣遍柡浣哥墛閻忓宕ｉ鍛﹂柕鍡楁湰缁哄搫煤缂佹鍘欏☉鎿冧簼濠р偓闁告帟缈伴埀顒€绋勭槐娆愪繆閸屾稓鏆欑憸鎷岀簿缁绘盯寮埞搴撳亾娴ｇ顨涢柛鎺戞闂娾晝绮垫径娑氱
        // 闁哄牆鎼崺妤€鈹戦埀顒€煤婵犳埃鍋撳宕囩畺 HasActiveCharacterSkill(skillIdentifier) 闁哄被鍎撮?
        // 闁煎搫鍊婚崑锝嗙▔婵犲嫭鐣?PermanentUpgradeEffect闁挎稑鐗嗛々?DamagePercent闁挎稑顦板Σ鎼佸箮閳ь剟鎳楅懞銉у煇闁汇劌瀚鍫熺▕閸涱厾娼ｉ柟?
        // 鐎瑰憡褰冨﹢?RecalculateCharacterBonuses 濞戞搩鍘奸ˇ鈺呮偠閸☆厾绀夊☉鎾崇Т缁ㄦ煡宕烽妸锔诲妰闂佹彃绉撮ˇ鍙夋償閺冨倹鏆?

        Debug.Log("[UpgradeManager] log.");

        // 闁瑰灈鍋撻柤瀹犳閸戯繝鏌呭宕囩畺 ActivateCharacterSkill() 婵炲鍔岄崬浠嬪礆?activeCharacterSkills 濞?
        // 闁瑰瓨蓱閺嬬喐绂掗敐鍥╁灣闂侇偅淇虹换?HasActiveCharacterSkill() 婵☆偀鍋撻柡灞诲劜濡叉悂宕ラ敃鈧幆搴ㄦ偨?
    }

    #endregion
}
