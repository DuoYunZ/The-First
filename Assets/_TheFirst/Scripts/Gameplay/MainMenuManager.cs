using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("面板引用")]
    public GameObject settingsPanel;
    public GameObject mainPanel; // 添加对主面板的引用

    [Header("社交链接")]
    [Tooltip("Discord 服务器邀请链接")]
    public string discordUrl = "https://discord.gg/你的邀请码";
    [Tooltip("QQ群链接")]
    public string qqGroupUrl = "https://qm.qq.com/q/你的群号";

    void Start()
    {
        // 确保游戏开始时，设置面板是隐藏的，主面板是显示的
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    /// <summary>
    /// 开始/继续游戏
    /// </summary>
    public void OnStartGameClicked()
    {
        // 现在这个按钮直接加载Hub场景，存档加载逻辑会在PlayerProgressManager中处理
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("HubScene");
        else SceneManager.LoadScene("HubScene");
    }

    /// <summary>
    /// 打开设置面板
    /// </summary>
    public void OnSettingsClicked()
    {
        if (settingsPanel != null && mainPanel != null)
        {
            settingsPanel.SetActive(true);
            mainPanel.SetActive(false); // 隐藏主面板
        }
    }

    /// <summary>
    /// 从设置面板返回主菜单
    /// </summary>
    public void OnBackFromSettingsClicked()
    {
        if (settingsPanel != null && mainPanel != null)
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true); // 显示主面板
        }
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void OnQuitGameClicked()
    {
        // 在退出前可以强制执行一次存档
        // PlayerProgressManager.Instance?.SaveGame(); 
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    public void OnClearSaveClicked()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.ClearSaveData();
        }
        else
        {
            Debug.LogError("未能找到 PlayerProgressManager 实例！");
        }

        // （可选）清除存档后，可以重启游戏或重新加载主菜单来确保所有状态刷新
        // SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// 打开 Discord 服务器链接
    /// </summary>
    public void OnDiscordClicked()
    {
        if (!string.IsNullOrEmpty(discordUrl))
        {
            Application.OpenURL(discordUrl);
        }
    }

    /// <summary>
    /// 打开 QQ群链接
    /// </summary>
    public void OnQQGroupClicked()
    {
        if (!string.IsNullOrEmpty(qqGroupUrl))
        {
            Application.OpenURL(qqGroupUrl);
        }
    }
}