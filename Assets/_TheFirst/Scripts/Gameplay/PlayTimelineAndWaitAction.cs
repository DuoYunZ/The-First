// --- PlayTimelineAction.cs (最终版，支持信号) ---
using UnityEngine;
using UnityEngine.Playables;

public class PlayTimelineAction : Node
{
    [Header("Timeline设置")]
    public PlayableDirector playableDirector;

    private bool hasStarted = false;
    private bool isSignalReceived = false; // 用信号来标记结束，而不是等Timeline自己播完

    void Awake()
    {
        // 确保每次游戏开始时状态都是重置的
        isSignalReceived = false;
        hasStarted = false;
    }

    public override NodeState Evaluate()
    {
        if (playableDirector == null) return NodeState.FAILURE;

        if (isSignalReceived)
        {
            isSignalReceived = false;
            hasStarted = false;
            return NodeState.SUCCESS;
        }

        if (!hasStarted)
        {
            hasStarted = true;
            // 我们不再需要订阅stopped事件，因为信号会告诉我们何时结束
            // playableDirector.stopped += OnTimelineFinished; 
            playableDirector.Play();
        }

        return NodeState.RUNNING;
    }

    // 【核心新增】一个公开的方法，用于被信号接收器(Signal Receiver)调用
    public void OnAnimationEndSignal()
    {
        this.isSignalReceived = true;
    }
}