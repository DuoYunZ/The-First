using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class ChainLightningVFX : MonoBehaviour
{
    private LineRenderer lineRenderer;

    public int points = 10; // 闪电的曲折点数量
    public float randomness = 0.5f; // 曲折的幅度
    public float lifetime = 0.2f; // 闪电持续时间

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// 设置闪电链的起点和终点
    /// </summary>
    public void Setup(Vector3 startPoint, Vector3 endPoint)
    {
        lineRenderer.positionCount = points; // 设置线段的点数

        for (int i = 0; i < points; i++)
        {
            // 使用线性插值找到基础点
            Vector3 pos = Vector3.Lerp(startPoint, endPoint, (float)i / (points - 1));

            // 为中间的点添加随机偏移，制造闪电的曲折感
            if (i > 0 && i < points - 1)
            {
                pos.x += Random.Range(-randomness, randomness);
                pos.y += Random.Range(-randomness, randomness);
                pos.z += Random.Range(-randomness, randomness);
            }

            lineRenderer.SetPosition(i, pos);
        }

        // 在设定的生命周期后销毁自己
        Destroy(gameObject, lifetime);
    }
}