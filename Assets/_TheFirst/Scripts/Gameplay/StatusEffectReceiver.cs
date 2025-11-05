using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StatusEffectReceiver : MonoBehaviour
{
    private Health enemyHealth;
    private EnemyAI enemyAI; // 如果需要處理減速等影響AI的效果
    private StraightMoverAI straightMoverAI;

    // 用於追蹤正在進行的狀態協程，避免同一狀態重複疊加
    private Dictionary<UpgradeType, Coroutine> activeStatusCoroutines = new Dictionary<UpgradeType, Coroutine>();

    public bool IsBurning { get; private set; } = false;
    public bool IsStunned { get; private set; } = false;

    [Header("特效预制件 (可选)")]
    public GameObject stunVfxPrefab;

    void Awake()
    {
        enemyHealth = GetComponent<Health>();
        enemyAI = GetComponent<EnemyAI>();
        straightMoverAI = GetComponent<StraightMoverAI>();
    }

    /// <summary>
    /// 應用燃燒效果
    /// </summary>
    public void ApplyBurn(int damagePerTick, float duration, float tickInterval)
    {
        // 【日誌 A】
        Debug.Log($"<color=orange>[StatusEffectReceiver] 收到 ApplyBurn 指令！傷害: {damagePerTick}, 持續: {duration}s, 間隔: {tickInterval}s</color>");

        if (activeStatusCoroutines.ContainsKey(UpgradeType.AoeDamage))
        {
            StopCoroutine(activeStatusCoroutines[UpgradeType.AoeDamage]);
        }

        Coroutine burnCoroutine = StartCoroutine(BurnRoutine(damagePerTick, duration, tickInterval));
        activeStatusCoroutines[UpgradeType.AoeDamage] = burnCoroutine;
    }

    // --- 新增：应用减速效果的方法 ---
    public void ApplySlow(float slowPercentage, float duration)
    {
        // 使用 UpgradeType.MoveSpeed 作为减速状态的标识符
        if (activeStatusCoroutines.ContainsKey(UpgradeType.MoveSpeed))
        {
            StopCoroutine(activeStatusCoroutines[UpgradeType.MoveSpeed]);
        }

        var slowCoroutine = StartCoroutine(SlowRoutine(slowPercentage, duration));
        activeStatusCoroutines[UpgradeType.MoveSpeed] = slowCoroutine;
    }

    public void ApplyStun(float duration)
    {
        // 确保你的 UpgradeType 枚举中有一个 "Stun" 的值
        if (activeStatusCoroutines.ContainsKey(UpgradeType.Stun))
        {
            StopCoroutine(activeStatusCoroutines[UpgradeType.Stun]);
        }

        var stunCoroutine = StartCoroutine(StunRoutine(duration));
        activeStatusCoroutines[UpgradeType.Stun] = stunCoroutine;
    }
    private IEnumerator StunRoutine(float duration)
    {
        IsStunned = true;

        GameObject stunVfxInstance = null;
        if (stunVfxPrefab != null)
        {
            // 在敌人头顶或中心生成特效，并将其设为子物体
            stunVfxInstance = Instantiate(stunVfxPrefab, transform.position, Quaternion.identity, transform);
        }
        // 停止两种可能的 AI 脚本
        if (enemyAI != null) enemyAI.SetStunned(true);
        if (straightMoverAI != null) straightMoverAI.SetStunned(true);

        // (可选) 在这里附加一个眩晕的粒子特效

        yield return new WaitForSeconds(duration);

        IsStunned = false;

        // 恢复两种可能的 AI 脚本
        if (enemyAI != null) enemyAI.SetStunned(false);
        if (straightMoverAI != null) straightMoverAI.SetStunned(false);

        if (stunVfxInstance != null)
        {
            Destroy(stunVfxInstance);
        }

        // (可选) 在这里移除眩晕的粒子特效

        activeStatusCoroutines.Remove(UpgradeType.Stun);
    }

    private IEnumerator SlowRoutine(float slowPercentage, float duration)
    {
        if (enemyAI == null) yield break;

        Debug.Log($"{gameObject.name} 被减速 {slowPercentage * 100}%，持续 {duration} 秒。");

        float originalSpeed = enemyAI.GetOriginalMoveSpeed();
        // 应用减速，确保速度不会低于0
        enemyAI.SetMoveSpeed(Mathf.Max(0, originalSpeed * (1f - slowPercentage)));

        // （可选）在这里可以改变敌人的颜色或添加冰霜特效
        GetComponentInChildren<Renderer>().material.color = Color.cyan;

        yield return new WaitForSeconds(duration);

        // 持续时间结束后，恢复原始速度
        Debug.Log($"{gameObject.name} 的减速效果结束。");
        enemyAI.SetMoveSpeed(originalSpeed);

        // （可选）恢复颜色和特效
        GetComponentInChildren<Renderer>().material.color = Color.white;

        activeStatusCoroutines.Remove(UpgradeType.MoveSpeed);
    }

    private IEnumerator BurnRoutine(int damagePerTick, float duration, float tickInterval)
    {
        Debug.Log($"<color=orange>[StatusEffectReceiver] 燃燒協程(BurnRoutine)已啟動，目標: {gameObject.name}</color>"); Debug.Log($"<color=orange>[StatusEffectReceiver] 燃燒協程(BurnRoutine)已啟動，目標: {gameObject.name}</color>");
        IsBurning = true;
        float timer = 0f;

        while (timer < duration)
        {
            // 等待一個 Tick 的間隔
            yield return new WaitForSeconds(tickInterval);
            timer += tickInterval;

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                Debug.Log($"{gameObject.name} 受到燃燒傷害: {damagePerTick}");
                // 燃燒傷害的攻擊者可以設為 null 或一個代表“環境”的物件
                enemyHealth.TakeDamage(damagePerTick, transform.position, null);
            }
            else
            {
                // 如果敵人在燃燒期間死亡，提前結束協程
                break;
            }
        }

        IsBurning = false;
        activeStatusCoroutines.Remove(UpgradeType.AoeDamage);
        // Debug.Log($"{gameObject.name} 的燃燒效果結束。");
    }
}