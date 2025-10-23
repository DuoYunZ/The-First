using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsMenu : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("游戏的主音频混合器")]
    public AudioMixer mainMixer;

    [Header("UI元素引用")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

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
    }

    public void SetMasterVolume(float value)
    {
        // UI滑块的值是线性的 (0-1)，而Mixer的音量是对数的 (dB)
        // 我们需要转换一下。当value为0时，log10(0)是负无穷，所以我们给一个极小值来代表静音。
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
}