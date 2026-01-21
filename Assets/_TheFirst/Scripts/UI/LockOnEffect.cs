using UnityEngine;
using UnityEngine.UI; // 必须引用 UI

public class LockOnEffect : MonoBehaviour
{
    [Header("跟随设置")]
    public Transform target; // 由炮塔脚本赋值
    public Vector3 offset = new Vector3(0, 1.0f, 0); // 基础高度偏移(胸口)
    public float cameraOffset = 0.5f; // 向摄像机拉近多少米 (防止插进身体)

    [Header("动画设置")]
    public float driftDuration = 0.5f; // 漂移锁定时间
    public float driftAmount = 50f;    // 左右漂移的初始距离 (像素或单位)
    public float blinkSpeed = 15f;     // 闪烁速度

    [Header("组件引用")]
    public RectTransform leftBracket;  // 左半边括号 (可选)
    public RectTransform rightBracket; // 右半边括号 (可选)
    public Image mainImage;            // 如果只有一张整图，拖这个

    // 内部变量
    private float timer;
    private CanvasGroup canvasGroup;
    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
        // 自动添加 CanvasGroup 用于控制透明度闪烁
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        timer = 0f;
        // 初始状态：完全透明，位置散开
        if (canvasGroup) canvasGroup.alpha = 0f;
        UpdateAnimation(0f);
    }

    void LateUpdate() // 使用 LateUpdate 防止抖动
    {
        if (target == null)
        {
            Destroy(gameObject); // 目标没了，自己销毁
            return;
        }

        // --- 1. 位置跟随与防穿模 ---
        // 基础目标位置
        Vector3 finalPos = target.position + offset;

        // 【核心防穿模逻辑】
        // 计算从 目标 -> 摄像机 的方向
        if (mainCam != null)
        {
            Vector3 dirToCam = (mainCam.transform.position - finalPos).normalized;
            // 沿着摄像机方向把 UI 拉出来一点点，这样绝对不会插进身体里，永远在最上层
            finalPos += dirToCam * cameraOffset;

            // --- 2. 始终面朝摄像机 (Billboard) ---
            transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                             mainCam.transform.rotation * Vector3.up);
        }

        transform.position = finalPos;

        // --- 3. 播放锁定动画 ---
        if (timer < driftDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / driftDuration);
            // 使用 EaseOut 曲线让动作更有力
            float easeCurve = 1f - Mathf.Pow(1f - progress, 3);
            UpdateAnimation(easeCurve);
        }
        else
        {
            // 锁定完成后的状态：保持常亮或低频呼吸
            if (canvasGroup) canvasGroup.alpha = 1f;
            // 这里可以加一个微小的呼吸效果
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.05f);
        }
    }

    void UpdateAnimation(float progress)
    {
        // A. 左右漂移 / 缩放收束
        // 如果你只有一张图，我们用 Scale 模拟“从大到小聚焦”
        float scale = Mathf.Lerp(2.0f, 1.0f, progress);
        transform.localScale = Vector3.one * scale;

        // B. 左右位移 (如果你拆分了左右括号)
        if (leftBracket != null && rightBracket != null)
        {
            // 左边从更左边移回来
            leftBracket.anchoredPosition = new Vector2(Mathf.Lerp(-driftAmount, 0, progress), 0);
            // 右边从更右边移回来
            rightBracket.anchoredPosition = new Vector2(Mathf.Lerp(driftAmount, 0, progress), 0);
        }
        else if (mainImage != null)
        {
            // 只有一张图：加一点随机抖动模拟“校准中”
            float jitter = Mathf.Lerp(0.5f, 0f, progress);
            Vector3 randomOffset = new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0);
            mainImage.transform.localPosition = randomOffset;
        }

        // C. 高频闪烁 (Alpha Blink)
        if (canvasGroup != null)
        {
            // 在漂移过程中快速闪烁
            float blink = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            // 随着锁定完成，透明度趋向于 1 (完全显示)
            canvasGroup.alpha = Mathf.Lerp(blink, 1f, progress);

            // 颜色变化 (可选)：从黄色变红色
            if (mainImage != null)
            {
                mainImage.color = Color.Lerp(Color.yellow, Color.red, progress);
            }
        }
    }

    // 供外部调用设置目标
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        // 尝试自动校准高度
        Transform aimPoint = target.Find("AimTargetPoint");
        if (aimPoint != null)
        {
            // 计算 AimPoint 相对于 Root 的局部高度差
            offset = aimPoint.position - target.position;
        }
    }
}