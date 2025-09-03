// --- VFXDamageController.cs (调试版) ---
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class VFXDamageController : MonoBehaviour
{
    private int damage;
    private GameObject attacker;
    private List<Health> hitTargets = new List<Health>();
    private GameObject hitEffectPrefab; // <--- 新增：用于存储命中特效

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