// --- PlayerBladeAttack.cs (最终诊断与健壮性修正版) ---
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 斩击攻击模式 — 融合大招切换
/// </summary>
public enum BladeMode
{
    Normal,     // 默认斩击
    WindBlade,  // 风刃模式（斩击+飓风）：每次攻击额外发射风刃子弹
    Thunder,    // 雷霆模式（斩击+闪电链）：三连刺快攻
    Fire        // 烈焰模式（斩击+火球）：范围增大+附带点燃
}

public class PlayerBladeAttack : MonoBehaviour
{
    [System.Serializable]
    public struct SlashPattern
    {
        public Vector3 positionOffset;
        public float angleOffset;
    }

    [Header("武器数据")]
    public WeaponStatBlock attackData;

    [Header("刃气弹 (技能树解锁)")]
    [Tooltip("刃气弹的预制件")]
    public GameObject bladeEnergyProjectilePrefab;
    [Tooltip("刃气弹的发射点")]
    public Transform bladeEnergySpawnPoint;

    [Header("技能树节点引用")]
    [Tooltip("将'刃气斩'（解锁刃气）的技能节点资产拖到这里")]
    public WeaponUpgradeNode unlockProjectileNode;
    [Tooltip("将'万刃归宗'（强化频率）的技能节点资产拖到这里")]
    public WeaponUpgradeNode improveFrequencyNode;

    private int attackCounter = 0;


    [Header("系统引用")]
    public Transform visualsTransform;
    public Transform slashSpawnPoint;
    public FloatingWeaponController floatingWeapon;
    public WeaponCooldownMaterial weaponCooldownMaterial;

    [Header("机制分支设置")]
    [Tooltip("精准斩击停顿时长（秒）")]
    public float precisionPauseDuration = 0.4f;
    [Tooltip("敏捷猎手身后刀伤害系数（0.7 = 70%主刀伤害）")]
    public float agileBackSlashDamageRatio = 0.7f;
    [Tooltip("身后刀位置偏移（相对角色本地坐标，Z负值=身后）")]
    public Vector3 backSlashOffset = new Vector3(0, 0, -4f);
    [Tooltip("侧方刀位置偏移（X正值=右侧，会根据左右自动取反）")]
    public float sideSlashOffset = 2.5f;
    [Tooltip("精准斩击命中后的移速加成持续时间（瞬身斩回）")]
    public float flashStepDuration = 0.8f;
    [Tooltip("精准斩击命中后的移速加成倍率")]
    public float flashStepSpeedBoost = 0.6f;

    [Header("高级天赋设置")]
    [Tooltip("疾风之势 — 每次攻击叠加攻速百分比")]
    public float windForceAtkSpeedPerStack = 0.05f;
    [Tooltip("疾风之势 — 最大叠加层数")]
    public int windForceMaxStacks = 10;
    [Tooltip("疾风之势 — 不攻击后多久重置叠加（秒）")]
    public float windForceDecayTime = 2f;
    [Tooltip("剑圣之道 — 停顿期间受伤减免百分比")]
    public float kenseiDamageReduction = 0.5f;
    [Tooltip("疾影无踪 — 击杀时生成影分身的概率")]
    public float shadowCloneChance = 0.15f;
    [Tooltip("疾影无踪 — 影分身持续时间（秒）")]
    public float shadowCloneDuration = 3f;
    [Tooltip("疾影无踪 — 影分身预制件（可选，为空则使用角色模型）")]
    public GameObject shadowClonePrefab;

    // 运行时天赋状态
    private int windForceStacks = 0;        // 疾风之势当前层数
    private float windForceTimer = 0f;       // 疾风之势衰减计时
    private bool isKenseiPausing = false;    // 剑圣之道 — 是否在停顿减伤状态

    [Header("音效设置")]
    [Tooltip("攻击时播放的挥舞音效，可以放多个")]
    public AudioSource attackAudioSource;
    public AudioClip[] slashSounds;

    [Header("音效时序设置")]
    [Tooltip("挥刀音效相对于视觉特效的延迟（负数为提前）")]
    public float soundEffectDelay = -0.1f; // 设置为负数，表示音效提前


    [Header("特效")]
    public GameObject flashEffectPrefab;

    [Header("刀光模式配置 (在此处进行可视化调整)")]
    public List<SlashPattern> slashesLevel1;
    public List<SlashPattern> slashesLevel2;
    public List<SlashPattern> slashesLevel3;
    public List<SlashPattern> slashesLevel4;
    public List<SlashPattern> slashesLevel5; // 5级模式列表

    [Header("融合大招模式")]
    [Tooltip("当前攻击模式（由融合大招切换）")]
    public BladeMode currentMode = BladeMode.Normal;

    [Header("风刃模式配置")]
    [Tooltip("风刃子弹预制件")]
    public GameObject windBladeProjectilePrefab;
    [Tooltip("风刃子弹速度")]
    public float windBladeSpeed = 15f;

    [Header("雷霆模式配置")]
    [Tooltip("雷霆三连刺的单次刀光模式")]
    public List<SlashPattern> thunderThrustPatterns;
    [Tooltip("雷霆特效预制件（叠加在刀光上）")]
    public GameObject thunderVfxOverride;
    [Tooltip("雷霆三连刺每次间隔（秒）")]
    public float thunderThrustInterval = 0.2f;
    [Tooltip("雷霆三连刺次数")]
    public int thunderThrustCount = 3;

    [Header("烈焰模式配置")]
    [Tooltip("烈焰模式刀光范围额外倍率")]
    public float fireScaleMultiplier = 1.5f;
    [Tooltip("烈焰模式刀光特效覆盖（可选，带火焰效果的VFX）")]
    public GameObject fireSlashVfxOverride;

    [Header("背部武器模型（融合切换时替换）")]
    [Tooltip("普通模式的背部武器预制件")]
    public GameObject normalWeaponModel;
    [Tooltip("风刃模式的背部武器预制件")]
    public GameObject windWeaponModel;
    [Tooltip("雷霆模式的背部武器预制件")]
    public GameObject thunderWeaponModel;
    [Tooltip("烈焰模式的背部武器预制件")]
    public GameObject fireWeaponModel;

    private float cooldownTimer;
    private bool isAttacking = false;
    private float cooldownDuration;

    private WeaponPart myWeaponPart;

    void Start()
    {
        if (attackData != null && attackData.baseFireRate > 0)
        {
            cooldownDuration = 1f / attackData.baseFireRate;
            cooldownTimer = cooldownDuration;
        }
        myWeaponPart = GetComponent<WeaponPart>();

        if (floatingWeapon == null)
        {
            floatingWeapon = GetComponentInChildren<FloatingWeaponController>();
        }
    }

    void Update()
    {
        if (isAttacking) return;

        // --- 动态应用冷却缩减 (Fire Rate) ---
        float fireRateMult = 1f;
        if (PlayerStats.Instance != null)
        {
            fireRateMult = PlayerStats.Instance.fireRateMultiplier;
        }

        // 【新增】应用局部攻速/冷却加成
        if (myWeaponPart != null)
        {
            // 假设 localFireRateBonus 是正数 (如 0.1 代表冷却缩减 10%)
            // 所以这里是减法
            fireRateMult -= myWeaponPart.localFireRateBonus;

            // 叠加能量石
            if (myWeaponPart.currentStone != null)
            {
                fireRateMult *= (1f + myWeaponPart.currentStone.fireRateModifier);
            }
        }

        // 限制最高射速 (防止冷却变成 0 或负数)
        if (fireRateMult < 0.1f) fireRateMult = 0.1f;

        // 风刃模式：攻速加倍（冷却减半）
        if (currentMode == BladeMode.WindBlade)
        {
            fireRateMult *= 0.5f;
        }

        // 重新计算当前帧的冷却速度
        if (attackData != null && attackData.baseFireRate > 0)
        {
            // Duration = BaseDuration * Multiplier (系数越小，冷却越快)
            cooldownDuration = (1f / attackData.baseFireRate) * fireRateMult;
        }

        cooldownTimer -= Time.deltaTime;

        // === 疏风之势：攻速叠加衰减计时 ===
        if (windForceStacks > 0)
        {
            windForceTimer -= Time.deltaTime;
            if (windForceTimer <= 0)
            {
                // 超时未攻击，重置所有叠加层
                windForceStacks = 0;
            }
        }

        if (cooldownTimer <= 0 && attackData != null && attackData.baseFireRate > 0)
        {
            StartCoroutine(AttackSequence());
            // 重置计时器（疏风之势加速会减少cooldownDuration）
            cooldownTimer = cooldownDuration * (1f - windForceStacks * windForceAtkSpeedPerStack);
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;

        // 检查局内角色技能卡状态（抽到卡片才生效）
        bool hasPrecision = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("PrecisionSlash");
        bool hasAgile = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("AgileHunter");

        // === 精准斩击：攻击前短暂停顿（蓄力感） ===
        if (hasPrecision)
        {
            bool hasHeavySlash = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Prec_HeavySlash");
            float pauseTime = hasHeavySlash ? 0.3f : precisionPauseDuration;

            // 剑圣之道：停顿期间开启减伤
            bool hasKensei = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Talent_Kensei");
            if (hasKensei)
            {
                isKenseiPausing = true;
                pauseTime += 0.05f;
            }

            // 停顿期间锁定移动（通过MechController标记，持续整个停顿）
            MechController mech = GetComponentInParent<MechController>();
            if (mech != null) mech.isMovementLocked = true;

            yield return new WaitForSeconds(pauseTime);

            // 停顿结束，解锁移动 + 关闭减伤
            if (mech != null) mech.isMovementLocked = false;
            isKenseiPausing = false;
        }

        if (floatingWeapon != null) floatingWeapon.HideWeapon();

        Transform effectTransform = floatingWeapon != null ? floatingWeapon.transform :
                                   (slashSpawnPoint != null ? slashSpawnPoint : transform);

        if (flashEffectPrefab != null)
            Instantiate(flashEffectPrefab, effectTransform.position, effectTransform.rotation);

        // === 精准斩击：覆盖角色朝向为鼠标方向 ===
        if (hasPrecision && visualsTransform != null)
        {
            Vector3 mouseWorldDir = GetMouseWorldDirection();
            if (mouseWorldDir.sqrMagnitude > 0.01f)
            {
                visualsTransform.rotation = Quaternion.LookRotation(mouseWorldDir);
            }
        }

        if (soundEffectDelay < 0)
        {
            PlaySlashSound();
            yield return new WaitForSeconds(Mathf.Abs(soundEffectDelay));
        }

        GenerateSlashVFX();

        // === 疾风之势：每次攻击叠加攻速 ===
        if (hasAgile)
        {
            bool hasWindForce = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Agile_WindForce");
            if (hasWindForce)
            {
                ApplyWindForceStack();
            }
        }

        if (soundEffectDelay >= 0)
        {
            yield return new WaitForSeconds(soundEffectDelay);
            PlaySlashSound();
        }

        yield return new WaitForSeconds(0.5f);
        if (floatingWeapon != null) floatingWeapon.ShowWeapon();
        isAttacking = false;

        // === 精准斩击-瞬身斩回：命中后短暂加速 ===
        if (hasPrecision)
        {
            bool hasFlashStep = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Prec_FlashStep");
            if (hasFlashStep && PlayerStats.Instance != null)
            {
                StartCoroutine(FlashStepBoost());
            }
        }

        // 攻击动画结束后才启动冷却视觉，确保充满 = 可以攻击
        if (weaponCooldownMaterial != null)
        {
            float remainingCooldown = cooldownTimer; // 剩余冷却时间
            if (remainingCooldown > 0.05f)
            {
                try { weaponCooldownMaterial.StartCooldown(remainingCooldown); }
                catch (System.Exception) { weaponCooldownMaterial = null; }
            }
            else
            {
                try { weaponCooldownMaterial.SetChargedEffect(); }
                catch (System.Exception) { weaponCooldownMaterial = null; }
            }
        }
    }

    /// <summary>
    /// 精准斩击-瞬身斩回：攻击后短暂移速加成
    /// </summary>
    private IEnumerator FlashStepBoost()
    {
        if (PlayerStats.Instance == null) yield break;
        // 同时修改temp（用于RecalculateStats恢复）和主字段（立刻生效）
        PlayerStats.Instance.tempMoveSpeedBonus += flashStepSpeedBoost;
        PlayerStats.Instance.moveSpeedMultiplier += flashStepSpeedBoost;
        yield return new WaitForSeconds(flashStepDuration);
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.tempMoveSpeedBonus -= flashStepSpeedBoost;
            PlayerStats.Instance.moveSpeedMultiplier -= flashStepSpeedBoost;
        }
    }

    // =========================================================
    //  高级天赋实现
    // =========================================================

    /// <summary>
    /// 疏风之势：每次攻击叠加一层攻速加成
    /// 最大叠加 windForceMaxStacks 层，超时不攻击则全部重置
    /// </summary>
    private void ApplyWindForceStack()
    {
        windForceStacks = Mathf.Min(windForceStacks + 1, windForceMaxStacks);
        windForceTimer = windForceDecayTime; // 重置衰减计时
    }

    /// <summary>
    /// 剑圣之道：查询当前是否在停顿减伤状态
    /// 外部伤害系统调用此方法获取减伤倍率
    /// 返回值：0~1，0=不减伤，0.5=减50%伤害
    /// </summary>
    public float GetKenseiDamageReduction()
    {
        if (isKenseiPausing)
            return kenseiDamageReduction;
        return 0f;
    }

    /// <summary>
    /// 疑影无踪：击杀敌人时概率生成影分身
    /// 由敌人死亡时调用（外部接入）
    /// </summary>
    public void TrySpawnShadowClone(Vector3 killPosition)
    {
        if (PlayerProgressManager.Instance == null) return;
        if (UpgradeManager.Instance == null || !UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Talent_Shadow")) return;

        // 概率判定
        if (Random.value > shadowCloneChance) return;

        StartCoroutine(ShadowCloneRoutine(killPosition));
    }

    /// <summary>
    /// 影分身协程：生成 → 持续攻击 → 自动消失
    /// </summary>
    private IEnumerator ShadowCloneRoutine(Vector3 spawnPosition)
    {
        // 创建影分身
        GameObject clone = null;
        if (shadowClonePrefab != null)
        {
            clone = Instantiate(shadowClonePrefab, spawnPosition, transform.rotation);
            // 移除预制件上不需要的组件（避免分身尝试控制角色）
            CleanupCloneComponents(clone);
        }
        else if (visualsTransform != null)
        {
            // 没有专用预制件，复制角色模型作为影分身
            clone = Instantiate(visualsTransform.gameObject, spawnPosition, transform.rotation);
            // 移除不需要的组件（避免影分身操控角色）
            CleanupCloneComponents(clone);
        }

        if (clone == null) yield break;

        // 安全保底：无论协程是否异常，都确保分身到时间后被销毁
        Destroy(clone, shadowCloneDuration + 0.5f);

        // 半透明效果（影分身视觉）
        var renderers = clone.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                Color c = mat.color;
                c.a = 0.5f;
                mat.color = c;
            }
        }

        // 影分身持续攻击：根据攻速循环释放斩击，直到时间结束
        float elapsed = 0f;
        float attackInterval = (attackData != null && attackData.baseFireRate > 0)
            ? (1f / attackData.baseFireRate) * 0.5f  // 影分身攻速是玩家的2倍
            : 0.4f;
        attackInterval = Mathf.Max(attackInterval, 0.2f); // 最小间隔0.2s

        Debug.Log($"[影分身] 生成成功！位置={spawnPosition}, 持续={shadowCloneDuration}s, 攻击间隔={attackInterval}s");

        while (elapsed < shadowCloneDuration)
        {
            // 在影分身位置生成斩击（70%伤害）
            if (clone != null)
            {
                float randomAngle = Random.Range(-30f, 30f);
                // 使用分身位置 + 高度修正（确保VFX在角色腰部而不是地面）
                Vector3 vfxPos = clone.transform.position + Vector3.up * 0.8f;
                SpawnSlashVFXAtPosition(vfxPos, clone.transform.rotation, randomAngle, 0.7f);
                Debug.Log($"[影分身] 释放斩击 @ {vfxPos}");
            }

            yield return new WaitForSeconds(attackInterval);
            elapsed += attackInterval;
        }

        Debug.Log("[影分身] 时间到，消失");
        if (clone != null)
            Destroy(clone);
    }

    /// <summary>
    /// 清理分身上不需要的组件（保留 Animator 和 Renderer 用于视觉表现）
    /// </summary>
    private void CleanupCloneComponents(GameObject clone)
    {
        if (clone == null) return;

        // 移除所有不需要的 MonoBehaviour（保留 Animator 用于播放动画）
        var allBehaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var behaviour in allBehaviours)
        {
            if (behaviour != null && !(behaviour is Animator))
            {
                Destroy(behaviour);
            }
        }

        // 移除碰撞体（避免分身与敌人/玩家发生物理交互）
        var colliders = clone.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            Destroy(col);
        }

        // 移除 Rigidbody（避免物理运算）
        var rigidbodies = clone.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rigidbodies)
        {
            Destroy(rb);
        }
    }

    /// <summary>
    /// 在指定位置生成斩击VFX（用于影分身等脱离角色位置的情况）
    /// </summary>
    private void SpawnSlashVFXAtPosition(Vector3 worldPos, Quaternion baseRotation, float angleOffset, float damageRatio)
    {
        if (attackData == null || attackData.slashEffectPrefab == null) return;

        Quaternion finalRotation = baseRotation * Quaternion.Euler(0, angleOffset, 0);
        GameObject slashVFX = Instantiate(attackData.slashEffectPrefab, worldPos, finalRotation);

        VFXDamageController damageController = slashVFX.GetComponent<VFXDamageController>();
        if (damageController != null)
        {
            int baseDamage = attackData.baseAoeDamage;
            float damageMult = PlayerStats.Instance != null ? PlayerStats.Instance.damageMultiplier : 1f;
            int finalDamage = Mathf.RoundToInt(baseDamage * damageMult * damageRatio);
            damageController.Initialize(finalDamage, attackData.hitEffectPrefab, this.gameObject, myWeaponPart);
        }
    }

    /// <summary>
    /// 获取鼠标在世界空间中相对于玩家的方向（Y轴归零）
    /// </summary>
    private Vector3 GetMouseWorldDirection()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.forward;

        // 使用新版 Input System 获取鼠标位置
        if (Mouse.current == null) return transform.forward;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        Ray ray = cam.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 dir = (hitPoint - transform.position);
            dir.y = 0;
            return dir.normalized;
        }
        return transform.forward;
    }
    private void GenerateSlashVFX()
    {
        if (attackData == null) return;
        attackCounter++;

        bool hasImprovedFrequency = PlayerProgressManager.Instance != null && PlayerProgressManager.Instance.IsNodeUnlocked(improveFrequencyNode);
        bool hasUnlockedProjectile = PlayerProgressManager.Instance != null && PlayerProgressManager.Instance.IsNodeUnlocked(unlockProjectileNode);

        // === 计算刀光数量加成（所有模式共用） ===
        int slashBonus = 0;
        if (PlayerStats.Instance != null)
            slashBonus += PlayerStats.Instance.bonusSlashCount;
        if (myWeaponPart != null)
            slashBonus += myWeaponPart.localSlashCountBonus;

        // === 雷霆模式：使用三连刺替代普通刀光（0.2s间隔） ===
        if (currentMode == BladeMode.Thunder)
        {
            StartCoroutine(ThunderThrustSequence(slashBonus));
            return; // 雷霆模式跳过普通刀光
        }

        // === 风刃模式：替换斩击为发射风刃子弹（不再生成刀光） ===
        if (currentMode == BladeMode.WindBlade && windBladeProjectilePrefab != null)
        {
            StartCoroutine(WindBladeSequence(1 + slashBonus));
            return; // 风刃模式跳过普通刀光
        }

        if (attackData.slashEffectPrefab != null)
        {
            // 1. 基础数量
            int baseCount = attackData.multiHitCount;

            // 2. 全局加成 (PlayerStats)
            int globalBonus = 0;
            if (PlayerStats.Instance != null)
            {
                globalBonus = PlayerStats.Instance.bonusSlashCount;
            }

            // 3. 局部加成 (WeaponPart)
            int localBonus = 0;
            if (myWeaponPart != null)
            {
                localBonus = myWeaponPart.localSlashCountBonus;

                if (myWeaponPart.currentStone != null)
                {
                    // localBonus += myWeaponPart.currentStone.slashCountModifier; 
                }
            }

            // 4. 计算总数
            int totalSlashCount = baseCount + globalBonus + localBonus;

            if (localBonus > 0)
            {
            }

            // 5. 生成刀光
            List<SlashPattern> currentPattern = GetCurrentSlashPattern(totalSlashCount);

            foreach (var slash in currentPattern)
            {
                SpawnSlashVFX(slash.positionOffset, slash.angleOffset);
                if (hasImprovedFrequency) FireBladeEnergyProjectile(slash.angleOffset);
            }

            // === 敏捷猎手：在身后额外生成一道斩击（70%伤害）===
            bool isAgileHunter = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("AgileHunter");
            if (isAgileHunter)
            {
                // 身后刀：使用Inspector可配置的偏移量
                SpawnSlashVFX(backSlashOffset, 180f, agileBackSlashDamageRatio);

                // 残影连斩：35%概率额外触发一次侧方斩击
                bool hasShadowSlash = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Agile_ShadowSlash");
                if (hasShadowSlash && Random.value < 0.35f)
                {
                    // 随机左或右，带侧向+后方位置偏移
                    bool isLeft = Random.value < 0.5f;
                    float sideAngle = isLeft ? 90f : 270f;
                    float sideX = isLeft ? -sideSlashOffset : sideSlashOffset;
                    // Z轴使用backSlashOffset.z的一半，让侧方刀也偏向身后
                    SpawnSlashVFX(new Vector3(sideX, 0, backSlashOffset.z * 0.5f), sideAngle, agileBackSlashDamageRatio * 0.8f);
                }
            }
        }

        if (hasUnlockedProjectile && !hasImprovedFrequency && attackCounter % 3 == 0)
        {
            FireBladeEnergyProjectile(0);
        }
    }

    /// <summary>
    /// 雷霆三连刺协程：每0.2秒释放一道刀光，次数受刀光数量升级影响
    /// </summary>
    private System.Collections.IEnumerator ThunderThrustSequence(int slashBonus)
    {
        List<SlashPattern> patterns = thunderThrustPatterns != null && thunderThrustPatterns.Count > 0
            ? thunderThrustPatterns
            : slashesLevel1; // 回退到基础模式

        int totalThrusts = thunderThrustCount + slashBonus; // 刀光数量升级增加连刺次数

        for (int i = 0; i < totalThrusts; i++)
        {
            // 每次使用一个模式（循环使用）
            SlashPattern pattern = patterns[i % patterns.Count];
            SpawnSlashVFX(pattern.positionOffset, pattern.angleOffset);

            // 播放音效
            PlaySlashSound();

            if (i < totalThrusts - 1)
                yield return new WaitForSeconds(thunderThrustInterval);
        }
    }

    /// <summary>
    /// 风刃连射协程：发射多道风刃，间隔0.3秒
    /// </summary>
    private System.Collections.IEnumerator WindBladeSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            FireWindBladeProjectile();
            PlaySlashSound();

            if (i < count - 1)
                yield return new WaitForSeconds(0.3f);
        }
    }

    private void FireBladeEnergyProjectile(float angleOffset)
    {
        if (attackData.bladeEnergyPrefab == null) return;

        Transform spawnPoint = bladeEnergySpawnPoint != null ? bladeEnergySpawnPoint : this.transform;
        Quaternion finalRotation = visualsTransform.rotation * Quaternion.Euler(0, angleOffset, 0);

        GameObject projectileGO = Instantiate(attackData.bladeEnergyPrefab, spawnPoint.position, finalRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            // 1. 计算伤害
            int finalDamage = attackData.bladeEnergyDamage;
            float damageMult = 1f;

            if (PlayerStats.Instance != null) damageMult = PlayerStats.Instance.damageMultiplier;
            if (myWeaponPart != null) damageMult += myWeaponPart.localDamageBonus;

            finalDamage = Mathf.RoundToInt(finalDamage * damageMult);

            // 2. 计算速度
            float finalSpeed = attackData.bladeEnergySpeed;
            // (可选) speedMult 计算...

            int finalPierce = attackData.bladeEnergyPierceCount;

            // =========================================================
            // 【核心调试区域】 - 看看数值到底是多少
            // =========================================================

            // A. 获取基础寿命
            float baseLife = attackData.baseProjectileLifetime > 0 ? attackData.baseProjectileLifetime : 0.25f;

            // B. 获取倍率
            float durationMult = 1f;
            float globalMult = (PlayerStats.Instance != null) ? PlayerStats.Instance.durationMultiplier : 1f;
            float localBonus = 0f;

            if (myWeaponPart != null)
            {
                localBonus = myWeaponPart.localDurationBonus;
                durationMult = globalMult + localBonus; // 逻辑：全局(1.0) + 局部(0.x)
            }
            else
            {
                Debug.LogError($"[BladeAttack调试] 警告！myWeaponPart 是空的！无法读取局部升级！");
                durationMult = globalMult;
            }

            // C. 计算最终寿命
            float finalLifetime = baseLife * durationMult;

            // --- 打印日志 (请在控制台查看这个) ---
            // =========================================================

            projectileScript.InitializeAsStraight(
                 finalRotation * Vector3.forward,
                 finalSpeed,
                 finalDamage,
                 false,
                 finalPierce,
                 finalLifetime, // 传入计算后的时间
                 attackData.shieldImpactEffectPrefab,
                 attackData.defaultImpactEffectPrefab,
                 0, 0, 0, 0, 0,
                 AttackType.Standard,
                 myWeaponPart
             );
            projectileGO.layer = LayerMask.NameToLayer("PlayerProjectile");
        }
    }
    private void PlaySlashSound()
    {
        if (slashSounds != null && slashSounds.Length > 0 && attackAudioSource != null)
        {
            attackAudioSource.PlayOneShot(slashSounds[Random.Range(0, slashSounds.Length)]);
        }
    }
    private List<SlashPattern> GetCurrentSlashPattern(int slashLevel)
    {
        switch (slashLevel)
        {
            case 2: return slashesLevel2;
            case 3: return slashesLevel3;
            case 4: return slashesLevel4;
            case 5:
            default: // 如果等级超过5，或等于1，或出现意外情况，都使用对应的列表
                if (slashLevel >= 5) return slashesLevel5;
                return slashesLevel1;
        }
    }

    // SpawnSlashVFX 和 OnDrawGizmosSelected 方法保持我们上一个版本即可
    void SpawnSlashVFX(Vector3 localPositionOffset, float angleOffset)
    {
        // 默认伤害系数1.0（100%伤害）
        SpawnSlashVFX(localPositionOffset, angleOffset, 1.0f);
    }

    /// <summary>
    /// 生成斩击VFX（支持伤害系数，用于敏捷猎手身后刀等）
    /// </summary>
    void SpawnSlashVFX(Vector3 localPositionOffset, float angleOffset, float damageRatio)
    {
        Transform spawnPoint = slashSpawnPoint != null ? slashSpawnPoint : transform;
        Quaternion baseRotation = visualsTransform.rotation;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(0, angleOffset, 0);
        Vector3 worldPositionOffset = visualsTransform.TransformDirection(localPositionOffset);
        Vector3 finalPosition = spawnPoint.position + worldPositionOffset;

        // 选择VFX预制件：雷霆模式可用覆盖特效，烈焰模式可用覆盖特效
        GameObject vfxPrefab = attackData.slashEffectPrefab;
        if (currentMode == BladeMode.Thunder && thunderVfxOverride != null)
            vfxPrefab = thunderVfxOverride;
        else if (currentMode == BladeMode.Fire && fireSlashVfxOverride != null)
            vfxPrefab = fireSlashVfxOverride;

        GameObject slashVFX = Instantiate(vfxPrefab, finalPosition, finalRotation);

        // --- 应用范围/大小加成 (Scale) ---
        float scaleMultiplier = 1f;
        if (PlayerStats.Instance != null)
        {
            scaleMultiplier = PlayerStats.Instance.aoeRadiusMultiplier;
        }

        // 应用局部范围加成
        if (myWeaponPart != null)
        {
            scaleMultiplier += myWeaponPart.localAreaBonus;

            if (myWeaponPart.currentStone != null)
            {
                scaleMultiplier += myWeaponPart.currentStone.scaleModifier;
            }
        }

        // 烈焰模式：额外放大范围
        if (currentMode == BladeMode.Fire)
        {
            scaleMultiplier *= fireScaleMultiplier;
        }

        // 蓄力重斩：额外放大范围40%
        bool hasHeavySlash = PlayerProgressManager.Instance != null 
            && UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Prec_HeavySlash");
        if (hasHeavySlash && damageRatio >= 1.0f) // 只对主斩击生效，不对身后刀生效
        {
            scaleMultiplier *= 1.4f;
        }

        slashVFX.transform.localScale *= scaleMultiplier;

        // --- 应用伤害加成 ---
        VFXDamageController damageController = slashVFX.GetComponent<VFXDamageController>();
        if (damageController != null)
        {
            int baseDamage = attackData.baseAoeDamage;
            int permanentBonus = (PlayerProgressManager.Instance != null) ? PlayerProgressManager.Instance.permanentMeleeAoeFlatDamageBonus : 0;

            float damageMult = 1f;
            float localDmgBonus = 0f;
            float stoneDmgMod = 0f;

            if (PlayerStats.Instance != null)
            {
                damageMult = PlayerStats.Instance.damageMultiplier;
            }

            if (myWeaponPart != null)
            {
                localDmgBonus = myWeaponPart.localDamageBonus;
                if (myWeaponPart.currentStone != null)
                {
                    stoneDmgMod = myWeaponPart.currentStone.damageModifier;
                }
            }

            float calculatedDamage = (baseDamage + permanentBonus) * (damageMult + localDmgBonus + stoneDmgMod);
            
            // 应用伤害系数（身后刀等减伤场景）
            calculatedDamage *= damageRatio;
            
            // 破甲一击：主斩击额外增伤 + 忽略护甲
            bool hasArmorBreak = PlayerProgressManager.Instance != null 
                && UpgradeManager.Instance != null 
                && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Prec_ArmorBreak");
            if (hasArmorBreak && damageRatio >= 1.0f) // 只对主斩击生效
            {
                calculatedDamage *= 1.3f; // 破甲增伤30%伤害
            }

            int damageInput = Mathf.RoundToInt(calculatedDamage);

            // 剑圣之道：为VFX添加额外暴击率
            float bonusCritRate = 0f;
            bool hasKenseiCrit = PlayerProgressManager.Instance != null 
                && UpgradeManager.Instance != null 
                && UpgradeManager.Instance.HasActiveCharacterSkill("Sword_Talent_Kensei");
            if (hasKenseiCrit)
            {
                bonusCritRate = 0.15f; // 剑圣之道额外+15%暴击率
            }

            damageController.Initialize(
                damageInput,
                attackData.hitEffectPrefab,
                this.gameObject,
                myWeaponPart
            );

            // 设置额外暴击率（剑圣之道）
            if (bonusCritRate > 0f)
            {
                damageController.bonusCritRate = bonusCritRate;
            }

            // 破甲一击：标记忽略护甲
            if (hasArmorBreak && damageRatio >= 1.0f)
            {
                damageController.ignoreArmor = true;
            }

            // 烈焰模式：强制附带点燃效果
            if (currentMode == BladeMode.Fire)
            {
                damageController.forceBurn = true;
                damageController.forceBurnDamage = Mathf.RoundToInt(damageInput * 0.15f); // 15%伤害/跳
                damageController.forceBurnDuration = 3f;
            }
        }
    }

    // OnDrawGizmosSelected 保持不变，它也需要使用新的诊断逻辑
    private void OnDrawGizmosSelected()
    {
        if (visualsTransform == null) return;

        // 在编辑器模式下，我们无法访问PlayerStats，所以提供一个手动预览的方式
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DrawGizmosForPattern(slashesLevel1);
            DrawGizmosForPattern(slashesLevel2);
            DrawGizmosForPattern(slashesLevel3);
            DrawGizmosForPattern(slashesLevel4);
            DrawGizmosForPattern(slashesLevel5);
            return;
        }
#endif

        int slashCount = 1 + (Application.isPlaying && PlayerStats.Instance != null ? PlayerStats.Instance.bonusSlashCount : 3); // 在编辑器中默认预览4级(3次升级)
        List<SlashPattern> currentPattern = slashesLevel1;
        if (slashCount == 2) currentPattern = slashesLevel2;
        else if (slashCount == 3) currentPattern = slashesLevel3;
        else if (slashCount == 4) currentPattern = slashesLevel4;
        else if (slashCount >= 5) currentPattern = slashesLevel5;
        DrawGizmosForPattern(currentPattern);
    }

    void DrawGizmosForPattern(List<SlashPattern> pattern)
    {
        if (pattern == null || pattern.Count == 0) return;
        Gizmos.color = Color.cyan;
        foreach (var slash in pattern)
        {
            Transform spawnPoint = slashSpawnPoint != null ? slashSpawnPoint : transform;
            Quaternion baseRotation = (visualsTransform != null) ? visualsTransform.rotation : transform.rotation;
            Quaternion finalRotation = baseRotation * Quaternion.Euler(0, slash.angleOffset, 0);
            Vector3 worldPositionOffset = baseRotation * slash.positionOffset;
            Vector3 finalPosition = (spawnPoint != null ? spawnPoint.position : transform.position) + worldPositionOffset;
            Gizmos.DrawSphere(finalPosition, 0.2f);
            Gizmos.DrawRay(finalPosition, finalRotation * Vector3.forward * 2f);
        }
    }

    // =============================================
    // 融合大招：模式切换
    // =============================================

    /// <summary>
    /// 设置斩击模式（由 UltimateManager 在释放融合大招时调用）
    /// 互斥替换：新模式会覆盖旧模式
    /// </summary>
    public void SetBladeMode(BladeMode newMode)
    {
        if (currentMode == newMode)
        {
            return;
        }

        BladeMode oldMode = currentMode;
        currentMode = newMode;

        // 切换背部武器模型
        if (floatingWeapon != null)
        {
            GameObject targetModel = null;
            switch (newMode)
            {
                case BladeMode.Normal:   targetModel = normalWeaponModel; break;
                case BladeMode.WindBlade: targetModel = windWeaponModel; break;
                case BladeMode.Thunder:  targetModel = thunderWeaponModel; break;
                case BladeMode.Fire:     targetModel = fireWeaponModel; break;
            }
            if (targetModel != null)
            {
                floatingWeapon.SwapModel(targetModel);
            }
        }

    }

    /// <summary>
    /// 风刃模式：发射风刃子弹
    /// </summary>
    private void FireWindBladeProjectile()
    {
        if (windBladeProjectilePrefab == null) return;

        Transform spawnPoint = bladeEnergySpawnPoint != null ? bladeEnergySpawnPoint : this.transform;
        Vector3 spawnPos = spawnPoint.position;

        // === 自动瞄准：寻找最近敌人 ===
        Vector3 fireDir = visualsTransform.rotation * Vector3.forward; // 默认朝玩家面向
        float aimRange = 20f;
        Collider[] enemies = Physics.OverlapSphere(spawnPos, aimRange, LayerMask.GetMask("Enemies"));
        float closestDist = float.MaxValue;
        Transform closestEnemy = null;

        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            float dist = Vector3.Distance(spawnPos, h.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestEnemy = h.transform;
            }
        }

        if (closestEnemy != null)
        {
            Vector3 targetDir = (closestEnemy.position - spawnPos);
            targetDir.y = 0f; // 保持水平
            if (targetDir.sqrMagnitude > 0.01f)
                fireDir = targetDir.normalized;
        }

        Quaternion fireRot = Quaternion.LookRotation(fireDir);

        GameObject projectileGO = Instantiate(windBladeProjectilePrefab, spawnPos, fireRot);
        Projectile proj = projectileGO.GetComponent<Projectile>();

        if (proj != null)
        {
            // 计算伤害 — 与斩击共享 baseAoeDamage
            int baseDmg = attackData.baseAoeDamage;
            float damageMult = 1f;
            if (PlayerStats.Instance != null) damageMult = PlayerStats.Instance.damageMultiplier;
            if (myWeaponPart != null) damageMult += myWeaponPart.localDamageBonus;
            int windDmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * damageMult));

            float lifeTime = 2f;

            proj.InitializeAsStraight(
                fireDir, windBladeSpeed, windDmg, false,
                999, lifeTime, // 穿透999（无限穿透）
                null, attackData.hitEffectPrefab,
                0, 0, 0, 0, 0,
                AttackType.Standard, myWeaponPart
            );

            // 设置击退力度
            proj.knockbackForce = 1.5f;

            // 应用范围/缩放加成（与斩击刀光一致）
            float scaleMultiplier = 1f;
            if (PlayerStats.Instance != null)
                scaleMultiplier = PlayerStats.Instance.aoeRadiusMultiplier;
            if (myWeaponPart != null)
                scaleMultiplier += myWeaponPart.localAreaBonus;
            if (scaleMultiplier != 1f)
                projectileGO.transform.localScale *= scaleMultiplier;
        }
    }
}