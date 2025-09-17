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
    private Animator animator;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        animator = GetComponent<Animator>();
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

        iconImage.sprite = sourceNode.skillIcon;
        nameText.text = sourceNode.skillName;
        descriptionText.text = displayedOption.description;

        Canvas.ForceUpdateCanvases();
        initialScale = transform.localScale;
        initialAnchorPos = rectTransform.anchoredPosition;

    }
    public void Show()
    {
        if (animator != null)
        {
            // 触发我们在Animator Controller中设置的名为"Show"的触发器
            animator.SetTrigger("Show");
        }
        else
        {
            Debug.LogError("在卡片上找不到Animator组件！", this);
        }
    }

    public void OnCardSelected()
    {
        if (UpgradeManager.Instance != null && sourceNode != null && displayedOption != null)
        {
            UpgradeManager.Instance.OnUpgradeOptionSelected(sourceNode, displayedOption);
        }
    }

   
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOAnchorPosY(initialAnchorPos.y + 50f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale * 1.05f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true); // 增加缩放
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOAnchorPosY(initialAnchorPos.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true); // 恢复缩放
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSelected) return;
        isSelected = true;

        // 停止所有正在进行的悬浮动画，避免冲突
        rectTransform.DOKill();
        transform.DOKill();

        if (animator != null)
        {
            animator.SetTrigger("Select");
        }
        DG.Tweening.Sequence clickSequence = DOTween.Sequence();

        clickSequence.AppendInterval(0.1f); // 根据Card_Selected动画的长度调整这个时间

    // 后续的渐隐和飞走动画
    clickSequence.Append(rectTransform.DOAnchorPosY(initialAnchorPos.y + 200f, 0.4f).SetEase(Ease.InBack));
    clickSequence.Join(canvasGroup.DOFade(0f, 0.3f).SetDelay(0.1f)); // 这个delay是相对于上一个动画而言的
    clickSequence.SetUpdate(true);
    
    // 动画序列完成后，才真正执行升级逻辑
    clickSequence.OnComplete(OnCardSelected);
    }
}