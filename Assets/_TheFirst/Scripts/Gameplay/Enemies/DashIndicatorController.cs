using UnityEngine;
using DG.Tweening;

public class DashIndicatorController : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("作为静态外框的Transform")]
    public Transform outerFrame;
    [Tooltip("用于播放填充动画的Transform")]
    public Transform innerFill;

    // 【新增】获取 SpriteRenderer 的引用
    private SpriteRenderer outerFrameRenderer;
    private SpriteRenderer innerFillRenderer;

    void Awake()
    {
        // 在 Awake 中获取引用
        if (outerFrame != null) outerFrameRenderer = outerFrame.GetComponent<SpriteRenderer>();
        if (innerFill != null) innerFillRenderer = innerFill.GetComponent<SpriteRenderer>();
    }
    /// <summary>
    /// 启动填充动画
    /// </summary>
    /// <param name="duration">填充动画的持续时间</param>
    /// <param name="width">预警框的宽度</param>
    /// <param name="length">预警框的长度/冲刺距离</param>
    public void Animate(float duration, float width, float length)
    {
        // 【核心修改】我们现在控制 Size 属性，而不是 transform.localScale
        if (outerFrameRenderer != null)
        {
            // 1. 立即设置外框的最终尺寸
            outerFrameRenderer.size = new Vector2(width, length);
        }

        if (innerFillRenderer != null)
        {
            // 2. 将填充物的初始宽度设为0，长度设为最终长度
            innerFillRenderer.size = new Vector2(0, length);

            // 3. 使用 DOTween 的通用 .To() 方法来制作填充动画
            DOTween.To(() => innerFillRenderer.size, // 我们要改变的值
                       x => innerFillRenderer.size = x,   // 如何设置这个值
                       new Vector2(width, length),        // 最终的目标值
                       duration)                          // 动画时长
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }

        // 4. 动画结束后销毁对象
        Destroy(gameObject, duration + 0.1f);
    }
}