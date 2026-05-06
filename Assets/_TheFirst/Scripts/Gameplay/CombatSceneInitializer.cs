// CombatSceneInitializer.cs (最终概念统一版)
using Cinemachine;
using System.Collections;
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
        // 【核心新增】根据当前角色重新计算技能树加成，确保不会使用其他角色的属性
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.RecalculateCharacterBonuses(characterData);
        }

        if (characterData.chassisPrefab == null)
        {
            Debug.LogError($"CharacterData '{characterData.name}' is missing its Prefab!");
            return;
        }

        GameObject playerInstance = Instantiate(characterData.chassisPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        playerInstance.name = characterData.characterName + "_RuntimeInstance";
        // --- 核心集成代码：在这里关联UI和玩家状态 ---
        Health playerHealth = playerInstance.GetComponent<Health>();
        PlayerShield playerShield = playerInstance.GetComponent<PlayerShield>();

        if (playerHealthUI != null && playerHealth != null && playerShield != null)
        {
            playerHealthUI.Initialize(playerHealth, playerShield);
        }
        else
        {
            Debug.LogWarning("未能关联 PlayerHealthUI，请检查是否已在Inspector中拖拽引用，以及玩家预制件上是否有Health和PlayerShield脚本。");
        }
        // --- 集成代码结束 ---

        WeaponController weaponController = playerInstance.GetComponentInChildren<WeaponController>();
        if (weaponController != null && characterData.initialWeapons != null)
        {
            // 法师分支武器替换：检测是否需要替换预制件上自带的武器
            bool shouldReplaceBranch = characterData.alternateStartWeapon != null
                && !string.IsNullOrEmpty(characterData.alternateStartMechanicID)
                && PlayerProgressManager.Instance != null
                && PlayerProgressManager.Instance.HasMechanic(characterData.alternateStartMechanicID);

            // 如果预制件上有内置武器且需要分支替换，直接替换其 StatBlock
            if (shouldReplaceBranch && weaponController.builtInBladeWeapon != null)
            {
                WeaponPart builtIn = weaponController.builtInBladeWeapon;
                string oldName = builtIn.StatBlock != null ? builtIn.StatBlock.weaponName : "无";
                builtIn.myStatBlock = characterData.alternateStartWeapon;
                Debug.Log($"<color=cyan>[法师分支] 替换预制件内置武器: {oldName} → {characterData.alternateStartWeapon.weaponName}</color>");
            }

            // 自动激活分支技能（IcePath/FirePath），无需抽卡
            if (UpgradeManager.Instance != null && !string.IsNullOrEmpty(characterData.alternateStartMechanicID))
            {
                if (shouldReplaceBranch)
                {
                    // 选了替换分支（冰锥之路）
                    UpgradeManager.Instance.ForceActivateCharacterSkill("IcePath");
                    Debug.Log("<color=cyan>[法师分支] 自动激活 IcePath 技能</color>");
                }
                else
                {
                    // 保持默认分支（火球之路）
                    UpgradeManager.Instance.ForceActivateCharacterSkill("FirePath");
                    Debug.Log("<color=orange>[法师分支] 自动激活 FirePath 技能</color>");
                }
            }

            // 构建实际初始武器列表
            List<WeaponStatBlock> actualWeapons = new List<WeaponStatBlock>(characterData.initialWeapons);

            foreach (WeaponStatBlock weaponStat in actualWeapons)
            {
                if (weaponStat == null) continue;

                // 跳过预制件已自带的内置武器（已被替换或原始的），防止重复添加
                if (weaponController.builtInBladeWeapon != null &&
                    weaponController.builtInBladeWeapon.StatBlock != null &&
                    weaponController.builtInBladeWeapon.StatBlock.weaponName == weaponStat.weaponName)
                {
                    continue;
                }

                // 如果分支替换了，也跳过旧武器（防止被替换的火球又从 initialWeapons 添加回来）
                if (shouldReplaceBranch && characterData.alternateStartWeapon != null
                    && weaponStat.weaponName != characterData.alternateStartWeapon.weaponName)
                {
                    // 检查这个武器是否是被替换掉的原始武器
                    // （如果 initialWeapons 里有火球，但分支已换成冰锥，就跳过火球）
                    bool isOriginalWeapon = false;
                    if (weaponController.builtInBladeWeapon != null
                        && weaponController.builtInBladeWeapon.StatBlock == characterData.alternateStartWeapon)
                    {
                        // builtInBladeWeapon 已经换成了新的，所以旧的武器ID需要跳过
                        // 通过检查 initialWeapons 的第一个是否就是这个来判断
                        if (characterData.initialWeapons.Count > 0
                            && characterData.initialWeapons[0] == weaponStat
                            && weaponStat != characterData.alternateStartWeapon)
                        {
                            isOriginalWeapon = true;
                        }
                    }
                    if (isOriginalWeapon) continue;
                }

                weaponController.AddNewWeapon(weaponStat);
            }

            // 法师特性：初始武器自动解锁大招（无需5颗宝石）
            if (characterData.autoUnlockInitialUltimate)
            {
                // 延迟一帧执行，确保武器完全初始化
                StartCoroutine(AutoUnlockInitialUltimate(weaponController));
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

    }
    private void PreloadAssets()
    {
        if (prefabsToPreload == null || prefabsToPreload.Count == 0)
        {
            return;
        }

        // 定义一个远离主摄像机的预加载位置
        Vector3 preloadPosition = new Vector3(0, -1000, 0);

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
    }

    /// <summary>
    /// 法师特性：延迟一帧后自动解锁初始武器的大招
    /// </summary>
    private IEnumerator AutoUnlockInitialUltimate(WeaponController weaponController)
    {
        yield return null; // 等待一帧，确保武器完全初始化

        foreach (var owned in weaponController.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && !owned.weaponPartInstance.isUltimateUnlocked)
            {
                WeaponPart part = owned.weaponPartInstance;
                part.isUltimateUnlocked = true;
                part.currentEnergy = part.StatBlock.maxEnergy;
                part.OnEnergyChanged?.Invoke(part.currentEnergy, part.StatBlock.maxEnergy);
                part.OnEnergyFull?.Invoke(part);

                Debug.Log($"<color=cyan>[法师亲和] {part.StatBlock.weaponName} 大招已自动解锁并充满能量</color>");
            }
        }

        // 内置武器也检查一下
        if (weaponController.builtInBladeWeapon != null && !weaponController.builtInBladeWeapon.isUltimateUnlocked)
        {
            WeaponPart blade = weaponController.builtInBladeWeapon;
            blade.isUltimateUnlocked = true;
            blade.currentEnergy = blade.StatBlock.maxEnergy;
            blade.OnEnergyChanged?.Invoke(blade.currentEnergy, blade.StatBlock.maxEnergy);
            blade.OnEnergyFull?.Invoke(blade);
        }
    }
}