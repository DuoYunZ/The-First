// MaterialTimeUpdater.cs (最终协程版)
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class MaterialTimeUpdater : MonoBehaviour
{
    // 【修改】直接引用 Image 组件，不再需要手动创建材质实例
    private Image controlledImage;
    private Coroutine timeUpdateCoroutine;

    void Awake()
    {
        controlledImage = GetComponent<Image>();
        if (controlledImage.material == null)
        {
            Debug.LogError("MaterialTimeUpdater: Image组件上没有材质！", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        // 启动协程
        if (timeUpdateCoroutine == null)
        {
            timeUpdateCoroutine = StartCoroutine(UpdateMaterialTime());
        }
    }

    void OnDisable()
    {
        // 停止协程，防止在对象禁用后继续运行
        if (timeUpdateCoroutine != null)
        {
            StopCoroutine(timeUpdateCoroutine);
            timeUpdateCoroutine = null;
        }
    }

    /// <summary>
    /// 【核心修改】使用协程和真实时间来更新
    /// </summary>
    IEnumerator UpdateMaterialTime()
    {
        // 这是一个永不停止的循环，只要脚本是激活的
        while (true)
        {
            if (controlledImage.material != null)
            {
                // 使用 Time.realtimeSinceStartup 获取不受缩放影响的真实游戏运行时间
                controlledImage.material.SetFloat("_UnscaledTime", Time.realtimeSinceStartup);
            }

            // 等待一小段时间（真实时间），然后继续下一次更新
            // 这能确保即使 Time.timeScale = 0，循环也能继续
            yield return new WaitForSecondsRealtime(0.02f); // 每秒大约更新50次
        }
    }
}