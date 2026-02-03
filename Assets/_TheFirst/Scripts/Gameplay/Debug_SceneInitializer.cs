// Debug_SceneInitializer.cs (已集成UI初始化逻辑)
using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class Debug_SceneInitializer : MonoBehaviour
{
    [Header("调试设置")]
    [Tooltip("将场景中预先放置的玩家角色拖到这里")]
    public GameObject playerInstance;
    [Tooltip("场景中的Cinemachine虚拟相机")]
    public CinemachineVirtualCamera virtualCamera;

    // --- 新增引用 ---
    [Header("UI 引用")]
    [Tooltip("将场景中承载 PlayerHealthUI 脚本的UI对象拖到这里")]
    public PlayerHealthUI playerHealthUI; // 在Unity Inspector中将你的血条UI对象拖拽到此字段

    [Header("预加载设置")]
    [Tooltip("将需要提前加载以避免卡顿的预制件（如护盾特效）拖到这里")]
    public List<GameObject> prefabsToPreload;

    void Start()
    {
        if (playerInstance == null || virtualCamera == null || playerHealthUI == null)
        {
            Debug.LogError("调试场景初始化失败：请在Inspector中设置Player、Virtual Camera 和 PlayerHealthUI 的引用！");
            return;
        }

        // --- 核心集成代码：在这里关联UI和玩家状态 ---
        Health playerHealth = playerInstance.GetComponent<Health>();
        PlayerShield playerShield = playerInstance.GetComponent<PlayerShield>();

        if (playerHealth != null && playerShield != null)
        {
            playerHealthUI.Initialize(playerHealth, playerShield);
            Debug.Log("PlayerHealthUI 已成功与调试玩家实例关联。");
        }
        else
        {
            Debug.LogWarning("未能关联 PlayerHealthUI，请检查玩家预制件上是否有Health和PlayerShield脚本。");
        }
        // --- 集成代码结束 ---

        // 1. 手动通知 GameManager 玩家已就绪
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerMechReadyInCombatScene(playerInstance);
        }

        // 2. 手动初始化 UIManager 的引用
        if (UIManager.Instance != null)
        {
            UIManager.Instance.InitializeCombatUIReferences();
        }

        // 3. 手动设置相机目标
        virtualCamera.Follow = playerInstance.transform;
        virtualCamera.LookAt = playerInstance.transform;
        virtualCamera.enabled = true;

        Debug.Log("--- 调试场景初始化完毕 ---");
    }
    private void PreloadAssets()
    {
        if (prefabsToPreload == null || prefabsToPreload.Count == 0)
        {
            return;
        }

        // 定义一个远离主摄像机的预加载位置
        Vector3 preloadPosition = new Vector3(0, -1000, 0);

        Debug.Log($"开始预加载 {prefabsToPreload.Count} 个资源...");
        foreach (GameObject prefab in prefabsToPreload)
        {
            if (prefab != null)
            {
                // 在屏幕外实例化，然后立即销毁。
                // 这个操作会强制Unity将Prefab及其所有依赖项加载到内存中。
                GameObject instance = Instantiate(prefab, preloadPosition, Quaternion.identity);
                Destroy(instance);
            }
        }
        Debug.Log("资源预加载完毕。");
    }
}