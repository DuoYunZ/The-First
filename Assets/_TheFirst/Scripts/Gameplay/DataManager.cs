using UnityEngine;
using System.Collections.Generic;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; } // 单例模式方便访问

    [Header("角色数据")]
    [Tooltip("所有角色数据列表（拖入所有 CharacterData SO）")]
    public List<CharacterData> allCharacters = new List<CharacterData>();

    /// <summary>
    /// 当前选中的角色数据（跨场景传递）
    /// </summary>
    public CharacterData selectedCharacter;

    /// <summary>
    /// 持久化记住玩家上次选择的角色ID
    /// </summary>
    public string selectedCharacterID;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 从 PlayerProgressManager 缓存中恢复上次选择的角色
            // （PPM 可能比 DM 更早 Awake，已将角色ID缓存到 savedSelectedCharacterID）
            if (PlayerProgressManager.Instance != null &&
                !string.IsNullOrEmpty(PlayerProgressManager.Instance.savedSelectedCharacterID))
            {
                selectedCharacterID = PlayerProgressManager.Instance.savedSelectedCharacterID;
                Debug.Log($"[DataManager] 从存档恢复角色选择: {selectedCharacterID}");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 根据 characterID 查找对应的 CharacterData
    /// </summary>
    public CharacterData GetCharacterByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var c in allCharacters)
        {
            if (c != null && c.characterID == id) return c;
        }
        return null;
    }

    /// <summary>
    /// 获取默认解锁的角色（第一个 isDefaultUnlocked 为 true 的）
    /// </summary>
    public CharacterData GetDefaultCharacter()
    {
        foreach (var c in allCharacters)
        {
            if (c != null && c.isDefaultUnlocked) return c;
        }
        // 回退：返回列表第一个
        return allCharacters.Count > 0 ? allCharacters[0] : null;
    }

    /// <summary>
    /// 尝试恢复上次选择的角色，如果找不到则使用默认角色
    /// </summary>
    public CharacterData ResolveSelectedCharacter()
    {
        // 优先用已设置的 selectedCharacter
        if (selectedCharacter != null) return selectedCharacter;

        // 尝试根据存档的ID恢复
        if (!string.IsNullOrEmpty(selectedCharacterID))
        {
            CharacterData found = GetCharacterByID(selectedCharacterID);
            if (found != null)
            {
                selectedCharacter = found;
                return found;
            }
        }

        // 回退到默认角色
        CharacterData defaultChar = GetDefaultCharacter();
        selectedCharacter = defaultChar;
        if (defaultChar != null) selectedCharacterID = defaultChar.characterID;
        return defaultChar;
    }
}