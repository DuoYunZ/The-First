using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerShield : MonoBehaviour
{
    public static PlayerShield Instance { get; private set; }

    [System.Serializable]
    public class ShieldChangedEvent : UnityEvent<int, int> { } // (当前护盾, 最大护盾)
    [Header("事件")]
    public ShieldChangedEvent OnShieldChanged;

    [Header("视觉效果")]
    [Tooltip("护盾受到伤害时生成的跳字预制件")]
    public GameObject shieldDamagePopupPrefab;
    [Tooltip("护盾受到伤害时在命中点生成的特效")]
    public GameObject shieldHitEffectPrefab;

    [Tooltip("护盾被直接击破时生成的跳字预制件")]
    public GameObject shieldBrokenPopupPrefab;

    [Header("运行时状态")]
    [SerializeField] private ShieldData equippedShieldData;
    [SerializeField] private int currentShieldValue;
    [SerializeField] private float cooldownTimer; 
    [SerializeField] private bool isUnlocked = false;

    private GameObject currentVisualInstance;

    // 护盾的最终属性（这部分逻辑是正确的）
    private int MaxShield => (equippedShieldData != null) ? Mathf.RoundToInt(equippedShieldData.baseMaxValue) : 0;
    private float Cooldown => (equippedShieldData != null) ? equippedShieldData.baseCooldown : 5f;

    [Header("反击设置")]
    [Tooltip("护盾反弹激光时，生成的玩家激光预制件")]
    public GameObject reflectedBeamPrefab;
    [Tooltip("【重要】指定一个发射反击光束的Transform点")]
    public Transform reflectionFirePoint; // <-- 修复 weaponMounts 报错

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // Update 方法是正确的，无需修改
        if (equippedShieldData != null && currentShieldValue <= 0 && cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                RegenerateShield();
            }
        }
    }

    // EquipShield, AbsorbDamage, BreakShield, RegenerateShield 方法都是正确的，无需修改

    public void EquipShield(ShieldData data)
    {
        if (data == null) return;
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
        }

        // 【新增】在装备时，将解锁状态设为 true
        isUnlocked = true;

        equippedShieldData = data;
        Debug.Log($"已装备护盾: {data.shieldName}");
        RegenerateShield();
    }

    public int AbsorbDamage(int damageAmount, Vector3 hitPosition, AttackType type, Projectile projectile, EnemyBeamAttack beamAttacker, out bool wasReflected)
    {
        Debug.Log($"<color=lime>[LOG 4 - 最终接收]</color> 护盾收到了攻击！类型是: {type}，来源是光束: {beamAttacker != null}");
        wasReflected = false;
        if (!isUnlocked) return damageAmount;

        // --- 1. 优先处理“击破护盾”的特殊攻击 ---
        if (type == AttackType.ShieldBreaking)
        {
            if (currentShieldValue > 0) // 只有在护盾有值时才触发破盾效果
            {
                if (shieldBrokenPopupPrefab != null)
                {
                    GameObject popupGO = Instantiate(shieldBrokenPopupPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
                    popupGO.GetComponent<StaticPopup>()?.Setup("护盾击破!", Color.yellow);
                }
                BreakShield();
            }
            return 0; // 无论护盾是否有值，破盾攻击都不穿透伤害
        }

        // 如果护盾已空，则不执行任何操作，直接返回全部伤害
        if (currentShieldValue <= 0) return damageAmount;

        // --- 2. 【统一】执行伤害吸收和反馈逻辑 ---
        int absorbedDamage = Mathf.Min(damageAmount, currentShieldValue);
        currentShieldValue -= absorbedDamage;

        if (absorbedDamage > 0)
        {
            if (shieldHitEffectPrefab != null) Instantiate(shieldHitEffectPrefab, hitPosition, Quaternion.identity);
            if (shieldDamagePopupPrefab != null)
            {
                GameObject popupGO = Instantiate(shieldDamagePopupPrefab, hitPosition + Vector3.up, Quaternion.identity);
                popupGO.GetComponent<DamagePopup>()?.Setup(absorbedDamage, true);
            }
        }

        OnShieldChanged?.Invoke(currentShieldValue, GetMaxShield());

        if (currentShieldValue <= 0)
        {
            BreakShield();
        }

        // --- 3. 在伤害吸收后，检查是否需要执行反弹 ---
        if (type == AttackType.Reflectable)
        {
            // --- 子弹反弹逻辑 ---
            if (projectile != null)
            {
                Debug.Log("护盾在承受伤害后，【反弹】了一枚子弹！");

                Vector3 incomingDirection = projectile.transform.forward;
                Vector3 flattenedNormal = (transform.position - projectile.transform.position);
                flattenedNormal.y = 0;

                Vector3 reflectionDirection = Vector3.Reflect(incomingDirection, -flattenedNormal.normalized);

                projectile.MarkAsPlayerProjectile();
                projectile.SetNewDirection(reflectionDirection);

                wasReflected = true;
                return 0; // 反弹成功，不造成任何穿透伤害
            }
            // --- 光束反弹逻辑 ---
            else if (beamAttacker != null)
            {
                TriggerReflectionBeam(beamAttacker);
                wasReflected = true; // 光束类攻击也被“处理”了，不应再有穿透伤害
                return 0;
            }
        }

        // --- 4. 如果是标准攻击，返回穿透的伤害 ---
        return damageAmount - absorbedDamage;
    }

    private void TriggerReflectionBeam(EnemyBeamAttack originalAttacker)
    {
        if (reflectedBeamPrefab == null || reflectionFirePoint == null || originalAttacker.attackData == null) return;

        Transform target = FindNearestEnemyTransform(originalAttacker.gameObject);
        if (target == null) target = originalAttacker.transform; // 如果没找到其他目标，就反弹给攻击者

        // 【修改】确保实例化的是带有 ReflectedBeamController 的预制件
        GameObject beamGO = Instantiate(reflectedBeamPrefab, reflectionFirePoint.position, reflectionFirePoint.rotation, reflectionFirePoint);
        ReflectedBeam beamController = beamGO.GetComponent<ReflectedBeam>(); // 获取新脚本的引用

        if (beamController != null)
        {
            Debug.Log("护盾触发反击光束！");
            // 【修改】使用新脚本的初始化方法
            beamController.Initialize(
                originalAttacker.attackData,
                WeaponController.Instance.gameObject,
                target,
                originalAttacker.transform
                //originalAttacker.GetRemainingDuration()
            );
        }
    }

    private Transform FindNearestEnemyTransform(GameObject excludeEnemy)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;

        // 【新增日志 A】显示搜索范围和排除目标
        Debug.Log($"--- 开始为反弹索敌 (排除: {excludeEnemy.name}) ---");

        Collider[] colliders = Physics.OverlapSphere(transform.position, 50f, LayerMask.GetMask("Enemies"));

        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider.gameObject == excludeEnemy) continue;

            Health enemyHealth = hitCollider.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                float dSqrToTarget = (transform.position - hitCollider.transform.position).sqrMagnitude;

                // 【新增日志 B】打印出每一个被考虑的目标和它的距离
                Debug.Log($"正在考虑目标: {hitCollider.name}, 距离平方: {dSqrToTarget.ToString("F2")}");

                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    nearestEnemy = enemyHealth.transform;

                    // 【新增日志 C】打印出何时更新了最近目标
                    Debug.Log($"<color=lime>更新最近目标为: {nearestEnemy.name}</color>");
                }
            }
        }

        // 【新增日志 D】报告最终的索敌结果
        if (nearestEnemy != null)
            Debug.Log($"--- 索敌结束, 最终选择: {nearestEnemy.name} ---");
        else
            Debug.LogWarning($"--- 索敌结束, 未找到有效目标 ---");

        return nearestEnemy;
    }

    private void BreakShield()
    {
        currentShieldValue = 0;
        cooldownTimer = Cooldown;
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
        }
        Debug.Log("护盾已击破！进入冷却...");
    }

    private void RegenerateShield()
    {
        if (!isUnlocked || equippedShieldData == null) return;

        currentShieldValue = MaxShield;
        cooldownTimer = 0;

        OnShieldChanged?.Invoke(currentShieldValue, GetMaxShield());

        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
        }

        if (equippedShieldData.shieldVisualPrefab != null)
        {
            // 1. 将护盾实例化为玩家的子对象
            currentVisualInstance = Instantiate(equippedShieldData.shieldVisualPrefab, transform);
            currentVisualInstance.transform.localPosition = Vector3.zero;
            currentVisualInstance.transform.localRotation = Quaternion.identity;

            // --- 【核心修改】 ---
            // 2. 获取玩家自身的 Rigidbody
            Rigidbody playerRb = GetComponent<Rigidbody>();

            // 3. 获取护盾实例上的 Fixed Joint
            FixedJoint joint = currentVisualInstance.GetComponent<FixedJoint>();

            // 4. 如果两者都存在，则将它们连接起来
            if (joint != null && playerRb != null)
            {
                joint.connectedBody = playerRb;
                Debug.Log("成功将护盾关节连接到玩家刚体。");
            }
            else
            {
                Debug.LogError("护盾连接失败：玩家或护盾预制件上缺少必要的 Rigidbody 或 Fixed Joint 组件！");
            }
            // --- 修改结束 ---
        }
        Debug.Log("护盾已再生！");
    }
    public void UnlockShield()
    {
        if (isUnlocked) return;
        isUnlocked = true;
        RegenerateShield(); // RegenerateShield 内部已经包含了事件通知
    }

    // --- 【核心修正2】修改 AddMaxShield 方法 ---
    public void AddMaxShield(int amount)
    {
        // 使用 isUnlocked 变量
        if (isUnlocked)
        {
            // 使用正确的变量名 currentShieldValue
            currentShieldValue += amount;

            // 确保当前护盾不会超过新的最大值
            if (currentShieldValue > MaxShield)
            {
                currentShieldValue = MaxShield;
            }
            OnShieldChanged?.Invoke(currentShieldValue, GetMaxShield());
        }
    }

    public int GetCurrentShield()
    {
        return currentShieldValue;
    }

    /// <summary>
    /// 返回最大护盾值
    /// </summary>
    public int GetMaxShield()
    {
        // 我们从 PlayerStats 获取最大值
        return PlayerStats.Instance != null ? PlayerStats.Instance.maxShield : 0;
    }
}