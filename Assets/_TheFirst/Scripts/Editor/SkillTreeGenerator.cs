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
        if (GUILayout.Button("Generate LightningStrike Nodes"))
        {
            GenerateLightningStrikeNodes();
        }
        if (GUILayout.Button("Generate Hurricane Nodes"))
        {
            GenerateHurricaneNodes();
        }
        if (GUILayout.Button("Generate Grenade Nodes"))
        {
            GenerateGrenadeNodes();
        }
        if (GUILayout.Button("Generate ChainLightning Nodes"))
        {
            GenerateChainLightningNodes();
        }
        if (GUILayout.Button("Generate FrostNova Nodes"))
        {
            GenerateFrostNovaNodes();
        }
        if (GUILayout.Button("Generate IceFusion Nodes"))
        {
            GenerateIceFusionNodes();
        }
        if (GUILayout.Button("Generate Orbiter Nodes"))
        {
            GenerateOrbiterNodes();
        }
        if (GUILayout.Button("Generate Landmine Nodes"))
        {
            GenerateLandmineNodes();
        }
        if (GUILayout.Button("Generate Aura (Support) Nodes"))
        {
            GenerateAuraNodes();
        }
        if (GUILayout.Button("Generate FlameDagger Nodes"))
        {
            GenerateFlameDaggerNodes();
        }
        if (GUILayout.Button("Generate Blade Nodes"))
        {
            GenerateBladeNodes();
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
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.FreezeChance, 30, ModifierType.Flat)  // +30% 冰冻概率
        });

        var n_split = CreateNode(folderPath, "IceShard_Split", "冰锥分裂", iceWeapon, "首次命中后分裂为 3 个小冰锥");
        SetEffects(n_split, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.SubProjectile, 1, ModifierType.Flat),       // 开启分裂
            (UpgradeType.SubProjectileCount, 3, ModifierType.Flat)   // 3 个小冰锥
        });

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
        SetEffects(n_split_dmg, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.SubProjectileDamageBonus, 80, ModifierType.Flat) // +80% 分裂伤害
        });
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
        var n_tri = CreateNode(folderPath, "IceShard_Tri", "三棱冰锥", iceWeapon, "分裂的冰锥继承穿透和冰冻概率");
        SetEffects(n_tri, new List<(UpgradeType, float, ModifierType)> { 
            (UpgradeType.SubProjectileInherit, 1, ModifierType.Flat) // 分裂继承母弹属性
        });
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

    private static void GenerateLightningStrikeNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/LightningStrike";
        EnsureDirectory(folderPath);

        WeaponStatBlock lsWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_LightningStrike.asset");
        if (lsWeapon == null) { Debug.LogError("找不到 LightningStrike Weapon SO"); return; }

        // === 第一层 ===

        var n_storm = CreateNode(folderPath, "LS_MagneticStorm", "磁暴", lsWeapon, "雷击命中后触发一次性范围爆炸");
        SetEffects(n_storm, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.MagneticStormBurst, 1, ModifierType.Flat)
        });

        var n_highvolt = CreateNode(folderPath, "LS_HighVoltage", "高压雷击", lsWeapon, "雷击伤害+30%，麻痹时间+1.5秒");
        SetEffects(n_highvolt, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.StunDuration, 1.5f, ModifierType.Flat)
        });

        var n_dmg = CreateNode(folderPath, "LS_DamageBoost", "雷击伤害增幅", lsWeapon, "雷击伤害+80%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 80, ModifierType.Percentage)
        });

        var n_chain1 = CreateNode(folderPath, "LS_ChainI", "连续雷击 I", lsWeapon, "落雷0.3秒后再落一道，伤害-20%");
        SetEffects(n_chain1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LightningRepeatCount, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });

        // === 第二层 ===

        // 磁暴分支
        var n_field = CreateNode(folderPath, "LS_ElectricField", "电磁场", lsWeapon, "磁暴后生成持续电磁场，场内攻击暴击率提升");
        SetEffects(n_field, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.ElectricField, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_field, n_storm);

        var n_expand = CreateNode(folderPath, "LS_StormExpand", "磁暴扩张", lsWeapon, "磁暴伤害与范围+75%");
        SetEffects(n_expand, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeDamage, 75, ModifierType.Percentage),
            (UpgradeType.AoeRadius, 75, ModifierType.Percentage)
        });
        SetPrerequisite(n_expand, n_storm);

        // 伤害增幅分支
        var n_sanction = CreateNode(folderPath, "LS_Sanction", "雷电制裁", lsWeapon, "雷击直击伤害+150%，杀死怪物时在小范围内再释放1次单体雷击");
        SetEffects(n_sanction, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 150, ModifierType.Percentage),
            (UpgradeType.OnKillChainLightning, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_sanction, n_dmg);

        var n_thunder = CreateNode(folderPath, "LS_Thunderbolt", "雷霆万钧", lsWeapon, "雷击初始目标+1");
        SetEffects(n_thunder, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AddProjectile, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_thunder, n_dmg);

        // 连续雷击分支
        var n_chain2 = CreateNode(folderPath, "LS_ChainII", "连续雷击 II", lsWeapon, "落雷后再额外释放一道");
        SetEffects(n_chain2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LightningRepeatCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_chain2, n_chain1);

        // === 第三层 ===

        // 电磁场进阶
        var n_stableField = CreateNode(folderPath, "LS_StableField", "稳定电场", lsWeapon, "电磁场伤害+400%，持续时间+2秒");
        SetEffects(n_stableField, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.ElectricFieldDamage, 400, ModifierType.Percentage),
            (UpgradeType.ElectricFieldDuration, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_stableField, n_field);

        // 连续雷击进阶
        var n_chain3 = CreateNode(folderPath, "LS_ChainIII", "连续雷击 III", lsWeapon, "落雷后再额外释放一道");
        SetEffects(n_chain3, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LightningRepeatCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_chain3, n_chain2);

        // --- 自动添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_storm, n_highvolt, n_dmg, n_chain1,
            n_field, n_expand, n_sanction, n_thunder, n_chain2,
            n_stableField, n_chain3
        });

        AssetDatabase.SaveAssets();
        Debug.Log("雷击术技能树节点生成完成。");
    }

    private static void GenerateHurricaneNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Hurricane";
        EnsureDirectory(folderPath);

        WeaponStatBlock hcWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Hurricane.asset");
        if (hcWeapon == null) { Debug.LogError("找不到 Hurricane Weapon SO"); return; }

        // === 第一层 ===

        var n_dmgBoost = CreateNode(folderPath, "HC_DamageBoost", "飓风增幅", hcWeapon, "飓风术伤害+60%");
        SetEffects(n_dmgBoost, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        var n_multi1 = CreateNode(folderPath, "HC_MultiI", "多重飓风", hcWeapon, "飓风术额外释放1次，伤害-15%");
        SetEffects(n_multi1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.BurstCount, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -15, ModifierType.Percentage)
        });

        var n_pierce = CreateNode(folderPath, "HC_Pierce", "贯通之风", hcWeapon, "飓风术穿透+6");
        SetEffects(n_pierce, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.PierceCount, 6, ModifierType.Flat)
        });

        // 真空和强袭飓风互斥
        var n_vacuum = CreateNode(folderPath, "HC_Vacuum", "真空", hcWeapon, "飓风术命中怪物时造成小范围伤害并牵引周围怪物");
        SetEffects(n_vacuum, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.VacuumPull, 1, ModifierType.Flat)
        });

        var n_assault = CreateNode(folderPath, "HC_Assault", "强袭飓风", hcWeapon, "飓风术伤害+40%，击退+50%");
        SetEffects(n_assault, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 40, ModifierType.Percentage),
            (UpgradeType.KnockbackForce, 50, ModifierType.Percentage)
        });

        // 真空和强袭飓风互斥
        SetMutuallyExclusive(n_vacuum, n_assault);

        var n_turb1 = CreateNode(folderPath, "HC_Turbulence", "乱流", hcWeapon, "飓风命中怪物时生成1个穿透效果和伤害更低的小飓风");
        SetEffects(n_turb1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.Turbulence, 1, ModifierType.Flat)
        });

        // === 第二层 ===

        var n_windReturn = CreateNode(folderPath, "HC_WindReturn", "风力回旋", hcWeapon, "飓风穿透耗尽时往其他方向再次释放一道无强化飓风");
        SetEffects(n_windReturn, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WindReturn, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_windReturn, n_dmgBoost);

        var n_multi2 = CreateNode(folderPath, "HC_MultiII", "多重飓风 II", hcWeapon, "飓风术额外释放一次");
        SetEffects(n_multi2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.BurstCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_multi2, n_multi1);

        var n_vacCollapse = CreateNode(folderPath, "HC_VacuumCollapse", "真空塌陷", hcWeapon, "真空伤害+50%，并施加2秒眩晕");
        SetEffects(n_vacCollapse, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.VacuumDamage, 50, ModifierType.Percentage),
            (UpgradeType.StunDuration, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_vacCollapse, n_vacuum);

        var n_turb2 = CreateNode(folderPath, "HC_TurbulenceII", "乱流加剧", hcWeapon, "飓风每命中1个怪物都额外生成1个小飓风");
        SetEffects(n_turb2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.TurbulenceIntensify, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_turb2, n_turb1);

        // === 第三层 ===

        var n_multi3 = CreateNode(folderPath, "HC_MultiIII", "多重飓风 III", hcWeapon, "飓风术伤害-30%，额外放2次");
        SetEffects(n_multi3, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.BurstCount, 2, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -30, ModifierType.Percentage)
        });
        SetPrerequisite(n_multi3, n_multi2);

        var n_turb3 = CreateNode(folderPath, "HC_TurbulenceIII", "乱流加剧 II", hcWeapon, "飓风每命中1个怪物都额外生成1个小飓风");
        SetEffects(n_turb3, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.TurbulenceIntensify, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_turb3, n_turb2);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_dmgBoost, n_multi1, n_pierce, n_vacuum, n_assault, n_turb1,
            n_windReturn, n_multi2, n_vacCollapse, n_turb2,
            n_multi3, n_turb3
        });

        AssetDatabase.SaveAssets();
        Debug.Log("飓风术技能树节点生成完成。");
    }

    private static void GenerateGrenadeNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Grenade";
        EnsureDirectory(folderPath);

        WeaponStatBlock grenadeWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Grenade.asset");
        if (grenadeWeapon == null) { Debug.LogError("找不到 Grenade Weapon SO"); return; }

        // === 第一层 ===

        var n_stun = CreateNode(folderPath, "GR_StunBomb", "震撼弹", grenadeWeapon, "榴弹爆炸时附带1.5秒眩晕");
        SetEffects(n_stun, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.Stun, 1.5f, ModifierType.Flat)
        });

        var n_burst = CreateNode(folderPath, "GR_BurstI", "连续投掷", grenadeWeapon, "榴弹额外释放1次");
        SetEffects(n_burst, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.BurstCount, 1, ModifierType.Flat)
        });

        var n_dmg1 = CreateNode(folderPath, "GR_DmgI", "榴弹伤害增幅", grenadeWeapon, "榴弹伤害+60%");
        SetEffects(n_dmg1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        var n_cd1 = CreateNode(folderPath, "GR_CooldownI", "投弹手", grenadeWeapon, "榴弹冷却-20%");
        SetEffects(n_cd1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponFireRate, -20, ModifierType.Percentage)
        });

        var n_bounce1 = CreateNode(folderPath, "GR_BounceI", "弹跳榴弹", grenadeWeapon, "榴弹伤害-20%，爆炸后弹跳1次");
        SetEffects(n_bounce1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.GrenadeBounce, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });

        var n_aoe = CreateNode(folderPath, "GR_AoeBoost", "榴弹爆破增幅", grenadeWeapon, "榴弹爆炸范围+100%");
        SetEffects(n_aoe, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 100, ModifierType.Percentage)
        });

        // === 第二层 ===

        var n_destroy = CreateNode(folderPath, "GR_Destroy", "毁灭炸弹", grenadeWeapon, "榴弹伤害+150%");
        SetEffects(n_destroy, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 150, ModifierType.Percentage)
        });
        SetPrerequisite(n_destroy, n_dmg1);

        var n_dmg2 = CreateNode(folderPath, "GR_DmgII", "榴弹伤害增幅 II", grenadeWeapon, "榴弹伤害+60%");
        SetEffects(n_dmg2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });
        SetPrerequisite(n_dmg2, n_dmg1);

        var n_cd2 = CreateNode(folderPath, "GR_CooldownII", "投弹手 II", grenadeWeapon, "榴弹冷却-20%");
        SetEffects(n_cd2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponFireRate, -20, ModifierType.Percentage)
        });
        SetPrerequisite(n_cd2, n_cd1);

        var n_bounce2 = CreateNode(folderPath, "GR_BounceII", "弹跳榴弹 II", grenadeWeapon, "再弹跳1次，伤害-20%");
        SetEffects(n_bounce2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.GrenadeBounce, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });
        SetPrerequisite(n_bounce2, n_bounce1);

        // === 第三层 ===

        var n_mega = CreateNode(folderPath, "GR_MegaBomb", "重磅炸弹", grenadeWeapon, "榴弹伤害+150%，冷却延长30%");
        SetEffects(n_mega, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 150, ModifierType.Percentage),
            (UpgradeType.WeaponFireRate, 30, ModifierType.Percentage)
        });
        SetPrerequisite(n_mega, n_destroy);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_stun, n_burst, n_dmg1, n_cd1, n_bounce1, n_aoe,
            n_destroy, n_dmg2, n_cd2, n_bounce2,
            n_mega
        });

        AssetDatabase.SaveAssets();
        Debug.Log("榴弹技能树节点生成完成。");
    }

    private static void GenerateChainLightningNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/ChainLightning";
        EnsureDirectory(folderPath);

        WeaponStatBlock clWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_ChainLightning.asset");
        if (clWeapon == null) { Debug.LogError("找不到 ChainLightning Weapon SO"); return; }

        // === 第一层 ===
        var n_cross1 = CreateNode(folderPath, "CL_CrossI", "交叉闪电", clWeapon, "闪电链初始目标+1，伤害-20%");
        SetEffects(n_cross1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AddProjectile, 1, ModifierType.Flat),
            (UpgradeType.WeaponDamage, -20, ModifierType.Percentage)
        });

        var n_highV = CreateNode(folderPath, "CL_HighVoltage", "高压电击", clWeapon, "闪电链伤害+30%并附带1秒麻痹");
        SetEffects(n_highV, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.Stun, 1f, ModifierType.Flat)
        });

        var n_extend = CreateNode(folderPath, "CL_Extend", "闪电延续", clWeapon, "闪电链伤害+30%，弹射次数+2");
        SetEffects(n_extend, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.ChainCount, 2, ModifierType.Flat)
        });

        var n_dmg = CreateNode(folderPath, "CL_DmgBoost", "闪电伤害增幅", clWeapon, "闪电链伤害+60%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        // === 第二层 ===
        var n_cross2 = CreateNode(folderPath, "CL_CrossII", "交叉闪电 II", clWeapon, "闪电链额外选取一个初始目标");
        SetEffects(n_cross2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AddProjectile, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_cross2, n_cross1);

        var n_sympathy = CreateNode(folderPath, "CL_Sympathy", "交感电击", clWeapon, "被麻痹怪物受伤时电击范围内1名怪物");
        SetEffects(n_sympathy, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.Stun, 0, ModifierType.Flat)
        });
        SetPrerequisite(n_sympathy, n_highV);

        var n_ion = CreateNode(folderPath, "CL_IonBurst", "离子爆破", clWeapon, "闪电链每次命中产生爆炸");
        SetEffects(n_ion, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IonExplosion, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_ion, n_highV);

        // === 第三层 ===
        var n_ionDmg = CreateNode(folderPath, "CL_IonDmg", "离子电压", clWeapon, "离子爆破伤害+40%，附带1秒麻痹");
        SetEffects(n_ionDmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IonExplosionDamage, 40, ModifierType.Percentage),
            (UpgradeType.Stun, 1f, ModifierType.Flat)
        });
        SetPrerequisite(n_ionDmg, n_ion);

        var n_ionRadius = CreateNode(folderPath, "CL_IonRadius", "离子扩散", clWeapon, "离子爆破范围+100%");
        SetEffects(n_ionRadius, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IonExplosionRadius, 100, ModifierType.Percentage)
        });
        SetPrerequisite(n_ionRadius, n_ion);

        var n_conduct = CreateNode(folderPath, "CL_Conduct", "电流传导", clWeapon, "闪电链伤害-60%，但能对路径间敌人造成伤害");
        SetEffects(n_conduct, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, -60, ModifierType.Percentage)
        });
        SetPrerequisite(n_conduct, n_extend);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_cross1, n_highV, n_extend, n_dmg,
            n_cross2, n_sympathy, n_ion,
            n_ionDmg, n_ionRadius, n_conduct
        });

        AssetDatabase.SaveAssets();
        Debug.Log("闪电链技能树节点生成完成。");
    }

    // --- Helper Methods ---

    private static void GenerateFrostNovaNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/FrostNova";
        EnsureDirectory(folderPath);

        WeaponStatBlock fnWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_FrostNova.asset");
        if (fnWeapon == null) { Debug.LogError("找不到 FrostNova Weapon SO"); return; }

        // === 第一层 ===
        var n_dmg = CreateNode(folderPath, "FN_DmgBoost", "新星伤害增幅", fnWeapon, "冰霜新星伤害+60%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        var n_chain1 = CreateNode(folderPath, "FN_ChainBurst", "连环霜爆", fnWeapon, "冰霜新星额外释放1次");
        SetEffects(n_chain1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.FrostNovaExtraCast, 1, ModifierType.Flat)
        });

        var n_extreme = CreateNode(folderPath, "FN_Extreme", "极寒新星", fnWeapon, "伤害+30%，冻结持续时间+2秒");
        SetEffects(n_extreme, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 30, ModifierType.Percentage),
            (UpgradeType.FreezeDuration, 2f, ModifierType.Flat)
        });

        // === 第二层 ===
        var n_field = CreateNode(folderPath, "FN_FrostField", "极寒领域", fnWeapon, "冷冻场伤害时10%概率产生1秒冰冻并附加5秒冻伤");
        SetEffects(n_field, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 0, ModifierType.Flat)
        });
        SetPrerequisite(n_field, n_dmg);

        var n_expand = CreateNode(folderPath, "FN_Expand", "新星扩张", fnWeapon, "冰霜新星范围+100%");
        SetEffects(n_expand, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 100, ModifierType.Percentage)
        });
        SetPrerequisite(n_expand, n_chain1);

        var n_chain2 = CreateNode(folderPath, "FN_ChainBurstII", "连环霜爆 II", fnWeapon, "冰霜新星额外释放1次");
        SetEffects(n_chain2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.FrostNovaExtraCast, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_chain2, n_chain1);

        var n_center = CreateNode(folderPath, "FN_FrostHeart", "寒霜之心", fnWeapon, "冰霜新星中心区域敌人额外受到1次伤害");
        SetEffects(n_center, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.FrostNovaCenterDamage, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_center, n_extreme);

        // === 第三层 ===
        var n_absZero = CreateNode(folderPath, "FN_AbsoluteZero", "绝对零度", fnWeapon, "冰霜新星冻结持续时间对中心区域敌人翻倍");
        SetEffects(n_absZero, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AbsoluteZero, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_absZero, n_center);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_dmg, n_chain1, n_extreme,
            n_field, n_expand, n_chain2, n_center,
            n_absZero
        });

        AssetDatabase.SaveAssets();
        Debug.Log("冰霜新星技能树节点生成完成。");
    }

    private static void GenerateIceFusionNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/IceFusion";
        EnsureDirectory(folderPath);

        WeaponStatBlock fnWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_FrostNova.asset");
        if (fnWeapon == null) { Debug.LogError("找不到 FrostNova Weapon SO"); return; }

        WeaponStatBlock iceShardWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_IceShard.asset");
        if (iceShardWeapon == null) { Debug.LogWarning("找不到 IceShard Weapon SO"); }

        // === 第一层（需要同时解锁冰霜新星+冰锥术） ===
        var n_frostBite = CreateNode(folderPath, "IF_FrostBite", "刺骨寒霜", fnWeapon, "冰霜新星冻结怪物后使其每秒扣1%最大生命值");
        SetEffects(n_frostBite, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.FrostBite, 1, ModifierType.Flat)
        });
        if (iceShardWeapon != null)
        {
            n_frostBite.requiredWeapons = new List<WeaponStatBlock> { iceShardWeapon, fnWeapon };
        }

        var n_shatter1 = CreateNode(folderPath, "IF_Shatter1", "冰晶碎裂", fnWeapon, "冰霜新星结束后分裂出2个小冰锥");
        SetEffects(n_shatter1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IceCrystalShatter, 2, ModifierType.Flat)
        });
        if (iceShardWeapon != null)
        {
            n_shatter1.requiredWeapons = new List<WeaponStatBlock> { iceShardWeapon, fnWeapon };
        }

        var n_mastery = CreateNode(folderPath, "IF_Mastery", "寒冰法术精通", fnWeapon, "冰霜新星、冰锥术冷却-25%");
        SetEffects(n_mastery, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CooldownReduction, 25, ModifierType.Percentage)
        });
        if (iceShardWeapon != null)
        {
            n_mastery.requiredWeapons = new List<WeaponStatBlock> { iceShardWeapon, fnWeapon };
        }

        // === 第二层 ===
        var n_shatter2 = CreateNode(folderPath, "IF_Shatter2", "冰晶碎裂 II", fnWeapon, "冰霜新星结束后分裂出4个小冰锥");
        SetEffects(n_shatter2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IceCrystalShatter, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_shatter2, n_shatter1);

        // === 第三层 ===
        var n_shatter3 = CreateNode(folderPath, "IF_Shatter3", "冰晶碎裂 III", fnWeapon, "冰霜新星结束后分裂出4个小冰锥");
        SetEffects(n_shatter3, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.IceCrystalShatter, 4, ModifierType.Flat)
        });
        SetPrerequisite(n_shatter3, n_shatter2);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_frostBite, n_shatter1, n_mastery,
            n_shatter2, n_shatter3
        });

        AssetDatabase.SaveAssets();
        Debug.Log("冰霜融合技能节点生成完成。");
    }

    private static void GenerateOrbiterNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Orbiter";
        EnsureDirectory(folderPath);

        WeaponStatBlock orbiterWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Orbit.asset");
        if (orbiterWeapon == null) { Debug.LogError("找不到 Orbiter Weapon SO (SO_Orbit)"); return; }

        // === 第一层 (基础) ===

        var n_dmg = CreateNode(folderPath, "OB_DmgBoost", "护盾伤害增幅", orbiterWeapon, "环绕武器伤害+60%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage)
        });

        var n_speed = CreateNode(folderPath, "OB_SpeedBoost", "加速旋转", orbiterWeapon, "环绕武器旋转速度+30%");
        SetEffects(n_speed, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalSpeed, 30, ModifierType.Percentage)
        });

        var n_count = CreateNode(folderPath, "OB_CountI", "护盾增殖 I", orbiterWeapon, "环绕武器数量+1");
        SetEffects(n_count, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalCount, 1, ModifierType.Flat)
        });

        var n_dur1 = CreateNode(folderPath, "OB_DurationI", "延续 I", orbiterWeapon, "环绕武器持续时间+30%");
        SetEffects(n_dur1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDuration, 30, ModifierType.Percentage)
        });

        var n_expand1 = CreateNode(folderPath, "OB_ExpandI", "巨化 I", orbiterWeapon, "环绕武器体积+40%，冷却+10%");
        SetEffects(n_expand1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 40, ModifierType.Percentage),
            (UpgradeType.WeaponFireRate, 10, ModifierType.Percentage)
        });

        var n_reload = CreateNode(folderPath, "OB_ReloadI", "快速装填 I", orbiterWeapon, "环绕武器冷却-15%");
        SetEffects(n_reload, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CooldownReduction, 15, ModifierType.Percentage)
        });

        // === 第二层 (基础进阶) ===

        var n_dur2 = CreateNode(folderPath, "OB_DurationII", "延续 II", orbiterWeapon, "环绕武器持续时间+30%");
        SetEffects(n_dur2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDuration, 30, ModifierType.Percentage)
        });
        SetPrerequisite(n_dur2, n_dur1);

        var n_expand2 = CreateNode(folderPath, "OB_ExpandII", "巨化 II", orbiterWeapon, "环绕武器体积+40%，冷却+10%");
        SetEffects(n_expand2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 40, ModifierType.Percentage),
            (UpgradeType.WeaponFireRate, 10, ModifierType.Percentage)
        });
        SetPrerequisite(n_expand2, n_expand1);

        var n_reload2 = CreateNode(folderPath, "OB_ReloadII", "快速装填 II", orbiterWeapon, "环绕武器冷却-15%");
        SetEffects(n_reload2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CooldownReduction, 15, ModifierType.Percentage)
        });
        SetPrerequisite(n_reload2, n_reload);

        var n_count2 = CreateNode(folderPath, "OB_CountII", "护盾增殖 II", orbiterWeapon, "环绕武器数量+1");
        SetEffects(n_count2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_count2, n_count);

        // === 第三层 (中阶) ===

        // 动能吸附：需要"延续 I"+"巨化 I"
        var n_absorb = CreateNode(folderPath, "OB_Absorb", "动能吸附", orbiterWeapon, "环绕武器摧毁敌方弹射物时延长0.5秒持续时间");
        SetEffects(n_absorb, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalAbsorbProjectiles, 1, ModifierType.Flat)
        });
        SetPrerequisites(n_absorb, new List<SkillTreeNodeData> { n_dur1, n_expand1 });

        // 引力呼吸：需要"加速旋转"
        var n_breathing = CreateNode(folderPath, "OB_Breathing", "引力呼吸", orbiterWeapon, "环绕武器半径周期性扩大和缩小");
        SetEffects(n_breathing, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalExpansionBreathing, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_breathing, n_speed);

        // 充能释放：需要"快速装填 II"
        var n_release = CreateNode(folderPath, "OB_Release", "充能释放", orbiterWeapon, "环绕武器持续时间结束时释放范围爆炸冲击波");
        SetEffects(n_release, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.OrbitalReleaseExplosion, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_release, n_reload2);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_dmg, n_speed, n_count, n_dur1, n_expand1, n_reload,
            n_dur2, n_expand2, n_reload2, n_count2,
            n_absorb, n_breathing, n_release
        });

        AssetDatabase.SaveAssets();
        Debug.Log("环绕武器技能树节点生成完成。");
    }

    private static void GenerateLandmineNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Landmine";
        EnsureDirectory(folderPath);

        WeaponStatBlock mineWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Landmine.asset");
        if (mineWeapon == null) { Debug.LogError("找不到 Landmine Weapon SO (SO_Landmine)"); return; }

        WeaponStatBlock fireballWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Fireball.asset");

        // === 第一层 (基础) ===

        var n_dmg = CreateNode(folderPath, "LM_DmgBoost", "烈性火药", mineWeapon, "地雷伤害+60%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeDamage, 60, ModifierType.Percentage)
        });

        var n_cd = CreateNode(folderPath, "LM_CooldownI", "引信缩短 I", mineWeapon, "地雷布置冷却-15%");
        SetEffects(n_cd, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CooldownReduction, 15, ModifierType.Percentage)
        });

        var n_radius = CreateNode(folderPath, "LM_RadiusI", "破片装药 I", mineWeapon, "地雷爆炸范围+30%");
        SetEffects(n_radius, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 30, ModifierType.Percentage)
        });

        var n_count = CreateNode(folderPath, "LM_CountI", "雷区扩张", mineWeapon, "每次布置地雷数+1，冷却+30%");
        SetEffects(n_count, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AddProjectile, 1, ModifierType.Flat),
            (UpgradeType.WeaponFireRate, 30, ModifierType.Percentage)
        });

        // === 第二层 (基础进阶) ===

        var n_cd2 = CreateNode(folderPath, "LM_CooldownII", "引信缩短 II", mineWeapon, "地雷布置冷却-15%");
        SetEffects(n_cd2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CooldownReduction, 15, ModifierType.Percentage)
        });
        SetPrerequisite(n_cd2, n_cd);

        var n_radius2 = CreateNode(folderPath, "LM_RadiusII", "破片装药 II", mineWeapon, "地雷爆炸范围+30%");
        SetEffects(n_radius2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 30, ModifierType.Percentage)
        });
        SetPrerequisite(n_radius2, n_radius);

        // === 第三层 (中阶 - 质变) ===

        // 引力陷阱：需要「破片装药 I」
        var n_gravity = CreateNode(folderPath, "LM_GravityTrap", "引力陷阱", mineWeapon, "地雷武装后吸引附近敌人，伤害-10%");
        SetEffects(n_gravity, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LandmineGravityTrap, 1, ModifierType.Flat),
            (UpgradeType.AoeDamage, -10, ModifierType.Percentage)
        });
        SetPrerequisite(n_gravity, n_radius);

        // 震撼弹片：需要「烈性火药」
        var n_stun = CreateNode(folderPath, "LM_Stun", "震撼弹片", mineWeapon, "地雷爆炸附加1.5秒眩晕，冷却+15%");
        SetEffects(n_stun, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LandmineStun, 1, ModifierType.Flat),
            (UpgradeType.WeaponFireRate, 15, ModifierType.Percentage)
        });
        SetPrerequisite(n_stun, n_dmg);

        // 能量回收：需要「引信缩短 I」
        var n_energy = CreateNode(folderPath, "LM_EnergyRecovery", "能量回收", mineWeapon, "地雷击杀时15%概率获得额外大招能量");
        SetEffects(n_energy, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LandmineEnergyRecovery, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_energy, n_cd);

        // 引力黑洞：需要「引力陷阱」
        var n_blackhole = CreateNode(folderPath, "LM_BlackHole", "引力黑洞", mineWeapon, "地雷爆炸后留下2.5秒黑洞吸引怪物，范围-10%");
        SetEffects(n_blackhole, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.LandmineBlackHole, 1, ModifierType.Flat),
            (UpgradeType.AoeRadius, -10, ModifierType.Percentage)
        });
        SetPrerequisite(n_blackhole, n_gravity);

        // === 融合技能（需要火球+地雷） ===
        if (fireballWeapon != null)
        {
            var n_napalm = CreateNode(folderPath, "LM_Napalm", "凝固汽油弹", mineWeapon, "地雷爆炸后留下4秒燃烧区域");
            SetEffects(n_napalm, new List<(UpgradeType, float, ModifierType)> {
                (UpgradeType.FusionNapalm, 1, ModifierType.Flat)
            });
            n_napalm.requiredWeapons = new List<WeaponStatBlock> { mineWeapon, fireballWeapon };
            SetPrerequisite(n_napalm, n_blackhole);

            AddToDatabase(new List<SkillTreeNodeData> {
                n_dmg, n_cd, n_radius, n_count,
                n_cd2, n_radius2,
                n_gravity, n_stun, n_energy, n_blackhole,
                n_napalm
            });
        }
        else
        {
            AddToDatabase(new List<SkillTreeNodeData> {
                n_dmg, n_cd, n_radius, n_count,
                n_cd2, n_radius2,
                n_gravity, n_stun, n_energy, n_blackhole
            });
        }

        AssetDatabase.SaveAssets();
        Debug.Log("地雷技能树节点生成完成。");
    }

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

    // === Aura 辅助型光环技能树 ===
    private static void GenerateAuraNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Aura";
        EnsureDirectory(folderPath);

        WeaponStatBlock auraWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Aura.asset");
        if (auraWeapon == null) { Debug.LogError("找不到 Aura Weapon SO"); return; }

        // === 第一层：基础强化 ===
        var n_expand = CreateNode(folderPath, "AURA_Expand", "光环扩展", auraWeapon, "光环范围+25%");
        SetEffects(n_expand, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 25, ModifierType.Percentage)
        });

        var n_freq = CreateNode(folderPath, "AURA_Frequency", "磁场共振", auraWeapon, "光环触发频率+20%");
        SetEffects(n_freq, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponFireRate, 20, ModifierType.Percentage)
        });

        var n_dmg = CreateNode(folderPath, "AURA_DmgBoost", "光环强化", auraWeapon, "光环伤害+40%");
        SetEffects(n_dmg, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 40, ModifierType.Percentage)
        });

        // === 第二层：进阶I（需要第一层任意一个前置） ===
        var n_healI = CreateNode(folderPath, "AURA_HealI", "生命脉动I", auraWeapon, "每60秒恢复3点生命值");
        SetEffects(n_healI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraHealingPulse, 3, ModifierType.Flat)
        });

        var n_slowI = CreateNode(folderPath, "AURA_SlowI", "迟缓力场I", auraWeapon, "光环范围内敌人移速降低25%（与冰系减速叠加）");
        SetEffects(n_slowI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraSluggishField, 25, ModifierType.Flat)
        });

        var n_fragileI = CreateNode(folderPath, "AURA_FragileI", "脆弱印记I", auraWeapon, "光环范围内敌人受到全部伤害+8%，但触发间隔增加");
        SetEffects(n_fragileI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraFragileMark, 8, ModifierType.Flat)
        });

        SetPrerequisites(n_healI, new List<SkillTreeNodeData> { n_expand, n_freq, n_dmg });
        SetPrerequisites(n_slowI, new List<SkillTreeNodeData> { n_expand, n_freq, n_dmg });
        SetPrerequisites(n_fragileI, new List<SkillTreeNodeData> { n_expand, n_freq, n_dmg });

        // === 第三层：进阶II（需要对应I级前置） ===
        var n_healII = CreateNode(folderPath, "AURA_HealII", "生命脉动II", auraWeapon, "每60秒恢复6点生命值（替代I级）");
        SetEffects(n_healII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraHealingPulse, 6, ModifierType.Flat)
        });
        SetPrerequisite(n_healII, n_healI);

        var n_slowII = CreateNode(folderPath, "AURA_SlowII", "迟缓力场II", auraWeapon, "光环范围内敌人移速降低35%（与冰系减速叠加）");
        SetEffects(n_slowII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraSluggishField, 35, ModifierType.Flat)
        });
        SetPrerequisite(n_slowII, n_slowI);

        var n_fragileII = CreateNode(folderPath, "AURA_FragileII", "脆弱印记II", auraWeapon, "光环范围内敌人受到全部伤害+15%，触发间隔减少");
        SetEffects(n_fragileII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AuraFragileMark, 15, ModifierType.Flat)
        });
        SetPrerequisite(n_fragileII, n_fragileI);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_expand, n_freq, n_dmg,
            n_healI, n_slowI, n_fragileI,
            n_healII, n_slowII, n_fragileII
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[技能树生成] Aura (辅助) 节点已生成完毕！共9个节点</color>");
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

    // ==================== 灵能飞刀 ====================
    private static void GenerateFlameDaggerNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/FlameDagger";
        EnsureDirectory(folderPath);

        WeaponStatBlock daggerWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_FlameDagger.asset");
        if (daggerWeapon == null) { Debug.LogError("找不到 FlameDagger Weapon SO"); return; }

        // === 第一层：基础强化 I ===
        var n_dmgI = CreateNode(folderPath, "DAGGER_DmgBoostI", "烈焰增幅I", daggerWeapon, "飞刀伤害+30%，环绕速度-15%");
        SetEffects(n_dmgI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerDamageBoost, 1, ModifierType.Flat)
        });

        var n_countI = CreateNode(folderPath, "DAGGER_ExtraCountI", "多重飞刀I", daggerWeapon, "额外+1把飞刀，每把伤害-15%");
        SetEffects(n_countI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerExtraCount, 1, ModifierType.Flat)
        });

        var n_speedI = CreateNode(folderPath, "DAGGER_SpeedBoostI", "焰舞加速I", daggerWeapon, "环绕速度x1.3，伤害间隔-20%");
        SetEffects(n_speedI, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerSpeedBoost, 1, ModifierType.Flat)
        });

        // === 第一层：基础强化 II（需对应I级前置） ===
        var n_dmgII = CreateNode(folderPath, "DAGGER_DmgBoostII", "烈焰增幅II", daggerWeapon, "飞刀伤害+60%，环绕速度-25%（替代I级）");
        SetEffects(n_dmgII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerDamageBoost, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_dmgII, n_dmgI);

        var n_countII = CreateNode(folderPath, "DAGGER_ExtraCountII", "多重飞刀II", daggerWeapon, "额外+2把飞刀，每把伤害-25%（替代I级）");
        SetEffects(n_countII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerExtraCount, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_countII, n_countI);

        var n_speedII = CreateNode(folderPath, "DAGGER_SpeedBoostII", "焰舞加速II", daggerWeapon, "环绕速度x1.6，伤害间隔-35%（替代I级）");
        SetEffects(n_speedII, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerSpeedBoost, 2, ModifierType.Flat)
        });
        SetPrerequisite(n_speedII, n_speedI);

        // === 第二层：进阶分支（需第一层任意1个前置） ===
        var n_homing = CreateNode(folderPath, "DAGGER_Homing", "锁魂追击", daggerWeapon, "索敌范围+50%，锁定+2秒，环绕半径-50%（紧贴目标提高命中率）");
        SetEffects(n_homing, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerHoming, 1, ModifierType.Flat)
        });
        SetPrerequisites(n_homing, new List<SkillTreeNodeData> { n_dmgI, n_countI, n_speedI });

        var n_clone = CreateNode(folderPath, "DAGGER_Clone", "刃影分身", daggerWeapon, "命中时1%概率生成分身飞刀（存在10秒，20%伤害），环绕半径-50%");
        SetEffects(n_clone, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerClone, 1, ModifierType.Flat)
        });
        SetPrerequisites(n_clone, new List<SkillTreeNodeData> { n_dmgI, n_countI, n_speedI });

        // 灵能烙印：需要火球的 Fireball_Ignite 作为跨武器前置
        var n_ignite = CreateNode(folderPath, "DAGGER_Ignite", "灵能烙印", daggerWeapon, "飞刀命中有20%概率点燃敌人（需解锁火球爆燃）");
        SetEffects(n_ignite, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerIgnite, 1, ModifierType.Flat)
        });
        SetPrerequisites(n_ignite, new List<SkillTreeNodeData> { n_dmgI, n_countI, n_speedI });
        // 跨武器前置：火球的 Fireball_Ignite 节点
        SkillTreeNodeData fireballIgnite = LoadAsset<SkillTreeNodeData>("Assets/_TheFirst/Prefabs/Skill Tree/Fireball/Fireball_Ignite.asset");
        if (fireballIgnite != null)
        {
            SetPrerequisite(n_ignite, fireballIgnite);
        }
        else
        {
            Debug.LogWarning("[技能树生成] 未找到 Fireball_Ignite 节点，灵能烙印的跨武器前置未设置");
        }

        // === 第三层：高级 ===
        var n_lifeSteal = CreateNode(folderPath, "DAGGER_LifeSteal", "灵魂收割", daggerWeapon, "飞刀击杀敌人恢复2%最大HP");
        SetEffects(n_lifeSteal, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerLifeSteal, 1, ModifierType.Flat)
        });
        SetPrerequisites(n_lifeSteal, new List<SkillTreeNodeData> { n_homing, n_clone });

        var n_chainExplosion = CreateNode(folderPath, "DAGGER_ChainExplosion", "连锁灵刃", daggerWeapon, "命中被点燃敌人触发小范围爆破伤害");
        SetEffects(n_chainExplosion, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.DaggerChainExplosion, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_chainExplosion, n_ignite);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_dmgI, n_countI, n_speedI,
            n_dmgII, n_countII, n_speedII,
            n_homing, n_clone, n_ignite,
            n_lifeSteal, n_chainExplosion
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=red>[技能树生成] 灵能飞刀节点已生成完毕！共11个节点</color>");
    }

    /// <summary>
    /// 斩击技能树节点生成 — 3层9个节点
    /// 设计原则：只强化底层数值，不涉及模式机制，确保与融合大招形态切换不冲突
    /// </summary>
    private static void GenerateBladeNodes()
    {
        string folderPath = "Assets/_TheFirst/Prefabs/Skill Tree/Blade";
        EnsureDirectory(folderPath);

        WeaponStatBlock bladeWeapon = LoadAsset<WeaponStatBlock>("Assets/_TheFirst/GameData/SO_Weapon/SO_Blade.asset");
        if (bladeWeapon == null) { Debug.LogError("找不到 Blade Weapon SO"); return; }

        // === 第一层（基础） ===

        var n_sharp1 = CreateNode(folderPath, "Blade_Sharp_I", "利刃 I", bladeWeapon, "斩击伤害 +40%");
        SetEffects(n_sharp1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 40, ModifierType.Percentage)
        });

        var n_swift = CreateNode(folderPath, "Blade_Swift", "迅捷斩", bladeWeapon, "攻击速度 +15%");
        SetEffects(n_swift, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponFireRate, 15, ModifierType.Percentage)
        });

        var n_weakness = CreateNode(folderPath, "Blade_Weakness", "弱点打击", bladeWeapon, "暴击率 +10%");
        SetEffects(n_weakness, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CritRate, 10, ModifierType.Percentage)
        });

        // === 第二层（进阶，需第一层前置） ===

        var n_sharp2 = CreateNode(folderPath, "Blade_Sharp_II", "利刃 II", bladeWeapon, "斩击伤害 +60%，攻速 -10%");
        SetEffects(n_sharp2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.WeaponDamage, 60, ModifierType.Percentage),
            (UpgradeType.WeaponFireRate, -10, ModifierType.Percentage)
        });
        SetPrerequisite(n_sharp2, n_sharp1);

        var n_armorBreak = CreateNode(folderPath, "Blade_ArmorBreak", "破甲斩", bladeWeapon, "暴击伤害 +50%");
        SetEffects(n_armorBreak, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CritDamage, 50, ModifierType.Percentage)
        });
        SetPrerequisite(n_armorBreak, n_weakness);

        var n_range1 = CreateNode(folderPath, "Blade_Range_I", "扩大范围 I", bladeWeapon, "斩击范围 +40%");
        SetEffects(n_range1, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 40, ModifierType.Percentage)
        });
        SetPrerequisite(n_range1, n_swift);

        // === 第三层（高级，需第二层前置） ===

        var n_multi = CreateNode(folderPath, "Blade_MultiSlash", "多重斩", bladeWeapon, "刀光数量 +1");
        SetEffects(n_multi, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.SlashCount, 1, ModifierType.Flat)
        });
        SetPrerequisite(n_multi, n_sharp2);

        var n_lethal = CreateNode(folderPath, "Blade_Lethal", "致命一击", bladeWeapon, "暴击率 +15%，暴击伤害 +30%");
        SetEffects(n_lethal, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.CritRate, 15, ModifierType.Percentage),
            (UpgradeType.CritDamage, 30, ModifierType.Percentage)
        });
        SetPrerequisite(n_lethal, n_armorBreak);

        var n_range2 = CreateNode(folderPath, "Blade_Range_II", "扩大范围 II", bladeWeapon, "斩击范围 +60%");
        SetEffects(n_range2, new List<(UpgradeType, float, ModifierType)> {
            (UpgradeType.AoeRadius, 60, ModifierType.Percentage)
        });
        SetPrerequisite(n_range2, n_range1);

        // --- 添加到数据库 ---
        AddToDatabase(new List<SkillTreeNodeData> {
            n_sharp1, n_swift, n_weakness,
            n_sharp2, n_armorBreak, n_range1,
            n_multi, n_lethal, n_range2
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[技能树生成] 斩击技能树节点已生成完毕！共9个节点</color>");
    }
}
