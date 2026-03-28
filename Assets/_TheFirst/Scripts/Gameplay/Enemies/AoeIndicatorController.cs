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
    /// <param name="radius">预警圈的最终半径（世界单位）</param>
    public void Animate(float duration, float radius)
    {
        // 计算目标直径（世界单位）
        float targetDiameter = radius * 2f;

        // 1. 设置外圈大小
        if (outerRing != null)
        {
            float localScale = CalculateLocalScale(outerRing, targetDiameter);
            outerRing.localScale = new Vector3(localScale, localScale, localScale);
        }

        // 2. 内圈从0扩张到和外圈一样大小
        if (innerCircle != null)
        {
            float targetScale = CalculateLocalScale(innerCircle, targetDiameter);
            innerCircle.localScale = Vector3.zero;

            // 使用 DOTween 制作内圈在 duration 秒内扩张到最终大小的动画
            innerCircle.DOScale(targetScale, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }

        // 3. 在动画结束后销毁整个预警圈对象
        Destroy(gameObject, duration + 0.1f);
    }

    /// <summary>
    /// 计算让目标达到指定世界直径所需的 localScale 值
    /// 会考虑 Sprite 原始尺寸和父对象缩放的影响
    /// </summary>
    private float CalculateLocalScale(Transform target, float worldDiameter)
    {
        // 获取 Sprite 在 localScale=(1,1,1) 时的原始尺寸
        float nativeSize = GetNativeSpriteSize(target);

        // 计算需要的世界缩放
        float worldScale = worldDiameter / nativeSize;

        // 补偿父对象的缩放（localScale * parentLossyScale = worldScale）
        float parentScaleFactor = 1f;
        if (target.parent != null)
        {
            // 取 X 轴（假设均匀缩放）
            parentScaleFactor = target.parent.lossyScale.x;
            if (parentScaleFactor < 0.001f) parentScaleFactor = 1f; // 防止除零
        }

        return worldScale / parentScaleFactor;
    }

    /// <summary>
    /// 获取 Sprite 在 localScale=(1,1,1) 时的世界空间直径
    /// 如果没有 SpriteRenderer 则默认为 1 单位
    /// </summary>
    private float GetNativeSpriteSize(Transform target)
    {
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) sr = target.GetComponentInChildren<SpriteRenderer>();

        if (sr != null && sr.sprite != null)
        {
            // Sprite 的像素宽度 / Pixels Per Unit = 世界单位直径
            return sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
        }

        // 没有 SpriteRenderer 的情况（比如 MeshRenderer/Quad），默认1单位
        return 1f;
    }
}