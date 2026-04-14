using UnityEngine;

/// <summary>
/// 燃烧轨迹控制器 — 挂在玩家身上
/// 角色移动时每隔一段距离在地面留下燃烧区域
/// </summary>
public class FlameTrailController : MonoBehaviour
{
    [Header("轨迹设置")]
    [Tooltip("燃烧区域预制件")]
    public GameObject flameZonePrefab;

    [Tooltip("满级时的留痕间距（米）")]
    public float minDropInterval = 1.5f;
    [Tooltip("1级时的留痕间距（米）")]
    public float maxDropInterval = 3.0f;

    [Tooltip("燃烧区域存在时间（秒）")]
    public float zoneDuration = 3f;
    [Tooltip("燃烧区域半径")]
    public float zoneRadius = 1.5f;
    [Tooltip("每跳伤害基础值")]
    public int baseDamagePerTick = 3;
    [Tooltip("每跳间隔（秒）")]
    public float tickInterval = 0.5f;

    private Vector3 lastDropPosition;
    private bool isActive = false;

    void Start()
    {
        lastDropPosition = transform.position;
    }

    void Update()
    {
        // 检查是否装备了燃烧轨迹被动
        if (PlayerStats.Instance == null) return;
        int level = PlayerStats.Instance.flameTrailLevel;

        if (level <= 0)
        {
            isActive = false;
            return;
        }

        isActive = true;

        // 根据等级插值计算掉落间距
        float dropInterval = Mathf.Lerp(maxDropInterval, minDropInterval, (float)(level - 1) / 4f);

        // 检查是否移动了足够的距离
        float distance = Vector3.Distance(transform.position, lastDropPosition);
        if (distance >= dropInterval)
        {
            DropFlameZone(level);
            lastDropPosition = transform.position;
        }
    }

    /// <summary>
    /// 在当前位置放下一个燃烧区域
    /// </summary>
    private void DropFlameZone(int level)
    {
        if (flameZonePrefab == null) return;

        // 计算伤害（每级+20%）
        float damageMultiplier = 1f + (level - 1) * 0.2f;
        int finalDamage = Mathf.RoundToInt(baseDamagePerTick * damageMultiplier);

        // 应用玩家全局伤害加成
        if (PlayerStats.Instance != null)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * PlayerStats.Instance.damageMultiplier);
        }

        // 生成燃烧区域
        Vector3 spawnPos = new Vector3(transform.position.x, 0.05f, transform.position.z);
        GameObject zone = Instantiate(flameZonePrefab, spawnPos, Quaternion.identity);

        // 初始化燃烧区域参数
        FlameTrailZone flameZone = zone.GetComponent<FlameTrailZone>();
        if (flameZone != null)
        {
            flameZone.Initialize(finalDamage, zoneDuration, zoneRadius, tickInterval);
        }
    }
}
