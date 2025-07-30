using UnityEngine;
using DG.Tweening; // 确保您项目中已安装 DOTween

public class AoeIndicatorController : MonoBehaviour
{
    [Header("组件引用")]
    public Transform outerRing;
    public Transform innerCircle;

    /// <summary>
    /// 启动预警动画
    /// </summary>
    /// <param name="duration">内圈扩张到外圈所需的时间</param>
    /// <param name="radius">预警圈的最终半径</param>
    public void Animate(float duration, float radius)
    {
        // Sprite默认是1x1单位，半径为0.5。所以要达到'radius'的半径，localScale需要是radius*2。
        float finalScale = radius * 2f;

        // 1. 立即设置外圈的大小
        if (outerRing != null)
        {
            outerRing.localScale = new Vector3(finalScale, finalScale, 1f);
        }

        // 2. 将内圈的初始大小设为0
        if (innerCircle != null)
        {
            innerCircle.localScale = Vector3.zero;

            // 3. 使用 DOTween 制作内圈在'duration'秒内扩张到最终大小的动画
            innerCircle.DOScale(finalScale, duration)
                .SetEase(Ease.Linear) // 使用线性缓动，确保扩张速度均匀
                .SetUpdate(true); // 保证在时间暂停时也能播放
        }

        // 4. 在动画结束后（加一点点缓冲时间），销毁整个预警圈对象
        Destroy(gameObject, duration + 0.1f);
    }
}