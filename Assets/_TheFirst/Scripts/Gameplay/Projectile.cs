using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 20f;     // 子弹飞行速度
    public float lifetime = 3f;     // 子弹存活时间
    public Vector3 direction = Vector3.forward; // 子弹飞行方向 (世界空间)
    public int damage = 10; // *** 新增：子弹伤害值 ***

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
        Debug.Log($"Projectile OnTriggerEnter with: {other.name} (Tag: {other.tag})"); // 1. 记录碰撞对象信息

        if (other.CompareTag("Enemy")) // 2. 检查 Tag 是否匹配
        {
            Debug.Log($"Tag 'Enemy' MATCHED on {other.name}!"); // 3. 确认 Tag 匹配成功

            // --- 修改这里：尝试用 GetComponentInParent ---
            // Health enemyHealth = other.GetComponent<Health>(); // 原来的代码
            Health enemyHealth = other.GetComponentInParent<Health>(); // 尝试向上查找 Health
                                                                       // --------------------------------------

            if (enemyHealth != null) // 4. 检查是否找到了 Health 组件
            {
                Debug.Log($"Health component FOUND on {other.name} or its parent. Attempting TakeDamage({damage})."); // 5. 确认找到 Health
                enemyHealth.TakeDamage(damage); // 6. 调用 TakeDamage
                Debug.Log($"Called TakeDamage on {other.name}."); // 7. 确认调用完成
            }
            else
            {
                // 8. 如果没找到 Health 组件，打印明确警告
                Debug.LogError($"Health component NOT FOUND on {other.name} or its parents!", other.gameObject);
            }

            // 销毁子弹
            Debug.Log($"Destroying projectile {this.gameObject.name}"); // 9. 确认销毁子弹
            Destroy(gameObject);
        }
        else if (other.CompareTag("Wall"))
        {
            Debug.Log($"Projectile hit Wall: {other.name}. Destroying projectile.");
            Destroy(gameObject);
        }
        // 可以添加 else 分支来查看撞到了其他什么东西
        // else { Debug.Log($"Projectile hit something else: {other.name} (Tag: {other.tag})"); }
    }


    // 如果你的碰撞体不是 Trigger，则修改 OnCollisionEnter
    /*
    void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Enemy")) {
            Health enemyHealth = collision.gameObject.GetComponent<Health>();
            if (enemyHealth != null) {
                enemyHealth.TakeDamage(damage);
            }
            Destroy(gameObject);
        } else if (collision.gameObject.CompareTag("Wall")) {
             Destroy(gameObject);
        }
    }
    */
}
