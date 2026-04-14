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

    private WeaponPart launcher; // 用于获取武器名称进行统计
    
    // 公共属性供Health.cs获取经验
    public WeaponPart sourceWeapon => launcher;

    private bool isArmed = false;

    // 【引力陷阱】配置
    private float gravityTrapRadius = 8f;  // 引力吸引范围
    private float gravityPullForce = 2f;    // 引力拉扯速度

    /// <summary>
    /// 由WeaponPart在实例化后调用，用于传递属性
    /// </summary>
    public void Initialize(int damage, float radius, float armingTime, float duration, GameObject attacker, GameObject vfxPrefab, LayerMask layersToDamage, WeaponPart launcher)
    {
        this.damage = damage;
        this.radius = radius;
        this.attacker = attacker;
        this.explosionVfxPrefab = vfxPrefab;
        this.damageableLayers = layersToDamage;
        this.launcher = launcher;

        // 在指定时间后激活
        StartCoroutine(ArmingRoutine(armingTime));
        // 在指定生命周期后销毁
        Destroy(gameObject, duration);
    }

    private IEnumerator ArmingRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        isArmed = true;
        
        GetComponent<Renderer>().material.color = Color.red; 
    }

    void Update()
    {
        // 【引力陷阱】武装后持续吸引附近敌人
        if (isArmed && launcher != null && launcher.isMineGravityTrap)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, gravityTrapRadius, damageableLayers);
            foreach (Collider col in nearby)
            {
                if (!col.CompareTag("Enemy")) continue;
                // 微弱地将敌人向地雷中心拉扯
                Vector3 dir = (transform.position - col.transform.position).normalized;
                col.transform.position += dir * gravityPullForce * Time.deltaTime;
            }
        }
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
                string weaponName = (launcher != null && launcher.StatBlock != null) ? launcher.StatBlock.weaponName : "Landmine";

                // 记录击杀前血量，用于判断是否击杀
                bool wasAlive = !enemyHealth.IsDead;

                // 对范围内的所有敌人造成伤害
                enemyHealth.TakeDamage(damage, transform.position, gameObject, AttackType.Standard, null, null, weaponName);

                // 【震撼弹片】爆炸附加 1.5 秒眩晕
                if (launcher != null && launcher.isMineStun)
                {
                    StatusEffectReceiver receiver = hit.GetComponentInParent<StatusEffectReceiver>();
                    if (receiver != null)
                    {
                        receiver.ApplyStun(1.5f);
                    }
                }

                // 【能量回收】击杀时 15% 概率获得额外能量
                if (launcher != null && launcher.isMineEnergyRecovery && wasAlive && enemyHealth.IsDead)
                {
                    if (Random.value <= 0.15f)
                    {
                        launcher.GainEnergy(damage * 2f); // 给予双倍伤害值的能量
                    }
                }
            }
        }
        // 【引力黑洞】爆炸后原地生成黑洞吸引怪物
        if (launcher != null && launcher.isMineBlackHole && launcher.StatBlock != null && launcher.StatBlock.blackHolePrefab != null)
        {
            GameObject bhGO = Instantiate(launcher.StatBlock.blackHolePrefab, transform.position, Quaternion.identity);
            BlackHoleField bhField = bhGO.GetComponent<BlackHoleField>();
            if (bhField != null)
            {
                bhField.Initialize(7f, 2.5f); // 拉扯速度7，持续2.5秒
            }
        }

        // 【凝固汽油弹】爆炸后原地生成燃烧区域
        if (launcher != null && launcher.isMineFusionNapalm && launcher.StatBlock != null && launcher.StatBlock.napalmPrefab != null)
        {
            GameObject napalmGO = Instantiate(launcher.StatBlock.napalmPrefab, transform.position, Quaternion.identity);
            GroundHazard hazard = napalmGO.GetComponent<GroundHazard>();
            if (hazard != null)
            {
                string wName = launcher.StatBlock != null ? launcher.StatBlock.weaponName : "Napalm";
                int napalmDmg = Mathf.RoundToInt(damage * 0.3f); // 每tick造成地雷伤害的30%
                hazard.hazardTypeTag = "NapalmHazard"; // 区分其他火海
                hazard.Initialize(napalmDmg, 4f, wName, attacker);
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