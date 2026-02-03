using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class SettlementUI : MonoBehaviour
{
    [System.Serializable]
    public class StatUI
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI value;

        // [新增] 运行时内部缓存，不需要在Inspector设置
        [HideInInspector] public RectTransform rect;
        [HideInInspector] public Vector2 originPos;
    }

    [Header("顶部数据组")]
    public StatUI timeStat;
    public StatUI killStat;
    public StatUI goldStat;

    [Header("动画设置")]
    [Tooltip("入场动画的位移偏移量 (例如 x=-100 代表从左边飞入)")]
    public Vector2 moveOffset = new Vector2(-100f, 0f);
    [Tooltip("单个统计项的动画总时长")]
    public float statAnimDuration = 0.5f;

    [Header("通用UI")]
    public GameObject panelRoot;
    public TextMeshProUGUI mainTitleText;
    public GameObject buttonsPanel;

    [Header("武器列表")]
    public Transform weaponStatContainer;
    public GameObject weaponStatItemPrefab;

    private float finalTime;
    private int finalKills;
    private int finalGold;

    // [新增] Awake 用于缓存位置
    void Awake()
    {
        // 提前缓存 RectTransform 和 原始位置
        CacheStatPos(timeStat);
        CacheStatPos(killStat);
        CacheStatPos(goldStat);
    }

    private void CacheStatPos(StatUI stat)
    {
        if (stat.root != null)
        {
            stat.rect = stat.root.GetComponent<RectTransform>();
            if (stat.rect != null)
            {
                stat.originPos = stat.rect.anchoredPosition;
            }
        }
    }

    public void Show(bool isVictory)
    {
        panelRoot.SetActive(true);
        Time.timeScale = 0f;

        mainTitleText.text = isVictory ? "任务完成" : "任务失败";
        mainTitleText.color = isVictory ? Color.yellow : Color.white;

        if (buttonsPanel) buttonsPanel.SetActive(false);

        // 1. 重置状态 (位置挪到偏移点，透明度归零)
        ResetStatUI(timeStat);
        ResetStatUI(killStat);
        ResetStatUI(goldStat);

        var stats = BattleStatisticsManager.Instance;
        if (stats != null)
        {
            finalTime = stats.GetSurvivalTime();
            finalKills = stats.TotalKills;
            finalGold = stats.TotalGoldEarned;
            StartCoroutine(FullSequence(stats.WeaponDamageStats));
        }
    }

    private void ResetStatUI(StatUI stat)
    {
        if (stat.root)
        {
            stat.root.SetActive(false);
            // [核心] 设置到偏移位置
            if (stat.rect != null)
            {
                stat.rect.anchoredPosition = stat.originPos + moveOffset;
            }
        }
        if (stat.icon) stat.icon.transform.localScale = Vector3.zero;
        if (stat.title) stat.title.alpha = 0f;
        if (stat.value) stat.value.text = "";
    }

    private IEnumerator FullSequence(Dictionary<string, int> damageStats)
    {
        // --- 阶段 A: 依次播放顶部三个数据 ---
        yield return StartCoroutine(AnimateSingleStat(timeStat, finalTime, true));
        yield return StartCoroutine(AnimateSingleStat(killStat, finalKills, false));
        yield return StartCoroutine(AnimateSingleStat(goldStat, finalGold, false));

        yield return new WaitForSecondsRealtime(0.2f);

        // --- 阶段 B: 武器列表 (保持之前的逻辑) ---
        foreach (Transform child in weaponStatContainer) Destroy(child.gameObject);

        int totalDamage = 0;
        foreach (var v in damageStats.Values) totalDamage += v;
        if (totalDamage == 0) totalDamage = 1;

        var sortedStats = damageStats.OrderByDescending(x => x.Value);
        var controller = WeaponController.Instance;

        foreach (var kvp in sortedStats)
        {
            GameObject item = Instantiate(weaponStatItemPrefab, weaponStatContainer);
            item.transform.localScale = Vector3.one;
            WeaponStatItemUI itemScript = item.GetComponent<WeaponStatItemUI>();

            Sprite icon = null;
            int level = 1;
            if (controller != null)
            {
                if (controller.builtInBladeWeapon?.StatBlock?.weaponName == kvp.Key)
                {
                    icon = controller.builtInBladeWeapon.StatBlock.weaponIcon;
                    level = 1 + (PlayerStats.Instance != null ? PlayerStats.Instance.bonusSlashCount : 0);
                }
                else
                {
                    foreach (var owned in controller.ownedWeapons)
                    {
                        if (owned.stats?.weaponName == kvp.Key)
                        {
                            icon = owned.stats.weaponIcon;
                            level = owned.currentLevel;
                            break;
                        }
                    }
                }
            }

            itemScript.SetupBaseInfo(icon, kvp.Key, level);
            float percent = (float)kvp.Value / totalDamage;

            itemScript.PlayAnimation(kvp.Value, percent);

            float waitTime = itemScript.GetAnimationDuration() * 0.6f;
            yield return new WaitForSecondsRealtime(waitTime);
        }

        yield return new WaitForSecondsRealtime(0.3f);
        if (buttonsPanel) buttonsPanel.SetActive(true);
    }

    // --- 核心：包含位移的单个动画 ---
    private IEnumerator AnimateSingleStat(StatUI stat, float targetValue, bool isTimeFormat)
    {
        if (stat.root == null) yield break;

        stat.root.SetActive(true);

        // 计算其实位置
        Vector2 startPos = stat.originPos + moveOffset;
        Vector2 endPos = stat.originPos;

        float timer = 0f;
        // 我们用 statAnimDuration 来控制整体移动和滚动的时长
        float duration = statAnimDuration;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            // [核心] 使用 SmoothStep 实现 慢入-快-慢出 (Sigmoid曲线)
            // 如果想要更有弹性的效果，也可以继续用之前的 BackOut 或 ElasticOut
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // 1. 位移动画
            if (stat.rect != null)
            {
                stat.rect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothProgress);
            }

            // 2. 图标缩放 (前50%的时间完成)
            if (stat.icon)
            {
                float iconProgress = Mathf.Clamp01(progress / 0.5f);
                // 简单的放大再回弹
                float scale = Mathf.Sin(iconProgress * Mathf.PI) * 0.2f + 1f;
                if (iconProgress >= 1) scale = 1f;
                stat.icon.transform.localScale = Vector3.one * Mathf.Lerp(0f, scale, iconProgress);
            }

            // 3. 标题淡入 (前30%的时间完成)
            if (stat.title)
            {
                float titleProgress = Mathf.Clamp01(progress / 0.3f);
                stat.title.alpha = Mathf.Lerp(0f, 1f, titleProgress);
            }

            // 4. 数字滚动 (全程跟随 smoothProgress)
            if (stat.value)
            {
                float currentVal = Mathf.Lerp(0, targetValue, smoothProgress);

                if (isTimeFormat)
                    stat.value.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(currentVal / 60), Mathf.FloorToInt(currentVal % 60));
                else
                    stat.value.text = Mathf.RoundToInt(currentVal).ToString();
            }

            yield return null;
        }

        // 强制归位，防止浮点数误差
        if (stat.rect != null) stat.rect.anchoredPosition = endPos;
        if (stat.icon) stat.icon.transform.localScale = Vector3.one;
        if (stat.title) stat.title.alpha = 1f;

        if (stat.value)
        {
            if (isTimeFormat)
                stat.value.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(targetValue / 60), Mathf.FloorToInt(targetValue % 60));
            else
                stat.value.text = Mathf.RoundToInt(targetValue).ToString();
        }

        // 稍微停顿一下再开始下一个组，增强节奏感
        yield return new WaitForSecondsRealtime(0.05f);
    }

    // 按钮事件保持不变...
    public void OnRestartClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnReturnToHubClicked()
    {
        Time.timeScale = 1f;
        PlayerProgressManager.Instance?.SaveGame();
        SceneManager.LoadScene(1);
    }
}