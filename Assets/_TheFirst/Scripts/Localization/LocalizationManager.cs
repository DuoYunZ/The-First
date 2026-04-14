using UnityEngine;
using System;

/// <summary>
/// 轻量级本地化管理器（单例）。
/// 提供 T("key") 静态方法获取翻译文本，支持运行时切换语言。
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // 语言变更事件，UI 脚本可以订阅此事件来刷新显示
    public static event Action OnLanguageChanged;

    // 支持的语言列表（与 LanguageTable 中的 key 对应）
    public static readonly SystemLanguage[] SupportedLanguages = 
    {
        SystemLanguage.ChineseSimplified,
        SystemLanguage.English
    };

    // 当前选择的语言
    private static SystemLanguage _currentLanguage = SystemLanguage.ChineseSimplified;
    public static SystemLanguage CurrentLanguage => _currentLanguage;

    // PlayerPrefs 存储键名
    private const string LANGUAGE_PREF_KEY = "GameLanguage";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 确保是根物体（DontDestroyOnLoad 仅对根物体生效）
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        // 加载已保存的语言偏好，如果没有则自动检测系统语言
        LoadLanguagePreference();
    }

    /// <summary>
    /// 核心翻译方法：根据 key 获取当前语言的文本
    /// 用法：LocalizationManager.T("ui.wave", waveNum)
    /// </summary>
    public static string T(string key, params object[] args)
    {
        string template = LanguageTable.Get(key, _currentLanguage);

        if (args == null || args.Length == 0)
            return template;

        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[本地化] 格式化失败: key='{key}', lang={_currentLanguage}");
            return template;
        }
    }

    /// <summary>
    /// 尝试获取翻译，找不到时返回 false（不打印警告）
    /// </summary>
    public static bool TryGet(string key, out string result)
    {
        result = LanguageTable.TryGet(key, _currentLanguage);
        return result != null;
    }

    /// <summary>
    /// 切换语言并保存偏好
    /// </summary>
    public static void SetLanguage(SystemLanguage language)
    {
        if (_currentLanguage == language) return;

        _currentLanguage = language;
        PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language.ToString());
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// 通过索引设置语言（供 Dropdown 使用）
    /// 索引对应 SupportedLanguages 数组
    /// </summary>
    public static void SetLanguageByIndex(int index)
    {
        if (index >= 0 && index < SupportedLanguages.Length)
        {
            SetLanguage(SupportedLanguages[index]);
        }
    }

    /// <summary>
    /// 获取当前语言在 SupportedLanguages 数组中的索引
    /// </summary>
    public static int GetCurrentLanguageIndex()
    {
        for (int i = 0; i < SupportedLanguages.Length; i++)
        {
            if (SupportedLanguages[i] == _currentLanguage) return i;
        }
        return 0; // 默认返回中文
    }

    /// <summary>
    /// 加载语言偏好，无保存记录时自动检测系统语言
    /// </summary>
    private void LoadLanguagePreference()
    {
        if (PlayerPrefs.HasKey(LANGUAGE_PREF_KEY))
        {
            string saved = PlayerPrefs.GetString(LANGUAGE_PREF_KEY);
            if (Enum.TryParse(saved, out SystemLanguage lang))
            {
                _currentLanguage = lang;
                return;
            }
        }

        // 自动检测系统语言
        SystemLanguage sysLang = Application.systemLanguage;
        if (sysLang == SystemLanguage.Chinese ||
            sysLang == SystemLanguage.ChineseSimplified ||
            sysLang == SystemLanguage.ChineseTraditional)
        {
            _currentLanguage = SystemLanguage.ChineseSimplified;
        }
        else
        {
            _currentLanguage = SystemLanguage.English;
        }
    }
}
