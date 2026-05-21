using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 角色技能树UI — 手动布局模式
/// 节点位置、连线均通过 Inspector 手动配置
/// BuildTree() 只负责：读取子节点的 assignedNode 并初始化状态
/// </summary>
public class CharacterSkillTreeGridUI : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("当前显示的角色数据（由 CharacterSelectManager 设置）")]
    public CharacterData characterData;

    [Header("容器")]
    [Tooltip("技能树内容的父容器（包含手动摆放的节点）")]
    public RectTransform treeContainer;

    [Header("连接线设置")]
    [Tooltip("连接线颜色（已解锁路径）")]
    public Color connectorUnlockedColor = new Color(1f, 0.85f, 0.6f, 1f);
    [Tooltip("连接线颜色（锁定路径）")]
    public Color connectorLockedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    [Tooltip("连接线颜色（互斥锁定路径）")]
    public Color connectorExcludedColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);

    [Header("连接线样式")]
    [Tooltip("连接线Sprite（糖果棒样式）")]
    public Sprite connectorSprite;
    [Tooltip("连接线粗细")]
    public float connectorThickness = 8f;

    // 运行时缓存
    private List<CharacterSkillNodeUI> allNodeUIs = new List<CharacterSkillNodeUI>();
    private List<GameObject> generatedConnectors = new List<GameObject>();

    /// <summary>
    /// 公开接口：由 CharacterSelectManager 调用
    /// 读取容器下所有手动摆放的 CharacterSkillNodeUI，用它们的 assignedNode 初始化
    /// </summary>
    public void BuildTree(CharacterData data)
    {
        characterData = data;

        // 清理之前生成的连接线
        ClearConnectors();

        // 收集容器下所有 CharacterSkillNodeUI
        allNodeUIs.Clear();
        if (treeContainer != null)
        {
            treeContainer.GetComponentsInChildren<CharacterSkillNodeUI>(true, allNodeUIs);
        }

        if (data == null || allNodeUIs.Count == 0) return;

        // 初始化每个节点
        foreach (var nodeUI in allNodeUIs)
        {
            if (nodeUI == null) continue;

            // 使用 Inspector 中预拖入的 assignedNode
            CharacterSkillNode nodeData = nodeUI.assignedNode;
            if (nodeData != null)
            {
                nodeUI.Setup(nodeData, characterData, OnNodeClicked);
            }
        }

        // 根据 connectTo 列表生成连接线
        DrawConnections();
    }

    /// <summary>
    /// 节点点击回调 — 刷新所有节点和连接线
    /// </summary>
    private void OnNodeClicked(CharacterSkillNodeUI clickedNode)
    {
        RefreshAll();
    }

    /// <summary>
    /// 根据每个节点的 connectTo 列表生成连接线
    /// </summary>
    private void DrawConnections()
    {
        foreach (var nodeUI in allNodeUIs)
        {
            if (nodeUI == null || nodeUI.connectTo == null) continue;

            RectTransform fromRect = nodeUI.GetComponent<RectTransform>();
            if (fromRect == null) continue;

            foreach (var targetNode in nodeUI.connectTo)
            {
                if (targetNode == null) continue;
                RectTransform toRect = targetNode.GetComponent<RectTransform>();
                if (toRect == null) continue;

                CreateLineConnector(fromRect, toRect, nodeUI, targetNode);
            }
        }
    }

    /// <summary>
    /// 在两个节点之间生成一条连接线
    /// </summary>
    private void CreateLineConnector(RectTransform from, RectTransform to,
        CharacterSkillNodeUI fromNode, CharacterSkillNodeUI toNode)
    {
        GameObject lineGO = new GameObject($"Conn_{fromNode.name}_to_{toNode.name}",
            typeof(RectTransform), typeof(Image));
        lineGO.transform.SetParent(treeContainer, false);

        RectTransform rect = lineGO.GetComponent<RectTransform>();
        Image img = lineGO.GetComponent<Image>();

        // 计算起点和终点（相对于容器）
        Vector2 fromPos = from.anchoredPosition;
        Vector2 toPos = to.anchoredPosition;
        Vector2 midPoint = (fromPos + toPos) / 2f;
        float distance = Vector2.Distance(fromPos, toPos);
        float angle = Mathf.Atan2(toPos.y - fromPos.y, toPos.x - fromPos.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = midPoint;
        rect.sizeDelta = new Vector2(distance, connectorThickness);
        rect.localRotation = Quaternion.Euler(0, 0, angle);

        if (connectorSprite != null)
        {
            img.sprite = connectorSprite;
            img.type = Image.Type.Tiled;
        }
        img.color = connectorLockedColor;
        img.raycastTarget = false;

        // 确保连接线渲染在节点下方
        lineGO.transform.SetAsFirstSibling();

        // 存储节点引用以便刷新颜色
        var connData = lineGO.AddComponent<ConnectorData>();
        connData.fromNode = fromNode;
        connData.toNode = toNode;

        generatedConnectors.Add(lineGO);
    }

    /// <summary>
    /// 刷新所有节点状态和连接线颜色
    /// </summary>
    public void RefreshAll()
    {
        // 刷新节点
        foreach (var nodeUI in allNodeUIs)
        {
            if (nodeUI != null) nodeUI.RefreshState();
        }

        // 刷新连接线颜色
        foreach (var connGO in generatedConnectors)
        {
            if (connGO == null) continue;

            ConnectorData data = connGO.GetComponent<ConnectorData>();
            Image img = connGO.GetComponent<Image>();
            if (data == null || img == null) continue;

            bool fromUnlocked = data.fromNode != null && data.fromNode.nodeData != null
                && PlayerProgressManager.Instance != null
                && PlayerProgressManager.Instance.IsCharacterNodeUnlocked(data.fromNode.nodeData);

            bool toUnlocked = data.toNode != null && data.toNode.nodeData != null
                && PlayerProgressManager.Instance != null
                && PlayerProgressManager.Instance.IsCharacterNodeUnlocked(data.toNode.nodeData);

            bool toExcluded = data.toNode != null && data.toNode.nodeData != null
                && PlayerProgressManager.Instance != null
                && PlayerProgressManager.Instance.IsNodeExcluded(data.toNode.nodeData);

            if (toExcluded)
                img.color = connectorExcludedColor;
            else if (fromUnlocked && toUnlocked)
                img.color = connectorUnlockedColor;
            else
                img.color = connectorLockedColor;
        }
    }

    /// <summary>
    /// 清理动态生成的连接线（不清理手动摆放的节点！）
    /// </summary>
    private void ClearConnectors()
    {
        foreach (var obj in generatedConnectors)
        {
            if (obj != null) Destroy(obj);
        }
        generatedConnectors.Clear();
    }

    /// <summary>
    /// 完全清理（包括缓存引用，但不销毁手动节点）
    /// </summary>
    public void ClearTree()
    {
        ClearConnectors();
        allNodeUIs.Clear();
    }
}

/// <summary>
/// 辅助组件：存储连接线两端的节点引用（运行时使用）
/// </summary>
public class ConnectorData : MonoBehaviour
{
    [HideInInspector] public CharacterSkillNodeUI fromNode;
    [HideInInspector] public CharacterSkillNodeUI toNode;
}
