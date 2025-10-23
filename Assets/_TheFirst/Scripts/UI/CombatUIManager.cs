using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // <-- 1. 引入新的 Input System 命名空间

public class CombatUIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    [Tooltip("在按下ESC时要显示的暂停面板")]
    public GameObject pausePanel;

    private bool isPaused = false;
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
    }

    void Update()
    {
        // 6. 使用新的方式检测按键
        // .triggered 相当于旧的 GetButtonDown
        if (pauseAction.triggered)
        {
            if (isPaused)
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
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
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
}