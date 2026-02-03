// --- PlayAnimationAction.cs ---
using UnityEngine;

public class PlayAnimationAction : Node
{
    [Header("动画设置")]
    [Tooltip("在Animator中设置的动画触发器（Trigger）名称")]
    public string animationTriggerName;
    [Tooltip("动画播放的时长，节点将等待这么久")]
    public float duration = 2f;

    private Animator animator;
    private float timer;
    private bool isPlaying = false;

    void Awake()
    {
        animator = GetComponentInParent<Animator>();
    }

    public override NodeState Evaluate()
    {
        if (animator == null || string.IsNullOrEmpty(animationTriggerName))
        {
            return NodeState.FAILURE;
        }

        if (!isPlaying)
        {
            isPlaying = true;
            timer = 0f;
            animator.SetTrigger(animationTriggerName);
            return NodeState.RUNNING;
        }
        else
        {
            timer += Time.deltaTime;
            if (timer >= duration)
            {
                isPlaying = false;
                return NodeState.SUCCESS;
            }
            else
            {
                return NodeState.RUNNING;
            }
        }
    }
}