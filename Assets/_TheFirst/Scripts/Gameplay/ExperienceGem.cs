using UnityEngine;
using UnityEngine.Events;
public class ExperienceGem : MonoBehaviour
{
    [Header("经验值设置")]
    [Tooltip("此宝石提供的经验值")]
    public int experienceAmount = 1;

    [Header("拾取设置")]
    [Tooltip("玩家靠近到多少距离内开始被吸引")]
    public float magnetRadius = 4f;
    [Tooltip("飞向玩家的速度")]
    public float collectionSpeed = 8f;

    private Transform playerTransform; // 玩家 (MechRoot) 的 Transform
    private bool isCollecting = false; // 是否正在被玩家吸引
    private PlayerLevelManager foundLevelManager; // 缓存找到的 Level Manager


    void Start()
    {
       
    }

    void Update()
    {
        // 如果还没找到玩家，或者玩家在 Build 模式下被清除了，就尝试重新获取
        if (playerTransform == null || foundLevelManager == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.Combat && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
                // 尝试从找到的 playerTransform (MechRoot) 获取 LevelManager
                foundLevelManager = playerTransform.GetComponent<PlayerLevelManager>();
                if (foundLevelManager == null)
                {
                    Debug.LogError("在玩家 Transform 上未找到 PlayerLevelManager 组件!", playerTransform);
                    // 如果玩家确实没有这个脚本，宝石将无法被拾取
                }
            }
            if (playerTransform == null || foundLevelManager == null) return; // 如果仍未找到，则不执行后续逻辑
        }

        // --- 处理靠近和吸附 ---
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position); // 可以用完整距离了

        // 如果不在收集状态，且玩家进入了吸引半径，则开始收集
        if (!isCollecting && distanceToPlayer <= magnetRadius)
        {
            isCollecting = true;
            // (可选) 可以在这里禁用物理交互，如果宝石有 Rigidbody 的话
            // Rigidbody rb = GetComponent<Rigidbody>();
            // if (rb != null) { rb.isKinematic = true; rb.velocity = Vector3.zero; }
        }

        // 如果正在收集状态，则飞向玩家
        if (isCollecting)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, collectionSpeed * Time.deltaTime);
            // 当非常接近时直接收集，防止穿透或抖动
            if (distanceToPlayer < 0.5f)
            { // 调整这个距离阈值
                Collect(); // 直接调用收集，不再依赖 OnTriggerEnter
            }
        }
    }

        // --- 触发器检测 ---
        void OnTriggerEnter(Collider other)
        {
            // 检查进入触发器的是否是玩家的 LevelManager (或其子对象)
            // 注意：这里的 other 可能是 ChassisCore 或其零件
            if (foundLevelManager != null && other.GetComponentInParent<PlayerLevelManager>() == foundLevelManager)
            {
                Collect(); // 调用统一的拾取逻辑
            }
        }

        // 拾取逻辑
        void Collect()
        {
            if (foundLevelManager != null)
            {
                foundLevelManager.AddExperience(experienceAmount);
                Debug.Log($"经验宝石已被拾取，增加了 {experienceAmount} XP"); // 使用中文日志
            }
            else
            {
                // 理论上在 Update 中已经确保 foundLevelManager 不是 null 了
                Debug.LogWarning("尝试收集经验，但未能找到 PlayerLevelManager 引用！");
            }

            // 销毁自身
            Destroy(gameObject);
        }
    }