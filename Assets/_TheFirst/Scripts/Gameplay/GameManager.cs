using UnityEngine;
using UnityEngine.Events;

// (GameState 枚举定义) ...
public enum GameState { Building, Combat }

public class GameManager : MonoBehaviour
{
    [Header("当前状态")]
    [SerializeField] // 在 Inspector 中可见，但不能直接修改
    private GameState currentState = GameState.Building; // 初始状态为建造

    [Header("系统引用")]
    [SerializeField] private MechBuilder mechBuilder; // 还是需要启用/禁用
    // BuildCameraController 现在应该在 buildCameraObject 上，不再需要单独引用
    [SerializeField] private Rigidbody chassisRigidbody;
    [SerializeField] private Transform chassisCoreTransform;

    [Header("UI 引用")]
    [SerializeField] private GameObject buildUIContainer;
    [SerializeField] private GameObject combatUIContainer;

    [Header("战斗模式设置")]
    [SerializeField] private GameObject mechRootPrefabOrObject;
    private GameObject currentMechRootInstance = null;
    private MechController rootMechController = null; // 父级上的控制器脚本

    // --- 新增：摄像机引用 ---
    [Header("摄像机引用 (拖拽场景中的对象)")]
    [SerializeField] private GameObject buildCameraObject; // 包含建造摄像机和 BuildCameraController 的对象
    [SerializeField] private GameObject combatCameraObject; // 包含战斗摄像机(及跟随脚本/Cinemachine)的对象
    // -----------------------
    [Header("系统引用")]
    // ... (其他引用) ...
    [SerializeField] private EnemySpawner enemySpawner; // *** 新增 ***
    public Transform playerTransform { get; private set; } // 对外只读属性

    // (可选) 事件，用于通知其他脚本状态已改变
    public UnityEvent OnEnterBuildMode;
    public UnityEvent OnEnterCombatMode;

    // ---- 单例模式 (可选，方便全局访问 GameManager) ----
    public static GameManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果需要跨场景保留 GameManager
        }
    }
    // ---- 单例模式结束 ----


    void Start()
    {
        // ... (检查 chassisCoreTransform, mechRootPrefabOrObject) ...
        // --- 新增：检查相机引用 ---
        if (buildCameraObject == null || combatCameraObject == null)
        {
            Debug.LogError("GameManager: 建造或战斗摄像机对象未设置!", this);
            enabled = false;
            return;
        }
        if (enemySpawner == null)
        {
            Debug.LogError("GameManager: Enemy Spawner 未设置!", this);
            enabled = false;
            return;
        }
        // -----------------------

        EnterBuildMode();
    }

    // 进入建造模式的逻辑
    public void EnterBuildMode()
    {
        Debug.Log("Entering Build Mode...");
        currentState = GameState.Building;

        // --- 解除父子关系 & 销毁/禁用 MechRoot ---
        if (currentMechRootInstance != null && chassisCoreTransform != null) { /* ... */ }
        if (currentMechRootInstance != null) { /* ... */ }
        // ------------------------------------

        // --- 启用/禁用脚本和对象 ---
        if (mechBuilder != null) mechBuilder.enabled = true;
        if (rootMechController != null) rootMechController.enabled = false; // 禁用战斗控制器
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;
        // -------------------------

        // --- 切换 UI ---
        if (buildUIContainer != null) buildUIContainer.SetActive(true);
        if (combatUIContainer != null) combatUIContainer.SetActive(false);
        // ---------------

        // --- 切换摄像机 ---
        if (combatCameraObject != null) combatCameraObject.SetActive(false); // 禁用战斗相机
        if (buildCameraObject != null) buildCameraObject.SetActive(true);  // 启用建造相机
        // 确保 BuildCameraController (如果它是 BuildCameraObject 上的组件) 也被启用
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = true;
        // ------------------

        // --- *** 新增：停止敌人生成 *** ---
        if (enemySpawner != null) enemySpawner.enabled = false; // 或者调用 enemySpawner.StopSpawning();

        playerTransform = null; // <--- 清除引用

        OnEnterBuildMode?.Invoke();
    }

    // 进入战斗模式的逻辑
    public void EnterCombatMode()
    {
        Debug.Log("EnterCombatMode: --- Start ---");

        // --- 显式日志记录 Inspector 引用 ---
        Debug.Log($"EnterCombatMode: Checking references - chassisCoreTransform is {(chassisCoreTransform == null ? "!!! NULL !!!" : chassisCoreTransform.name)}");
        Debug.Log($"EnterCombatMode: Checking references - mechRootPrefabOrObject is {(mechRootPrefabOrObject == null ? "!!! NULL !!!" : mechRootPrefabOrObject.name)}");
        // ---------------------------------

        // 安全检查
        if (chassisCoreTransform == null || mechRootPrefabOrObject == null)
        {
            Debug.LogError("无法进入战斗模式：Chassis Core Transform 或 Mech Root 未设置！请在 GameManager Inspector 中检查赋值！");
            return; // 阻止后续执行
        }
        Debug.Log("EnterCombatMode: Initial reference checks passed.");

        currentState = GameState.Combat;
        Debug.Log("EnterCombatMode: State set to Combat.");

        // 禁用建造系统
        Debug.Log("EnterCombatMode: Disabling Build Systems...");
        if (mechBuilder != null) mechBuilder.enabled = false;
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = false;
        mechBuilder?.ClearSelection();
        Debug.Log("EnterCombatMode: Build Systems Disabled.");

        // 创建/启用 MechRoot
        Debug.Log("EnterCombatMode: Creating/Enabling MechRoot...");
        if (currentMechRootInstance == null)
        {
            if (mechRootPrefabOrObject.scene.IsValid())
            { // 场景对象
                currentMechRootInstance = mechRootPrefabOrObject;
                currentMechRootInstance.SetActive(true);
                Debug.Log($"EnterCombatMode: Activated existing MechRoot from scene: {currentMechRootInstance.name}");
            }
            else
            { // Prefab
                Debug.Log($"EnterCombatMode: Attempting to Instantiate prefab: {mechRootPrefabOrObject.name}");
                currentMechRootInstance = Instantiate(mechRootPrefabOrObject);
                // --- 在 Instantiate 后立刻检查 ---
                if (currentMechRootInstance == null)
                {
                    Debug.LogError("!!! FATAL: Instantiate(mechRootPrefabOrObject) 返回了 NULL! 请检查预设文件是否存在且有效！", mechRootPrefabOrObject);
                    EnterBuildMode(); // 尝试恢复
                    return; // 阻止后续执行
                }
                Debug.Log($"EnterCombatMode: Instantiated MechRoot from prefab: {currentMechRootInstance.name}");
                // -------------------------------
            }
            currentMechRootInstance.name = "MechRoot_ActiveInstance";
            if (enemySpawner != null) enemySpawner.enabled = true;

            // --- 在访问 Transform 前再次进行严格检查 ---
            if (currentMechRootInstance == null)
            {
                Debug.LogError("!!! FATAL: currentMechRootInstance 在设置 Transform 前意外变为 NULL!", this); EnterBuildMode(); return;
            }
            if (currentMechRootInstance.transform == null)
            {
                Debug.LogError("!!! FATAL: currentMechRootInstance.transform 是 NULL!", currentMechRootInstance); EnterBuildMode(); return;
            }
            if (chassisCoreTransform == null)
            { // 再次检查 ChassisCore
                Debug.LogError("!!! FATAL: chassisCoreTransform 在设置 Transform 前意外变为 NULL!", this); EnterBuildMode(); return;
            }
            Debug.Log($"EnterCombatMode: 即将设置位置/旋转. MechRoot: {currentMechRootInstance.name}, Chassis: {chassisCoreTransform.name}");
            // ---------------------------------------

            // 设置位置和旋转 (大约在原来的 Line 147 附近)
            currentMechRootInstance.transform.position = chassisCoreTransform.position; // <-- 错误可能在这里
            currentMechRootInstance.transform.rotation = chassisCoreTransform.rotation; // <-- 或者这里

            Debug.Log("EnterCombatMode: MechRoot Transform set."); // 如果能执行到这里，说明上面两行没问题

            // 获取控制器
            rootMechController = currentMechRootInstance.GetComponent<MechController>();
            if (rootMechController == null) { /* ... */ return; }
            playerTransform = currentMechRootInstance.transform; // 设置玩家引用
        }
        else
        {
            Debug.Log("EnterCombatMode: Using existing MechRoot instance.");
        }

        // 设置父子关系
        Debug.Log("EnterCombatMode: Parenting...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true);
        Debug.Log("EnterCombatMode: Parenting Done.");

        // 设置物理状态
        Debug.Log("EnterCombatMode: Setting Physics State (Kinematic)...");
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;
        Debug.Log("EnterCombatMode: Physics State Set.");

        // 启用战斗控制器
        Debug.Log("EnterCombatMode: Enabling Combat Controller...");
        if (rootMechController != null) rootMechController.enabled = true;
        Debug.Log("EnterCombatMode: Combat Controller Enabled.");

        // 切换 UI
        Debug.Log("EnterCombatMode: Switching UI...");
        if (buildUIContainer != null) buildUIContainer.SetActive(false);
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
        Debug.Log("EnterCombatMode: UI Switched.");

        // 切换摄像机
        Debug.Log("EnterCombatMode: Switching Cameras...");
        if (buildCameraObject != null) buildCameraObject.SetActive(false);
        if (combatCameraObject != null) combatCameraObject.SetActive(true);
        if (combatCameraObject != null && !combatCameraObject.CompareTag("MainCamera")) { /* ... */ }
        Debug.Log("EnterCombatMode: Cameras Switched.");

        // 触发事件
        Debug.Log("EnterCombatMode: Invoking Event...");
        OnEnterCombatMode?.Invoke();
        Debug.Log("EnterCombatMode: --- Finished Successfully ---");
    }


    // 提供一个公共方法供 UI 按钮调用
    public void SwitchToCombatMode()
    {
        if (currentState == GameState.Building)
        {
            EnterCombatMode();
        }
        else
        {
            Debug.LogWarning("已经在战斗模式或未知状态，无法切换到战斗模式。");
        }
    }

    // (可选) 提供切换回建造模式的方法
    public void SwitchToBuildMode()
    {
        if (currentState == GameState.Combat)
        {
            EnterBuildMode();
            // 可能需要重置机甲位置、状态等
            // ResetMechPosition();
        }
        else
        {
            Debug.LogWarning("已经在建造模式或未知状态，无法切换到建造模式。");
        }
    }


    // (可选) 获取当前状态
    public GameState GetCurrentState()
    {
        return currentState;
    }
}