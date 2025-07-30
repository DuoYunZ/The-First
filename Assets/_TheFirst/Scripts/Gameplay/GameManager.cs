using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// 將遊戲狀態枚舉放在類外部，方便其他腳本引用
public enum GameState { Building, Combat, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("場景名稱")]
    public string characterSelectSceneName = "CharacterSelectScene";

    [Header("UI 引用")]
    [Tooltip("遊戲結束 UI 面板")]
    public GameObject gameOverPanel;
    [Tooltip("戰鬥 UI 的容器")]
    public GameObject combatUIContainer;

    [Header("執行時狀態 (Runtime State)")]
    [SerializeField] private GameState currentState = GameState.Building;
    public Transform playerTransform { get; private set; }
    private Health playerHealthComponent = null;
    public Transform playerAimTarget { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 確保 GameManager 在切換場景時不被銷毀
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 在遊戲首次啟動時，確保所有相關UI都處於正確的初始狀態
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        // combatUIContainer 的顯隱由進入戰鬥場景的邏輯控制
    }

    /// <summary>
    /// 由 CombatSceneInitializer 在完成機甲重建和場景設定後呼叫
    /// </summary>
    public void PlayerMechReadyInCombatScene(GameObject playerInstance)
    {
        if (playerInstance == null)
        {
            Debug.LogError("[GameManager] Player instance provided by Initializer is null! Cannot start combat.");
            return;
        }

        Debug.Log($"[GameManager] 玩家機甲 '{playerInstance.name}' 已準備就緒。遊戲進入戰鬥狀態。");
        currentState = GameState.Combat;
        Time.timeScale = 1f; // 確保遊戲時間正常流動

        // 保存對玩家核心組件的引用
        playerTransform = playerInstance.transform;
        playerHealthComponent = playerInstance.GetComponent<Health>();

        // --- 【新增】查找并保存瞄准点 ---
        if (playerTransform != null)
        {
            playerAimTarget = playerTransform.Find("AimTargetPoint");
            if (playerAimTarget == null)
            {
                Debug.LogWarning($"[GameManager] 在玩家预制件 '{playerInstance.name}' 上没有找到名为 'AimTargetPoint' 的子对象！敌人将继续瞄准脚底。");
                // 如果找不到，就用根对象作为后备，避免报错
                playerAimTarget = playerTransform;
            }
        }

        // 訂閱玩家的死亡事件，這是觸發遊戲結束的關鍵
        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnDeath.RemoveListener(HandleGameOver); // 先移除，防止重複訂閱
            playerHealthComponent.OnDeath.AddListener(HandleGameOver);
            Debug.Log("[GameManager] 已成功訂閱玩家的 OnDeath 事件。");
        }
        else
        {
            Debug.LogError($"[GameManager] 玩家預制件 '{playerInstance.name}' 上缺少 Health 組件！");
        }

        // 不再由 GameManager 直接控制，而是呼叫 UIManager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowCombatUI();
        }
    }

    /// <summary>
    /// 處理玩家死亡，由玩家 Health 腳本的 OnDeath 事件觸發
    /// </summary>
    private void HandleGameOver()
    {
        if (currentState == GameState.GameOver) return; // 防止重複執行

        currentState = GameState.GameOver;
        Time.timeScale = 0f; // 凍結遊戲時間
        Debug.Log("[GameManager] 遊戲結束！時間已暫停。");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideCombatUI();
            UIManager.Instance.ShowGameOverPanel();
        }
        else
        {
            Debug.LogError("[GameManager] UIManager.Instance is null! Cannot show/hide UI panels.");
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.enabled = false;
        }
    }

    #region Public Methods for UI Buttons

    /// <summary>
    /// 供“重新開始戰鬥”按鈕呼叫
    /// </summary>
    public void RestartCombat()
    {
        Debug.Log("[GameManager] 正在重新開始戰鬥...");
        Time.timeScale = 1f;
        // 重新載入當前場景。DataManager 中的機甲配置仍然存在，
        // 所以 CombatSceneInitializer 會用同樣的配置重建機甲。
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 供“返回機庫/初始場景”按鈕呼叫
    /// </summary>
    public void ReturnToCharacterSelect()
    {
        Debug.Log($"[GameManager] 正在返回角色选择场景: {characterSelectSceneName}");
        Time.timeScale = 1f;

        // 清理当前选择的角色数据，以便重新开始
        if (DataManager.Instance != null)
        {
            // 【核心修改】使用新的 selectedCharacter 变量
            DataManager.Instance.selectedCharacter = null;
        }

        SceneManager.LoadScene(characterSelectSceneName);
    }

    #endregion

    public GameState GetCurrentState()
    {
        return currentState;
    }
}