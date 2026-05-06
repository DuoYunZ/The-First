using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AutoLayoutMageSkillTree : EditorWindow
{
    [MenuItem("Tools/Auto Layout Mage Skill Tree")]
    public static void AutoLayout()
    {
        // 查找 MageSkillNodeContainer
        GameObject container = GameObject.Find("MageSkillNodeContainer");
        if (container == null)
        {
            Debug.LogError("No MageSkillNodeContainer found in current scene!");
            return;
        }

        Dictionary<string, CharacterSkillNodeUI> nodes = new Dictionary<string, CharacterSkillNodeUI>();
        CharacterSkillNodeUI[] uis = container.GetComponentsInChildren<CharacterSkillNodeUI>(true);
        foreach (var ui in uis)
        {
            nodes[ui.gameObject.name] = ui;
            ui.connectTo = new List<CharacterSkillNodeUI>(); // 清空旧连接
        }

        // 定义坐标和连接
        SetNode(nodes, "L1_魔力I", -450, 0, "L1_生命I");
        SetNode(nodes, "L1_生命I", -300, 0, "L1_速度I");
        SetNode(nodes, "L1_速度I", -150, 0, "L2_🔥火球之路", "L2_❄冰锥之路");

        // 火球分支 (Fire) - 向上
        SetNode(nodes, "L2_🔥火球之路", 0, 150, "L3_🔥燃烧大地");
        SetNode(nodes, "L3_🔥燃烧大地", 150, 150, "L3_🔥烈焰轨迹", "L3_🌪风助火势");
        SetNode(nodes, "L3_🔥烈焰轨迹", 300, 150); // 末端分支
        SetNode(nodes, "L3_🌪风助火势", 150, 300, "L4_⭐炼狱之焰"); // 走上面一层
        SetNode(nodes, "L4_⭐炼狱之焰", 300, 300); // 终点

        // 冰锥分支 (Ice) - 向下
        SetNode(nodes, "L2_❄冰锥之路", 0, -150, "L3_❄冰锥连射");
        SetNode(nodes, "L3_❄冰锥连射", 150, -150, "L3_🌨冰雹风暴", "L3_⚡毁灭雷击");
        SetNode(nodes, "L3_🌨冰雹风暴", 300, -150); // 末端分支
        SetNode(nodes, "L3_⚡毁灭雷击", 150, -300, "L4_⭐永冻领域"); // 走下面一层
        SetNode(nodes, "L4_⭐永冻领域", 300, -300); // 终点

        EditorUtility.SetDirty(container);
        Debug.Log("Mage UI layout created!");
    }

    private static void SetNode(Dictionary<string, CharacterSkillNodeUI> dict, string name, float x, float y, params string[] connectToParams)
    {
        if (dict.TryGetValue(name, out var ui))
        {
            RectTransform rect = ui.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(x, y);
            }

            foreach (var target in connectToParams)
            {
                if (dict.TryGetValue(target, out var targetUI))
                {
                    ui.connectTo.Add(targetUI);
                }
            }
        }
        else
        {
            Debug.LogWarning("Node not found: " + name);
        }
    }
}
