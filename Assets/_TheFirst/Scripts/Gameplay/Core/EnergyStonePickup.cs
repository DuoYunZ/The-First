using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnergyStonePickup : MonoBehaviour
{
    [Header("能量石数据")]
    [Tooltip("此拾取物代表的能量石 (必须在生成时指定)")]
    public EnergyStoneSO stoneData;

    [Header("拾取设置")]
    public float rotationSpeed = 90f;
    public float popDuration = 0.3f;
    public float popHeight = 1f;

    // (你可以像 GoldPickup 和 ExperienceGem 一样添加磁铁/吸收逻辑)

    void Start()
    {
        // (可选：添加一个像金币/经验一样的出生动画)
        // StartCoroutine(SpawnRoutine(popHeight, popDuration));
    }


    void Update()
    {
        // 简单的待机旋转
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stoneData == null)
        {
            Debug.LogError("能量石拾取物没有分配 stoneData!", this);
            return;
        }

        if (other.CompareTag("Player"))
        {
            // 关键：通知 FusionUIManager 开始融合流程
            // 我们假设 FusionUIManager 是一个单例
            FusionUIManager.Instance.StartFusion(stoneData);

            // (播放拾取音效/特效)

            // 销毁拾取物
            Destroy(gameObject);
        }
    }
}