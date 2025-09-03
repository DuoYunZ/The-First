// ExperienceGem.cs (最终整合版)
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
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

    // --- 【新增】拾取延迟 ---
    [Tooltip("宝石生成后，延迟多久才能被拾取（秒）")]
    public float pickupDelay = 0.5f;

    // --- 【新增】掉落动画 ---
    [Header("掉落动画")]
    [Tooltip("宝石向上弹跳的高度")]
    public float popHeight = 0.75f;
    [Tooltip("宝石完成弹跳动画所需的时间")]
    public float popDuration = 0.3f;

    // --- 【新增】吸收浮空效果 ---
    [Header("吸收浮空效果")]
    [Tooltip("吸收时向上浮空的高度")]
    public float absorbFloatHeight = 1.0f;
    [Tooltip("吸收浮空的时间")]
    public float absorbFloatDuration = 0.3f;
    [Tooltip("浮空后停滞的时间")]
    public float absorbHoverDuration = 0.2f;

    // --- 内部状态变量 ---
    private Transform collectionTarget;
    private PlayerLevelManager foundLevelManager;
    private bool isCollecting = false;
    private bool canBePickedUp = false; // 【新增】控制是否可被拾取的“总开关”
    private bool isAbsorbFloating = false; // 【新增】是否正在吸收浮空状态
    private Vector3 absorbStartPosition; // 【新增】吸收浮空起始位置


    void Start()
    {
        // 确保碰撞体是触发器
        GetComponent<Collider>().isTrigger = true;

        // 在 Start 中只获取一次玩家引用，后续不再重复获取
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            Transform playerRoot = GameManager.Instance.playerTransform;
            foundLevelManager = playerRoot.GetComponent<PlayerLevelManager>();

            // 1. 尝试寻找 "AimTargetPoint"
            Transform aimTarget = playerRoot.Find("AimTargetPoint");
            if (aimTarget != null)
            {
                // 如果找到了，就用它作为目标
                collectionTarget = aimTarget;
            }
            else
            {
                // 如果没找到，就用玩家的根坐标作为后备
                collectionTarget = playerRoot;
                Debug.LogWarning("在玩家身上未找到 'AimTargetPoint'，经验球将飞向玩家脚底。");
            }
        }

        // 启动掉落动画和拾取延迟计时
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        // --- 1. 动态掉落动画 ---
        Vector3 startPoint = transform.position;
        Vector3 endPoint = startPoint + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));

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
        transform.position = new Vector3(endPoint.x, startPoint.y, endPoint.z); // 确保落地在同一水平面

        // --- 2. 拾取延迟 ---
        yield return new WaitForSeconds(pickupDelay);

        // --- 3. 延迟结束，打开“总开关” ---
        canBePickedUp = true;
    }

    void Update()
    {
        // 如果"总开关"没打开，或者没有玩家，则不执行任何吸附逻辑
        if (!canBePickedUp || collectionTarget == null) return;

        // --- 如果正在吸收浮空状态，不执行其他逻辑 ---
        if (isAbsorbFloating) return;

        // --- 原有的磁铁吸附逻辑 ---
        float distanceToPlayer = Vector3.Distance(transform.position, collectionTarget.position);

        if (!isCollecting && distanceToPlayer <= magnetRadius)
        {
            StartAbsorbSequence();
        }

        if (isCollecting && !isAbsorbFloating)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectionTarget.position, collectionSpeed * Time.deltaTime);
            if (distanceToPlayer < 0.5f)
            {
                Collect();
            }
        }
    }
    private void StartAbsorbSequence()
    {
        if (isCollecting) return; // 防止重复触发

        isCollecting = true;
        isAbsorbFloating = true;
        absorbStartPosition = transform.position;

        // 启动吸收浮空协程
        StartCoroutine(AbsorbFloatRoutine());
    }

    // 【新增】吸收浮空协程
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

        // 2. 在浮空位置停滞一会儿
        yield return new WaitForSeconds(absorbHoverDuration);

        // 3. 结束浮空状态，开始飞向玩家
        isAbsorbFloating = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // 只有在"总开关"打开时，碰撞才有效
        if (canBePickedUp && other.CompareTag("Player") && !isCollecting)
        {
            StartAbsorbSequence();
        }
    }

    void Collect()
    {
        if (foundLevelManager != null)
        {
            foundLevelManager.AddExperience(experienceAmount);
        }
        else
        {
            // 作为后备方案，如果初始未找到，再找一次
            var lvlManager = collectionTarget.GetComponent<PlayerLevelManager>();
            if (lvlManager != null) lvlManager.AddExperience(experienceAmount);
        }

        // 销毁自身
        Destroy(gameObject);
    }
}