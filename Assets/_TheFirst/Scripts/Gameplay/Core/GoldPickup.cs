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
        if (isSpinning) transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        if (isCollecting || !canBePickedUp || collectionTarget == null || isAbsorbFloating) return;

        float distanceToPlayer = Vector3.Distance(transform.position, collectionTarget.position);

        // --- 【修复】应用拾取范围加成 ---
        float finalRadius = magnetRadius;
        if (PlayerStats.Instance != null)
        {
            finalRadius *= PlayerStats.Instance.pickupRadiusMultiplier;
        }

        if (!isCollecting && distanceToPlayer <= finalRadius) // 使用 finalRadius
        {
            StartAbsorbSequence();
        }
        // -----------------------------
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
        // (1. 向上浮空 - 保持不变)
        Vector3 floatTarget = absorbStartPosition + Vector3.up * absorbFloatHeight;
        float timer = 0f;
        while (timer < absorbFloatDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / absorbFloatDuration);
            transform.position = Vector3.Lerp(absorbStartPosition, floatTarget, t);
            yield return null;
        }

        // (2. 短暂悬停 - 保持不变)
        yield return new WaitForSeconds(absorbHoverDuration);

        // (3. 结束浮空)
        isAbsorbFloating = false; //

        // --- vvv [新增] vvv ---
        // (4. 立即开始飞向玩家，直到被 Collect() 销毁)
        while (true)
        {
            if (collectionTarget == null)
            {
                Destroy(gameObject); // 玩家消失了？销毁自己
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, collectionTarget.position, collectionSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, collectionTarget.position) < 0.5f)
            {
                Collect(); //
                yield break;
            }
            yield return null;
        }
        // --- ^^^ [新增] ^^^ ---
    }

    // 玩家直接走上去碰撞拾取
    void OnTriggerEnter(Collider other)
    {
        if (canBePickedUp && other.CompareTag("Player") && !isCollecting)
        {
            StartAbsorbSequence();
        }
    }

    public void TriggerMagnet(Transform target)
    {
        // 确保只触发一次
        if (isCollecting || !canBePickedUp) return;

        if (collectionTarget == null)
        {
            collectionTarget = target;
        }

        StartAbsorbSequence();
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

        if (BattleStatisticsManager.Instance != null)
            BattleStatisticsManager.Instance.AddGold(goldValue);
    }
}