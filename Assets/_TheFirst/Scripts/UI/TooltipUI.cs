using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 通用气泡提示框 UI（单例）。
/// 由其他脚本调用 Show/Hide 控制显示，气泡大小自动适应文字长度。
/// 需要在 Canvas 下放置一个 TooltipUI 预制件并挂载此脚本。
/// </summary>
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("气泡背景面板（需要 ContentSizeFitter + 子级 TMP 文本）")]
    public RectTransform tooltipPanel;

    [Tooltip("气泡内的文字组件")]
    public TextMeshProUGUI tooltipText;

    [Header("偏移设置")]
    [Tooltip("气泡相对鼠标位置的偏移（像素）")]
    public Vector2 offset = new Vector2(0f, 60f);

    [Tooltip("是否跟随鼠标移动")]
    public bool followMouse = false;

    private Canvas _parentCanvas;
    private RectTransform _canvasRect;
    private bool _isShowing = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 获取父级 Canvas 信息，用于坐标转换
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas != null)
            _canvasRect = _parentCanvas.GetComponent<RectTransform>();

        // 初始隐藏
        if (tooltipPanel != null)
            tooltipPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        // 如果显示中且跟随鼠标，更新位置
        if (_isShowing && followMouse)
        {
            UpdatePosition(Input.mousePosition);
        }
    }

    /// <summary>
    /// 显示气泡提示
    /// </summary>
    /// <param name="text">要显示的文字</param>
    /// <param name="anchorPosition">屏幕空间中的锚点位置（一般传鼠标位置或UI元素位置）</param>
    public void Show(string text, Vector2 anchorPosition)
    {
        if (tooltipPanel == null || tooltipText == null) return;
        if (string.IsNullOrEmpty(text)) return;

        tooltipText.text = text;
        tooltipPanel.gameObject.SetActive(true);
        _isShowing = true;

        // 强制刷新布局，让 ContentSizeFitter 重新计算大小
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

        // 延迟一帧再次刷新并定位（确保 ContentSizeFitter 完成计算）
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

        UpdatePosition(anchorPosition);
    }

    /// <summary>
    /// 隐藏气泡提示
    /// </summary>
    public void Hide()
    {
        if (tooltipPanel != null)
            tooltipPanel.gameObject.SetActive(false);
        _isShowing = false;
    }

    /// <summary>
    /// 更新气泡位置（水平居中于锚点上方，确保不超出屏幕）
    /// </summary>
    private void UpdatePosition(Vector2 screenPos)
    {
        if (_parentCanvas == null || tooltipPanel == null) return;

        // 将屏幕坐标转为 Canvas 本地坐标
        Vector2 localPoint;
        Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _parentCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, cam, out localPoint);

        // 获取气泡的实际大小
        Vector2 tooltipSize = tooltipPanel.sizeDelta;

        // 水平居中：气泡中心对齐到锚点 X 位置
        localPoint.x -= tooltipSize.x * 0.5f;
        // 垂直向上偏移
        localPoint.y += offset.y;

        // 边缘修正：防止气泡超出屏幕
        Vector2 canvasSize = _canvasRect.sizeDelta;
        float halfCanvasW = canvasSize.x * 0.5f;
        float halfCanvasH = canvasSize.y * 0.5f;

        // 右侧超出
        if (localPoint.x + tooltipSize.x > halfCanvasW)
            localPoint.x = halfCanvasW - tooltipSize.x;
        // 左侧超出
        if (localPoint.x < -halfCanvasW)
            localPoint.x = -halfCanvasW;
        // 顶部超出 → 翻到图标下方
        if (localPoint.y + tooltipSize.y > halfCanvasH)
            localPoint.y = localPoint.y - tooltipSize.y - offset.y * 2;
        // 底部超出
        if (localPoint.y < -halfCanvasH)
            localPoint.y = -halfCanvasH;

        tooltipPanel.localPosition = localPoint;
    }
}
