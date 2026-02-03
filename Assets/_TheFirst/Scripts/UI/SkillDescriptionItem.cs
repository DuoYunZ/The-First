using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class SkillDescriptionItem : MonoBehaviour
{
    [Header("UI 组件引用")]
    public Button myButton;                 // 自身的按钮组件
    public Image bgImage;                   // 背景图

    [Header("文本信息")]
    public TextMeshProUGUI descriptionText; // 显示技能描述
    public TextMeshProUGUI costText;        // 显示价格数字

    [Header("动画对象")]
    public GameObject costRoot;             // 金币图标和价格的父物体
    public GameObject checkmarkObject;      // 【新增】对钩图片的物体
    public CanvasGroup costCanvasGroup;     // 【新增】用于控制 Cost 的透明度

    [Header("颜色配置")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color unlockedColor = new Color(0.2f, 0.6f, 0.2f, 1f);
    public Color cannotAffordColor = new Color(0.5f, 0.2f, 0.2f, 1f);

    [Header("动画参数 (Juice)")]
    public float animDuration = 0.5f;
    public AnimationCurve scaleCurve = new AnimationCurve( // 默认的弹跳曲线
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1.2f), // 弹得比 1.0 大一点
        new Keyframe(1f, 1f)      // 回到 1.0
    );

    public WeaponUpgradeNode NodeData { get; private set; }
    private System.Action onPurchaseCallback;

    public void Setup(WeaponUpgradeNode nodeData, System.Action onPurchase)
    {
        this.NodeData = nodeData;
        this.onPurchaseCallback = onPurchase;

        // 1. 设置文本
        if (descriptionText != null) descriptionText.text = nodeData.description;
        if (costText != null) costText.text = nodeData.cost.ToString();

        // 2. 初始化状态 (无动画)
        bool isUnlocked = PlayerProgressManager.Instance.IsNodeUnlocked(nodeData);
        InitializeVisuals(isUnlocked);

        // 3. 绑定点击
        if (myButton != null)
        {
            myButton.onClick.RemoveAllListeners();
            myButton.onClick.AddListener(OnClickPurchase);
        }
    }

    // 初始化静态显示 (不播放动画)
    private void InitializeVisuals(bool isUnlocked)
    {
        if (isUnlocked)
        {
            // 已解锁：显示绿色，隐藏价格，显示对钩
            if (bgImage != null) bgImage.color = unlockedColor;
            if (descriptionText != null) descriptionText.color = Color.white;

            if (costRoot != null) costRoot.SetActive(false);
            if (checkmarkObject != null)
            {
                checkmarkObject.SetActive(true);
                checkmarkObject.transform.localScale = Vector3.one; // 正常大小
            }
            if (myButton != null) myButton.interactable = false;
        }
        else
        {
            // 未解锁：显示价格，隐藏对钩
            if (costRoot != null)
            {
                costRoot.SetActive(true);
                // 重置透明度和位置
                if (costCanvasGroup != null) costCanvasGroup.alpha = 1f;
                costRoot.transform.localPosition = Vector3.zero; // 假设初始位置是0 (或者是布局自动控制)
            }
            if (checkmarkObject != null) checkmarkObject.SetActive(false);

            bool canAfford = PlayerProgressManager.Instance.CanAfford(NodeData.cost);
            if (bgImage != null) bgImage.color = canAfford ? normalColor : cannotAffordColor;
            if (descriptionText != null) descriptionText.color = Color.gray;
            if (myButton != null) myButton.interactable = canAfford;
        }
    }

    private void OnClickPurchase()
    {
        if (PlayerProgressManager.Instance.IsNodeUnlocked(NodeData)) return;
        if (!PlayerProgressManager.Instance.CanAfford(NodeData.cost)) return;

        // 1. 逻辑处理
        PlayerProgressManager.Instance.SpendGold(NodeData.cost);
        PlayerProgressManager.Instance.UnlockNode(NodeData);

        // 2. 播放 Juice 动画！
        StartCoroutine(PlayUnlockJuice());

        // 3. 通知外部 (只刷新六边形，不要刷新我！)
        onPurchaseCallback?.Invoke();
    }

    private IEnumerator PlayUnlockJuice()
    {
        // 禁用按钮防止连点
        if (myButton != null) myButton.interactable = false;

        // --- 阶段 1: 背景变绿 ---
        if (bgImage != null) bgImage.color = unlockedColor;
        if (descriptionText != null) descriptionText.color = Color.white;

        // --- 阶段 2: Cost 向右滑动并淡出 ---
        if (costRoot != null)
        {
            RectTransform costRect = costRoot.GetComponent<RectTransform>();
            Vector3 startPos = costRect.anchoredPosition;
            Vector3 endPos = startPos + new Vector3(100f, 0f, 0f); // 向右移动 100 像素

            float timer = 0f;
            while (timer < 0.3f) // 0.3秒淡出
            {
                timer += Time.unscaledDeltaTime; // 使用 unscaled 以防时间暂停
                float t = timer / 0.3f;

                // 移动
                costRect.anchoredPosition = Vector3.Lerp(startPos, endPos, t);

                // 淡出
                if (costCanvasGroup != null) costCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

                yield return null;
            }
            costRoot.SetActive(false); // 彻底隐藏
        }

        // --- 阶段 3: 对钩弹出来 (Scale Bounce) ---
        if (checkmarkObject != null)
        {
            checkmarkObject.SetActive(true);
            checkmarkObject.transform.localScale = Vector3.zero; // 从0开始

            float timer = 0f;
            while (timer < animDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / animDuration;

                // 使用曲线计算缩放值 (0 -> 1.2 -> 1.0)
                float scaleVal = scaleCurve.Evaluate(t);
                checkmarkObject.transform.localScale = new Vector3(scaleVal, scaleVal, 1f);

                yield return null;
            }
            checkmarkObject.transform.localScale = Vector3.one; // 确保最后是 1
        }
    }
}