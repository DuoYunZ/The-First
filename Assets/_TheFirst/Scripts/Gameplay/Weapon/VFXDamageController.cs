// --- VFXDamageController.cs (调试版) ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VFXDamageController : MonoBehaviour
{
    private int damage;
    private GameObject attacker;
    private List<Health> hitTargets = new List<Health>();
    private GameObject hitEffectPrefab; // <--- 新增：用于存储命中特效
    [Header("生命周期与伤害窗口")]
    [Tooltip("特效的总生命周期（秒），之后将销毁自身")]
    public float totalLifetime = 2f;
    [Tooltip("碰撞体保持有效的时间（秒），即伤害判定的窗口期")]
    public float damageActiveDuration = 0.2f; // 例如，只在前0.2秒造成伤害
    private Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
    }

    void Start()
    {
        // 预定在总生命周期结束后销毁整个GameObject
        Destroy(gameObject, totalLifetime);

        // 启动一个协程，在指定的伤害窗口期后，禁用碰撞体
        StartCoroutine(DeactivateColliderRoutine());
    }

    private IEnumerator DeactivateColliderRoutine()
    {
        // 等待伤害窗口期结束
        yield return new WaitForSeconds(damageActiveDuration);

        // 时间一到，立即禁用碰撞体，停止伤害判定
        if (col != null)
        {
            col.enabled = false;
        }
    }
    // 修改 Initialize 方法，让它能接收 WeaponStatBlock
    public void Initialize(WeaponStatBlock weaponData, GameObject attacker)
    {
        this.damage = weaponData.baseAoeDamage;
        this.attacker = attacker;
        this.hitEffectPrefab = weaponData.hitEffectPrefab; // <--- 新增：从武器数据中获取命中特效
    }

    void OnTriggerEnter(Collider other)
    {
        if (damage <= 0) return;

        Health targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth != null && targetHealth.CompareTag("Enemy") && !hitTargets.Contains(targetHealth))
        {
            // 在造成伤害前，先在命中点生成特效
            if (hitEffectPrefab != null)
            {
                // 使用 other.ClosestPoint(transform.position) 可以获得更精确的碰撞点
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward)); // 让特效朝向攻击来源
            }

            hitTargets.Add(targetHealth);
            targetHealth.TakeDamage(damage, other.transform.position, attacker, AttackType.Standard);
        }
    }
}