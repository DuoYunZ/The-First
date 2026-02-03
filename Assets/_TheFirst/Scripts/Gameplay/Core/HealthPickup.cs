using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("设置")]
    public int healAmount = 20; // 恢复多少血

    [Header("特效")]
    [Tooltip("拾取时在玩家身上播放的治疗特效")]
    public GameObject pickupVfxPrefab;
    [Tooltip("拾取时的音效 (可选)")]
    public AudioClip pickupSound;

    private void OnTriggerEnter(Collider other)
    {
        // 只检测玩家
        if (other.CompareTag("Player"))
        {
            // 尝试获取玩家的 Health 组件
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth == null) playerHealth = other.GetComponentInParent<Health>();

            if (playerHealth != null)
            {
                // 调用我们在 Health.cs 里新写的 Heal 方法
                // 只有当玩家真的回血了(没满血)，才消耗血包
                if (playerHealth.Heal(healAmount))
                {
                    // 1. 播放特效 (挂在玩家身上，跟随玩家)
                    if (pickupVfxPrefab != null)
                    {
                        GameObject vfx = Instantiate(pickupVfxPrefab, other.transform.position, Quaternion.identity, other.transform);
                        Destroy(vfx, 2.0f); // 2秒后销毁特效，防止残留
                    }

                    // 2. 播放音效 (在原位置播放)
                    if (pickupSound != null)
                    {
                        AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                    }

                    // 3. 销毁血包
                    Destroy(gameObject);
                }
            }
        }
    }
}