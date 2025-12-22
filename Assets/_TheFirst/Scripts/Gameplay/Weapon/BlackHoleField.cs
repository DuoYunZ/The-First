using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class BlackHoleField : MonoBehaviour
{
    private float pullSpeed; // 这里我们把 Force 理解为 Speed (每秒移动多少米)
    private float duration;

    // 记录在范围内的敌人
    private HashSet<Transform> victims = new HashSet<Transform>();

    public void Initialize(float force, float lifeTime)
    {
        // 这里的 force 建议填 5-10 左右
        this.pullSpeed = force;
        this.duration = lifeTime;
        Destroy(gameObject, duration);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            victims.Add(other.transform);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            // 离开黑洞时，恢复敌人的控制权 (可选)
            // RestoreEnemyControl(other.transform);
            victims.Remove(other.transform);
        }
    }

    // 使用 LateUpdate 确保在敌人AI计算完移动后，我们强行覆盖它的位置
    void LateUpdate()
    {
        // 1. 清理已死亡的敌人
        victims.RemoveWhere(t => t == null);

        foreach (var enemyTransform in victims)
        {
            // 2. 压制敌人的 AI 移动 (防止它挣扎)
            NavMeshAgent agent = enemyTransform.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                // 将 AI 的速度归零，通过“冻结”AI 来防止它乱跑
                // (比 agent.enabled = false 更安全，不会导致 Agent 穿墙或重置路径)
                agent.velocity = Vector3.zero;

                // 如果你发现怪还是在抖动，可以尝试短暂禁用 updatePosition
                // agent.updatePosition = false; 
            }

            // 3. 计算吸附位移
            // Vector3.MoveTowards 的魔力：它会把物体移向目标，
            // 但如果距离小于这一帧的移动量，它会直接让物体停在目标点，绝不过冲！
            float step = pullSpeed * Time.deltaTime;

            // 目标高度：保持怪物当前的 Y 轴，或者稍微拉向黑洞中心的 Y
            // 这里我们只在水平面上吸，防止把怪拉进地里
            Vector3 targetPos = transform.position;
            targetPos.y = enemyTransform.position.y;

            enemyTransform.position = Vector3.MoveTowards(enemyTransform.position, targetPos, step);

            // 4. (可选) 如果你想同步 Agent 的逻辑位置，防止瞬移后 AI 错乱
            if (agent != null && agent.enabled)
            {
                agent.nextPosition = enemyTransform.position;
            }
        }
    }

    // 销毁时清理列表（虽非必须，但好习惯）
    void OnDestroy()
    {
        victims.Clear();
    }
}