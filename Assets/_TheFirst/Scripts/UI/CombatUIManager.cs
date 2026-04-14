using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // <-- 1. 引入新的 Input System 命名空间

public class CombatUIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    [Tooltip("在按下ESC时要显示的暂停面板")]
    public GameObject pausePanel;
    
    [Tooltip("暂停菜单内的设置面板")]
    public GameObject settingsPanel;

    private bool isPaused = false;
    private bool isSettingsOpen = false;
    private InputAction pauseAction; // <-- 2. 创建一个 InputAction 变量

    void Awake()
    {
        // 3. 初始化这个 Action 并绑定到键盘的 Escape 键
        pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");
    }

    private void OnEnable()
    {
        // 4. 激活 Action
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        // 5. 禁用 Action，防止在场景卸载后还占用资源
        pauseAction.Disable();
    }


    void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        else
        {
            Debug.LogError("暂停面板 (PausePanel) 未在 CombatUIManager 中分配!", this);
        }
        
        // 初始化设置面板为隐藏状态
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 当游戏窗口失去/获得焦点时调用（Alt+Tab、切换应用等）
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !isPaused)
        {
            // 失去焦点时自动暂停
            PauseGame();
        }
        else if (hasFocus && isPaused)
        {
            // 获得焦点时，即使仍在暂停状态也要防止物理追赶
            // 通过临时极低的 maximumDeltaTime 防止 FixedUpdate 积压
            Time.maximumDeltaTime = Time.fixedDeltaTime;
        }
    }

    void Update()
    {
        // 6. 使用新的方式检测按键
        // .triggered 相当于旧的 GetButtonDown
        if (pauseAction.triggered)
        {
            // 如果设置面板打开，则关闭设置面板
            if (isSettingsOpen)
            {
                CloseSettings();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // 暂停游戏
    public void PauseGame()
    {
        if (pausePanel == null) return;

        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        // 暂停物理模拟，防止恢复时大量 FixedUpdate 追赶
        Physics.simulationMode = SimulationMode.Script;

        var selectable = pausePanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
        if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    // 继续游戏 (这个方法现在可以被按钮和ESC键共用)
    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        isSettingsOpen = false;
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // 恢复物理模拟
        Physics.simulationMode = SimulationMode.FixedUpdate;
        // 限制第一帧的 deltaTime，防止所有系统一次性处理大量累积时间
        Time.maximumDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = 1f;

        // 下一帧恢复正常的 maximumDeltaTime
        StartCoroutine(RestoreMaxDeltaTime());
    }

    /// <summary>
    /// 延迟一帧后恢复 maximumDeltaTime 到正常值
    /// </summary>
    private System.Collections.IEnumerator RestoreMaxDeltaTime()
    {
        yield return null; // 等一帧
        Time.maximumDeltaTime = 0.3333333f; // Unity 默认值
    }

    // 打开设置面板
    public void OpenSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(true);
        isSettingsOpen = true;

        var selectable = settingsPanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
        if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    // 关闭设置面板
    public void CloseSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(false);
        isSettingsOpen = false;

        if (pausePanel != null && pausePanel.activeInHierarchy)
        {
            var selectable = pausePanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
            if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }

    // 返回枢纽场景
    public void ReturnToHub()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }
        else
        {
            Debug.LogWarning("未找到 PlayerProgressManager 实例，金币可能未被保存！");
        }

        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 恢复物理模拟
        isPaused = false;
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("HubScene");
        else SceneManager.LoadScene("HubScene");
    }

    // 返回主菜单
    public void ReturnToMainMenu()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }

        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 恢复物理模拟
        isPaused = false;
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("MainMenu");
        else SceneManager.LoadScene("MainMenu");
    }

    // 退出游戏
    public void QuitGame()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }

        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}