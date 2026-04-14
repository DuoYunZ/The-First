using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using System.Text;
using System.Linq;
using System;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("核心UI组件")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("数值颜色配置")]
    public Color betterStatColor = Color.green;
    public Color worseStatColor = Color.red;

    [Header("宝石插槽显示")]
    [Tooltip("5个宝石插槽Image（卡片底部，需要在预制件中创建）")]
    public Image[] gemSlotImages;
    [Tooltip("空插槽Sprite")]
    public Sprite emptyGemSprite;
    [Tooltip("已填充宝石Sprite（第一轮）")]
    public Sprite filledGemSprite;
    [Tooltip("已填充宝石Sprite（第二轮）")]
    public Sprite filledGemSpriteTier1;

    [Header("大招宝石（图标下方的红宝石位置）")]
    [Tooltip("大招宝石插槽Image（卡片顶部图标下方）")]
    public Image ultimateGemSlot;
    [Tooltip("大招宝石Sprite（已解锁状态）")]
    public Sprite ultimateGemSprite;

    [Header("宝石飞入动画")]
    [Tooltip("飞行宝石预制件（一个带Image的RectTransform）")]
    public GameObject flyingGemPrefab;
    [Tooltip("飞行宝石起始位置偏移（相对于卡片中心）")]
    public Vector2 flyingGemStartOffset = new Vector2(0, 400f);
    [Tooltip("宝石飞行时间")]
    public float gemFlyDuration = 0.45f;
    [Tooltip("宝石镶嵌后的展示停留时间")]
    public float gemShowDelay = 0.0f;
    [Tooltip("插槽发光Image（可选）")]
    public Image gemGlowEffect;

    // 内部变量
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 initialScale;
    private Vector2 initialAnchorPos;
    private SkillTreeNodeData sourceNode;
    private UpgradeOption displayedOption;
    private bool isSelected = false;
    private Animator animator;

    // 分支选择回调（如果设置，则点击时调用此回调而非UpgradeManager）
    private System.Action onBranchSelected;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        animator = GetComponent<Animator>();
    }

    public void Setup(SkillTreeNodeData node, UpgradeOption option)
    {
        // 确保组件已初始化（防止Awake未执行的情况）
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (animator == null) animator = GetComponent<Animator>();

        this.sourceNode = node;
        this.displayedOption = option;
        this.isSelected = false;

        if (displayedOption == null)
        {
            gameObject.SetActive(false);
            return;
        }

        iconImage.sprite = sourceNode.skillIcon;
        nameText.text = (sourceNode.associatedWeapon != null && !string.IsNullOrEmpty(sourceNode.associatedWeapon.weaponID))
            ? LocalizationManager.T("weapon." + sourceNode.associatedWeapon.weaponID)
            : sourceNode.skillName;

        // --- 核心：生成预览文本 ---
        string finalDesc = displayedOption.LocalizedDescription;
        string previewText = GeneratePreviewText(node, option);
        if (!string.IsNullOrEmpty(previewText))
        {
            finalDesc += "\n\n" + previewText;
        }
        descriptionText.text = finalDesc;
        // -----------------------

        // 强制刷新 UI 布局并记录正确坐标（修复飞卡 Bug 的关键）
        Canvas.ForceUpdateCanvases();
        initialScale = transform.localScale;
        initialAnchorPos = rectTransform.anchoredPosition;

        // 刷新宝石插槽显示
        RefreshGemSlots();
    }

    // 分支选择专用的Setup重载
    public void SetupForBranch(SkillTreeNodeData node, UpgradeOption option, System.Action onSelected)
    {
        Setup(node, option);
        this.onBranchSelected = onSelected;
    }

    // 刷新初始位置（用于布局完成后重新记录）
    public void RefreshInitialPosition()
    {
        if (rectTransform != null)
        {
            initialScale = transform.localScale;
            initialAnchorPos = rectTransform.anchoredPosition;
        }
    }

    // --- 动画逻辑 (保持不变) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOKill();
        transform.DOKill();
        rectTransform.DOAnchorPosY(initialAnchorPos.y + 50f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale * 1.05f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;
        rectTransform.DOKill();
        transform.DOKill();
        rectTransform.DOAnchorPosY(initialAnchorPos.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(initialScale, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSelected) return;
        isSelected = true;

        rectTransform.DOKill();
        transform.DOKill();

        // 【关键】禁用Animator，防止和DOTween抢占Transform控制权
        if (animator != null) animator.enabled = false;

        // 【关键】禁用父容器的布局组件，防止其他卡片消失后剩余卡片跳位
        Transform container = transform.parent;
        if (container != null)
        {
            var layoutGroup = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (layoutGroup != null) layoutGroup.enabled = false;
            var contentFitter = container.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (contentFitter != null) contentFitter.enabled = false;
        }

        // 通知其他卡片淡出消失
        NotifyOtherCardsDismiss();

        // 判断是否需要播放宝石飞入动画（武器相关的卡片才播放）
        bool isWeaponCard = sourceNode != null && sourceNode.associatedWeapon != null;
        bool hasGemSlots = (gemSlotImages != null && gemSlotImages.Length > 0) || ultimateGemSlot != null;

        if (isWeaponCard && hasGemSlots && UpgradeManager.Instance != null)
        {
            // 先播放宝石镶嵌动画，再消失
            PlayGemEmbedThenDismiss();
        }
        else
        {
            // 非武器卡 / 没有宝石插槽：直接消失
            PlayDismissAnimation();
        }
    }

    /// <summary>
    /// 通知同一批次的其他卡片淡出消失
    /// </summary>
    private void NotifyOtherCardsDismiss()
    {
        if (UpgradeManager.Instance == null) return;
        // 获取同一容器下的所有卡片
        Transform container = transform.parent;
        if (container == null) return;

        foreach (Transform child in container)
        {
            if (child == this.transform) continue; // 跳过自己
            var otherCard = child.GetComponent<UpgradeCardUI>();
            if (otherCard != null && !otherCard.isSelected)
            {
                otherCard.FadeOutPassive();
            }
        }
    }

    /// <summary>
    /// 被动淡出（被其他卡片选中时调用）
    /// </summary>
    public void FadeOutPassive()
    {
        isSelected = true; // 防止再次点击
        rectTransform.DOKill();
        transform.DOKill();

        // 【关键】禁用Animator，防止和DOTween抢占Transform控制权
        if (animator != null) animator.enabled = false;

        // 原地缩小+淡出
        DG.Tweening.Sequence fadeSeq = DOTween.Sequence();
        fadeSeq.Append(transform.DOScale(initialScale * 0.8f, 0.25f).SetEase(Ease.InBack));
        fadeSeq.Join(canvasGroup.DOFade(0f, 0.25f));
        fadeSeq.SetUpdate(true);
        fadeSeq.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// 播放宝石飞入插槽动画，完成后再消失
    /// 大招卡：飞入顶部红宝石位置
    /// 普通武器卡：飞入底部技能宝石位置
    /// </summary>
    private void PlayGemEmbedThenDismiss()
    {
        // 判断是否是大招解锁卡
        bool isUltimateCard = displayedOption != null &&
            displayedOption.effects != null &&
            displayedOption.effects.Exists(e => e.actionType == EffectActionType.UnlockUltimate);

        if (isUltimateCard)
        {
            // 大招卡：宝石飞入顶部红宝石位置
            PlayUltimateGemEmbed();
        }
        else
        {
            // 普通武器卡：宝石飞入底部技能插槽
            PlaySkillGemEmbed();
        }
    }

    /// <summary>
    /// 大招卡的宝石飞入动画（飞入顶部红宝石位置）
    /// </summary>
    private void PlayUltimateGemEmbed()
    {
        if (ultimateGemSlot == null || ultimateGemSprite == null)
        {
            PlayDismissAnimation();
            return;
        }

        Image targetSlot = ultimateGemSlot;
        Sprite gemSprite = ultimateGemSprite;

        // 激活并设为透明
        targetSlot.gameObject.SetActive(true);
        targetSlot.sprite = gemSprite;
        targetSlot.color = new Color(1f, 1f, 1f, 0f);

        // 播放飞入动画
        PlayGemFlyAnimation(targetSlot, gemSprite);
    }

    /// <summary>
    /// 普通武器卡的宝石飞入动画（飞入底部技能插槽）
    /// </summary>
    private void PlaySkillGemEmbed()
    {
        // 计算宝石信息
        int totalGemsBefore = UpgradeManager.Instance.GetGemCountForWeapon(sourceNode.associatedWeapon);
        int gemIndex = totalGemsBefore % UpgradeManager.GEM_SLOT_COUNT;
        int gemTier = totalGemsBefore / UpgradeManager.GEM_SLOT_COUNT;

        // 确保目标插槽有效
        if (gemIndex < 0 || gemIndex >= gemSlotImages.Length || gemSlotImages[gemIndex] == null)
        {
            PlayDismissAnimation();
            return;
        }

        Image targetSlot = gemSlotImages[gemIndex];
        Sprite gemSprite = gemTier > 0 ? filledGemSpriteTier1 : filledGemSprite;
        if (gemSprite == null)
        {
            PlayDismissAnimation();
            return;
        }

        // 激活目标插槽并设为透明
        targetSlot.gameObject.SetActive(true);
        targetSlot.sprite = gemSprite;
        targetSlot.color = new Color(1f, 1f, 1f, 0f);

        // 播放飞入动画
        PlayGemFlyAnimation(targetSlot, gemSprite);
    }

    /// <summary>
    /// 统一的宝石飞入动画（从卡片上方飞到目标插槽）
    /// </summary>
    private void PlayGemFlyAnimation(Image targetSlot, Sprite gemSprite)
    {
        if (flyingGemPrefab != null)
        {
            // 在卡片内创建飞行宝石
            GameObject flyingGem = Instantiate(flyingGemPrefab, transform);
            RectTransform flyRT = flyingGem.GetComponent<RectTransform>();
            Image flyImage = flyingGem.GetComponent<Image>();
            if (flyImage != null) flyImage.sprite = gemSprite;

            // 起始位置：卡片上方
            flyRT.anchoredPosition = flyingGemStartOffset;
            flyRT.localScale = Vector3.one * 0.5f;

            // 目标位置
            Vector3 targetPos = targetSlot.transform.position;

            // 全部动画在同一个Sequence中，消除帧间延迟
            DG.Tweening.Sequence seq = DOTween.Sequence();

            // 1. 宝石飞向插槽
            seq.Append(flyRT.DOMove(targetPos, gemFlyDuration).SetEase(Ease.InBack));
            seq.Join(flyRT.DOScale(1f, gemFlyDuration).SetEase(Ease.OutQuad));

            // 2. 到达后：点亮插槽，销毁飞行宝石
            seq.AppendCallback(() =>
            {
                Destroy(flyingGem);
                targetSlot.sprite = gemSprite;
                targetSlot.color = Color.white;
            });

            // 3. 插槽弹跳缩放
            seq.Append(targetSlot.transform.DOScale(1.4f, 0.1f).SetEase(Ease.OutQuad));
            seq.Append(targetSlot.transform.DOScale(1f, 0.1f).SetEase(Ease.InOutBounce));

            // 4. 发光特效（可选）
            if (gemGlowEffect != null)
            {
                gemGlowEffect.transform.position = targetPos;
                gemGlowEffect.gameObject.SetActive(true);
                gemGlowEffect.color = new Color(1f, 0.9f, 0.4f, 0f);
                seq.Append(gemGlowEffect.DOFade(0.8f, 0.1f));
                seq.Append(gemGlowEffect.DOFade(0f, 0.1f));
                seq.AppendCallback(() => gemGlowEffect.gameObject.SetActive(false));
            }

            // 5. 可选停留
            if (gemShowDelay > 0f)
                seq.AppendInterval(gemShowDelay);

            // 6. 卡片缩小淡出（在同一Sequence中，无帧间延迟）
            seq.Append(transform.DOScale(initialScale * 0.7f, 0.25f).SetEase(Ease.InBack));
            seq.Join(canvasGroup.DOFade(0f, 0.25f));

            // 7. 完成后通知
            seq.OnComplete(() =>
            {
                RestoreLayoutGroup();
                if (onBranchSelected != null)
                    onBranchSelected.Invoke();
                else if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.OnUpgradeOptionSelected(sourceNode, displayedOption);
            });

            seq.SetUpdate(true);
        }
        else
        {
            // 没有飞行宝石预制件：直接点亮插槽然后消失
            targetSlot.sprite = gemSprite;
            targetSlot.color = Color.white;
            PlayDismissAnimation();
        }
    }

    /// <summary>
    /// 卡片消失动画（非宝石路径使用）
    /// </summary>
    private void PlayDismissAnimation()
    {
        DG.Tweening.Sequence clickSequence = DOTween.Sequence();
        clickSequence.Append(transform.DOScale(initialScale * 0.7f, 0.2f).SetEase(Ease.InQuad));
        clickSequence.Join(canvasGroup.DOFade(0f, 0.2f));
        clickSequence.SetUpdate(true);
        clickSequence.OnComplete(() => {
            RestoreLayoutGroup();
            if (onBranchSelected != null)
                onBranchSelected.Invoke();
            else if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradeOptionSelected(sourceNode, displayedOption);
        });
    }

    /// <summary>
    /// 恢复父容器的布局组件
    /// </summary>
    private void RestoreLayoutGroup()
    {
        Transform container = transform.parent;
        if (container == null) return;
        var layoutGroup = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = true;
        var contentFitter = container.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (contentFitter != null) contentFitter.enabled = true;
    }

    public void Show() { if (animator != null) animator.SetTrigger("Show"); }

    // ========================================================================================
    //                                  【新增】数值预览核心逻辑
    // ========================================================================================
    #region 数值预览计算

    private string GeneratePreviewText(SkillTreeNodeData node, UpgradeOption option)
    {
        if (WeaponController.Instance == null || PlayerStats.Instance == null) return "";
        StringBuilder sb = new StringBuilder();

        WeaponStatBlock targetStatBlock = node.associatedWeapon;

        // 1. 如果是特定武器升级 (且玩家已拥有该武器)
        if (targetStatBlock != null)
        {
            var ownedWeapon = WeaponController.Instance.ownedWeapons.FirstOrDefault(w => w.stats == targetStatBlock);
            if (ownedWeapon != null)
            {
                foreach (var effect in option.effects)
                {
                    string changeLine = SimulateEffectOnWeapon(targetStatBlock, effect);
                    if (!string.IsNullOrEmpty(changeLine)) sb.AppendLine(changeLine);
                }
            }
            // 武器解锁预览
            /*else if (option.effects.Any(e => e.actionType == EffectActionType.UnlockWeapon))
            {
                sb.AppendLine($"<color=#FFFF00>基础面板:</color>");
                // 智能显示伤害（区分AOE/直伤/DPS）
                int dmg = targetStatBlock.baseDirectDamage;
                if (dmg == 0) dmg = targetStatBlock.baseAoeDamage;
                if (targetStatBlock.behavior == WeaponBehaviorType.Beam) dmg = targetStatBlock.beamDamagePerSecond;

                sb.AppendLine($"伤害: {dmg}");
                sb.AppendLine($"冷却: {1f / targetStatBlock.baseFireRate:F1}s");
            }*/
        }
        // 2. 如果是全局属性升级 (通用被动)
        else
        {
            foreach (var effect in option.effects)
            {
                string changeLine = SimulateGlobalStat(effect);
                if (!string.IsNullOrEmpty(changeLine)) sb.AppendLine(changeLine);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 模拟【武器专属】属性变化 (例如：烈焰剑伤害 25 -> 30)
    /// </summary>
    private string SimulateEffectOnWeapon(WeaponStatBlock weapon, UpgradeEffect effect)
    {
        if (effect.actionType != EffectActionType.ModifyStat) return "";
        PlayerStats stats = PlayerStats.Instance;

        // 1. 获取当前武器实例 (为了读取局部变量)
        WeaponPart activePart = null;
        if (WeaponController.Instance != null)
        {
            var owned = WeaponController.Instance.ownedWeapons.FirstOrDefault(w => w.stats == weapon);
            if (owned != null) activePart = owned.weaponPartInstance;
        }

        // 获取增量
        float addPercent = (effect.modType == ModifierType.Percentage) ? (effect.value / 100f) : 0f;
        float addFlat = (effect.modType == ModifierType.Flat) ? effect.value : 0f;

        switch (effect.statToModify)
        {
            case UpgradeType.WeaponDamage:
                // 智能判断基础伤害
                int baseDmg = weapon.baseDirectDamage;
                if (weapon.behavior == WeaponBehaviorType.MeleeAOE ||
                    weapon.behavior == WeaponBehaviorType.ParabolicAOE ||
                    weapon.behavior == WeaponBehaviorType.Landmine ||
                    weapon.behavior == WeaponBehaviorType.Orbital)
                {
                    baseDmg = weapon.baseAoeDamage > 0 ? weapon.baseAoeDamage : baseDmg;
                }
                else if (weapon.behavior == WeaponBehaviorType.Beam) baseDmg = weapon.beamDamagePerSecond;
                else if (weapon.behavior == WeaponBehaviorType.PersistentAOE) baseDmg = weapon.baseAreaDamagePerTick;

                // 【核心修复】读取当前已有的局部加成
                float currentLocalDmg = (activePart != null) ? activePart.localDamageBonus : 0f;

                // 当前 = 基础 * (全局 + 局部)
                int curDmg = Mathf.RoundToInt(baseDmg * (stats.damageMultiplier + currentLocalDmg) + stats.flatDamageBonus);

                // 未来 = 基础 * (全局 + 局部 + 新增)
                int futDmg = Mathf.RoundToInt(baseDmg * (stats.damageMultiplier + currentLocalDmg + addPercent) + stats.flatDamageBonus);

                return FormatChange(LocalizationManager.T("stat.attack"), curDmg, futDmg);

            case UpgradeType.WeaponFireRate:
                // 【核心修复】读取局部冷却缩减
                float currentLocalRate = (activePart != null) ? activePart.localFireRateBonus : 0f;

                // 冷却计算：(全局 - 局部)
                float curCoolMultiplier = Mathf.Max(0.1f, stats.fireRateMultiplier - currentLocalRate);
                float futCoolMultiplier = Mathf.Max(0.1f, stats.fireRateMultiplier - currentLocalRate - addPercent); // 注意这里通常是减法(缩减)

                // 也有可能你的升级卡配置是增加攻速，如果是那样逻辑反过来。假设是冷却缩减：
                float curCool = (1f / weapon.baseFireRate) * curCoolMultiplier;
                float futCool = (1f / weapon.baseFireRate) * futCoolMultiplier;

                if (Mathf.Abs(curCool - futCool) > 0.01f) return FormatChange(LocalizationManager.T("stat.cooldown"), curCool, futCool, true, "s");
                break;

            case UpgradeType.AoeRadius:
                float currentLocalArea = (activePart != null) ? activePart.localAreaBonus : 0f;

                float curRad = weapon.baseAoeRadius * (stats.aoeRadiusMultiplier + currentLocalArea);
                float futRad = weapon.baseAoeRadius * (stats.aoeRadiusMultiplier + currentLocalArea + addPercent);

                if (Mathf.Abs(curRad - futRad) > 0.01f) return FormatChange(LocalizationManager.T("stat.range"), curRad, futRad, false, "m");
                break;

            case UpgradeType.OrbitalSpeed:
            case UpgradeType.WeaponProjectileSpeed:
                float baseSpd = weapon.baseOrbitalSpeed > 0 ? weapon.baseOrbitalSpeed : weapon.baseLaunchForce;
                float currentLocalSpeed = (activePart != null) ? activePart.localSpeedBonus : 0f;

                float curSpdVal = baseSpd * (1f + currentLocalSpeed);
                float futSpdVal = baseSpd * (1f + currentLocalSpeed + addPercent);

                string unit = (effect.statToModify == UpgradeType.OrbitalSpeed) ? "°/s" : "m/s";
                return FormatChange(LocalizationManager.T("stat.speed"), curSpdVal, futSpdVal, false, unit);

            case UpgradeType.WeaponDuration:
                float baseDur = GetBaseDuration(weapon);
                float currentLocalDur = (activePart != null) ? activePart.localDurationBonus : 0f;

                float curDur = baseDur * (stats.durationMultiplier + currentLocalDur);
                float futDur = baseDur * (stats.durationMultiplier + currentLocalDur + addPercent);
                if (effect.modType == ModifierType.Flat) futDur = curDur + addFlat;

                if (Mathf.Abs(curDur - futDur) > 0.01f || (baseDur == 0 && futDur > 0))
                {
                    return FormatChange(LocalizationManager.T("stat.duration"), curDur, futDur, false, "s");
                }
                break;

            case UpgradeType.CritRate:
                float currentLocalCrit = (activePart != null) ? activePart.localCritRateBonus : 0f;
                float totalCritRate = stats.critRate + weapon.baseCritRate + currentLocalCrit;

                float curCrit = totalCritRate * 100f;
                float futCrit = curCrit + effect.value;
                return FormatChange(LocalizationManager.T("stat.crit_rate"), curCrit, futCrit, false, "%");

            case UpgradeType.CritDamage:
                float currentLocalCritDmg = (activePart != null) ? activePart.localCritDamageBonus : 0f;
                float totalCritDmg = stats.critDamage + weapon.baseCritDamage + currentLocalCritDmg;

                float curCD = totalCritDmg * 100f;
                float futCD = curCD + effect.value;
                return FormatChange(LocalizationManager.T("stat.crit_dmg"), curCD, futCD, false, "%");

            case UpgradeType.PierceCount:
                int curP = weapon.basePierceCount + stats.bonusPierceCount;
                int futP = curP + Mathf.RoundToInt(effect.value);
                if (curP != futP) return FormatChange(LocalizationManager.T("stat.pierce"), curP, futP);
                break;

            case UpgradeType.OrbitalCount:
            case UpgradeType.SlashCount:
            case UpgradeType.AddProjectile:
                // 读取局部数量
                int localCount = (activePart != null) ? activePart.localOrbitalCountBonus : 0;
                // 读取全局数量 (假设这些类型通常存在全局加成里，或者你需要去 specific 变量找)
                int globalCount = (effect.statToModify == UpgradeType.OrbitalCount) ? stats.bonusOrbitalCount :
                                  (effect.statToModify == UpgradeType.SlashCount) ? stats.bonusSlashCount : stats.bonusProjectileCount;

                // 基础 + 全局 + 局部
                int baseCount = (effect.statToModify == UpgradeType.SlashCount) ? weapon.multiHitCount : weapon.baseOrbitalCount;
                if (baseCount == 0 && effect.statToModify == UpgradeType.AddProjectile) baseCount = 1; // 很多发射类武器没填这个字段，默认是1

                int curCountVal = baseCount + globalCount + localCount;
                int futCountVal = curCountVal + Mathf.RoundToInt(effect.value);
                return FormatChange(LocalizationManager.T("stat.count"), curCountVal, futCountVal);
        }
        return "";
    }

    /// <summary>
    /// 模拟【全局被动】属性变化 (例如：菠菜伤害 +10%，护甲 0->1)
    /// </summary>
    private string SimulateGlobalStat(UpgradeEffect effect)
    {
        if (effect.actionType != EffectActionType.ModifyStat) return "";
        PlayerStats stats = PlayerStats.Instance;
        float val = effect.value;

        switch (effect.statToModify)
        {
            // --- 整数显示类 ---
            case UpgradeType.Armor:
                return FormatChange(LocalizationManager.T("stat.armor"), stats.armor, stats.armor + val);

            case UpgradeType.MaxHealth:
                int currentHP = 0;
                // 尝试从玩家身上获取 Health 组件 (假设你的血量脚本叫 Health 且有 maxHealth 字段)
                var healthComp = stats.GetComponent<Health>();
                if (healthComp != null)
                {
                    // 【关键】这里读取的是角色的实际总血量 (基础 + 加成)
                    currentHP = healthComp.maxHealth;
                }
                else
                {
                    // 如果找不到 Health 组件，保底显示 Bonus
                    currentHP = stats.bonusMaxHealth;
                }
                return FormatChange(LocalizationManager.T("stat.max_hp"), currentHP, currentHP + (int)val);
            case UpgradeType.Revival:
                return FormatChange(LocalizationManager.T("stat.revival"), stats.revivalCount, stats.revivalCount + (int)val);

            // --- 百分比显示类 (10% -> 20%) ---
            case UpgradeType.WeaponDamage: // 对应被动：菠菜 (Might)
                float curDmg = (stats.damageMultiplier - 1f) * 100f;
                float nextDmg = curDmg + val;
                return FormatChange(LocalizationManager.T("stat.dmg_bonus"), curDmg, nextDmg, false, "%");

            case UpgradeType.WeaponFireRate: // 对应被动：时光曲奇 (Cooldown)
                // 1. 计算当前冷却增幅 (例如 0.8 -> -20%)
                float curRate = (stats.fireRateMultiplier - 1f) * 100f;

                // 2. 处理数值格式 (兼容 0.1 和 10 两种写法)
                // 如果填的是 0.1 (10%)，显示时需要 * 100
                float effectiveVal = val;
                if (Mathf.Abs(val) <= 1f && val != 0) effectiveVal = val * 100f;

                // 3. 【关键修复】冷却缩减是减法！
                // 当前 -20%，再缩减 10%，应该是 -30%
                float nextRate = curRate - effectiveVal;

                return FormatChange(LocalizationManager.T("stat.cooldown_time"), curRate, nextRate, true, "%");

            case UpgradeType.AoeRadius: // 对应被动：烛台 (Area)
                float curArea = (stats.aoeRadiusMultiplier - 1f) * 100f;
                float nextArea = curArea + val;
                return FormatChange(LocalizationManager.T("stat.aoe_range"), curArea, nextArea, false, "%");

            case UpgradeType.MoveSpeed:
                float curSpd = (stats.moveSpeedMultiplier - 1f) * 100f;
                return FormatChange(LocalizationManager.T("stat.move_speed"), curSpd, curSpd + val, false, "%");

            case UpgradeType.CritRate:
                float curCrit = stats.critRate * 100f;
                return FormatChange(LocalizationManager.T("stat.crit_rate"), curCrit, curCrit + val, false, "%");

            case UpgradeType.CritDamage:
                float curCD = stats.critDamage * 100f;
                return FormatChange(LocalizationManager.T("stat.crit_dmg"), curCD, curCD + val, false, "%");

            case UpgradeType.WeaponDuration:
                float curDurMul = (stats.durationMultiplier - 1f) * 100f; // 修正：显示增幅部分
                return FormatChange(LocalizationManager.T("stat.duration_time"), curDurMul, curDurMul + val, false, "%");

            // --- 小数/数值显示类 ---
            case UpgradeType.PickupRadius:
                // 拾取范围：显示 3.0 -> 3.3
                float basePick = 3.0f; // 假设基础拾取范围
                float curPick = basePick * stats.pickupRadiusMultiplier;
                // 假设 val 是百分比增幅 (如10代表10%)，则 next = base * (mult + 0.1)
                float nextPick = basePick * (stats.pickupRadiusMultiplier + (effect.modType == ModifierType.Percentage ? val / 100f : 0f));
                // 如果是直接加数值，则 nextPick = curPick + val; (看你配置习惯，这里按百分比处理)

                return FormatChange(LocalizationManager.T("stat.pickup"), curPick, nextPick, false, "m");

            case UpgradeType.Luck:
                return FormatChange(LocalizationManager.T("stat.luck"), stats.luck, stats.luck + (effect.modType == ModifierType.Percentage ? val / 100f : val));

            // === 被动道具 — 触发机制型 ===
            case UpgradeType.BerserkerHeart:
                float curBerserker = stats.berserkerDamagePerLevel * stats.berserkerLevel * 100f;
                float nextBerserker = effect.value * (stats.berserkerLevel + 1) * 100f;
                return FormatChange("低血增伤", curBerserker, nextBerserker, false, "%");

            case UpgradeType.FlameTrail:
                return FormatChange("燃烧轨迹", stats.flameTrailLevel, stats.flameTrailLevel + 1);

            case UpgradeType.ThornsDamage:
                float curThorns = stats.thornsReflectPercent * 100f;
                float nextThorns = (stats.thornsReflectPercent + effect.value) * 100f;
                return FormatChange("伤害反弹", curThorns, nextThorns, false, "%");

            case UpgradeType.KillHeal:
                int curKillHeal = stats.killHealAmount;
                int nextKillHeal = curKillHeal + Mathf.RoundToInt(effect.value);
                return FormatChange("击杀回血", curKillHeal, nextKillHeal);

            case UpgradeType.GlobalFreezeChance:
                float curFreeze = stats.globalFreezeChance * 100f;
                float nextFreeze = (stats.globalFreezeChance + effect.value) * 100f;
                return FormatChange("冰冻概率", curFreeze, nextFreeze, false, "%");

            case UpgradeType.ThunderWill:
                float curThunderChance = stats.thunderWillChance * 100f;
                int curThunderLevel = Mathf.RoundToInt(stats.thunderWillChance / 0.08f);
                float nextThunderChance = (curThunderLevel + 1) * 8f; // 每级+8%
                return FormatChange("雷击概率", curThunderChance, nextThunderChance, false, "%");

            case UpgradeType.LifeStealPassive:
                float curSteal = stats.lifeStealPercent * 100f;
                float nextSteal = (stats.lifeStealPercent + effect.value) * 100f;
                return FormatChange("伤害吸血", curSteal, nextSteal, false, "%");

            case UpgradeType.DashExplosion:
                return FormatChange("冲刺余烬", stats.dashExplosionLevel, stats.dashExplosionLevel + 1);

            case UpgradeType.ExperienceGain:
                float curXP = stats.experienceGainMultiplier * 100f;
                float nextXP = (stats.experienceGainMultiplier + effect.value) * 100f;
                return FormatChange("经验获取", curXP, nextXP, false, "%");
        }
        return "";
    }

    // 辅助：智能获取武器的“基础持续时间”字段
    private float GetBaseDuration(WeaponStatBlock weapon)
    {
        if (weapon.behavior == WeaponBehaviorType.Orbital) return weapon.baseDuration;
        if (weapon.behavior == WeaponBehaviorType.SummonDrone) return weapon.summonDuration;
        if (weapon.behavior == WeaponBehaviorType.Beam) return weapon.beamDuration;
        if (weapon.behavior == WeaponBehaviorType.PersistentAOE) return weapon.baseAreaDuration;
        if (weapon.behavior == WeaponBehaviorType.Landmine) return weapon.mineDuration;
        return weapon.baseProjectileLifetime;
    }

    // 格式化：浮点数 (10.0 -> 15.0)
    private string FormatChange(string label, float current, float future, bool smallerIsBetter = false, string unit = "")
    {
        string cStr = current.ToString("0.##");
        string fStr = future.ToString("0.##");
        bool isBetter = smallerIsBetter ? (future < current) : (future > current);
        string colorHex = ColorUtility.ToHtmlStringRGB(isBetter ? betterStatColor : worseStatColor);
        return $"{label}: {cStr}{unit} -> <color=#{colorHex}>{fStr}{unit}</color>";
    }

    // 格式化：整数 (10 -> 15)
    private string FormatChange(string label, int current, int future)
    {
        bool isBetter = future > current;
        string colorHex = ColorUtility.ToHtmlStringRGB(isBetter ? betterStatColor : worseStatColor);
        return $"{label}: {current} -> <color=#{colorHex}>{future}</color>";
    }

    #endregion

    #region 宝石插槽

    /// <summary>
    /// 刷新卡片上的宝石插槽显示
    /// 根据该武器在UpgradeManager中记录的宝石数量更新UI
    /// </summary>
    private void RefreshGemSlots()
    {
        // 获取关联的武器（大招卡使用 associatedWeapon）
        WeaponStatBlock weapon = (sourceNode != null) ? sourceNode.associatedWeapon : null;

        // --- 刷新底部5个技能宝石插槽 ---
        if (gemSlotImages != null && gemSlotImages.Length > 0)
        {
            if (weapon == null)
            {
                // 非武器卡片，隐藏所有插槽
                foreach (var slot in gemSlotImages)
                {
                    if (slot != null) slot.gameObject.SetActive(false);
                }
            }
            else
            {
                // 从UpgradeManager获取当前宝石数
                int totalGems = 0;
                if (UpgradeManager.Instance != null)
                {
                    totalGems = UpgradeManager.Instance.GetGemCountForWeapon(weapon);
                }

                int filledCount = totalGems % UpgradeManager.GEM_SLOT_COUNT;
                int gemTier = totalGems / UpgradeManager.GEM_SLOT_COUNT;

                for (int i = 0; i < gemSlotImages.Length; i++)
                {
                    if (gemSlotImages[i] == null) continue;

                    if (i < filledCount)
                    {
                        // 当前轮次的新宝石（覆盖上一轮）
                        gemSlotImages[i].gameObject.SetActive(true);
                        gemSlotImages[i].sprite = gemTier > 0 ? filledGemSpriteTier1 : filledGemSprite;
                        gemSlotImages[i].color = Color.white;
                    }
                    else if (gemTier > 0)
                    {
                        // 上一轮的宝石仍然保留显示（宝石永不消耗）
                        gemSlotImages[i].gameObject.SetActive(true);
                        gemSlotImages[i].sprite = (gemTier > 1) ? filledGemSpriteTier1 : filledGemSprite;
                        gemSlotImages[i].color = Color.white;
                    }
                    else
                    {
                        // 真正的空插槽：隐藏
                        gemSlotImages[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        // --- 刷新大招宝石（顶部红宝石位置） ---
        if (ultimateGemSlot != null)
        {
            if (weapon == null)
            {
                ultimateGemSlot.gameObject.SetActive(false);
            }
            else
            {
                // 检查该武器是否已解锁大招
                bool ultimateUnlocked = false;
                if (WeaponController.Instance != null)
                {
                    var wrapper = WeaponController.Instance.ownedWeapons
                        .FirstOrDefault(w => w.stats == weapon);
                    if (wrapper != null && wrapper.weaponPartInstance != null)
                    {
                        ultimateUnlocked = wrapper.weaponPartInstance.isUltimateUnlocked;
                    }
                }

                if (ultimateUnlocked && ultimateGemSprite != null)
                {
                    // 大招已解锁：显示红宝石
                    ultimateGemSlot.gameObject.SetActive(true);
                    ultimateGemSlot.sprite = ultimateGemSprite;
                    ultimateGemSlot.color = Color.white;
                }
                else
                {
                    // 大招未解锁：隐藏
                    ultimateGemSlot.gameObject.SetActive(false);
                }
            }
        }
    }

    #endregion
}