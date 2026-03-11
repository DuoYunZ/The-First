using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UISelectionGroup : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("需要交互的组件列表 (Button, Slider, Toggle, Dropdown)")]
    public List<Selectable> menuItems;
    
    [Tooltip("黑色的高亮底图指示器")]
    public RectTransform highlightBackground; 

    [Header("效果设置")]
    [Tooltip("鼠标悬停或选中时的音效")]
    public AudioClip hoverSound;
    private AudioSource audioSource;
    
    [Tooltip("底图移动的平滑度")]
    public float movementSpeed = 15f; 
    
    [Header("缩放与呼吸效果")]
    [Tooltip("选中时的基础放大倍数")]
    public float selectedScale = 1.1f;
    [Tooltip("缩放动画速度")]
    public float scaleSpeed = 10f;
    [Tooltip("呼吸效果的幅度 (基于 selectedScale 波动)")]
    public float breathMagnitude = 0.05f;
    [Tooltip("呼吸效果的速度")]
    public float breathSpeed = 3f;

    private RectTransform targetButtonRect;
    private Selectable currentSelectedButton;

    // 记录原始缩放大小以防万一
    private Dictionary<Selectable, Vector3> originalScales = new Dictionary<Selectable, Vector3>();

    void Start()
    {
        // 如果没有分配 AudioSource，尝试获取或添加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        foreach (var item in menuItems)
        {
            if (item == null) continue;

            // 记录初始缩放大小
            originalScales[item] = item.GetComponent<RectTransform>().localScale;

            // 动态添加事件监听组件
            var listener = item.gameObject.AddComponent<UIHoverListener>();
            listener.onHover += () => OnButtonSelected(item);
            listener.onSelect += () => OnButtonSelected(item);
        }

        // 初始化时选中第一个
        if (menuItems.Count > 0 && menuItems[0] != null)
        {
            // 如果一开始黑底图需要立刻飞到按钮下，或者直接定位
            targetButtonRect = menuItems[0].GetComponent<RectTransform>();
            if (highlightBackground != null)
            {
                highlightBackground.position = targetButtonRect.position;
            }

            OnButtonSelected(menuItems[0]);
            // 确保 EventSystem 选中了第一个，以支持立刻用手柄向下浏览
            EventSystem.current.SetSelectedGameObject(menuItems[0].gameObject);
        }
    }

    void Update()
    {
        // 1. 平滑移动底图
        if (targetButtonRect != null && highlightBackground != null)
        {
            highlightBackground.position = Vector3.Lerp(
                highlightBackground.position,
                targetButtonRect.position,
                Time.unscaledDeltaTime * movementSpeed
            );
        }

        // 2. 处理每个按钮的缩放与呼吸效果
        foreach (var item in menuItems)
        {
            if (item == null) continue;

            RectTransform itemRect = item.GetComponent<RectTransform>();
            Vector3 originalScale = originalScales.ContainsKey(item) ? originalScales[item] : Vector3.one;

            if (item == currentSelectedButton)
            {
                // 计算呼吸效果的目标缩放比例
                float breath = Mathf.Sin(Time.unscaledTime * breathSpeed) * breathMagnitude;
                float currentTargetScale = selectedScale + breath;
                Vector3 targetVector = originalScale * currentTargetScale;

                itemRect.localScale = Vector3.Lerp(
                    itemRect.localScale, 
                    targetVector, 
                    Time.unscaledDeltaTime * scaleSpeed
                );
            }
            else
            {
                // 未选中的恢复原始大小
                itemRect.localScale = Vector3.Lerp(
                    itemRect.localScale, 
                    originalScale, 
                    Time.unscaledDeltaTime * scaleSpeed
                );
            }
        }
    }

    public void OnButtonSelected(Selectable item)
    {
        if (currentSelectedButton == item) return; // 避免重复触发同个按钮

        currentSelectedButton = item;
        targetButtonRect = item.GetComponent<RectTransform>();

        // 播放音效
        if (hoverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }
}

/// <summary>
/// 辅助组件：监听 UGUI 的悬停和选中事件
/// </summary>
public class UIHoverListener : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public System.Action onHover;
    public System.Action onSelect;

    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke();
        // 当鼠标悬停时，也将其设置为 EventSystem 的当前选中对象，确保手柄和鼠标的状态同步
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        onSelect?.Invoke();
    }
}
