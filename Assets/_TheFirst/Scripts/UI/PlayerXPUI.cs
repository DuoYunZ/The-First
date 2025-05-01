using UnityEngine;
using UnityEngine.UI; // 用于 Slider
using TMPro; // 用于 TextMeshPro

public class PlayerXPUI : MonoBehaviour // 建议将此脚本挂载在 CombatUIContainer 或一个专门的 PlayerStatsUI 对象上
{
    [Header("UI 引用 (在 Inspector 中指定)")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("玩家数据源")]
    [Tooltip("（可选）手动指定玩家 Level Manager。如果为空，会自动查找。")]
    [SerializeField] private PlayerLevelManager levelManager;

    private bool isInitialized = false;

    void Update()
    {
        // 如果 levelManager 还未找到或初始化，则尝试查找
        if (levelManager == null)
        {
            // 尝试通过 GameManager 获取 (推荐)
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                levelManager = GameManager.Instance.playerTransform.GetComponent<PlayerLevelManager>();
                if (levelManager != null)
                {
                    Debug.Log("PlayerXPUI found PlayerLevelManager via GameManager.");
                }
            }
            // 或者全局查找 (效率较低)
            // levelManager = FindObjectOfType<PlayerLevelManager>();

            if (levelManager == null)
            {
                // Debug.LogWarning("PlayerXPUI waiting for PlayerLevelManager...");
                return; // 如果还是没找到，等待下一帧
            }
        }

        // 如果找到了 Level Manager，更新 UI
        if (xpSlider != null)
        {
            // 确保 maxValue 不是 0，避免除零错误
            int xpToNext = levelManager.GetXPToNextLevel();
            xpSlider.maxValue = xpToNext > 0 ? xpToNext : 1; // 防止为 0
            xpSlider.value = levelManager.GetCurrentXP();
        }

        if (levelText != null)
        {
            levelText.text = "Level: " + levelManager.GetLevel();
        }
    }
}