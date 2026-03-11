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
