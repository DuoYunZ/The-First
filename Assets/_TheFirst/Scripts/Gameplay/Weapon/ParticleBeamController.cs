// --- ParticleBeamController.cs (带命中特效版) ---
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ParticleBeamController : MonoBehaviour
{
    // 伤害相关
    private GameObject firer;
    private float damagePerSecond;
    private float damageTickRate;
    private float damagePerTick;
    private float damageTimer;

    // 【新增】命中特效相关
    private float maxDistance;
    private GameObject impactEffectPrefab;
    private GameObject impactEffectInstance; // 用于存储生成的特效实例

    [Header("【新】射线检测设置")]
    [Tooltip("设置射线可以击中的层，例如'Player'和'Ground'")]
    public LayerMask hitLayers;

    // 由BeamAttackAction调用，用于初始化
    public void Activate(GameObject firer, WeaponStatBlock beamData)
    {
        this.firer = firer;
        this.damagePerSecond = beamData.beamDamagePerSecond;
        this.damageTickRate = beamData.beamDamageTickRate;
        this.maxDistance = beamData.beamMaxDistance;
        this.impactEffectPrefab = beamData.beamImpactVfxPrefab;

        if (this.damageTickRate > 0)
        {
            this.damagePerTick = Mathf.CeilToInt(this.damagePerSecond / this.damageTickRate);
        }

        if (this.impactEffectPrefab != null)
        {
            impactEffectInstance = Instantiate(this.impactEffectPrefab);
            impactEffectInstance.SetActive(false);
        }
    }

    // 【新增】使用Update来处理视觉定位
    void Update()
    {
        // --- 【新增】可视化调试 ---
        // 在Scene视图中，画出一条绿色的线，代表我们的射线
        // 这条线只在编辑器里可见，不影响最终游戏
        Debug.DrawRay(transform.position, transform.forward * maxDistance, Color.green);

        // --- 原有的射线检测逻辑 ---
        if (impactEffectInstance == null) return;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide))
        {
            // 【新增】如果击中，在Console窗口打印出击中对象的名字
            Debug.Log($"[ParticleBeamController] 射线击中了: {hit.collider.name}");

            impactEffectInstance.SetActive(true);
            impactEffectInstance.transform.position = hit.point;
            impactEffectInstance.transform.rotation = Quaternion.LookRotation(hit.normal);
        }
        else
        {
            impactEffectInstance.SetActive(false);
        }
    }

    // OnTriggerStay 逻辑保持不变，继续负责伤害判定
    void OnTriggerStay(Collider other)
    {
        // ... (原有的伤害逻辑完全不变) ...
        if (damagePerTick <= 0 || !other.CompareTag("Player"))
        {
            return;
        }
        damageTimer += Time.deltaTime;
        if (damageTimer >= 1f / damageTickRate)
        {
            damageTimer = 0f;
            Health health = other.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(Mathf.CeilToInt(damagePerTick), other.transform.position, firer, AttackType.Standard);
            }
        }
    }

    // 【新增】在光束被销毁时，确保命中特效也被一并销毁
    void OnDestroy()
    {
        if (impactEffectInstance != null)
        {
            Destroy(impactEffectInstance);
        }
    }
}