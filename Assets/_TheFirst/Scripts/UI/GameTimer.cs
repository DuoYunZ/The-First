using UnityEngine;
using TMPro; // 引入 TextMeshPro 命名空间

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("用于显示计时器的 TextMeshPro UGUI 组件")]
    public TextMeshProUGUI timerText;

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

    private void UpdateTimerDisplay()
    {
        // 将总秒数转换为分钟和秒
        int minutes = (int)(elapsedTime / 60);
        int seconds = (int)(elapsedTime % 60);

        // 格式化为 "MM:SS" 字符串
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public float GetElapsedTime()
    {
        return elapsedTime;
    }
}