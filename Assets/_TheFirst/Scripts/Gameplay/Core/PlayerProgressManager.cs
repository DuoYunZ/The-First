using UnityEngine;
using System.Collections.Generic;
using System.IO; // Required for file operations
using System.Linq; // Required for HashSet to List conversion


public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    // 闁诲氦顫夐惌顔剧不閻斿吋鍋嬮柍杞扮劍閹倿鎮峰▎娆戠М闁衡偓閿濆棛顩查悗锝傛櫆椤?
    public static event System.Action<string> OnItemUnlocked;

    [Header("Global Config")]
    public List<WeaponSkillTree> allSkillTrees;

    [Header("Player Data")]
    public int startingGold = 1000; // 缂傚倷鐒﹂悷銈囪姳閿濆棛鈻旈柍褜鍓氱粋宥呪槈濡櫣浠存繝娈垮枛椤戝宕抽崜褎鏆滃ù锝囧劋閺嗗繐霉濠婂啫顒㈢紒浣割嚟閹?
    public int currentGold;

    public Dictionary<string, int> progressStats = new Dictionary<string, int>();
    public Dictionary<string, int> achievementStats = new Dictionary<string, int>();
    private readonly HashSet<string> unlockedAchievementIDs = new HashSet<string>();
    private const float AchievementStatsAutoSaveInterval = 5f;
    private bool achievementStatsDirty;
    private float nextAchievementStatsAutoSaveTime;

    public List<string> unlockedItems = new List<string>();
    // 缂傚倸鍊归幐鎼佹偤閵娧€鍋撳☉娅虫垿濡撮崒娑氣枖妞ゆ挴妲呴崵銏ゆ煕濞嗘劗澧辨繛鍫熷灩閹叉挳骞掗弴鐑嗘闂備緡鍋勯ˇ鎵偓姘ュ姂閺佸秹宕奸敍鐗堝浮瀹?Awake 闂佸搫鍟冲▔娑氳姳椤撱垺鈷掓い鏇楀亾妞わ絼绮欓弫宥団偓娑樼暜taManager 闂佸憡鐟崹鐢稿礂濮椻偓瀵彃顫濋銈堝 PPM 闂佸憡甯楃换鍌烇綖閹版澘绀岄柡宥囨暩缁€?
    [HideInInspector] public string savedSelectedCharacterID;
    // 婵炶揪缍€濞夋洟寮妶鍡欌枖闁逞屽墯缁嬪鎮—顪筯Set闂佸搫顦崕閬嶆偤閵娾晛纾奸柕濞垮劚閸ゆ帡鎮峰▎娆戠М闁衡偓閿濆鍤嶉柛灞剧矊娴狀垶鏌ｉ妸銉ユD (婵炴垶姊婚崰搴☆潩閵娾晛鍙婃い鏍ㄦ皑閺嗗﹤霉閻欌偓閸撴稑鈻撻幈顡﹔iptableObject闂佸搫鍊稿ú锝呪枎閵忋倕瑙?
    // HashSet闂佹眹鍔岀€氼參鎮￠敓鐘茬濡わ絽鍟犻崑鎾绘偄瀹勯偊鍞洪梻鍌氱墢閸嬫盯鎮ラ幆鎵杸?
    private HashSet<string> unlockedNodeIDs = new HashSet<string>();

    public bool IsNodeUnlockedRaw(string nodeID)
    {
        return unlockedNodeIDs.Contains(nodeID);
    }

    [Header("Permanent Stat Bonuses")]
    public int permanentFlatDamageBonus = 0;
    public int permanentMeleeAoeFlatDamageBonus = 0;
    public float permanentDamagePercentBonus = 0f;
    public float permanentFireRateBonus = 0f;

    [Header("Character Skill Tree Bonuses")]
    public int permanentMaxHealthBonus = 0;
    public float permanentArmorBonus = 0f;
    public float permanentMoveSpeedBonus = 0f;
    public float permanentCooldownReduction = 0f;
    public float permanentEnergyGainBonus = 0f;
    public float permanentLifeStealPercent = 0f;
    public float permanentCharDamagePercentBonus = 0f; // 闁荤喐鐟︾敮鐔哥珶婵犲洤绠柍褜鍓熼幊妤呮嚍閵壯冪厙闂佹眹鍔岀€氼參寮ィ鍐ㄧ閻犲洦褰冮～鏃堟煟瑜庨崕鎶藉垂鎼粹寬鎺楀棘閸噮娼遍梺鐟扮摠閸斞呮濞嗘挻鍋戞い鎺戝€昏ぐ灞矫瑰鍐劉妞ゆ帞鍠栧畷鎶藉Ω閿曗偓铻氶梺鐓庣枃婵倝鎮ラ弻銉︽櫖?

    [System.Serializable]
    private class SaveData
    {
        public int savedGold;
        public List<string> savedUnlockedNodeIDs;
        public int savedFlatDamageBonus;
        public int savedMeleeAoeFlatDamageBonus;
        public float savedDamagePercentBonus;
        public float savedFireRateBonus;

        // --- 闂侀潧妫欓崝妞剧昂闂傚倸瀚ㄩ崐鎴﹀焵椤掍礁鐏辨い顐ｎ殜閹虫繈宕ｆ径濞㈡鏌ょ€圭姴袚闁绘牞灏欐禒锕傛倷缁懓浜剧憸宀€绮径鎰鐎广儱瀚粻浠嬫倵濞戞鎴﹀汲閻旂厧纾圭痪顓㈩棑缁€澶愭煛閳ь剟鏌呭☉婊咁槹婵炲濮存鎼佸礄閿涘嫭鍠嗛柨婵嗩槹閺佹岸鏌ら崫鍕偓濠氬磻閿濆绀夐柕濠忚吂閸嬫挻鎷呯粵瀣倎缂?---
        // 闂佸憡甯炴繛鈧繛?savedMaxHealthBonus, savedArmorBonus 缂備焦绋戦ˇ顖炴偤瑜嶉埢?

        // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬪鍛啍闁诲孩绋掗…鍥ㄦ櫠閸ф浼犲ù锝呭建閹烘鐓ュù锝囨櫕缁犲骞?---
        public List<string> savedUnlockedItems;

        // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬪鍛啍闁诲孩绋掕摫闁搞劍姘ㄦ禍鍛婃綇椤愵偄鎮侀梺?(闁诲孩绋掗〃鍛村触閳ь剟鏌熷畡鐗堫棞闁搞劌瀛╃粙澶嬫姜閹峰备鎷℃繛鎴炴尵鐎氱珨st婵烇絽娲︾换鍌炴偤? ---
        public List<string> savedStatKeys;
        public List<int> savedStatValues;
        public List<string> savedUnlockedAchievementIDs;

        // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬪鍛啍闁诲孩绋掗敋缂傚秴顑夊畷婊冾吋婢跺棗浜惧璺侯槼閸橆剟鏌ｉ妸銉ヮ伂妞ゎ偅顨婇幊婵嬫儓閻?---
        public string savedSelectedCharacterID;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
            AutoPopulateSkillTreesInEditor();
#endif

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
#if UNITY_EDITOR
        AutoPopulateSkillTreesInEditor();
#endif
        ValidateRetroactiveUnlocks();
        // 濠电偞鎸搁幉锟犲垂濞嗘劗鈻旈柍褜鍓欓锝夊焵椤掍焦鍙忛悗锝呭缁€澶愭倶韫囨岸鍝烘繛鎻掓健瀵剚锛愭担渚紘濠电偛妫寸换婵嬪闯閸撗勬殰濞达綀顫夐埢鏃傜磼閳?

        int currentIgnite = achievementStats.ContainsKey("Ignite_Count") ? achievementStats["Ignite_Count"] : 0;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }

        AchievementManager.Instance?.SyncFromProgress(this);
    }

    void Update()
    {
        if (achievementStatsDirty && Time.unscaledTime >= nextAchievementStatsAutoSaveTime)
        {
            FlushAchievementStatSave();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            FlushAchievementStatSave();
        }
    }

    void OnApplicationQuit()
    {
        FlushAchievementStatSave();
    }
    // --- 闂備礁寮堕崹鐢电博閻㈢數涓嶉柨娑樺閸?---
#if UNITY_EDITOR
    private void AutoPopulateSkillTreesInEditor()
    {
        if (allSkillTrees == null)
        {
            allSkillTrees = new List<WeaponSkillTree>();
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponSkillTree", new[] { "Assets/_TheFirst/Prefabs/Skill Tree" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            WeaponSkillTree tree = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponSkillTree>(path);
            if (tree != null && !allSkillTrees.Contains(tree))
            {
                allSkillTrees.Add(tree);
            }
        }
    }
#endif

    public bool CanAfford(int amount)
    {
        return currentGold >= amount;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        // 闂侀潧妫欓崝鎺楀箞閵娾晛缁╂鐐茬氨閸嬫捇骞嬪┑鍥惈闂備礁寮堕崹鐢电博閸偅娅犻柣鎰絻椤綁鏌￠崘顓у晣缂佽鲸绻堝瀵糕偓娑櫳戦悡鈧琔I闂佸搫瀚晶浠嬪Φ?
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }

    public void SpendGold(int amount)
    {
        currentGold -= amount;

        // 闂侀潧妫欓崝鎺楀箞閵娾晛缁╂鐐茬氨閸嬫捇骞嬪┑鍥惈闂備礁寮堕崹鐢电博閻㈠憡鍤嶉弶鍫亜閻庮參鏌￠崘顓у晣缂佽鲸绻堝瀵糕偓娑櫳戦悡鈧琔I闂佸搫瀚晶浠嬪Φ?
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }

    // --- 闂佺懓鐏堥崑鎾绘煠閸愭祴鍋撻悢鑲烘繈鏌ｉ幇顒佽础妞ゆ挻鎮傞幃?---
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

                // --- 闂侀潻璐熼崝蹇曟崲閺嶎厽鐓傞悘鐐电摂濡查亶鏌ｉ悙鍙夘棏闁逞屽墯缁矂宕规径濠庢僵闁哄倽銆€閸嬫挸鈹戦崼銏℃儯闂佸搫鐗嗛幖顐﹀春濡ゅ懎绠戠憸鎴ｎ暰闂?---
                case PermanentUpgradeType.UnlockBladeEnergyProjectile:
                    // 闁哄鏅滈悷褔锝為幒妤€鐏虫繝濠傚暟绾惧鏌涜箛鏃€鐏遍柍褜鍓氱换鍌炴倵閻戣姤鍎嶉柛鏇ㄥ暕閹烘鐓ュ〒姘功缁€澶愭煙鐎涙ê鐏ｆ繝濠冨灥椤斿繘鎳犻鍌滄⒕闂?PlayerProgressManager 婵炴垶鎼╅崣鍐焵椤掍椒浜㈢紒?IsNodeUnlocked(node) 闁荤姳鐒﹀妯肩礊瀹ュ棛顩?
                    // 闂佺懓鐡ㄨ摫闁哄鍠栭弻鍛村及韫囨洖绔奸梺鐓庣摠绾板秴锕㈡导鏉戠煑妞ゆ牗鐟ょ花浼存煟閳轰胶鎽犻悽顖涙尦瀵濡烽…鎴濇畱 PlayerProgressManager.Instance.IsNodeUnlocked(...)
                    break;

                    // ... 闂佺绻戝﹢鍦垝?case ...

                // --- 闁荤喐鐟︾敮鐔哥珶婵犲洤绠柍褜鍓熼幊妤呮嚍閵壯冪厙闁诲繒鍋熼崑鐐哄焵?---
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

    // --- 闁荤喐鐟︾敮鐔哥珶婵犲洤绠柍褜鍓熼幊妤呮嚍閵壯冪厙闂佺厧鎼崐濠氬磻閿濆洨涓嶉柨娑樺閸?---

    /// <summary>
    /// 濠碘槅鍋€閸嬫捇鏌＄仦璇插姤妞ゎ偅顨婇幊婵嬪矗婢跺á妤呮煠閸愭祴鍋撻悢鑲烘繈鏌ｉ幇顖ｆ綈婵″弶鎮傚畷銉╂晝閳ь剟宕欓敍鍕枂闁挎繂顦伴弫?
    /// </summary>
    public bool IsCharacterNodeUnlocked(CharacterSkillNode node)
    {
        if (node == null) return false;
        return unlockedNodeIDs.Contains(node.name);
    }

    /// <summary>
    /// 闁荤喐鐟辩紞渚€寮ㄩ敐鍥ㄥ枂闁圭儤娲栭ˉ蹇涙煙閸ㄦ稑浜鹃梺鐓庡暱閳ь剛鍠庤灐闂?
    /// </summary>
    public void UnlockCharacterNode(CharacterSkillNode node)
    {
        if (node == null || IsCharacterNodeUnlocked(node)) return;

        unlockedNodeIDs.Add(node.name);
        
        // 闂侀潧妫欓崝鎺楀箞閵娾晛缁╂鐐茬氨閸嬫捇骞嬪鍛喒闂佸憡鍔曠粔璺好洪崸妤€绠抽柕澹懏鐒鹃梺鍛婃⒒婵敻寮查妷鈺佸嚑婵犲﹥鍔楃粈澶愭煠閺夋寧婀版俊鍙夋倐閺屽苯顓奸崱妯煎帎闁荤姳绶ょ槐鏇㈡偩閺勫繈浜归柟鎯у暱椤ゅ懘鎮峰▎鎰濠㈢懓锕幆鍐礋椤愩垹浼庨梻渚囧枔閸斿本鎱ㄩ悙鍝勭?
        // 闂佸吋鍎抽崲鑼躲亹閸ヮ亗浜归柟鎯у暱椤ゅ懘鎮峰▎鎰濠㈢懓锕顐︽偋閸繄銈?
        CharacterData currentChar = null;
        if (DataManager.Instance != null)
        {
            currentChar = DataManager.Instance.selectedCharacter;
        }
        RecalculateCharacterBonuses(currentChar);

        SaveGame();
    }

    /// <summary>
    /// 闂備焦褰冪粔鍫曟偪閸℃稑绠伴柛銉戝懏姣庨柣鐔哥懄鐢喐绔熸繝鍥ㄥ剭闁告洦鍓欒灇闂佺厧鐤囨慨銈夋偉閺屻儲鏅柛顐ゅ枔椤忔悂姊婚崟鈺佲偓鏍ㄦ櫠瀹ュ瀚夊璺猴工閸ゆ帡鎮峰▎娆戠М闁衡偓閿濆鍤嶉柛灞剧矊娴狀垶鏌ㄥ☉妯侯殭缂佹顦甸弻鍛村焵椤掍焦浜ゆ慨妞诲亾闁革絽澧介弫顔界瑹婵犲嫮顦?
    /// </summary>
    public void ResetCharacterSkillTree(CharacterData charData)
    {
        if (charData == null || charData.characterSkillNodes == null) return;

        // 缂備礁顦…宄扳枍鎼达絾瀚氶柕澶樼厛濞硷繝鏌ら悷鏉跨骇濠⒀冪Ч瀵灚寰勬惔鈭ユ繈鏌ｉ幇顒佽础婵炲牊鍨归幉鎾晲婢跺鏆侀柣鐘辩劍濠㈡绱?
        foreach (var node in charData.characterSkillNodes)
        {
            if (node != null)
            {
                unlockedNodeIDs.Remove(node.name);
            }
        }

        // 婵炲瓨绮嶇敮妤呭几婵傚憡鍋愰柤鍝ヮ暯閸嬫挻鎷呯憴鍕戯箓鏌涢弬璇插闁逞屽厸濞村洭顢橀崫銉т笉婵°倕鍟悾閬嶆煥濞戞澧曢柣鏂哄亾婵炲瓨绮屽锕侇暰闂備礁銇橀懗鑸垫叏閹间礁绠戝〒姘功缁€鍡涙煥濞戞ɑ婀版俊顐犲€濆Λ渚€鍩€椤掍緤绱ｆ繝闈涙－濡棙绻涢幘铏殗婵?

        // 闂備焦褰冪粔鐢稿蓟婵犲嫭濯奸柨娑樺閺嗩剟鎮橀悙闈涗沪闁?
        RecalculateCharacterBonuses(charData);
        SaveGame();

        Debug.Log($"[CharacterSkillTree] Applied node for {charData.characterName}");
    }

    private void ApplyCharacterNodeEffect(PermanentUpgradeEffect effect)
    {
        switch (effect.upgradeType)
        {
            case PermanentUpgradeType.DamagePercent:
                permanentCharDamagePercentBonus += effect.value; // 缂備線纭搁崹鐗堟叏閻愬搫绀嗛柟宄扮焾濞硷繝鏌ら悷鏉跨骇濠碘姍鍥ㄥ殑闁兼亽鍎抽崺鎰槈閹惧瓨灏柣顐ｎ焽閳ь剚绋掗〃鍡涱敊?
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
    /// 闂佺娴氶崜娆撳矗閿熺姴绠抽柕澶堝劚缂嶆捇鏌ㄥ☉娆戠叝缂?UpgradeManager 闂侀潻璐熼崝宀勬儑椤掑嫬绀冮柛娑卞枛閳绘洟鏌涢幒鎾愁€滄い顐ｎ殜閹虫繈骞撻幒鎴濊€块梺鍝勫暞閸庡ジ鎯冮悢鍏煎仺妞ゎ厽甯炵粈澶愭煛閸パ呮憼闁?PPM 闂佸憡鍔曢幊姗€宕曠€靛憡濯奸柍鈺佸暞濞堝爼鏌?
    /// </summary>
    public void ApplyCharacterNodeEffectPublic(PermanentUpgradeEffect effect)
    {
        ApplyCharacterNodeEffect(effect);
    }

    /// <summary>
    /// 闂侀潧妫欓崝鏍偋鐎圭姷鐤€闁告劏鏅滈悡鈧繝鈷€鍛杭闁逞屽墯閸ㄥ爼鎮ч幘顔肩妞ゆ棁妫勯惁褰掓倵鐟欏嫪浜㈡い顐ｎ殜閹虫繈鎳犻浣烘殸閻庤鐡曠亸顏囶暰闂備礁銇樺ù鍥ㄤ繆椤撱垺鍊风憸鐗堝笒濞呫垽鏌￠崒娑橆€滄い鎾虫憸缁螖閸曞灚鍋ラ梺鑲╂嚀瀵埖淇婅閹虫鎳為妷褍鐓囬梺鍛婃⒒婵敻宕?
    /// 闁荤喐鐟辩徊浠嬪窗閸涱喚顩查柛鈩冩礈閻熸繈鏌涘顒傜劮妞ゎ偅顨婇幊婵嬪箵閹烘垵缍戞繛瀛樼矤閸嬪嫰骞冮幘瀵糕枖闁逞屽墰缁辨帡宕熼銈嗗墤闂佽鍎搁崨顔炬殸闂傚倸鍋嗛崳锝夈€?
    /// </summary>
    public void RecalculateCharacterBonuses(CharacterData forCharacter)
    {
        // 濠电偞鎸搁幊妯好归崒婊勫枂闁圭儤娲栭ˉ蹇涙煙閸ㄦ稑浜鹃梺鐓庣枃婵倝鎮ラ懠顑挎勃闁绘劗琛ラ崑?
        permanentMaxHealthBonus = 0;
        permanentArmorBonus = 0f;
        permanentMoveSpeedBonus = 0f;
        permanentCooldownReduction = 0f;
        permanentEnergyGainBonus = 0f;
        permanentLifeStealPercent = 0f;
        permanentCharDamagePercentBonus = 0f; // 濠电偞鎸搁幊妯好归崒婊勫枂闁圭儤娲栭ˉ蹇涙煙閸ㄦ稑浜鹃梺鐓庣枃婵倝鎮ラ弻銉ョ哗閻犲洦褰冨В濠囨煕閺冣偓缁嬫垶鎱ㄩ悙鍝勭?

        if (forCharacter == null || forCharacter.characterSkillNodes == null) return;

        // 闂佸憡鐟禍鐐烘偪椤曗偓瀹曟繈鎮╅懠顒傂梺鍛婃尭缁夌兘锝炲Δ鍛殞闁圭粯甯掗崵鎺楁偡濞嗘瑧绉柡鈧敐澶嬪殟闁稿本绮屾禒顖炴煟閵娿儱顏柡鍛劦瀵?
        foreach (var node in forCharacter.characterSkillNodes)
        {
            if (node != null && IsCharacterNodeUnlocked(node))
            {
                // 闂佸搫鐗嗛ˇ顖炲矗瑜旈幊鏇㈠棘閸喖鑰块梺缁橆殔濞层劌鈻撻幋锔藉殟闁稿本绮屾禒顖炴煥濞戞ɑ楗痑yer 2+闂佹寧绋戦¨鈧紒杈ㄧ箘娴狅箓鎮欑划鐟颁壕鐟滃秴鈻庨姀鈩冧氦闁绘柨鍢查悡鍌炴倶閻愨晛浜鹃梺鍛婂姇閹冲繑绻涙繝鍥х闁斥晛鍟ˇ褔鏌熼棃娑毿ら柡浣规崌瀵?
                if (node.linkedUpgradeNode != null) continue;

                foreach (var effect in node.effects)
                {
                    ApplyCharacterNodeEffect(effect);
                }
            }
        }
    }

    /// <summary>
    /// 闂佸吋鍎抽崲鑼躲亹閸ヮ剙绠伴柛銉戝懏姣庨柣蹇曞仜閸婄粯顨ラ崶褜鍟呴柤鑲╃礋閹烘鐓ュù锝囧劋閻ｉ亶鏌ら崫鍕偓濠氬磻閿濆鏋佸ù鍏兼綑濞?
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
    /// 闂佸吋鍎抽崲鑼躲亹閸ヮ剙绠伴柛銉戝懏姣庨柣蹇曞仜閸婄粯顨ラ崶顒佸剭闁告洦鍏橀崑鎾诲及韫囨拹婵嬫煟閹邦垼娼愰柡?
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
    /// 濠碘槅鍋€閸嬫捇鏌＄仦璇插姕闁绘挸娲幊鐐哄磼濠婂啩鍖栭梺鍝勫閸ㄤ即骞嗘担鍓茬叆闂勫洭宕橀懡銈嗗枂闁挎繂顦伴弫姘舵煛婢跺棌鍋撻崣澶樺仺
    /// 闂佸憡鐗曢幊搴ㄥ箚閸儱绀堢€广儱娲ㄩ弸鍌炴煠閸濆嫬鈧宕戦敐鍛傛盯鍩€椤掑嫬钃熼柕澶堝劜鐎氭彃霉濠婂嫬绗氶柡瀣偢閹崇偤宕掑鍐у寲濠碘槅鍋€閸嬫捇鏌?
    /// </summary>
    public bool CanUnlockCharacterNode(CharacterData charData, CharacterSkillNode node)
    {
        if (node == null) return false;
        if (IsCharacterNodeUnlocked(node)) return false; // 閻庤鐡曠亸顏囶暰闂?

        // 婵炲瓨绮嶇敮妤呭几閻撳宫娑㈠焵椤掑嫬钃熼柕澶樺灣缁愭鈹戦垾鍐测偓褰掓倶婢跺顩查柟鐑樻尰閻忛亶鏌ら崫鍕偓濠氬磻閿濆棛鈻旀い鎾跺枑缁犳帒霉閻樼數甯涢柛鎾跺缁嬪鍩€椤掍胶鈻旀い蹇撳閸ゆ帡鎮峰▎娆戠М闁衡偓閿濆鏅悘鐐舵閻忕喓鎲搁悧鍫熷碍濠⒀呭█閹崇偤宕掑鍐у寲濠殿喗锕㈤弲鑼不濞嗘挻鐓ュù锝呮憸閺?
        if (node.mutuallyExclusiveNodes != null)
        {
            foreach (var exclusive in node.mutuallyExclusiveNodes)
            {
                if (exclusive != null && IsCharacterNodeUnlocked(exclusive))
                    return false; // 婵炲瓨绮嶇敮妤呭几婵傚憡鍤嶉柛灞剧矊娴狀垳鈧鐡曠亸顏堬綖閿曞倹鐒诲璺侯儏椤忋儵鏌ㄥ☉妯垮缂傚秴顑夊畷婊冾吋閸惊婵嬫煟閹版壋鍋撳☉姘辨喒闂佸憡鐟崹浣冾暰闂?
            }
        }

        // 闂佸搫鍟版慨瀛樻櫠閻樼數纾炬い鏂跨仢铻￠梺?闂?闂佸憡鐟崹鍐裁洪崸妤€绠抽柕澶堝壉閹烘鐓?
        if (node.prerequisites == null || node.prerequisites.Count == 0)
            return true;

        // 闂佸湱顣介崑鎾绘煛閸繍妲稿褏濮风槐鏃堫敊閸啣婵嬫煟閹邦喗鍤€缂佺儵鍋撴俊鐐€涢褔宕欓敍鍕枂闁挎繂顦伴弫?
        foreach (var prereq in node.prerequisites)
        {
            if (prereq == null) continue;
            if (!IsCharacterNodeUnlocked(prereq))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 濠碘槅鍋€閸嬫捇鏌＄仦璇插姕闁绘挸娲幊鐐哄磼濠婂啩鍖栭梺鍝勫閸ㄤ即骞嗘笟鈧畷鍫曟偐鏉堚晠鐎洪梺鍝勫€堕崕鎾焵椤掆偓閻線锝為敃鈧～婵嬫⒐閹邦喚鏆梻浣搞仒缁€渚€鎮?
    /// </summary>
    public bool IsNodeExcluded(CharacterSkillNode node)
    {
        if (node == null || node.mutuallyExclusiveNodes == null) return false;
        foreach (var exclusive in node.mutuallyExclusiveNodes)
        {
            if (exclusive != null && IsCharacterNodeUnlocked(exclusive))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 闂佸搫琚崕鎾敋濡ゅ懏鍋濋柍杞扮贰閸熲偓闂佸搫瀚烽崹浼村箚娓氣偓楠炲繘濡烽敂鐣岀暢闂佸搫鏈崝鎺楁煂濠婂牆瀚夐柛婵嗗閻撴垿鏌涢幒鎴烆棡闁轰緡鍣ｉ弫宥夊醇閻旈浠滈梺鍝勫€归〃鍫ユ焾鐎靛摜纾奸柣鏃囨硾閳诲繘鏌ｉ姀鈺冨帨缂?
    /// </summary>
    public bool HasMechanic(string mechanicID)
    {
        if (string.IsNullOrEmpty(mechanicID)) return false;
        if (DataManager.Instance == null || DataManager.Instance.selectedCharacter == null) return false;

        CharacterData charData = DataManager.Instance.selectedCharacter;
        if (charData.characterSkillNodes == null) return false;

        foreach (var node in charData.characterSkillNodes)
        {
            if (node != null && node.isMechanicBranch 
                && node.mechanicID == mechanicID 
                && IsCharacterNodeUnlocked(node))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 闂備焦褰冪粔鍫曟偪閸℃瑦鍠嗛柟鐑樻礀椤ュ繘鏌熼崹娑樹壕闂佺厧鐤囨慨銈夋偉閺屻儲鍎嶉柛鏇ㄥ亜閻庡鏌￠埀顒勵敍閻愨晛浜惧璺侯儏椤忋儵鏌ㄥ☉妯煎缂侇噮鍨抽幏纭呫亹閹烘垶顏熺紓鍌氬枤閸ｎ垳妲?
    /// 闂佸憡鐟禍顏堝闯閸濄儳纾炬い鏃囧Г缁ㄦ岸鏌涢幒鏇熺【闁搞劌閰ｅ銊╊敍濮橆剚灏嬮梺绋跨箺濞夋盯骞冨Δ鍐＜妞ゆ挴鈧緞婵嬫煟閹邦喗顏熺紒杈ㄧ箖缁屽崬鈹戦崼鐔哥暚闂佺绻樻禍鍫曞焵椤掍浇澹橀柣鏂哄亾缂佺虎鍘搁崑鎾绘倶?
    /// </summary>
    public bool ResetCharacterBranch(CharacterData charData, int resetCost)
    {
        if (charData == null || !CanAfford(resetCost)) return false;

        // 闂佽　鍋撻柛顐ｆ礃閼茬娀鏌熺喊妯轰壕闂佸搫鐗嗛ˇ闈涖€掗崜浣瑰暫濞撴埃鍋撻柛锝呮憸缁辨棃顢欓悙顒傛殸闂佺厧鎼崐濠氬磻閿濆鏅柛褎鐛寉er >= 2 闂佹眹鍔岀€氼垱淇婇銏″€烽柣褍鎽滅粈?
        List<string> nodesToReset = new List<string>();
        if (charData.characterSkillNodes != null)
        {
            foreach (var node in charData.characterSkillNodes)
            {
                if (node != null && node.layer >= 2 && IsCharacterNodeUnlocked(node))
                {
                    nodesToReset.Add(node.name);
                }
            }
        }

        if (nodesToReset.Count == 0) return false; // 濠电偛澶囬崜婵嗭耿娓氣偓濡線鍩€椤掑倹鍟哄〒姘ｅ亾闁革絽鎽滅槐鏃堫敊閻愵剛鏆犻梺鐓庢惈閸婂宕?

        // 闂佸湱顣紞浣糕枍鎼淬劍鐓傞柟瀛樼箘椤?
        SpendGold(resetCost);

        // 缂備礁顦…宄扳枍鎼粹槅鍟呴柤鑲╃礋閹烘鐓ュù锝囧劋閻ｉ亶鏌ら崫鍕偓濠氬磻?
        foreach (var nodeID in nodesToReset)
        {
            unlockedNodeIDs.Remove(nodeID);
        }

        // 闂備焦褰冪粔鐢稿蓟婵犲嫭濯奸柨娑樺閺嗩剟鎮峰▎鎰濠㈢懓锕︽禒锕傛倷缁懓浜剧憸宥嗘叏閻愬搫绠?
        RecalculateCharacterBonuses(charData);
        SaveGame();

        Debug.Log($"[CharacterSkillTree] Reset {charData.characterName}, removed {nodesToReset.Count} nodes.");
        return true;
    }

    public void AddStat(string statKey, int amount)
    {
        if (string.IsNullOrEmpty(statKey) || amount == 0) return;

        // 1. 婵犫拃鍛粶濠殿喚鍋ゅ顐﹀级鎼存挸浜?
        if (!achievementStats.ContainsKey(statKey)) achievementStats[statKey] = 0;
        achievementStats[statKey] += amount;
        int currentValue = achievementStats[statKey];

        // 闂侀潧妫欓崝鏇㈠矗瑜旈弻銊╊敊鐠恒劍顔嶉柣鐘叉处濞插繘鍩€椤掍礁鐏╂繝鈧崨瀛樺剳閻庯綆鍋呯粻鎺撶箾鐏炵澧叉繝鈧笟鈧幆鍥偄閻戞鏆犻梺闈╄礋閸斿秶鈧?
        // 2. 闂侀潧妫欓崝鏍偋鐎圭姷鐤€闁告劧鑵归崑鎾诲箣閻戝棙鈷曢梺鍝勮閸庢彃危閹间礁瑙﹂柨鏇氱劍瑜把囨煕閹烘挸顎滅悮娆撴⒑婢跺摜鍔嶆繛纰卞灡缁?
        CheckUnlocks(statKey);
        AchievementManager.Instance?.NotifyStatChanged(statKey, currentValue);
        QueueAchievementStatSave();

        // 3. 闂侀潧妫欓崝鏍偋鐎圭姷鐤€闁告劧鑵归崑鎾诲箣濠婂懐顔旈柣搴㈢⊕閿曨偆妲愰幒鎾茬箚闁稿本绋撴禍顖氣槈閹惧磭小缂佺粯姘ㄩ埀顒佺⊕閿曨偆妲愬┑鍥┾枖閻庯綆鍋掗崑褔寮堕埡鍌溾姇闁绘牕鐖奸獮瀣疀濮樿京顔掗柟鑹版彧缁犳垵顫濋妸锔锯枖妞ゎ偒鍘鹃崯?
        // 婵犵鈧啿鈧綊鎮樻径灞惧枂濠㈣泛锕︾换浣规叏閿濆懐绠叉い鎰偢瀹曟繈鎮╃拠鎻掑箣婵烇絽娲︾换鍌炴偤閵婏箑绶炴い蹇撴礌閸嬫挸螖娴ｆ彃浜剧憸鎴﹀礂濮椻偓閺佸秶浠﹂挊澶庮唹婵炲濮伴崕鏌ュ棘娓氣偓瀹?CheckUnlocks 闂備焦褰冮懟顖炵嵁閹惧鈹嶆繝闈涙閹界娀鏌ㄥ☉妯绘拱闁搞劊鍔戦幊鎾诲川椤撶偛缍橀梺鍛婎殣缁辨洜鍒掗妸鈺佺骇闁绘柨澧庣粻浠嬫倵?
        // 婵炶揪绲藉Λ鏃傛嫻閻斿摜顩查柛鈩冾焽閵堟挳鎮归崶銊︾┛缂佽鲸绻堝畷妤呭醇濠靛洩鍚柡澶嗘櫆閻熲晠宕抽柨瀣攳婵犻潧妫涢幗?
        // SaveGame(); 
    }

    public void IncreaseAchievementStat(string statKey, int amount = 1)
    {
        if (string.IsNullOrEmpty(statKey) || amount == 0) return;

        // 1. 闂佸搫娲ら悺銊╁蓟婵犲嫧鍋撳☉娆樻當闁告埃鍋撻梺杞拌兌婢ф鐣?
        if (achievementStats.ContainsKey(statKey))
        {
            achievementStats[statKey] += amount;
        }
        else
        {
            achievementStats.Add(statKey, amount);
        }

        int currentValue = achievementStats[statKey];

        CheckUnlocks(statKey);
        AchievementManager.Instance?.NotifyStatChanged(statKey, currentValue);

        // Save immediately for explicit achievement stat updates.
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
            if (tree == null || tree.associatedWeapon == null) continue;
            // 1. 婵犵鈧啿鈧綊鎮樻径瀣氦婵炲棗閰ｉ崵瀣煛瀹ュ棗鐏ラ柛鎴磿缁辨帟绠涘В鎸庡浮閺屻劍鎷呮搴℃櫓闂佹寧绋戦惌渚€鎮滈敂鑺ヤ氦?
            // (濠电偛顦崝宥夊礈娴煎瓨鏅慨妯虹－缁犲綊姊洪幓鎺曞濞存粣绲块幏?weaponID 闂佸搫瀚烽崹浣冾暰闂備礁銇樼粈渚€宕甸悢鐑樺珰?
            string id = tree.associatedWeapon.weaponID;
            if (unlockedItems.Contains(id)) continue;

            // 2. 濠碘槅鍋€閸嬫捇鏌＄仦璇插姤缂佺粯鐗楃粙澶愵敂閸涱垰鐓囬梺鍝勫閸ㄤ即骞嗘笟鈧畷妤呭礃閼碱剙顩悷婊呭閹稿憡鏅堕悩璇茬煑婵☆垰鎼褔鏌?StatKey
            // 濠殿噯绲鹃弻銊┿€?ignite_count 闂佸憡鐟︾湁缂侇煉绻濋弫宥囦沪閹呬函婵炲濯崜娆掋亹瑜嶈灋闁逞屽墴瀵濡烽妷銉ョ稑闂婎偄娲ら崯浼村磻閿濆鍋嗛柛鎰典簼閻ｉ亶鏌?
            if (tree.unlockStatKey == changedStatKey)
            {
                // 3. 濠碘槅鍋€閸嬫捇鏌＄仦璇插姕闁哄棛鍠栧畷鎰板礂閸忓す锕傛煕濮樺墽鐣抽柟娲讳邯瀵?(闂佺儵鏅涢悺銊ф暜鐎靛憡瀚氬┑鐘宠壘鐢磭绱撻崘鎯ф珝闁革絿鍏橀幆?Threshold!)
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
            SaveGame(); // 闁荤姳鐒﹀妯兼鏉堛劎鈹嶆繝闈涙閹界娀鏌?

            OnItemUnlocked?.Invoke(itemName);

            if (itemName == "Role03")
            {
                SetAchievementStatMax("Engineer_Unlocked", 1);
                UnlockItem("Landmine");
            }
        }
    }

    public bool IsItemUnlocked(string itemName)
    {
        return !string.IsNullOrEmpty(itemName) && unlockedItems.Contains(itemName);
    }

    public IEnumerable<string> GetUnlockedAchievementIds()
    {
        return unlockedAchievementIDs;
    }

    public bool IsAchievementUnlocked(string achievementID)
    {
        return !string.IsNullOrEmpty(achievementID) && unlockedAchievementIDs.Contains(achievementID);
    }

    public bool MarkAchievementUnlocked(string achievementID)
    {
        if (string.IsNullOrEmpty(achievementID) || unlockedAchievementIDs.Contains(achievementID))
        {
            return false;
        }

        unlockedAchievementIDs.Add(achievementID);
        SaveGame();
        return true;
    }

    public int GetAchievementStat(string statKey)
    {
        if (string.IsNullOrEmpty(statKey)) return 0;
        return achievementStats.TryGetValue(statKey, out int value) ? value : 0;
    }

    public void SetAchievementStatMax(string statKey, int value)
    {
        if (string.IsNullOrEmpty(statKey)) return;
        int clampedValue = Mathf.Max(0, value);
        int currentValue = GetAchievementStat(statKey);
        if (currentValue >= clampedValue) return;

        achievementStats[statKey] = clampedValue;
        CheckUnlocks(statKey);
        AchievementManager.Instance?.NotifyStatChanged(statKey, clampedValue);
        QueueAchievementStatSave();
    }

    public void RecordWeaponLevelReached(WeaponStatBlock weapon, int level)
    {
        string key = GetWeaponLevelStatKey(weapon);
        if (string.IsNullOrEmpty(key)) return;
        SetAchievementStatMax(key, level);
    }

    public void RecordPassiveLevelReached(PassiveItemData passive, int level)
    {
        string key = GetPassiveLevelStatKey(passive);
        if (string.IsNullOrEmpty(key)) return;
        SetAchievementStatMax(key, level);
    }

    public static string GetWeaponLevelStatKey(WeaponStatBlock weapon)
    {
        if (weapon == null) return "";
        string id = !string.IsNullOrEmpty(weapon.weaponID) ? weapon.weaponID : weapon.name;
        return string.IsNullOrEmpty(id) ? "" : "WeaponLevel_" + id;
    }

    public static string GetPassiveLevelStatKey(PassiveItemData passive)
    {
        if (passive == null) return "";
        string id = !string.IsNullOrEmpty(passive.itemName) ? passive.itemName : passive.name;
        return string.IsNullOrEmpty(id) ? "" : "PassiveLevel_" + id;
    }

    private void QueueAchievementStatSave()
    {
        achievementStatsDirty = true;
        if (nextAchievementStatsAutoSaveTime <= Time.unscaledTime)
        {
            nextAchievementStatsAutoSaveTime = Time.unscaledTime + AchievementStatsAutoSaveInterval;
        }
    }

    private void FlushAchievementStatSave()
    {
        if (!achievementStatsDirty) return;

        achievementStatsDirty = false;
        nextAchievementStatsAutoSaveTime = 0f;
        SaveGame();
    }

    public void RecordDemoVictory(string sceneName, string timelineName)
    {
        if (!DemoContentGate.DemoModeEnabled) return;
        if (!DemoContentGate.IsSceneAllowed(sceneName)) return;

        IncreaseAchievementStat("Demo_TotalClears", 1);

        bool isHardTimeline = DemoContentGate.IsHardTimelineName(timelineName);
        if (isHardTimeline)
        {
            IncreaseAchievementStat(DemoContentGate.HardClearStatKey, 1);
            UnlockItem(DemoContentGate.FlameDaggerWeaponId);
            UnlockItem(DemoContentGate.ArcaneMasteryPassiveName);
            UnlockItem(DemoContentGate.ElementalResonancePassiveName);
            return;
        }

        IncreaseAchievementStat(DemoContentGate.IntroClearStatKey, 1);
        UnlockItem(DemoContentGate.MageCharacterId);
        UnlockItem(DemoContentGate.HardUnlockItemId);
        UnlockItem(DemoContentGate.LightningStrikeWeaponId);
        UnlockItem(DemoContentGate.ExperienceGainPassiveName);
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

        // --- 闂侀潧妫欓崝鎺楀箞閵娾晛缁╂鐐茬氨閸嬫捇骞嬮敐搴㈠仴闂佽偐鎳撳鑸典繆瑜旈幊妤呮嚍閵壯冪厙闁诲繒鍋熼崑鐐哄焵椤戭剙鍊婚悷婵嬫煕閹邦剛小缂佺粯姘ㄩ埀顒佺⊕钃遍柡鍡欏枛瀹曟劗娑垫搴ｎ槷婵炲濮存鎼佸礄閿涘嫭鍠嗛柨婵嗩槹閺佹岸鏌ら崫鍕偓濠氬磻閿濆绀夐柕濠忚吂閸嬫挻鎷呯粵瀣倎缂?---

        // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬪鍛啍闁诲孩绋掕摫闁哄苯锕顐︽偋閸繄銈?---
        data.savedUnlockedItems = this.unlockedItems;

        // Split dictionaries into lists for JsonUtility.
        data.savedStatKeys = new List<string>(this.achievementStats.Keys);
        data.savedStatValues = new List<int>(this.achievementStats.Values);
        data.savedUnlockedAchievementIDs = this.unlockedAchievementIDs.ToList();

        // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬪鍛啍闁诲孩绋掗敋缂傚秴顑夊畷婊冾吋婢跺棗浜惧璺侯槼閸橆剟鏌ｉ妸銉ヮ伂妞ゎ偅顨婇幊?---
        if (DataManager.Instance != null)
            data.savedSelectedCharacterID = DataManager.Instance.selectedCharacterID;
        else
            data.savedSelectedCharacterID = savedSelectedCharacterID;
        // -------------------------

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSaveFilePath(), json);
        achievementStatsDirty = false;
        nextAchievementStatsAutoSaveTime = 0f;

    }

    public void LoadGame()
    {
        string path = GetSaveFilePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            this.currentGold = data.savedGold;
            this.unlockedNodeIDs = data.savedUnlockedNodeIDs != null
                ? new HashSet<string>(data.savedUnlockedNodeIDs)
                : new HashSet<string>();
            this.permanentFlatDamageBonus = data.savedFlatDamageBonus;
            this.permanentMeleeAoeFlatDamageBonus = data.savedMeleeAoeFlatDamageBonus;
            this.permanentDamagePercentBonus = data.savedDamagePercentBonus;
            this.permanentFireRateBonus = data.savedFireRateBonus;

            // --- 闂侀潧妫欓崝鎺楀箞閵娾晛缁╂鐐茬氨閸嬫捇骞嬮敐搴㈠仴闂佽偐鎳撳鑸典繆瑜旈幊妤呮嚍閵壯冪厙闁诲繒鍋熼崑鐐哄焵椤戭剙鍊婚悷婵喢归悩鏌ヮ€楅柣掳鍔岄々濂告晲閸涱収娼遍柡澶屽仦閺嬭崵妲愬┑瀣哗闂侇偅绋栫粈瀣煕閺傝濡介柍褜鍏涘ù鍥敇閸濄儳涓?---
            // 闂佺绻愰悧濠勭博婵犳碍鈷栭悹浣告贡缁€澶愭煕濮橆剚鎹ｉ柣銏狀煼閹?RecalculateCharacterBonuses 闂佸搫绉烽～澶婄暤娴ｅ浜归柟鎯у暱椤ゅ懘鎮峰▎鎰濠㈢懓锕弻灞筋吋閸℃鍘愰柣鐘辩筏缁辨洟鎮?
            this.permanentMaxHealthBonus = 0;
            this.permanentArmorBonus = 0f;
            this.permanentMoveSpeedBonus = 0f;
            this.permanentCooldownReduction = 0f;
            this.permanentEnergyGainBonus = 0f;
            this.permanentLifeStealPercent = 0f;
            this.permanentCharDamagePercentBonus = 0f; // 闁荤喐鐟︾敮鐔哥珶婵犲洤绠柍褜鍓熼幊妤呮嚍閵壯冪厙闂佽　鍋撻悹鍥ㄥ絻濮ｅ﹪鏌涢弮鈧粙鎴炴叏閻愬搫绠ｉ柟閭︿簽閻﹀秹鏌涢弬璇插闁逞屽厸濞村洭顢橀崫銉т笉?

            // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬮敐搴℃闂佸憡鐟﹂悧婊勬櫠閸ф浼犲ù锝呭建閹烘鐓?---
            if (data.savedUnlockedItems != null)
            {
                this.unlockedItems = data.savedUnlockedItems;
            }
            else
            {
                this.unlockedItems = new List<string>();
            }

            // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬮敐搴℃闂佸憡鐟﹂悧妤呭垂濮樺彉鐒婇弶鍫亽閸氣偓闂?(缂傚倷绀佺€氼垶藟婵犲洤鐐婇柣鎰皺閹界喖鏌? ---
            this.achievementStats = new Dictionary<string, int>();
            if (data.savedStatKeys != null && data.savedStatValues != null)
            {
                // 缂佺虎鍙庨崰鏇犳崲?key 闂?value 闂佽桨妞掗崡鎶藉闯閻戞鈻旈柍褜鍓熼幊娑欐綇閸撗咁槷闂傚倸鍟鍫曨敆濞戙垹绠柕澶嗘櫆閺?
                int count = Mathf.Min(data.savedStatKeys.Count, data.savedStatValues.Count);
                for (int i = 0; i < count; i++)
                {
                    this.achievementStats[data.savedStatKeys[i]] = data.savedStatValues[i];
                }
            }
            unlockedAchievementIDs.Clear();
            if (data.savedUnlockedAchievementIDs != null)
            {
                foreach (string achievementID in data.savedUnlockedAchievementIDs)
                {
                    if (!string.IsNullOrEmpty(achievementID))
                    {
                        unlockedAchievementIDs.Add(achievementID);
                    }
                }
            }
            // ----------------------------------------

            // --- 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬮悙鏉垮灊婵犮垼娉涚粔宕囩箔閸屾埃鏋庨柨鐔哄Л閸嬫挻寰勬径搴″箑闂佹眹鍔岀€氼垶锝炲Δ鍛殞?---
            // 闂佺绻愰悧鍡涙偤閵娾晛绀嗛柟宄拌嫰濞堜即妫呴澶婁簻闁烩姍鍐ｆ灁缁炬澘顦辩粈澶愭煕閵壯冃￠悹?DataManager 闂佸憡鐟崹鐢稿礂濡棿鐒婃慨姗嗗幗瀵捇鏌涢幒鎾剁畵妞ゎ偅鍔欏畷?
            this.savedSelectedCharacterID = data.savedSelectedCharacterID;
            if (DataManager.Instance != null && !string.IsNullOrEmpty(data.savedSelectedCharacterID))
            {
                DataManager.Instance.selectedCharacterID = data.savedSelectedCharacterID;
            }

        }
        else
        {
            ResetProgressToDefault(); // 閻庣偣鍊濈紓姘额敊閸涙潙绀夐柣妯夸含閻熸劙寮堕埡鍌滄噥闂佸弶绮撳畷姘攽閸♀晜缍忛梺鍛婄墬閻楃偤鎯冮悢鍏煎仺?
        }
        ValidateRetroactiveUnlocks();
        AchievementManager.Instance?.SyncFromProgress(this);
    }
    private void ResetProgressToDefault()
    {
        currentGold = startingGold;
        unlockedNodeIDs.Clear();
        permanentFlatDamageBonus = 0;
        permanentMeleeAoeFlatDamageBonus = 0;
        permanentDamagePercentBonus = 0f;
        permanentFireRateBonus = 0f;

        // 闁荤喐鐟︾敮鐔哥珶婵犲洤绠柍褜鍓熼幊妤呮嚍閵壯冪厙闁诲繒鍋熼崑鐐哄焵?
        permanentMaxHealthBonus = 0;
        permanentArmorBonus = 0f;
        permanentMoveSpeedBonus = 0f;
        permanentCooldownReduction = 0f;
        permanentEnergyGainBonus = 0f;
        permanentLifeStealPercent = 0f;
        permanentCharDamagePercentBonus = 0f;

        // 闂侀潧妫欓崝鏍蓟婵犲啯娅犻柣鎰典簴閸嬫捇骞嬮悙娈夸紩闂傚倸瀚ㄩ崐娑綖濡ゅ懏鍤岄柤鑲╃礋閹烘鐓ュù锝呮啞鐎氭煡鏌熺€涙ê濮囨慨妯稿姂瀵偊鎮ч崼婵堛偊
        unlockedItems.Clear();
        achievementStats.Clear();
        unlockedAchievementIDs.Clear();
        achievementStatsDirty = false;
        nextAchievementStatsAutoSaveTime = 0f;
        savedSelectedCharacterID = null;
    }

    private void ValidateRetroactiveUnlocks()
    {
        if (allSkillTrees == null) return;

        foreach (var tree in allSkillTrees)
        {
            if (tree == null || tree.associatedWeapon == null) continue;
            // 1. 闁荤姴鎼悿鍥╂崲閸愨晩娓舵俊顖涱儥閸氬洭鎮峰▎娆戠М闁衡偓閿濆鍎?
            if (tree.isDefaultUnlocked) continue;

            string id = tree.associatedWeapon.weaponID;

            // 2. 婵犵鈧啿鈧綊鎮樻径濠庡晠闁肩⒈鍓涢惀鍛存偡濞嗘瑧绉柡鈧敐鍡欘洸闁糕槅鍘剧粈澶愭偣閸濆嫮鏋冪紒?
            if (unlockedItems.Contains(id)) continue;

            // 3. 濠碘槅鍋€閸嬫捇鏌＄仦璇插姕婵炵⒈鍨辩粋鎺楁嚋閸倣锕傛煕濮樺墽鐣遍柣掳鍔戝畷?
            if (string.IsNullOrEmpty(tree.unlockStatKey)) continue;

            // 4. 闂佸吋鍎抽崲鑼躲亹閸ヮ亗浜归柟鎯у暱椤ゅ懘寮堕埡鍌溾槈閻?
            int currentVal = 0;
            if (achievementStats.ContainsKey(tree.unlockStatKey))
            {
                currentVal = achievementStats[tree.unlockStatKey];
            }

            // 5. 闂侀潧妫欓崝鏍偋鐎圭姷鐤€闁告劧鑵归崑鎾诲箣閻樺磭鍑介梺瑙勪航閸庨亶顢氶澶规帡寮崼婵嗚祴缂傚倸鍠氶崳锝夊闯閻戣姤鍎?Threshold
            if (currentVal >= tree.unlockThreshold)
            {
                Debug.LogWarning($"[闂佺厧顨庢禍婊勬叏閳哄倻鈹嶆い鏃傗拡濡茬薄 闂佸憡鐟﹂崹褰掔嵁?{id} 闂佸搫顦埀顒€寮堕鐣屸偓瑙勭摃鐏忣亪骞撻鍫濆唨?({currentVal}/{tree.unlockThreshold}) 婵炶揪绲藉Λ娆忥耿椤撶姵鍠嗛柨婵嗩槹閺佹岸鏌ㄥ☉妯绘拱妞ゆ帗绮撳畷鐑藉Ω閵婂顦辩划?..");
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

        // 闂佸憡甯炴繛鈧繛鍛叄瀵剟宕堕敂绛嬪仺闂佸憡鑹鹃崙鐣屾濠靛牏鍗氶悗锝庝簻閻撴繈鎮樿箛鎾搭棞闁哥偛顕埀顒佺⊕鐪夐柤鍨灴閹啴宕熼浣诡啀闂佺顕栭崰鏇犲姬閸愵喗鐓傜€广儱娲ㄩ弸?
        ResetProgressToDefault();

        // 闂佸憡鑹鹃張顒勵敆閻愬眰鈧帡宕ㄧ€涙褰?DataManager 婵炴垶鎼╅崢鎯р枔閹寸姵鍠嗛柟鐑樻礀椤ュ繘姊洪銏╂Ч閻?
        if (DataManager.Instance != null)
        {
            DataManager.Instance.selectedCharacterID = null;
            DataManager.Instance.selectedCharacter = null;
        }

        // 婵犵鈧啿鈧綊鎮樻繅姗ч梺闈╄礋閸斿鈧潧鐭傞幊锝咁煥閸愩劎顦伴梺鍛婄懐閸ㄧ敻锝為崱娑欐櫖鐎光偓閳ь剟鎮靛☉銏犵闁惧浚鍋呯痪顖炴煛?
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(currentGold);
        }
    }
}
