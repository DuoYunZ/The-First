// --- BossAnimationBridge.cs (扩展版) ---
using UnityEngine;

public class BossAnimationBridge : MonoBehaviour
{
    [Header("行为树引用")]
    [Tooltip("将你的行为树根节点（例如Boss_AI那个对象）拖到这里")]
    public GameObject behaviorTreeRoot;

    // --- SpiralFireAction Events ---
    public void AnimEvent_TriggerSpiralWindupEffect()
    {
        if (behaviorTreeRoot == null) return;
        SpiralFireAction action = behaviorTreeRoot.GetComponentInChildren<SpiralFireAction>();
        if (action != null)
        {
            action.TriggerWindupEffect();
        }
    }
    public void AnimEvent_StartSpiralLoopEffect()
    {
        SpiralFireAction action = behaviorTreeRoot.GetComponentInChildren<SpiralFireAction>();
        if (action != null)
        {
            action.StartFiringLoopEffect();
        }
    }
    // ... 可以为SpiralFireAction添加更多事件...

    // --- 【新增】CircularFireAction Events ---
    public void AnimEvent_TriggerCircularWindupEffect()
    {
        if (behaviorTreeRoot == null) return;
        CircularFireAction action = behaviorTreeRoot.GetComponentInChildren<CircularFireAction>();
        if (action != null)
        {
            action.TriggerWindupEffect();
        }
    }

    public void AnimEvent_TriggerCircularRecoveryEffect()
    {
        if (behaviorTreeRoot == null) return;
        CircularFireAction action = behaviorTreeRoot.GetComponentInChildren<CircularFireAction>();
        if (action != null)
        {
            action.TriggerRecoveryEffect();
        }
    }

    // --- 【新增】DashAttackAction Events ---
    public void AnimEvent_TriggerDashWindupEffect()
    {
        if (behaviorTreeRoot == null) return;
        DashAttackAction action = behaviorTreeRoot.GetComponentInChildren<DashAttackAction>();
        if (action != null)
        {
            action.TriggerWindupEffect();
        }
    }

    public void AnimEvent_TriggerDashRecoveryEffect()
    {
        if (behaviorTreeRoot == null) return;
        DashAttackAction action = behaviorTreeRoot.GetComponentInChildren<DashAttackAction>();
        if (action != null)
        {
            action.TriggerRecoveryEffect();
        }
    }
    public void AnimEvent_TriggerBeamWindupEffect()
    {
        if (behaviorTreeRoot == null) return;
        BeamAttackAction action = behaviorTreeRoot.GetComponentInChildren<BeamAttackAction>();
        if (action != null)
        {
            action.TriggerWindupEffect();
        }
    }
}