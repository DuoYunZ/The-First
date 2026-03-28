// CombatSceneInitializer.cs (最终概念统一版)
using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class CombatSceneInitializer : MonoBehaviour
{
    [Header("场景引用")]
    public Transform playerSpawnPoint;
    public CinemachineVirtualCamera combatCamera;
    public EnemySpawner enemySpawner;
    public WaveManager waveManager;

    [Header("预加载设置")]
    [Tooltip("将需要提前加载以避免卡顿的预制件（如护盾特效）拖到这里")]
    public List<GameObject> prefabsToPreload;

    // --- 新增引用 ---
    [Header("UI 引用")]
    [Tooltip("将场景中承载 PlayerHealthUI 脚本的UI对象拖到这里")]
    public PlayerHealthUI playerHealthUI; // 在Unity Inspector中将你的血条UI对象拖拽到此字段

    void Start()
    {
        PreloadAssets();

        if (DataManager.Instance == null || DataManager.Instance.selectedCharacter == null)
        {
            Debug.LogError("CombatSceneInitializer: CharacterData not found!");
            // --- 新增 ---
            if (playerHealthUI != null) playerHealthUI.gameObject.SetActive(false); // 如果没有角色数据，隐藏血条
            return;
        }

        CharacterData characterToSpawn = DataManager.Instance.selectedCharacter;
        InitializeCombatScene(characterToSpawn);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.InitializeCombatUIReferences();
        }
    }

    private void InitializeCombatScene(CharacterData characterData)
    {
        if (characterData.chassisPrefab == null)
        {
            Debug.LogError($"CharacterData '{characterData.name}' is missing its Prefab!");
            return;
        }

        GameObject playerInstance = Instantiate(characterData.chassisPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        playerInstance.name = characterData.characterName + "_RuntimeInstance";
        Debug.Log($"已生成角色: {playerInstance.name}");

        // --- 核心集成代码：在这里关联UI和玩家状态 ---
        Health playerHealth = playerInstance.GetComponent<Health>();
        PlayerShield playerShield = playerInstance.GetComponent<PlayerShield>();

        if (playerHealthUI != null && playerHealth != null && playerShield != null)
        {
            playerHealthUI.Initialize(playerHealth, playerShield);
            Debug.Log("PlayerHealthUI 已成功与玩家实例关联。");
        }
        else
        {
            Debug.LogWarning("未能关联 PlayerHealthUI，请检查是否已在Inspector中拖拽引用，以及玩家预制件上是否有Health和PlayerShield脚本。");
        }
        // --- 集成代码结束 ---

        WeaponController weaponController = playerInstance.GetComponentInChildren<WeaponController>();
        if (weaponController != null && characterData.initialWeapons != null)
        {
            foreach (WeaponStatBlock weaponStat in characterData.initialWeapons)
            {
                if (weaponStat == null) continue;

                // 跳过预制件已自带的内置武器，防止重复添加
                if (weaponController.builtInBladeWeapon != null &&
                    weaponController.builtInBladeWeapon.StatBlock != null &&
                    weaponController.builtInBladeWeapon.StatBlock.weaponName == weaponStat.weaponName)
                {
                    Debug.Log($"[CombatInit] 跳过内置武器: {weaponStat.weaponName}（预制件已自带）");
                    continue;
                }

                weaponController.AddNewWeapon(weaponStat);
            }
        }

        if (combatCamera != null)
        {
            combatCamera.Follow = playerInstance.transform;
            combatCamera.LookAt = playerInstance.transform;
            combatCamera.enabled = true;
        }

        if (enemySpawner != null) enemySpawner.gameObject.SetActive(true);
        if (waveManager != null) waveManager.enabled = true;
        if (GameManager.Instance != null) GameManager.Instance.PlayerMechReadyInCombatScene(playerInstance);

        Debug.Log("CombatSceneInitializer: 所有战斗系统初始化完毕。");
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