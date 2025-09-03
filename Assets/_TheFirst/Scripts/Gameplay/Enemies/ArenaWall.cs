// --- ArenaWall.cs (带自毁功能版) ---
using UnityEngine;
using System.Collections;

public class ArenaWall : MonoBehaviour
{
    [Tooltip("墙壁在场上存在的总时间")]
    public float lifetime = 10f;
    [Tooltip("出现动画时长")]
    public float appearDuration = 0.5f;
    [Tooltip("消失动画时长")]
    public float disappearDuration = 1.0f;
    [Tooltip("墙壁的视觉模型，我们将缩放它")]
    public Transform visualElement;

    void Awake()
    {
        if (visualElement == null) visualElement = transform;
        visualElement.localScale = Vector3.zero;
    }

    // "出现"
    public void Activate()
    {
        StartCoroutine(ScaleOverTime(Vector3.one, appearDuration));
        // 【核心修改】在激活时，就启动一个延时自毁/消失的协程
        StartCoroutine(DeactivateAfterDelay(lifetime));
    }

    // "消失"
    public void Deactivate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        StartCoroutine(ScaleOverTime(Vector3.zero, disappearDuration, true));
    }

    // 【新增】延时调用Deactivate的方法
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Deactivate();
    }

    private IEnumerator ScaleOverTime(Vector3 targetScale, float duration, bool destroyOnEnd = false)
    {
        Vector3 initialScale = visualElement.localScale;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            visualElement.localScale = Vector3.Lerp(initialScale, targetScale, timer / duration);
            yield return null;
        }

        visualElement.localScale = targetScale;
        if (destroyOnEnd)
        {
            Destroy(gameObject);
        }
    }
}