using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 宝石镶嵌动画覆盖层
/// 选择武器升级卡后弹出，播放宝石飞入插槽的动画
/// 需要在Canvas下作为独立面板挂载
/// </summary>
public class GemEmbedOverlay : MonoBehaviour
{
    [Header("卡片背景")]
    [Tooltip("卡片底板Image（显示武器图标）")]
    public Image cardBackground;
    public Image weaponIcon;

    [Header("宝石插槽")]
    [Tooltip("5个宝石插槽Image，从左到右排列")]
    public Image[] gemSlots = new Image[5];

    [Header("宝石资源")]
    [Tooltip("第一轮宝石Sprite（基础样式）")]
    public Sprite gemSpriteTier0;
    [Tooltip("第二轮宝石Sprite（升级样式）")]
    public Sprite gemSpriteTier1;
    [Tooltip("空插槽Sprite")]
    public Sprite emptySlotSprite;

    [Header("飞行宝石")]
    [Tooltip("飞行宝石的预制件（一个带Image的RectTransform）")]
    public GameObject flyingGemPrefab;
    [Tooltip("飞行宝石的起始位置")]
    public RectTransform flyingGemStart;

    [Header("动画参数")]
    public float flyDuration = 0.5f;
    public float bounceScale = 1.3f;
    public float glowDuration = 0.3f;
    public float showDuration = 1.5f; // 动画展示总时长

    [Header("发光特效")]
    public Image glowEffect; // 插槽发光Image

    // 内部
    private CanvasGroup canvasGroup;
    private Action onComplete;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示宝石镶嵌动画
    /// </summary>
    /// <param name="weapon">武器数据</param>
    /// <param name="gemIndex">要填充的插槽索引 (0-4)</param>
    /// <param name="gemTier">宝石轮次 (0=第一轮, 1=第二轮...)</param>
    /// <param name="totalGems">该武器的总宝石数</param>
    /// <param name="onFinished">动画完成回调</param>
    public void Show(WeaponStatBlock weapon, int gemIndex, int gemTier, int totalGems, Action onFinished)
    {
        this.onComplete = onFinished;
        gameObject.SetActive(true);

        // 设置武器图标
        if (weaponIcon != null && weapon.weaponIcon != null)
        {
            weaponIcon.sprite = weapon.weaponIcon;
        }

        // 初始化插槽显示状态
        RefreshSlots(weapon, totalGems - 1, gemTier); // 显示镶嵌前的状态

        // 淡入
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.2f).SetUpdate(true).OnComplete(() =>
        {
            // 播放宝石飞入动画
            PlayGemFlyAnimation(gemIndex, gemTier, () =>
            {
                // 等待一会儿后淡出
                DOVirtual.DelayedCall(showDuration, () =>
                {
                    canvasGroup.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
                    {
                        gameObject.SetActive(false);
                        onComplete?.Invoke();
                    });
                }, false).SetUpdate(true);
            });
        });
    }

    /// <summary>
    /// 刷新所有插槽的显示（根据当前宝石数）
    /// </summary>
    private void RefreshSlots(WeaponStatBlock weapon, int gemsBeforeThis, int currentTier)
    {
        for (int i = 0; i < gemSlots.Length; i++)
        {
            if (gemSlots[i] == null) continue;

            // 计算这个插槽应该显示什么
            int slotGemCount = gemsBeforeThis; // 镶嵌前的总数
            int filledInCurrentTier = slotGemCount % UpgradeManager.GEM_SLOT_COUNT;

            if (i < filledInCurrentTier)
            {
                // 已填充的插槽
                Sprite gemSprite = currentTier > 0 ? gemSpriteTier1 : gemSpriteTier0;
                gemSlots[i].sprite = gemSprite;
                gemSlots[i].color = Color.white;
            }
            else
            {
                // 空插槽
                if (emptySlotSprite != null)
                    gemSlots[i].sprite = emptySlotSprite;
                gemSlots[i].color = new Color(1f, 1f, 1f, 0.3f);
            }
        }
    }

    /// <summary>
    /// 播放宝石飞入指定插槽的动画
    /// </summary>
    private void PlayGemFlyAnimation(int targetSlotIndex, int gemTier, Action onAnimDone)
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= gemSlots.Length)
        {
            onAnimDone?.Invoke();
            return;
        }

        Image targetSlot = gemSlots[targetSlotIndex];
        Sprite gemSprite = gemTier > 0 ? gemSpriteTier1 : gemSpriteTier0;

        // 如果没有飞行宝石预制件，直接设置
        if (flyingGemPrefab == null)
        {
            targetSlot.sprite = gemSprite;
            targetSlot.color = Color.white;
            targetSlot.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5).SetUpdate(true);
            onAnimDone?.Invoke();
            return;
        }

        // 创建飞行宝石
        GameObject flyingGem = Instantiate(flyingGemPrefab, transform);
        RectTransform flyRT = flyingGem.GetComponent<RectTransform>();
        Image flyImage = flyingGem.GetComponent<Image>();

        if (flyImage != null) flyImage.sprite = gemSprite;

        // 起始位置
        Vector3 startPos = flyingGemStart != null
            ? flyingGemStart.position
            : transform.position + Vector3.up * 300f;
        flyRT.position = startPos;
        flyRT.localScale = Vector3.one * 0.5f;

        // 目标位置
        Vector3 targetPos = targetSlot.transform.position;

        // 飞行动画序列
        DG.Tweening.Sequence flySeq = DOTween.Sequence();

        // 1. 宝石放大 + 飞向插槽
        flySeq.Append(flyRT.DOMove(targetPos, flyDuration).SetEase(Ease.InBack));
        flySeq.Join(flyRT.DOScale(1f, flyDuration).SetEase(Ease.OutQuad));

        // 2. 到达后：销毁飞行宝石，点亮插槽
        flySeq.AppendCallback(() =>
        {
            Destroy(flyingGem);
            targetSlot.sprite = gemSprite;
            targetSlot.color = Color.white;
        });

        // 3. 插槽弹跳缩放
        flySeq.Append(targetSlot.transform.DOScale(bounceScale, 0.15f).SetEase(Ease.OutQuad));
        flySeq.Append(targetSlot.transform.DOScale(1f, 0.15f).SetEase(Ease.InOutBounce));

        // 4. 发光特效
        if (glowEffect != null)
        {
            glowEffect.transform.position = targetPos;
            glowEffect.gameObject.SetActive(true);
            glowEffect.color = new Color(1f, 0.9f, 0.4f, 0f);
            flySeq.Append(glowEffect.DOFade(0.8f, glowDuration * 0.5f));
            flySeq.Append(glowEffect.DOFade(0f, glowDuration * 0.5f));
            flySeq.AppendCallback(() => glowEffect.gameObject.SetActive(false));
        }

        flySeq.SetUpdate(true);
        flySeq.OnComplete(() => onAnimDone?.Invoke());
    }
}
