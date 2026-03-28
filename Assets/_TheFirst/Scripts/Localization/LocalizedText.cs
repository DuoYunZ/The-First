using UnityEngine;
using TMPro;

/// <summary>
/// 挂在 TextMeshPro UI 文本上的本地化组件。
/// 自动翻译文本，并支持每种语言的布局微调（位置、大小、字号）。
/// 语言切换时自动刷新文本和布局。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [Header("本地化设置")]
    [Tooltip("翻译 key，对应 LanguageTable 中的条目\n例如：ui.settings")]
    public string localizationKey;

    [Header("英文布局微调")]
    [Tooltip("勾选后，切换英文时使用下方的位置/大小/字号覆盖值\n不勾则只替换文本，位置不变")]
    public bool useEnglishOverrides = false;

    [Tooltip("英文布局的锚点位置 (anchoredPosition)")]
    public Vector2 englishPosition;

    [Tooltip("英文布局的尺寸 (sizeDelta)\n设为 (0,0) 则保持原始尺寸不变")]
    public Vector2 englishSizeDelta;

    [Tooltip("英文布局的字号\n设为 0 则保持原始字号不变")]
    public float englishFontSize = 0;

    // 缓存原始中文布局数据
    private Vector2 _originalPosition;
    private Vector2 _originalSizeDelta;
    private float _originalFontSize;
    private bool _hasCachedOriginals = false;

    private TextMeshProUGUI _text;
    private RectTransform _rect;

    void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
        _rect = GetComponent<RectTransform>();
        CacheOriginals();
    }

    /// <summary>
    /// 缓存当前（中文）布局数据，作为切回中文时的恢复值
    /// </summary>
    private void CacheOriginals()
    {
        if (_hasCachedOriginals) return;
        if (_rect != null)
        {
            _originalPosition = _rect.anchoredPosition;
            _originalSizeDelta = _rect.sizeDelta;
        }
        if (_text != null)
        {
            _originalFontSize = _text.fontSize;
        }
        _hasCachedOriginals = true;
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateText;
    }

    /// <summary>
    /// 根据当前语言刷新文本和布局
    /// </summary>
    public void UpdateText()
    {
        if (_text == null || string.IsNullOrEmpty(localizationKey)) return;

        // 1. 更新翻译文本
        _text.text = LocalizationManager.T(localizationKey);

        // 2. 如果启用了英文布局覆盖，应用或恢复布局
        if (useEnglishOverrides)
        {
            bool isEnglish = LocalizationManager.CurrentLanguage == SystemLanguage.English;
            ApplyLayout(isEnglish);
        }
    }

    /// <summary>
    /// 应用布局覆盖：英文用自定义值，中文恢复原始值
    /// </summary>
    private void ApplyLayout(bool useEnglish)
    {
        if (useEnglish)
        {
            // 应用英文布局
            if (_rect != null)
            {
                _rect.anchoredPosition = englishPosition;
                // sizeDelta 为 (0,0) 时保持原始尺寸
                if (englishSizeDelta != Vector2.zero)
                {
                    _rect.sizeDelta = englishSizeDelta;
                }
            }
            if (_text != null && englishFontSize > 0)
            {
                _text.enableAutoSizing = false;
                _text.fontSize = englishFontSize;
            }
        }
        else
        {
            // 恢复中文布局
            if (_rect != null)
            {
                _rect.anchoredPosition = _originalPosition;
                _rect.sizeDelta = _originalSizeDelta;
            }
            if (_text != null)
            {
                _text.enableAutoSizing = false;
                _text.fontSize = _originalFontSize;
            }
        }
    }

    /// <summary>
    /// 编辑器辅助：按钮"从当前位置复制"
    /// 在英文模式下手动调整好 RectTransform 后，点击此按钮复制当前值到英文覆盖字段
    /// </summary>
    [ContextMenu("将当前 RectTransform 值复制到英文覆盖")]
    private void CopyCurrentToEnglishOverrides()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_text == null) _text = GetComponent<TextMeshProUGUI>();

        englishPosition = _rect.anchoredPosition;
        englishSizeDelta = _rect.sizeDelta;
        if (_text != null) englishFontSize = _text.fontSize;
        useEnglishOverrides = true;

        Debug.Log($"[LocalizedText] 已复制当前布局到英文覆盖: pos={englishPosition}, size={englishSizeDelta}, fontSize={englishFontSize}");
    }
}
