using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

[RequireComponent(typeof(WeaponPart))]
public class DroneAI : MonoBehaviour
{
    private enum DroneState { Orbiting, Attacking }
    private DroneState currentState = DroneState.Orbiting;


    [Header("AI 设置")]
    [Tooltip("无人机的索敌范围")]
    public float detectionRange = 25f;
    [Tooltip("无人机与目标的理想保持距离")]
    public float idealDistance = 10f;
    [Tooltip("跟随时与玩家的距离")]
    public float followDistance = 5f;
    [Tooltip("无人机的飞行速度")]
    public float moveSpeed = 15f;
    [Tooltip("无人机的转向速度")]
    public float turnSpeed = 10f;
    [Tooltip("无人机索敌的目标层级")]
    public LayerMask enemyLayer;

    [Header("巡航设置 (Orbit Settings)")]
    [Tooltip("围绕玩家旋转的基础速度")]
    public float orbitSpeed = 50f;
    [Tooltip("巡航路径的随机扰动幅度")]
    public float orbitNoiseStrength = 0.5f;
    [Tooltip("巡航路径的随机扰动变化速度")]
    public float orbitNoiseSpeed = 0.2f;

    // 内部变量
    private Transform currentTarget;
    private WeaponPart myWeaponPart;
    private float lifeTimer;
    private Transform ownerTransform; // 【新增】主人的Transform
    private float flightAltitude; // 【新增】用于存储飞行高度

    // 用于轨道巡航的变量
    private float orbitAngle = 0f;
    private float noiseSeed; // 每个无人机拥有自己独立的随机种子
    /// <summary>
    /// 由召唤者（玩家）在实例化后调用，用于传递属性
    /// </summary>
    public void Initialize(WeaponStatBlock droneWeaponStats, float duration, Transform owner)
    {
        myWeaponPart = GetComponent<WeaponPart>();
        myWeaponPart.StatBlock = droneWeaponStats;
        this.lifeTimer = duration;
        this.ownerTransform = owner;
        this.flightAltitude = transform.position.y;
        this.orbitAngle = Random.Range(0f, 360f);
        this.noiseSeed = Random.Range(0f, 100f); // 为柏林噪声设置随机种子

        if (duration > 0)
        {
            Destroy(gameObject, duration);
        }
    }

    void Update()
    {
        FindTarget(); // 每帧都尝试索敌

        switch (currentState)
        {
            case DroneState.Orbiting:
                OrbitOwner(); // 【修改】将 FollowOwner 改为 OrbitOwner
                break;
            case DroneState.Attacking:
                AttackTarget();
                break;
        }
    }

    void FindTarget()
    {
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, detectionRange, enemyLayer);
        if (enemiesInRange.Length > 0)
        {
            currentTarget = enemiesInRange[0].transform;
            currentState = DroneState.Attacking;
        }
        else
        {
            currentTarget = null;
            currentState = DroneState.Orbiting;
        }
    }

    void OrbitOwner()
    {
        if (ownerTransform == null) return;

        // 1. 更新轨道角度
        orbitAngle += orbitSpeed * Time.deltaTime;

        // 2. 使用柏林噪声为巡航半径添加随机扰动，使其路径不规则
        float noise = (Mathf.PerlinNoise(noiseSeed, Time.time * orbitNoiseSpeed) - 0.5f) * 2f; // 范围从 -1 到 1
        float currentFollowDistance = followDistance + noise * orbitNoiseStrength;

        // 3. 计算轨道上的目标点
        float offsetX = Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * currentFollowDistance;
        float offsetZ = Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * currentFollowDistance;
        Vector3 orbitPosition = ownerTransform.position + new Vector3(offsetX, 0, offsetZ);
        orbitPosition.y = this.flightAltitude;

        // 4. 计算移动方向（即轨道的切线方向）
        Vector3 moveDirection = (orbitPosition - transform.position).normalized;

        // 5. 移动
        transform.position = Vector3.Slerp(transform.position, orbitPosition, moveSpeed * Time.deltaTime);

        // 6. 【朝向修复】让无人机朝向它正在飞行的方向
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    void AttackTarget()
    {
        if (currentTarget == null)
        {
            currentState = DroneState.Orbiting;
            return;
        }

        // --- 【核心修改】 ---

        // 1. 在索敌到的敌人身上，优先寻找我们设置的“被瞄准点”
        Transform enemyAimTarget = currentTarget.transform.Find("AimTargetPoint");
        // 如果敌人身上没有这个点（例如一些简单的敌人），则后备为攻击其根对象
        if (enemyAimTarget == null)
        {
            enemyAimTarget = currentTarget.transform;
        }

        // 2. 移动逻辑：无人机自身的位置计算，依然基于敌人的根对象
        Vector3 directionToTarget2D = (currentTarget.position - transform.position);
        directionToTarget2D.y = 0;
        directionToTarget2D.Normalize();
        Vector3 targetPosition = currentTarget.position - directionToTarget2D * idealDistance;
        targetPosition.y = this.flightAltitude;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // 3. 转向和开火逻辑：使用我们找到的精确瞄准点 enemyAimTarget
        Vector3 preciseDirection = (enemyAimTarget.position - transform.position).normalized;

        if (preciseDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(preciseDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // 将精确的3D方向传递给武器部件
        myWeaponPart.Fire(preciseDirection);
    }
}