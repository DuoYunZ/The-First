using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
    [Tooltip("技能树节点的父容器（你在这个容器下手动摆放节点）")]
    public Transform skillNodeContainer;
    [Tooltip("连接线的父容器（线会生成在这里，放在节点下方的层级）")]
    public Transform connectorContainer;

    [Header("连接线设置")]
    [Tooltip("连接线 Sprite（糖果棒样式，可为空则用纯色）")]
    public Sprite connectorSprite;
    [Tooltip("连接线颜色（已解锁路径）")]
    public Color connectorUnlockedColor = new Color(1f, 0.85f, 0.6f, 1f);
    [Tooltip("连接线颜色（锁定路径）")]
    public Color connectorLockedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
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

        if (selectButton != null) selectButton.onClick.AddListener(OnSelectClicked);
        if (unlockButton != null) unlockButton.onClick.AddListener(OnUnlockClicked);
        if (popupUnlockButton != null) popupUnlockButton.onClick.AddListener(OnPopupUnlockClicked);
    }

    public void ShowCharacter(CharacterData data)
    {
        if (data == null) return;
        currentCharacter = data;

        if (panelRoot != null) panelRoot.SetActive(true);

        if (characterIconImage != null && data.characterIcon != null)
            characterIconImage.sprite = data.characterIcon;
        if (nameText != null) nameText.text = data.characterName;
        if (descriptionText != null) descriptionText.text = data.description;

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

        if (skillNodeContainer == null) return;
        if (data.characterSkillNodes == null || data.characterSkillNodes.Count == 0) return;

        if (!IsCharacterUnlocked(data))
        {
            skillNodeContainer.gameObject.SetActive(false);
            return;
        }
        skillNodeContainer.gameObject.SetActive(true);

        // 找到容器下所有 CharacterSkillNodeUI（按层级顺序）
        CharacterSkillNodeUI[] nodeUIs = skillNodeContainer.GetComponentsInChildren<CharacterSkillNodeUI>(true);

        // 按 SO 列表顺序绑定
        int count = Mathf.Min(data.characterSkillNodes.Count, nodeUIs.Length);
        for (int i = 0; i < count; i++)
        {
            nodeUIs[i].Setup(data.characterSkillNodes[i], data, (clicked) => ShowNodeDetailPopup(clicked));
            activeSkillNodeUIs.Add(nodeUIs[i]);
        }

        // 画连接线
        GenerateConnectors();
    }

    /// <summary>
    /// 根据 connectTo 画连接线
    /// </summary>
    private void GenerateConnectors()
    {
        Transform lineParent = connectorContainer != null ? connectorContainer : skillNodeContainer;

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

            if (fromOk && toOk)
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
        if (goldText != null && PlayerProgressManager.Instance != null)
            goldText.text = PlayerProgressManager.Instance.currentGold.ToString();
    }

    // =========================================================
    //  节点详情弹窗（全屏遮罩层关闭方案）
    // =========================================================

    /// <summary>
    /// 显示弹窗（已解锁的节点不弹窗）
    /// </summary>
    private void ShowNodeDetailPopup(CharacterSkillNodeUI nodeUI)
    {
        if (nodeDetailPopup == null || nodeUI == null || nodeUI.nodeData == null) return;

        // 已解锁的节点不需要弹窗
        if (PlayerProgressManager.Instance != null &&
            PlayerProgressManager.Instance.IsCharacterNodeUnlocked(nodeUI.nodeData))
            return;

        currentPopupNode = nodeUI;
        var node = nodeUI.nodeData;

        if (popupNodeName != null) popupNodeName.text = node.nodeName;
        if (popupDescription != null) popupDescription.text = node.description;

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
    /// 弹窗按钮 — 只显示费用数字
    /// </summary>
    private void RefreshPopupButton(CharacterSkillNodeUI nodeUI)
    {
        if (popupUnlockButton == null || nodeUI == null) return;
        var node = nodeUI.nodeData;
        bool isUnlocked = PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node);
        bool canAfford = PlayerProgressManager.Instance.CanAfford(node.cost);

        if (isUnlocked)
        {
            popupUnlockButton.interactable = false;
            if (popupUnlockButtonText != null) popupUnlockButtonText.text = "\u2713";
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

        var node = currentPopupNode.nodeData;
        if (PlayerProgressManager.Instance.IsCharacterNodeUnlocked(node)) return;
        if (!PlayerProgressManager.Instance.CanUnlockCharacterNode(currentPopupNode.characterData, node)) return;
        if (!PlayerProgressManager.Instance.CanAfford(node.cost)) return;

        PlayerProgressManager.Instance.SpendGold(node.cost);
        PlayerProgressManager.Instance.UnlockCharacterNode(node);

        Debug.Log($"<color=cyan>[角色技能树] 已解锁: {node.nodeName}，花费 {node.cost} 金币</color>");

        RefreshAllSkillNodes();

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
        currentPopupNode = null;
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
        if (goldText != null && PlayerProgressManager.Instance != null)
            goldText.text = PlayerProgressManager.Instance.currentGold.ToString();

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
    }

    private void OnSelectClicked()
    {
        if (currentCharacter == null) return;
        if (DataManager.Instance != null)
        {
            DataManager.Instance.selectedCharacter = currentCharacter;
            DataManager.Instance.selectedCharacterID = currentCharacter.characterID;
        }
        Debug.Log($"<color=green>[角色选择] 已选择角色: {currentCharacter.characterName}</color>");
        OnCharacterSelected?.Invoke(currentCharacter);
        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.SaveGame();
        RefreshUI(currentCharacter);
    }

    private void OnUnlockClicked()
    {
        if (currentCharacter == null || PlayerProgressManager.Instance == null) return;
        if (!PlayerProgressManager.Instance.CanAfford(currentCharacter.unlockCost))
        {
            Debug.Log("[角色解锁] 金币不足！");
            return;
        }
        PlayerProgressManager.Instance.SpendGold(currentCharacter.unlockCost);
        PlayerProgressManager.Instance.UnlockItem(currentCharacter.characterID);
        Debug.Log($"<color=yellow>[角色解锁] 已解锁角色: {currentCharacter.characterName}，花费 {currentCharacter.unlockCost} 金币</color>");
        RefreshUI(currentCharacter);
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
