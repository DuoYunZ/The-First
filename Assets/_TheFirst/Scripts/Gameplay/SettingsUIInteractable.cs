using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Selectable))]
public class SettingsUIInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("音效设置")]
    [Tooltip("悬停音效")]
    public AudioClip hoverSound;
    [Tooltip("交互/改变值时的音效")]
    public AudioClip interactSound;
    
    private AudioSource audioSource;
    private Selectable selectable;

    [Header("滑块特有效果 (仅Slider有效)")]
    [Tooltip("滑块悬停时的放大倍数")]
    public float handleHoverScale = 1.3f;
    [Tooltip("动画速度")]
    public float scaleSpeed = 10f;
    
    // 如果是Slider，记录其Handle
    private Slider slider;
    private RectTransform handleRect;
    private Vector3 originalHandleScale = Vector3.one;
    private bool isHoveredOrSelected = false;
    private Image fillImage;
    private Color originalFillColor;

    void Start()
    {
        selectable = GetComponent<Selectable>();
        
        // 尝试获取或添加 AudioSource
        // 为了避免每个 UI 都有 AudioSource，也可以从统管的 AudioManager 播放。
        // 这里简单处理为自带或寻找主相机的。
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            var audioObj = GameObject.Find("AudioManager") ?? Camera.main.gameObject;
            audioSource = audioObj.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = audioObj.AddComponent<AudioSource>();
        }

        // 判断是否是 Slider
        slider = GetComponent<Slider>();
        if (slider != null)
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
            if (slider.handleRect != null)
            {
                handleRect = slider.handleRect;
                originalHandleScale = handleRect.localScale;
            }
            if (slider.fillRect != null)
            {
                fillImage = slider.fillRect.GetComponent<Image>();
                if (fillImage != null) originalFillColor = fillImage.color;
            }
        }

        // 判断是否是 Toggle
        var toggle = GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        // 判断是否是 Dropdown
        var dropdown = GetComponent<TMPro.TMP_Dropdown>();
        if (dropdown != null)
        {
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    void Update()
    {
        if (slider != null && handleRect != null)
        {
            // 对于被选中或悬停的 Slider Handle 放大
            bool active = isHoveredOrSelected || (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);
            
            Vector3 targetScale = active ? originalHandleScale * handleHoverScale : originalHandleScale;
            handleRect.localScale = Vector3.Lerp(handleRect.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);

            if (fillImage != null)
            {
                Color targetColor = active ? originalFillColor : new Color(originalFillColor.r, originalFillColor.g, originalFillColor.b, 0.7f);
                fillImage.color = Color.Lerp(fillImage.color, targetColor, Time.unscaledDeltaTime * scaleSpeed);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHoveredOrSelected = true;
        PlaySound(hoverSound);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHoveredOrSelected = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        isHoveredOrSelected = true;
        PlaySound(hoverSound);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHoveredOrSelected = false;
    }

    private void OnSliderValueChanged(float val)
    {
        // 避免拖拽时每一帧都放声音，最好能限制频率，这里简单处理
        PlaySound(interactSound);
    }

    private void OnToggleValueChanged(bool val)
    {
        PlaySound(interactSound);
    }

    private void OnDropdownValueChanged(int val)
    {
        PlaySound(interactSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
