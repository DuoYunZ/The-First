// Landmine.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Landmine : MonoBehaviour
{
    private int damage;
    private float radius;
    private GameObject explosionVfxPrefab;
    private GameObject attacker; // 伤害来源
    private LayerMask damageableLayers;

    private bool isArmed = false;

    /// <summary>
    /// 由WeaponPart在实例化后调用，用于传递属性
    /// </summary>
    public void Initialize(int damage, float radius, float armingTime, float duration, GameObject attacker, GameObject vfxPrefab, LayerMask layersToDamage)
    {
        this.damage = damage;
        this.radius = radius;
        this.attacker = attacker;
        this.explosionVfxPrefab = vfxPrefab;
        this.damageableLayers = layersToDamage;

        // 在指定时间后激活
        StartCoroutine(ArmingRoutine(armingTime));
        // 在指定生命周期后销毁
        Destroy(gameObject, duration);
    }

    private IEnumerator ArmingRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        isArmed = true;
        // (可选) 在这里可以播放一个“已激活”的提示音或视觉效果
        // GetComponent<Renderer>().material.color = Color.red; 
    }

    void OnTriggerEnter(Collider other)
    {
        // 如果地雷尚未激活，或者碰到的不是敌人，则不反应
        if (!isArmed || !other.CompareTag("Enemy")) return;

        // 如果条件满足，则引爆
        Explode();
    }

    private void Explode()
    {
        // 播放爆炸特效
        if (explosionVfxPrefab != null)
        {
            Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
        }

        // 进行范围伤害检测
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, damageableLayers);
        foreach (Collider hit in hits)
        {
            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                // 对范围内的所有敌人造成伤害
                enemyHealth.TakeDamage(damage, transform.position, attacker, AttackType.Standard);
            }
        }

        // 销毁地雷自身
        Destroy(gameObject);
    }

    // 在场景中绘制辅助线，方便调试
    void OnDrawGizmos()
    {
        Gizmos.color = isArmed ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}