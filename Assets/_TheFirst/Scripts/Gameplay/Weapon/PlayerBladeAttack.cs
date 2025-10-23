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

    void Start()
    {
        if (attackData != null && attackData.baseFireRate > 0)
        {
            cooldownDuration = 1f / attackData.baseFireRate;
            cooldownTimer = cooldownDuration;
        }       
    }

    void Update()
    {
        if (isAttacking) return;
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0 && attackData != null && attackData.baseFireRate > 0)
        {
            StartCoroutine(AttackSequence());
            cooldownTimer = cooldownDuration;
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;
        weaponCooldownMaterial?.StartCooldown(cooldownDuration);

        if (floatingWeapon != null) floatingWeapon.HideWeapon();
        if (flashEffectPrefab != null) Instantiate(flashEffectPrefab, floatingWeapon.transform.position, floatingWeapon.transform.rotation);

        // --- 音效时序逻辑 ---
        // 1. 如果设置为提前播放音效
        if (soundEffectDelay < 0)
        {
            PlaySlashSound(); // 先播放声音
            yield return new WaitForSeconds(Mathf.Abs(soundEffectDelay)); // 再等待
        }

        // 2. 生成刀光特效 (这部分逻辑不变)
        GenerateSlashVFX();

        // 3. 如果设置为延迟或同时播放音效
        if (soundEffectDelay >= 0)
        {
            yield return new WaitForSeconds(soundEffectDelay); // 先等待
            PlaySlashSound(); // 再播放声音
        }
        // --- 音效时序逻辑结束 ---

        yield return new WaitForSeconds(0.5f); // 攻击动作的整体持续时间
        if (floatingWeapon != null) floatingWeapon.ShowWeapon();
        isAttacking = false;
    }
    private void GenerateSlashVFX()
    {
        if (attackData == null) return;

        attackCounter++;

        bool hasImprovedFrequency = false;
        bool hasUnlockedProjectile = false;

        if (PlayerProgressManager.Instance != null)
        {
            hasImprovedFrequency = PlayerProgressManager.Instance.IsNodeUnlocked(improveFrequencyNode);
            hasUnlockedProjectile = PlayerProgressManager.Instance.IsNodeUnlocked(unlockProjectileNode);
        }

        if (attackData.slashEffectPrefab != null)
        {
            int slashCount = 1 + PlayerStats.Instance.bonusSlashCount;
            List<SlashPattern> currentPattern = GetCurrentSlashPattern(slashCount);

            if (currentPattern.Count > 0)
            {
                foreach (var slash in currentPattern)
                {
                    SpawnSlashVFX(slash.positionOffset, slash.angleOffset);

                    // --- 4. 【核心新增】在生成每一道刀光时，判断是否要发射刃气 ---
                    if (hasImprovedFrequency)
                    {
                        // 如果解锁了最高级技能，每一道刀光都发射刃气
                        FireBladeEnergyProjectile(slash.angleOffset);
                    }                    
                }
            }
        }
        // --- 5. 处理非最高级的刃气技能 ---
        // (放在循环外，确保无论有多少道刀光，一次攻击只判定一次)
        if (hasUnlockedProjectile && !hasImprovedFrequency)
        {
            if (attackCounter % 3 == 0)
            {
                Debug.Log($"攻击次数达到 {attackCounter}，触发“刃气斩”！");
                // 从基础方向发射一道刃气
                FireBladeEnergyProjectile(0);
            }
        }
    }

    private void FireBladeEnergyProjectile(float angleOffset)
    {
        if (attackData.bladeEnergyPrefab == null)
        {
            Debug.LogWarning("想要发射刃气，但 WeaponStatBlock 中的 BladeEnergyPrefab 未设置！");
            return;
        }

        Transform spawnPoint = bladeEnergySpawnPoint != null ? bladeEnergySpawnPoint : this.transform;

        Quaternion baseRotation = visualsTransform.rotation;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(0, angleOffset, 0);

        // 【修改】使用新的预制件
        GameObject projectileGO = Instantiate(attackData.bladeEnergyPrefab, spawnPoint.position, finalRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            // --- 【核心修改】从 WeaponStatBlock 读取属性并初始化子弹 ---

            // 1. 获取基础属性
            int finalDamage = attackData.bladeEnergyDamage;
            float finalSpeed = attackData.bladeEnergySpeed;
            int finalPierce = attackData.bladeEnergyPierceCount;

            // 2. （可选）应用玩家全局属性加成
            finalDamage = Mathf.RoundToInt(finalDamage * PlayerStats.Instance.damageMultiplier);

            // 3. （可选）应用刃气距离增长的技能树效果
            // bool hasRangeUpgrade = DataManager.Instance.IsUpgradeUnlocked("Blade_ImproveRange");
            // if(hasRangeUpgrade) finalSpeed *= 1.3f; // 假设距离增长30%是通过提速实现

            // 4. 调用 Projectile.cs 的初始化方法
            projectileScript.InitializeAsStraight(
                finalRotation * Vector3.forward, // 发射方向
                finalSpeed,                      // 飞行速度
                finalDamage,                     // 伤害
                false,                           // isEnemyBullet = false
                finalPierce,                     // 穿透次数
                5f,                              // 子弹生存时间
                attackData.shieldImpactEffectPrefab, // 命中护盾特效
                attackData.defaultImpactEffectPrefab, // 默认命中特效
                0, 0, 0, 0, 0 // 其他效果（燃烧、减速等），暂时设为0
            );

            // 【重要】设置刃气弹的物理层
            projectileGO.layer = LayerMask.NameToLayer("PlayerProjectiles");

            Debug.Log($"发射了一道刃气！伤害: {finalDamage}, 速度: {finalSpeed}");
        }
    }
    private void PlaySlashSound()
    {
        if (slashSounds != null && slashSounds.Length > 0 && attackAudioSource != null)
        {
            AudioClip clipToPlay = slashSounds[Random.Range(0, slashSounds.Length)];
            attackAudioSource.PlayOneShot(clipToPlay);
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
        VFXDamageController damageController = slashVFX.GetComponent<VFXDamageController>();
        if (damageController != null)
        {
            int baseDamage = attackData.baseAoeDamage;

            // 2. 从 PlayerProgressManager 获取已解锁的永久固定伤害加成
            //    (添加安全检查，以防 PlayerProgressManager 不存在)
            int permanentBonus = 0;
            if (PlayerProgressManager.Instance != null)
            {
                permanentBonus = PlayerProgressManager.Instance.permanentMeleeAoeFlatDamageBonus;
            }

            // 3. 计算应用了永久加成后的伤害值
            int totalDamage = baseDamage + permanentBonus;

            // 4. (重要!) 再应用 PlayerStats 中的当局伤害乘数
            //    (同样添加安全检查)
            if (PlayerStats.Instance != null)
            {
                totalDamage = Mathf.RoundToInt(totalDamage * PlayerStats.Instance.aoeDamageMultiplier);
            }

            // 5. 调用 VFXDamageController 的【新版】Initialize 方法
            damageController.Initialize(
                totalDamage,                 // 第一个参数：最终计算出的伤害值
                attackData.hitEffectPrefab,  // 第二个参数：命中特效
                this.gameObject              // 第三个参数：攻击者
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