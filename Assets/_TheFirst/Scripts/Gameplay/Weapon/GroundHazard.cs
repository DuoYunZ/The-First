using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class GroundHazard : MonoBehaviour
{
    private int damagePerTick;

    public int DamagePerTick => damagePerTick;

    private float duration;

    // 伤害间隔 (例如 0.5秒 或 1.0秒)
    private float tickInterval = 1f;

    private string weaponName;
    private GameObject owner;

    // 记录在这个具体火堆范围内的敌人
    private HashSet<Health> enemiesInRange = new HashSet<Health>();

    // =========================================================
    // 【核心修复】静态字典：记录所有怪物对于特定类型伤害的冷却
    // Key: 敌人ID + 伤害类型标签, Value: 下次允许受伤的时间
    // =========================================================
    private static Dictionary<string, float> globalDamageCooldowns = new Dictionary<string, float>();

    // 给火海一个类型标签，防止跟毒沼泽等其他地面伤害混淆
    // 如果你有多种地面伤害，可以在 Initialize 里改这个值
    public string hazardTypeTag = "FireHazard";

    public void Initialize(int damage, float lifeTime, string sourceWeaponName, GameObject ownerInfo)
    {
        this.damagePerTick = damage;
        this.duration = lifeTime;
        this.weaponName = sourceWeaponName;
        this.owner = ownerInfo;

        Destroy(gameObject, duration);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h != null)
            {
                enemiesInRange.Add(h);
                // 踩上去瞬间，尝试造成伤害 (受全局冷却限制)
                TryDealDamage(h);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h != null && enemiesInRange.Contains(h))
            {
                enemiesInRange.Remove(h);
            }
        }
    }

    void Update()
    {
        // 我们不再使用单独的 timer 变量来倒计时
        // 而是每一帧都检查范围内的敌人，看他们的“全局冷却”是否结束了

        // 1. 清理死掉的敌人
        enemiesInRange.RemoveWhere(h => h == null || h.IsDead);

        // 2. 遍历范围内的每个敌人
        foreach (var h in enemiesInRange)
        {
            TryDealDamage(h);
        }
    }

    /// <summary>
    /// 尝试造成伤害 (包含防叠加逻辑)
    /// </summary>
    private void TryDealDamage(Health h)
    {
        if (h == null) return;

        // 生成一个唯一的 Key：敌人ID + 伤害类型
        // 这样同一个敌人对 "FireHazard" 有冷却，但对 "PoisonHazard" 没有冷却
        string cooldownKey = h.GetInstanceID() + "_" + hazardTypeTag;

        // 检查当前时间 是否已经超过了 下次允许受伤的时间
        if (IsCooldownReady(cooldownKey))
        {
            // --- 造成伤害 ---
            h.TakeDamage(damagePerTick, h.transform.position, owner, AttackType.Standard, null, null, weaponName);

            // 刷新燃烧 Debuff
            StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
            if (receiver != null)
            {
                receiver.ApplyBurn(5, 3f, 1f, weaponName);
            }

            // --- 设置新的冷却时间 ---
            // 下一次受伤必须在 "当前时间 + 间隔" 之后
            globalDamageCooldowns[cooldownKey] = Time.time + tickInterval;
        }
    }

    private bool IsCooldownReady(string key)
    {
        // 如果字典里没记录，说明可以直接打
        if (!globalDamageCooldowns.ContainsKey(key)) return true;

        // 如果记录的时间小于当前时间，说明冷却转好了
        return Time.time >= globalDamageCooldowns[key];
    }

    // 可选：在场景切换时清理静态字典，防止内存泄漏 (虽然后果很小)
    void OnDestroy()
    {
        // 这是一个简单的清理策略：如果这个火堆销毁时，字典太大，就清理一下
        // (在Roguelike里，更严谨的做法是在 GameManager 重启关卡时清理)
        if (globalDamageCooldowns.Count > 1000)
        {
            globalDamageCooldowns.Clear();
        }
    }
}