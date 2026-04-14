using UnityEngine;
using TMPro; // 引入 TextMeshPro 命名空间

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("用于显示计时器的 TextMeshPro UGUI 组件")]
    public TextMeshProUGUI timerText;

    [Header("计时设置")]
    [Tooltip("关卡总时长（秒），用于倒计时显示。由 GameTimelineManager 自动设置")]
    public float totalDuration = 600f;

    private float elapsedTime;
    private bool isTimerRunning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (timerText == null)
        {
            Debug.LogError("计时器UI文本未分配!", this);
            return;
        }
        // 重置并开始计时
        ResetTimer();
        StartTimer();
    }

    void Update()
    {
        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }

    public void StartTimer()
    {
        isTimerRunning = true;
    }

    public void StopTimer()
    {
        isTimerRunning = false;
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
        UpdateTimerDisplay();
    }

    /// <summary>
    /// 设置总时长并重置计时器（由 GameTimelineManager 调用）
    /// </summary>
    public void SetTotalDuration(float duration)
    {
        totalDuration = duration;
        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        // 倒计时：显示剩余时间
        float remainingTime = Mathf.Max(0, totalDuration - elapsedTime);
        int minutes = (int)(remainingTime / 60);
        int seconds = (int)(remainingTime % 60);

        // 格式化为 "MM:SS" 字符串
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        // 最后30秒变红色提示
        if (remainingTime <= 30f && remainingTime > 0f)
        {
            timerText.color = Color.red;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    /// <summary>
    /// 获取已经过的时间（秒），供 GameTimelineManager 使用
    /// </summary>
    public float GetElapsedTime()
    {
        return elapsedTime;
    }

    /// <summary>
    /// 获取剩余时间（秒）
    /// </summary>
    public float GetRemainingTime()
    {
        return Mathf.Max(0, totalDuration - elapsedTime);
    }
}