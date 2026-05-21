// TreasureChestPickup.cs — 宝箱拾取物
// 怪物死亡掉落，玩家触碰拾取后触发独立宝箱开奖界面
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class TreasureChestPickup : MonoBehaviour
{
    [Header("掉落动画")]
    [Tooltip("宝箱向上弹跳的高度")]
    public float popHeight = 1.0f;
    [Tooltip("宝箱完成弹跳动画所需的时间")]
    public float popDuration = 0.4f;

    [Header("待机效果")]
    [Tooltip("宝箱待机时的自转速度（度/秒）")]
    public float rotationSpeed = 60f;
    [Tooltip("宝箱上下浮动的幅度")]
    public float bobAmplitude = 0.15f;
    [Tooltip("宝箱上下浮动的频率")]
    public float bobFrequency = 2f;

    [Header("音效与特效")]
    [Tooltip("宝箱被拾取时播放的音效")]
    public AudioClip pickupSound;
    [Tooltip("宝箱被拾取时的特效")]
    public GameObject pickupVfxPrefab;

    // --- 内部状态 ---
    private bool canBePickedUp = false; // 弹跳动画完成后才可拾取
    private bool isPickedUp = false;    // 防止重复拾取
    private Vector3 restPosition;       // 落地后的静止位置

    void Start()
    {
        // 确保碰撞体是触发器
        GetComponent<Collider>().isTrigger = true;

        // 启动掉落弹跳动画
        StartCoroutine(SpawnRoutine());
    }

    /// <summary>
    /// 掉落弹跳动画：先弹起再落下，与经验球/金币风格一致
    /// </summary>
    IEnumerator SpawnRoutine()
    {
        Vector3 startPoint = transform.position;
        // 随机小偏移，让多个掉落物不重叠
        Vector3 endPoint = startPoint + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));

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

        // 确保落地在同一水平面
        restPosition = new Vector3(endPoint.x, startPoint.y, endPoint.z);
        transform.position = restPosition;

        // 动画结束，允许拾取
        canBePickedUp = true;
    }

    void Update()
    {
        if (!canBePickedUp || isPickedUp) return;

        // 待机自转
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // 上下浮动
        float bobOffset = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.position = restPosition + Vector3.up * bobOffset;
    }

    /// <summary>
    /// 玩家直接触碰拾取
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (!canBePickedUp || isPickedUp) return;

        if (other.CompareTag("Player"))
        {
            PickUp();
        }
    }

    [Header("多选概率")]
    [Tooltip("一次可选2张卡的基础概率（0~1）")]
    public float doublePickChance = 0.15f;
    [Tooltip("一次可选3张卡（全选）的基础概率（0~1）")]
    public float triplePickChance = 0.05f;

    /// <summary>
    /// 根据幸运值计算本次宝箱可选卡片张数
    /// 幸运值作为概率乘数：luck=1.0为无加成，luck=1.5则概率×1.5
    /// </summary>
    private int CalculatePickCount()
    {
        float luck = PlayerStats.Instance != null ? PlayerStats.Instance.luck : 1.0f;

        // 幸运值加成后的概率（上限钳制到0.8防止必中）
        float tripleChance = Mathf.Min(triplePickChance * luck, 0.8f);
        float doubleChance = Mathf.Min(doublePickChance * luck, 0.8f);

        float roll = Random.value;

        // 优先判定全选（3张），再判定双选（2张），否则标准（1张）
        if (roll < tripleChance)
        {
            Debug.Log($"<color=gold>[宝箱] 大丰收！可选3张卡 (roll={roll:F3}, 需<{tripleChance:F3}, luck={luck:F2})</color>");
            return 3;
        }
        else if (roll < tripleChance + doubleChance)
        {
            Debug.Log($"<color=yellow>[宝箱] 双倍收获！可选2张卡 (roll={roll:F3}, 需<{tripleChance + doubleChance:F3}, luck={luck:F2})</color>");
            return 2;
        }
        else
        {
            Debug.Log($"<color=white>[宝箱] 标准收获，可选1张卡 (luck={luck:F2})</color>");
            return 1;
        }
    }

    /// <summary>
    /// 执行拾取逻辑：触发宝箱老虎机式自动奖励
    /// </summary>
    private void PickUp()
    {
        isPickedUp = true;

        // 播放拾取音效
        if (pickupSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySoundEffect(pickupSound);
        }

        // 播放拾取特效
        if (pickupVfxPrefab != null)
        {
            GameObject vfx = Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.TriggerTreasureSlotMachineReward();
        }
        else
        {
            Debug.LogWarning("[TreasureChestPickup] UpgradeManager 未找到，无法触发宝箱奖励！");
        }

        // 销毁宝箱
        Destroy(gameObject);
    }
}
