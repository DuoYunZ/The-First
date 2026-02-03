// 创建新脚本 Orbiter.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Orbiter : MonoBehaviour
{
    [Header("基础设置")]
    public float selfRotationSpeed = 1440f;
    private int damage = 10;
    private WeaponPart launcher;

    [Header("冷却设置")]
    private float hitCooldown = 0.5f;
    private float lastHitTime = -1f;
    private Dictionary<Health, float> hitTargetsCooldown = new Dictionary<Health, float>();

    [Header("风暴进化 (Wind Evolution)")]
    private float currentSpinSpeed;
    private float windSpinTimer = 0f;
    // 假设 WeaponStatBlock 里我们要去读取这些特殊参数，
    // 为了方便，这里也可以直接定义默认值，或者从 launcher.StatBlock 读取自定义字段
    private float windMaxSpeedMultiplier = 3.0f; // 最大转速倍率
    private float windRampUpTime = 3.0f; // 加速所需时间
    private bool isWindEvolution = false;

    [Header("大地进化 (Earth Evolution)")]
    private bool isEarthEvolution = false;

    [Header("雷电进化 (Lightning Evolution)")]
    private bool isLightningEvolution = false;

    public void Initialize(int damage, WeaponPart part)
    {
        this.damage = damage;
        this.launcher = part;
        this.currentSpinSpeed = selfRotationSpeed;

        // --- 识别进化类型 ---
        if (launcher != null && launcher.StatBlock != null)
        {
            WeaponStatBlock stats = launcher.StatBlock;

            // 这里我们通过简单的逻辑判断进化类型
            // 实际项目中，建议在 WeaponStatBlock 里加个 Enum EvolutionType
            isEarthEvolution = stats.weaponName.Contains("盾") || stats.weaponName.Contains("Shield"); // 示例判断
            isWindEvolution = stats.weaponName.Contains("风") || stats.weaponName.Contains("Storm");
            isLightningEvolution = stats.baseChainCount > 0 || stats.weaponName.Contains("雷");

            // 如果是风属性，应用原生击退
            if (stats.nativeKnockback) isWindEvolution = true;
        }
    }

    void Update()
    {
        float speedToUse = selfRotationSpeed;

        // --- 风暴进化逻辑：越转越快 ---
        if (isWindEvolution)
        {
            windSpinTimer += Time.deltaTime;

            // 计算当前加速比例 (0 到 1)
            float t = Mathf.Clamp01(windSpinTimer / windRampUpTime);
            // 速度插值
            speedToUse = Mathf.Lerp(selfRotationSpeed, selfRotationSpeed * windMaxSpeedMultiplier, t);

            // 达到最大速度，甩出龙卷风并重置
            if (windSpinTimer >= windRampUpTime)
            {
                ThrowTornado();
                windSpinTimer = 0f; // 重置，重新开始加速循环
            }
        }

        // 绕 Y 轴旋转
        transform.Rotate(Vector3.up, speedToUse * Time.deltaTime);
    }

    private void ThrowTornado()
    {
        if (launcher == null || launcher.StatBlock == null) return;

        if (launcher.StatBlock.subProjectilePrefab != null)
        {
            // 1. 计算抛出方向 (背离圆心)
            Vector3 throwDir = Vector3.zero;

            if (transform.parent != null)
            {
                throwDir = (transform.position - transform.parent.position).normalized;
            }

            // 【修复】如果算出来的方向是0 (比如圆心重合)，就默认向前飞，防止不动
            if (throwDir == Vector3.zero)
            {
                throwDir = transform.forward;
            }

            // 强制水平
            throwDir.y = 0;
            throwDir.Normalize();

            // 2. 生成龙卷风
            // 稍微向外偏移一点生成，防止和盾牌重叠
            Vector3 spawnPos = transform.position + throwDir * 1.0f;

            GameObject tornado = Instantiate(launcher.StatBlock.subProjectilePrefab, spawnPos, Quaternion.LookRotation(throwDir));

            // 3. 初始化
            Projectile p = tornado.GetComponent<Projectile>();
            int tornadoDmg = Mathf.RoundToInt(damage * 0.5f);
            if (tornadoDmg < 1) tornadoDmg = 1;

            if (p != null)
            {
                p.InitializeAsStraight(
                    throwDir,
                    8f, // 速度稍微慢一点，方便吸怪
                    0,  // 【关键】Projectile 直接伤害设为 0！完全由 TornadoController 接管伤害
                    false,
                    999, // 无限穿透
                    4f,  // 存活 4 秒
                    launcher.StatBlock.shieldImpactEffectPrefab,
                    launcher.StatBlock.defaultImpactEffectPrefab,
                    0, 0, 0, 0, 0,
                    AttackType.Standard,
                    launcher
                );
            }

            // 4. 【新增】初始化龙卷风逻辑 (TornadoController)
            TornadoController tc = tornado.GetComponent<TornadoController>();
            if (tc != null)
            {
                tc.Setup(tornadoDmg, launcher);
            }
            else
            {
                Debug.LogWarning("生成的龙卷风预制体上缺少 'TornadoController' 脚本！");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isEarthEvolution)
        {
            if (other.CompareTag("EnemyProjectile"))
            {
                // 播放一个格挡特效 (如果需要)
                // Instantiate(blockEffect, transform.position, ...);

                Destroy(other.gameObject);
                return; // 挡住子弹就不处理后面的伤害逻辑了
            }
        }

        if (!other.CompareTag("Enemy")) return;

        Health enemyHealth = other.GetComponentInParent<Health>();

        // 检查是否获取到有效的Health组件，以及敌人是否已死亡
        if (enemyHealth == null || enemyHealth.IsDead) return;

        // 【修改后】的冷却判断逻辑
        // 检查1: 字典里是否已经有这个敌人了？
        if (hitTargetsCooldown.ContainsKey(enemyHealth))
        {
            // 如果有，再检查它的独立冷却时间是否已过
            if (Time.time > hitTargetsCooldown[enemyHealth] + hitCooldown)
            {
                // 冷却已过，可以再次造成伤害
                ApplyDamage(enemyHealth);
            }
            // 如果冷却没过，则什么都不做
        }
        else
        {
            // 如果字典里没有这个敌人，说明是第一次命中，直接造成伤害
            ApplyDamage(enemyHealth);
        }
    }
    private void ApplyDamage(Health enemyHealth)
    {
        // 1. 造成基础伤害
        enemyHealth.TakeDamage(damage, transform.position, this.gameObject, AttackType.Standard);

        // 2. 更新该敌人的冷却时间
        hitTargetsCooldown[enemyHealth] = Time.time;

        // 3. 统计伤害数据
        if (BattleStatisticsManager.Instance != null && launcher != null && launcher.StatBlock != null)
        {
            BattleStatisticsManager.Instance.AddDamage(launcher.StatBlock.weaponName, damage);
        }

        // =========================================================
        //  核心逻辑：元素与异常状态判定
        // =========================================================

        // 基础安全检查
        if (launcher == null) return;

        StatusEffectReceiver receiver = enemyHealth.GetComponent<StatusEffectReceiver>();
        WeaponStatBlock stats = launcher.StatBlock;
        EnergyStoneSO stone = launcher.currentStone; // 获取当前镶嵌的石头

        // 如果没有接收器或武器数据，无法应用特效
        if (receiver == null || stats == null) return;

        // ---------------------------------------------------------
        // 1. 雷电逻辑 (Lightning Logic) - [核心修改：感电联动]
        // ---------------------------------------------------------
        // 判定条件：是雷电进化武器 OR 镶嵌了雷石
        bool isLightningWeapon = isLightningEvolution || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain));

        if (isLightningWeapon)
        {
            // 启动协程，处理雷电的延迟触发
            StartCoroutine(DelayedLightningRoutine(enemyHealth, receiver, stone, stats));
        }

        // ---------------------------------------------------------
        // 2. 火焰逻辑 (Fire Logic)
        // ---------------------------------------------------------
        bool hasBurn = stats.nativeBurn || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn));

        if (hasBurn)
        {
            int fireStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyBurn);

            // 确定数值：优先用石头，否则用原生
            int bDmg = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnDamage : stats.baseDotDamage;
            float bDur = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnDuration : stats.baseDotDuration;
            float bTick = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyBurn)) ? stone.burnTickInterval : stats.dotTickInterval;

            // 堆叠引爆逻辑
            if (fireStoneCount >= 2 && receiver.IsBurning)
            {
                receiver.Ignite();
            }
            else if (!receiver.IsBurning)
            {
                // 施加燃烧
                receiver.ApplyBurn(bDmg, bDur, bTick, stats.weaponName);
            }
        }

        // ---------------------------------------------------------
        // 3. 风暴/击退逻辑 (Wind Logic)
        // ---------------------------------------------------------
        // 这里的击退对于环绕物很有用，可以把怪推开防止贴脸
        bool hasKnockback = stats.nativeKnockback || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback));

        if (hasKnockback)
        {
            float kForce = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyKnockback)) ? stone.knockbackForce : stats.nativeKnockbackForce;

            // 堆叠增强
            int windStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyKnockback);
            if (windStoneCount >= 2 && stone != null) kForce = stone.knockbackForce_Stacked;

            // 计算方向：从玩家中心 -> 推向怪物 (径向推开)
            Vector3 pushDir = (enemyHealth.transform.position - transform.parent.position).normalized;
            pushDir.y = 0;

            // 施加击退 (给予较短的时间 0.1f，因为环绕物攻速快)
            receiver.ApplyKnockback(pushDir, kForce, 0.1f);
        }

        // ---------------------------------------------------------
        // 4. 寒冰/减速逻辑 (Ice Logic)
        // ---------------------------------------------------------
        if (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplySlow))
        {
            receiver.ApplySlow(stone.slowPercentage, stone.slowDuration, stone.slowColor);

            int iceStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplySlow);
            // 冰冻判定
            if (iceStoneCount >= 2 && receiver.IsSlowed && !receiver.IsStunned)
            {
                if (Random.value <= stone.freezeChance)
                {
                    receiver.ApplyStun(stone.freezeDuration, stone.freezeVfxPrefab);
                }
            }
        }

        // ---------------------------------------------------------
        // 5. 腐蚀/剧毒逻辑 (Corrode Logic)
        // ---------------------------------------------------------
        bool hasCorrode = stats.nativeCorrode || (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode));

        if (hasCorrode)
        {
            float mult = 1.0f;
            Color cColor = stats.nativeCorrodeColor;

            if (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyCorrode))
            {
                int poisonCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyCorrode);
                mult = (poisonCount >= 2) ? stone.corrodeMultiplier_Stacked : stone.corrodeMultiplier;
                cColor = stone.corrodeColor;
            }
            else
            {
                mult = stats.nativeCorrodeMultiplier;
            }

            receiver.ApplyCorrode(mult, 5f, cColor, stats.weaponName);
        }

        // ---------------------------------------------------------
        // 6. 大地/眩晕逻辑 (Earth Logic)
        // ---------------------------------------------------------
        if (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyStun))
        {
            int earthCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyStun);
            float chance = (earthCount >= 2) ? stone.stunChance_Stacked : stone.stunChance;

            if (Random.value <= chance && !receiver.IsStunned)
            {
                receiver.ApplyStun(stone.stunDuration);
            }
        }
    }

    private IEnumerator DelayedLightningRoutine(Health target, StatusEffectReceiver receiver, EnergyStoneSO stone, WeaponStatBlock stats)
    {
        // 1. 延迟 0.1 秒 (让伤害跳字错开)
        yield return new WaitForSeconds(0.1f);

        if (target == null || target.IsDead) yield break;

        // 2. 判定触发概率
        float chance = (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain))
                       ? stone.lightningChance
                       : stats.nativeLightningChance;

        // 3. 判定雷击 (感电必爆 OR 随机)
        // 注意：这里的 IsElectrified 检查的是 0.1秒前的状态，或者是其他武器挂上的
        bool shouldTriggerSmite = receiver.IsElectrified || (Random.value <= chance);

        if (shouldTriggerSmite)
        {
            TriggerLightningEffect(target, stone, stats, damage);
        }

        // 4. 【核心修复】施加感电 (仅限进化武器！)
        // 规则：单纯的雷石(Stone)不给感电，只有进化后的(Native)才给
        if (stats.nativeElectrify)
        {
            // 施加感电，为下一次攻击做必爆铺垫
            receiver.ApplyElectrified(3.0f);
        }
    }
    private void TriggerLightningEffect(Health target, EnergyStoneSO stone, WeaponStatBlock stats, int baseDmg)
    {
        // 1. 计算雷击伤害
        int smiteDmg = 0;
        GameObject smiteVfx = null;

        if (stone != null && stone.stoneEffects.Contains(EnergyStoneEffectType.ApplyChain))
        {
            smiteDmg = Mathf.RoundToInt(stone.smiteDamage * (PlayerStats.Instance.damageMultiplier + stone.damageModifier));
            smiteVfx = stone.smiteVfxPrefab;
        }
        else
        {
            smiteDmg = Mathf.RoundToInt(baseDmg * 0.5f);
            smiteVfx = stats.nativeSmiteVfxPrefab;
        }

        // 2. 造成伤害
        target.TakeDamage(smiteDmg, target.transform.position, launcher.gameObject, AttackType.Standard);
        if (smiteVfx != null) Instantiate(smiteVfx, target.transform.position, Quaternion.identity);

        // 3. 计数器 & 连锁
        int chainStoneCount = PlayerStats.Instance.GetStoneCount(EnergyStoneEffectType.ApplyChain);
        // 如果是雷进化武器，无条件计数
        if (isLightningEvolution || chainStoneCount >= 1)
        {
            PlayerStats.Instance.lightningSmiteCounter++;
            // 环绕物攻速极快，建议阈值调高一点，比如 5 或 8，否则满屏闪电太卡了
            int threshold = 5;

            if (PlayerStats.Instance.lightningSmiteCounter >= threshold)
            {
                PlayerStats.Instance.lightningSmiteCounter = 0;

                int cCount = (stone != null) ? stone.chainTargets : stats.baseChainCount;
                float cRange = (stone != null) ? stone.chainRange : stats.chainRange;
                GameObject cVfx = (stone != null) ? stone.chainVfxPrefab : stats.nativeChainVfxPrefab;
                GameObject cImp = (stone != null) ? stone.chainImpactVfxPrefab : stats.nativeChainImpactVfxPrefab;

                // 我们没有直接引用 launcher.ChainLightningFromTarget 因为它里面可能有旧逻辑
                // 最好是把那个协程公开，或者在这里直接用 launcher 调用
                launcher.ChainLightningFromTarget(target.transform, cCount, smiteDmg, cRange);
            }
        }
    }
}