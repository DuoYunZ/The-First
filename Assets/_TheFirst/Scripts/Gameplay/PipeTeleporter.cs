using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 管道传送器 — 玩家进入管道触发器后，弹出地图关卡选择界面，
/// 选择关卡后执行吸入动画并传送到对应战斗场景
/// </summary>
public class PipeTeleporter : MonoBehaviour
{
    [Header("传送设置")]
    [Tooltip("默认场景名（仅在没有 LevelSelectUI 时作为回退使用）")]
    public string fallbackScene = "CombatArena01";

    [Header("动画控制")]
    [Tooltip("管道入口中心点（角色会被吸向这个点）")]
    public Transform pipeEntrance;
    [Tooltip("吸入动画的持续时间")]
    public float suckDuration = 1.0f;
    [Tooltip("旋转速度（越高转得越快）")]
    public float spinSpeed = 1080f;

    [Header("音效与视觉")]
    [Tooltip("吸入时的音效")]
    public AudioClip suckSound;
    private AudioSource audioSource;

    private bool isActivated = false;

    // 缓存玩家 Transform，用于关卡选择确认后执行吸入动画
    private Transform cachedPlayerTransform;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // 如果没有指定入口中心点，默认使用自身位置向上偏一点
        if (pipeEntrance == null)
        {
            var entranceObj = new GameObject("PipeEntrance_Auto");
            entranceObj.transform.SetParent(transform);
            entranceObj.transform.localPosition = new Vector3(0, 1.5f, 0); // 假设管道口在上方 1.5 米处
            pipeEntrance = entranceObj.transform;
            Debug.LogWarning("未指定管道入口中心点 pipeEntrance，已自动生成一个默认位置点，请在 Inspector 中调整。", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivated || !other.CompareTag("Player"))
        {
            return;
        }

        isActivated = true;
        cachedPlayerTransform = other.transform;

        // 尝试打开地图关卡选择界面
        if (LevelSelectUI.Instance != null)
        {
            // 打开地图选择UI，传入回调：玩家确认关卡后执行传送
            LevelSelectUI.Instance.Show(OnLevelSelected);
        }
        else
        {
            // 没有地图选择UI，直接用默认场景传送（兼容旧行为）
            Debug.LogWarning("[PipeTeleporter] 未找到 LevelSelectUI，将直接传送到默认场景。", this);
            StartCoroutine(SuckInSequence(cachedPlayerTransform, fallbackScene));
        }
    }

    /// <summary>
    /// 玩家在地图UI中确认选择关卡后的回调
    /// </summary>
    /// <param name="targetScene">目标场景名称</param>
    private void OnLevelSelected(string targetScene)
    {
        if (cachedPlayerTransform == null)
        {
            Debug.LogError("[PipeTeleporter] 缓存的玩家 Transform 为空！无法执行传送动画。");
            isActivated = false;
            return;
        }

        // 开始吸入动画 + 传送
        StartCoroutine(SuckInSequence(cachedPlayerTransform, targetScene));
    }

    /// <summary>
    /// 吸入动画序列：禁用玩家控制 → 播放音效 → 缩小旋转移动到管道中心 → 加载场景
    /// </summary>
    private IEnumerator SuckInSequence(Transform playerTransform, string sceneToLoad)
    {
        // 1. 禁用玩家物理与控制
        var rb = playerTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
        }
        
        var charController = playerTransform.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }
        
        // 发送消息禁用角色控制器的移动逻辑
        playerTransform.SendMessage("DisableMovement", SendMessageOptions.DontRequireReceiver);

        // 2. 播放音效
        if (suckSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(suckSound);
        }

        // 3. 开始补间动画 - 缩放、移动、旋转
        Vector3 startPos = playerTransform.position;
        Vector3 targetPos = pipeEntrance.position;
        
        Vector3 startScale = playerTransform.localScale;
        Vector3 targetScale = Vector3.zero;

        float elapsedTime = 0f;

        while (elapsedTime < suckDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / suckDuration;

            // 使用平滑曲线加强"被吸"的感受 (先慢后快)
            float curveT = t * t * t; 

            // 移动到管道中心
            playerTransform.position = Vector3.Lerp(startPos, targetPos, curveT);
            
            // 逐渐缩小到0
            playerTransform.localScale = Vector3.Lerp(startScale, targetScale, curveT);
            
            // 快速旋转
            playerTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            yield return null;
        }

        // 确保最终状态
        playerTransform.position = targetPos;
        playerTransform.localScale = Vector3.zero;

        // 稍微等待一小会儿，让音效播完或者感受一下空档期
        yield return new WaitForSeconds(0.2f);

        // 4. 加载场景
        // 恢复时间缩放以防万一
        Time.timeScale = 1f;

        // 尝试使用 TransitionBlocks 插件进行过渡加载
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition())
        {
            transitioner.TransitionToScene(sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    /// <summary>
    /// 当关卡选择UI被关闭（玩家取消选择）时，重置传送器状态
    /// 允许玩家再次进入管道触发传送
    /// </summary>
    public void ResetTeleporter()
    {
        isActivated = false;
        cachedPlayerTransform = null;
    }
}
