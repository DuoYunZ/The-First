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
    }

    // 继续游戏 (这个方法现在可以被按钮和ESC键共用)
    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        isSettingsOpen = false;
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // 打开设置面板
    public void OpenSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(true);
        isSettingsOpen = true;
    }

    // 关闭设置面板
    public void CloseSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(false);
        isSettingsOpen = false;
    }

    // 返回枢纽场景
    public void ReturnToHub()
    {
        Debug.Log("正在返回枢纽...");

        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }
        else
        {
            Debug.LogWarning("未找到 PlayerProgressManager 实例，金币可能未被保存！");
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("HubScene");
    }

    // 返回主菜单
    public void ReturnToMainMenu()
    {
        Debug.Log("正在返回主菜单...");

        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // 退出游戏
    public void QuitGame()
    {
        Debug.Log("正在退出游戏...");

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