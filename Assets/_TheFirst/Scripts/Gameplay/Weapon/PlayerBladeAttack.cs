// --- PlayerBladeAttack.cs (最终诊断与健壮性修正版) ---
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;

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
    public List<SlashPattern> slashesLevel5; // 【新增】5级模式列表

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

        // 重新计算当前帧的冷却速度
        if (attackData != null && attackData.baseFireRate > 0)
        {
            // Duration = BaseDuration * Multiplier (系数越小，冷却越快)
            cooldownDuration = (1f / attackData.baseFireRate) * fireRateMult;
        }

        cooldownTimer -= Time.deltaTime;

        if (cooldownTimer <= 0 && attackData != null && attackData.baseFireRate > 0)
        {
            StartCoroutine(AttackSequence());
            // 重置计时器
            cooldownTimer = cooldownDuration;
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;
        weaponCooldownMaterial?.StartCooldown(cooldownDuration);

        if (floatingWeapon != null) floatingWeapon.HideWeapon();

        Transform effectTransform = floatingWeapon != null ? floatingWeapon.transform :
                                   (slashSpawnPoint != null ? slashSpawnPoint : transform);

        if (flashEffectPrefab != null)
            Instantiate(flashEffectPrefab, effectTransform.position, effectTransform.rotation);

        if (soundEffectDelay < 0)
        {
            PlaySlashSound();
            yield return new WaitForSeconds(Mathf.Abs(soundEffectDelay));
        }

        GenerateSlashVFX();

        if (soundEffectDelay >= 0)
        {
            yield return new WaitForSeconds(soundEffectDelay);
            PlaySlashSound();
        }

        yield return new WaitForSeconds(0.5f);
        if (floatingWeapon != null) floatingWeapon.ShowWeapon();
        isAttacking = false;
    }
    private void GenerateSlashVFX()
    {
        if (attackData == null) return;
        attackCounter++;

        bool hasImprovedFrequency = PlayerProgressManager.Instance != null && PlayerProgressManager.Instance.IsNodeUnlocked(improveFrequencyNode);
        bool hasUnlockedProjectile = PlayerProgressManager.Instance != null && PlayerProgressManager.Instance.IsNodeUnlocked(unlockProjectileNode);

        if (attackData.slashEffectPrefab != null)
        {
            // --- 【核心修复 3】 ---

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
                // 读取我们刚刚加的变量
                localBonus = myWeaponPart.localSlashCountBonus;

                // 如果有能量石加成，也在这里读
                if (myWeaponPart.currentStone != null)
                {
                    // localBonus += myWeaponPart.currentStone.slashCountModifier; 
                }
            }

            // 4. 计算总数
            int totalSlashCount = baseCount + globalBonus + localBonus;

            // --- 调试日志 ---
            if (localBonus > 0)
            {
                Debug.Log($"[BladeAttack] 发起攻击: 基础{baseCount} + 全局{globalBonus} + 局部{localBonus} = 总计 {totalSlashCount} 道刀光");
            }
            // ----------------

            // 5. 生成刀光
            List<SlashPattern> currentPattern = GetCurrentSlashPattern(totalSlashCount);

            foreach (var slash in currentPattern)
            {
                SpawnSlashVFX(slash.positionOffset, slash.angleOffset);
                if (hasImprovedFrequency) FireBladeEnergyProjectile(slash.angleOffset);
            }
        }

        if (hasUnlockedProjectile && !hasImprovedFrequency && attackCounter % 3 == 0)
        {
            FireBladeEnergyProjectile(0);
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
            Debug.Log($"[BladeAttack调试] 基础时间:{baseLife} * (全局:{globalMult} + 局部:{localBonus}) = 最终:{finalLifetime} | WeaponPart存在? {myWeaponPart != null}");
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
        Transform spawnPoint = slashSpawnPoint != null ? slashSpawnPoint : transform;
        Quaternion baseRotation = visualsTransform.rotation;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(0, angleOffset, 0);
        Vector3 worldPositionOffset = visualsTransform.TransformDirection(localPositionOffset);
        Vector3 finalPosition = spawnPoint.position + worldPositionOffset;

        GameObject slashVFX = Instantiate(attackData.slashEffectPrefab, finalPosition, finalRotation);

        // --- 应用范围/大小加成 (Scale) ---
        float scaleMultiplier = 1f;
        if (PlayerStats.Instance != null)
        {
            scaleMultiplier = PlayerStats.Instance.aoeRadiusMultiplier;
        }

        // 【新增】应用局部范围加成
        if (myWeaponPart != null)
        {
            scaleMultiplier += myWeaponPart.localAreaBonus;

            // 叠加能量石
            if (myWeaponPart.currentStone != null)
            {
                scaleMultiplier += myWeaponPart.currentStone.scaleModifier;
            }
        }
        slashVFX.transform.localScale *= scaleMultiplier;

        // --- 应用伤害加成 ---
        VFXDamageController damageController = slashVFX.GetComponent<VFXDamageController>();
        if (damageController != null)
        {
            int baseDamage = attackData.baseAoeDamage;
            int permanentBonus = (PlayerProgressManager.Instance != null) ? PlayerProgressManager.Instance.permanentMeleeAoeFlatDamageBonus : 0;

            float damageMult = 1f;
            float localDmgBonus = 0f; // 【新增】
            float stoneDmgMod = 0f;

            if (PlayerStats.Instance != null)
            {
                damageMult = PlayerStats.Instance.damageMultiplier;
            }

            // 【新增】读取局部变量
            if (myWeaponPart != null)
            {
                localDmgBonus = myWeaponPart.localDamageBonus;
                if (myWeaponPart.currentStone != null)
                {
                    stoneDmgMod = myWeaponPart.currentStone.damageModifier;
                }
            }

            // 基础计算：(基础 + 永久) * (玩家加成 + 局部加成 + 石头加成)
            float calculatedDamage = (baseDamage + permanentBonus) * (damageMult + localDmgBonus + stoneDmgMod);
            int damageInput = Mathf.RoundToInt(calculatedDamage);

            // 初始化控制器
            damageController.Initialize(
                damageInput,
                attackData.hitEffectPrefab,
                this.gameObject,
                myWeaponPart
            );
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
            Debug.Log("Gizmos 预览: Level 1");
            DrawGizmosForPattern(slashesLevel1);
            Debug.Log("Gizmos 预览: Level 2");
            DrawGizmosForPattern(slashesLevel2);
            Debug.Log("Gizmos 预览: Level 3");
            DrawGizmosForPattern(slashesLevel3);
            Debug.Log("Gizmos 预览: Level 4");
            DrawGizmosForPattern(slashesLevel4);
            Debug.Log("Gizmos 预览: Level 6");
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
}