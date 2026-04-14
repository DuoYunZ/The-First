using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class SettingsMenu : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("游戏的主音频混合器")]
    public AudioMixer mainMixer;

    [Header("Tab 页切换")]
    [Tooltip("常规设置面板（音量、分辨率、语言）")]
    public GameObject generalSettingsPanel;
    [Tooltip("键位设置面板")]
    public GameObject keyBindingPanel;
    [Tooltip("常规设置 Tab 按钮")]
    public Button generalTabButton;
    [Tooltip("键位设置 Tab 按钮")]
    public Button keyBindingTabButton;

    [Header("Tab 按钮样式")]
    [Tooltip("选中状态的 Tab 按钮颜色")]
    public Color tabActiveColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("未选中状态的 Tab 按钮颜色")]
    public Color tabInactiveColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    [Header("UI元素引用")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("显示设置")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("语言设置")]
    [Tooltip("语言选择下拉框")]
    public TMP_Dropdown languageDropdown;

    private Resolution[] resolutions;

    // PlayerPrefs 保存的键名
    private const string MASTER_VOL_KEY = "MasterVolume";
    private const string BGM_VOL_KEY = "BGMVolume";
    private const string SFX_VOL_KEY = "SFXVolume";

    void Start()
    {
        // 游戏启动时，加载已保存的设置
        LoadVolumeSettings();

        // 为每个滑块添加监听器，当值改变时调用对应的方法
        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        // 初始化分辨率设置
        InitResolutionSettings();

        // 初始化语言设置
        InitLanguageSettings();

        // 初始化 Tab 页切换
        InitTabButtons();

        // 默认显示常规设置 Tab
        SwitchToTab(0);
    }

    // ===== Tab 页切换 =====

    private void InitTabButtons()
    {
        if (generalTabButton != null)
        {
            generalTabButton.onClick.AddListener(() => SwitchToTab(0));
        }
        if (keyBindingTabButton != null)
        {
            keyBindingTabButton.onClick.AddListener(() => SwitchToTab(1));
        }
    }

    /// <summary>
    /// 切换到指定 Tab 页
    /// </summary>
    /// <param name="tabIndex">0=常规设置, 1=键位设置</param>
    public void SwitchToTab(int tabIndex)
    {
        // 切换面板显示
        if (generalSettingsPanel != null)
            generalSettingsPanel.SetActive(tabIndex == 0);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(tabIndex == 1);

        // 更新 Tab 按钮样式
        UpdateTabButtonStyle(generalTabButton, tabIndex == 0);
        UpdateTabButtonStyle(keyBindingTabButton, tabIndex == 1);
    }

    /// <summary>
    /// 更新 Tab 按钮的视觉样式
    /// </summary>
    private void UpdateTabButtonStyle(Button button, bool isActive)
    {
        if (button == null) return;

        // 更新按钮文字颜色
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = isActive ? tabActiveColor : tabInactiveColor;
        }

        // 更新按钮图片透明度
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            Color c = image.color;
            c.a = isActive ? 1f : 0.3f;
            image.color = c;
        }
    }

    // ===== 分辨率设置 =====

    private void InitResolutionSettings()
    {
        resolutions = Screen.resolutions;

        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + resolutions[i].refreshRateRatio.value.ToString("F0") + "Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 添加监听器
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        // 初始化全屏 Toggle
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    // ===== 音量设置 =====

    public void SetMasterVolume(float value)
    {
        // UI滑块的值是线性的 (0-1)，而Mixer的音量是对数的 (dB)
        float volumeInDb = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20;
        mainMixer.SetFloat("MasterVolume", volumeInDb);
        PlayerPrefs.SetFloat(MASTER_VOL_KEY, value);
    }

    public void SetBGMVolume(float value)
    {
        float volumeInDb = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20;
        mainMixer.SetFloat("BGMVolume", volumeInDb);
        PlayerPrefs.SetFloat(BGM_VOL_KEY, value);
    }

    public void SetSFXVolume(float value)
    {
        float volumeInDb = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20;
        mainMixer.SetFloat("SFXVolume", volumeInDb);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, value);
    }

    private void LoadVolumeSettings()
    {
        // 从 PlayerPrefs 加载值，如果不存在则默认为1 (满音量)
        float masterValue = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1f);
        float bgmValue = PlayerPrefs.GetFloat(BGM_VOL_KEY, 1f);
        float sfxValue = PlayerPrefs.GetFloat(SFX_VOL_KEY, 1f);

        // 将加载的值应用到UI滑块上
        masterVolumeSlider.value = masterValue;
        bgmVolumeSlider.value = bgmValue;
        sfxVolumeSlider.value = sfxValue;

        // 同时，立即将这些值应用到Audio Mixer
        SetMasterVolume(masterValue);
        SetBGMVolume(bgmValue);
        SetSFXVolume(sfxValue);
    }

    // 当脚本被销毁时，确保所有设置都被保存
    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    // ===== 语言设置 =====

    private void InitLanguageSettings()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();

        // 使用各语言的原生名称作为显示文本
        var options = new List<string> { "中文", "English" };
        languageDropdown.AddOptions(options);

        // 设置为当前语言
        languageDropdown.value = LocalizationManager.GetCurrentLanguageIndex();
        languageDropdown.RefreshShownValue();

        // 添加监听器
        languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    /// <summary>
    /// 语言下拉框值改变时调用
    /// </summary>
    public void SetLanguage(int index)
    {
        LocalizationManager.SetLanguageByIndex(index);
    }
}