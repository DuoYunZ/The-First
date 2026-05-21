using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 角色技能树节点UI — 手动布局模式
/// 你在 Unity 中手动摆放节点位置，脚本自动画连接线
/// 通过 connectTo 列表指定当前节点连向哪些节点
/// 在 Inspector 中拖入 assignedNode 指定对应的技能SO
/// </summary>
[RequireComponent(typeof(Button))]
public class CharacterSkillNodeUI : MonoBehaviour
{
    [Header("UI 组件")]
    public Image iconImage;       // 技能图标
    public Image borderImage;     // 边框/底板（用于状态变色）

    [Header("技能数据（在Inspector中拖入对应的SO）")]
    [Tooltip("将对应的 CharacterSkillNode SO 拖到这里，拖入后GameObject自动改名")]
    public CharacterSkillNode assignedNode;

    [Header("连线目标")]
    [Tooltip("从当前节点画线连到这些节点（在 Inspector 中拖入）")]
    public List<CharacterSkillNodeUI> connectTo = new List<CharacterSkillNodeUI>();

    [Header("状态颜色")]
    public Color unlockedBorderColor = new Color(0.3f, 1f, 0.3f, 1f);   // 绿色 — 已解锁
    public Color availableBorderColor = new Color(1f, 0.9f, 0.4f, 1f);  // 金色 — 可购买
    public Color lockedBorderColor = new Color(0.4f, 0.4f, 0.4f, 1f);   // 灰色 — 锁定
    public Color excludedBorderColor = new Color(0.8f, 0.2f, 0.2f, 0.8f); // 红色 — 互斥锁定

    [Header("灰度效果")]
    [Tooltip("锁定时使用的灰度 Material（使用 UI/Grayscale Shader）")]
    public Material grayscaleMaterial;

    [Header("互斥标记")]
    [Tooltip("互斥锁定时显示的❌图标（可选）")]
    public GameObject excludedOverlay;

    // 运行时数据
    [HideInInspector] public CharacterSkillNode nodeData;
    [HideInInspector] public CharacterData characterData;
    private System.Action<CharacterSkillNodeUI> onClicked;
    private Button cachedButton;
    private CanvasGroup cachedCanvasGroup;
    private Material defaultMaterial; // 默认 Material（亮色状态）

    /// <summary>
    /// 编辑器中拖入SO后自动改名 — 方便在Hierarchy中识别节点
    /// </summary>
    private void OnValidate()
    {
        if (assignedNode != null)
        {
            // 用层级+技能名命名，例如 "L1_攻击力I" 或 "L2_⚔️精准斩击"
            string cleanName = assignedNode.nodeName.Replace(" ", "");
            gameObject.name = $"L{assignedNode.layer}_{cleanName}";
        }
    }

    /// <summary>
    /// 初始化节点数据和回调
    /// 在手动布局模式下，node参数可以为null（使用assignedNode）
    /// </summary>
    public void Setup(CharacterSkillNode node, CharacterData charData, System.Action<CharacterSkillNodeUI> onClick)
    {
        // 优先使用传入的node，其次使用Inspector中拖入的assignedNode
        nodeData = node != null ? node : assignedNode;
        characterData = charData;
        onClicked = onClick;

        // 记住默认 Material
        if (iconImage != null && defaultMaterial == null)
            defaultMaterial = iconImage.material;

        cachedCanvasGroup = GetComponent<CanvasGroup>();
        if (cachedCanvasGroup == null)
            cachedCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 设置图标
        if (iconImage != null && nodeData != null && nodeData.icon != null)
            iconImage.sprite = nodeData.icon;

        // 自动获取 Button
        cachedButton = GetComponent<Button>();
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveAllListeners();
            cachedButton.onClick.AddListener(() =>
            {
                onClicked?.Invoke(this);
            });
        }

        // 初始隐藏互斥覆盖层
        if (excludedOverlay != null)
            excludedOverlay.SetActive(false);

        RefreshState();
    }

    /// <summary>
    /// 刷新节点视觉状态
    /// </summary>
    public void RefreshState()
    {
        if (nodeData == null || PlayerProgressManager.Instance == null) return;

        bool isUnlocked = PlayerProgressManager.Instance.IsCharacterNodeUnlocked(nodeData);
        bool isExcluded = PlayerProgressManager.Instance.IsNodeExcluded(nodeData);
        bool canUnlock = !isExcluded && PlayerProgressManager.Instance.CanUnlockCharacterNode(characterData, nodeData);

        if (cachedCanvasGroup == null)
        {
            cachedCanvasGroup = GetComponent<CanvasGroup>();
            if (cachedCanvasGroup == null)
                cachedCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (cachedCanvasGroup != null)
        {
            cachedCanvasGroup.alpha = isUnlocked ? 1f : isExcluded ? 0.28f : canUnlock ? 0.62f : 0.32f;
            cachedCanvasGroup.blocksRaycasts = true;
            cachedCanvasGroup.interactable = true;
        }

        // 边框颜色
        if (borderImage != null)
        {
            if (isUnlocked)
                borderImage.color = unlockedBorderColor;
            else if (isExcluded)
                borderImage.color = excludedBorderColor; // 互斥锁定 — 红色
            else if (canUnlock)
                borderImage.color = availableBorderColor;
            else
                borderImage.color = lockedBorderColor;
        }

        // 图标材质
        if (iconImage != null)
        {
            if (isUnlocked)
            {
                // 恢复默认 Material（彩色）
                iconImage.material = defaultMaterial;
                iconImage.color = Color.white;
            }
            else if (isExcluded)
            {
                // 互斥锁定：灰度/红色降亮
                if (grayscaleMaterial != null)
                    iconImage.material = grayscaleMaterial;
                iconImage.color = new Color(0.75f, 0.35f, 0.35f, 1f);
            }
            else if (canUnlock)
            {
                iconImage.material = defaultMaterial;
                iconImage.color = new Color(1f, 0.9f, 0.55f, 1f);
            }
            else
            {
                // 普通锁定：即使没有灰度材质，也明显压暗。
                if (grayscaleMaterial != null)
                    iconImage.material = grayscaleMaterial;
                iconImage.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }

        // 互斥覆盖层（❌图标）
        if (excludedOverlay != null)
            excludedOverlay.SetActive(isExcluded);

        // 已解锁 → 可点击查看；可解锁 → 可点击购买；其他 → 不可点击
        if (cachedButton != null)
        {
            cachedButton.interactable = isUnlocked || canUnlock;
        }
    }
}
