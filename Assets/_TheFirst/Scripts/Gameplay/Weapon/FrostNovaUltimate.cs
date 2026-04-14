using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 冰霜新星大招：冰爽之星
/// 在前方生成一个大型冰晶体，冰冻范围内敌人并阻碍移动
/// </summary>
[RequireComponent(typeof(Collider))]
public class FrostNovaUltimate : MonoBehaviour
{
    [Header("基础设置")]
    [Tooltip("冰冻半径")]
    public float freezeRadius = 5f;
    [Tooltip("冰冻持续时间")]
    public float freezeDuration = 3f;
    [Tooltip("大招伤害")]
    public int damage = 150;
    [Tooltip("大招存在时间")]
    public float lifetime = 10f;

    [Header("阻挡设置")]
    [Tooltip("碰撞体是否阻碍敌人移动")]
    public bool blockEnemies = true;

    [Header("持续冰冻")]
    [Tooltip("进入范围的敌人持续冰冻间隔")]
    public float rechillInterval = 1f;

    // 内部变量
    private float rechillTimer = 0f;
    private LayerMask enemyLayer;
    private HashSet<int> alreadyHit = new HashSet<int>(); // 初始爆发只命中一次

    void Start()
    {
        // 支持 Enemy 和 Enemies 两种 layer 命名
        enemyLayer = LayerMask.GetMask("Enemy") | LayerMask.GetMask("Enemies");
        // 设置层为 Default，确保和 Enemy 层碰撞生效
        gameObject.layer = LayerMask.NameToLayer("Default");
        // 子物体也设置为 Default
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = gameObject.layer;
        }

        // 确保碰撞体为非触发（阻挡敌人移动）
        Collider col = GetComponent<Collider>();
        if (col != null && blockEnemies)
        {
            col.isTrigger = false;

            // 添加刚体（Kinematic）使碰撞生效但不受物理影响
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // 忽略与玩家的碰撞，只阻挡敌人
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                Collider[] playerCols = GameManager.Instance.playerTransform.GetComponentsInChildren<Collider>();
                foreach (var playerCol in playerCols)
                {
                    Physics.IgnoreCollision(col, playerCol, true);
                }
            }
        }

        // 添加 NavMeshObstacle 阻挡敌人寻路
        // NavMeshAgent 不受物理碰撞体影响，必须用 NavMeshObstacle 才能阻断路径
        if (blockEnemies)
        {
            UnityEngine.AI.NavMeshObstacle obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true; // 在 NavMesh 上挖洞，阻止敌人通过
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;

            // 用碰撞体大小来设置障碍物大小
            Collider existingCol = GetComponent<Collider>();
            if (existingCol is SphereCollider sphere)
            {
                obstacle.radius = sphere.radius * transform.lossyScale.x;
                obstacle.height = sphere.radius * 2f * transform.lossyScale.y;
            }
            else if (existingCol is BoxCollider box)
            {
                obstacle.radius = Mathf.Max(box.size.x, box.size.z) * 0.5f * transform.lossyScale.x;
                obstacle.height = box.size.y * transform.lossyScale.y;
            }
            else if (existingCol is CapsuleCollider capsule)
            {
                obstacle.radius = capsule.radius * transform.lossyScale.x;
                obstacle.height = capsule.height * transform.lossyScale.y;
            }
            else
            {
                // 默认大小
                obstacle.radius = 3f;
                obstacle.height = 5f;
            }
        }

        // 初始爆发：冰冻范围内所有敌人
        DealInitialBurst();

        // 自动销毁
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// 初始爆发：范围伤害+冰冻
    /// </summary>
    void DealInitialBurst()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, freezeRadius, enemyLayer);
        int hitCount = 0;

        foreach (var hit in hits)
        {
            int id = hit.gameObject.GetInstanceID();
            if (alreadyHit.Contains(id)) continue;
            alreadyHit.Add(id);

            // 造成伤害
            Health h = hit.GetComponent<Health>();
            if (h == null) h = hit.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                h.TakeDamage(damage, transform.position, gameObject);

                // 冰冻
                StatusEffectReceiver receiver = h.GetComponent<StatusEffectReceiver>();
                if (receiver == null) receiver = h.GetComponentInParent<StatusEffectReceiver>();
                if (receiver != null)
                {
                    receiver.ApplyFreeze(freezeDuration);
                }
                hitCount++;
            }
        }

    }

    // 后续只靠 NavMeshObstacle 阻挡移动，不再持续冰冻
}
