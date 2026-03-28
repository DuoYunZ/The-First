using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 一次性编辑器工具：批量填充所有 SkillTreeNodeData 中 UpgradeOption 的英文描述。
/// 使用方法：Unity 菜单 → Tools → 填充技能英文描述
/// 运行后可删除此脚本。
/// </summary>
public class FillSkillDescriptions : EditorWindow
{
    [MenuItem("Tools/Fill Skill Descriptions EN")]
    static void Execute()
    {
        // 中文 -> 英文 映射表
        var map = new Dictionary<string, string>
        {
            // ===== Aura 光环 =====
            { "光环伤害+40%", "Aura DMG +40%" },
            { "光环范围+25%", "Aura range +25%" },
            { "光环范围内敌人受到全部伤害+8%", "Enemies in aura take +8% total DMG" },
            { "光环范围内敌人受到全部伤害+15%", "Enemies in aura take +15% total DMG" },
            { "光环触发频率+20%", "Aura tick rate +20%" },
            { "每60秒恢复5点生命值，伤害减少20%", "Heal 5 HP per 60s, DMG -20%" },
            { "每60秒恢复6点生命值", "Heal 6 HP per 60s" },
            { "光环范围内敌人移速降低10%", "Slow enemies in aura by 10%" },
            { "光环范围内敌人移速降低20%", "Slow enemies in aura by 20%" },

            // ===== ChainLightning 闪电链 =====
            { "闪电链初始目标+1，伤害-20%", "Chain Lightning +1 target, DMG -20%" },
            { "闪电链额外选取一个初始目标", "Chain Lightning +1 extra target" },
            { "闪电链伤害+60%", "Chain Lightning DMG +60%" },
            { "闪电链伤害+30%，弹射次数+2", "Chain Lightning DMG +30%, +2 bounces" },
            { "闪电链伤害+30%并附带1秒麻痹", "Chain Lightning DMG +30%, 1s stun" },
            { "闪电链每次命中产生爆炸", "Each hit triggers an explosion" },
            { "离子爆破伤害+40%，附带1秒麻痹", "Ion Burst DMG +40%, 1s stun" },
            { "离子爆破范围+100%", "Ion Burst radius +100%" },

            // ===== Fireball 火焰球 =====
            { "爆炸范围 +80%", "Blast radius +80%" },
            { "火球伤害 +50%", "Fireball DMG +50%" },
            { "命中后引燃敌人 6 秒", "Ignite enemies for 6s on hit" },
            { "火球伤害 +20%，冷却 -10%", "Fireball DMG +20%, cooldown -10%" },
            { "额外释放 +1 次，伤害 -40%", "Extra +1 cast, DMG -40%" },
            { "再额外释放 +1 次，伤害 -30%", "Extra +1 cast, DMG -30%" },
            { "再额外释放 +1 次，伤害 -20%", "Extra +1 cast, DMG -20%" },
            { "每次释放3个火球", "Fire 3 fireballs per cast" },
            { "爆炸后向四周溅射 2 个小火花", "Spawn 2 sparks on explosion" },
            { "爆炸后向四周溅射 5 个小火花", "Spawn 5 sparks on explosion" },
            { "投射物数量 +1", "Projectile count +1" },
            { "燃烧每跳额外造成目标 1% 最大生命值的伤害", "Burn deals extra 1% max HP per tick" },

            // ===== FlameDagger 灵能飞刃 =====
            { "命中被点燃敌人触发小范围爆破伤害", "Hit ignited enemy triggers AoE burst" },
            { "命中时1%概率生成分身飞刀（存在10秒，20%伤害），环绕半径-50%", "1% clone chance (10s, 20% DMG), orbit radius -50%" },
            { "飞刀伤害+30%，环绕速度-15%", "Dagger DMG +30%, orbit speed -15%" },
            { "飞刀伤害+60%，环绕速度-25%（替代I级）", "Dagger DMG +60%, orbit speed -25%" },
            { "额外+1把飞刀，每把伤害-15%", "Extra +1 dagger, each -15% DMG" },
            { "额外+2把飞刀，每把伤害-25%（替代I级）", "Extra +2 daggers, each -25% DMG" },
            { "索敌范围+50%，锁定+2秒，环绕半径-50%（紧贴目标提高命中率）", "Track range +50%, lock +2s, orbit -50%" },
            { "飞刀命中有20%概率点燃敌人（需解锁火球爆燃）", "20% ignite chance on hit" },
            { "飞刀击杀敌人恢复2%最大HP", "Kill heals 2% max HP" },
            { "飞行速度x1.3", "Flight speed x1.3" },
            { "飞行速度x1.6", "Flight speed x1.6" },

            // ===== FrostNova 冰霜新星 =====
            { "冰霜新星冻结持续时间对中心区域敌人翻倍", "Freeze duration doubled for center enemies" },
            { "冰霜新星额外释放1次", "Frost Nova +1 extra cast" },
            { "冰霜新星伤害+60%", "Frost Nova DMG +60%" },
            { "冰霜新星范围+50%", "Frost Nova range +50%" },
            { "伤害+30%，冻结持续时间+2秒", "DMG +30%, freeze +2s" },
            { "冷冻场伤害时10%概率产生1秒冰冻并附加5秒冻伤", "10% freeze chance + 5s frostbite on chill" },
            { "冰霜新星中心区域敌人额外受到1次伤害", "Center enemies take 1 extra hit" },

            // ===== Grenade 燃烧瓶 =====
            { "榴弹爆炸范围+100%", "Grenade blast radius +100%" },
            { "榴弹伤害-20%，爆炸后弹跳1次", "Grenade DMG -20%, +1 bounce" },
            { "再弹跳1次，伤害-20%", "Extra bounce, DMG -20%" },
            { "榴弹额外释放1次", "Grenade +1 extra cast" },
            { "榴弹冷却-20%", "Grenade cooldown -20%" },
            { "榴弹伤害+150%", "Grenade DMG +150%" },
            { "榴弹伤害+60%", "Grenade DMG +60%" },
            { "榴弹伤害+150%，冷却延长30%", "Grenade DMG +150%, cooldown +30%" },
            { "榴弹爆炸时附带1.5秒眩晕", "Grenade stuns for 1.5s" },

            // ===== Hurricane 小龙卷 =====
            { "旋风术伤害+40%，击退+50%", "Tornado DMG +40%, knockback +50%" },
            { "旋风术伤害+60%", "Tornado DMG +60%" },
            { "旋风术额外释放1次，伤害-15%", "Tornado +1 cast, DMG -15%" },
            { "旋风术额外释放一次", "Tornado +1 extra cast" },
            { "旋风术伤害-30%，额外放2次", "Tornado DMG -30%, +2 casts" },
            { "旋风术穿透+6", "Tornado pierce +6" },
            { "旋风命中怪物时生成1个穿透效果和伤害更低的小旋风", "Hit spawns 1 mini tornado" },
            { "旋风每命中1个怪物都额外生成1个小旋风", "Each hit spawns 1 mini tornado" },
            { "旋风术命中怪物时造成小范围伤害并牵引周围怪物", "Hit deals AoE and pulls enemies" },
            { "真空伤害+50%，并施加2秒眩晕", "Vacuum DMG +50%, 2s stun" },
            { "旋风穿透耗尽时往其他方向再次释放一道无强化旋风", "On expire, fires another tornado" },

            // ===== IceFusion 冰系融合 =====
            { "冰霜新星冻结怪物后使其每秒扣1%最大生命值", "Frozen enemies lose 1% max HP/s" },
            { "冰霜新星、冰锥术冷却-25%", "Frost Nova & Ice Shard cooldown -25%" },
            { "冰霜新星结束后分裂出2个小冰锥", "Frost Nova shatters into 2 ice shards" },
            { "冰霜新星结束后分裂出4个小冰锥", "Frost Nova shatters into 4 ice shards" },

            // ===== IceShard 冰锥术 =====
            { "伤害 +60%", "DMG +60%" },
            { "伤害 +30%，冰冻概率增加10%", "DMG +30%, +10% freeze chance" },
            { "速度 -30%，伤害 +50%，穿透 +1", "Speed -30%, DMG +50%, pierce +1" },
            { "穿透 +2", "Pierce +2" },
            { "额外释放 1 次，伤害 -20%", "Extra cast, DMG -20%" },
            { "额外释放 +1 次", "Extra +1 cast" },
            { "伤害 +30%，穿透 +2", "DMG +30%, pierce +2" },
            { "首次命中后分裂为 3 个小冰锥", "Splits into 3 shards on 1st hit" },
            { "分裂出的小冰锥伤害 +80%", "Split shard DMG +80%" },
            { "分裂的冰锥继承穿透和冰冻概率", "Split shards inherit pierce & freeze" },
            { "子弹 +1，伤害 -40%", "Projectile +1, DMG -40%" },

            // ===== Landmine 地雷 =====
            { "地雷爆炸后留下2.5秒黑洞吸引怪物，范围-10%", "Explosion leaves 2.5s black hole, range -10%" },
            { "地雷布置冷却-15%", "Mine cooldown -15%" },
            { "每次布置地雷数+1，冷却+30%", "Mine count +1, cooldown +30%" },
            { "地雷伤害+60%", "Mine DMG +60%" },
            { "地雷击杀时15%概率获得额外大招能量", "15% ult energy on mine kill" },
            { "地雷武装后吸引附近敌人，伤害-10%", "Armed mine attracts enemies, DMG -10%" },
            { "地雷爆炸后留下4秒燃烧区域", "Explosion leaves 4s fire zone" },
            { "地雷爆炸范围+30%", "Mine blast radius +30%" },
            { "地雷爆炸附加1.5秒眩晕，冷却+15%", "Mine stuns 1.5s, cooldown +15%" },

            // ===== LightningStrike 雷击 =====
            { "落雷0.3秒后再落一道，伤害-20%", "2nd bolt after 0.3s, DMG -20%" },
            { "落雷后再额外释放一道", "Extra bolt after strike" },
            { "雷击伤害+80%", "Lightning DMG +80%" },
            { "磁暴后生成持续电磁场，场内攻击暴击率提升", "Storm spawns EM field, +crit rate inside" },
            { "雷击伤害+30%，麻痹时间+1.5秒", "Lightning DMG +30%, stun +1.5s" },
            { "雷击命中后触发一次性范围爆炸", "Hit triggers AoE explosion" },
            { "雷击直击伤害+150%，杀死怪物时在小范围内再释放1次单体雷击", "Direct DMG +150%, kill triggers nearby bolt" },
            { "电磁场伤害+400%，持续时间+2秒", "EM field DMG +400%, duration +2s" },
            { "磁暴伤害与范围+75%", "Storm DMG & range +75%" },
            { "雷击初始目标+1", "Lightning +1 initial target" },

            // ===== Orbiter 大地岩盾 =====
            { "环绕武器摧毁敌方弹射物时延长0.5秒持续时间", "Destroying projectile extends 0.5s" },
            { "环绕武器半径周期性扩大和缩小", "Orbit radius pulses in and out" },
            { "环绕武器数量+1", "Orbiter count +1" },
            { "环绕武器伤害+60%", "Orbiter DMG +60%" },
            { "环绕武器持续时间+30%", "Orbiter duration +30%" },
            { "环绕武器体积+40%，冷却+10%", "Orbiter size +40%, cooldown +10%" },
            { "环绕武器冷却-15%", "Orbiter cooldown -15%" },
            { "环绕武器冷却-30%", "Orbiter cooldown -30%" },
            { "环绕武器旋转速度+30%", "Orbiter spin speed +30%" },
        };

        // 搜索 Skill Tree 文件夹下的所有子文件夹
        string basePath = "Assets/_TheFirst/Prefabs/Skill Tree";
        string[] folders = { "Aura", "ChainLightning", "Fireball", "FlameDagger", "FrostNova",
                             "Grenade", "Hurricane", "IceFusion", "IceShard", "Landmine",
                             "LightningStrike", "Orbiter" };

        int totalUpdated = 0;
        int totalSkipped = 0;
        List<string> notFound = new List<string>();

        foreach (var folder in folders)
        {
            string searchPath = $"{basePath}/{folder}";
            string[] guids = AssetDatabase.FindAssets("t:SkillTreeNodeData", new[] { searchPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var node = AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(path);
                if (node == null || node.possibleOptions == null) continue;

                bool dirty = false;
                foreach (var option in node.possibleOptions)
                {
                    if (string.IsNullOrEmpty(option.description)) continue;
                    if (!string.IsNullOrEmpty(option.descriptionEN)) continue; // 已有英文则跳过

                    string zhDesc = option.description.Trim();
                    if (map.TryGetValue(zhDesc, out string enDesc))
                    {
                        option.descriptionEN = enDesc;
                        dirty = true;
                        totalUpdated++;
                    }
                    else
                    {
                        notFound.Add($"{node.name}: {zhDesc}");
                        totalSkipped++;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(node);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = $"完成！已填充 {totalUpdated} 条英文描述，跳过 {totalSkipped} 条。";
        if (notFound.Count > 0)
        {
            message += "\n\n未匹配的描述:\n" + string.Join("\n", notFound.Take(20));
        }
        EditorUtility.DisplayDialog("批量填充英文描述", message, "OK");
        Debug.Log(message);
    }
}
