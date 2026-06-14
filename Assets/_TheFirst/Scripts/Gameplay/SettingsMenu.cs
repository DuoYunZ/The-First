using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Components")]
    public AudioMixer mainMixer;

    [Header("Tabs")]
    public GameObject generalSettingsPanel;
    public GameObject keyBindingPanel;
    public Button generalTabButton;
    public Button keyBindingTabButton;

    [Header("Tab Style")]
    public Color tabActiveColor = new Color(1f, 1f, 1f, 1f);
    public Color tabInactiveColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    [Header("Audio")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Display")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Language")]
    public TMP_Dropdown languageDropdown;

    [Header("Controller Navigation")]
    public Button backButton;
    public bool enableGamepadTabShortcuts = true;

    private const string MASTER_VOL_KEY = "MasterVolume";
    private const string BGM_VOL_KEY = "BGMVolume";
    private const string SFX_VOL_KEY = "SFXVolume";

    private Resolution[] resolutions;
    private InputAction previousTabAction;
    private InputAction nextTabAction;
    private InputAction cancelAction;
    private int currentTabIndex;
    private bool initialized;

    private void Awake()
    {
        CreateInputActions();
        FindBackButtonIfNeeded();
    }

    private void OnEnable()
    {
        EnableInputActions();

        if (initialized)
        {
            StartCoroutine(SelectDefaultControlNextFrame());
        }
    }

    private void OnDisable()
    {
        DisableInputActions();
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        previousTabAction?.Dispose();
        nextTabAction?.Dispose();
        cancelAction?.Dispose();
    }

    private void Start()
    {
        LoadVolumeSettings();

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        InitResolutionSettings();
        InitLanguageSettings();
        InitTabButtons();

        initialized = true;
        SwitchToTab(0, false);
        StartCoroutine(SelectDefaultControlNextFrame());
    }

    private void Update()
    {
        if (!initialized
            || KeyBindingManager.Instance != null
            && (KeyBindingManager.Instance.IsRebinding || KeyBindingManager.Instance.RebindEndedThisFrame))
        {
            return;
        }

        KeyBindingManager.UpdateActiveDeviceFromCurrentInput();

        if (enableGamepadTabShortcuts)
        {
            if (previousTabAction.WasPressedThisFrame())
            {
                SwitchToTab(currentTabIndex == 0 ? 1 : currentTabIndex - 1);
            }
            else if (nextTabAction.WasPressedThisFrame())
            {
                SwitchToTab(currentTabIndex == 1 ? 0 : currentTabIndex + 1);
            }
        }

        if (cancelAction.WasPressedThisFrame())
        {
            InvokeBack();
        }
    }

    private void CreateInputActions()
    {
        previousTabAction = new InputAction("SettingsPreviousTab", InputActionType.Button);
        previousTabAction.AddBinding("<Gamepad>/leftShoulder");

        nextTabAction = new InputAction("SettingsNextTab", InputActionType.Button);
        nextTabAction.AddBinding("<Gamepad>/rightShoulder");

        cancelAction = new InputAction("SettingsCancel", InputActionType.Button);
        cancelAction.AddBinding("<Keyboard>/escape");
        cancelAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void EnableInputActions()
    {
        previousTabAction?.Enable();
        nextTabAction?.Enable();
        cancelAction?.Enable();
    }

    private void DisableInputActions()
    {
        previousTabAction?.Disable();
        nextTabAction?.Disable();
        cancelAction?.Disable();
    }

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

    public void SwitchToTab(int tabIndex)
    {
        SwitchToTab(tabIndex, true);
    }

    private void SwitchToTab(int tabIndex, bool selectDefault)
    {
        currentTabIndex = Mathf.Clamp(tabIndex, 0, 1);

        if (generalSettingsPanel != null)
            generalSettingsPanel.SetActive(currentTabIndex == 0);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(currentTabIndex == 1);

        UpdateTabButtonStyle(generalTabButton, currentTabIndex == 0);
        UpdateTabButtonStyle(keyBindingTabButton, currentTabIndex == 1);

        if (selectDefault)
        {
            StartCoroutine(SelectDefaultControlNextFrame());
        }
    }

    private void UpdateTabButtonStyle(Button button, bool isActive)
    {
        if (button == null) return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = isActive ? tabActiveColor : tabInactiveColor;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = isActive ? 1f : 0.3f;
            image.color = color;
        }
    }

    public void SelectDefaultControl()
    {
        Selectable selectable = GetDefaultSelectable();
        if (selectable == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }

    private IEnumerator SelectDefaultControlNextFrame()
    {
        yield return null;
        SelectDefaultControl();
    }

    private Selectable GetDefaultSelectable()
    {
        if (currentTabIndex == 1 && keyBindingPanel != null)
        {
            KeyBindingUI keyBindingUI = keyBindingPanel.GetComponent<KeyBindingUI>();
            Selectable keyBindingSelectable = keyBindingUI != null ? keyBindingUI.GetFirstSelectable() : null;
            if (IsUsableSelectable(keyBindingSelectable))
            {
                return keyBindingSelectable;
            }
        }

        GameObject activePanel = currentTabIndex == 1 ? keyBindingPanel : generalSettingsPanel;
        if (activePanel != null)
        {
            foreach (Selectable selectable in activePanel.GetComponentsInChildren<Selectable>(false))
            {
                if (IsUsableSelectable(selectable))
                {
                    return selectable;
                }
            }
        }

        if (IsUsableSelectable(currentTabIndex == 1 ? keyBindingTabButton : generalTabButton))
        {
            return currentTabIndex == 1 ? keyBindingTabButton : generalTabButton;
        }

        foreach (Selectable selectable in GetComponentsInChildren<Selectable>(false))
        {
            if (IsUsableSelectable(selectable))
            {
                return selectable;
            }
        }

        return null;
    }

    private bool IsUsableSelectable(Selectable selectable)
    {
        return selectable != null
            && selectable.gameObject.activeInHierarchy
            && selectable.IsInteractable()
            && !(selectable is Scrollbar);
    }

    private void FindBackButtonIfNeeded()
    {
        if (backButton != null) return;

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            LocalizedText localizedText = button.GetComponentInChildren<LocalizedText>(true);
            if (localizedText != null && localizedText.localizationKey == "ui.back")
            {
                backButton = button;
                return;
            }
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            string buttonName = button.gameObject.name;
            if (buttonName.Contains("Back") || buttonName.Contains("Return") || buttonName.Contains("Close"))
            {
                backButton = button;
                return;
            }
        }
    }

    private void InvokeBack()
    {
        FindBackButtonIfNeeded();

        if (backButton != null && backButton.gameObject.activeInHierarchy && backButton.IsInteractable())
        {
            backButton.onClick.Invoke();
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
        }

        CombatUIManager combatUI = Object.FindFirstObjectByType<CombatUIManager>();
        if (combatUI != null && combatUI.settingsPanel == gameObject)
        {
            combatUI.CloseSettings();
            return;
        }

        MainMenuManager mainMenu = Object.FindFirstObjectByType<MainMenuManager>();
        if (mainMenu != null && mainMenu.settingsPanel == gameObject)
        {
            mainMenu.OnBackFromSettingsClicked();
            return;
        }

        gameObject.SetActive(false);
    }

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
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutions == null || resolutionIndex < 0 || resolutionIndex >= resolutions.Length)
        {
            return;
        }

        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetMasterVolume(float value)
    {
        SetMixerVolume("MasterVolume", value);
        PlayerPrefs.SetFloat(MASTER_VOL_KEY, value);
    }

    public void SetBGMVolume(float value)
    {
        SetMixerVolume("BGMVolume", value);
        PlayerPrefs.SetFloat(BGM_VOL_KEY, value);
    }

    public void SetSFXVolume(float value)
    {
        SetMixerVolume("SFXVolume", value);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, value);
    }

    private void SetMixerVolume(string parameterName, float value)
    {
        if (mainMixer == null) return;

        float volumeInDb = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20;
        mainMixer.SetFloat(parameterName, volumeInDb);
    }

    private void LoadVolumeSettings()
    {
        float masterValue = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1f);
        float bgmValue = PlayerPrefs.GetFloat(BGM_VOL_KEY, 1f);
        float sfxValue = PlayerPrefs.GetFloat(SFX_VOL_KEY, 1f);

        if (masterVolumeSlider != null) masterVolumeSlider.value = masterValue;
        if (bgmVolumeSlider != null) bgmVolumeSlider.value = bgmValue;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxValue;

        SetMasterVolume(masterValue);
        SetBGMVolume(bgmValue);
        SetSFXVolume(sfxValue);
    }

    private void InitLanguageSettings()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new List<string> { "中文", "English" });
        languageDropdown.value = LocalizationManager.GetCurrentLanguageIndex();
        languageDropdown.RefreshShownValue();
        languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    public void SetLanguage(int index)
    {
        LocalizationManager.SetLanguageByIndex(index);
    }
}
