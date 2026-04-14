using Cinemachine;
using UnityEngine;

/// <summary>
/// HubScene初始化器 — 在Hub场景加载时生成玩家角色
/// 支持角色切换：监听 CharacterSelectManager.OnCharacterSelected 事件
/// </summary>
public class HubSceneInitializer : MonoBehaviour
{
    [Header("默认角色设置")]
    [Tooltip("将用作回退角色的CharacterData资产拖拽到这里")]
    public CharacterData defaultCharacterData;

    [Header("场景引用")]
    [Tooltip("玩家的出生点位置")]
    public Transform playerSpawnPoint;
    [Tooltip("将场景中的枢纽虚拟相机拖到这里")]
    public CinemachineVirtualCamera hubCamera;

    [Header("角色选择UI")]
    [Tooltip("场景中的角色选择面板管理器")]
    public CharacterSelectManager characterSelectManager;

    // 当前生成的玩家实例
    private GameObject currentPlayerInstance;

    void Start()
    {
        // --- 安全检查 ---
        if (playerSpawnPoint == null)
        {
            Debug.LogError("没有设置玩家出生点！请在HubSceneInitializer的Inspector中设置。", this);
            return;
        }
        if (DataManager.Instance == null)
        {
            Debug.LogError("DataManager 未找到！无法初始化玩家。", this);
            return;
        }

        // --- 恢复上次选择的角色 ---
        CharacterData characterToSpawn = DataManager.Instance.ResolveSelectedCharacter();

        // 如果还是找不到，用默认角色回退
        if (characterToSpawn == null)
        {
            characterToSpawn = defaultCharacterData;
            if (characterToSpawn != null)
            {
                DataManager.Instance.selectedCharacter = characterToSpawn;
                DataManager.Instance.selectedCharacterID = characterToSpawn.characterID;
            }
        }

        if (characterToSpawn == null)
        {
            Debug.LogError("没有可用的角色数据！请检查 DataManager 或设置默认角色。", this);
            return;
        }

        // 生成玩家
        SpawnPlayer(characterToSpawn);

        // 注册角色切换事件（如果未手动拖入，自动查找）
        if (characterSelectManager == null)
            characterSelectManager = Object.FindFirstObjectByType<CharacterSelectManager>();
        if (characterSelectManager != null)
        {
            characterSelectManager.OnCharacterSelected += OnCharacterSwitched;
        }
        else
        {
            Debug.LogWarning("[Hub] 未找到 CharacterSelectManager，角色切换功能不可用。");
        }
    }

    void OnDestroy()
    {
        // 取消注册事件
        if (characterSelectManager != null)
        {
            characterSelectManager.OnCharacterSelected -= OnCharacterSwitched;
        }
    }

    /// <summary>
    /// 生成玩家角色
    /// </summary>
    private void SpawnPlayer(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogError("[Hub] SpawnPlayer: characterData 为 null！");
            return;
        }
        if (characterData.chassisPrefab == null)
        {
            Debug.LogError($"角色 '{characterData.characterName}' 没有设置角色预制件 (chassisPrefab)！", this);
            return;
        }

        // 记录旧角色的位置和朝向（切换角色时在原地生成新角色）
        Vector3 spawnPos = playerSpawnPoint.position;
        Quaternion spawnRot = playerSpawnPoint.rotation;
        if (currentPlayerInstance != null)
        {
            spawnPos = currentPlayerInstance.transform.position;
            spawnRot = currentPlayerInstance.transform.rotation;
            // 【关键】使用 DestroyImmediate 立即销毁旧角色
            // Destroy 是帧末延迟执行的，导致新角色 Instantiate 时旧角色的单例引用仍存在
            // 新角色的 Awake() 检测到单例 Instance != null → 直接自毁
            // DestroyImmediate 立即销毁 → 触发 OnDestroy 清除单例 → 新角色 Awake 正常注册
            DestroyImmediate(currentPlayerInstance);
            currentPlayerInstance = null;
        }

        // 生成新角色（在旧角色位置或出生点）
        currentPlayerInstance = Instantiate(characterData.chassisPrefab, spawnPos, spawnRot);

        if (currentPlayerInstance == null)
        {
            Debug.LogError("[Hub] Instantiate 返回 null！生成角色失败！");
            return;
        }

        // 禁用武器攻击逻辑（Hub中不需要）
        DisableWeapons(currentPlayerInstance);

        // 关联相机
        if (hubCamera != null)
        {
            hubCamera.Follow = currentPlayerInstance.transform;
            hubCamera.LookAt = currentPlayerInstance.transform;
        }
    }

    /// <summary>
    /// 禁用Hub中的武器和攻击逻辑
    /// </summary>
    private void DisableWeapons(GameObject playerInstance)
    {
        // 禁用 WeaponController，防止武器注册和自动开火
        WeaponController wc = playerInstance.GetComponentInChildren<WeaponController>();
        if (wc != null)
        {
            wc.enabled = false;
        }

        // 隐藏并禁用所有武器
        WeaponPart[] weapons = playerInstance.GetComponentsInChildren<WeaponPart>(true);
        foreach (WeaponPart weapon in weapons)
        {
            weapon.enabled = false;
            // 如果武器是独立子物体（不是玩家根对象），隐藏它
            if (weapon.gameObject != playerInstance)
            {
                weapon.gameObject.SetActive(false);
            }
        }

        // 禁用近战攻击
        PlayerBladeAttack[] bladeAttacks = playerInstance.GetComponentsInChildren<PlayerBladeAttack>(true);
        foreach (PlayerBladeAttack bladeAttack in bladeAttacks)
        {
            bladeAttack.enabled = false;
        }
    }

    /// <summary>
    /// 在销毁旧角色前，清除其身上所有单例的 Instance 引用
    /// 否则新角色 Instantiate 时，单例 Awake() 检测到 Instance 已存在，
    /// 会调用 Destroy(gameObject) 把新角色自己整个销毁
    /// </summary>
    private void ClearPlayerSingletons(GameObject oldPlayer)
    {
        // PlayerStats 单例
        PlayerStats ps = oldPlayer.GetComponent<PlayerStats>();
        if (ps != null && PlayerStats.Instance == ps)
        {
            // 通过反射或直接设置来清除（PlayerStats.Instance 是 private set）
            // 这里我们直接让旧的 OnDestroy 时不再是 Instance
        }

        // WeaponController 单例
        WeaponController wc = oldPlayer.GetComponentInChildren<WeaponController>();
        if (wc != null && WeaponController.Instance == wc)
        {
        }

        // PlayerShield 单例
        PlayerShield shield = oldPlayer.GetComponent<PlayerShield>();
        if (shield != null && PlayerShield.Instance == shield)
        {
        }
    }

    /// <summary>
    /// 角色切换回调 — 当玩家在角色选择面板中选择了新角色
    /// </summary>
    private void OnCharacterSwitched(CharacterData newCharacter)
    {
        if (newCharacter == null) return;

        SpawnPlayer(newCharacter);
    }
}