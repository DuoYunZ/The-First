// --- BossDeathCeremony.cs ---
// Boss 死亡表演控制器：击杀 Boss 后播放华丽的死亡动画 + 金币喷泉 + 延迟胜利结算
using UnityEngine;
using System.Collections;

public class BossDeathCeremony : MonoBehaviour
{
    public static BossDeathCeremony Instance { get; private set; }

    [Header("金币喷泉设置")]
    [Tooltip("金币预制件（与普通掉落金币相同即可）")]
    public GameObject coinPrefab;

    [Tooltip("喷出的金币总数")]
    public int coinCount = 60;

    [Tooltip("喷泉持续时间（秒）")]
    public float fountainDuration = 2.5f;

    [Tooltip("金币向上喷射的最小高度")]
    public float minArcHeight = 3f;

    [Tooltip("金币向上喷射的最大高度")]
    public float maxArcHeight = 8f;

    [Tooltip("金币水平散射半径")]
    public float horizontalSpread = 6f;

    [Tooltip("单个金币飞行时间")]
    public float coinFlightDuration = 0.8f;

    [Header("表演时间线")]
    [Tooltip("Boss 倒下后等待多久开始喷金币（秒）")]
    public float delayBeforeFountain = 0.5f;

    [Tooltip("金币喷泉结束后等待多久显示结算（秒）")]
    public float delayAfterFountain = 1.5f;

    [Header("慢动作设置")]
    [Tooltip("死亡瞬间的慢动作倍率")]
    public float slowMotionScale = 0.3f;

    [Tooltip("慢动作持续时间（真实秒）")]
    public float slowMotionDuration = 1.5f;

    [Header("死亡动画")]
    [Tooltip("Animator 中死亡动画的 Trigger 参数名")]
    public string deathTriggerName = "doDie";

    // 内部状态
    private bool isCeremonyActive = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 开始 Boss 死亡表演。由 GameTimelineManager 调用，替代直接触发胜利。
    /// </summary>
    /// <param name="bossPosition">Boss 死亡时的世界坐标</param>
    /// <param name="bossGameObject">Boss 的 GameObject（用于播放死亡动画）</param>
    public void StartCeremony(Vector3 bossPosition, GameObject bossGameObject)
    {
        if (isCeremonyActive) return;
        isCeremonyActive = true;

        Debug.Log("<color=gold>[BossDeathCeremony] ★ Boss 死亡表演开始！</color>");
        StartCoroutine(CeremonySequence(bossPosition, bossGameObject));
    }

    private IEnumerator CeremonySequence(Vector3 bossPos, GameObject bossGO)
    {
        // ===== 阶段1：慢动作 + Boss 死亡动画 =====
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * slowMotionScale;

        // 尝试播放 Boss 的死亡动画
        if (bossGO != null)
        {
            // 停止 Boss 的 AI 和行为树（必须在设置动画之前！）
            EnemyAI ai = bossGO.GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;

            BehaviorTree bt = bossGO.GetComponentInChildren<BehaviorTree>();
            if (bt != null) bt.enabled = false;

            // 停止 NavMeshAgent
            UnityEngine.AI.NavMeshAgent agent = bossGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // 播放死亡动画
            Animator bossAnimator = bossGO.GetComponentInChildren<Animator>();
            if (bossAnimator != null)
            {
                // 切换到 UnscaledTime 模式，确保慢动作下动画仍然正常播放
                bossAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

                // 重置所有可能残留的 Trigger，防止状态机冲突
                foreach (var param in bossAnimator.parameters)
                {
                    if (param.type == AnimatorControllerParameterType.Trigger)
                    {
                        bossAnimator.ResetTrigger(param.name);
                    }
                    // 重置 Bool 参数（如 isFiringSpiral 等），防止卡在技能动画
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        bossAnimator.SetBool(param.name, false);
                    }
                }

                // 设置死亡触发器
                bossAnimator.SetTrigger(deathTriggerName);
                Debug.Log($"<color=gold>[BossDeathCeremony] 已触发死亡动画: {deathTriggerName}</color>");
            }
            else
            {
                Debug.LogWarning("[BossDeathCeremony] 未找到 Boss 的 Animator 组件！");
            }
        }

        // 等待慢动作结束（使用真实时间）
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        // 恢复正常时间
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // ===== 阶段2：延迟后开始金币喷泉 =====
        yield return new WaitForSeconds(delayBeforeFountain);

        // 更新喷泉位置（Boss 可能在动画中移动了）
        Vector3 fountainPos = bossGO != null ? bossGO.transform.position : bossPos;
        // 稍微抬高喷出点
        fountainPos.y += 1.5f;

        // 开始喷金币（协程在后台运行）
        StartCoroutine(SpawnCoinFountain(fountainPos));

        // ===== 阶段3：等待喷泉效果 =====
        yield return new WaitForSeconds(fountainDuration);

        // ===== 阶段3.5：将所有未拾取的金币快速吸向玩家 =====
        AbsorbAllRemainingCoins();

        // 动态等待：直到所有金币被拾取完毕（最多等 5 秒防卡死）
        float maxWaitTime = 5f;
        float waitTimer = 0f;
        while (waitTimer < maxWaitTime)
        {
            GoldPickup[] remaining = FindObjectsByType<GoldPickup>(FindObjectsSortMode.None);
            if (remaining.Length == 0) break;
            waitTimer += Time.deltaTime;
            yield return null;
        }

        // 额外等一小会让玩家欣赏一下
        yield return new WaitForSeconds(0.5f);

        // ===== 阶段4：销毁 Boss 并触发胜利 =====
        if (bossGO != null)
        {
            Destroy(bossGO);
        }

        // 触发胜利结算
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleVictory();
        }

        isCeremonyActive = false;
    }

    /// <summary>
    /// 喷泉式生成金币，用抛物线协程模拟飞行轨迹
    /// </summary>
    private IEnumerator SpawnCoinFountain(Vector3 origin)
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("[BossDeathCeremony] coinPrefab 未设置！无法喷射金币。");
            yield break;
        }

        // 计算每个金币之间的间隔
        float interval = fountainDuration / coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            // 随机计算落点
            Vector2 randomCircle = Random.insideUnitCircle * horizontalSpread;
            Vector3 landingPos = origin + new Vector3(randomCircle.x, -origin.y + 0.1f, randomCircle.y);

            // 在喷出点创建金币
            GameObject coin = Instantiate(coinPrefab, origin, Quaternion.identity);

            // 启动抛物线飞行协程
            float arcHeight = Random.Range(minArcHeight, maxArcHeight);
            float flightTime = coinFlightDuration + Random.Range(-0.2f, 0.3f);
            StartCoroutine(ArcMoveCoin(coin, origin, landingPos, arcHeight, flightTime));

            // 间隔生成
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// 单个金币的抛物线飞行
    /// </summary>
    private IEnumerator ArcMoveCoin(GameObject coin, Vector3 start, Vector3 end, float arcHeight, float duration)
    {
        if (coin == null) yield break;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            // 水平线性插值
            Vector3 currentPos = Vector3.Lerp(start, end, t);

            // 垂直抛物线：y = 4h * t * (1 - t)，在 t=0.5 时达到最高点
            currentPos.y += arcHeight * 4f * t * (1f - t);

            // 防止金币被销毁后报错
            if (coin == null) yield break;
            coin.transform.position = currentPos;

            // 金币旋转效果
            coin.transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

            yield return null;
        }

        // 确保落地位置精确
        if (coin != null)
        {
            coin.transform.position = end;
        }
    }
    /// <summary>
    /// 将场景中所有未拾取的金币强制吸向玩家
    /// </summary>
    private void AbsorbAllRemainingCoins()
    {
        Transform playerTarget = null;

        // 获取玩家位置
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
            // 优先用 AimTargetPoint
            Transform aimTarget = playerTarget.Find("AimTargetPoint");
            if (aimTarget != null) playerTarget = aimTarget;
        }

        if (playerTarget == null)
        {
            Debug.LogWarning("[BossDeathCeremony] 找不到玩家，无法吸收剩余金币");
            return;
        }

        // 找到场景中所有金币
        GoldPickup[] allCoins = FindObjectsByType<GoldPickup>(FindObjectsSortMode.None);
        int absorbedCount = 0;

        foreach (GoldPickup coin in allCoins)
        {
            if (coin != null)
            {
                // 加速金币飞行：大幅提高速度 + 跳过浮空/悬停动画
                coin.collectionSpeed = 50f;
                coin.absorbFloatHeight = 0.3f;
                coin.absorbFloatDuration = 0.05f;
                coin.absorbHoverDuration = 0f;
                coin.pickupDelay = 0f;
                coin.magnetRadius = 999f; // 确保立即触发
                // 用 ForceCollect 强制吸收（跳过延迟、浮空动画，高速飞向玩家）
                coin.ForceCollect(playerTarget);
                absorbedCount++;
            }
        }

        Debug.Log($"<color=gold>[BossDeathCeremony] 正在吸收 {absorbedCount} 枚剩余金币！</color>");
    }

    /// <summary>
    /// 重置状态（场景重载时）
    /// </summary>
    public void ResetCeremony()
    {
        isCeremonyActive = false;
        StopAllCoroutines();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
