using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PersistentAoeField : MonoBehaviour
{
    private int damagePerTick;
    private float tickInterval;
    private float duration;
    private GameObject attacker;

    // 儲存當前在領域內的敵人
    private List<Health> enemiesInField = new List<Health>();
    private Coroutine damageCoroutine;

    /// <summary>
    /// 初始化傷害領域的屬性
    /// </summary>
    public void Setup(int dmgPerTick, float interval, float dur, GameObject creator)
    {
        this.damagePerTick = dmgPerTick;
        this.tickInterval = interval;
        this.duration = dur;
        this.attacker = creator;

        // 在指定的持續時間後銷毀自己
        Destroy(gameObject, duration);
        // 啟動造成傷害的協程
        damageCoroutine = StartCoroutine(DamageRoutine());
    }

    // 當有物體進入觸發器時
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Health enemyHealth = other.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemiesInField.Contains(enemyHealth))
            {
                // 將敵人添加到列表中
                enemiesInField.Add(enemyHealth);
            }
        }
    }

    // 當有物體離開觸發器時
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Health enemyHealth = other.GetComponentInParent<Health>();
            if (enemyHealth != null && enemiesInField.Contains(enemyHealth))
            {
                // 從列表中移除敵人
                enemiesInField.Remove(enemyHealth);
            }
        }
    }

    // 持續造成傷害的協程
    private IEnumerator DamageRoutine()
    {
        while (true) // 一個無限循環，因為整個物件會在持續時間結束後被銷毀
        {
            // 等待一個傷害間隔
            yield return new WaitForSeconds(tickInterval);

            // 對列表中的所有敵人造成傷害
            // 我們從後往前遍歷，以防有敵人在中途死亡導致列表變化
            for (int i = enemiesInField.Count - 1; i >= 0; i--)
            {
                if (enemiesInField[i] != null && !enemiesInField[i].IsDead)
                {
                    enemiesInField[i].TakeDamage(damagePerTick, transform.position, attacker);
                }
                else
                {
                    // 如果敵人在列表中但已經死亡或失效，則將其移除
                    enemiesInField.RemoveAt(i);
                }
            }
        }
    }
}