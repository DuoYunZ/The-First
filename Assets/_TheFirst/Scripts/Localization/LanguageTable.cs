using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 静态翻译表，存储所有 UI 文本的多语言翻译。
/// 新增翻译只需在 InitTable() 中添加一行即可。
/// </summary>
public static class LanguageTable
{
    // key -> (语言 -> 翻译文本)
    private static Dictionary<string, Dictionary<SystemLanguage, string>> _table;

    /// <summary>
    /// 获取指定 key 和语言的翻译文本
    /// </summary>
    public static string Get(string key, SystemLanguage language)
    {
        if (_table == null) InitTable();

        if (_table.TryGetValue(key, out var translations))
        {
            // 优先返回目标语言，其次回退到中文
            if (translations.TryGetValue(language, out string text))
                return text;

            if (translations.TryGetValue(SystemLanguage.ChineseSimplified, out string fallback))
                return fallback;
        }

        // 找不到 key 时返回 key 本身，方便排查
        Debug.LogWarning($"[本地化] 未找到翻译: key='{key}', lang={language}");
        return $"[{key}]";
    }

    /// <summary>
    /// 尝试获取翻译文本，找不到时返回 null（不打印警告）
    /// </summary>
    public static string TryGet(string key, SystemLanguage language)
    {
        if (_table == null) InitTable();

        if (_table.TryGetValue(key, out var translations))
        {
            if (translations.TryGetValue(language, out string text))
                return text;

            if (translations.TryGetValue(SystemLanguage.ChineseSimplified, out string fallback))
                return fallback;
        }

        return null;
    }

    /// <summary>
    /// 通过中文武器名反查英文翻译
    /// 遍历所有 weapon.* 条目，找到中文值匹配的条目后返回英文值
    /// 找不到则返回原始名称
    /// </summary>
    public static string LocalizeWeaponName(string chineseName, SystemLanguage targetLang)
    {
        if (_table == null) InitTable();
        if (targetLang == SystemLanguage.ChineseSimplified) return chineseName;

        foreach (var kvp in _table)
        {
            if (!kvp.Key.StartsWith("weapon.")) continue;

            if (kvp.Value.TryGetValue(SystemLanguage.ChineseSimplified, out string zhName)
                && zhName == chineseName)
            {
                if (kvp.Value.TryGetValue(targetLang, out string translated))
                    return translated;
            }
        }
        return chineseName; // 找不到则回退原名
    }

    /// <summary>
    /// 初始化翻译表。所有翻译条目集中管理于此。
    /// 格式：Add("key", "中文文本", "English Text")
    /// 支持 string.Format 占位符：{0}, {1}, ...
    /// </summary>
    private static void InitTable()
    {
        _table = new Dictionary<string, Dictionary<SystemLanguage, string>>();

        // ===== 战斗 UI =====
        Add("ui.wave",               "波次: {0}",                 "Wave: {0}");
        Add("ui.wave_named",         "波次: {0} - {1}",           "Wave: {0} - {1}");
        Add("ui.next_wave",          "下一波: {0}s",              "Next Wave: {0}s");
        Add("ui.enemies_remaining",  "剩余敌人: {0}",             "Enemies: {0}");

        // ===== 结算界面 =====
        Add("ui.mission_complete",   "任务完成",                  "Mission Complete");
        Add("ui.mission_failed",     "任务失败",                  "Mission Failed");
        Add("ui.battle_report",      "战报",                      "Battle Report");
        Add("ui.survival_time",      "存活时间",                  "Survival Time");
        Add("ui.kill_count",         "击杀数",                    "Kills");
        Add("ui.gold_earned",        "获得金币",                  "Gold Earned");
        Add("ui.restart",            "重新开始",                  "Restart");
        Add("ui.return",             "返回",                      "Return");

        // ===== 融合 UI =====
        Add("ui.no_weapon",          "没有武器",                  "No Weapon");
        Add("ui.empty_slot",         "[ 空插槽 ]",               "[ Empty Slot ]");
        Add("ui.socketed",           "[已镶嵌: {0}]",            "[Socketed: {0}]");

        // ===== Meta 升级 =====
        Add("ui.max_level",          "已满级 (加成: {0:F1})",     "MAX (Bonus: {0:F1})");
        Add("ui.current_next",       "当前: {0:F1} → 下一级: {1:F1}", "Current: {0:F1} → Next: {1:F1}");
        Add("ui.cost",               "费用: {0}",                "Cost: {0}");

        // ===== 通知 =====
        Add("ui.unlocked",           "已解锁: {0}",              "Unlocked: {0}");

        // ===== 武器统计 =====
        Add("ui.damage",             "伤害: {0:N0}",             "DMG: {0:N0}");

        // ===== 主菜单 =====
        Add("ui.start_game",         "开始游戏",                 "Start Game");
        Add("ui.quit_game",          "退出游戏",                 "Quit");

        // ===== 暂停菜单 (Hub / Combat 共用) =====
        Add("ui.resume",             "继续游戏",                 "Resume");
        Add("ui.main_menu",          "主菜单",                   "Main Menu");
        Add("ui.return_hub",         "返回枢纽",                 "Return");

        // ===== 设置面板 =====
        Add("ui.settings",           "设置",                     "Settings");
        Add("ui.master_volume",      "总音量",                   "Master");
        Add("ui.bgm_volume",         "游戏音量",                 "Music");
        Add("ui.sfx_volume",         "音效",                     "SFX");
        Add("ui.resolution",         "分辨率",                   "Display");
        Add("ui.fullscreen",         "全屏显示",                 "Fullscreen");
        Add("ui.language",           "语言",                     "Lang");
        Add("ui.back",               "返回",                     "Back");

        // ===== 武器名称 (中文必须与 WeaponStatBlock.weaponName 完全一致) =====
        Add("weapon.Fireball",       "火球术",                   "Fireball");
        Add("weapon.ChainLightning", "闪电链",                   "Chain Lightning");
        Add("weapon.SupportAura",    "光环",                     "Aura");
        Add("weapon.Hurricane",      "旋风术",                   "Tornado");
        Add("weapon.Grenade",        "Grenade",                  "Grenade");
        Add("weapon.Landmine",       "地雷",                     "Landmine");
        Add("weapon.Orbit",          "大地岩盾",                 "Rock Shield");
        Add("weapon.FlameDagger",    "灵能飞刃",                 "Flame Dagger");
        Add("weapon.IceShard",       "冰锥术",                   "Ice Shard");
        Add("weapon.FrostNova",      "寒冰新星",                 "Frost Nova");
        Add("weapon.LightningStrike","落雷",                     "Lightning Strike");
        Add("weapon.ExtremeIceShard","极寒冰锥",                 "Cryo Lance");
        Add("weapon.Blade",          "斩击",                     "Blade");

        // ===== 连携技名称 (key = combo.{文件名}) =====
        Add("combo.Combo_Fireball_Landmine",       "喷火塔",     "Flamethrower");
        Add("combo.Combo_Fireball_hurricane",      "火焰风暴",   "Firestorm");
        Add("combo.Combo_FlameDagger_Fireball",    "火焰灵刃",   "Flame Blade");
        Add("combo.Combo_Hurricane_Aura",          "风之环",     "Wind Ring");
        Add("combo.Combo_Orbit_ChainLightning",    "雷霆盾",     "Thunder Shield");

        // ===== 升级卡数值预览标签 =====
        Add("stat.attack",           "攻击",                     "ATK");
        Add("stat.cooldown",         "冷却",                     "CD");
        Add("stat.range",            "范围",                     "Range");
        Add("stat.speed",            "速度",                     "Speed");
        Add("stat.duration",         "持续",                     "Dur");
        Add("stat.crit_rate",        "暴击率",                   "Crit Rate");
        Add("stat.crit_dmg",         "暴伤",                     "Crit DMG");
        Add("stat.pierce",           "穿透",                     "Pierce");
        Add("stat.count",            "数量",                     "Count");
        Add("stat.armor",            "护甲",                     "Armor");
        Add("stat.max_hp",           "生命上限",                 "Max HP");
        Add("stat.revival",          "复活次数",                 "Revive");
        Add("stat.dmg_bonus",        "伤害增幅",                 "DMG Bonus");
        Add("stat.cooldown_time",    "冷却时间",                 "Cooldown");
        Add("stat.aoe_range",        "攻击范围",                 "AoE Range");
        Add("stat.move_speed",       "移速",                     "Move Spd");
        Add("stat.duration_time",    "持续时间",                 "Duration");
        Add("stat.pickup",           "拾取范围",                 "Pickup");
        Add("stat.luck",             "幸运值",                   "Luck");

        // ===== 角色选择 =====
        Add("ui.character.inuse",    "使用中",                   "In Use");
        Add("ui.character.select",   "选择此角色",               "Select");
        Add("ui.character.unlock",   "解锁",                     "Unlock");
        Add("ui.gold",              "金币",                      "Gold");
    }

    /// <summary>
    /// 便捷添加方法，支持中文 + 英文双语
    /// </summary>
    private static void Add(string key, string zh, string en)
    {
        _table[key] = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.ChineseSimplified, zh },
            { SystemLanguage.English, en }
        };
    }
}
