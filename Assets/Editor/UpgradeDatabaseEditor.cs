using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(UpgradeDatabase))]
public class UpgradeDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        UpgradeDatabase database = (UpgradeDatabase)target;

        GUILayout.Space(10);
        GUILayout.Label("自动填充工具", EditorStyles.boldLabel);

        // 按钮 1：寻找通用被动技能 (原来的 SkillTreeNodeData)
        if (GUILayout.Button("Find Passive Upgrades (SkillNodes)"))
        {
            FindPassives(database);
        }

        // 按钮 2：寻找武器升级链 (新的 WeaponUpgradeChainSO)
        if (GUILayout.Button("Find Weapon Chains"))
        {
            FindWeaponChains(database);
        }
    }

    private void FindPassives(UpgradeDatabase database)
    {
        // 查找所有 SkillTreeNodeData
        string[] guids = AssetDatabase.FindAssets("t:SkillTreeNodeData");

        // 初始化列表防止空引用
        if (database.passiveUpgrades == null) database.passiveUpgrades = new List<SkillTreeNodeData>();
        database.passiveUpgrades.Clear();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillTreeNodeData node = AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(path);

            // 【过滤逻辑】只添加没有关联武器的节点（即通用被动）
            // 如果你希望所有旧节点都暂存在这里，可以去掉这个 if
            if (node != null && node.associatedWeapon == null)
            {
                database.passiveUpgrades.Add(node);
            }
        }

        EditorUtility.SetDirty(database);
        Debug.Log($"找到并添加了 {database.passiveUpgrades.Count} 个通用被动技能！");
    }

    private void FindWeaponChains(UpgradeDatabase database)
    {
        // 查找所有 WeaponUpgradeChainSO
        string[] guids = AssetDatabase.FindAssets("t:WeaponUpgradeChainSO");

        if (database.weaponChains == null) database.weaponChains = new List<WeaponUpgradeChainSO>();
        database.weaponChains.Clear();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponUpgradeChainSO chain = AssetDatabase.LoadAssetAtPath<WeaponUpgradeChainSO>(path);
            if (chain != null)
            {
                database.weaponChains.Add(chain);
            }
        }

        EditorUtility.SetDirty(database);
        Debug.Log($"找到并添加了 {database.weaponChains.Count} 个武器升级链！");
    }
}