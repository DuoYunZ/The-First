using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 20f;     // 子弹飞行速度
    public float lifetime = 3f;     // 子弹存活时间
    public Vector3 direction = Vector3.forward; // 子弹飞行方向 (世界空间)

    void Start()
    {
        // 在指定时间后自动销毁子弹 GameObject
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 每帧沿着指定方向移动
        // 使用 Space.World 确保沿世界空间方向移动
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    // --- 碰撞/触发处理 (未来添加) ---
    // 如果 Collider 的 Is Trigger 勾选了:
    void OnTriggerEnter(Collider other)
    {
        // 检查是否碰撞到了敌人 (例如通过 Tag 或 Layer)
        if (other.CompareTag("Enemy")) // 假设敌人 Tag 为 "Enemy"
        {
            Debug.Log($"子弹击中了敌人: {other.name}");
            // 在这里处理伤害逻辑...
            // 销毁敌人? other.GetComponent<EnemyHealth>()?.TakeDamage(10);
            Destroy(gameObject); // 击中后销毁子弹
        }
        // 可以添加对墙壁或其他障碍物的处理
        else if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    // 如果 Collider 的 Is Trigger 没有勾选 (物理碰撞):
    /*
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log($"子弹击中了敌人: {collision.gameObject.name}");
            // 处理伤害...
            Destroy(gameObject);
        }
        else if (collision.gameObject.CompareTag("Wall")) {
             Destroy(gameObject);
        }
    }
    */
}