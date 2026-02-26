using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 确保目录存在
public class SkillTreeGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Skill Tree Nodes")]
    public static void ShowWindow()
    {
        GetWindow<SkillTreeGenerator>("Skill Tree Generator");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Fireball Nodes"))
        {
            GenerateFireballNodes();
        }
        if (GUILayout.Button("Generate IceShard Nodes"))
        {
            GenerateIceShardNodes();
        }
    }

    private static void GenerateFireballNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Fireball";
        EnsureDirectory(folderPath);

        WeaponStatBlock fireballWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Fireball.asset");
        if (fireballWeapon == null) { Debug.LogError("找不到 Fireball Weapon SO"); return; }

        // --- 1. 创建节点 ---

        // 第一层
        var n_multishot_1 = CreateNode(folderPath, "Fireball_Multishot_I", "连珠火球 I", fireballWeapon, "额外释放 +1 次，伤害 -20%");
        SetEffects(n_multishot_1, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.BurstCount, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });

        var n_explosion = CreateNode(folderPath, "Fireball_Explosion_Dmg", "火球爆破", fireballWeapon, "爆炸伤害 +80%");
        SetEffects(n_explosion, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.AoeDamage, 80, ModifierType.Percentage)
        });

        var n_impact = CreateNode(folderPath, "Fireball_Impact_Dmg", "火球冲击", fireballWeapon, "冲击伤害 +80%");
        SetEffects(n_impact, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponDamage, 80, ModifierType.Percentage)
        });

        // 第二层
        var n_multishot_2 = CreateNode(folderPath, "Fireball_Multishot_II", "连珠火球 II", fireballWeapon, "再额外释放 +2 次，伤害 -30%");
        SetEffects(n_multishot_2, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.BurstCount, 2, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -30, ModifierType.Percentage)
        });

        var n_sparks = CreateNode(folderPath, "Fireball_Sparks", "火花", fireballWeapon, "爆炸后向四周溅射 5 个小火花");
        SetEffects(n_sparks, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.SubProjectile, 1, ModifierType.Flat),       // 开启分裂
            (UpgradeType.SubProjectileCount, 5, ModifierType.Flat)   // 5 个火花
        });

        var n_ignite = CreateNode(folderPath, "Fireball_Ignite", "火球爆燃", fireballWeapon, "命中后引燃敌人 6 秒");
        SetEffects(n_ignite, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.IgnitionChance, 100, ModifierType.Flat),    // +100% 点燃概率 (必定点燃)
            (UpgradeType.BurnDuration, 6, ModifierType.Flat)         // +6 秒燃烧时长
        });

        var n_area = CreateNode(folderPath, "Fireball_Burst", "火球爆发", fireballWeapon, "爆炸范围 +80%");
         SetEffects(n_area, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.AoeRadius, 80, ModifierType.Percentage)
        });

        var n_volley = CreateNode(folderPath, "Fireball_Volley", "火球齐射", fireballWeapon, "投射物数量 +1");
         SetEffects(n_volley, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.AddProjectile, 1, ModifierType.Flat)
        });

        // 第三层
        var n_multishot_3 = CreateNode(folderPath, "Fireball_Multishot_III", "连珠火球 III", fireballWeapon, "再额外释放 +3 次，伤害 -40%");
        SetEffects(n_multishot_3, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.BurstCount, 3, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -40, ModifierType.Percentage)
        });

        var n_burn_dmg = CreateNode(folderPath, "Fireball_IntenseBurn", "猛烈燃烧", fireballWeapon, "燃烧每跳额外造成目标 1% 最大生命值的伤害");
        SetEffects(n_burn_dmg, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.MaxHealthBurn, 1, ModifierType.Flat)        // 每跳 +1% 最大生命值伤害
        });

        var n_pyroblast = CreateNode(folderPath, "Fireball_Pyroblast", "炎爆术", fireballWeapon, "火球变为巨大的炎爆术，穿透+2，每次命中触发爆炸");
        SetEffects(n_pyroblast, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.PierceCount, 2, ModifierType.Flat)
        });
        // TODO: 炎爆特殊逻辑

        // 组合
        var n_mastery = CreateNode(folderPath, "Fireball_Mastery", "火球术精通", fireballWeapon, "火球伤害 +20%，冷却 -10%");
        SetEffects(n_mastery, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponDamage, 20, ModifierType.Percentage),
            (UpgradeType.WeaponFireRate, 0.1f, ModifierType.Percentage) // 0.1代表10%冷却缩减
        });

        // --- 2. 设置前置 (Prerequisites) ---
        // 第一层无前置
        
        // 第二层
        SetPrerequisite(n_multishot_2, n_multishot_1);
        SetPrerequisite(n_sparks, n_explosion);
        SetPrerequisite(n_ignite, n_explosion);
        SetPrerequisite(n_area, n_explosion);
        SetPrerequisite(n_volley, n_impact);

        // 第三层
        SetPrerequisite(n_multishot_3, n_multishot_2);
        SetPrerequisite(n_burn_dmg, n_ignite);
        SetPrerequisite(n_pyroblast, n_area);

        // 组合技能
        SetPrerequisites(n_mastery, new List<SkillTreeNodeData> { n_area, n_impact });

        // --- 3. 自动添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> { 
            n_multishot_1, n_explosion, n_impact,
            n_multishot_2, n_sparks, n_ignite, n_area, n_volley,
            n_multishot_3, n_burn_dmg, n_pyroblast,
            n_mastery
        });

        // --- 4. 自动配置 SO_Fireball 的分裂子弹引用 ---
        GameObject sparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TheFirst/Prefabs/Gameplay/ExplosiveFireball_Spark.prefab");
        if (sparkPrefab != null)
        {
            fireballWeapon.subProjectilePrefab = sparkPrefab;
            fireballWeapon.subProjectileCount = 0; // 基础数量=0，完全由技能树加成提供
            EditorUtility.SetDirty(fireballWeapon);
            Debug.Log($"[SO_Fireball] 已自动配置 subProjectilePrefab = {sparkPrefab.name}, subProjectileCount = 0");
        }
        else
        {
            Debug.LogWarning("[警告] 找不到 ExplosiveFireball_Spark.prefab，请确保它存在于 Assets/_TheFirst/Prefabs/Gameplay/ 下");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("火球术技能树节点生成完成，SO_Fireball 已自动配置。");
    }

    private static void GenerateIceShardNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/IceShard";
        EnsureDirectory(folderPath);

        WeaponStatBlock iceWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_IceShard.asset");
        if (iceWeapon == null) { Debug.LogError("找不到 IceShard Weapon SO"); return; }
        
        // --- 第一层 ---
        var n_dmg = CreateNode(folderPath, "IceShard_Dmg", "冰锥伤害增幅", iceWeapon, "伤害 +60%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        // 互斥组 A: 贯穿 vs 连发
        var n_pierce = CreateNode(folderPath, "IceShard_Pierce", "冰锥贯穿", iceWeapon, "伤害 +30%，穿透 +2");
        SetEffects(n_pierce, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.PierceCount, 2, ModifierType.Flat)
        });

        var n_multishot = CreateNode(folderPath, "IceShard_Multishot", "冰锥连发", iceWeapon, "额外释放 1 次，伤害 -20%");
        SetEffects(n_multishot, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.BurstCount, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });

        SetMutuallyExclusive(n_pierce, n_multishot); // 互斥绑定

        var n_freeze = CreateNode(folderPath, "IceShard_Freeze", "极寒冰锥", iceWeapon, "伤害 +30%，命中后冻结怪物 2 秒");
        SetEffects(n_freeze, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage)
        });
        // TODO: 冻结效果需特殊实现

        var n_split = CreateNode(folderPath, "IceShard_Split", "冰锥分裂", iceWeapon, "首次命中后分裂为 3 个小冰锥");
        // TODO: 分裂效果

        // --- 第二层 (流派分支) ---

        // 流派 A (需贯穿)
        var n_heavy = CreateNode(folderPath, "IceShard_Heavy", "重冰锥", iceWeapon, "速度 -30%，伤害 +50%，穿透 +1");
        SetEffects(n_heavy, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.WeaponProjectileSpeed, -30, ModifierType.Percentage),
            (UpgradeType.WeaponDamage, 50, ModifierType.Percentage),
            (UpgradeType.PierceCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_heavy, n_pierce);

        // 流派 B (需连发)
        var n_multishot_2 = CreateNode(folderPath, "IceShard_Multishot_II", "冰锥连发 II", iceWeapon, "额外释放 +1 次");
        SetEffects(n_multishot_2, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.BurstCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_multishot_2, n_multishot);

        // 流派 C (需分裂)
        var n_split_dmg = CreateNode(folderPath, "IceShard_Split_Dmg", "冰片伤害增幅", iceWeapon, "分裂出的小冰锥伤害 +80%");
        // TODO: 作用于子弹的子弹
        SetPrerequisite(n_split_dmg, n_split);

        // 流派 D (需极寒)
        var n_knockback = CreateNode(folderPath, "IceShard_Knockback", "冰锥冲击", iceWeapon, "击退 +200%，穿透 +2");
        SetEffects(n_knockback, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.PierceCount, 2, ModifierType.Flat)
        });
        // TODO: 击退UpgradeType
        SetPrerequisite(n_knockback, n_freeze);

        var n_volley = CreateNode(folderPath, "IceShard_Volley", "冰锥齐射", iceWeapon, "子弹 +1，伤害 -40%");
        SetEffects(n_volley, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.AddProjectile, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -40, ModifierType.Percentage)
        });
        SetPrerequisite(n_volley, n_freeze);

        // --- 组合与进阶 ---
        var n_tri = CreateNode(folderPath, "IceShard_Tri", "三棱冰锥", iceWeapon, "命中后分裂出带有所有强化效果的冰锥");
        // TODO: 三棱效果
        SetPrerequisite(n_tri, n_heavy);

        // --- 自动添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> { 
            n_dmg, n_pierce, n_multishot, n_freeze, n_split,
            n_heavy, n_multishot_2, n_split_dmg, n_knockback, n_volley,
            n_tri
        });

        AssetDatabase.SaveAssets();
        Debug.Log("IceShard nodes generated (Complete).");
    }

    // --- Helper Methods ---

    private static void AddToDatabase(List<SkillTreeNodeData> newNodes)
    {
        UpgradeDatabase db = LoadAsset<UpgradeDatabase>("Assets/_TheFirst/GameData/UpgradeDatabase.asset");
        if (db == null)
        {
            Debug.LogError("无法找到 UpgradeDatabase，请手动添加节点。");
            return;
        }

        if (db.weaponSkillNodes == null) db.weaponSkillNodes = new List<SkillTreeNodeData>();

        int count = 0;
        foreach (var node in newNodes)
        {
            if (!db.weaponSkillNodes.Contains(node))
            {
                db.weaponSkillNodes.Add(node);
                count++;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"已自动向 UpgradeDatabase 添加了 {count} 个新节点。");
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    private static T LoadAsset<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static SkillTreeNodeData CreateNode(string folder, string id, string name, WeaponStatBlock weapon, string desc)
    {
        string path = $"{folder}/{id}.asset";
        SkillTreeNodeData node = AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(path);
        
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<SkillTreeNodeData>();
            AssetDatabase.CreateAsset(node, path);
        }

        node.skillName = name;
        node.associatedWeapon = weapon;
        node.isWeaponSkillTreeNode = true;
        node.isOneTimeOnly = true;
        node.possibleOptions = new List<UpgradeOption>();
        node.prerequisites = new List<SkillTreeNodeData>();
        node.mutuallyExclusive = new List<SkillTreeNodeData>();
        
        UpgradeOption section = new UpgradeOption();
        section.description = desc;
        section.rarity = Rarity.Common;
        section.effects = new List<UpgradeEffect>();
        node.possibleOptions.Add(section);

        EditorUtility.SetDirty(node);
        return node;
    }

    private static void SetEffects(SkillTreeNodeData node, List<(UpgradeType, float, ModifierType)> effects)
    {
        if (node.possibleOptions.Count == 0) return;
        var option = node.possibleOptions[0];
        option.effects.Clear();

        foreach(var (type, val, mod) in effects)
        {
             UpgradeEffect eff = new UpgradeEffect();
             eff.actionType = EffectActionType.ModifyStat;
             eff.statToModify = type;
             eff.value = val;
             eff.modType = mod;
             option.effects.Add(eff);
        }
        EditorUtility.SetDirty(node);
    }

    private static void SetPrerequisite(SkillTreeNodeData node, SkillTreeNodeData prereq)
    {
        if (prereq == null) return;
        if (!node.prerequisites.Contains(prereq)) node.prerequisites.Add(prereq);
        EditorUtility.SetDirty(node);
    }
    
    private static void SetPrerequisites(SkillTreeNodeData node, List<SkillTreeNodeData> prereqs)
    {
        foreach(var p in prereqs) SetPrerequisite(node, p);
    }

    private static void SetMutuallyExclusive(SkillTreeNodeData nodeA, SkillTreeNodeData nodeB)
    {
        if (nodeA == null || nodeB == null) return;
        if (!nodeA.mutuallyExclusive.Contains(nodeB)) nodeA.mutuallyExclusive.Add(nodeB);
        if (!nodeB.mutuallyExclusive.Contains(nodeA)) nodeB.mutuallyExclusive.Add(nodeA);
        EditorUtility.SetDirty(nodeA);
        EditorUtility.SetDirty(nodeB);
    }
}
