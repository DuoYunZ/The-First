// --- PlayTimelineAndForgetAction.cs ---
using UnityEngine;
using UnityEngine.Playables;

public class PlayTimelineAndForgetAction : Node
{
    [Header("Timeline设置")]
    [Tooltip("将场景中挂载了PlayableDirector的那个GameObject拖到这里")]
    public PlayableDirector playableDirector;

    public override NodeState Evaluate()
    {
        if (playableDirector == null)
        {
            Debug.LogError("PlayTimelineAndForgetAction: 未指定PlayableDirector！", this.gameObject);
            return NodeState.FAILURE;
        }

        // 立即播放，然后立即成功
        playableDirector.Play();
        return NodeState.SUCCESS;
    }
}