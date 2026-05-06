using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 编辑器工具：批量生成剑士角色专属技能卡片 SO
/// 使用方法：Unity菜单 → Tools → 生成剑士技能卡片
/// </summary>
public class SwordSkillCardGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Sword Skill Cards")]
    static void GenerateCards()
    {
        string savePath = "Assets/_TheFirst/GameData/CharacterSkillCards/Swordsman";

        // 确保目录存在
        if (!AssetDatabase.IsValidFolder("Assets/_TheFirst/GameData/CharacterSkillCards"))
            AssetDatabase.CreateFolder("Assets/_TheFirst/GameData", "CharacterSkillCards");
        if (!AssetDatabase.IsValidFolder(savePath))
            AssetDatabase.CreateFolder("Assets/_TheFirst/GameData/CharacterSkillCards", "Swordsman");

        // 定义所有需要生成的卡片
        var cardDefs = new List<CardDefinition>
        {
            // 机制分支卡
            new CardDefinition("SwordCard_PrecisionSlash", "⚔️ 精准斩击",
                "解锁手动控制斩击朝向\n攻击时短暂停顿，但伤害+12%",
                "PrecisionSlash", Rarity.Rare),

            new CardDefinition("SwordCard_AgileHunter", "🗡️ 敏捷猎手",
                "解锁身后额外斩击\n攻击同时在身后释放刀光",
                "AgileHunter", Rarity.Rare),

            // 精准分支强化
            new CardDefinition("SwordCard_HeavySlash", "💥 蓄力重斩",
                "主刀范围+40%\n停顿时间延长但伤害更高",
                "Sword_Prec_HeavySlash", Rarity.Uncommon),

            new CardDefinition("SwordCard_ArmorBreak", "🛡️ 破甲一击",
                "主斩击伤害+30%\n无视敌人护甲",
                "Sword_Prec_ArmorBreak", Rarity.Uncommon),

            new CardDefinition("SwordCard_FlashStep", "⚡ 闪步突袭",
                "命中后短暂移速提升\n更灵活地穿梭战场",
                "Sword_Prec_FlashStep", Rarity.Uncommon),

            // 敏捷分支强化
            new CardDefinition("SwordCard_WindForce", "🌪️ 疾风之势",
                "每次攻击叠加攻速\n最高叠加5层",
                "Sword_Agile_WindForce", Rarity.Uncommon),

            new CardDefinition("SwordCard_ShadowSlash", "👤 残影连斩",
                "35%概率额外触发侧方斩击\n多角度覆盖敌人",
                "Sword_Agile_ShadowSlash", Rarity.Uncommon),

            new CardDefinition("SwordCard_Instinct", "🐾 猎手本能",
                "移速额外+10%，护甲+3\n多段命中时有概率回复少量生命",
                "Sword_Agile_Instinct", Rarity.Uncommon),

            // 高级天赋
            new CardDefinition("SwordCard_Kensei", "🏯 剑圣之道",
                "停顿期间减伤50%\n额外+15%暴击率",
                "Sword_Talent_Kensei", Rarity.Epic),

            new CardDefinition("SwordCard_Shadow", "👥 疾影无踪",
                "击杀敌人时有概率生成影分身\n影分身持续攻击3秒",
                "Sword_Talent_Shadow", Rarity.Epic),
        };

        int created = 0;
        foreach (var def in cardDefs)
        {
            string assetPath = $"{savePath}/{def.fileName}.asset";

            // 检查是否已存在
            if (AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(assetPath) != null)
            {
                Debug.Log($"[卡片生成] 跳过已存在: {def.fileName}");
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
            Debug.Log($"[卡片生成] 创建成功: {def.fileName} → skillIdentifier={def.skillIdentifier}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("生成完成",
            $"成功创建 {created} 张剑士技能卡片\n保存至: {savePath}\n\n" +
            "接下来请在每个 CharacterSkillNode SO 的\n「局内卡片关联」字段中拖入对应的卡片。",
            "确定");
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
