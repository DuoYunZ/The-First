using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 编辑器工具：一键生成角色技能树节点SO
/// 使用方式：Unity菜单 → Tools → 生成角色技能树SO
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

        // 生成法师节点
        GenerateMageNodes(mageDir);

        // 生成剑士节点
        GenerateSwordsmanNodes(swordDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[技能树生成器] 已生成法师13个 + 剑士13个 = 26个技能节点SO！</color>");

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

    private static void GenerateMageNodes(string dir)
    {
        // ===== 第1层（基础层）=====
        CreateNode(dir, "Mage_ATK_1", "攻击力 I", "攻击力 +5%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.05f });

        CreateNode(dir, "Mage_HP_1", "生命值 I", "生命上限 +10", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 10f });

        CreateNode(dir, "Mage_SPD_1", "速度 I", "移动速度 +5%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.05f });

        CreateNode(dir, "Mage_CDR_1", "冷却时间 I", "冷却缩减 +5%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.05f });

        // ===== 第2层（进阶层）=====
        CreateNode(dir, "Mage_ATK_2", "攻击力 II", "攻击力 +10%", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.10f });

        CreateNode(dir, "Mage_DEF_1", "防御力 I", "护甲 +2", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.ArmorFlat, value = 2f });

        CreateNode(dir, "Mage_SPD_2", "速度 II", "移动速度 +8%", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.08f });

        CreateNode(dir, "Mage_CDR_2", "冷却时间 II", "冷却缩减 +8%", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.08f });

        // ===== 第3层（精通层）=====
        CreateNode(dir, "Mage_ATK_3", "攻击力 III", "攻击力 +15%", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.15f });

        CreateNode(dir, "Mage_HP_2", "生命值 II", "生命上限 +20", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 20f });

        CreateNode(dir, "Mage_SPD_3", "速度 III", "移动速度 +10%", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.10f });

        CreateNode(dir, "Mage_CDR_3", "冷却时间 III", "冷却缩减 +12%", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.12f });

        // ===== 天赋层 =====
        CreateNode(dir, "Mage_Talent", "⭐ 魔力亲和", "能量获取效率 +20%", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.EnergyGainPercent, value = 0.20f });
    }

    private static void GenerateSwordsmanNodes(string dir)
    {
        // ===== 第1层（基础层）=====
        CreateNode(dir, "Sword_ATK_1", "攻击力 I", "攻击力 +5%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.05f });

        CreateNode(dir, "Sword_DEF_1", "防御力 I", "护甲 +2", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.ArmorFlat, value = 2f });

        CreateNode(dir, "Sword_HP_1", "生命值 I", "生命上限 +15", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 15f });

        CreateNode(dir, "Sword_SPD_1", "速度 I", "移动速度 +5%", 1, 50,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.05f });

        // ===== 第2层（进阶层）=====
        CreateNode(dir, "Sword_ATK_2", "攻击力 II", "攻击力 +10%", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.10f });

        CreateNode(dir, "Sword_DEF_2", "防御力 II", "护甲 +3", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.ArmorFlat, value = 3f });

        CreateNode(dir, "Sword_HP_2", "生命值 II", "生命上限 +25", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 25f });

        CreateNode(dir, "Sword_CDR_1", "冷却时间 I", "冷却缩减 +8%", 2, 120,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.CooldownReductionPercent, value = 0.08f });

        // ===== 第3层（精通层）=====
        CreateNode(dir, "Sword_ATK_3", "攻击力 III", "攻击力 +15%", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.DamagePercent, value = 0.15f });

        CreateNode(dir, "Sword_DEF_3", "防御力 III", "护甲 +5", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.ArmorFlat, value = 5f });

        CreateNode(dir, "Sword_HP_3", "生命值 III", "生命上限 +30", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MaxHealthFlat, value = 30f });

        CreateNode(dir, "Sword_SPD_2", "速度 II", "移动速度 +10%", 3, 250,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.MoveSpeedPercent, value = 0.10f });

        // ===== 天赋层 =====
        CreateNode(dir, "Sword_Talent", "⭐ 战意吸血", "造成伤害的 2% 转化为生命恢复", 4, 500,
            new PermanentUpgradeEffect { upgradeType = PermanentUpgradeType.LifeStealPercent, value = 0.02f });
    }

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
