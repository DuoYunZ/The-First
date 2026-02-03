using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class WeaponStatItemUI : MonoBehaviour
{
    [Header("UI 组件引用")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI damageText;
    public Image damageBarFill;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetupBaseInfo(Sprite icon, string name, int level)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = (icon != null);
            iconImage.transform.localScale = Vector3.zero; // 初始缩小，准备做弹出动画
        }

        nameText.text = $"{name} <size=80%>Lv.{level}</size>";
        nameText.alpha = 0f; // 初始隐形

        damageBarFill.fillAmount = 0f;
        damageText.text = ""; // 初始为空
        canvasGroup.alpha = 1f; // 整体可见，但内部组件隐藏
    }

    /// <summary>
    /// 播放完整的单条动画
    /// </summary>
    /// <returns>返回动画持续时间，供外部等待</returns>
    public float GetAnimationDuration()
    {
        // 图标(0.2s) + 名字(0.1s) + 进度条(0.5s)
        return 0.2f + 0.1f + 0.5f;
    }

    public void PlayAnimation(int targetDamage, float targetPercent)
    {
        StartCoroutine(SequenceRoutine(targetDamage, targetPercent));
    }

    private IEnumerator SequenceRoutine(int targetDamage, float targetPercent)
    {
        // 1. 图标弹出 (Scale 0 -> 1.2 -> 1.0)
        if (iconImage != null)
        {
            float timer = 0f;
            while (timer < 0.2f)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / 0.2f;
                // 简单的弹性曲线
                float scale = Mathf.Sin(t * Mathf.PI) * 0.2f + 1f;
                if (t < 1) iconImage.transform.localScale = Vector3.one * Mathf.Lerp(0f, 1.2f, t);
                else iconImage.transform.localScale = Vector3.one;
                yield return null;
            }
            iconImage.transform.localScale = Vector3.one;
        }

        // 2. 名字淡入
        float nameTimer = 0f;
        while (nameTimer < 0.15f)
        {
            nameTimer += Time.unscaledDeltaTime;
            nameText.alpha = Mathf.Lerp(0f, 1f, nameTimer / 0.15f);
            yield return null;
        }
        nameText.alpha = 1f;

        // 3. 进度条增长 + 数字滚动
        float barDuration = 0.5f; // 进度条时间
        float barTimer = 0f;
        while (barTimer < barDuration)
        {
            barTimer += Time.unscaledDeltaTime;
            float progress = barTimer / barDuration;
            float ease = 1f - Mathf.Pow(1f - progress, 3); // EaseOut

            damageBarFill.fillAmount = Mathf.Lerp(0f, targetPercent, ease);
            int currentDmg = Mathf.RoundToInt(Mathf.Lerp(0, targetDamage, ease));
            damageText.text = $"伤害: {currentDmg:N0}";

            yield return null;
        }

        // 最终定格
        damageBarFill.fillAmount = targetPercent;
        damageText.text = $"伤害: {targetDamage:N0}";
    }
}