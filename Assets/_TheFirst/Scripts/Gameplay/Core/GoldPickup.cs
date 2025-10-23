// GoldPickup.cs (Enhanced Version)
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class GoldPickup : MonoBehaviour
{
    [Header("金币设置")]
    [Tooltip("此金币提供的价值")]
    public int goldValue = 10;

    [Header("拾取设置")]
    [Tooltip("玩家靠近到多少距离内开始被吸引")]
    public float magnetRadius = 4f;
    [Tooltip("飞向玩家的速度")]
    public float collectionSpeed = 8f;
    [Tooltip("金币生成后，延迟多久才能被拾取（秒）")]
    public float pickupDelay = 0.5f;

    [Header("掉落动画")]
    [Tooltip("金币向上弹跳的高度")]
    public float popHeight = 0.75f;
    [Tooltip("金币完成弹跳动画所需的时间")]
    public float popDuration = 0.3f;

    [Header("吸收浮空效果")]
    [Tooltip("吸收时向上浮空的高度")]
    public float absorbFloatHeight = 1.0f;
    [Tooltip("吸收浮空的时间")]
    public float absorbFloatDuration = 0.3f;
    [Tooltip("浮空后停滞的时间")]
    public float absorbHoverDuration = 0.2f;

    [Header("待机旋转")]
    [Tooltip("金币待机时的自转速度（度/秒）")]
    public float rotationSpeed = 180f;

    [Header("音效与特效")]
    [Tooltip("金币被玩家收集时播放的音效")]
    public AudioClip collectionSound;
    [Tooltip("金币被玩家收集时，在玩家身上播放的粒子特效")]
    public GameObject collectionVfxPrefab;

    // --- 内部状态变量 ---
    private Transform collectionTarget;
    private bool isCollecting = false;
    private bool canBePickedUp = false;
    private bool isAbsorbFloating = false;
    private bool isSpinning = false;
    private Vector3 absorbStartPosition;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        // 寻找玩家作为吸收目标
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            Transform playerRoot = GameManager.Instance.playerTransform;

            // 优先飞向 AimTargetPoint 以获得更好的视觉效果
            Transform aimTarget = playerRoot.Find("AimTargetPoint");
            collectionTarget = (aimTarget != null) ? aimTarget : playerRoot;
        }

        StartCoroutine(SpawnRoutine());
    }

    // 生成时的动画和延迟
    IEnumerator SpawnRoutine()
    {
        // 1. 掉落弹跳动画
        Vector3 startPoint = transform.position;
        // 随机一个小的落地偏移，让掉落更自然
        Vector3 endPoint = startPoint + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));

        float timer = 0f;
        while (timer < popDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / popDuration);

            Vector3 horizontalPosition = Vector3.Lerp(startPoint, endPoint, t);
            float verticalPosition = Mathf.Sin(t * Mathf.PI) * popHeight;

            transform.position = new Vector3(horizontalPosition.x, startPoint.y + verticalPosition, horizontalPosition.z);
            yield return null;
        }
        transform.position = new Vector3(endPoint.x, startPoint.y, endPoint.z);

        // 2. 拾取延迟
        yield return new WaitForSeconds(pickupDelay);

        // 3. 激活拾取
        canBePickedUp = true;
        isSpinning = true;
    }

    void Update()
    {
        if (isSpinning)
        {
            // 对于俯视角游戏中平放的金币，围绕Y轴(向上轴)旋转可以产生“悬浮旋转”效果。
            // 这通常是期望的效果。
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        if (!canBePickedUp || collectionTarget == null || isAbsorbFloating) return;

        // 检查距离，如果足够近则开始吸收
        float distanceToPlayer = Vector3.Distance(transform.position, collectionTarget.position);
        if (!isCollecting && distanceToPlayer <= magnetRadius)
        {
            StartAbsorbSequence();
        }

        // 如果正在被吸收，则飞向玩家
        if (isCollecting)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectionTarget.position, collectionSpeed * Time.deltaTime);
            if (distanceToPlayer < 0.5f) // 足够近时完成收集
            {
                Collect();
            }
        }
    }

    // 开始吸收序列（浮空 -> 飞向玩家）
    private void StartAbsorbSequence()
    {
        if (isCollecting) return;

        isCollecting = true;
        isAbsorbFloating = true;
        absorbStartPosition = transform.position;

        StartCoroutine(AbsorbFloatRoutine());
    }

    // 吸收前的浮空动画
    IEnumerator AbsorbFloatRoutine()
    {
        // 1. 向上浮空
        Vector3 floatTarget = absorbStartPosition + Vector3.up * absorbFloatHeight;
        float timer = 0f;
        while (timer < absorbFloatDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / absorbFloatDuration);
            transform.position = Vector3.Lerp(absorbStartPosition, floatTarget, t);
            yield return null;
        }

        // 2. 短暂悬停
        yield return new WaitForSeconds(absorbHoverDuration);

        // 3. 结束浮空，允许 Update 中的移动逻辑接管
        isAbsorbFloating = false;
    }

    // 玩家直接走上去碰撞拾取
    void OnTriggerEnter(Collider other)
    {
        if (canBePickedUp && other.CompareTag("Player") && !isCollecting)
        {
            StartAbsorbSequence();
        }
    }

    // 完成收集
    void Collect()
    {
        // --- 核心逻辑修改：给予金币而非经验 ---
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddGold(goldValue);
        }
        // --- 修改结束 ---

        // 播放音效和特效（与经验球逻辑相同）
        if (collectionTarget != null)
        {
            if (collectionVfxPrefab != null)
            {
                Instantiate(collectionVfxPrefab, collectionTarget.position, collectionTarget.rotation);
            }

            if (collectionSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySoundEffect(collectionSound);
            }
        }

        Destroy(gameObject);
    }
}