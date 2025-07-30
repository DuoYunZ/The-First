// 在 DataManager.cs (或者你的 GameManager 脚本) 中
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; } // 单例模式方便访问
    public CharacterData selectedCharacter;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 使其在加载新场景时不被销毁
        }
        else
        {
            Destroy(gameObject); // 销毁重复的实例
        }
    }
}