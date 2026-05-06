using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 编辑器工具：一键生成角色技能树节点SO
/// 使用方式：Unity菜单 → Tools → 生成角色技能树SO
/// 
/// 剑士采用Y形分支结构：
///   第1层 共通基础（3节点）
///   第2层 命运抉择（2个互斥分支）
///   第3层 路线专属强化（每条路线3节点）
///   第4层 路线终极天赋（每条路线1节点）
/// </summary>
public class CharacterSkillTreeGenerator : EditorWindow
{
    [MenuItem("Tools/生成角色技能树SO")]
    public static void Generate()
    {
        // 确保目录存在
        string mageDir = "Assets/_TheFirst/GameData/CharacterSkillTree/Mage";
        string swordDir = "Assets/_TheFirst/GameData/CharacterSkillTree/Swordsman";

        EnsureDirectory(mageDir);
        EnsureDirectory(swordDir);

        // 生成法师节点（保持原有结构）
        GenerateMageNodes(mageDir);

        // 生成剑士节点（新Y形分支结构）
        GenerateSwordsmanNodes(swordDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 设置互斥关系（必须在所有节点创建后执行）
        SetupSwordsmanMutualExclusion(swordDir);
        SetupMageMutualExclusion(mageDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[技能树生成器] 生成完成！剑士Y形分支+法师Y形分支</color>");

        // 自动配置到 CharacterData（Role01=南瓜剑士, Role02=南瓜法师）
        AssignToCharacterData("Assets/_TheFirst/GameData/Character/Role01_Data.asset", swordDir);
        AssignToCharacterData("Assets/_TheFirst/GameData/Character/Role02_Data.asset", mageDir);
    }

    /// <summary>
    /// 将指定目录下的技能节点SO按层级顺序配置到CharacterData
    /// </summary>
    private static void AssignToCharacterData(string charDataPath, string skillNodeDir)
    {
        CharacterData charData = AssetDatabase.LoadAssetAtPath<CharacterData>(charDataPath);
        if (charData == null)
        {
            Debug.LogWarning($"[技能树生成器] 未找到 CharacterData: {charDataPath}");
            return;
        }

        // 加载目录下所有节点
        string[] guids = AssetDatabase.FindAssets("t:CharacterSkillNode", new[] { skillNodeDir });
        List<CharacterSkillNode> nodes = new List<CharacterSkillNode>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSkillNode node = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>(path);
            if (node != null) nodes.Add(node);
        }

        // 按层级排序（同层按名称排序）
        nodes.Sort((a, b) =>
        {
            if (a.layer != b.layer) return a.layer.CompareTo(b.layer);
            return a.name.CompareTo(b.name);
        });

        charData.characterSkillNodes = nodes;
        EditorUtility.SetDirty(charData);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=cyan>[技能树生成器] 已将 {nodes.Count} 个节点配置到 {charData.characterName}（{charDataPath}）</color>");
    }

    // =============================================
    // 法师节点生成（Y形分支：冰锥之路 vs 火球之路）
    // =============================================
    private static void GenerateMageNodes(string dir)
    {
        // ===== 第1层：共通基础（3节点）=====
        CreateNode(dir, "Mage_Base_ATK", "魔力 I", "攻击力 +8%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.08f });

        CreateNode(dir, "Mage_Base_HP", "生命 I", "生命上限 +20", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 20f });

        CreateNode(dir, "Mage_Base_SPD", "速度 I", "移动速度 +6%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.06f });

        // ===== 第2层：命运抉择（2个互斥分支）=====
        CreateMechanicBranchNode(dir, "Mage_Branch_Ice",
            "❄ 冰锥之路",
            "初始携带「冰锥术」\n并直接习得冰锥大招\n移速 +8%",
            "Start with Ice Shard\nUltimate auto-unlocked\nMove Speed +8%",
            2, 200, "IcePath",
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.08f });

        CreateMechanicBranchNode(dir, "Mage_Branch_Fire",
            "🔥 火球之路",
            "初始携带「火球术」\n并直接习得火球大招\n伤害 +12%",
            "Start with Fireball\nUltimate auto-unlocked\nDamage +12%",
            2, 200, "FirePath",
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.12f });

        // ===== 第3层A：冰锥路线专属强化（3节点）=====
        CreateNode(dir, "Mage_Ice_Barrage", "❄ 冰锥连射",
            "冰锥每穿透20次敌人\n自身发射8枚冰锥\n（发射的冰锥不计入穿透）", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.05f });

        CreateNode(dir, "Mage_Ice_Hail", "🌨 冰雹风暴",
            "冰锥大招额外下落冰雹\n持续5秒区域伤害", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 15f });

        CreateNode(dir, "Mage_Ice_Thunder", "⚡ 毁灭雷击",
            "需装备冰锥+落雷\n对冰冻目标攻击100%暴击率", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.05f });

        // ===== 第3层B：火球路线专属强化（3节点）=====
        CreateNode(dir, "Mage_Fire_Ignite", "🔥 燃烧大地",
            "火球造成伤害时\n有概率点燃地面生成火海\n（持续3秒灼烧）", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.05f });

        CreateNode(dir, "Mage_Fire_Trail", "🔥 烈焰轨迹",
            "火球大招的火球\n经过地面自动留下火焰轨迹", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.08f });

        CreateNode(dir, "Mage_Fire_Wind", "🌪 风助火势",
            "需装备火球+飓风\n飓风经过火海可扩散火海范围", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.06f });

        // ===== 第4层A：冰锥路线终极天赋 =====
        CreateNode(dir, "Mage_Talent_Blizzard", "⭐ 永冻领域",
            "每20秒生成永冻领域（半径8）\n持续6秒，敌人减速70%\n每秒冰冻判定\n冰锥穿透不消耗", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.LifeStealPercent, value = 0.02f });

        // ===== 第4层B：火球路线终极天赋 =====
        CreateNode(dir, "Mage_Talent_Inferno", "⭐ 炼狱之焰",
            "场上存在3处以上火海时\n自动触发炼狱\n全场火海伤害+200%\n持续5秒，冷却25秒", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.EnergyGainPercent, value = 0.15f });
    }

    // =============================================
    // 剑士节点生成（新Y形分支结构）
    // =============================================
    private static void GenerateSwordsmanNodes(string dir)
    {
        // ===== 第1层：共通基础（3节点）=====
        CreateNode(dir, "Sword_Base_ATK", "攻击力 I", "攻击力 +8%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.08f });

        CreateNode(dir, "Sword_Base_HP", "生命值 I", "生命上限 +20", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 20f });

        CreateNode(dir, "Sword_Base_SPD", "速度 I", "移动速度 +6%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.06f });

        // ===== 第2层：命运抉择（2个互斥分支）=====
        // 注意：互斥关系在 SetupSwordsmanMutualExclusion 中设置
        CreateMechanicBranchNode(dir, "Sword_Branch_Precision", 
            "⚔️ 精准斩击", 
            "解锁手动控制斩击朝向\n攻击时短暂停顿，但伤害 +12%",
            "解锁手动控制斩击朝向。攻击时短暂停顿，但伤害 +12%", 
            2, 200, "PrecisionSlash",
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.12f });

        CreateMechanicBranchNode(dir, "Sword_Branch_Agile",
            "🏃 敏捷猎手",
            "每次斩击时，身后额外释放一道斩击\n移动速度 +8%",
            "每次斩击时，身后额外释放一道斩击（70%伤害）。移动速度 +8%",
            2, 200, "AgileHunter",
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.08f });

        // ===== 第3层A：精准斩击路线专属强化（3节点）=====
        CreateNode(dir, "Sword_Prec_ArmorBreak", "破甲一击", 
            "精准斩击命中≤2个目标时\n伤害额外 +25%", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.25f });

        CreateNode(dir, "Sword_Prec_HeavySlash", "蓄力重斩",
            "攻击停顿延长至0.3秒\n但斩击范围 +40%，伤害 +20%", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.20f });

        CreateNode(dir, "Sword_Prec_FlashStep", "瞬身斩回",
            "精准斩击命中后\n获得0.8秒 +60%移速突进", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.06f });

        // ===== 第3层B：敏捷猎手路线专属强化（3节点）=====
        CreateNode(dir, "Sword_Agile_ShadowSlash", "残影连斩",
            "身后斩击有35%概率\n额外触发一次侧方斩击", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.05f });

        CreateNode(dir, "Sword_Agile_WindForce", "疾风之势",
            "每连续命中不同敌人，攻速+6%\n最多叠5层，2秒未命中重置", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.05f });

        // 猎手本能：不再使用受伤无敌（已有该机制），改为闪避+护甲
        CreateNode(dir, "Sword_Agile_Instinct", "猎手本能",
            "移速额外 +10%，护甲 +3\n多段命中时有概率回复少量生命", 3, 150,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.ArmorFlat, value = 3f });

        // ===== 第4层A：精准路线终极天赋 =====
        CreateNode(dir, "Sword_Talent_Kensei", "⭐ 剑圣之道",
            "暴击率 +15%\n暴击时恢复已损失生命3%\n攻击停顿期间免伤50%", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.LifeStealPercent, value = 0.03f });

        // ===== 第4层B：敏捷路线终极天赋 =====
        CreateNode(dir, "Sword_Talent_Shadow", "⭐ 疾影无踪",
            "击杀敌人2%概率触发影分身\n（持续3秒原地自动斩击）\n所有斩击附带2%吸血", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.LifeStealPercent, value = 0.02f });
    }

    /// <summary>
    /// 设置剑士互斥关系和前置关系
    /// 必须在所有节点创建后调用
    /// </summary>
    private static void SetupSwordsmanMutualExclusion(string dir)
    {
        // 加载分支节点
        CharacterSkillNode branchPrecision = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Branch_Precision.asset");
        CharacterSkillNode branchAgile = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Branch_Agile.asset");

        if (branchPrecision == null || branchAgile == null)
        {
            Debug.LogError("[技能树生成器] 找不到分支节点，无法设置互斥关系！");
            return;
        }

        // 设置互斥关系（双向）
        branchPrecision.mutuallyExclusiveNodes = new List<CharacterSkillNode> { branchAgile };
        branchAgile.mutuallyExclusiveNodes = new List<CharacterSkillNode> { branchPrecision };

        // 加载共通基础节点（作为分支的前置）
        CharacterSkillNode baseATK = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Base_ATK.asset");
        CharacterSkillNode baseHP = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Base_HP.asset");
        CharacterSkillNode baseSPD = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Base_SPD.asset");

        // 分支前置：第1层至少解锁2个（这里用prerequisites列表中任意2个来表示）
        // 由于现有系统是"全部前置都需要解锁"，我们改为只需要其中1个作为前置门槛
        // 实际的"至少2个"检查在UI层面处理
        if (baseATK != null)
        {
            branchPrecision.prerequisites = new List<CharacterSkillNode> { baseATK };
            branchAgile.prerequisites = new List<CharacterSkillNode> { baseATK };
        }

        EditorUtility.SetDirty(branchPrecision);
        EditorUtility.SetDirty(branchAgile);

        // 设置第3层A节点的前置 = 精准斩击分支
        SetPrerequisite(dir, "Sword_Prec_ArmorBreak", branchPrecision);
        SetPrerequisite(dir, "Sword_Prec_HeavySlash", branchPrecision);
        SetPrerequisite(dir, "Sword_Prec_FlashStep", branchPrecision);

        // 设置第3层B节点的前置 = 敏捷猎手分支
        SetPrerequisite(dir, "Sword_Agile_ShadowSlash", branchAgile);
        SetPrerequisite(dir, "Sword_Agile_WindForce", branchAgile);
        SetPrerequisite(dir, "Sword_Agile_Instinct", branchAgile);

        // 设置第4层A天赋的前置 = 精准路线3个节点全部解锁
        CharacterSkillNode precAB = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Prec_ArmorBreak.asset");
        CharacterSkillNode precHS = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Prec_HeavySlash.asset");
        CharacterSkillNode precFS = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Prec_FlashStep.asset");
        CharacterSkillNode talentKensei = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Talent_Kensei.asset");
        if (talentKensei != null && precAB != null && precHS != null && precFS != null)
        {
            talentKensei.prerequisites = new List<CharacterSkillNode> { precAB, precHS, precFS };
            EditorUtility.SetDirty(talentKensei);
        }

        // 设置第4层B天赋的前置 = 敏捷路线3个节点全部解锁
        CharacterSkillNode agileSS = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Agile_ShadowSlash.asset");
        CharacterSkillNode agileWF = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Agile_WindForce.asset");
        CharacterSkillNode agileIN = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Agile_Instinct.asset");
        CharacterSkillNode talentShadow = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Sword_Talent_Shadow.asset");
        if (talentShadow != null && agileSS != null && agileWF != null && agileIN != null)
        {
            talentShadow.prerequisites = new List<CharacterSkillNode> { agileSS, agileWF, agileIN };
            EditorUtility.SetDirty(talentShadow);
        }

        Debug.Log("<color=yellow>[技能树生成器] 剑士互斥关系和前置关系已配置完成</color>");
    }

    /// <summary>
    /// 设置法师互斥关系和前置关系
    /// </summary>
    private static void SetupMageMutualExclusion(string dir)
    {
        // 加载分支节点
        CharacterSkillNode branchIce = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Branch_Ice.asset");
        CharacterSkillNode branchFire = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Branch_Fire.asset");

        // 设置互斥
        if (branchIce != null && branchFire != null)
        {
            branchIce.mutuallyExclusiveNodes = new List<CharacterSkillNode> { branchFire };
            branchFire.mutuallyExclusiveNodes = new List<CharacterSkillNode> { branchIce };
            EditorUtility.SetDirty(branchIce);
            EditorUtility.SetDirty(branchFire);
        }

        // 设置第3层前置关系
        // 冰锥路线 → 需要先选冰锥之路
        SetPrerequisite(dir, "Mage_Ice_Barrage", branchIce);
        SetPrerequisite(dir, "Mage_Ice_Hail", branchIce);
        SetPrerequisite(dir, "Mage_Ice_Thunder", branchIce);

        // 火球路线 → 需要先选火球之路
        SetPrerequisite(dir, "Mage_Fire_Ignite", branchFire);
        SetPrerequisite(dir, "Mage_Fire_Trail", branchFire);
        SetPrerequisite(dir, "Mage_Fire_Wind", branchFire);

        // 设置第4层前置关系（需要对应路线的任意1个Layer3节点）
        CharacterSkillNode iceBarrage = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Ice_Barrage.asset");
        CharacterSkillNode fireIgnite = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Fire_Ignite.asset");

        CharacterSkillNode talentBlizzard = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Talent_Blizzard.asset");
        CharacterSkillNode talentInferno = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/Mage_Talent_Inferno.asset");

        if (talentBlizzard != null && iceBarrage != null)
        {
            talentBlizzard.prerequisites = new List<CharacterSkillNode> { iceBarrage };
            EditorUtility.SetDirty(talentBlizzard);
        }
        if (talentInferno != null && fireIgnite != null)
        {
            talentInferno.prerequisites = new List<CharacterSkillNode> { fireIgnite };
            EditorUtility.SetDirty(talentInferno);
        }

        Debug.Log("<color=yellow>[技能树生成器] 法师互斥关系和前置关系已配置完成</color>");
    }

    /// <summary>
    /// 辅助方法：为指定节点设置前置
    /// </summary>
    private static void SetPrerequisite(string dir, string nodeName, CharacterSkillNode prerequisite)
    {
        CharacterSkillNode node = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>($"{dir}/{nodeName}.asset");
        if (node != null && prerequisite != null)
        {
            node.prerequisites = new List<CharacterSkillNode> { prerequisite };
            EditorUtility.SetDirty(node);
        }
    }

    // =============================================
    // 节点创建辅助方法
    // =============================================

    /// <summary>
    /// 创建普通属性节点
    /// </summary>
    private static void CreateNode(string dir, string fileName, string nodeName, string desc,
        int layer, int cost, PermanentUpgradeEffect effect)
    {
        string path = $"{dir}/{fileName}.asset";

        // 如果已存在则跳过
        if (AssetDatabase.LoadAssetAtPath<CharacterSkillNode>(path) != null)
        {
            Debug.Log($"[跳过] {path} 已存在");
            return;
        }

        CharacterSkillNode node = ScriptableObject.CreateInstance<CharacterSkillNode>();
        node.nodeName = nodeName;
        node.description = desc;
        node.layer = layer;
        node.cost = cost;
        node.effects = new List<PermanentUpgradeEffect> { effect };

        AssetDatabase.CreateAsset(node, path);
        Debug.Log($"[已创建] {path}");
    }

    /// <summary>
    /// 创建机制分支节点
    /// </summary>
    private static void CreateMechanicBranchNode(string dir, string fileName, 
        string nodeName, string desc, string descEN,
        int layer, int cost, string mechanicID, PermanentUpgradeEffect effect)
    {
        string path = $"{dir}/{fileName}.asset";

        // 如果已存在则跳过
        if (AssetDatabase.LoadAssetAtPath<CharacterSkillNode>(path) != null)
        {
            Debug.Log($"[跳过] {path} 已存在");
            return;
        }

        CharacterSkillNode node = ScriptableObject.CreateInstance<CharacterSkillNode>();
        node.nodeName = nodeName;
        node.description = desc;
        node.descriptionEN = descEN;
        node.layer = layer;
        node.cost = cost;
        node.isMechanicBranch = true;
        node.mechanicID = mechanicID;
        node.effects = new List<PermanentUpgradeEffect> { effect };

        AssetDatabase.CreateAsset(node, path);
        Debug.Log($"[已创建-机制分支] {path} (mechanicID={mechanicID})");
    }

    private static void EnsureDirectory(string path)
    {
        // 逐级创建目录
        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
