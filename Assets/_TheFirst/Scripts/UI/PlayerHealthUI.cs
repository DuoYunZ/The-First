using UnityEngine;
using UnityEngine.UI; // 需要引入 UI 命名空间

[RequireComponent(typeof(Slider))] // 确保脚本挂载的对象有 Slider 组件
public class PlayerHealthUI : MonoBehaviour
{
    private Slider healthSlider;
    private Health playerHealth; // 玩家 Health 组件的引用

    void Awake()
    {
        healthSlider = GetComponent<Slider>(); // 获取同一个对象上的 Slider 组件
    }

    void Update()
    {
        // --- 持续查找玩家 Health 组件 (直到找到) ---
        // 因为玩家 MechRoot 可能在游戏开始后才激活
        if (playerHealth == null)
        {
            // 尝试通过 GameManager 获取 (如果 GameManager 单例可用且玩家存在)
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerHealth = GameManager.Instance.playerTransform.GetComponent<Health>();
                if (playerHealth != null)
                {
                    InitializeSlider(); // 找到后初始化 Slider
                }
            }
            // 如果没找到，下一帧继续找...
            if (playerHealth == null) return; // 本帧不更新 UI
        }
        // ------------------------------------------

        // --- 如果找到了玩家 Health，则更新 Slider 值 ---
        if (healthSlider != null) // 确保 Slider 存在
        {
            // 平滑更新或直接更新
            healthSlider.value = playerHealth.GetCurrentHealth();
            // 或者带点平滑效果:
            // healthSlider.value = Mathf.Lerp(healthSlider.value, playerHealth.GetCurrentHealth(), Time.deltaTime * 10f);
        }
    }

    // 初始化 Slider 最大值等设置
    void InitializeSlider()
    {
        if (healthSlider != null && playerHealth != null)
        {
            healthSlider.maxValue = playerHealth.GetMaxHealth();
            healthSlider.value = playerHealth.GetCurrentHealth(); // 设置初始血量
            Debug.Log("PlayerHealthUI Initialized. MaxHealth: " + healthSlider.maxValue);
        }
    }

    // (可选) 当玩家对象销毁时，可能需要隐藏或处理 Slider
    // 可以通过 Health 的 OnDeath 事件来处理
}