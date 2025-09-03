// MeleeWarningIndicatorController.cs (最终版本)
using UnityEngine;
using DG.Tweening;

public class MeleeWarningIndicatorController : MonoBehaviour
{
    private Material mat;

    void Awake()
    {
        // 使用GetComponentInChildren确保能找到Sprite Renderer，即使它在子对象上
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            // 获取材质实例，避免修改项目中的原始材质
            mat = renderer.material;
        }
        else
        {
            Debug.LogError("MeleeWarningIndicatorController: 在对象或其子对象中找不到Sprite Renderer组件！", this.gameObject);
        }
    }

    public void Animate(float duration, float attackRadius, float attackAngle, float visualRadius)
    {
        if (mat == null)
        {
            // 如果材质为空，在指定时间后销毁自己并退出，防止报错
            Destroy(gameObject, duration);
            return;
        }

        // 我们不再通过代码控制视觉大小，完全依赖于您在Prefab中设置的Scale

        // 1. 设置Shader的角度
        mat.SetFloat("_SectorAngle", attackAngle);

        mat.SetFloat("_TargetFillAmount", attackRadius / visualRadius);

        // 2. 计算并动画填充量
        float targetFillAmount = 0f;
        if (visualRadius > 0.001f) // 增加一个安全检查，防止除以零
        {
            targetFillAmount = attackRadius / visualRadius;
        }

        mat.SetFloat("_FillAmount", 0f);
        mat.DOFloat(targetFillAmount, "_FillAmount", duration).SetEase(Ease.Linear);

        // 在动画结束后稍作延迟再销毁，确保动画能完整播放
        Destroy(gameObject, duration + 0.1f);
    }
}