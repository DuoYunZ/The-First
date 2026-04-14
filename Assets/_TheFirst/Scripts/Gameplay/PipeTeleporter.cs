using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PipeTeleporter : MonoBehaviour
{
    [Header("传送设置")]
    [Tooltip("要加载的战斗场景的名称")]
    public string sceneToLoad = "CombatArena01"; 

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
        
        StartCoroutine(SuckInSequence(other.transform));
    }

    private IEnumerator SuckInSequence(Transform playerTransform)
    {
        // 1. 禁用玩家物理与控制 (这里尝试多种可能的方式禁用)
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
        
        // （视您项目的角色控制器脚本而定，可以尝试发消息禁用其 Update）
        playerTransform.SendMessage("DisableMovement", SendMessageOptions.DontRequireReceiver);

        // 2. 播放音效
        if (suckSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(suckSound);
        }

        // 3. 开始补间动画 (Tween) - 缩放、移动、旋转
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
}
