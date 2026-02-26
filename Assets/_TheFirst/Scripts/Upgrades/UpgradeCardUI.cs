using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using System.Text;
using System.Linq;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("核心UI组件")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("数值颜色配置")]
    public Color betterStatColor = Color.green;
    public Color worseStatColor = Color.red;

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
        nameText.text = sourceNode.skillName;

        // --- 核心：生成预览文本 ---
        string finalDesc = displayedOption.description;
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
        if (animator != null) animator.SetTrigger("Select");

        DG.Tweening.Sequence clickSequence = DOTween.Sequence();
        clickSequence.AppendInterval(0.1f);
        clickSequence.Append(rectTransform.DOAnchorPosY(initialAnchorPos.y + 200f, 0.4f).SetEase(Ease.InBack));
        clickSequence.Join(canvasGroup.DOFade(0f, 0.3f).SetDelay(0.1f));
        clickSequence.SetUpdate(true);
        clickSequence.OnComplete(() => {
            // 如果是分支选择模式，调用分支回调
            if (onBranchSelected != null)
            {
                onBranchSelected.Invoke();
            }
            else if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnUpgradeOptionSelected(sourceNode, displayedOption);
            }
        });
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

                return FormatChange("攻击", curDmg, futDmg);

            case UpgradeType.WeaponFireRate:
                // 【核心修复】读取局部冷却缩减
                float currentLocalRate = (activePart != null) ? activePart.localFireRateBonus : 0f;

                // 冷却计算：(全局 - 局部)
                float curCoolMultiplier = Mathf.Max(0.1f, stats.fireRateMultiplier - currentLocalRate);
                float futCoolMultiplier = Mathf.Max(0.1f, stats.fireRateMultiplier - currentLocalRate - addPercent); // 注意这里通常是减法(缩减)

                // 也有可能你的升级卡配置是增加攻速，如果是那样逻辑反过来。假设是冷却缩减：
                float curCool = (1f / weapon.baseFireRate) * curCoolMultiplier;
                float futCool = (1f / weapon.baseFireRate) * futCoolMultiplier;

                if (Mathf.Abs(curCool - futCool) > 0.01f) return FormatChange("冷却", curCool, futCool, true, "s");
                break;

            case UpgradeType.AoeRadius:
                float currentLocalArea = (activePart != null) ? activePart.localAreaBonus : 0f;

                float curRad = weapon.baseAoeRadius * (stats.aoeRadiusMultiplier + currentLocalArea);
                float futRad = weapon.baseAoeRadius * (stats.aoeRadiusMultiplier + currentLocalArea + addPercent);

                if (Mathf.Abs(curRad - futRad) > 0.01f) return FormatChange("范围", curRad, futRad, false, "m");
                break;

            case UpgradeType.OrbitalSpeed:
            case UpgradeType.WeaponProjectileSpeed:
                float baseSpd = weapon.baseOrbitalSpeed > 0 ? weapon.baseOrbitalSpeed : weapon.baseLaunchForce;
                float currentLocalSpeed = (activePart != null) ? activePart.localSpeedBonus : 0f;

                float curSpdVal = baseSpd * (1f + currentLocalSpeed);
                float futSpdVal = baseSpd * (1f + currentLocalSpeed + addPercent);

                string unit = (effect.statToModify == UpgradeType.OrbitalSpeed) ? "°/s" : "m/s";
                return FormatChange("速度", curSpdVal, futSpdVal, false, unit);

            case UpgradeType.WeaponDuration:
                float baseDur = GetBaseDuration(weapon);
                float currentLocalDur = (activePart != null) ? activePart.localDurationBonus : 0f;

                float curDur = baseDur * (stats.durationMultiplier + currentLocalDur);
                float futDur = baseDur * (stats.durationMultiplier + currentLocalDur + addPercent);
                if (effect.modType == ModifierType.Flat) futDur = curDur + addFlat;

                if (Mathf.Abs(curDur - futDur) > 0.01f || (baseDur == 0 && futDur > 0))
                {
                    return FormatChange("持续", curDur, futDur, false, "s");
                }
                break;

            case UpgradeType.CritRate:
                float currentLocalCrit = (activePart != null) ? activePart.localCritRateBonus : 0f;
                float totalCritRate = stats.critRate + weapon.baseCritRate + currentLocalCrit;

                float curCrit = totalCritRate * 100f;
                float futCrit = curCrit + effect.value;
                return FormatChange("暴击率", curCrit, futCrit, false, "%");

            case UpgradeType.CritDamage:
                float currentLocalCritDmg = (activePart != null) ? activePart.localCritDamageBonus : 0f;
                float totalCritDmg = stats.critDamage + weapon.baseCritDamage + currentLocalCritDmg;

                float curCD = totalCritDmg * 100f;
                float futCD = curCD + effect.value;
                return FormatChange("暴伤", curCD, futCD, false, "%");

            case UpgradeType.PierceCount:
                int curP = weapon.basePierceCount + stats.bonusPierceCount;
                int futP = curP + Mathf.RoundToInt(effect.value);
                if (curP != futP) return FormatChange("穿透", curP, futP);
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
                return FormatChange("数量", curCountVal, futCountVal);
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
                return FormatChange("护甲", stats.armor, stats.armor + val);

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
                return FormatChange("生命上限", currentHP, currentHP + (int)val);
            case UpgradeType.Revival:
                return FormatChange("复活次数", stats.revivalCount, stats.revivalCount + (int)val);

            // --- 百分比显示类 (10% -> 20%) ---
            case UpgradeType.WeaponDamage: // 对应被动：菠菜 (Might)
                float curDmg = (stats.damageMultiplier - 1f) * 100f;
                float nextDmg = curDmg + val;
                return FormatChange("伤害增幅", curDmg, nextDmg, false, "%");

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

                return FormatChange("冷却时间", curRate, nextRate, true, "%");

            case UpgradeType.AoeRadius: // 对应被动：烛台 (Area)
                float curArea = (stats.aoeRadiusMultiplier - 1f) * 100f;
                float nextArea = curArea + val;
                return FormatChange("攻击范围", curArea, nextArea, false, "%");

            case UpgradeType.MoveSpeed:
                float curSpd = (stats.moveSpeedMultiplier - 1f) * 100f;
                return FormatChange("移速", curSpd, curSpd + val, false, "%");

            case UpgradeType.CritRate:
                float curCrit = stats.critRate * 100f;
                return FormatChange("暴击率", curCrit, curCrit + val, false, "%");

            case UpgradeType.CritDamage:
                float curCD = stats.critDamage * 100f;
                return FormatChange("暴伤", curCD, curCD + val, false, "%");

            case UpgradeType.WeaponDuration:
                float curDurMul = (stats.durationMultiplier - 1f) * 100f; // 修正：显示增幅部分
                return FormatChange("持续时间", curDurMul, curDurMul + val, false, "%");

            // --- 小数/数值显示类 ---
            case UpgradeType.PickupRadius:
                // 拾取范围：显示 3.0 -> 3.3
                float basePick = 3.0f; // 假设基础拾取范围
                float curPick = basePick * stats.pickupRadiusMultiplier;
                // 假设 val 是百分比增幅 (如10代表10%)，则 next = base * (mult + 0.1)
                float nextPick = basePick * (stats.pickupRadiusMultiplier + (effect.modType == ModifierType.Percentage ? val / 100f : 0f));
                // 如果是直接加数值，则 nextPick = curPick + val; (看你配置习惯，这里按百分比处理)

                return FormatChange("拾取范围", curPick, nextPick, false, "m");

            case UpgradeType.Luck:
                return FormatChange("幸运值", stats.luck, stats.luck + (effect.modType == ModifierType.Percentage ? val / 100f : val));
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
}