using UnityEngine;
using UnityEngine.UI; // 引入UI命名空间
using System.Collections; // <--- 添加这一行 (Add this line)
using TMPro; // <--- 确保有这一行



public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI 面板引用")]
    public GameObject gameOverPanel;
    public GameObject combatUIContainer;

    [Header("UI元素引用")]
    public TextMeshProUGUI waveNumberText;         // <--- 修改类型
    public TextMeshProUGUI nextWaveTimerText;      // <--- 修改类型
    public TextMeshProUGUI enemiesRemainingText;   // <--- 修改类型
    public GameObject waveMessagePanel;            // 这个是 GameObject 类型，保持不变
    public TextMeshProUGUI waveMessageText;        // <--- 修改类型 (Panel下的文本)
    public PlayerHealthUI playerHealthUI;
    public TextMeshProUGUI goldDisplayText;

    [Header("UI 面板引用")]
    [Tooltip("对 SkillTreeUIManager 脚本的引用")]
    public SkillTreeUIManager skillTreeUIManager;

    public Transform weaponUiContainer; // 拖入 Canvas 里的一个 Layout Group
    public GameObject weaponSlotPrefab; // 拖入上面做好的 Prefab

    // 【新增】缓存战斗 UI 数据，用于语言切换时刷新
    private int _cachedWaveNum;
    private string _cachedWaveName = "";
    private float _cachedNextWaveTime;
    private int _cachedEnemiesRemaining;

    void Start()
    {
        EnsureGoldDisplayReference();

        if (PlayerProgressManager.Instance != null)
        {
            // 直接调用已有的更新方法，传入当前的金币值
            UpdateGoldDisplay(PlayerProgressManager.Instance.currentGold);
        }
        else
        {
            // 这是一个保险措施，正常情况下不应该发生
            Debug.LogWarning("在UIManager启动时未能找到PlayerProgressManager！");
        }
        // if (gameOverPanel != null) gameOverPanel.SetActive(false);
        // if (combatUIContainer != null) combatUIContainer.SetActive(false); // 戰鬥UI預設也關閉

    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果您的UIManager是持久化的
            EnsureGoldDisplayReference();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshAllText;
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshAllText;
    }

    /// <summary>
    /// 语言切换时调用，立即刷新所有当前显示的战斗 UI 文本
    /// </summary>
    private void RefreshAllText()
    {
        UpdateWaveNumber(_cachedWaveNum, _cachedWaveName);
        UpdateNextWaveTimer(_cachedNextWaveTime);
        UpdateEnemiesRemaining(_cachedEnemiesRemaining);
    }

    public void InitializeCombatUIReferences()
    {
        // 1. 寻找战斗UI的根容器
        // 注意：这里的名字必须和您场景Hierarchy中的完全一致
        GameObject combatUI_Container_GO = GameObject.Find("---Combat UI Container---");

        if (combatUI_Container_GO == null)
        {
            Debug.LogError("[UIManager] 初始化失败: 在场景中未能找到名为 '---Combat UI Container---' 的对象！");
            return;
        }

        // 2. 将找到的根容器赋值给 combatUIContainer 变量
        combatUIContainer = combatUI_Container_GO;

        // 3. 在这个根容器下，根据层级结构查找每一个具体的UI元素
        // 我们使用 transform.Find() 来精确定位
        Transform canvasTransform = combatUIContainer.transform.Find("Canvas");
        if (canvasTransform == null)
        {
            Debug.LogError("[UIManager] 初始化失败: 在 '---Combat UI Container---' 下未能找到名为 'Canvas' 的子对象！");
            return;
        }

        // 根据您之前的截图结构，我们来查找各个组件
        waveNumberText = canvasTransform.Find("WaveNumber_Text")?.GetComponent<TextMeshProUGUI>();
        nextWaveTimerText = canvasTransform.Find("NextWaveTimer_Text")?.GetComponent<TextMeshProUGUI>();
        enemiesRemainingText = canvasTransform.Find("EnemiesRemaining_Text")?.GetComponent<TextMeshProUGUI>();

        waveMessagePanel = canvasTransform.Find("WaveMessage_Panel")?.gameObject;
        if (waveMessagePanel != null)
        {
            // waveMessageText 是 waveMessagePanel 的子对象
            waveMessageText = waveMessagePanel.transform.Find("WaveMessage_Text")?.GetComponent<TextMeshProUGUI>();
        }

        // 4. 单独寻找 GameOverPanel (假设它在场景根层级)
        //gameOverPanel = GameObject.Find("GameOverPanel");


        // 5. 添加一些安全检查，确保都找到了
        if (waveNumberText == null) Debug.LogWarning("UIManager: 未能找到 WaveNumber_Text");
        if (nextWaveTimerText == null) Debug.LogWarning("UIManager: 未能找到 NextWaveTimer_Text");
        if (enemiesRemainingText == null) Debug.LogWarning("UIManager: 未能找到 EnemiesRemaining_Text");
        if (waveMessagePanel == null) Debug.LogWarning("UIManager: 未能找到 WaveMessage_Panel");
        if (waveMessageText == null) Debug.LogWarning("UIManager: 未能找到 WaveMessage_Text");
        //if (gameOverPanel == null) Debug.LogWarning("UIManager: 未能找到 GameOverPanel");


        // 6. 初始化完成后，确保战斗UI是可见的
        ShowCombatUI();
    }
    public void UpdateWaveNumber(int waveNum, string waveName = "", WaveType type = WaveType.Normal)
    {
        _cachedWaveNum = waveNum;
        _cachedWaveName = waveName;
        if (waveNumberText != null)
            waveNumberText.text = string.IsNullOrEmpty(waveName)
                ? LocalizationManager.T("ui.wave", waveNum)
                : LocalizationManager.T("ui.wave_named", waveNum, waveName);
        // 可以根据 WaveType 改变UI风格等
    }

    public void UpdateNextWaveTimer(float time)
    {
        _cachedNextWaveTime = time;
        if (nextWaveTimerText != null)
        {
            if (time > 0)
            {
                nextWaveTimerText.gameObject.SetActive(true);
                nextWaveTimerText.text = LocalizationManager.T("ui.next_wave", Mathf.CeilToInt(time));
            }
            else
            {
                nextWaveTimerText.gameObject.SetActive(false);
            }
        }
    }

    public void UpdateEnemiesRemaining(int count)
    {
        _cachedEnemiesRemaining = count;
        if (enemiesRemainingText != null)
        {
            if (count > 0)
            {
                enemiesRemainingText.gameObject.SetActive(true);
                enemiesRemainingText.text = LocalizationManager.T("ui.enemies_remaining", count);
            }
            else
            {
                enemiesRemainingText.gameObject.SetActive(false);
            }
        }
    }

    public void ShowWaveMessage(string message, float duration = 2f)
    {
        if (waveMessagePanel != null && waveMessageText != null)
        {
            waveMessageText.text = message;
            StartCoroutine(ShowMessageCoroutine(duration));
        }
    }

    IEnumerator ShowMessageCoroutine(float duration)
    {
        waveMessagePanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        waveMessagePanel.SetActive(false);
    }

    // --- 提供給 GameManager 呼叫的公共方法 ---
    public void ShowCombatUI()
    {
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
    }

    public void HideCombatUI()
    {
        if (combatUIContainer != null) combatUIContainer.SetActive(false);
    }

    public void ShowGameOverPanel()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void UpdateGoldDisplay(int amount)
    {
        EnsureGoldDisplayReference();

        if (goldDisplayText != null)
        {
            goldDisplayText.gameObject.SetActive(true);
            goldDisplayText.enabled = true;
            goldDisplayText.transform.SetAsLastSibling();
            goldDisplayText.color = new Color(0.24f, 0.12f, 0.03f, 1f);
            goldDisplayText.text = $"{amount}";
        }
    }

    private void EnsureGoldDisplayReference()
    {
        if (goldDisplayText != null) return;

        TextMeshProUGUI[] textComponents =
            FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (TextMeshProUGUI textComponent in textComponents)
        {
            if (textComponent != null && textComponent.name == "Gold_Display_Text")
            {
                goldDisplayText = textComponent;
                Debug.Log("[UIManager] Auto-bound goldDisplayText to Gold_Display_Text.");
                return;
            }
        }

        Debug.LogWarning("[UIManager] goldDisplayText is missing and Gold_Display_Text was not found.");
    }

    public void CreateUiForWeapon(WeaponPart weapon)
    {
        GameObject slotObj = Instantiate(weaponSlotPrefab, weaponUiContainer);
        WeaponStatusSlot slotScript = slotObj.GetComponent<WeaponStatusSlot>();

        // 【关键】绑定
        slotScript.BindWeapon(weapon);
    }
}
