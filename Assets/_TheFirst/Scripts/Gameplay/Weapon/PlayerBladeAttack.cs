// --- PlayerBladeAttack.cs (最终诊断与健壮性修正版) ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [Header("系统引用")]
    public Transform visualsTransform;
    public Transform slashSpawnPoint;
    public FloatingWeaponController floatingWeapon;
    public WeaponCooldownMaterial weaponCooldownMaterial;

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
        yield return new WaitForSeconds(0.1f);

        if (attackData != null && attackData.slashEffectPrefab != null)
        {
            int slashCount = 1 + PlayerStats.Instance.bonusSlashCount;

            // 为了健壮性，我们创建一个新的List来引用正确的模式
            List<SlashPattern> currentPattern = new List<SlashPattern>();
            string patternName = "Level 1";

            if (slashCount == 2)
            {
                currentPattern = slashesLevel2;
                patternName = "Level 2";
            }
            else if (slashCount == 3)
            {
                currentPattern = slashesLevel3;
                patternName = "Level 3";
            }
            else if (slashCount == 4)
            {
                currentPattern = slashesLevel4;
                patternName = "Level 4";
            }
            else if (slashCount >= 5)
            { // 如果等于或超过5，都使用5级的模式
                currentPattern = slashesLevel5;
                patternName = "Level 5+ (顶级)";
            }
            else
            {
                currentPattern = slashesLevel1;
                patternName = "Level 1";
            }

            // --- 【决定性的诊断日志】 ---
            // 这行日志会告诉我们，代码认为当前选中的列表里到底有多少个元素。
            Debug.Log($"<color=cyan>[诊断] 攻击开始! 刀光等级: {slashCount}. 使用模式: '{patternName}'. 该模式配置的刀光数量为: {currentPattern.Count}</color>");

            if (currentPattern.Count == 0)
            {
                Debug.LogWarning($"模式 '{patternName}' 的列表为空！请在PlayerBladeAttack组件中配置它。", this);
            }
            else
            {
                // 使用 foreach 循环确保每一个配置都被执行
                foreach (var slash in currentPattern)
                {
                    SpawnSlashVFX(slash.positionOffset, slash.angleOffset);
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        if (floatingWeapon != null) floatingWeapon.ShowWeapon();
        isAttacking = false;
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
            damageController.Initialize(attackData, this.gameObject);
        }
        Destroy(slashVFX, 2f);
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