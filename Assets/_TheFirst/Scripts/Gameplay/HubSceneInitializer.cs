using Cinemachine;
using UnityEngine;

public class HubSceneInitializer : MonoBehaviour
{
    [Header("默认角色设置")]
    [Tooltip("将用作初始角色的CharacterData资产拖拽到这里")]
    public CharacterData defaultCharacterData;

    [Header("场景引用")]
    [Tooltip("玩家的出生点位置")]
    public Transform playerSpawnPoint;
    [Tooltip("将场景中的枢纽虚拟相机拖到这里")]
    public CinemachineVirtualCamera hubCamera; // 【新增】

    void Start()
    {
        // --- 安全检查 ---
        if (defaultCharacterData == null)
        {
            Debug.LogError("没有设置默认角色数据！请在HubSceneInitializer的Inspector中设置。", this);
            return;
        }
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
        if (defaultCharacterData.chassisPrefab == null)
        {
            Debug.LogError("默认角色数据中没有设置角色预制件 (characterPrefab)！", this);
            return;
        }

        // --- 核心逻辑 ---

        // 1. 将默认角色数据存入 DataManager
        //    这取代了之前在角色选择界面所做的事情
        DataManager.Instance.selectedCharacter = defaultCharacterData;
        Debug.Log($"已使用默认角色: {DataManager.Instance.selectedCharacter.characterName} 初始化游戏。");

        // 2. 在指定的出生点生成玩家角色
        GameObject playerInstance = Instantiate(defaultCharacterData.chassisPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);

        // --- 【新增】禁用所有武器逻辑 ---
        // 在这个非战斗场景中，我们不希望武器自动攻击。
        Debug.Log("正在禁用枢纽场景中的武器攻击功能...");

        // 获取玩家实例上所有的 WeaponPart 组件（包括子物体中的）
        WeaponPart[] weapons = playerInstance.GetComponentsInChildren<WeaponPart>();
        foreach (WeaponPart weapon in weapons)
        {
            weapon.enabled = false;
            Debug.Log($"已禁用通用武器部件: {weapon.gameObject.name}");
        }

        // b) 【新增】禁用特定的攻击控制脚本 (例如您的刀光攻击)
        // 这是解决您问题的关键，它会阻止脚本动态生成浮游武器
        PlayerBladeAttack[] bladeAttacks = playerInstance.GetComponentsInChildren<PlayerBladeAttack>();
        foreach (PlayerBladeAttack bladeAttack in bladeAttacks)
        {
            bladeAttack.enabled = false;
            Debug.Log($"已禁用特定的刀光攻击逻辑: {bladeAttack.gameObject.name}");
        }
        if (hubCamera != null)
        {
            // 将虚拟相机的“跟随”和“看向”目标都设置为新生成的玩家实例
            hubCamera.Follow = playerInstance.transform;
            hubCamera.LookAt = playerInstance.transform;
            Debug.Log("枢纽相机已关联到玩家。");
        }
        else
        {
            Debug.LogWarning("未在HubSceneInitializer中设置枢纽相机！", this);
        }

        // 3. （可选）如果玩家生成后需要进一步初始化（例如设置初始血量、武器），
        //    可以在这里或在玩家预制件的Awake/Start方法中完成。
        //    您现有的玩家生成逻辑应该已经处理了这一点。
    }
}