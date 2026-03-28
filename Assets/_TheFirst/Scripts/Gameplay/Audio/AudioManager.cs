using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    private AudioSource sfxSource;
    private AudioSource bgmSource; // BGM 专用音源

    [Header("BGM 设置")]
    [Tooltip("BGM 音量")]
    [Range(0f, 1f)]
    public float bgmVolume = 0.5f;
    [Tooltip("BGM 切换时的淡入淡出时长（秒）")]
    public float bgmFadeDuration = 1.0f;

    [Header("场景 BGM 映射")]
    [Tooltip("配置每个场景对应的 BGM")]
    public List<SceneBgmEntry> sceneBgmList = new List<SceneBgmEntry>();

    [System.Serializable]
    public class SceneBgmEntry
    {
        [Tooltip("场景名称（必须与 Build Settings 中的名字完全一致）")]
        public string sceneName;
        [Tooltip("该场景使用的 BGM")]
        public AudioClip bgmClip;
        [Tooltip("该场景的 BGM 音量覆盖（0 = 使用全局音量）")]
        [Range(0f, 1f)]
        public float volumeOverride = 0f;
    }

    // 用于快速查找的字典
    private Dictionary<string, SceneBgmEntry> bgmLookup = new Dictionary<string, SceneBgmEntry>();
    private Coroutine fadeCoroutine;

    void Awake()
    {
        // 实现单例模式，确保只有一个AudioManager
        if (Instance == null)
        {
            Instance = this;
            // 让这个GameObject在加载新场景时不被销毁
            DontDestroyOnLoad(gameObject);

            // 音效音源
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0; // 确保是2D音效

            // BGM 音源
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.spatialBlend = 0;
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;

            // 构建查找字典
            BuildBgmLookup();

            // 监听场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // 如果已存在AudioManager，则销毁这个重复的
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // 取消监听，防止内存泄漏
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    /// <summary>
    /// 构建场景名 -> BGM 的快速查找字典
    /// </summary>
    private void BuildBgmLookup()
    {
        bgmLookup.Clear();
        foreach (var entry in sceneBgmList)
        {
            if (!string.IsNullOrEmpty(entry.sceneName) && entry.bgmClip != null)
            {
                bgmLookup[entry.sceneName] = entry;
            }
        }
    }

    /// <summary>
    /// 场景加载完成回调：自动切换对应 BGM
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (bgmLookup.TryGetValue(scene.name, out SceneBgmEntry entry))
        {
            float vol = entry.volumeOverride > 0 ? entry.volumeOverride : bgmVolume;
            PlayBgm(entry.bgmClip, vol);
        }
        // 如果场景没有配置 BGM，保持当前音乐不变
    }

    /// <summary>
    /// 播放 BGM（带淡入淡出）
    /// </summary>
    public void PlayBgm(AudioClip clip, float volume = -1f)
    {
        if (clip == null) return;
        if (volume < 0) volume = bgmVolume;

        // 如果正在播放同一首，不重复切换
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CrossFadeBgm(clip, volume));
    }

    /// <summary>
    /// 停止 BGM（带淡出）
    /// </summary>
    public void StopBgm()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutBgm());
    }

    /// <summary>
    /// 交叉淡入淡出切换 BGM
    /// </summary>
    private IEnumerator CrossFadeBgm(AudioClip newClip, float targetVolume)
    {
        // 淡出当前 BGM
        if (bgmSource.isPlaying && bgmSource.volume > 0)
        {
            float startVol = bgmSource.volume;
            float halfFade = bgmFadeDuration * 0.5f;
            float t = 0;
            while (t < halfFade)
            {
                t += Time.unscaledDeltaTime; // 用 unscaledDeltaTime 防止暂停时卡住
                bgmSource.volume = Mathf.Lerp(startVol, 0, t / halfFade);
                yield return null;
            }
        }

        // 切换曲目
        bgmSource.clip = newClip;
        bgmSource.volume = 0;
        bgmSource.Play();

        // 淡入新 BGM
        {
            float halfFade = bgmFadeDuration * 0.5f;
            float t = 0;
            while (t < halfFade)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0, targetVolume, t / halfFade);
                yield return null;
            }
            bgmSource.volume = targetVolume;
        }

        fadeCoroutine = null;
    }

    private IEnumerator FadeOutBgm()
    {
        float startVol = bgmSource.volume;
        float t = 0;
        while (t < bgmFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0, t / bgmFadeDuration);
            yield return null;
        }
        bgmSource.Stop();
        bgmSource.clip = null;
        fadeCoroutine = null;
    }

    /// <summary>
    /// 播放一次性音效（2D）
    /// </summary>
    public void PlaySoundEffect(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volumeScale);
        }
    }

    /// <summary>
    /// 动态设置 BGM 音量（如设置界面中使用）
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = bgmVolume;
        }
    }
}