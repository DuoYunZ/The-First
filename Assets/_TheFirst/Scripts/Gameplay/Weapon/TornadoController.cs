using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class TornadoController : MonoBehaviour
{
    [Header("龙卷风属性")]
    public float pullForce = 15f; // 吸力（移动速度）
    public float damageInterval = 0.2f; // 伤害间隔 (0.2s = 1秒5跳)
    [HideInInspector] public bool isComboUltimate = false; // 标识是否来自融合大招（阻止能量增加）

    private int damagePerTick;
    private WeaponPart launcher;
    private string weaponName;

    private float timer = 0f;

    // 记录卷入风中的敌人
    private HashSet<Health> victims = new HashSet<Health>();

    // 初始化方法 (由 Orbiter 调用)
    public void Setup(int damage, WeaponPart source)
    {
        this.damagePerTick = damage; // 建议伤害设低一点，因为频率很高
        this.launcher = source;
        if (source != null && source.StatBlock != null)
        {
            this.weaponName = source.StatBlock.weaponName;
        }

        // 龙卷风通常持续几秒后消失，Projectile 脚本已经处理了 Destroy，这里不用管
    }

    void OnTriggerEnter(Collider other)
    {
        // 用 Layer 或 Tag 检测敌人
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemies") || other.CompareTag("Enemy"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h != null) victims.Add(h);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemies") || other.CompareTag("Enemy"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h != null) victims.Remove(h);
        }
    }

    void Update()
    {
        // 1. 处理伤害 (绞杀)
        timer += Time.deltaTime;
        if (timer >= damageInterval)
        {
            DealDamagePulse();
            timer = 0f;
        }

        // 2. 处理吸附 (卷入) - 放在 LateUpdate 效果更好，但在 Update 做也可以
        // 为了平滑，我们每帧都拉
        ApplySuction();
    }

    private void ApplySuction()
    {
        // 清理死亡单位
        victims.RemoveWhere(h => h == null || h.IsDead);

        foreach (var h in victims)
        {
            Transform enemyTrans = h.transform;
            if (StatusEffectReceiver.IsKnockbackImmune(enemyTrans)) continue;

            // --- 核心吸附逻辑 (参考 BlackHole) ---

            // 1. 压制 AI
            NavMeshAgent agent = enemyTrans.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.velocity = Vector3.zero;
            }

            // 2. 强制位移 (向龙卷风中心移动)
            // 保持 Y 轴不变，防止把怪拉到天上或地下
            Vector3 targetPos = transform.position;
            targetPos.y = enemyTrans.position.y;

            // 使用 MoveTowards 平滑吸入
            float step = pullForce * Time.deltaTime;
            enemyTrans.position = Vector3.MoveTowards(enemyTrans.position, targetPos, step);
        }
    }

    private void DealDamagePulse()
    {
        // 再次清理，防止报错
        victims.RemoveWhere(h => h == null || h.IsDead);

        foreach (var h in victims)
        {
            // 造成伤害
            GameObject attackerGO = (launcher != null) ? launcher.gameObject : gameObject;
            h.TakeDamage(damagePerTick, h.transform.position, attackerGO, AttackType.Standard, null, null, weaponName);

            // --- 触发元素效果 ---
            // 因为是持续伤害，我们手动触发 StatusEffectReceiver
            // 这样龙卷风也能触发感电、点燃等
            if (launcher != null && launcher.currentStone != null)
            {
                // 这里可以复用 Orbiter 或 Projectile 里的 ApplyElementalEffects 逻辑
                // 为了简单，我们这里只简单处理最重要的：感电
                StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                if (receiver != null)
                {
                    // 如果母武器有雷电属性，龙卷风也能挂感电
                    if (launcher.StatBlock.nativeElectrify)
                    {
                        receiver.ApplyElectrified(1.0f); // 持续刷新
                    }
                    // 如果有火属性，挂点燃
                    if (launcher.StatBlock.nativeBurn)
                    {
                        receiver.ApplyBurn(5, 3f, 1f, weaponName);
                    }
                }
            }
        }
    }
}
