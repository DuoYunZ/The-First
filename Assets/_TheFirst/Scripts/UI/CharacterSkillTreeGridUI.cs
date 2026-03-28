using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 网格式角色技能树UI — 自动按层级生成节点 + 糖果连接线
/// 挂载在技能树面板根对象上
/// </summary>
public class CharacterSkillTreeGridUI : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("当前显示的角色数据（由 CharacterSelectManager 设置）")]
    public CharacterData characterData;

    [Header("容器")]
    [Tooltip("技能树内容的父容器")]
    public RectTransform treeContainer;

    [Header("预制件")]
    [Tooltip("技能节点预制件（需挂载 CharacterSkillNodeUI 组件）")]
    public GameObject skillNodePrefab;

    [Header("连接线设置")]
    [Tooltip("水平连接线的Sprite（糖果棒样式）")]
    public Sprite horizontalConnectorSprite;
    [Tooltip("垂直连接线的Sprite（可与水平线相同，会旋转90°）")]
    public Sprite verticalConnectorSprite;
    [Tooltip("连接线颜色（已解锁路径）")]
    public Color connectorUnlockedColor = new Color(1f, 0.85f, 0.6f, 1f);
    [Tooltip("连接线颜色（锁定路径）")]
    public Color connectorLockedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    [Header("布局参数")]
    [Tooltip("节点图标大小")]
    public float nodeSize = 80f;
    [Tooltip("水平连接线长度")]
    public float horizontalGap = 60f;
    [Tooltip("垂直连接线长度")]
    public float verticalGap = 50f;
    [Tooltip("连接线粗细")]
    public float connectorThickness = 16f;

    // 运行时数据
    private List<GameObject> generatedObjects = new List<GameObject>();
    private List<Image> connectorImages = new List<Image>();
    // 按层级分组的节点UI引用
    private Dictionary<int, List<CharacterSkillNodeUI>> nodesByLayer = new Dictionary<int, List<CharacterSkillNodeUI>>();

    /// <summary>
    /// 公开接口：由 CharacterSelectManager 调用生成技能树
    /// </summary>
    public void BuildTree(CharacterData data)
    {
        characterData = data;
        ClearTree();

        if (data == null || data.characterSkillNodes == null || data.characterSkillNodes.Count == 0)
            return;

        // 按层级分组
        Dictionary<int, List<CharacterSkillNode>> layerGroups = new Dictionary<int, List<CharacterSkillNode>>();
        foreach (var node in data.characterSkillNodes)
        {
            if (node == null) continue;
            if (!layerGroups.ContainsKey(node.layer))
                layerGroups[node.layer] = new List<CharacterSkillNode>();
            layerGroups[node.layer].Add(node);
        }

        // 获取最大层级
        int maxLayer = 0;
        foreach (var key in layerGroups.Keys)
            if (key > maxLayer) maxLayer = key;

        // 计算布局
        // 每一层的节点水平居中排列，层与层之间用垂直连接线连接
        float totalHeight = 0;

        for (int layerIdx = 1; layerIdx <= maxLayer; layerIdx++)
        {
            if (!layerGroups.ContainsKey(layerIdx)) continue;
            var nodesInLayer = layerGroups[layerIdx];
            int nodeCount = nodesInLayer.Count;

            // 计算本层宽度
            float layerWidth = nodeCount * nodeSize + (nodeCount - 1) * horizontalGap;
            float startX = -layerWidth / 2f + nodeSize / 2f;

            // 生成节点
            List<CharacterSkillNodeUI> layerNodeUIs = new List<CharacterSkillNodeUI>();
            for (int i = 0; i < nodeCount; i++)
            {
                float x = startX + i * (nodeSize + horizontalGap);
                float y = -totalHeight;

                // 创建节点
                GameObject nodeGO = Instantiate(skillNodePrefab, treeContainer);
                RectTransform nodeRect = nodeGO.GetComponent<RectTransform>();
                nodeRect.anchoredPosition = new Vector2(x, y);
                nodeRect.sizeDelta = new Vector2(nodeSize, nodeSize);

                CharacterSkillNodeUI nodeUI = nodeGO.GetComponent<CharacterSkillNodeUI>();
                if (nodeUI != null)
                {
                    nodeUI.Setup(nodesInLayer[i], data, (self) => RefreshAll());
                    layerNodeUIs.Add(nodeUI);
                }

                generatedObjects.Add(nodeGO);

                // 水平连接线（节点之间，最后一个节点后不画）
                if (i < nodeCount - 1)
                {
                    float connX = x + nodeSize / 2f + horizontalGap / 2f;
                    CreateConnector(connX, y, horizontalGap, connectorThickness, false, layerIdx);
                }
            }

            nodesByLayer[layerIdx] = layerNodeUIs;

            // 垂直连接线（本层节点到下一层，如果下一层存在）
            if (layerIdx < maxLayer && layerGroups.ContainsKey(layerIdx + 1))
            {
                var nextLayerNodes = layerGroups[layerIdx + 1];
                int nextCount = nextLayerNodes.Count;
                float nextLayerWidth = nextCount * nodeSize + (nextCount - 1) * horizontalGap;

                // 连接策略：
                // 如果下一层节点数 == 1（天赋层），从当前层中间节点连一条竖线
                // 否则每个当前层节点往下连一条竖线
                if (nextCount == 1)
                {
                    // 天赋层：从当前层中间位置画一条竖线
                    float connY = -(totalHeight + nodeSize / 2f + verticalGap / 2f);
                    CreateConnector(0, connY, verticalGap, connectorThickness, true, layerIdx);
                }
                else
                {
                    // 普通层：每个节点画一条竖线（对齐上下层共有位置）
                    // 取两层节点数的较小值
                    int connCount = Mathf.Min(nodeCount, nextCount);
                    float nextStartX = -nextLayerWidth / 2f + nodeSize / 2f;

                    for (int i = 0; i < connCount; i++)
                    {
                        float x = startX + i * (nodeSize + horizontalGap);
                        float connY = -(totalHeight + nodeSize / 2f + verticalGap / 2f);
                        CreateConnector(x, connY, verticalGap, connectorThickness, true, layerIdx);
                    }
                }

                totalHeight += nodeSize + verticalGap;
            }
            else
            {
                totalHeight += nodeSize + verticalGap;
            }
        }

        // 设置容器大小
        float maxLayerWidth = 0;
        foreach (var kvp in layerGroups)
        {
            float w = kvp.Value.Count * nodeSize + (kvp.Value.Count - 1) * horizontalGap;
            if (w > maxLayerWidth) maxLayerWidth = w;
        }
        treeContainer.sizeDelta = new Vector2(maxLayerWidth + 40f, totalHeight + 40f);

        // 刷新连接线颜色
        RefreshConnectorColors();
    }

    /// <summary>
    /// 创建连接线
    /// </summary>
    private void CreateConnector(float x, float y, float length, float thickness, bool isVertical, int fromLayer)
    {
        GameObject connGO = new GameObject("Connector", typeof(RectTransform), typeof(Image));
        connGO.transform.SetParent(treeContainer, false);

        RectTransform rect = connGO.GetComponent<RectTransform>();
        Image img = connGO.GetComponent<Image>();

        if (isVertical)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(thickness, length);
            img.sprite = verticalConnectorSprite != null ? verticalConnectorSprite : horizontalConnectorSprite;
            // 如果用水平素材做垂直线，旋转90度
            if (verticalConnectorSprite == null && horizontalConnectorSprite != null)
                rect.localRotation = Quaternion.Euler(0, 0, 90f);
        }
        else
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(length, thickness);
            img.sprite = horizontalConnectorSprite;
        }

        // 没有Sprite则用纯色
        if (img.sprite == null)
        {
            img.color = connectorLockedColor;
        }

        img.type = Image.Type.Tiled; // 使用平铺模式让糖果花纹重复
        img.raycastTarget = false;

        // 确保连接线渲染在节点下方
        connGO.transform.SetAsFirstSibling();

        // 用 fromLayer 标记，用于后续刷新颜色
        connGO.name = $"Connector_L{fromLayer}_{(isVertical ? "V" : "H")}";

        connectorImages.Add(img);
        generatedObjects.Add(connGO);
    }

    /// <summary>
    /// 刷新所有连接线颜色
    /// </summary>
    private void RefreshConnectorColors()
    {
        if (PlayerProgressManager.Instance == null || characterData == null) return;

        foreach (var img in connectorImages)
        {
            if (img == null) continue;
            // 从名称中解析层级
            string name = img.gameObject.name;
            int layerNum = 1;
            if (name.Contains("_L"))
            {
                string layerStr = name.Split('_')[1].Replace("L", "");
                int.TryParse(layerStr, out layerNum);
            }

            // 如果该层有至少2个已解锁节点，连接线变亮
            int unlocked = PlayerProgressManager.Instance.GetUnlockedCountInLayer(characterData, layerNum);
            img.color = unlocked >= 2 ? connectorUnlockedColor : connectorLockedColor;
        }
    }

    /// <summary>
    /// 刷新所有节点和连接线
    /// </summary>
    private void RefreshAll()
    {
        foreach (var kvp in nodesByLayer)
        {
            foreach (var nodeUI in kvp.Value)
            {
                if (nodeUI != null) nodeUI.RefreshState();
            }
        }
        RefreshConnectorColors();
    }

    /// <summary>
    /// 清理
    /// </summary>
    public void ClearTree()
    {
        foreach (var obj in generatedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        generatedObjects.Clear();
        connectorImages.Clear();
        nodesByLayer.Clear();
    }
}
