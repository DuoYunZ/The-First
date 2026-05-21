using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 角色技能树布局映射：将角色数据关联到对应的技能树UI容器
/// </summary>
[System.Serializable]
public class CharacterSkillTreeLayout
{
    [Tooltip("角色数据 SO")]
    public CharacterData character;
    [Tooltip("该角色技能树节点的父容器")]
    public Transform nodeContainer;
    [Tooltip("该角色连接线的父容器（可选）")]
    public Transform connectorContainer;
}

/// <summary>
/// 角色信息面板UI管理器 — 手动布局模式
/// 节点由你在 Unity 中手动摆放，脚本负责数据绑定 + 自动画线 + 弹窗
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    [Header("面板根对象")]
    [Tooltip("角色信息面板的根 GameObject")]
    public GameObject panelRoot;

    [Header("角色信息 UI")]
    public Image characterIconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("3D 预览")]
    public Transform characterPreviewContainer;

    [Header("按钮")]
    [Tooltip("选择此角色按钮")]
    public Button selectButton;
    public TextMeshProUGUI selectButtonText;
    [Tooltip("解锁按钮")]
    public Button unlockButton;
    public TextMeshProUGUI unlockButtonText;

    [Header("状态显示")]
    [Tooltip("'当前使用中'标记")]
    public GameObject currentCharacterBadge;
    [Tooltip("锁定遮罩/图标")]
    public GameObject lockedOverlay;

    [Header("金币显示")]
    [Tooltip("显示当前金币数量的文本")]
    public TextMeshProUGUI goldText;

    [Header("角色技能树 — 手动布局")]
    [Tooltip("每个角色的技能树容器映射（在Inspector中配置）")]
    public List<CharacterSkillTreeLayout> skillTreeLayouts = new List<CharacterSkillTreeLayout>();

    [Tooltip("默认技能树容器（当角色没有配置专属布局时使用）")]
    public Transform skillNodeContainer;
    [Tooltip("默认连接线容器")]
    public Transform connectorContainer;

    [Header("连接线设置")]
    [Tooltip("连接线 Sprite（糖果棒样式，可为空则用纯色）")]
    public Sprite connectorSprite;
    [Tooltip("连接线颜色（已解锁路径）")]
    public Color connectorUnlockedColor = new Color(1f, 0.85f, 0.6f, 1f);
    [Tooltip("连接线颜色（锁定路径）")]
    public Color connectorLockedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    public Color connectorExcludedColor = new Color(0.8f, 0.2f, 0.2f, 0.35f);
    [Tooltip("连接线粗细")]
    public float connectorThickness = 14f;
    [Tooltip("连接线灰度 Material（锁定时使用）")]
    public Material connectorGrayscaleMaterial;

    [Header("节点详情弹窗")]
    [Tooltip("点击节点后弹出的详情面板")]
    public GameObject nodeDetailPopup;
    [Tooltip("弹窗中的节点名称")]
    public TextMeshProUGUI popupNodeName;
    [Tooltip("弹窗中的节点描述")]
    public TextMeshProUGUI popupDescription;
    [Tooltip("弹窗中的解锁按钮")]
    public Button popupUnlockButton;
    [Tooltip("弹窗中的解锁按钮文字")]
    public TextMeshProUGUI popupUnlockButtonText;

    [Header("分支重置")]
    [Tooltip("重置分支选择的按钮")]
    public Button resetBranchButton;
    [Tooltip("重置按钮文字")]
    public TextMeshProUGUI resetBranchButtonText;
    [Tooltip("重置费用")]
    public int resetBranchCost = 500;

    // 公开属性
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    public CharacterData CurrentCharacter => currentCharacter;
    public System.Action<CharacterData> OnCharacterSelected;

    // 内部变量
    private CharacterData currentCharacter;
    private GameObject currentPreviewModel;
    private List<CharacterSkillNodeUI> activeSkillNodeUIs = new List<CharacterSkillNodeUI>();
    private List<GameObject> generatedConnectors = new List<GameObject>();
    private List<ConnectorInfo> connectorInfos = new List<ConnectorInfo>();
    private CharacterSkillNodeUI currentPopupNode;
    private GameObject popupBlocker; // 全屏遮罩（点击关闭弹窗）
    private Transform activeSkillNodeContainer; // 当前激活的技能树容器
    private Transform activeConnectorContainer; // 当前激活的连接线容器

    private struct ConnectorInfo
    {
        public Image image;
        public CharacterSkillNodeUI fromNode;
        public CharacterSkillNodeUI toNode;
    }

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (nodeDetailPopup != null) nodeDetailPopup.SetActive(false);

        EnsureButtonBindings();
    }

    void OnEnable()
    {
        EnsureButtonBindings();
        RefreshGoldText();
    }

    private void EnsureButtonBindings()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectClicked);
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        if (unlockButton != null)
        {
            unlockButton.onClick.RemoveListener(OnUnlockClicked);
            unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        if (popupUnlockButton != null)
        {
            popupUnlockButton.onClick.RemoveListener(OnPopupUnlockClicked);
            popupUnlockButton.onClick.RemoveListener(OnResetConfirmed);
            popupUnlockButton.onClick.AddListener(OnPopupUnlockClicked);
        }

        if (resetBranchButton != null)
        {
            resetBranchButton.onClick.RemoveListener(OnResetBranchClicked);
            resetBranchButton.onClick.AddListener(OnResetBranchClicked);
        }
    }

    public void ShowCharacter(CharacterData data)
    {
        if (data == null) return;
        currentCharacter = data;

        EnsureButtonBindings();

        if (panelRoot != null) panelRoot.SetActive(true);

        if (characterIconImage != null && data.characterIcon != null)
            characterIconImage.sprite = data.characterIcon;
        if (nameText != null) nameText.text = data.LocalizedName;
        if (descriptionText != null) descriptionText.text = data.LocalizedDescription;

        UpdatePreviewModel(data);
        RefreshUI(data);
        BindSkillNodes(data);
    }

    public void ClosePanel()
    {
        CloseNodeDetailPopup();

        if (panelRoot != null) panelRoot.SetActive(false);
        currentCharacter = null;

        if (currentPreviewModel != null)
        {
            Destroy(currentPreviewModel);
            currentPreviewModel = null;
        }
    }

    // =========================================================
    //  角色技能树 — 手动布局 + 自动连线
    // =========================================================

    /// <summary>
    /// 绑定数据到手动摆放的节点 + 生成连接线
    /// </summary>
    private void BindSkillNodes(CharacterData data)
    {
        ClearConnectors();
        activeSkillNodeUIs.Clear();

        // 切换到对应角色的技能树容器
        SwitchSkillTreeLayout(data);

        if (activeSkillNodeContainer == null) return;

        if (!IsCharacterUnlocked(data))
        {
            activeSkillNodeContainer.gameObject.SetActive(false);
            return;
        }
        activeSkillNodeContainer.gameObject.SetActive(true);

        // 找到容器下所有 CharacterSkillNodeUI
        CharacterSkillNodeUI[] nodeUIs = activeSkillNodeContainer.GetComponentsInChildren<CharacterSkillNodeUI>(true);

        // 使用每个节点自身的 assignedNode（Inspector中拖入的SO）
        foreach (var nodeUI in nodeUIs)
        {
            if (nodeUI == null) continue;

            // 优先使用 assignedNode（手动拖入的），这是正确的数据源
            CharacterSkillNode nodeData = nodeUI.assignedNode;
            if (nodeData != null)
            {
                nodeUI.Setup(nodeData, data, (clicked) => ShowNodeDetailPopup(clicked));
                activeSkillNodeUIs.Add(nodeUI);
            }
            else
            {
                Debug.LogWarning($"[技能树] 节点 '{nodeUI.name}' 没有设置 assignedNode，请在Inspector中拖入对应的SO！");
            }
        }

        // 画连接线
        GenerateConnectors();
    }

    /// <summary>
    /// 切换技能树布局：隐藏所有容器，显示当前角色的容器
    /// </summary>
    private void SwitchSkillTreeLayout(CharacterData data)
    {
        // 先隐藏所有布局容器
        foreach (var layout in skillTreeLayouts)
        {
            if (layout.nodeContainer != null)
                layout.nodeContainer.gameObject.SetActive(false);
        }
        if (skillNodeContainer != null)
            skillNodeContainer.gameObject.SetActive(false);

        // 查找当前角色的专属布局
        activeSkillNodeContainer = null;
        activeConnectorContainer = null;

        foreach (var layout in skillTreeLayouts)
        {
            if (layout.character == data)
            {
                activeSkillNodeContainer = layout.nodeContainer;
                activeConnectorContainer = layout.connectorContainer;
                Debug.Log($"[技能树] 使用 {data.characterName} 的专属布局");
                break;
            }
        }

        // 如果没有找到专属布局，使用默认容器
        if (activeSkillNodeContainer == null)
        {
            activeSkillNodeContainer = skillNodeContainer;
            activeConnectorContainer = connectorContainer;
            Debug.Log($"[技能树] {data.characterName} 使用默认布局");
        }
    }

    /// <summary>
    /// 根据 connectTo 画连接线
    /// </summary>
    private void GenerateConnectors()
    {
        Transform lineParent = activeConnectorContainer != null ? activeConnectorContainer : activeSkillNodeContainer;

        foreach (var nodeUI in activeSkillNodeUIs)
        {
            if (nodeUI == null || nodeUI.connectTo == null) continue;

            foreach (var target in nodeUI.connectTo)
            {
                if (target == null) continue;

                Image lineImg = CreateLineBetween(nodeUI, target, lineParent);
                if (lineImg != null)
                {
                    connectorInfos.Add(new ConnectorInfo
                    {
                        image = lineImg,
                        fromNode = nodeUI,
                        toNode = target
                    });
                }
            }
        }

        RefreshConnectorColors();
    }

    /// <summary>
    /// 在两个节点中心之间画线
    /// </summary>
    private Image CreateLineBetween(CharacterSkillNodeUI from, CharacterSkillNodeUI to, Transform parent)
    {
        RectTransform fromRect = from.GetComponent<RectTransform>();
        RectTransform toRect = to.GetComponent<RectTransform>();
        if (fromRect == null || toRect == null) return null;

        Vector2 fromPos = GetLocalPositionInParent(fromRect, parent);
        Vector2 toPos = GetLocalPositionInParent(toRect, parent);

        Vector2 diff = toPos - fromPos;
        float distance = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        Vector2 midPoint = (fromPos + toPos) / 2f;

        GameObject lineGO = new GameObject("Connector", typeof(RectTransform), typeof(Image));
        lineGO.transform.SetParent(parent, false);
        lineGO.transform.SetAsFirstSibling(); // 渲染在最底层

        RectTransform lineRect = lineGO.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = midPoint;
        lineRect.sizeDelta = new Vector2(distance, connectorThickness);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);

        Image img = lineGO.GetComponent<Image>();
        img.sprite = connectorSprite;
        img.color = connectorLockedColor;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;

        generatedConnectors.Add(lineGO);
        return img;
    }

    private Vector2 GetLocalPositionInParent(RectTransform rect, Transform parent)
    {
        Vector3 worldPos = rect.position;
        if (parent is RectTransform parentRect)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out localPos);
            return localPos;
        }
        return rect.anchoredPosition;
    }

    /// <summary>
    /// 刷新连接线颜色（两端节点都已解锁 → 亮色）
    /// </summary>
    private void RefreshConnectorColors()
    {
        if (PlayerProgressManager.Instance == null) return;
        foreach (var info in connectorInfos)
        {
            if (info.image == null) continue;
            bool fromOk = info.fromNode != null && info.fromNode.nodeData != null &&
                          PlayerProgressManager.Instance.IsCharacterNodeUnlocked(info.fromNode.nodeData);
            bool toOk = info.toNode != null && info.toNode.nodeData != null &&
                        PlayerProgressManager.Instance.IsCharacterNodeUnlocked(info.toNode.nodeData);
            bool toExcluded = info.toNode != null && info.toNode.nodeData != null &&
                              PlayerProgressManager.Instance.IsNodeExcluded(info.toNode.nodeData);

            if (toExcluded && !toOk)
            {
                info.image.color = connectorExcludedColor;
                if (connectorGrayscaleMaterial != null)
                    info.image.material = connectorGrayscaleMaterial;
            }
            else if (fromOk && toOk)
            {
                info.image.color = connectorUnlockedColor;
                info.image.material = null; // 彩色
            }
            else
            {
                info.image.color = connectorLockedColor;
                if (connectorGrayscaleMaterial != null)
                    info.image.material = connectorGrayscaleMaterial;
            }
        }
    }

    private void ClearConnectors()
    {
        foreach (var go in generatedConnectors)
        {
            if (go != null) Destroy(go);
        }
        generatedConnectors.Clear();
        connectorInfos.Clear();
    }

    private void RefreshAllSkillNodes()
    {
        foreach (var nodeUI in activeSkillNodeUIs)
        {
            if (nodeUI != null) nodeUI.RefreshState();
        }
        RefreshConnectorColors();
        RefreshGoldText();
    }

    // =========================================================
    //  节点详情弹窗（全屏遮罩层关闭方案）
    // =========================================================

    /// <summary>
    /// 显示弹窗（已解锁/未解锁都可查看）
    /// </summary>
    private void ShowNodeDetailPopup(CharacterSkillNodeUI nodeUI)
    {
        if (nodeDetailPopup == null || nodeUI == null || nodeUI.nodeData == null) return;

        currentPopupNode = nodeUI;
        var node = nodeUI.nodeData;
        RestorePopupUnlockButtonAction();

        if (popupNodeName != null) popupNodeName.text = node.LocalizedNodeName;
        if (popupDescription != null) popupDescription.text = node.LocalizedDescription;

        // 自动适配弹窗大小：让底图根据文本内容自动调整高度
        EnsurePopupAutoSize();

        RefreshPopupButton(nodeUI);
        PositionPopupNearNode(nodeUI);

        // 先创建遮罩层（全屏透明按钮，点击关闭弹窗）
        CreatePopupBlocker();

        nodeDetailPopup.SetActive(true);
        // 确保弹窗在遮罩层上方
        nodeDetailPopup.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 创建全屏遮罩层 — 点击任意位置关闭弹窗
    /// </summary>
    private void CreatePopupBlocker()
    {
        DestroyPopupBlocker();

        // 在弹窗的父级下创建遮罩
        Transform parent = nodeDetailPopup.transform.parent;
        if (parent == null) return;

        popupBlocker = new GameObject("PopupBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
        popupBlocker.transform.SetParent(parent, false);

        // 全屏铺满
        RectTransform blockerRect = popupBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.sizeDelta = Vector2.zero;
        blockerRect.anchoredPosition = Vector2.zero;

        // 完全透明但可点击
        Image blockerImg = popupBlocker.GetComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0); // 透明
        blockerImg.raycastTarget = true;

        // 点击遮罩 → 关闭弹窗
        Button blockerBtn = popupBlocker.GetComponent<Button>();
        blockerBtn.onClick.AddListener(CloseNodeDetailPopup);

        // 遮罩在弹窗下方（先设置，弹窗随后会 SetAsLastSibling）
        popupBlocker.transform.SetSiblingIndex(nodeDetailPopup.transform.GetSiblingIndex());
    }

    private void DestroyPopupBlocker()
    {
        if (popupBlocker != null)
        {
            Destroy(popupBlocker);
            popupBlocker = null;
        }
    }

    /// <summary>
    /// 弹窗按钮 — 已解锁显示"已解锁"(不可点击)，未解锁显示费用
    /// </summary>
    private void RefreshPopupButton(CharacterSkillNodeUI nodeUI)
    {
        if (popupUnlockButton == null || nodeUI == null || PlayerProgressManager.Instance == null) return;
        var node = nodeUI.nodeData;
        bool isUnlocked = PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node);
        bool canUnlock = PlayerProgressManager.Instance.CanUnlockCharacterNode(nodeUI.characterData, node);
        bool canAfford = PlayerProgressManager.Instance.CanAfford(node.cost);

        if (isUnlocked)
        {
            popupUnlockButton.interactable = false;
            if (popupUnlockButtonText != null) popupUnlockButtonText.text = "\u5df2\u89e3\u9501";
        }
        else if (!canUnlock)
        {
            popupUnlockButton.interactable = false;
            if (popupUnlockButtonText != null) popupUnlockButtonText.text = "\u6761\u4ef6\u672a\u6ee1\u8db3";
        }
        else
        {
            popupUnlockButton.interactable = canAfford;
            if (popupUnlockButtonText != null) popupUnlockButtonText.text = $"{node.cost}";
        }
    }

    private void PositionPopupNearNode(CharacterSkillNodeUI nodeUI)
    {
        if (nodeDetailPopup == null) return;
        RectTransform popupRect = nodeDetailPopup.GetComponent<RectTransform>();
        RectTransform nodeRect = nodeUI.GetComponent<RectTransform>();
        if (popupRect != null && nodeRect != null)
        {
            popupRect.position = nodeRect.position + new Vector3(0, nodeRect.rect.height * 0.8f, 0);
        }
    }

    private void OnPopupUnlockClicked()
    {
        if (currentPopupNode == null || currentPopupNode.nodeData == null) return;
        if (PlayerProgressManager.Instance == null) return;

        CharacterData unlockedCharacter = currentPopupNode.characterData != null
            ? currentPopupNode.characterData
            : currentCharacter;
        var node = currentPopupNode.nodeData;
        if (PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node)) return;
        if (!PlayerProgressManager.Instance.CanUnlockCharacterNode(currentPopupNode.characterData, node)) return;
        if (!PlayerProgressManager.Instance.CanAfford(node.cost)) return;

        PlayerProgressManager.Instance.SpendGold(node.cost);
        PlayerProgressManager.Instance.UnlockCharacterNode(node);

        RefreshAllSkillNodes();
        if (unlockedCharacter != null)
            RefreshUI(unlockedCharacter);

        // 解锁成功，直接关闭弹窗
        CloseNodeDetailPopup();
    }

    /// <summary>
    /// 关闭弹窗
    /// </summary>
    public void CloseNodeDetailPopup()
    {
        DestroyPopupBlocker();
        if (nodeDetailPopup != null) nodeDetailPopup.SetActive(false);
        RestorePopupUnlockButtonAction();
        currentPopupNode = null;
    }

    private void RestorePopupUnlockButtonAction()
    {
        if (popupUnlockButton == null) return;
        popupUnlockButton.onClick.RemoveAllListeners();
        popupUnlockButton.onClick.AddListener(OnPopupUnlockClicked);
    }

    /// <summary>
    /// 确保弹窗面板有自适应大小的组件
    /// 首次调用时自动添加 VerticalLayoutGroup + ContentSizeFitter
    /// </summary>
    private void EnsurePopupAutoSize()
    {
        if (nodeDetailPopup == null) return;

        // 只添加一次 ContentSizeFitter
        ContentSizeFitter csf = nodeDetailPopup.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = nodeDetailPopup.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 宽度固定
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 高度自适应
        }

        // 只添加一次 VerticalLayoutGroup
        VerticalLayoutGroup vlg = nodeDetailPopup.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = nodeDetailPopup.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 15, 15);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        // 强制刷新布局（文本更新后立刻重算大小）
        LayoutRebuilder.ForceRebuildLayoutImmediate(nodeDetailPopup.GetComponent<RectTransform>());
    }

    // =========================================================
    //  角色选择逻辑（不变）
    // =========================================================

    private void RefreshUI(CharacterData data)
    {
        bool isUnlocked = IsCharacterUnlocked(data);
        bool isCurrent = IsCurrentCharacter(data);

        if (lockedOverlay != null) lockedOverlay.SetActive(!isUnlocked);
        if (characterIconImage != null)
            characterIconImage.color = isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
        if (currentCharacterBadge != null) currentCharacterBadge.SetActive(isCurrent);
        RefreshGoldText();

        if (isUnlocked)
        {
            if (unlockButton != null) unlockButton.gameObject.SetActive(false);
            if (selectButton != null)
            {
                selectButton.gameObject.SetActive(true);
                if (isCurrent)
                {
                    selectButton.interactable = false;
                    if (selectButtonText != null)
                        selectButtonText.text = LocalizationManager.T("ui.character.inuse");
                }
                else
                {
                    selectButton.interactable = true;
                    if (selectButtonText != null)
                        selectButtonText.text = LocalizationManager.T("ui.character.select");
                }
            }
        }
        else
        {
            if (selectButton != null) selectButton.gameObject.SetActive(false);
            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(true);
                bool canAfford = PlayerProgressManager.Instance != null &&
                                 PlayerProgressManager.Instance.CanAfford(data.unlockCost);
                unlockButton.interactable = canAfford;
                if (unlockButtonText != null)
                    unlockButtonText.text = LocalizationManager.T("ui.character.unlock") +
                                            $" ({data.unlockCost} " +
                                            LocalizationManager.T("ui.gold") + ")";
            }
        }

        // Keep the reset control visible for unlocked characters, and grey it out when there is nothing to reset.
        if (resetBranchButton != null)
        {
            int spentNodeCount = isUnlocked ? CountUnlockedCharacterNodes(data) : 0;
            bool hasSpentNode = spentNodeCount > 0;

            resetBranchButton.gameObject.SetActive(isUnlocked);
            resetBranchButton.interactable = hasSpentNode;

            if (resetBranchButtonText != null)
                resetBranchButtonText.text = hasSpentNode
                    ? "\u91cd\u7f6e\u6280\u80fd\u6811"
                    : "\u6682\u65e0\u53ef\u91cd\u7f6e";
        }
    }

    private void OnSelectClicked()
    {
        if (currentCharacter == null) return;
        if (DataManager.Instance != null)
        {
            DataManager.Instance.selectedCharacter = currentCharacter;
            DataManager.Instance.selectedCharacterID = currentCharacter.characterID;
        }
        OnCharacterSelected?.Invoke(currentCharacter);
        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.SaveGame();
        RefreshUI(currentCharacter);
    }

    private void OnUnlockClicked()
    {
        if (currentCharacter == null || PlayerProgressManager.Instance == null) return;
        if (string.IsNullOrEmpty(currentCharacter.characterID))
        {
            Debug.LogError($"[CharacterUnlock] {currentCharacter.name} is missing characterID.");
            return;
        }
        if (!PlayerProgressManager.Instance.CanAfford(currentCharacter.unlockCost))
        {
            Debug.LogWarning($"[CharacterUnlock] Not enough gold: {PlayerProgressManager.Instance.currentGold}/{currentCharacter.unlockCost}");
            return;
        }
        PlayerProgressManager.Instance.SpendGold(currentCharacter.unlockCost);
        PlayerProgressManager.Instance.UnlockItem(currentCharacter.characterID);
        PlayerProgressManager.Instance.SaveGame();
        RefreshUI(currentCharacter);
        BindSkillNodes(currentCharacter); // 解锁后立即刷新技能树显示
    }

    /// <summary>
    /// 重置分支按钮点击回调 —— 显示二次确认弹窗
    /// </summary>
    private void OnResetBranchClicked()
    {
        if (currentCharacter == null)
        {
            Debug.LogWarning("[CharacterSkillTree] Reset clicked without a current character.");
            return;
        }

        if (PlayerProgressManager.Instance == null)
        {
            Debug.LogWarning("[CharacterSkillTree] Reset clicked but PlayerProgressManager is missing.");
            return;
        }

        int spentNodeCount = CountUnlockedCharacterNodes(currentCharacter);
        if (spentNodeCount <= 0)
        {
            Debug.Log("[CharacterSkillTree] Reset clicked, but this character has no unlocked skill nodes.");
            RefreshUI(currentCharacter);
            return;
        }

        ShowResetConfirmDialog();
    }

    /// <summary>
    /// 显示重置确认弹窗
    /// </summary>
    private void ShowResetConfirmDialog()
    {
        // 复用节点详情弹窗作为确认弹窗
        if (nodeDetailPopup == null)
        {
            Debug.LogWarning("[CharacterSkillTree] Reset confirm popup is not assigned.");
            return;
        }

        if (popupNodeName != null) popupNodeName.text = "\u91cd\u7f6e\u786e\u8ba4";
        if (popupDescription != null)
            popupDescription.text = "\u786e\u5b9a\u8981\u91cd\u7f6e\u5f53\u524d\u89d2\u8272\u7684\n\u6280\u80fd\u6811\u5206\u652f\u5417\uff1f\n\n\u91cd\u7f6e\u540e\u53ef\u91cd\u65b0\u9009\u62e9\u5206\u652f\u65b9\u5411";

        EnsurePopupAutoSize();

        // 按钮改为确认功能
        if (popupUnlockButton != null)
        {
            popupUnlockButton.interactable = true;
            popupUnlockButton.onClick.RemoveAllListeners();
            popupUnlockButton.onClick.AddListener(OnResetConfirmed);
        }
        if (popupUnlockButtonText != null) popupUnlockButtonText.text = "\u786e\u8ba4\u91cd\u7f6e";

        // 居中显示
        RectTransform popupRect = nodeDetailPopup.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.anchoredPosition = Vector2.zero;
        }

        CreatePopupBlocker();
        nodeDetailPopup.SetActive(true);
        nodeDetailPopup.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 确认重置回调 —— 执行实际重置（不扣金币）
    /// </summary>
    private void OnResetConfirmed()
    {
        if (currentCharacter == null || PlayerProgressManager.Instance == null) return;

        // 使用新的全重置方法（不扣金币）
        PlayerProgressManager.Instance.ResetCharacterSkillTree(currentCharacter);
        Debug.Log($"[CharacterSkillTree] Reset skill tree for {currentCharacter.characterID}.");

        // 关闭弹窗并恢复按钮功能
        CloseResetConfirmDialog();

        // 重新绑定节点状态
        BindSkillNodes(currentCharacter);
        RefreshUI(currentCharacter);
        RefreshAllSkillNodes();
    }

    /// <summary>
    /// 关闭确认弹窗并恢复按钮原始功能
    /// </summary>
    private void CloseResetConfirmDialog()
    {
        DestroyPopupBlocker();
        if (nodeDetailPopup != null) nodeDetailPopup.SetActive(false);

        // 恢复解锁按钮的原始点击事件
        RestorePopupUnlockButtonAction();
        currentPopupNode = null;
    }

    private void RefreshGoldText()
    {
        if (PlayerProgressManager.Instance == null) return;

        int currentGold = PlayerProgressManager.Instance.currentGold;
        if (goldText != null)
        {
            goldText.gameObject.SetActive(true);
            goldText.enabled = true;
            goldText.transform.SetAsLastSibling();
            goldText.color = new Color(0.24f, 0.12f, 0.03f, 1f);
            goldText.text = currentGold.ToString();
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateGoldDisplay(currentGold);
    }

    private int CountUnlockedCharacterNodes(CharacterData data)
    {
        if (data == null || data.characterSkillNodes == null || PlayerProgressManager.Instance == null)
            return 0;

        int count = 0;
        foreach (var node in data.characterSkillNodes)
        {
            if (node != null && PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node))
                count++;
        }

        return count;
    }

    private bool IsCharacterUnlocked(CharacterData data)
    {
        if (data == null) return false;
        if (data.isDefaultUnlocked) return true;
        if (PlayerProgressManager.Instance != null)
            return PlayerProgressManager.Instance.unlockedItems.Contains(data.characterID);
        return false;
    }

    private bool IsCurrentCharacter(CharacterData data)
    {
        if (data == null || DataManager.Instance == null) return false;
        return DataManager.Instance.selectedCharacter == data;
    }

    private void UpdatePreviewModel(CharacterData data)
    {
        if (currentPreviewModel != null)
        {
            Destroy(currentPreviewModel);
            currentPreviewModel = null;
        }
        if (data.characterPreviewPrefab == null || characterPreviewContainer == null) return;

        currentPreviewModel = Instantiate(data.characterPreviewPrefab, characterPreviewContainer);
        currentPreviewModel.transform.localPosition = Vector3.zero;
        currentPreviewModel.transform.localRotation = Quaternion.identity;

        Animator previewAnimator = currentPreviewModel.GetComponent<Animator>();
        if (previewAnimator != null) previewAnimator.Play("Idle");

        SetLayerRecursively(currentPreviewModel, LayerMask.NameToLayer("UI_CharacterPreview"));
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null || newLayer < 0) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
