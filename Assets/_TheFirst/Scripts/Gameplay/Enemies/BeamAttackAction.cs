// --- BeamAttackAction.cs ---
using UnityEngine;

public class BeamAttackAction : Node
{
    [Header("武器设置")]
    public WeaponStatBlock weaponToFire;
    [Tooltip("指定光束的发射挂点")]
    public Transform beamFirePoint;

    [Header("动作表现")]
    public string windupAnimationTrigger;
    public float windupDuration = 1.5f;
    public string firingAnimationBool = "isFiringBeam";
    public string recoveryAnimationTrigger;
    public float recoveryDuration = 1f;

    [Header("【新增】配合特效的预制件")]
    public GameObject windupEffectPrefab;
    [Tooltip("【新增】前摇特效的生成位置挂点")] // 【新增】
    public Transform windupEffectSpawnPoint;    // 【新增】

    [Header("冷却设置")]
    public string attackName = "BeamAttack";

    private enum ActionState { Ready, WindingUp, Firing, Recovering, Completed }
    private ActionState currentState;
    private float timer;
    private Animator animator;
    private EnemyAI regularAI;
    private Transform selfTransform;
    private GameObject activeBeamInstance;


    void Awake()
    {
        // ... (和我们其他Action脚本一样的Awake内容)
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null) selfTransform = bossRb.transform;
        animator = GetComponentInParent<Animator>();
        regularAI = GetComponentInParent<EnemyAI>();
    }
    private void ActivateBeam()
    {
        if (weaponToFire.beamVfxPrefab == null) return;

        Transform spawnPoint = (beamFirePoint != null) ? beamFirePoint : selfTransform;
        activeBeamInstance = Instantiate(weaponToFire.beamVfxPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);

        BossBeamController beamController = activeBeamInstance.GetComponent<BossBeamController>();
        if (beamController != null)
        {
            // 【问题二修正】将玩家的 AimTargetPoint 作为目标传入
            beamController.Initialize(weaponToFire, selfTransform.gameObject, GameManager.Instance.playerAimTarget);
        }
    }
    public override NodeState Evaluate()
    {
        if (weaponToFire == null || weaponToFire.behavior != WeaponBehaviorType.Beam) return NodeState.FAILURE;

        if (currentState == ActionState.Completed) currentState = ActionState.Ready;

        if (currentState == ActionState.Ready)
        {
            if (regularAI != null) regularAI.enabled = false;
            currentState = ActionState.WindingUp;
            timer = 0f;
            if (animator != null && !string.IsNullOrEmpty(windupAnimationTrigger)) animator.SetTrigger(windupAnimationTrigger);
            GetComponentInParent<BehaviorTree>().StartCooldown(attackName, weaponToFire.beamCooldown);
            return NodeState.RUNNING;
        }

        switch (currentState)
        {
            case ActionState.WindingUp:
                timer += Time.deltaTime;
                if (timer >= windupDuration)
                {
                    currentState = ActionState.Firing;
                    timer = 0f;
                    if (animator != null && !string.IsNullOrEmpty(firingAnimationBool)) animator.SetBool(firingAnimationBool, true);
                    ActivateBeam();
                }
                break;

            case ActionState.Firing:
                if (GameManager.Instance?.playerTransform != null)
                {
                    Vector3 direction = (GameManager.Instance.playerTransform.position - selfTransform.position).normalized;
                    direction.y = 0;
                    selfTransform.rotation = Quaternion.Slerp(selfTransform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
                }

                timer += Time.deltaTime;
                if (timer >= weaponToFire.beamDuration)
                {
                    currentState = ActionState.Recovering;
                    timer = 0f;
                    if (animator != null && !string.IsNullOrEmpty(firingAnimationBool)) animator.SetBool(firingAnimationBool, false);
                    if (animator != null && !string.IsNullOrEmpty(recoveryAnimationTrigger)) animator.SetTrigger(recoveryAnimationTrigger);

                    if (activeBeamInstance != null) Destroy(activeBeamInstance);
                }
                break;

            case ActionState.Recovering:
                timer += Time.deltaTime;
                if (timer >= recoveryDuration)
                {
                    currentState = ActionState.Completed;
                    return NodeState.SUCCESS;
                }
                break;
        }

        return NodeState.RUNNING;
    }
    public void TriggerWindupEffect()
    {
        if (windupEffectPrefab != null)
        {
            // 【核心修改】使用专用的挂点，如果没指定，再用光束发射点作为备用
            Transform spawnPoint = (windupEffectSpawnPoint != null) ? windupEffectSpawnPoint :
                                   (beamFirePoint != null) ? beamFirePoint : selfTransform;

            Instantiate(windupEffectPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }
    }
}