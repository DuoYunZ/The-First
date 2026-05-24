using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(SkillTreeNodeData))]
public class SkillTreeNodeDataEditor : Editor
{
    private Dictionary<UpgradeType, bool> foldouts = new Dictionary<UpgradeType, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "possibleOptions");

        SkillTreeNodeData node = (SkillTreeNodeData)target;       

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("升级效果配置 (自动生成)", EditorStyles.boldLabel);

        // --- 定制化UI ---
        var allStatTypesInNode = node.possibleOptions
                                     .SelectMany(opt => opt.effects)
                                     .Where(eff => eff.actionType == EffectActionType.ModifyStat)
                                     .Select(eff => eff.statToModify)
                                     .Distinct()
                                     .ToList();

        foreach (UpgradeType statType in allStatTypesInNode)
        {
            if (!foldouts.ContainsKey(statType))
            {
                foldouts[statType] = true;
            }

            foldouts[statType] = EditorGUILayout.Foldout(foldouts[statType], $"属性: {statType}", true);

            if (foldouts[statType])
            {
                EditorGUI.indentLevel++;

                // 【修正】将标签文本传递给方法
                DrawRarityFieldsForStat(node, statType, $"{statType} 增加", ModifierType.Flat);
                DrawRarityFieldsForStat(node, statType, $"{statType} 增加百分比", ModifierType.Percentage);

                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space(5);


        if (GUILayout.Button("添加新的升级属性"))
        {
            // 为简化，我们添加一个临时的、可配置的 UpgradeOption
            var newOption = new UpgradeOption
            {
                description = "新属性 - 请配置",
                rarity = Rarity.Common,
                effects = new List<UpgradeEffect> { new UpgradeEffect { statToModify = UpgradeType.WeaponDamage, modType = ModifierType.Percentage, value = 0.05f } }
            };
            node.possibleOptions.Add(newOption);
        }

        EditorGUILayout.Space(10);

        // 绘制原始数据，用于处理解锁武器等特殊情况
        EditorGUILayout.LabelField("升级选项池 (所有效果)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("possibleOptions"), true);

        serializedObject.ApplyModifiedProperties();
    }

    // 【修正】方法签名增加了 string label
    private void DrawRarityFieldsForStat(SkillTreeNodeData node, UpgradeType statType, string label, ModifierType modType)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        DrawSingleRarityField(node, statType, Rarity.Common, modType, label);
        DrawSingleRarityField(node, statType, Rarity.Uncommon, modType, label);
        DrawSingleRarityField(node, statType, Rarity.Rare, modType, label);
        DrawSingleRarityField(node, statType, Rarity.Epic, modType, label);

        EditorGUILayout.EndHorizontal();
    }

    // 【修正】方法签名增加了 string label
    private void DrawSingleRarityField(SkillTreeNodeData node, UpgradeType statType, Rarity rarity, ModifierType modType, string label)
    {
        var option = node.possibleOptions.FirstOrDefault(o => o.rarity == rarity && o.effects.Count == 1 && o.effects.Any(e => e.statToModify == statType && e.modType == modType));
        UpgradeEffect effect = null;

        if (option != null)
        {
            effect = option.effects.First();
        }

        float oldValue = (effect != null) ? effect.value : 0;

        string rarityInitial = rarity.ToString().Substring(0, 1);
        float newValue = EditorGUILayout.FloatField(rarityInitial, oldValue);

        if (newValue != oldValue)
        {
            Undo.RecordObject(node, $"Change {rarity} {statType} Value");

            if (option == null)
            {
                option = new UpgradeOption
                {
                    rarity = rarity,
                    effects = new List<UpgradeEffect>()
                };

                // 【修正】创建 Effect 时不再包含 rarity
                effect = new UpgradeEffect { statToModify = statType, modType = modType };

                option.effects.Add(effect);
                node.possibleOptions.Add(option);
            }

            effect.value = newValue;

            // 【修正】使用传入的 label 自动生成描述
            string prefix = newValue > 0 ? "+" : "";
            string suffix = modType == ModifierType.Percentage ? "%" : "";
            option.description = $"{label} {prefix}{newValue * (modType == ModifierType.Percentage ? 100 : 1)}{suffix}";

            EditorUtility.SetDirty(node);
        }
    }
}