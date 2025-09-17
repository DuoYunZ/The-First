// 创建新脚本 Orbiter.cs
using System.Collections.Generic;
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
    private Dictionary<Health, float> hitTargetsCooldown = new Dictionary<Health, float>();


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
        if (!other.CompareTag("Enemy")) return;

        Health enemyHealth = other.GetComponentInParent<Health>();

        // 检查是否获取到有效的Health组件，以及敌人是否已死亡
        if (enemyHealth == null || enemyHealth.IsDead) return;

        // 【修改后】的冷却判断逻辑
        // 检查1: 字典里是否已经有这个敌人了？
        if (hitTargetsCooldown.ContainsKey(enemyHealth))
        {
            // 如果有，再检查它的独立冷却时间是否已过
            if (Time.time > hitTargetsCooldown[enemyHealth] + hitCooldown)
            {
                // 冷却已过，可以再次造成伤害
                ApplyDamage(enemyHealth);
            }
            // 如果冷却没过，则什么都不做
        }
        else
        {
            // 如果字典里没有这个敌人，说明是第一次命中，直接造成伤害
            ApplyDamage(enemyHealth);
        }
    }
    private void ApplyDamage(Health enemyHealth)
    {
        // 1. 造成伤害
        enemyHealth.TakeDamage(damage, transform.position, this.gameObject);

        // 2. 更新或添加该敌人的命中时间到字典中
        hitTargetsCooldown[enemyHealth] = Time.time;
    }
}