using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 编辑器工具：批量生成法师角色专属技能卡片 SO
/// 使用方法：Unity菜单 → Tools → Generate Mage Skill Cards
///
/// 生成后需要手动操作：
/// 1. 将每张卡片拖入对应 CharacterSkillNode 的 linkedUpgradeNode 字段
/// 2. 组合技卡片需要在 requiredWeapons 中配置所需武器的 WeaponStatBlock
/// </summary>
public class MageSkillCardGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Mage Skill Cards")]
    static void GenerateCards()
    {
        string savePath = "Assets/_TheFirst/GameData/CharacterSkillCards/Mage";

        // 确保目录存在
        if (!AssetDatabase.IsValidFolder("Assets/_TheFirst/GameData/CharacterSkillCards"))
            AssetDatabase.CreateFolder("Assets/_TheFirst/GameData", "CharacterSkillCards");
        if (!AssetDatabase.IsValidFolder(savePath))
            AssetDatabase.CreateFolder("Assets/_TheFirst/GameData/CharacterSkillCards", "Mage");

        // 定义所有法师卡片
        var cardDefs = new List<CardDefinition>
        {
            // ===== Layer 2: 分支选择卡（2张）=====
            new CardDefinition("MageCard_IcePath", "❄ 冰锥之路",
                "初始武器变为「冰锥术」\n自动解锁冰锥大招\n移速 +8%",
                "IcePath", Rarity.Rare),

            new CardDefinition("MageCard_FirePath", "🔥 火球之路",
                "保持初始「火球术」\n自动解锁火球大招\n伤害 +12%",
                "FirePath", Rarity.Rare),

            // ===== Layer 3A: 冰锥路线强化（3张）=====
            new CardDefinition("MageCard_IceBarrage", "❄ 冰锥连射",
                "冰锥每穿透20次敌人\n自动向8方向发射冰锥\n（连射冰锥不计入穿透）",
                "Mage_Ice_Barrage", Rarity.Uncommon),

            new CardDefinition("MageCard_IceHail", "🌨 冰雹风暴",
                "冰锥大招额外下落冰雹\n持续5秒区域伤害",
                "Mage_Ice_Hail", Rarity.Uncommon),

            new CardDefinition("MageCard_IceThunder", "⚡ 毁灭雷击",
                "【组合技：冰锥+落雷】\n对冰冻目标攻击必定暴击\n需同时装备冰锥和落雷",
                "Mage_Ice_Thunder", Rarity.Rare),

            // ===== Layer 3B: 火球路线强化（3张）=====
            new CardDefinition("MageCard_FireIgnite", "🔥 燃烧大地",
                "火球造成伤害时\n15%概率点燃地面生成火海\n火海持续3秒灼烧敌人",
                "Mage_Fire_Ignite", Rarity.Uncommon),

            new CardDefinition("MageCard_FireTrail", "🔥 烈焰轨迹",
                "火球大招的火球\n经过地面自动留下火焰轨迹\n扩大灼烧覆盖范围",
                "Mage_Fire_Trail", Rarity.Uncommon),

            new CardDefinition("MageCard_FireWind", "🌪 风助火势",
                "【组合技：火球+飓风】\n飓风经过火海可扩散火海范围\n需同时装备火球和飓风",
                "Mage_Fire_Wind", Rarity.Rare),

            // ===== Layer 4: 终极天赋（2张）=====
            new CardDefinition("MageCard_Blizzard", "⭐ 永冻领域",
                "每20秒自动生成永冻领域\n半径8，持续6秒\n敌人减速70%，每秒冰冻判定\n冰锥穿透不消耗",
                "Mage_Talent_Blizzard", Rarity.Epic),

            new CardDefinition("MageCard_Inferno", "⭐ 炼狱之焰",
                "场上存在3处以上火海时\n自动触发炼狱\n全场火海伤害+200%\n持续5秒，冷却25秒",
                "Mage_Talent_Inferno", Rarity.Epic),
        };

        int created = 0;
        foreach (var def in cardDefs)
        {
            string assetPath = $"{savePath}/{def.fileName}.asset";

            // 检查是否已存在
            if (AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(assetPath) != null)
            {
                Debug.Log($"[法师卡片生成] 跳过已存在: {def.fileName}");
                continue;
            }

            SkillTreeNodeData node = ScriptableObject.CreateInstance<SkillTreeNodeData>();
            node.skillName = def.displayName;
            node.maxLevel = 1;
            node.isOneTimeOnly = true;
            node.isWeaponSkillTreeNode = false;

            // 创建升级选项
            UpgradeOption option = new UpgradeOption();
            option.description = def.description;
            option.rarity = def.rarity;
            option.effects = new List<UpgradeEffect>();

            // 创建 ActivateCharSkill 效果
            UpgradeEffect effect = new UpgradeEffect();
            effect.actionType = EffectActionType.ActivateCharSkill;
            effect.skillIdentifier = def.skillIdentifier;
            option.effects.Add(effect);

            node.possibleOptions = new List<UpgradeOption> { option };

            AssetDatabase.CreateAsset(node, assetPath);
            created++;
            Debug.Log($"[法师卡片生成] 创建成功: {def.fileName} → skillIdentifier={def.skillIdentifier}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 自动关联卡片到技能树节点
        LinkCardsToSkillNodes(savePath);

        EditorUtility.DisplayDialog("生成完成",
            $"成功创建 {created} 张法师技能卡片\n保存至: {savePath}\n\n" +
            "已自动关联到对应的 CharacterSkillNode。\n" +
            "组合技卡片（毁灭雷击/风助火势）需手动配置 requiredWeapons 字段。",
            "确定");
    }

    /// <summary>
    /// 自动将卡片关联到对应的 CharacterSkillNode
    /// </summary>
    static void LinkCardsToSkillNodes(string cardDir)
    {
        string nodeDir = "Assets/_TheFirst/GameData/CharacterSkillTree/Mage";

        // 卡片文件名 → 节点文件名 的映射关系
        var mapping = new Dictionary<string, string>
        {
            { "MageCard_IcePath",     "Mage_Branch_Ice" },
            { "MageCard_FirePath",    "Mage_Branch_Fire" },
            { "MageCard_IceBarrage",  "Mage_Ice_Barrage" },
            { "MageCard_IceHail",     "Mage_Ice_Hail" },
            { "MageCard_IceThunder",  "Mage_Ice_Thunder" },
            { "MageCard_FireIgnite",  "Mage_Fire_Ignite" },
            { "MageCard_FireTrail",   "Mage_Fire_Trail" },
            { "MageCard_FireWind",    "Mage_Fire_Wind" },
            { "MageCard_Blizzard",    "Mage_Talent_Blizzard" },
            { "MageCard_Inferno",     "Mage_Talent_Inferno" },
        };

        int linked = 0;
        foreach (var pair in mapping)
        {
            string cardPath = $"{cardDir}/{pair.Key}.asset";
            string nodePath = $"{nodeDir}/{pair.Value}.asset";

            SkillTreeNodeData card = AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(cardPath);
            CharacterSkillNode node = AssetDatabase.LoadAssetAtPath<CharacterSkillNode>(nodePath);

            if (card == null)
            {
                Debug.LogWarning($"[法师卡片关联] 找不到卡片: {cardPath}");
                continue;
            }
            if (node == null)
            {
                Debug.LogWarning($"[法师卡片关联] 找不到节点: {nodePath}");
                continue;
            }

            node.linkedUpgradeNode = card;
            EditorUtility.SetDirty(node);
            linked++;
            Debug.Log($"[法师卡片关联] {pair.Value} ← {pair.Key}");
        }

        if (linked > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[法师卡片关联] 成功关联 {linked} 张卡片到技能树节点</color>");
        }
    }

    struct CardDefinition
    {
        public string fileName;
        public string displayName;
        public string description;
        public string skillIdentifier;
        public Rarity rarity;

        public CardDefinition(string fileName, string displayName, string description, string skillIdentifier, Rarity rarity)
        {
            this.fileName = fileName;
            this.displayName = displayName;
            this.description = description;
            this.skillIdentifier = skillIdentifier;
            this.rarity = rarity;
        }
    }
}
