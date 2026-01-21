using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StatusEffectReceiver : MonoBehaviour
{
    [Header("引用")]
    private Health health; // 获取 AimTargetPoint 用

    private Health enemyHealth;
    private EnemyAI enemyAI; // 如果需要處理減速等影響AI的效果
    private StraightMoverAI straightMoverAI;
    private Animator animator; // <--- vvv 新增

    private EnemyMeleeAttack meleeAttackScript;
    private EnemyProjectileAttack projectileAttackScript; // <--- [新增]

    private Renderer enemyRenderer; // <--- vvv 新增
    private Color originalColor; // <--- vvv 新增


    private HashSet<object> persistentSlowSources = new HashSet<object>();
    private HashSet<object> persistentWeakenSources = new HashSet<object>();
    private HashSet<object> persistentCorrodeSources = new HashSet<object>(); // <--- [新增]

    private Color activePersistentSlowColor;
    private Color activePersistentCorrodeColor;

    // 用於追蹤正在進行的狀態協程，避免同一狀態重複疊加
    private Dictionary<DebuffType, Coroutine> activeStatusCoroutines = new Dictionary<DebuffType, Coroutine>();

    public bool IsSlowed { get; private set; } = false; // <--- [新增]
    public bool IsBurning { get; private set; } = false;
    public bool IsStunned { get; private set; } = false;

    [Header("状态属性 (运行时)")]
    public bool IsWeakened { get; private set; } = false;
    [Tooltip("弱化状态下的伤害乘数")]
    public float weakenDamageMultiplier { get; private set; } = 1.0f; // 1.0 = 100% 伤害

    public bool IsCorroded { get; private set; } = false; // <--- [新增]
    [Tooltip("腐蚀状态下的伤害乘数")]
    public float corrodeDamageMultiplier { get; private set; } = 1.0f; // <--- [新增]


    private float burnDurationRemaining = 0f;
    private int currentBurnDamagePerTick = 0;
    private float currentBurnTickInterval = 1f;

    [Header("特效预制件 (可选)")]
    public GameObject stunVfxPrefab;
    public GameObject burnVfxPrefab; // <--- vvv 新增

    [Tooltip("爆燃 (火焰石堆叠) 触发时的专属特效")]
    public GameObject ignitionVfxPrefab;

    private GameObject stunVfxInstance; // (我们把这个也改成私有变量)
    private GameObject burnVfxInstance; // <--- vvv 新增

    public bool IsElectrified { get; private set; } = false; // [新增] 感电状态
    public GameObject electrifiedVfxPrefab; // [新增] 感电特效
    private GameObject electrifiedVfxInstance;

    [Header("感电状态 (Shock)")]
    public bool IsShocked { get; private set; }
    private float shockTimer = 0f;
    private GameObject currentShockVfx;

    private string currentBurnSource = "";
    private string currentCorrodeSource = "";

    void Awake()
    {
        health = GetComponent<Health>();
        if (health == null) health = GetComponentInParent<Health>();

        enemyHealth = GetComponent<Health>();
        enemyAI = GetComponent<EnemyAI>();
        straightMoverAI = GetComponent<StraightMoverAI>();
        animator = GetComponentInChildren<Animator>(); // <--- vvv 新增
        meleeAttackScript = GetComponent<EnemyMeleeAttack>();
        projectileAttackScript = GetComponent<EnemyProjectileAttack>(); // <--- [新增]
        enemyRenderer = GetComponentInChildren<Renderer>(); // <--- vvv 新增

        if (enemyRenderer != null)
        {
            originalColor = enemyRenderer.material.color; // <--- vvv 新增 (保存原始颜色)
        }
    }

    void Update()
    {
        HandleShockState();
        // HandleBurnState(); 
    }
    /// <summary>
    /// 應用燃燒效果
    /// </summary>
    public void ApplyBurn(int damagePerTick, float duration, float tickInterval, string sourceWeaponName = "")
    {
        // 1. 存储燃烧数据 (用于爆燃)
        currentBurnDamagePerTick = damagePerTick;
        currentBurnTickInterval = (tickInterval > 0) ? tickInterval : 1f;
        // (如果已经燃烧，我们刷新(刷新/叠加)持续时间，而不是重置)
        burnDurationRemaining = Mathf.Max(burnDurationRemaining, duration);

        if (!string.IsNullOrEmpty(sourceWeaponName)) currentBurnSource = sourceWeaponName;

        if (!IsBurning)
        {
            if (activeStatusCoroutines.ContainsKey(DebuffType.Burn))
            {
                StopCoroutine(activeStatusCoroutines[DebuffType.Burn]);
            }
            Coroutine burnCoroutine = StartCoroutine(BurnRoutine());
            activeStatusCoroutines[DebuffType.Burn] = burnCoroutine;
        }
        if (sourceWeaponName == "火球术" || sourceWeaponName == "Fireball")
        {
            if (PlayerProgressManager.Instance != null)
            {
                PlayerProgressManager.Instance.AddStat("Ignite_Count", 1);
            }
        }
    }
    public void Ignite()
    {
        if (!IsBurning || enemyHealth == null) return; //

        // 1. 计算剩余伤害
        int remainingTicks = Mathf.FloorToInt(burnDurationRemaining / currentBurnTickInterval); //
        int ignitionDamage = remainingTicks * currentBurnDamagePerTick; //

        if (ignitionDamage > 0)
        {
            // 2. 造成爆燃伤害 (使用 Standard 或 Ignition 类型)
            enemyHealth.TakeDamage(ignitionDamage, transform.position, null, AttackType.Standard); //

            // --- vvv [新增] vvv ---
            // 3. 播放爆燃特效
            if (ignitionVfxPrefab != null)
            {
                // 在敌人当前位置播放一次性的爆燃特效
                Instantiate(ignitionVfxPrefab, transform.position, Quaternion.identity);
            }
            // --- ^^^ [新增] ^^^ ---
        }

        // 4. 停止燃烧
        StopBurn(); //
    }

    private void StopBurn()
    {
        if (!IsBurning) return;

        if (activeStatusCoroutines.ContainsKey(DebuffType.Burn))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Burn]);
            activeStatusCoroutines.Remove(DebuffType.Burn);
        }

        IsBurning = false;
        burnDurationRemaining = 0f;

        if (burnVfxInstance != null)
        {
            Destroy(burnVfxInstance);
            burnVfxInstance = null;
        }
    }


    // --- 新增：应用减速效果的方法 ---
    public void ApplySlow(float slowPercentage, float duration)
    {
        Color defaultSlowColor = Color.cyan;
        ApplySlow(slowPercentage, duration, defaultSlowColor);
    }

    public void ApplySlow(float slowPercentage, float duration, Color newColor)
    {
        if (activeStatusCoroutines.ContainsKey(DebuffType.Slow))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Slow]);
        }
        var slowCoroutine = StartCoroutine(SlowRoutine(slowPercentage, duration, newColor));
        activeStatusCoroutines[DebuffType.Slow] = slowCoroutine;
    }

    public void ApplyPersistentSlow(object source, float percentage, Color color)
    {
        if (persistentSlowSources.Add(source))
        {
            activePersistentSlowColor = color; // [!] 存储颜色
            UpdateSlowState();
        }
    }
    public void RemovePersistentSlow(object source)
    {
        if (persistentSlowSources.Remove(source))
        {
            UpdateSlowState();
        }
    }

    private void UpdateSlowState()
    {
        if (persistentSlowSources.Count > 0)
        {
            if (IsSlowed) return;
            IsSlowed = true;

            float speedMultiplier = 1.0f - 0.3f; // (TODO: 0.3f 应从 source 读取)

            if (enemyAI != null) enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed() * speedMultiplier);
            if (animator != null) animator.speed = speedMultiplier;
            if (enemyRenderer != null) enemyRenderer.material.color = activePersistentSlowColor; // [!] 使用存储的颜色
        }
        else
        {
            if (!IsSlowed) return;
            IsSlowed = false;

            if (enemyAI != null) enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed());
            if (animator != null) animator.speed = 1f;
            if (enemyRenderer != null) enemyRenderer.material.color = originalColor; // [!] 恢复原始颜色
        }
    }
    public void ApplyStun(float duration) //
    {
        if (meleeAttackScript != null)
        {
            meleeAttackScript.InterruptAttack(); //
        }

        if (projectileAttackScript != null)
            projectileAttackScript.InterruptAttack(); // <--- [新增]

        // --- vvv [ 核心修改 4 ] vvv ---
        // 使用 DebuffType.Stun 作为 Key
        if (activeStatusCoroutines.ContainsKey(DebuffType.Stun))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Stun]);
        }

        var stunCoroutine = StartCoroutine(StunRoutine(duration, stunVfxPrefab)); //
        // --- ^^^ [修改] ^^^ ---
        activeStatusCoroutines[DebuffType.Stun] = stunCoroutine; //
        // --- ^^^ [ 核心修改 4 ] ^^^ ---
    }

    public void ApplyStun(float duration, GameObject vfxOverride)
    {
        // (打断逻辑保持不变)
        if (meleeAttackScript != null)
            meleeAttackScript.InterruptAttack(); //
        if (projectileAttackScript != null)
            projectileAttackScript.InterruptAttack(); //

        if (activeStatusCoroutines.ContainsKey(DebuffType.Stun)) //
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Stun]); //
        }

        // (调用协程，并传递 *自定义* 的 vfxOverride)
        var stunCoroutine = StartCoroutine(StunRoutine(duration, vfxOverride)); //
        activeStatusCoroutines[DebuffType.Stun] = stunCoroutine; //
    }

    public void ApplyKnockback(Vector3 forceDirection, float forceAmount, float duration = 0.3f)
    {
        // 1. 打断攻击
        if (meleeAttackScript != null) meleeAttackScript.InterruptAttack();
        if (projectileAttackScript != null) projectileAttackScript.InterruptAttack();

        // 2. 转发给 AI
        if (enemyAI != null)
        {
            enemyAI.ApplyKnockback(forceDirection, forceAmount, duration);
        }
        if (straightMoverAI != null)
        {
            straightMoverAI.ApplyKnockback(forceDirection, forceAmount);
        }
    }

    public void ApplyWeaken(float percentage, float duration) //
    {
        // --- vvv [ 核心修改 5 - 这修复了你的 Bug ] vvv ---
        // 使用 DebuffType.Weaken 作为 Key
        if (activeStatusCoroutines.ContainsKey(DebuffType.Weaken))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Weaken]);
        }

        var weakenCoroutine = StartCoroutine(WeakenRoutine(percentage, duration));
        activeStatusCoroutines[DebuffType.Weaken] = weakenCoroutine;
        // --- ^^^ [ 核心修改 5 ] ^^^ ---
    }

    private IEnumerator WeakenRoutine(float percentage, float duration) //
    {
        // ... (协程内部逻辑保持不变) ...
        IsWeakened = true; //
        weakenDamageMultiplier = 1.0f - percentage; //
        yield return new WaitForSeconds(duration);
        IsWeakened = false; //
        weakenDamageMultiplier = 1.0f; //

        // (确保 Key 匹配)
        activeStatusCoroutines.Remove(DebuffType.Weaken); //
    }
    public void ApplyCorrode(float multiplier, float duration)
    {
        ApplyCorrode(multiplier, duration, new Color(0.5f, 1f, 0.5f)); // [!] 使用默认绿色
    }
    public void ApplyCorrode(float multiplier, float duration, Color color, string sourceWeaponName = "")
    {
        if (activeStatusCoroutines.ContainsKey(DebuffType.Corrode))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Corrode]);
        }
        var corrodeCoroutine = StartCoroutine(CorrodeRoutine(multiplier, duration, color)); // [!] 传递颜色
        activeStatusCoroutines[DebuffType.Corrode] = corrodeCoroutine; //
    }
    public void ApplyPersistentCorrode(object source, float multiplier, Color color) // [!] 传递颜色
    {
        if (persistentCorrodeSources.Add(source)) //
        {
            activePersistentCorrodeColor = color; // [!] 存储颜色
            UpdateCorrodeState(multiplier); //
        }
    }
    public void RemovePersistentCorrode(object source)
    {
        if (persistentCorrodeSources.Remove(source))
        {
            UpdateCorrodeState(1.0f); // (TODO: 应改为读取剩余源中的最大值)
        }
    }
    private void UpdateCorrodeState(float multiplier) //
    {
        if (persistentCorrodeSources.Count > 0) //
        {
            IsCorroded = true; //
            corrodeDamageMultiplier = multiplier; //
            if (enemyRenderer != null) enemyRenderer.material.color = activePersistentCorrodeColor; // [!] 应用颜色
        }
        else
        {
            IsCorroded = false; //
            corrodeDamageMultiplier = 1.0f; //
            if (enemyRenderer != null) enemyRenderer.material.color = originalColor; // [!] 恢复颜色
        }
    }
    private IEnumerator CorrodeRoutine(float multiplier, float duration, Color color) // [!] 传递颜色
    {
        IsCorroded = true; //
        corrodeDamageMultiplier = multiplier; //
        if (enemyRenderer != null) enemyRenderer.material.color = color; // [!] 应用颜色

        yield return new WaitForSeconds(duration);

        IsCorroded = false; //
        corrodeDamageMultiplier = 1.0f; //
        if (enemyRenderer != null) enemyRenderer.material.color = originalColor; // [!] 恢复颜色

        activeStatusCoroutines.Remove(DebuffType.Corrode); //
    }
    public void ApplyPersistentWeaken(object source, float percentage)
    {
        if (persistentWeakenSources.Add(source))
        {
            UpdateWeakenState();
        }
    }
    public void RemovePersistentWeaken(object source)
    {
        if (persistentWeakenSources.Remove(source))
        {
            UpdateWeakenState();
        }
    }
    private void UpdateWeakenState()
    {
        if (persistentWeakenSources.Count > 0)
        {
            IsWeakened = true; //
            weakenDamageMultiplier = 1.0f - 0.2f; // 假设减伤20% (TODO: 从 source 读取)
        }
        else
        {
            IsWeakened = false; //
            weakenDamageMultiplier = 1.0f; //
        }
    }

    private IEnumerator StunRoutine(float duration, GameObject vfxToUse)
    {
        IsStunned = true;

        if (vfxToUse != null && stunVfxInstance == null)
        {
            stunVfxInstance = Instantiate(vfxToUse, transform.position, Quaternion.identity, transform);
        }

        if (enemyAI != null) enemyAI.SetStunned(true); //
        if (straightMoverAI != null) straightMoverAI.SetStunned(true); //

        yield return new WaitForSeconds(duration);

        IsStunned = false;

        if (enemyAI != null) enemyAI.SetStunned(false); //
        if (straightMoverAI != null) straightMoverAI.SetStunned(false); //

        if (persistentSlowSources.Count > 0) //
        {
            // 重新应用光环 的减速 效果
            UpdateSlowState(); //
        }
        else
        {
            // (如果没有任何光环 效果，则安全地重置动画速度)
            if (animator != null) animator.speed = 1f;
        }

        if (stunVfxInstance != null)
        {
            Destroy(stunVfxInstance);
            stunVfxInstance = null;
        }

        activeStatusCoroutines.Remove(DebuffType.Stun); //
    }

    private IEnumerator SlowRoutine(float slowPercentage, float duration, Color newColor)
    {
        // (检查 enemyAI 和 straightMoverAI 是否为 null)
        if (enemyAI == null && straightMoverAI == null) yield break;

        float speedMultiplier = Mathf.Max(0, 1f - slowPercentage);

        // 1. 减速移动
        if (enemyAI != null)
        {
            float originalSpeed = enemyAI.GetOriginalMoveSpeed(); //
            enemyAI.SetMoveSpeed(originalSpeed * speedMultiplier); //
        }
        if (straightMoverAI != null)
        {
            // (确保你的 StraightMoverAI 也有 GetOriginalMoveSpeed 和 SetMoveSpeed)
        }

        // 2. 减速动画
        if (animator != null)
        {
            animator.speed = speedMultiplier;
        }

        // 3. 改变颜色
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = newColor;
        }

        yield return new WaitForSeconds(duration);

        if (persistentSlowSources.Count > 0) //
        {
            // (光环 仍在激活，所以这个瞬时协程不应该重置状态，
            //  它只需要停止自己即可)
            activeStatusCoroutines.Remove(DebuffType.Slow); //
            yield break; // 提前退出
        }

        // 持续时间结束后，恢复
        if (enemyAI != null)
        {
            enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed()); //
        }
        if (animator != null)
        {
            animator.speed = 1f;
        }
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = originalColor;
        }

        activeStatusCoroutines.Remove(DebuffType.Slow);
    }

    private IEnumerator BurnRoutine( )
    {
        IsBurning = true;

        // 1. 启动特效
        if (burnVfxPrefab != null && burnVfxInstance == null)
        {
            burnVfxInstance = Instantiate(burnVfxPrefab, transform.position, Quaternion.identity, transform);
        }

        // (使用类级变量)
        float tickTimer = currentBurnTickInterval;

        while (burnDurationRemaining > 0)
        {
            // 1. 等待
            float waitTime = Mathf.Min(tickTimer, burnDurationRemaining);
            yield return new WaitForSeconds(waitTime);

            burnDurationRemaining -= waitTime;
            tickTimer -= waitTime;

            // 2. 造成伤害
            if (tickTimer <= 0.01f)
            {
                if (enemyHealth != null && !enemyHealth.IsDead)
                {
                    enemyHealth.TakeDamage(currentBurnDamagePerTick, transform.position, null, AttackType.Standard, null, null, currentBurnSource);
                }
                else
                {
                    break; // 目标死亡
                }
                tickTimer = currentBurnTickInterval; // 重置跳字计时器
            }
        }

        // 3. 结束燃烧
        StopBurn();
    }
    public void ApplyElectrified(float duration)
    {
        if (activeStatusCoroutines.ContainsKey(DebuffType.Electrified)) // 需在枚举里加 Electrified
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Electrified]);
        }
        activeStatusCoroutines[DebuffType.Electrified] = StartCoroutine(ElectrifiedRoutine(duration));
    }
    private IEnumerator ElectrifiedRoutine(float duration)
    {
        IsElectrified = true;
        // 生成感电特效 (滋滋滋的电流)
        if (electrifiedVfxPrefab != null && electrifiedVfxInstance == null)
        {
            electrifiedVfxInstance = Instantiate(electrifiedVfxPrefab, transform.position, Quaternion.identity, transform);
        }

        yield return new WaitForSeconds(duration);

        IsElectrified = false;
        if (electrifiedVfxInstance != null) Destroy(electrifiedVfxInstance);
        activeStatusCoroutines.Remove(DebuffType.Electrified);
    }
    public void ApplyShock(float duration, GameObject vfxPrefab)
    {
        // 1. 刷新持续时间
        shockTimer = duration;

        // 2. 如果之前没有处于感电状态，或者是特效丢失了，就生成一个新的
        if (!IsShocked || currentShockVfx == null)
        {
            IsShocked = true;

            if (vfxPrefab != null)
            {
                // --- 【位置修正逻辑】 ---
                Transform mountParent = transform; // 默认挂在自己身上
                Vector3 offset = Vector3.zero;

                // 尝试找 Health 里的瞄准点
                if (health != null && health.AimTargetPoint != null)
                {
                    mountParent = health.AimTargetPoint;
                }
                else
                {
                    // 【保底方案】：如果没有 AimTargetPoint，强制向上偏移 1.0 米 (大概胸口位置)
                    // 注意：这里我们不挂载到 AimTargetPoint，而是挂载到 root，但修改本地坐标
                    offset = Vector3.up * 1.0f;
                }

                // 生成特效
                currentShockVfx = Instantiate(vfxPrefab, mountParent.position + offset, Quaternion.identity, mountParent);

                // 确保它确实挂上去了，并且位置正确
                currentShockVfx.transform.localPosition = (health != null && health.AimTargetPoint != null) ? Vector3.zero : offset;
            }
        }
    }

    private void HandleShockState()
    {
        if (IsShocked)
        {
            shockTimer -= Time.deltaTime;

            // 1. 倒计时结束清理
            if (shockTimer <= 0)
            {
                ClearShockEffect();
            }
            // 2. 如果怪物死了，也要清理 (防止特效遗留在尸体上)
            else if (health != null && health.IsDead)
            {
                ClearShockEffect();
            }
        }
    }
    private void ClearShockEffect()
    {
        IsShocked = false;
        if (currentShockVfx != null)
        {
            Destroy(currentShockVfx);
            currentShockVfx = null;
        }
    }

    // --- 【关键】当物体被禁用/销毁时，强制清理特效 ---
    void OnDisable()
    {
        ClearShockEffect();
    }
}