using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    private AudioSource sfxSource;

    void Awake()
    {
        // 实现单例模式，确保只有一个AudioManager
        if (Instance == null)
        {
            Instance = this;
            // 让这个GameObject在加载新场景时不被销毁
            DontDestroyOnLoad(gameObject);
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0; // 确保是2D音效
        }
        else
        {
            // 如果已存在AudioManager，则销毁这个重复的
            Destroy(gameObject);
        }
    }
    public void PlaySoundEffect(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volumeScale);
        }
    }
}