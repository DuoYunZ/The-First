using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 战斗场景调试工具面板
/// 提供一键无敌和游戏加速功能，方便测试
/// 打包前请删除此脚本或禁用相关GameObject
/// </summary>
public class DebugCheatPanel : MonoBehaviour
{
    [Header("快捷键说明")]
    [Tooltip("F1: 切换无敌模式\nF2: 切换游戏加速 (1x/2x/3x/5x)")]
    public bool showHelpInInspector = true;

    [Header("加速设置")]
    [Tooltip("可用的时间倍率列表")]
    public float[] speedOptions = { 1f, 2f, 3f, 5f };

    // 状态追踪
    private bool isGodMode = false;
    private int currentSpeedIndex = 0;
    private float savedTimeScale = 1f; // 用于暂停菜单恢复后保持加速

    // UI 显示
    private bool showUI = true;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;

    // 输入
    private InputAction godModeAction;
    private InputAction speedAction;
    private InputAction toggleUIAction;

    void Awake()
    {
        // 绑定快捷键
        godModeAction = new InputAction("GodMode", binding: "<Keyboard>/f1");
        speedAction = new InputAction("Speed", binding: "<Keyboard>/f2");
        toggleUIAction = new InputAction("ToggleDebugUI", binding: "<Keyboard>/f3");
    }

    void OnEnable()
    {
        godModeAction.Enable();
        speedAction.Enable();
        toggleUIAction.Enable();
    }

    void OnDisable()
    {
        godModeAction.Disable();
        speedAction.Disable();
        toggleUIAction.Disable();

        // 关闭时还原
        if (isGodMode && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.isInvincible = false;
        }
        Time.timeScale = 1f;
    }

    void Update()
    {
        // F1: 切换无敌
        if (godModeAction.WasPressedThisFrame())
        {
            ToggleGodMode();
        }

        // F2: 切换加速
        if (speedAction.WasPressedThisFrame())
        {
            CycleSpeed();
        }

        // F3: 切换调试UI显示
        if (toggleUIAction.WasPressedThisFrame())
        {
            showUI = !showUI;
        }

        // 如果游戏不在暂停状态，持续应用加速倍率
        // (防止从暂停菜单恢复时 timeScale 被重置为 1)
        if (Time.timeScale > 0f && currentSpeedIndex > 0)
        {
            Time.timeScale = speedOptions[currentSpeedIndex];
        }
    }

    private void ToggleGodMode()
    {
        isGodMode = !isGodMode;
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.isInvincible = isGodMode;
        }
    }

    private void CycleSpeed()
    {
        currentSpeedIndex = (currentSpeedIndex + 1) % speedOptions.Length;

        // 仅在非暂停时应用
        if (Time.timeScale > 0f)
        {
            Time.timeScale = speedOptions[currentSpeedIndex];
        }
    }

    void OnGUI()
    {
        if (!showUI) return;

        // 初始化样式
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));
        }

        // 右上角显示状态
        float boxWidth = 220;
        float boxHeight = 80;
        float margin = 10;
        Rect boxRect = new Rect(Screen.width - boxWidth - margin, margin, boxWidth, boxHeight);

        GUI.Box(boxRect, "", boxStyle);

        // 无敌状态
        string godText = isGodMode ? "<color=lime>开启</color>" : "<color=red>关闭</color>";
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 5, boxWidth - 20, 25),
            $"[F1] 无敌: {godText}", labelStyle);

        // 加速状态
        float curSpeed = speedOptions[currentSpeedIndex];
        string speedColor = curSpeed > 1f ? "yellow" : "white";
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 30, boxWidth - 20, 25),
            $"[F2] 速度: <color={speedColor}>{curSpeed}x</color>", labelStyle);

        // 提示
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 55, boxWidth - 20, 20),
            "<color=#888>[F3] 隐藏面板</color>", labelStyle);
    }

    // 创建纯色纹理
    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
