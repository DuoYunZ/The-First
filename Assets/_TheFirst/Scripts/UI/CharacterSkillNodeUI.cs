using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 角色技能树节点UI — 手动布局模式
/// 你在 Unity 中手动摆放节点位置，脚本自动画连接线
/// 通过 connectTo 列表指定当前节点连向哪些节点
/// </summary>
[RequireComponent(typeof(Button))]
public class CharacterSkillNodeUI : MonoBehaviour
{
    [Header("UI 组件")]
    public Image iconImage;       // 技能图标
    public Image borderImage;     // 边框/底板（用于状态变色）

    [Header("连线目标")]
    [Tooltip("从当前节点画线连到这些节点（在 Inspector 中拖入）")]
    public List<CharacterSkillNodeUI> connectTo = new List<CharacterSkillNodeUI>();

    [Header("状态颜色")]
    public Color unlockedBorderColor = new Color(0.3f, 1f, 0.3f, 1f);   // 绿色 — 已解锁
    public Color availableBorderColor = new Color(1f, 0.9f, 0.4f, 1f);  // 金色 — 可购买
    public Color lockedBorderColor = new Color(0.4f, 0.4f, 0.4f, 1f);   // 灰色 — 锁定

    [Header("灰度效果")]
    [Tooltip("锁定时使用的灰度 Material（使用 UI/Grayscale Shader）")]
    public Material grayscaleMaterial;

    // 运行时数据
    [HideInInspector] public CharacterSkillNode nodeData;
    [HideInInspector] public CharacterData characterData;
    private System.Action<CharacterSkillNodeUI> onClicked;
    private Button cachedButton;
    private Material defaultMaterial; // 默认 Material（亮色状态）

    /// <summary>
    /// 初始化节点数据和回调
    /// </summary>
    public void Setup(CharacterSkillNode node, CharacterData charData, System.Action<CharacterSkillNodeUI> onClick)
    {
        nodeData = node;
        characterData = charData;
        onClicked = onClick;

        // 记住默认 Material
        if (iconImage != null && defaultMaterial == null)
            defaultMaterial = iconImage.material;

        // 设置图标
        if (iconImage != null && node != null && node.icon != null)
            iconImage.sprite = node.icon;

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

        RefreshState();
    }

    /// <summary>
    /// 刷新节点视觉状态
    /// </summary>
    public void RefreshState()
    {
        if (nodeData == null || PlayerProgressManager.Instance == null) return;

        bool isUnlocked = PlayerProgressManager.Instance.IsCharacterNodeUnlocked(nodeData);
        bool canUnlock = PlayerProgressManager.Instance.CanUnlockCharacterNode(characterData, nodeData);

        // 边框颜色（全不透明）
        if (borderImage != null)
        {
            if (isUnlocked)
                borderImage.color = unlockedBorderColor;
            else if (canUnlock)
                borderImage.color = availableBorderColor;
            else
                borderImage.color = lockedBorderColor;
        }

        // 图标：已解锁 = 正常彩色，锁定/可解锁 = 灰度 Material
        if (iconImage != null)
        {
            if (isUnlocked)
            {
                // 恢复默认 Material（彩色）
                iconImage.material = defaultMaterial;
                iconImage.color = Color.white;
            }
            else
            {
                // 使用灰度 Material（去饱和度）
                if (grayscaleMaterial != null)
                    iconImage.material = grayscaleMaterial;
                // 保持颜色全白全不透明，灰度由 Shader 处理
                iconImage.color = Color.white;
            }
        }

        // 锁定节点不可点击
        if (cachedButton != null)
        {
            cachedButton.interactable = isUnlocked || canUnlock;
        }
    }
}
