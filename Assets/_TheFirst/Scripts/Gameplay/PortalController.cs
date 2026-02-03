using UnityEngine;
using UnityEngine.SceneManagement; // 【关键】引入场景管理所需的命名空间

public class PortalController : MonoBehaviour
{
    [Header("传送设置")]
    [Tooltip("要加载的战斗场景的名称")]
    public string sceneToLoad = "CombatArena01"; // 默认设置为您的战斗场景名

    [Header("视觉与交互")]
    [Tooltip("（可选）当玩家进入时，需要关闭的视觉特效")]
    public GameObject portalVisuals;
    [Tooltip("（可选）当玩家进入时，播放的传送特效")]
    public GameObject activationEffectPrefab;

    private bool isActivated = false; // 防止重复触发

    /// <summary>
    /// 当有其他碰撞体进入这个触发器时，Unity会自动调用这个方法。
    /// </summary>
    /// <param name="other">进入触发器的另一个物体的碰撞体</param>
    private void OnTriggerEnter(Collider other)
    {
        // 如果已经触发过，或者进入的不是玩家，则直接返回
        if (isActivated || !other.CompareTag("Player"))
        {
            return;
        }

        // 检查进入的是否是玩家（需要确保您的玩家预制件的Tag被设置为了"Player"）
        Debug.Log("玩家已进入传送门，准备传送...");
        isActivated = true; // 标记为已激活

        // 开始传送流程
        StartCoroutine(TeleportSequence());
    }

    private System.Collections.IEnumerator TeleportSequence()
    {
        // 1. （可选）播放传送特效
        if (portalVisuals != null) portalVisuals.SetActive(false);
        if (activationEffectPrefab != null) Instantiate(activationEffectPrefab, transform.position, Quaternion.identity);

        // 2. （可选）给特效一点播放时间，或者做一个屏幕淡出效果
        // 简单的做法是直接等待一小段时间
        yield return new WaitForSeconds(1.0f); // 等待1秒

        // 3. 核心：加载战斗场景
        // 在加载前，DataManager中已经保存了我们在HubScene中初始化的角色数据
        Debug.Log($"正在加载场景: {sceneToLoad}...");
        SceneManager.LoadScene(sceneToLoad);
    }
}