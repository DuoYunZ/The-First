// 创建新脚本 Orbiter.cs
using UnityEngine;
public class Orbiter : MonoBehaviour
{
    [Header("自转设置")]
    [Tooltip("轨道物体自身的旋转速度（度/秒）")]
    public float selfRotationSpeed = 1440f; // 在这里设置一个默认的旋转速度

    private int damage = 10;
    // 如果需要，还可以有独立的冷却计时器，防止它对同一个敌人造成过于频繁的伤害
    private float hitCooldown = 0.5f;
    private float lastHitTime = -1f;

    public void Initialize(int dmg)
    {
        this.damage = dmg;
    }

    void Update()
    {
        // 让轨道物体围绕自己的Y轴（Vector3.up）进行旋转
        // Time.deltaTime 确保旋转是平滑且独立于帧率的
        transform.Rotate(Vector3.up, selfRotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time > lastHitTime + hitCooldown && other.CompareTag("Enemy"))
        {
            Health enemyHealth = other.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                // 呼叫新的 TakeDamage 方法，並把自己作為攻擊者傳入
                enemyHealth.TakeDamage(damage, transform.position, this.gameObject);
                lastHitTime = Time.time;
            }
        }
    }
}