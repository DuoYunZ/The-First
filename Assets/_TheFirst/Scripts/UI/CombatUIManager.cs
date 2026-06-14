using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // <-- 1. 引入新的 Input System 命名空间
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CombatUIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    [Tooltip("在按下ESC时要显示的暂停面板")]
    public GameObject pausePanel;
    
    [Tooltip("暂停菜单内的设置面板")]
    public GameObject settingsPanel;

    [Tooltip("成就面板。为空时会在运行时创建。")]
    public AchievementPanelUI achievementPanel;

    [Tooltip("Achievement panel prefab. If empty, Resources/UI/AchievementPanel will be loaded.")]
    public AchievementPanelUI achievementPanelPrefab;

    [SerializeField] private string achievementPanelResourcePath = "UI/AchievementPanel";

    private bool isPaused = false;
    private bool isSettingsOpen = false;
    private InputAction pauseAction; // <-- 2. 创建一个 InputAction 变量
    private Button achievementButton;
    private UISelectionGroup pauseSelectionGroup;

    void Awake()
    {
        // 3. 初始化这个 Action 并绑定到键盘的 Escape 键
        pauseAction = new InputAction("Pause");
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
    }

    private void OnEnable()
    {
        // 4. 激活 Action
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        // 5. 禁用 Action，防止在场景卸载后还占用资源
        pauseAction.Disable();
    }


    void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        else
        {
            Debug.LogError("暂停面板 (PausePanel) 未在 CombatUIManager 中分配!", this);
        }
        
        // 初始化设置面板为隐藏状态
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        EnsureAchievementPanel();
        EnsureAchievementButton();
        RefreshPauseSelectionGroup();
    }

    /// <summary>
    /// 当游戏窗口失去/获得焦点时调用（Alt+Tab、切换应用等）
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !isPaused && !IsCodexPanelOpen() && !IsAchievementPanelOpen())
        {
            // 失去焦点时自动暂停
            PauseGame();
        }
        else if (hasFocus && isPaused)
        {
            // 获得焦点时，即使仍在暂停状态也要防止物理追赶
            // 通过临时极低的 maximumDeltaTime 防止 FixedUpdate 积压
            Time.maximumDeltaTime = Time.fixedDeltaTime;
        }
    }

    void Update()
    {
        if (KeyBindingManager.Instance != null
            && (KeyBindingManager.Instance.IsRebinding || KeyBindingManager.Instance.RebindEndedThisFrame))
        {
            return;
        }

        // 6. 使用新的方式检测按键
        // .triggered 相当于旧的 GetButtonDown
        if (pauseAction.triggered)
        {
            if (IsAchievementPanelOpen())
            {
                CloseAchievements();
                return;
            }

            if (IsCodexPanelOpen())
            {
                UIManager.Instance.skillTreeUIManager.ClosePanel();
                return;
            }

            // 如果设置面板打开，则关闭设置面板
            if (isSettingsOpen)
            {
                CloseSettings();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    private bool IsCodexPanelOpen()
    {
        return UIManager.Instance != null
            && UIManager.Instance.skillTreeUIManager != null
            && UIManager.Instance.skillTreeUIManager.IsPanelOpen();
    }

    private bool IsAchievementPanelOpen()
    {
        return achievementPanel != null && achievementPanel.IsOpen;
    }

    // 暂停游戏
    public void PauseGame()
    {
        if (pausePanel == null) return;

        EnsureAchievementPanel();
        EnsureAchievementButton();
        RefreshPauseSelectionGroup();

        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        // 暂停物理模拟，防止恢复时大量 FixedUpdate 追赶
        Physics.simulationMode = SimulationMode.Script;

        var selectable = pausePanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
        if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    // 继续游戏 (这个方法现在可以被按钮和ESC键共用)
    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        isSettingsOpen = false;
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (achievementPanel != null) achievementPanel.Hide();

        // 恢复物理模拟
        Physics.simulationMode = SimulationMode.FixedUpdate;
        // 限制第一帧的 deltaTime，防止所有系统一次性处理大量累积时间
        Time.maximumDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = 1f;

        // 下一帧恢复正常的 maximumDeltaTime
        StartCoroutine(RestoreMaxDeltaTime());
    }

    /// <summary>
    /// 延迟一帧后恢复 maximumDeltaTime 到正常值
    /// </summary>
    private System.Collections.IEnumerator RestoreMaxDeltaTime()
    {
        yield return null; // 等一帧
        Time.maximumDeltaTime = 0.3333333f; // Unity 默认值
    }

    // 打开设置面板
    public void OpenSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(true);
        isSettingsOpen = true;

        SettingsMenu settingsMenu = settingsPanel.GetComponent<SettingsMenu>();
        if (settingsMenu != null)
        {
            settingsMenu.SelectDefaultControl();
        }
        else
        {
            var selectable = settingsPanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
            if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }

    // 关闭设置面板
    public void CloseSettings()
    {
        if (settingsPanel == null) return;
        
        settingsPanel.SetActive(false);
        isSettingsOpen = false;

        if (pausePanel != null && pausePanel.activeInHierarchy)
        {
            var selectable = pausePanel.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
            if (selectable != null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }

    public void OpenAchievements()
    {
        EnsureAchievementPanel();
        if (achievementPanel == null)
        {
            Debug.LogWarning("[CombatUIManager] Achievement panel is missing and could not be created.");
            return;
        }

        isPaused = true;
        isSettingsOpen = false;
        Time.timeScale = 0f;
        Physics.simulationMode = SimulationMode.Script;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        achievementPanel.Show();
    }

    public void CloseAchievements()
    {
        if (achievementPanel != null)
        {
            achievementPanel.Hide();
        }

        if (isPaused && pausePanel != null)
        {
            pausePanel.SetActive(true);
            RefreshPauseSelectionGroup();
            SelectPauseButton(achievementButton);
        }
    }

    private void EnsureAchievementPanel()
    {
        if (achievementPanel != null)
        {
            achievementPanel.CloseRequested = CloseAchievements;
            return;
        }

        achievementPanel = GetComponentInChildren<AchievementPanelUI>(true);
        if (achievementPanel == null)
        {
            AchievementPanelUI[] panels = FindObjectsByType<AchievementPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (panels != null && panels.Length > 0)
            {
                achievementPanel = panels[0];
            }
        }

        if (achievementPanel == null)
        {
            Canvas canvas = pausePanel != null ? pausePanel.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null)
            {
                canvas = Object.FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return;
            }

            AchievementPanelUI prefab = achievementPanelPrefab != null
                ? achievementPanelPrefab
                : Resources.Load<AchievementPanelUI>(achievementPanelResourcePath);

            if (prefab != null)
            {
                achievementPanel = Instantiate(prefab, canvas.transform, false);
                achievementPanel.name = prefab.name;
                StretchToParent(achievementPanel.GetComponent<RectTransform>());
            }
            else
            {
                GameObject panelObject = new GameObject("Runtime_AchievementPanelHost", typeof(RectTransform));
                panelObject.transform.SetParent(canvas.transform, false);
                StretchToParent(panelObject.GetComponent<RectTransform>());
                achievementPanel = panelObject.AddComponent<AchievementPanelUI>();
            }
        }

        achievementPanel.CloseRequested = CloseAchievements;
        achievementPanel.Hide();
    }

    private void StretchToParent(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void EnsureAchievementButton()
    {
        if (pausePanel == null)
        {
            return;
        }

        Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);
        Button existingButton = FindAchievementButton(buttons);
        if (existingButton != null)
        {
            BindAchievementButton(existingButton);
            return;
        }

        Button template = FindPauseButtonTemplate(buttons);
        Transform parent = template != null && template.transform.parent != null
            ? template.transform.parent
            : pausePanel.transform;

        Button createdButton = CreatePauseAchievementButton(parent, template);
        if (template != null)
        {
            createdButton.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        }

        BindAchievementButton(createdButton);
    }

    private Button FindAchievementButton(Button[] buttons)
    {
        if (achievementButton != null && achievementButton.transform != null && achievementButton.transform.IsChildOf(pausePanel.transform))
        {
            return achievementButton;
        }

        if (buttons == null)
        {
            return null;
        }

        foreach (Button button in buttons)
        {
            if (IsAchievementButton(button))
            {
                return button;
            }
        }

        return null;
    }

    private bool IsAchievementButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        string objectName = button.gameObject.name;
        if (!string.IsNullOrEmpty(objectName)
            && (objectName.IndexOf("Achievement", System.StringComparison.OrdinalIgnoreCase) >= 0 || objectName.Contains("成就")))
        {
            return true;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        string labelText = label != null ? label.text : null;
        return !string.IsNullOrEmpty(labelText)
            && (labelText.IndexOf("Achievement", System.StringComparison.OrdinalIgnoreCase) >= 0 || labelText.Contains("成就"));
    }

    private void BindAchievementButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        achievementButton = button;
        achievementButton.onClick.RemoveListener(OpenAchievements);
        achievementButton.onClick.AddListener(OpenAchievements);
        SetButtonText(achievementButton, GetAchievementButtonText());
        SetLayerRecursive(achievementButton.gameObject, pausePanel.layer);
    }

    private void RefreshPauseSelectionGroup()
    {
        if (pausePanel == null)
        {
            return;
        }

        UISelectionGroup selectionGroup = GetPauseSelectionGroup();
        if (selectionGroup == null)
        {
            return;
        }

        List<Selectable> menuButtons = GetPauseMenuSelectables();
        if (menuButtons.Count == 0)
        {
            return;
        }

        selectionGroup.SetMenuItems(menuButtons);
    }

    private UISelectionGroup GetPauseSelectionGroup()
    {
        if (pauseSelectionGroup != null)
        {
            return pauseSelectionGroup;
        }

        pauseSelectionGroup = GetComponent<UISelectionGroup>();
        if (pauseSelectionGroup == null)
        {
            pauseSelectionGroup = GetComponentInParent<UISelectionGroup>();
        }

        return pauseSelectionGroup;
    }

    private List<Selectable> GetPauseMenuSelectables()
    {
        List<Selectable> directButtons = new List<Selectable>();
        List<Selectable> fallbackButtons = new List<Selectable>();
        Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null || !button.transform.IsChildOf(pausePanel.transform))
            {
                continue;
            }

            fallbackButtons.Add(button);
            if (button.transform.parent == pausePanel.transform)
            {
                directButtons.Add(button);
            }
        }

        List<Selectable> result = directButtons.Count > 0 ? directButtons : fallbackButtons;
        result.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
        return result;
    }

    private Button FindPauseButtonTemplate(Button[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
        {
            return null;
        }

        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name.Contains("Settings"))
            {
                return button;
            }
        }

        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name.Contains("Resume"))
            {
                return button;
            }
        }

        return buttons[0];
    }

    private Button CreatePauseAchievementButton(Transform parent, Button template)
    {
        GameObject buttonObject = new GameObject("Achievement Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        RectTransform templateRect = template != null ? template.GetComponent<RectTransform>() : null;
        if (templateRect != null)
        {
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.pivot = templateRect.pivot;
            rect.sizeDelta = templateRect.sizeDelta;

            if (parent != null && parent.GetComponent<LayoutGroup>() == null)
            {
                float yOffset = Mathf.Abs(templateRect.sizeDelta.y) + 12f;
                rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -yOffset);
            }
        }
        else
        {
            rect.sizeDelta = new Vector2(220f, 52f);
        }

        Image image = buttonObject.GetComponent<Image>();
        Image templateImage = template != null ? template.GetComponent<Image>() : null;
        if (templateImage != null)
        {
            image.sprite = templateImage.sprite;
            image.type = templateImage.type;
            image.color = templateImage.color;
            image.material = templateImage.material;
        }
        else
        {
            image.color = new Color(0.18f, 0.12f, 0.08f, 0.95f);
        }

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        LayoutElement templateLayout = template != null ? template.GetComponent<LayoutElement>() : null;
        if (templateLayout != null)
        {
            layout.minWidth = templateLayout.minWidth;
            layout.minHeight = templateLayout.minHeight;
            layout.preferredWidth = templateLayout.preferredWidth;
            layout.preferredHeight = templateLayout.preferredHeight;
            layout.flexibleWidth = templateLayout.flexibleWidth;
            layout.flexibleHeight = templateLayout.flexibleHeight;
        }
        else
        {
            layout.preferredWidth = 220f;
            layout.preferredHeight = 52f;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (template != null)
        {
            button.transition = template.transition;
            button.colors = template.colors;
            button.spriteState = template.spriteState;
            button.animationTriggers = template.animationTriggers;
            button.targetGraphic = image;
        }

        CreateButtonText(buttonObject.transform, template, GetAchievementButtonText());
        return button;
    }

    private void CreateButtonText(Transform parent, Button template, string text)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI templateLabel = template != null ? template.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (templateLabel != null)
        {
            label.font = templateLabel.font;
            label.fontMaterial = templateLabel.fontMaterial;
            label.fontSize = templateLabel.fontSize;
            label.fontStyle = templateLabel.fontStyle;
            label.color = templateLabel.color;
            label.alignment = templateLabel.alignment;
        }
        else
        {
            label.fontSize = 24f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
        }

        label.text = text;
        label.raycastTarget = true;
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = text;
            label.raycastTarget = true;
        }
    }

    private void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }
    }

    private string GetAchievementButtonText()
    {
        return LocalizationManager.CurrentLanguage == SystemLanguage.English ? "Achievements" : "成就";
    }

    private void SelectPauseButton(Selectable selectable)
    {
        if (selectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            return;
        }

        if (pausePanel == null || EventSystem.current == null)
        {
            return;
        }

        Selectable fallback = pausePanel.GetComponentInChildren<Selectable>(false);
        if (fallback != null)
        {
            EventSystem.current.SetSelectedGameObject(fallback.gameObject);
        }
    }

    // 返回枢纽场景
    public void ReturnToHub()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }
        else
        {
            Debug.LogWarning("未找到 PlayerProgressManager 实例，金币可能未被保存！");
        }

        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 恢复物理模拟
        isPaused = false;
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("HubScene");
        else SceneManager.LoadScene("HubScene");
    }

    // 返回主菜单
    public void ReturnToMainMenu()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }

        Time.timeScale = 1f;
        Physics.simulationMode = SimulationMode.FixedUpdate; // 恢复物理模拟
        isPaused = false;
        var transitioner = Object.FindFirstObjectByType<Transitioner>();
        if (transitioner != null && transitioner.CanTransition()) transitioner.TransitionToScene("MainMenu");
        else SceneManager.LoadScene("MainMenu");
    }

    // 退出游戏
    public void QuitGame()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.SaveGame();
        }

        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
