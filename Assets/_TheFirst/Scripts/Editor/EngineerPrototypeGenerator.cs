using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EngineerPrototypeGenerator
{
    private const string RolePath = "Assets/_TheFirst/GameData/Character/Role03_Data.asset";
    private const string SkillTreeDir = "Assets/_TheFirst/GameData/CharacterSkillTree/Engineer";
    private const string SkillCardDir = "Assets/_TheFirst/GameData/CharacterSkillCards/Engineer";

    private const string Role01Path = "Assets/_TheFirst/GameData/Character/Role01_Data.asset";
    private const string LandminePath = "Assets/_TheFirst/GameData/SO_Weapon/SO_Landmine.asset";
    private const string OrbitPath = "Assets/_TheFirst/GameData/SO_Weapon/SO_Orbit.asset";
    private const string LaserCorePath = "Assets/_TheFirst/GameData/SO_Weapon/SO_Laser_Tank.asset";
    private const string FlameTurretPath = "Assets/_TheFirst/GameData/SO_Weapon/discard/SO_FlameTurret.asset";
    private const string SuperMechPath = "Assets/_TheFirst/GameData/SO_Weapon/SO_SuperMech.asset";

    [MenuItem("Tools/TheFirst/Generate Engineer Prototype")]
    public static void Generate()
    {
        EnsureFolder(SkillTreeDir);
        EnsureFolder(SkillCardDir);

        CharacterData role01 = AssetDatabase.LoadAssetAtPath<CharacterData>(Role01Path);
        WeaponStatBlock landmine = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(LandminePath);
        WeaponStatBlock orbit = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(OrbitPath);
        WeaponStatBlock laserCore = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(LaserCorePath);
        WeaponStatBlock flameTurret = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(FlameTurretPath);
        WeaponStatBlock superMech = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(SuperMechPath);

        var cards = CreateEngineerCards(landmine, orbit, laserCore, flameTurret, superMech);
        var nodes = CreateEngineerNodes(landmine, laserCore, flameTurret, cards);
        CreateOrUpdateEngineerRole(role01, landmine, nodes);
        AppendEngineerToLoadedDataManagers();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[EngineerPrototype] Generated engineer role, {nodes.Count} skill nodes, and {cards.Count} role cards.</color>");
    }

    private static Dictionary<string, SkillTreeNodeData> CreateEngineerCards(
        WeaponStatBlock landmine,
        WeaponStatBlock orbit,
        WeaponStatBlock laserCore,
        WeaponStatBlock flameTurret,
        WeaponStatBlock superMech)
    {
        var cards = new Dictionary<string, SkillTreeNodeData>();

        cards["EngineerFortress"] = CreateCard(
            "EngineerCard_FortressMode",
            "堡垒工程",
            "获得喷火塔。机械部署物更偏向阵地压制。",
            "Gain Flame Turret. Mechanical deployables lean into area control.",
            Rarity.Rare,
            "EngineerFortress",
            flameTurret,
            flameTurret != null ? flameTurret.weaponIcon : landmine != null ? landmine.weaponIcon : null);

        cards["EngineerOverclock"] = CreateCard(
            "EngineerCard_OverclockMode",
            "超频工程",
            "获得镭射核心。机械武器更偏向高频输出。",
            "Gain Laser Core. Mechanical weapons lean into high-frequency output.",
            Rarity.Rare,
            "EngineerOverclock",
            laserCore,
            laserCore != null ? laserCore.weaponIcon : landmine != null ? landmine.weaponIcon : null);

        cards["Engineer_Fortress_Minefield"] = CreateCard(
            "EngineerCard_Minefield",
            "连锁雷区",
            "地雷额外部署1颗，爆炸范围提升，并短暂牵引和眩晕敌人。",
            "Landmines deploy one extra mine, gain radius, and briefly pull and stun enemies.",
            Rarity.Uncommon,
            "Engineer_Fortress_Minefield",
            null,
            landmine != null ? landmine.weaponIcon : null);

        cards["Engineer_Fortress_AutoTurret"] = CreateCard(
            "EngineerCard_AutoTurret",
            "副塔协议",
            "喷火塔部署时额外生成一座短持续副塔。",
            "Flame Turret also deploys a shorter-lived support turret.",
            Rarity.Uncommon,
            "Engineer_Fortress_AutoTurret",
            flameTurret,
            flameTurret != null ? flameTurret.weaponIcon : null);

        cards["Engineer_Overclock_LaserGrid"] = CreateCard(
            "EngineerCard_LaserGrid",
            "棱镜阵列",
            "镭射核心额外生成1个核心，并获得折射与更短过热冷却。",
            "Laser Core gains one extra core, refraction, and shorter overheat cooldown.",
            Rarity.Rare,
            "Engineer_Overclock_LaserGrid",
            laserCore,
            laserCore != null ? laserCore.weaponIcon : null);

        cards["Engineer_Overclock_RotorArray"] = CreateCard(
            "EngineerCard_RotorArray",
            "转子集群",
            "获得环绕装置。机械环绕和核心类武器额外增加1个单位。",
            "Gain Orbit. Mechanical orbital and core weapons gain one extra unit.",
            Rarity.Uncommon,
            "Engineer_Overclock_RotorArray",
            orbit,
            orbit != null ? orbit.weaponIcon : null);

        cards["Engineer_Talent_SuperMech"] = CreateCard(
            "EngineerCard_SuperMech",
            "巨型机器人协议",
            "需要地雷、环绕装置、镭射核心。直接召唤巨型机器人。",
            "Requires Landmine, Orbit, and Laser Core. Summon the giant robot directly.",
            Rarity.Epic,
            "Engineer_Talent_SuperMech",
            superMech,
            superMech != null ? superMech.weaponIcon : laserCore != null ? laserCore.weaponIcon : null,
            landmine,
            orbit,
            laserCore);

        cards["Engineer_Talent_AssemblyLine"] = CreateCard(
            "EngineerCard_AssemblyLine",
            "自动化产线",
            "机械武器冷却进一步缩短，部署节奏明显加快。",
            "Mechanical weapon cooldowns are further reduced for faster deployment tempo.",
            Rarity.Epic,
            "Engineer_Talent_AssemblyLine",
            null,
            flameTurret != null ? flameTurret.weaponIcon : landmine != null ? landmine.weaponIcon : null);

        return cards;
    }

    private static List<CharacterSkillNode> CreateEngineerNodes(
        WeaponStatBlock landmine,
        WeaponStatBlock laserCore,
        WeaponStatBlock flameTurret,
        Dictionary<string, SkillTreeNodeData> cards)
    {
        Sprite landmineIcon = landmine != null ? landmine.weaponIcon : null;
        Sprite laserIcon = laserCore != null ? laserCore.weaponIcon : landmineIcon;
        Sprite turretIcon = flameTurret != null ? flameTurret.weaponIcon : landmineIcon;

        CharacterSkillNode baseAtk = CreateNode(
            "Engineer_Base_ATK",
            "结构弱点分析",
            "全局伤害 +8%",
            "Structural Analysis",
            "Global damage +8%",
            landmineIcon,
            1,
            50,
            null,
            false,
            "",
            null,
            Effect(PermanentUpgradeType.DamagePercent, 0.08f));

        CharacterSkillNode baseCdr = CreateNode(
            "Engineer_Base_CDR",
            "快速装配",
            "全局冷却缩减 +6%",
            "Rapid Assembly",
            "Global cooldown reduction +6%",
            turretIcon,
            1,
            50,
            null,
            false,
            "",
            null,
            Effect(PermanentUpgradeType.CooldownReductionPercent, 0.06f));

        CharacterSkillNode baseArmor = CreateNode(
            "Engineer_Base_ARM",
            "加固底盘",
            "护甲 +1，生命上限 +15",
            "Reinforced Chassis",
            "Armor +1, max health +15",
            laserIcon,
            1,
            50,
            null,
            false,
            "",
            null,
            Effect(PermanentUpgradeType.ArmorFlat, 1f),
            Effect(PermanentUpgradeType.MaxHealthFlat, 15f));

        CharacterSkillNode fortress = CreateNode(
            "Engineer_Branch_Fortress",
            "堡垒工程",
            "局内可抽到喷火塔，阵地压制能力提升。护甲 +2",
            "Fortress Engineering",
            "Flame Turret can appear in-run. Armor +2",
            turretIcon,
            2,
            200,
            cards["EngineerFortress"],
            true,
            "EngineerFortress",
            new[] { baseAtk },
            Effect(PermanentUpgradeType.ArmorFlat, 2f));

        CharacterSkillNode overclock = CreateNode(
            "Engineer_Branch_Overclock",
            "超频工程",
            "局内可抽到镭射核心，机械输出频率提升。冷却缩减 +8%",
            "Overclock Engineering",
            "Laser Core can appear in-run. Cooldown reduction +8%",
            laserIcon,
            2,
            200,
            cards["EngineerOverclock"],
            true,
            "EngineerOverclock",
            new[] { baseAtk },
            Effect(PermanentUpgradeType.CooldownReductionPercent, 0.08f));

        fortress.mutuallyExclusiveNodes = new List<CharacterSkillNode> { overclock };
        overclock.mutuallyExclusiveNodes = new List<CharacterSkillNode> { fortress };
        EditorUtility.SetDirty(fortress);
        EditorUtility.SetDirty(overclock);

        CharacterSkillNode minefield = CreateNode(
            "Engineer_Fortress_Minefield",
            "连锁雷区",
            "地雷额外部署1颗，并获得牵引与眩晕表现。",
            "Chain Minefield",
            "Landmines deploy one extra mine and gain pull/stun behavior.",
            landmineIcon,
            3,
            150,
            cards["Engineer_Fortress_Minefield"],
            false,
            "",
            new[] { fortress },
            Effect(PermanentUpgradeType.DamagePercent, 0.05f));

        CharacterSkillNode autoTurret = CreateNode(
            "Engineer_Fortress_AutoTurret",
            "副塔协议",
            "喷火塔部署时追加一座短持续副塔。",
            "Support Turret Protocol",
            "Flame Turret deploys a short-lived support turret.",
            turretIcon,
            3,
            150,
            cards["Engineer_Fortress_AutoTurret"],
            false,
            "",
            new[] { fortress },
            Effect(PermanentUpgradeType.CooldownReductionPercent, 0.05f));

        CharacterSkillNode plating = CreateNode(
            "Engineer_Fortress_Plating",
            "备用装甲板",
            "护甲 +3，生命上限 +25",
            "Spare Plating",
            "Armor +3, max health +25",
            turretIcon,
            3,
            150,
            null,
            false,
            "",
            new[] { fortress },
            Effect(PermanentUpgradeType.ArmorFlat, 3f),
            Effect(PermanentUpgradeType.MaxHealthFlat, 25f));

        CharacterSkillNode laserGrid = CreateNode(
            "Engineer_Overclock_LaserGrid",
            "棱镜阵列",
            "镭射核心额外生成1个核心，并获得折射。",
            "Prism Grid",
            "Laser Core gains one extra core and refraction.",
            laserIcon,
            3,
            150,
            cards["Engineer_Overclock_LaserGrid"],
            false,
            "",
            new[] { overclock },
            Effect(PermanentUpgradeType.DamagePercent, 0.05f));

        CharacterSkillNode rotorArray = CreateNode(
            "Engineer_Overclock_RotorArray",
            "转子集群",
            "获得环绕装置，机械单位数量进一步提升。",
            "Rotor Array",
            "Gain Orbit and increase mechanical unit count.",
            laserIcon,
            3,
            150,
            cards["Engineer_Overclock_RotorArray"],
            false,
            "",
            new[] { overclock },
            Effect(PermanentUpgradeType.MoveSpeedPercent, 0.06f));

        CharacterSkillNode capacitor = CreateNode(
            "Engineer_Overclock_Capacitor",
            "高压电容",
            "伤害 +6%，冷却缩减 +4%",
            "High Voltage Capacitor",
            "Damage +6%, cooldown reduction +4%",
            laserIcon,
            3,
            150,
            null,
            false,
            "",
            new[] { overclock },
            Effect(PermanentUpgradeType.DamagePercent, 0.06f),
            Effect(PermanentUpgradeType.CooldownReductionPercent, 0.04f));

        CharacterSkillNode superMech = CreateNode(
            "Engineer_Talent_SuperMech",
            "巨型机器人协议",
            "局内可通过地雷、环绕装置、镭射核心组合召唤巨型机器人。",
            "Giant Robot Protocol",
            "Combine Landmine, Orbit, and Laser Core in-run to summon the giant robot.",
            laserIcon,
            4,
            500,
            cards["Engineer_Talent_SuperMech"],
            false,
            "",
            new[] { minefield, autoTurret, plating },
            Effect(PermanentUpgradeType.DamagePercent, 0.1f));

        CharacterSkillNode assemblyLine = CreateNode(
            "Engineer_Talent_AssemblyLine",
            "自动化产线",
            "局内机械武器部署节奏大幅加快。",
            "Automated Assembly Line",
            "Mechanical weapon deployment tempo increases sharply in-run.",
            turretIcon,
            4,
            500,
            cards["Engineer_Talent_AssemblyLine"],
            false,
            "",
            new[] { laserGrid, rotorArray, capacitor },
            Effect(PermanentUpgradeType.CooldownReductionPercent, 0.1f));

        return new List<CharacterSkillNode>
        {
            baseAtk,
            baseCdr,
            baseArmor,
            fortress,
            overclock,
            minefield,
            autoTurret,
            plating,
            laserGrid,
            rotorArray,
            capacitor,
            superMech,
            assemblyLine
        };
    }

    private static void CreateOrUpdateEngineerRole(CharacterData role01, WeaponStatBlock landmine, List<CharacterSkillNode> nodes)
    {
        CharacterData role = LoadOrCreate<CharacterData>(RolePath);
        role.characterName = "南瓜工程师";
        role.description = "机械流派角色。以地雷、喷火塔、镭射核心和巨型机器人构建阵地型 Build。";
        role.characterNameEN = "Pumpkin Engineer";
        role.descriptionEN = "Mechanical build character using landmines, flame turrets, laser cores, and giant robot setups.";
        role.characterIcon = landmine != null && landmine.weaponIcon != null ? landmine.weaponIcon : role01 != null ? role01.characterIcon : null;
        role.characterPreviewPrefab = role01 != null ? role01.characterPreviewPrefab : null;
        role.chassisPrefab = role01 != null ? role01.chassisPrefab : null;
        role.initialWeapons = landmine != null ? new List<WeaponStatBlock> { landmine } : new List<WeaponStatBlock>();
        role.alternateStartWeapon = null;
        role.alternateStartMechanicID = "";
        role.autoUnlockInitialUltimate = false;
        role.characterID = "Role03";
        role.isDefaultUnlocked = false;
        role.unlockCost = 300;
        role.characterSkillNodes = nodes;
        EditorUtility.SetDirty(role);
    }

    private static CharacterSkillNode CreateNode(
        string fileName,
        string nodeName,
        string description,
        string nodeNameEN,
        string descriptionEN,
        Sprite icon,
        int layer,
        int cost,
        SkillTreeNodeData linkedCard,
        bool isMechanicBranch,
        string mechanicID,
        IReadOnlyList<CharacterSkillNode> prerequisites,
        params PermanentUpgradeEffect[] effects)
    {
        CharacterSkillNode node = LoadOrCreate<CharacterSkillNode>($"{SkillTreeDir}/{fileName}.asset");
        node.nodeName = nodeName;
        node.description = description;
        node.nodeNameEN = nodeNameEN;
        node.descriptionEN = descriptionEN;
        node.icon = icon;
        node.layer = layer;
        node.cost = cost;
        node.prerequisites = prerequisites != null ? new List<CharacterSkillNode>(prerequisites) : new List<CharacterSkillNode>();
        if (node.mutuallyExclusiveNodes == null) node.mutuallyExclusiveNodes = new List<CharacterSkillNode>();
        node.isMechanicBranch = isMechanicBranch;
        node.mechanicID = mechanicID;
        node.effects = new List<PermanentUpgradeEffect>(effects);
        node.linkedUpgradeNode = linkedCard;
        EditorUtility.SetDirty(node);
        return node;
    }

    private static SkillTreeNodeData CreateCard(
        string fileName,
        string skillName,
        string description,
        string descriptionEN,
        Rarity rarity,
        string skillIdentifier,
        WeaponStatBlock weaponToUnlock,
        Sprite icon,
        params WeaponStatBlock[] requiredWeapons)
    {
        SkillTreeNodeData card = LoadOrCreate<SkillTreeNodeData>($"{SkillCardDir}/{fileName}.asset");
        card.skillName = skillName;
        card.skillIcon = icon;
        card.associatedWeapon = null;
        card.prerequisites = new List<SkillTreeNodeData>();
        card.requiredWeapons = new List<WeaponStatBlock>();
        if (requiredWeapons != null)
        {
            foreach (WeaponStatBlock required in requiredWeapons)
            {
                if (required != null) card.requiredWeapons.Add(required);
            }
        }
        card.mutuallyExclusive = new List<SkillTreeNodeData>();
        card.maxLevel = 1;
        card.isWeaponSkillTreeNode = false;
        card.isOneTimeOnly = true;

        UpgradeOption option = new UpgradeOption
        {
            description = description,
            descriptionEN = descriptionEN,
            rarity = rarity,
            effects = new List<UpgradeEffect>
            {
                new UpgradeEffect
                {
                    actionType = EffectActionType.ActivateCharSkill,
                    skillIdentifier = skillIdentifier
                }
            }
        };

        if (weaponToUnlock != null)
        {
            option.effects.Add(new UpgradeEffect
            {
                actionType = EffectActionType.UnlockWeapon,
                weaponToUnlock = weaponToUnlock
            });
        }

        card.possibleOptions = new List<UpgradeOption> { option };
        EditorUtility.SetDirty(card);
        return card;
    }

    private static PermanentUpgradeEffect Effect(PermanentUpgradeType type, float value)
    {
        return new PermanentUpgradeEffect { upgradeType = type, value = value };
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/"));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "Assets" || AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void AppendEngineerToLoadedDataManagers()
    {
        CharacterData role01 = AssetDatabase.LoadAssetAtPath<CharacterData>(Role01Path);
        CharacterData role02 = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_TheFirst/GameData/Character/Role02_Data.asset");
        CharacterData role03 = AssetDatabase.LoadAssetAtPath<CharacterData>(RolePath);

        foreach (DataManager dataManager in Resources.FindObjectsOfTypeAll<DataManager>())
        {
            if (EditorUtility.IsPersistent(dataManager)) continue;
            if (dataManager.allCharacters == null) dataManager.allCharacters = new List<CharacterData>();

            bool changed = false;
            changed |= AppendCharacterIfMissing(dataManager.allCharacters, role01);
            changed |= AppendCharacterIfMissing(dataManager.allCharacters, role02);
            changed |= AppendCharacterIfMissing(dataManager.allCharacters, role03);

            if (changed)
            {
                EditorUtility.SetDirty(dataManager);
                if (dataManager.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(dataManager.gameObject.scene);
                }
            }
        }
    }

    private static bool AppendCharacterIfMissing(List<CharacterData> list, CharacterData character)
    {
        if (character == null) return false;
        if (list.Contains(character)) return false;
        list.Add(character);
        return true;
    }
}
