using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class InGameDebugLog : MonoBehaviour
{
    public static InGameDebugLog Instance { get; private set; }

    [Header("UI引用")]
    [Tooltip("用于显示日志的 TextMeshProUGUI 组件")]
    public TextMeshProUGUI logText;

    [Header("设置")]
    [Tooltip("屏幕上最多显示的日志行数")]
    public int maxLines = 20;

    private readonly Queue<string> logMessages = new Queue<string>();
    private readonly StringBuilder stringBuilder = new StringBuilder();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果您需要在多个场景中持续显示日志，可以取消这行注释
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (logText == null)
        {
            Debug.LogError("InGameDebugLog: logText 字段未在 Inspector 中设置!");
            enabled = false;
        }
    }

    public static void Log(string message)
    {
        // 确保单例存在
        if (Instance == null)
        {
            Debug.Log(message); // 如果单例不存在，则回退到标准日志
            return;
        }

        // 调用实例方法
        Instance.AddMessage(message);
    }

    private void AddMessage(string message)
    {
        // 格式化消息，加入时间戳
        string formattedMessage = $"[{Time.time:F2}] {message}";

        // 同时输出到 Unity 的标准日志，方便在 Player.log 中也看到
        Debug.Log(formattedMessage);

        // 将消息添加到队列
        if (logMessages.Count >= maxLines)
        {
            logMessages.Dequeue(); // 如果超过最大行数，移除最旧的一条
        }
        logMessages.Enqueue(formattedMessage);

        // 更新UI文本
        UpdateLogText();
    }

    private void UpdateLogText()
    {
        stringBuilder.Clear();
        foreach (string message in logMessages)
        {
            stringBuilder.AppendLine(message);
        }
        logText.text = stringBuilder.ToString();
    }
}