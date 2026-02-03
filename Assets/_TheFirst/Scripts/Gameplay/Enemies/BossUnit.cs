using UnityEngine;

// 这个脚本专门挂在 BOSS 预制体上
// 它的作用只有两个：
// 1. 告诉 UI "我来了，显示血条"
// 2. 告诉 UI "我死了/消失了，隐藏血条"
public class BossUnit : MonoBehaviour
{
    [Header("Boss 信息配置")]
    public string bossName = "终极机甲·原型机"; // 你可以在 Inspector 里随便改名字

    private Health myHealth;

    void Start()
    {
        // 1. 获取自身的 Health 组件
        // (前提是你的 Boss 身上必须挂着通用的 Health 脚本)
        myHealth = GetComponent<Health>();

        if (myHealth == null)
        {
            Debug.LogError($"[BossUnit] 错误：在 {name} 身上找不到 Health 组件！血条无法初始化。");
            return;
        }

        // 2. 呼叫 UI 单例，把自己的血量数据传过去
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.InitializeBossBar(myHealth, bossName);
        }
        else
        {
            // 如果场景里忘了放 UI Canvas，这里会提醒你
            Debug.LogWarning("场景里找不到 BossHealthBarUI！请检查是否创建了 Canvas/BossHealthPanel。");
        }
    }

    void OnDestroy()
    {
        // 当 Boss 被销毁时（无论是被打死还是被代码删掉），通知 UI 关闭血条
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBossBar();
        }
    }
}