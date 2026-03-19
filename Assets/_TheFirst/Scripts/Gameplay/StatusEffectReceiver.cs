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
    private float currentMaxHealthBurnPercent = 0f; // 【新增】猛烈燃烧：每跳附加最大生命值百分比

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

    public bool IsFrozen { get; private set; } = false;
    public GameObject freezeVfxPrefab; // 【新增】在 Inspector 里拖一个冰块特效
    private GameObject freezeVfxInstance;

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
    public void ApplyBurn(int damagePerTick, float duration, float tickInterval, string sourceWeaponName = "", float maxHealthBurnPercent = 0f)
    {
        // 1. 存储燃烧数据 (用于爆燃)
        currentBurnDamagePerTick = damagePerTick;
        currentBurnTickInterval = (tickInterval > 0) ? tickInterval : 1f;
        // (如果已经燃烧，我们刷新(刷新/叠加)持续时间，而不是重置)
        burnDurationRemaining = Mathf.Max(burnDurationRemaining, duration);
        // 【新增】猛烈燃烧：存储最大生命值百分比伤害 (取最大值，避免降级)
        currentMaxHealthBurnPercent = Mathf.Max(currentMaxHealthBurnPercent, maxHealthBurnPercent);

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
        currentMaxHealthBurnPercent = 0f; // [新增] 清理百分比伤害

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

    public void ApplyFreeze(float duration, GameObject vfxOverride = null, bool applyFrostBite = false)
    {
        // 1. 打断攻击
        if (meleeAttackScript != null) meleeAttackScript.InterruptAttack();
        if (projectileAttackScript != null) projectileAttackScript.InterruptAttack();

        // ==========================================
        // 【核心修复 1】停止旧冰冻时，必须强制解冻！
        // ==========================================
        // 防止旧协程被 Kill 后，怪物卡在眩晕状态
        if (activeStatusCoroutines.ContainsKey(DebuffType.Freeze))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Freeze]);
            activeStatusCoroutines.Remove(DebuffType.Freeze);

            // 强制清理残留状态 (保险起见)
            if (freezeVfxInstance != null) Destroy(freezeVfxInstance);
            IsFrozen = false;
            if (enemyAI != null) enemyAI.SetStunned(false); // <--- 关键！
            if (straightMoverAI != null) straightMoverAI.SetStunned(false);
        }

        // 2. 确定特效
        GameObject vfxToUse = (vfxOverride != null) ? vfxOverride : freezeVfxPrefab;

        // 3. 启动新协程
        var freezeCoroutine = StartCoroutine(FreezeRoutine(duration, vfxToUse, applyFrostBite));
        activeStatusCoroutines[DebuffType.Freeze] = freezeCoroutine;
    }
    private IEnumerator FreezeRoutine(float duration, GameObject vfxToUse, bool applyFrostBite)
    {
        IsFrozen = true;

        // --- 控制 ---
        if (enemyAI != null) enemyAI.SetStunned(true);
        if (straightMoverAI != null) straightMoverAI.SetStunned(true);
        if (animator != null) animator.speed = 0f;

        // --- 视觉 (变蓝) ---
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = new Color(0.3f, 0.6f, 1f);
        }

        // ==========================================
        // 【核心修复 2】特效位置修正 (生成在脚底)
        // ==========================================
        if (vfxToUse != null)
        {
            try
            {
                // 直接生成在 transform.position (脚底)，不要去 AimTargetPoint 了
                freezeVfxInstance = Instantiate(vfxToUse, transform.position, Quaternion.identity, transform);

                // 如果需要确保特效不随怪物旋转，可以把最后一参数 transform 去掉，
                // 或者生成后 reset rotation:
                // freezeVfxInstance.transform.rotation = Quaternion.identity;
            }
            catch (System.Exception e)
            {
                // 捕获错误，防止因为特效问题导致后续解冻逻辑不执行
                Debug.LogError($"[Freeze] 特效生成失败: {e.Message}");
            }
        }

        // --- 等待（加入刺骨寒霜扣血判断） ---
        float elapsed = 0f;
        float tickTimer = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            
            if (applyFrostBite)
            {
                tickTimer += Time.deltaTime;
                if (tickTimer >= 1f)
                {
                    tickTimer -= 1f;
                    if (health != null)
                    {
                        // 扣除1%最大生命值
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(health.maxHealth * 0.01f));
                        // 传入空 source 避免再次触发层级伤害特效，也可以传 transform.gameObject
                        health.TakeDamage(dmg, transform.position, null);
                        Debug.Log($"<color=#88DDFF>[刺骨寒霜] 对 {gameObject.name} 造成 {dmg} 点冰冻扣血！</color>");
                    }
                }
            }
        }

        // ==========================================
        // 【核心修复 3】稳健的恢复逻辑
        // ==========================================
        IsFrozen = false;

        // 恢复行动
        if (enemyAI != null) enemyAI.SetStunned(false);
        if (straightMoverAI != null) straightMoverAI.SetStunned(false);

        // 销毁特效
        if (freezeVfxInstance != null)
        {
            Destroy(freezeVfxInstance);
            freezeVfxInstance = null;
        }

        // 恢复颜色和动画
        // 检查：如果此时怪物身上还有【减速】状态 (来自子弹 IsSlowed 或 光环 persistentSlowSources)
        // 应该恢复成减速的样子，而不是完全恢复原样
        bool isStillSlowed = IsSlowed || persistentSlowSources.Count > 0;

        if (isStillSlowed)
        {
            UpdateSlowState(); // 恢复为减速状态 (颜色变青，动画变慢)
        }
        else
        {
            // 完全恢复
            if (enemyRenderer != null) enemyRenderer.material.color = originalColor;
            if (animator != null) animator.speed = 1f;
        }

        // 移除记录
        if (activeStatusCoroutines.ContainsKey(DebuffType.Freeze))
        {
            activeStatusCoroutines.Remove(DebuffType.Freeze);
        }
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
            // 眩晕特效挂到头顶 HP_Head
            Transform headMount = transform.Find("HP_Head");
            if (headMount == null && health != null && health.AimTargetPoint != null)
                headMount = health.AimTargetPoint;
            Transform mountParent = headMount != null ? headMount : transform;
            stunVfxInstance = Instantiate(vfxToUse, mountParent.position, Quaternion.identity, mountParent);
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
        // 1. 标记状态 (核心修复!)
        IsSlowed = true;

        // 2. 减速逻辑 (保持不变)
        if (enemyAI == null && straightMoverAI == null) yield break;

        float speedMultiplier = Mathf.Max(0, 1f - slowPercentage);

        if (enemyAI != null) enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed() * speedMultiplier);
        // if (straightMoverAI != null) ...

        if (animator != null) animator.speed = speedMultiplier;

        if (enemyRenderer != null) enemyRenderer.material.color = newColor;

        // 3. 等待
        yield return new WaitForSeconds(duration);

        // 4. 恢复状态 (核心修复!)
        // 如果没有光环在作用，才取消 IsSlowed
        if (persistentSlowSources.Count == 0)
        {
            IsSlowed = false;

            // 恢复速度和颜色
            if (enemyAI != null) enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed());
            if (animator != null) animator.speed = 1f;
            if (enemyRenderer != null) enemyRenderer.material.color = originalColor;
        }
        else
        {
            // 如果还有光环，就转交给光环逻辑处理
            UpdateSlowState();
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
                    // 【新增】猛烈燃烧：基础燃烧伤害 + 最大生命值百分比附加
                    int finalBurnDmg = currentBurnDamagePerTick;
                    if (currentMaxHealthBurnPercent > 0f)
                    {
                        int maxHpDmg = Mathf.CeilToInt(enemyHealth.maxHealth * currentMaxHealthBurnPercent);
                        finalBurnDmg += maxHpDmg;
                    }
                    enemyHealth.TakeDamage(finalBurnDmg, transform.position, null, AttackType.Standard, null, null, currentBurnSource);
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

    /// <summary>
    /// 麻痹效果：复用感电VFX，但实际停止敌人移动（独立于眩晕）
    /// </summary>
    public void ApplyParalyze(float duration)
    {
        if (activeStatusCoroutines.ContainsKey(DebuffType.Paralyze))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.Paralyze]);
        }
        activeStatusCoroutines[DebuffType.Paralyze] = StartCoroutine(ParalyzeRoutine(duration));
    }

    private IEnumerator ParalyzeRoutine(float duration)
    {
        IsStunned = true; // 实际停止移动

        // 复用感电VFX
        GameObject paralyzeVfx = null;
        if (electrifiedVfxPrefab != null)
        {
            paralyzeVfx = Instantiate(electrifiedVfxPrefab, transform.position, Quaternion.identity, transform);
        }

        if (enemyAI != null) enemyAI.SetStunned(true);
        if (straightMoverAI != null) straightMoverAI.SetStunned(true);

        yield return new WaitForSeconds(duration);

        IsStunned = false;
        if (enemyAI != null) enemyAI.SetStunned(false);
        if (straightMoverAI != null) straightMoverAI.SetStunned(false);

        if (persistentSlowSources.Count > 0)
        {
            UpdateSlowState();
        }
        else
        {
            if (animator != null) animator.speed = 1f;
        }

        if (paralyzeVfx != null) Destroy(paralyzeVfx);
        activeStatusCoroutines.Remove(DebuffType.Paralyze);
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

    // === 脆弱印记 (FragileMark) ===
    [HideInInspector] public bool IsFragile = false;
    [HideInInspector] public float fragileDamageMultiplier = 1f;
    private Coroutine fragileCoroutine;

    /// <summary>
    /// 施加脆弱印记，使敌人受到的伤害增加
    /// </summary>
    public void ApplyFragileMark(float damageMultiplierIncrease, float duration)
    {
        if (fragileCoroutine != null) StopCoroutine(fragileCoroutine);
        fragileCoroutine = StartCoroutine(FragileMarkRoutine(damageMultiplierIncrease, duration));
    }

    private IEnumerator FragileMarkRoutine(float damageMultiplierIncrease, float duration)
    {
        IsFragile = true;
        fragileDamageMultiplier = 1f + damageMultiplierIncrease;
        yield return new WaitForSeconds(duration);
        IsFragile = false;
        fragileDamageMultiplier = 1f;
        fragileCoroutine = null;
    }

    /// <summary>
    /// 光环专用减速（独立于冰系减速，两者可叠加）
    /// </summary>
    public void ApplyAuraSlow(float slowPercentage, float duration)
    {
        if (activeStatusCoroutines.ContainsKey(DebuffType.AuraSlow))
        {
            StopCoroutine(activeStatusCoroutines[DebuffType.AuraSlow]);
        }
        var auraSlowCoroutine = StartCoroutine(AuraSlowRoutine(slowPercentage, duration));
        activeStatusCoroutines[DebuffType.AuraSlow] = auraSlowCoroutine;
    }

    private IEnumerator AuraSlowRoutine(float slowPercentage, float duration)
    {
        if (enemyAI == null) yield break;

        float speedMultiplier = Mathf.Max(0, 1f - slowPercentage);
        enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed() * speedMultiplier);

        yield return new WaitForSeconds(duration);

        // 恢复速度（如果没有其他减速在作用）
        if (enemyAI != null && !IsSlowed)
        {
            enemyAI.SetMoveSpeed(enemyAI.GetOriginalMoveSpeed());
        }
        activeStatusCoroutines.Remove(DebuffType.AuraSlow);
    }

    // --- 【关键】当物体被禁用/销毁时，强制清理特效 ---
    void OnDisable()
    {
        ClearShockEffect();
    }
}