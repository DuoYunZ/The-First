using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic; // 需要 List
using System.Linq; // 可能需要 Linq

// 游戏状态枚举
public enum GameState { Building, Combat, GameOver }

public class GameManager : MonoBehaviour
{
    [Header("当前状态")]
    [SerializeField] private GameState currentState = GameState.Building;

    [Header("系统引用")]
    [SerializeField] private MechBuilder mechBuilder;
    [SerializeField] private Rigidbody chassisRigidbody;
    [SerializeField] private Transform chassisCoreTransform; // 需要 ChassisCore 的 Transform 引用
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("UI 引用")]
    [SerializeField] private GameObject buildUIContainer;
    [SerializeField] private GameObject combatUIContainer;
    [SerializeField] private GameObject gameOverPanel;

    [Header("战斗模式设置")]
    [SerializeField] private GameObject mechRootPrefabOrObject; // 父级控制器预设或对象
    private GameObject currentMechRootInstance = null;
    private MechController rootMechController = null;
    private Health playerHealthComponent = null;
    public Transform playerTransform { get; private set; } // 供其他脚本访问玩家 (MechRoot) 的 Transform

    [Header("摄像机引用")]
    [SerializeField] private GameObject buildCameraObject;
    [SerializeField] private GameObject combatCameraObject;

    // --- vvv 新增：落地设置 vvv ---
    [Header("落地设置 (Ground Drop Settings)")]
    [Tooltip("向下检测地面的最大距离")]
    public float groundCheckDistance = 10f;
    [Tooltip("机甲基准点(通常是MechRoot的Pivot)距离下方最高地面点的期望高度")]
    public float pivotHeightAboveGround = 0.1f; // 需要根据模型和Pivot仔细调整
    [Tooltip("用于检测地面的层 (在 Inspector 中选择，例如 Default, Ground)")]
    public LayerMask groundLayerMask = 1; // 默认只检测 Default 层, **请务必在 Inspector 中修改!**
    [Tooltip("用于向下射线检测的机甲底部采样点 (相对于 ChassisCore 的局部坐标)")]
    public List<Vector3> groundCheckOffsets = new List<Vector3>() {
        Vector3.zero, // 中心点
        new Vector3(0.5f, 0, 0.5f), // 右前方 (这些值需要根据你的机甲大概尺寸调整)
        new Vector3(-0.5f, 0, 0.5f), // 左前方
        new Vector3(0.5f, 0, -0.5f), // 右后方
        new Vector3(-0.5f, 0, -0.5f)  // 左后方
    };
    // --- ^^^ 新增：落地设置 ^^^ ---

    [Header("事件")]
    public UnityEvent OnEnterBuildMode;
    public UnityEvent OnEnterCombatMode;

    // --- 单例模式 ---
    public static GameManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; /* DontDestroyOnLoad(gameObject); */ }
    }
    // ---------------

    void Start()
    {
        // --- 执行必要的空值检查 ---
        if (chassisCoreTransform == null && chassisRigidbody != null) chassisCoreTransform = chassisRigidbody.transform;
        bool checkFailed = false;
        if (CheckNull(chassisCoreTransform, "Chassis Core Transform")) checkFailed = true;
        if (CheckNull(mechRootPrefabOrObject, "Mech Root Prefab Or Object")) checkFailed = true;
        if (CheckNull(buildCameraObject, "Build Camera Object")) checkFailed = true;
        if (CheckNull(combatCameraObject, "Combat Camera Object")) checkFailed = true;
        if (CheckNull(enemySpawner, "Enemy Spawner")) checkFailed = true;
        if (CheckNull(buildUIContainer, "Build UI Container")) checkFailed = true;
        if (CheckNull(combatUIContainer, "Combat UI Container")) checkFailed = true;
        if (CheckNull(gameOverPanel, "Game Over Panel")) checkFailed = true;
        if (CheckNull(mechBuilder, "Mech Builder")) checkFailed = true;
        if (CheckNull(chassisRigidbody, "Chassis Rigidbody")) checkFailed = true; // 也检查一下 Rigidbody

        if (checkFailed)
        {
            Debug.LogError("GameManager Start: 一个或多个必要的引用未在 Inspector 中设置！脚本已禁用。", this);
            enabled = false;
            return;
        }
        // -----------------------

        EnterBuildMode(); // 游戏开始时进入建造模式
    }

    // 辅助函数，用于检查引用是否为空并打印错误
    private bool CheckNull(object obj, string fieldName)
    {
        if (obj == null || obj.Equals(null))
        { // Unity 对象重载了 == null
            Debug.LogError($"GameManager Error: '{fieldName}' 未在 Inspector 中设置!", this);
            return true;
        }
        return false;
    }


    public void EnterBuildMode()
    {
        Debug.Log("Entering Build Mode...");
        currentState = GameState.Building;
        Time.timeScale = 1f; // 恢复时间

        // 解除父子关系 & 清理 MechRoot
        if (currentMechRootInstance != null)
        {
            if (playerHealthComponent != null)
            {
                playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath);
                playerHealthComponent = null;
            }
            if (chassisCoreTransform != null)
            {
                chassisCoreTransform.SetParent(null, true);
            }

            // 根据是场景对象还是 Prefab 实例来处理
            if (mechRootPrefabOrObject != null && mechRootPrefabOrObject.scene.IsValid() && currentMechRootInstance == mechRootPrefabOrObject)
            {
                currentMechRootInstance.SetActive(false);
            }
            else
            {
                Destroy(currentMechRootInstance);
            }
            currentMechRootInstance = null;
            rootMechController = null;
            playerTransform = null;
        }

        // 启用建造系统
        if (mechBuilder != null) mechBuilder.enabled = true;
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;

        // 禁用战斗系统
        if (enemySpawner != null) enemySpawner.enabled = false;

        // 切换 UI
        if (combatUIContainer != null) combatUIContainer.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (buildUIContainer != null) buildUIContainer.SetActive(true);

        // 切换摄像机
        if (combatCameraObject != null) combatCameraObject.SetActive(false);
        if (buildCameraObject != null)
        {
            buildCameraObject.SetActive(true);
            var buildCamController = buildCameraObject.GetComponent<BuildCameraController>();
            if (buildCamController != null) buildCamController.enabled = true;
        }

        OnEnterBuildMode?.Invoke();
        Debug.Log("Entered Build Mode setup complete.");
    }


    public void EnterCombatMode()
    {
        Debug.Log("EnterCombatMode: --- Start ---");
        if (currentState != GameState.Building) { Debug.LogWarning("Already in Combat/GameOver, cannot re-enter Combat."); return; } // 防止重入

        // 安全检查 (Start 时已检查，这里再确认一次)
        if (chassisCoreTransform == null || mechRootPrefabOrObject == null) { Debug.LogError("Cannot enter Combat Mode: References missing!"); return; }

        currentState = GameState.Combat;
        Time.timeScale = 1f;
        Debug.Log("EnterCombatMode: State set to Combat.");

        // --- 禁用建造系统 ---
        Debug.Log("EnterCombatMode: Disabling Build Systems...");
        if (mechBuilder != null) mechBuilder.enabled = false;
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = false;
        mechBuilder?.ClearSelection(); // 清除可能的幽灵零件
        Debug.Log("EnterCombatMode: Build Systems Disabled.");

        // --- 创建/启用 MechRoot ---
        Debug.Log("EnterCombatMode: Creating/Enabling MechRoot...");
        if (currentMechRootInstance == null)
        {
            // ... (实例化或激活 MechRoot 的逻辑) ...
            if (mechRootPrefabOrObject.scene.IsValid()) { /*...*/ } else { currentMechRootInstance = Instantiate(mechRootPrefabOrObject); }
            if (currentMechRootInstance == null) { /* 致命错误检查 */ EnterBuildMode(); return; }
            currentMechRootInstance.name = "MechRoot_ActiveInstance";
            // ... (获取 rootMechController 和 playerHealthComponent, 订阅事件) ...
            rootMechController = currentMechRootInstance.GetComponent<MechController>();
            if (rootMechController == null) { /* Error */ EnterBuildMode(); return; }
            playerHealthComponent = currentMechRootInstance.GetComponent<Health>();
            if (playerHealthComponent != null) { playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath); playerHealthComponent.OnDeath.AddListener(HandlePlayerDeath); }
            else { /* Error */ }
        }
        else { /* 使用现有实例 */ }
        // -------------------------

        // --- 设置 MechRoot 初始位置/旋转 ---
        currentMechRootInstance.transform.position = chassisCoreTransform.position;
        currentMechRootInstance.transform.rotation = chassisCoreTransform.rotation;
        playerTransform = currentMechRootInstance.transform; // 可以在这里就设置 playerTransform
        Debug.Log("EnterCombatMode: MechRoot initial transform set.");

        // --- *** 2. 先设置父子关系 *** ---
        Debug.Log("EnterCombatMode: Parenting ChassisCore to MechRoot...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true); // worldPositionStays = true
        Debug.Log("EnterCombatMode: Parenting Done.");
        // --- *** 父子关系设置结束 *** ---

        // --- vvv 落地逻辑 vvv ---
        Debug.Log("EnterCombatMode: Starting ground drop logic..."); // <-- Log A: 开始执行落地逻辑
        float highestGroundHitY = -Mathf.Infinity;
        bool groundFound = false;

        // --- 检查进入循环的条件 ---
        if (chassisCoreTransform != null && groundCheckOffsets != null && groundCheckOffsets.Count > 0)
        {
            Debug.Log("EnterCombatMode: Ground check condition PASSED. Entering foreach loop..."); // <-- Log B: 进入循环的条件满足

            foreach (Vector3 localOffset in groundCheckOffsets)
            {
                Debug.Log("EnterCombatMode: Inside foreach loop for offset: " + localOffset); // <-- Log C: 循环内部开始执行

                // 计算射线起点
                Vector3 worldOffsetPoint = chassisCoreTransform.TransformPoint(localOffset);
                Vector3 rayOrigin = new Vector3(worldOffsetPoint.x, chassisCoreTransform.position.y + 1.0f, worldOffsetPoint.z);

                // --- 可视化射线 ---
                Debug.DrawRay(rayOrigin, Vector3.down * groundCheckDistance, Color.cyan, 3f); // 持续 3 秒显示青色射线
                // -----------------

                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 1.0f, groundLayerMask))
                {
                    groundFound = true;
                    if (hit.point.y > highestGroundHitY)
                    {
                        highestGroundHitY = hit.point.y;
                    }
                }
            } // --- foreach 循环结束 ---
        }
        else // --- 如果进入循环的条件不满足 ---
        {
            // 打印具体是哪个条件失败了
            Debug.LogError($"EnterCombatMode: Ground check condition FAILED! " +
                           $"chassisCoreTransform is {(chassisCoreTransform == null ? "NULL" : "OK")}, " +
                           $"groundCheckOffsets is {(groundCheckOffsets == null ? "NULL" : "OK")}, " +
                           $"Count is {groundCheckOffsets?.Count ?? -1}"); // <-- Log D: 条件检查失败详情
        }


        if (groundFound)
        {
            // --- 添加这些日志 ---
            Debug.Log($"Ground found! Highest ground Y = {highestGroundHitY}"); // 打印找到的最高地面 Y
            float targetRootY = highestGroundHitY + pivotHeightAboveGround;
            Debug.Log($"Pivot Height Offset = {pivotHeightAboveGround}, Calculated Target MechRoot Y = {targetRootY}"); // 打印计算出的目标 Y
            Vector3 currentRootPos = currentMechRootInstance.transform.position;
            Debug.Log($"MechRoot current Y BEFORE adjustment = {currentRootPos.y}"); // 打印调整前 Y
            // ------------------

            currentRootPos.y = targetRootY;
            currentMechRootInstance.transform.position = currentRootPos; // 应用调整后的位置

            // --- 添加这个日志 ---
            Debug.Log($"MechRoot Y AFTER adjustment = {currentMechRootInstance.transform.position.y}"); // 打印调整后 Y
            // ------------------
            Debug.Log($"EnterCombatMode: Mech dropped command executed."); // 精简日志
        }
        else
        {
            Debug.LogWarning("EnterCombatMode: Multi-raycast could not find ground...");
        }
        // --- ^^^ 落地逻辑结束 ^^^ ---

        // --- 设置 GameManager 对 Player Transform 的引用 ---
        if (currentMechRootInstance != null) playerTransform = currentMechRootInstance.transform;
        // ----------------------------------------------

        // --- 设置父子关系 ---
        Debug.Log("EnterCombatMode: Parenting ChassisCore to MechRoot...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true); // 保持世界变换
        Debug.Log("EnterCombatMode: Parenting Done.");

        // --- 设置物理状态 ---
        Debug.Log("EnterCombatMode: Setting ChassisCore Physics State (Kinematic)...");
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true; // 保持 Kinematic
        Debug.Log("EnterCombatMode: Physics State Set.");

        // --- 启用战斗控制器 ---
        Debug.Log("EnterCombatMode: Enabling Combat Controller...");
        if (rootMechController != null) rootMechController.enabled = true;
        Debug.Log("EnterCombatMode: Combat Controller Enabled.");

        // --- 切换 UI ---
        Debug.Log("EnterCombatMode: Switching UI...");
        if (buildUIContainer != null) buildUIContainer.SetActive(false);
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Debug.Log("EnterCombatMode: UI Switched.");

        // --- 切换摄像机 ---
        Debug.Log("EnterCombatMode: Switching Cameras...");
        if (buildCameraObject != null) buildCameraObject.SetActive(false);
        if (combatCameraObject != null) combatCameraObject.SetActive(true);
        if (combatCameraObject != null && !combatCameraObject.CompareTag("MainCamera")) { Debug.LogWarning("Combat Camera Object might need 'MainCamera' tag!"); }
        Debug.Log("EnterCombatMode: Cameras Switched.");

        // --- 启动敌人生成 ---
        Debug.Log("EnterCombatMode: Enabling Enemy Spawner...");
        if (enemySpawner != null) enemySpawner.enabled = true;
        Debug.Log("EnterCombatMode: Enemy Spawner Enabled.");

        // --- 触发事件 ---
        Debug.Log("EnterCombatMode: Invoking OnEnterCombatMode Event...");
        OnEnterCombatMode?.Invoke();
        Debug.Log("EnterCombatMode: --- Finished Successfully ---");
    }


    public void SwitchToCombatMode()
    {
        Debug.Log($"SwitchToCombatMode called. Current state: {currentState}");
        if (currentState == GameState.Building) { EnterCombatMode(); }
        else { Debug.LogWarning("Already in Combat/Game Over mode, cannot switch to Combat."); }
    }


    public void SwitchToBuildMode()
    {
        Debug.Log($"SwitchToBuildMode called. Current state: {currentState}");
        if (currentState == GameState.Combat || currentState == GameState.GameOver) { EnterBuildMode(); }
        else { Debug.LogWarning("Already in Building mode, cannot switch to Build."); }
    }


    public GameState GetCurrentState() { return currentState; }


    void HandlePlayerDeath()
    {
        Debug.Log("GAME OVER!");
        if (currentState == GameState.GameOver) return;
        currentState = GameState.GameOver;
        Time.timeScale = 0f; // 暂停时间

        if (rootMechController != null) rootMechController.enabled = false;
        if (enemySpawner != null) enemySpawner.enabled = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // Unsubscribe handled in EnterBuildMode or can be done here too
        // if (playerHealthComponent != null) playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath);
    }

    public void RestartGame()
    {
        Debug.Log("Restarting game...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}