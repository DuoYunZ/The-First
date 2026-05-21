using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// 將遊戲狀態枚舉放在類外部，方便其他腳本引用
public enum GameState { Building, Combat, GameOver, Victory } // [新增] Victory 状态

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("場景名稱")]
    public string characterSelectSceneName = "CharacterSelectScene";

    [Tooltip("新的结算界面脚本引用")]
    public SettlementUI settlementUI;

    [Tooltip("戰鬥 UI 的容器")]
    public GameObject combatUIContainer;

    [Header("執行時狀態 (Runtime State)")]
    [SerializeField] private GameState currentState = GameState.Building;
    public Transform playerTransform { get; private set; }
    private Health playerHealthComponent = null;
    public Transform playerAimTarget { get; private set; }
    private bool victoryPending = false;

    [Header("能量石掉落池")]
    [Tooltip("所有可能掉落的能量石 (EnergyStoneSO) 资产文件")]
    public List<EnergyStoneSO> energyStoneLootTable;

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
        if (settlementUI != null) settlementUI.gameObject.SetActive(false);
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

        currentState = GameState.Combat;
        victoryPending = false;
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
        if (currentState == GameState.GameOver || currentState == GameState.Victory || victoryPending) return; // 防止重複執行

        currentState = GameState.GameOver;

        // 【图鉴成就】记录死亡次数 (不死鸟的羽毛解锁条件)
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddStat("Death_Count", 1);
        }

        Time.timeScale = 0f; // 凍結遊戲時間
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideCombatUI();
            // [已删除] UIManager.Instance.ShowGameOverPanel(); <-- 不再使用旧的
        }
        if (settlementUI != null)
        {
            settlementUI.Show(false); // false = 失敗/死亡
        }
        else
        {
            Debug.LogError("[GameManager] SettlementUI 未赋值！请在 Inspector 中设置。");
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.enabled = false;
        }
    }

    #region Public Methods for UI Buttons


    public void HandleVictory()
    {
        if (currentState == GameState.Victory) return;
        if (currentState == GameState.GameOver && !victoryPending) return;

        currentState = GameState.Victory;
        victoryPending = false;
        Time.timeScale = 0f;

        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.IncreaseAchievementStat("Victory_Count", 1);
            string timelineName = GameTimelineManager.Instance != null
                ? GameTimelineManager.Instance.GetActiveTimelineName()
                : string.Empty;
            PlayerProgressManager.Instance.RecordDemoVictory(SceneManager.GetActiveScene().name, timelineName);
        }
        // 1. 隱藏戰鬥UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideCombatUI();
        }

        // 2. 顯示新的結算界面 (勝利)
        if (settlementUI != null)
        {
            settlementUI.Show(true); // true = 勝利
        }
    }

    public void BeginVictoryPending()
    {
        if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
        victoryPending = true;
    }
    /// <summary>
    /// 供“重新開始戰鬥”按鈕呼叫
    /// </summary>
    public void RestartCombat()
    {
        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 确保物理模拟恢复
        // 重新載入當前場景。DataManager 中的機甲配置仍然存在，
        // 所以 CombatSceneInitializer 會用同樣的配置重建機甲。
        if (settlementUI != null) settlementUI.gameObject.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 供“返回機庫/初始場景”按鈕呼叫
    /// </summary>
    public void ReturnToCharacterSelect()
    {
        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 确保物理模拟恢复
        if (settlementUI != null) settlementUI.gameObject.SetActive(false);

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
