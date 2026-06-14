using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    private enum SavePanelMode { Load, NewGame }

    [Header("Panels")]
    public GameObject settingsPanel;
    public GameObject mainPanel;

    [Header("Main Menu Entries")]
    public Button continueButton;
    public Button loadSaveButton;
    public Button newGameButton;
    public bool autoCreateSaveMenuEntries = true;
    public Vector2 continueButtonPosition = new Vector2(960f, -832f);
    public Vector2 loadSaveButtonPosition = new Vector2(960f, -907f);
    public Vector2 newGameButtonPosition = new Vector2(960f, -982f);
    public Vector2 settingsButtonPosition = new Vector2(960f, -1057f);
    public Vector2 quitButtonPosition = new Vector2(960f, -1132f);

    [Header("Save Slots")]
    public GameObject saveArchivePanelPrefab;
    public GameObject saveArchivePanel;
    public TextMeshProUGUI saveArchiveInfoText;

    [Header("Social Links")]
    [Tooltip("Discord server invite URL")]
    public string discordUrl = "https://discord.gg/你的邀请码";
    [Tooltip("QQ group URL")]
    public string qqGroupUrl = "https://qm.qq.com/q/你的群号";

    private TextMeshProUGUI savePanelTitleText;
    private TextMeshProUGUI savePanelHintText;
    private TextMeshProUGUI[] savePanelTitleTexts = new TextMeshProUGUI[0];
    private TextMeshProUGUI[] savePanelHintTexts = new TextMeshProUGUI[0];
    private readonly Button[] slotButtons = new Button[PlayerProgressManager.SaveSlotCount];
    private readonly TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[PlayerProgressManager.SaveSlotCount];
    private readonly TextMeshProUGUI[][] slotLabelGroups = new TextMeshProUGUI[PlayerProgressManager.SaveSlotCount][];
    private readonly Button[] slotDeleteButtons = new Button[PlayerProgressManager.SaveSlotCount];
    private readonly TextMeshProUGUI[] slotDeleteLabels = new TextMeshProUGUI[PlayerProgressManager.SaveSlotCount];
    private readonly TextMeshProUGUI[][] slotDeleteLabelGroups = new TextMeshProUGUI[PlayerProgressManager.SaveSlotCount][];
    private TextMeshProUGUI[] backButtonLabelTexts = new TextMeshProUGUI[0];
    private SavePanelMode currentSavePanelMode = SavePanelMode.Load;
    private Button settingsButton;
    private Button quitButton;
    private bool cachedHasSaveAvailable;
    private const string SaveArchivePanelResourcePath = "UI/SaveArchivePanel";

    private void Awake()
    {
        EnsureMainMenuEntries();
        EnsureSaveArchivePanel();
    }

    private void Start()
    {
        RefreshMainMenuText();
        RefreshMainMenuAvailability();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (saveArchivePanel != null) saveArchivePanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void LateUpdate()
    {
        if (mainPanel == null || !mainPanel.activeInHierarchy) return;
        if (settingsPanel != null && settingsPanel.activeInHierarchy) return;
        if (saveArchivePanel != null && saveArchivePanel.activeInHierarchy) return;

        ApplyMainMenuAvailability(cachedHasSaveAvailable, false);
    }

    public void OnStartGameClicked()
    {
        OnContinueGameClicked();
    }

    public void OnContinueGameClicked()
    {
        PlayerProgressManager progress = PlayerProgressManager.Instance;
        if (progress != null)
        {
            int latestSlot = progress.GetLatestSaveSlot();
            if (latestSlot > 0)
            {
                progress.LoadGameFromSlot(latestSlot);
            }
            else
            {
                progress.StartNewGameInSlot(1);
            }
        }

        LoadHubScene();
    }

    public void OnLoadSaveClicked()
    {
        if (!HasAnySaveAvailable())
        {
            RefreshMainMenuAvailability();
            return;
        }

        ShowSaveArchivePanel(SavePanelMode.Load);
    }

    public void OnNewGameClicked()
    {
        ShowSaveArchivePanel(SavePanelMode.NewGame);
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null && mainPanel != null)
        {
            settingsPanel.SetActive(true);
            mainPanel.SetActive(false);

            SettingsMenu settingsMenu = settingsPanel.GetComponent<SettingsMenu>();
            if (settingsMenu != null)
            {
                settingsMenu.SelectDefaultControl();
            }
        }
    }

    public void OnBackFromSettingsClicked()
    {
        if (settingsPanel != null && mainPanel != null)
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
            RefreshMainMenuAvailability();
        }
    }

    public void OnSaveArchiveClicked()
    {
        OnLoadSaveClicked();
    }

    public void OnSaveArchiveBackClicked()
    {
        if (saveArchivePanel != null) saveArchivePanel.SetActive(false);
        RefreshMainMenuAvailability();
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    public void OnManualSaveClicked()
    {
        PlayerProgressManager.Instance?.SaveGame();
        RefreshMainMenuAvailability();
        RefreshSaveSlotViews();
    }

    public void OnQuitGameClicked()
    {
        PlayerProgressManager progress = PlayerProgressManager.Instance;
        if (progress != null && progress.HasSaveInSlot(progress.ActiveSaveSlot))
        {
            progress.SaveGame();
        }

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnClearSaveClicked()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.ClearSaveData();
        }
        else
        {
            Debug.LogError("Could not find PlayerProgressManager instance.");
        }

        RefreshSaveSlotViews();
        RefreshMainMenuAvailability();
    }

    public void OnDiscordClicked()
    {
        if (!string.IsNullOrEmpty(discordUrl))
        {
            Application.OpenURL(discordUrl);
        }
    }

    public void OnQQGroupClicked()
    {
        if (!string.IsNullOrEmpty(qqGroupUrl))
        {
            Application.OpenURL(qqGroupUrl);
        }
    }

    private void LoadHubScene()
    {
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("HubScene");
        else SceneManager.LoadScene("HubScene");
    }

    private void EnsureMainMenuEntries()
    {
        if (!autoCreateSaveMenuEntries || mainPanel == null) return;

        Button template = FindTemplateMenuButton();
        if (template == null) return;

        continueButton = continueButton != null ? continueButton : FindButtonByLocalizationKey("ui.continue_game");
        continueButton = continueButton != null ? continueButton : FindButtonByLocalizationKey("ui.start_game");
        continueButton = continueButton != null ? continueButton : FindButtonByName("ContinueGameButton");
        continueButton = continueButton != null ? continueButton : FindButtonByName("StartGameButton");
        if (continueButton == null) continueButton = template;

        loadSaveButton = FindOrCreateMenuButton("LoadSaveButton", template);
        newGameButton = FindOrCreateMenuButton("NewGameButton", template);
        settingsButton = FindButtonByLocalizationKey("ui.settings");
        quitButton = FindButtonByLocalizationKey("ui.quit_game");

        ConfigureMenuButton(continueButton, "ContinueGameButton", "ui.continue_game", GetContinueText(), continueButtonPosition, OnContinueGameClicked);
        ConfigureMenuButton(loadSaveButton, "LoadSaveButton", "ui.load_save", GetLoadSaveText(), loadSaveButtonPosition, OnLoadSaveClicked);
        ConfigureMenuButton(newGameButton, "NewGameButton", "ui.new_game", GetNewGameText(), newGameButtonPosition, OnNewGameClicked);

        MoveButton(settingsButton, settingsButtonPosition);
        MoveButton(quitButton, quitButtonPosition);
        RebuildMainMenuSelection(true);
    }

    private void RefreshMainMenuAvailability()
    {
        cachedHasSaveAvailable = HasAnySaveAvailable();
        ApplyMainMenuAvailability(cachedHasSaveAvailable, true);
    }

    private void ApplyMainMenuAvailability(bool hasSave, bool rebuildSelection)
    {
        settingsButton = settingsButton != null ? settingsButton : FindButtonByLocalizationKey("ui.settings");
        quitButton = quitButton != null ? quitButton : FindButtonByLocalizationKey("ui.quit_game");

        SetButtonVisible(continueButton, hasSave);
        SetButtonVisible(loadSaveButton, hasSave);
        SetButtonVisible(newGameButton, true);
        SetButtonVisible(settingsButton, true);
        SetButtonVisible(quitButton, true);

        if (hasSave)
        {
            MoveButton(continueButton, continueButtonPosition);
            MoveButton(loadSaveButton, loadSaveButtonPosition);
            MoveButton(newGameButton, newGameButtonPosition);
            MoveButton(settingsButton, settingsButtonPosition);
            MoveButton(quitButton, quitButtonPosition);
        }
        else
        {
            MoveButton(newGameButton, continueButtonPosition);
            MoveButton(settingsButton, loadSaveButtonPosition);
            MoveButton(quitButton, newGameButtonPosition);
        }

        if (rebuildSelection)
        {
            RebuildMainMenuSelection(hasSave);
        }
    }

    private bool HasAnySaveAvailable()
    {
        return PlayerProgressManager.Instance != null && PlayerProgressManager.Instance.HasAnySave();
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        button.interactable = visible;

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic != null) graphic.raycastTarget = visible;
        }
    }

    private void RebuildMainMenuSelection(bool hasSave)
    {
        UISelectionGroup selectionGroup = GetComponent<UISelectionGroup>();
        if (selectionGroup == null || selectionGroup.menuItems == null) return;

        List<Selectable> items = new List<Selectable>();
        if (hasSave)
        {
            AddMenuSelectable(items, continueButton);
            AddMenuSelectable(items, loadSaveButton);
        }

        AddMenuSelectable(items, newGameButton);
        AddMenuSelectable(items, settingsButton);
        AddMenuSelectable(items, quitButton);
        selectionGroup.SetMenuItems(items);
        selectionGroup.SelectFirstItem();
    }

    private Button FindTemplateMenuButton()
    {
        UISelectionGroup selectionGroup = GetComponent<UISelectionGroup>();
        if (selectionGroup != null && selectionGroup.menuItems != null)
        {
            foreach (Selectable selectable in selectionGroup.menuItems)
            {
                if (selectable is Button button && button != null) return button;
            }
        }

        return mainPanel.GetComponentInChildren<Button>(true);
    }

    private Button FindButtonByLocalizationKey(string localizationKey)
    {
        if (mainPanel == null || string.IsNullOrEmpty(localizationKey)) return null;

        UISelectionGroup selectionGroup = GetComponent<UISelectionGroup>();
        if (selectionGroup != null && selectionGroup.menuItems != null)
        {
            foreach (Selectable selectable in selectionGroup.menuItems)
            {
                Button button = selectable as Button;
                if (button == null) continue;

                LocalizedText localizedText = GetButtonLocalizedText(button);
                if (localizedText != null && localizedText.localizationKey == localizationKey)
                {
                    return button;
                }
            }
        }

        Button[] buttons = mainPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            LocalizedText localizedText = GetButtonLocalizedText(button);
            if (localizedText != null && localizedText.localizationKey == localizationKey)
            {
                return button;
            }
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        if (mainPanel == null || string.IsNullOrEmpty(objectName)) return null;

        Transform found = FindDeepChild(mainPanel.transform, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private Button FindOrCreateMenuButton(string objectName, Button template)
    {
        Transform existing = mainPanel.transform.Find(objectName);
        if (existing == null) existing = FindDeepChild(mainPanel.transform, objectName);
        if (existing != null)
        {
            Button existingButton = existing.GetComponent<Button>();
            if (existingButton != null) return existingButton;
        }

        GameObject buttonObject = Instantiate(template.gameObject, mainPanel.transform);
        buttonObject.name = objectName;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (rect != null) rect.localScale = Vector3.one;
        return buttonObject.GetComponent<Button>();
    }

    private void ConfigureMenuButton(Button button, string objectName, string localizationKey, string fallbackText, Vector2 position, UnityEngine.Events.UnityAction clickAction)
    {
        if (button == null) return;

        button.gameObject.name = objectName;
        MoveButton(button, position);
        SetButtonText(button, localizationKey, fallbackText);
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(clickAction);
    }

    private void MoveButton(Button button, Vector2 anchoredPosition)
    {
        if (button == null) return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
        }
    }

    private void SetButtonText(Button button, string localizationKey, string fallbackText)
    {
        if (button == null) return;

        LocalizedText[] localizedTexts = button.GetComponentsInChildren<LocalizedText>(true);
        foreach (LocalizedText localizedText in localizedTexts)
        {
            if (localizedText == null) continue;
            localizedText.localizationKey = localizationKey;
            localizedText.UpdateText();
        }

        TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label != null) label.text = fallbackText;
        }
    }

    private LocalizedText GetButtonLocalizedText(Button button)
    {
        if (button == null) return null;

        LocalizedText localizedText = button.GetComponent<LocalizedText>();
        if (localizedText != null) return localizedText;

        return button.GetComponentInChildren<LocalizedText>(true);
    }

    private TextMeshProUGUI GetButtonLabel(Button button)
    {
        if (button == null) return null;

        TextMeshProUGUI label = button.GetComponent<TextMeshProUGUI>();
        if (label != null) return label;

        return button.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void AddMenuSelectable(UISelectionGroup selectionGroup, Button button)
    {
        if (selectionGroup == null || selectionGroup.menuItems == null || button == null) return;
        if (!selectionGroup.menuItems.Contains(button)) selectionGroup.menuItems.Add(button);
    }

    private void AddMenuSelectable(List<Selectable> items, Button button)
    {
        if (items == null || button == null || !button.gameObject.activeSelf) return;
        if (!items.Contains(button)) items.Add(button);
    }

    private void RefreshMainMenuText()
    {
        SetButtonText(continueButton, "ui.continue_game", GetContinueText());
        SetButtonText(loadSaveButton, "ui.load_save", GetLoadSaveText());
        SetButtonText(newGameButton, "ui.new_game", GetNewGameText());
    }

    private void EnsureSaveArchivePanel()
    {
        if (saveArchivePanel == null)
        {
            Transform parent = mainPanel != null && mainPanel.transform.parent != null ? mainPanel.transform.parent : transform;
            GameObject prefab = saveArchivePanelPrefab != null
                ? saveArchivePanelPrefab
                : Resources.Load<GameObject>(SaveArchivePanelResourcePath);

            if (prefab == null)
            {
                Debug.LogError($"Save archive panel prefab not found. Assign one or create Resources/{SaveArchivePanelResourcePath}.prefab.");
                return;
            }

            saveArchivePanel = Instantiate(prefab, parent, false);
            saveArchivePanel.name = prefab.name;
        }

        BindSaveArchivePanelReferences();
        saveArchivePanel.SetActive(false);
    }

    private void BindSaveArchivePanelReferences()
    {
        if (saveArchivePanel == null) return;

        savePanelTitleTexts = FindPanelTexts("TitleGroup", "Title");
        savePanelHintTexts = FindPanelTexts("HintGroup", "Hint");
        savePanelTitleText = FirstText(savePanelTitleTexts);
        savePanelHintText = FirstText(savePanelHintTexts);
        saveArchiveInfoText = savePanelHintText;

        for (int i = 0; i < PlayerProgressManager.SaveSlotCount; i++)
        {
            int slotIndex = i + 1;

            slotButtons[i] = FindPanelButton($"Slot{slotIndex}Button");
            slotLabelGroups[i] = FindButtonTexts(slotButtons[i]);
            slotLabels[i] = FirstText(slotLabelGroups[i]);
            ConfigurePanelButton(slotButtons[i], () => OnSaveSlotClicked(slotIndex));

            slotDeleteButtons[i] = FindPanelButton($"DeleteSlot{slotIndex}Button");
            slotDeleteLabelGroups[i] = FindButtonTexts(slotDeleteButtons[i]);
            slotDeleteLabels[i] = FirstText(slotDeleteLabelGroups[i]);
            ConfigurePanelButton(slotDeleteButtons[i], () => OnDeleteSaveSlotClicked(slotIndex));
        }

        Button backButton = FindPanelButton("BackButton");
        backButtonLabelTexts = FindButtonTexts(backButton);
        ConfigurePanelButton(backButton, OnSaveArchiveBackClicked);
    }

    private Button FindPanelButton(string objectName)
    {
        if (saveArchivePanel == null || string.IsNullOrEmpty(objectName)) return null;

        Transform target = FindDeepChild(saveArchivePanel.transform, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private TextMeshProUGUI[] FindPanelTexts(params string[] objectNames)
    {
        if (saveArchivePanel == null || objectNames == null) return new TextMeshProUGUI[0];

        foreach (string objectName in objectNames)
        {
            Transform target = FindDeepChild(saveArchivePanel.transform, objectName);
            if (target == null) continue;

            TextMeshProUGUI[] texts = target.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0) return texts;
        }

        return new TextMeshProUGUI[0];
    }

    private TextMeshProUGUI[] FindButtonTexts(Button button)
    {
        if (button == null) return new TextMeshProUGUI[0];

        Transform labelGroup = FindDeepChild(button.transform, "LabelGroup");
        if (labelGroup != null)
        {
            TextMeshProUGUI[] groupTexts = labelGroup.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (groupTexts.Length > 0) return groupTexts;
        }

        Transform label = FindDeepChild(button.transform, "Label");
        if (label != null)
        {
            TextMeshProUGUI[] labelTexts = label.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (labelTexts.Length > 0) return labelTexts;
        }

        return button.GetComponentsInChildren<TextMeshProUGUI>(true);
    }

    private Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null || string.IsNullOrEmpty(objectName)) return null;
        if (parent.name == objectName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), objectName);
            if (result != null) return result;
        }

        return null;
    }

    private void ConfigurePanelButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null) return;

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(callback);
    }

    private TextMeshProUGUI FirstText(TextMeshProUGUI[] texts)
    {
        return texts != null && texts.Length > 0 ? texts[0] : null;
    }

    private void SetTexts(TextMeshProUGUI[] texts, string value)
    {
        if (texts == null) return;

        foreach (TextMeshProUGUI text in texts)
        {
            if (text != null) text.text = value;
        }
    }

    private void ShowSaveArchivePanel(SavePanelMode mode)
    {
        currentSavePanelMode = mode;
        EnsureSaveArchivePanel();
        RefreshSaveSlotViews();

        if (saveArchivePanel != null && mainPanel != null)
        {
            saveArchivePanel.SetActive(true);
            mainPanel.SetActive(false);
        }
    }

    private void RefreshSaveSlotViews()
    {
        if (saveArchivePanel == null) return;

        bool english = LocalizationManager.CurrentLanguage == SystemLanguage.English;
        SetTexts(savePanelTitleTexts, currentSavePanelMode == SavePanelMode.Load
            ? english ? "Load Save" : "读取存档"
            : english ? "New Game" : "新游戏");

        SetTexts(savePanelHintTexts, currentSavePanelMode == SavePanelMode.Load
            ? english ? "Choose a saved slot." : "选择一个已有存档。"
            : english ? "Choose a slot. Existing data in that slot will be replaced." : "选择槽位开始新游戏，已有记录会被覆盖。");
        SetTexts(backButtonLabelTexts, GetBackText());

        for (int i = 0; i < PlayerProgressManager.SaveSlotCount; i++)
        {
            int slotIndex = i + 1;
            PlayerProgressManager.SaveSlotInfo info = PlayerProgressManager.Instance != null
                ? PlayerProgressManager.Instance.GetSaveSlotInfo(slotIndex)
                : new PlayerProgressManager.SaveSlotInfo { slotIndex = slotIndex };

            SetTexts(slotLabelGroups[i], BuildSlotLabel(info));

            bool canUseSlot = currentSavePanelMode == SavePanelMode.NewGame || info.hasSave;
            if (slotButtons[i] != null)
            {
                slotButtons[i].interactable = canUseSlot;
                Image image = slotButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = info.hasSave
                        ? new Color(0.96f, 0.74f, 0.28f, 0.95f)
                        : new Color(0.34f, 0.31f, 0.27f, 0.86f);
                }
            }

            SetTexts(slotDeleteLabelGroups[i], GetDeleteText());
            if (slotDeleteButtons[i] != null)
            {
                slotDeleteButtons[i].interactable = info.hasSave;
                Image image = slotDeleteButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = info.hasSave
                        ? new Color(0.72f, 0.22f, 0.16f, 0.95f)
                        : new Color(0.24f, 0.22f, 0.2f, 0.8f);
                }
            }
        }
    }

    private string BuildSlotLabel(PlayerProgressManager.SaveSlotInfo info)
    {
        bool english = LocalizationManager.CurrentLanguage == SystemLanguage.English;
        int slotIndex = info != null ? info.slotIndex : 0;
        if (info == null || !info.hasSave)
        {
            return english ? $"Slot {slotIndex}\nEmpty" : $"槽位 {slotIndex}\n空";
        }

        string timeText = info.lastSaveTimeLocal.ToString("yyyy-MM-dd HH:mm");
        string preview = string.IsNullOrEmpty(info.unlockedPreview) ? "" : $"\n{info.unlockedPreview}";
        return english
            ? $"Slot {slotIndex}\nTime: {timeText}\nUnlocked: {info.unlockedCount}{preview}"
            : $"槽位 {slotIndex}\n时间：{timeText}\n解锁：{info.unlockedCount} 项{preview}";
    }

    private void OnSaveSlotClicked(int slotIndex)
    {
        PlayerProgressManager progress = PlayerProgressManager.Instance;
        if (progress == null)
        {
            LoadHubScene();
            return;
        }

        if (currentSavePanelMode == SavePanelMode.Load)
        {
            if (!progress.HasSaveInSlot(slotIndex)) return;
            progress.LoadGameFromSlot(slotIndex);
        }
        else
        {
            progress.StartNewGameInSlot(slotIndex);
        }

        LoadHubScene();
    }

    private void OnDeleteSaveSlotClicked(int slotIndex)
    {
        PlayerProgressManager progress = PlayerProgressManager.Instance;
        if (progress == null || !progress.HasSaveInSlot(slotIndex)) return;

        progress.DeleteSaveInSlot(slotIndex);
        RefreshSaveSlotViews();
        RefreshMainMenuAvailability();
    }

    private string GetContinueText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Continue" : "继续游戏";
    }

    private string GetLoadSaveText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Load Save" : "读取存档";
    }

    private string GetNewGameText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "New Game" : "新游戏";
    }

    private string GetBackText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Back" : "返回";
    }

    private string GetDeleteText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Delete" : "删除";
    }
}
