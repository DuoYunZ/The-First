using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{  
    [Header("核心UI组件 (在预制件中设置)")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    // 内部变量
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 initialScale;
    private Vector2 initialAnchorPos;
    private SkillTreeNodeData sourceNode;
    private UpgradeOption displayedOption; // 【修改】现在存储的是一个 UpgradeOption
    private bool isSelected = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // Setup 方法现在接收“技能节点”和“具体选项”两个参数
    public void Setup(SkillTreeNodeData node, UpgradeOption option)
    {
        this.sourceNode = node;
        this.displayedOption = option;

        if (displayedOption == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 直接更新UI内容，不再关心背景样式
        iconImage.sprite = sourceNode.skillIcon;
        nameText.text = sourceNode.skillName;
        descriptionText.text = displayedOption.description;

        // 播放出现动画
        Canvas.ForceUpdateCanvases();
        initialScale = transform.localScale;
        initialAnchorPos = rectTransform.anchoredPosition;
        AnimateIn();
    }

    public void OnCardSelected()
    {
        if (UpgradeManager.Instance != null && sourceNode != null && displayedOption != null)
        {
            UpgradeManager.Instance.OnUpgradeOptionSelected(sourceNode, displayedOption);
        }
    }

    // --- 所有动画和交互方法保持不变 ---
    private void AnimateIn()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0;
        transform.localScale = initialScale * 0.8f;
        transform.DOScale(initialScale, 0.5f).SetUpdate(true).SetEase(Ease.OutBack);
        canvasGroup.DOFade(1f, 0.4f).SetUpdate(true);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOAnchorPosY(initialAnchorPos.y + 50f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale * 1.05f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOAnchorPosY(initialAnchorPos.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSelected) return;
        isSelected = true;

        DG.Tweening.Sequence clickSequence = DOTween.Sequence();
        clickSequence.Append(transform.DOPunchScale(new Vector3(-0.1f, -0.1f, 0), 0.2f, 1));
        clickSequence.Append(rectTransform.DOAnchorPosY(initialAnchorPos.y + 200f, 0.4f).SetEase(Ease.InBack));
        clickSequence.Join(canvasGroup.DOFade(0f, 0.3f).SetDelay(0.1f));
        clickSequence.SetUpdate(true);
        clickSequence.OnComplete(OnCardSelected);
    }
}