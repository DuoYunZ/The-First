using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 键位设置 UI 面板
/// 点击按键文本直接进入改键模式
/// </summary>
public class KeyBindingUI : MonoBehaviour
{
    [Header("UI 模板")]
    [Tooltip("键位绑定行的预制件模板（包含：ActionNameText、KeyDisplayText）")]
    public GameObject bindingRowPrefab;

    [Header("UI 容器")]
    [Tooltip("放置所有键位绑定行的父容器")]
    public Transform bindingRowContainer;

    [Header("按钮引用")]
    [Tooltip("恢复默认按钮")]
    public Button resetDefaultsButton;

    [Header("样式设置")]
    [Tooltip("改键等待时的文字")]
    public string waitingForKeyText = "...";
    [Tooltip("改键等待时按键文本的颜色")]
    public Color waitingColor = Color.yellow;
    [Tooltip("正常状态按键文本的颜色")]
    public Color normalColor = Color.white;

    // 缓存已生成的行 UI 引用
    private List<BindingRowUI> bindingRows = new List<BindingRowUI>();

    // 当前正在改键的行索引
    private int currentRebindingRow = -1;

    private class BindingRowUI
    {
        public TextMeshProUGUI actionNameText;
        public TextMeshProUGUI keyDisplayText;
        public Button keyButton;           // 按键文本上的 Button 组件（可点击）
        public string actionName;
        public int bindingIndex;
    }

    void OnEnable()
    {
        // 每次显示面板时刷新
        RefreshAllBindingDisplay();

        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingChanged += OnBindingChanged;
    }

    void OnDisable()
    {
        if (KeyBindingManager.Instance != null)
        {
            if (KeyBindingManager.Instance.IsRebinding)
                KeyBindingManager.Instance.CancelCurrentRebind();
            KeyBindingManager.Instance.OnBindingChanged -= OnBindingChanged;
        }
        currentRebindingRow = -1;
    }

    void Start()
    {
        GenerateBindingRows();

        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);

        RefreshAllBindingDisplay();
    }

    /// <summary>
    /// 动态生成键位绑定行
    /// </summary>
    private void GenerateBindingRows()
    {
        if (bindingRowPrefab == null || bindingRowContainer == null)
        {
            Debug.LogError("[KeyBindingUI] bindingRowPrefab 或 bindingRowContainer 未赋值！");
            return;
        }

        var actions = KeyBindingManager.GetBindableActions();

        // 临时实例用于读取默认绑定
        var tempControls = new PlayerControls();
        KeyBindingManager.ApplyOverrides(tempControls);

        for (int i = 0; i < actions.Count; i++)
        {
            var bindable = actions[i];

            GameObject rowGO = Instantiate(bindingRowPrefab, bindingRowContainer);
            rowGO.SetActive(true);
            rowGO.name = $"BindingRow_{bindable.actionName}_{bindable.bindingIndex}";

            var row = new BindingRowUI();
            row.actionName = bindable.actionName;
            row.bindingIndex = bindable.bindingIndex;

            row.actionNameText = rowGO.transform.Find("ActionNameText")?.GetComponent<TextMeshProUGUI>();
            row.keyDisplayText = rowGO.transform.Find("KeyDisplayText")?.GetComponent<TextMeshProUGUI>();

            // 获取 KeyDisplayText 上的 Button 组件（如果没有则自动添加）
            var keyDisplayGO = rowGO.transform.Find("KeyDisplayText");
            if (keyDisplayGO != null)
            {
                row.keyButton = keyDisplayGO.GetComponent<Button>();
                if (row.keyButton == null)
                {
                    row.keyButton = keyDisplayGO.gameObject.AddComponent<Button>();
                    // 设置为透明按钮（不需要额外图片）
                    row.keyButton.transition = Selectable.Transition.None;
                }
            }

            // 设置操作名称
            if (row.actionNameText != null)
                row.actionNameText.text = bindable.displayName;

            // 设置当前按键显示
            if (row.keyDisplayText != null)
            {
                var action = tempControls.asset.FindAction(bindable.actionName);
                if (action != null && bindable.bindingIndex < action.bindings.Count)
                {
                    row.keyDisplayText.text = action.GetBindingDisplayString(
                        bindable.bindingIndex,
                        InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
                }
            }

            // 点击按键文本直接进入改键
            if (row.keyButton != null)
            {
                int rowIndex = i;
                row.keyButton.onClick.AddListener(() => OnKeyDisplayClicked(rowIndex));
            }

            bindingRows.Add(row);
        }

        tempControls.Dispose();
        bindingRowPrefab.SetActive(false);
    }

    /// <summary>
    /// 点击按键文本，直接进入改键模式
    /// </summary>
    private void OnKeyDisplayClicked(int rowIndex)
    {
        if (KeyBindingManager.Instance == null)
        {
            Debug.LogError("[KeyBindingUI] KeyBindingManager.Instance 为 null！");
            return;
        }

        // 如果已在改键，先取消
        if (KeyBindingManager.Instance.IsRebinding)
            KeyBindingManager.Instance.CancelCurrentRebind();

        currentRebindingRow = rowIndex;
        var row = bindingRows[rowIndex];

        // 显示等待状态
        if (row.keyDisplayText != null)
        {
            row.keyDisplayText.text = waitingForKeyText;
            row.keyDisplayText.color = waitingColor;
        }

        // 禁用所有按钮
        SetAllButtonsInteractable(false);

        // 开始交互式改键
        KeyBindingManager.Instance.StartInteractiveRebind(
            row.actionName,
            row.bindingIndex,
            onComplete: () =>
            {
                currentRebindingRow = -1;
                RefreshAllBindingDisplay();
                SetAllButtonsInteractable(true);
            },
            onCancel: () =>
            {
                currentRebindingRow = -1;
                RefreshAllBindingDisplay();
                SetAllButtonsInteractable(true);
            }
        );
    }

    private void OnResetDefaultsClicked()
    {
        if (KeyBindingManager.Instance != null)
        {
            KeyBindingManager.Instance.ResetAllBindings();
            RefreshAllBindingDisplay();
        }
    }

    /// <summary>
    /// 刷新所有行的按键显示
    /// </summary>
    public void RefreshAllBindingDisplay()
    {
        if (KeyBindingManager.Instance == null) return;

        for (int i = 0; i < bindingRows.Count; i++)
        {
            var row = bindingRows[i];
            if (row.keyDisplayText != null)
            {
                row.keyDisplayText.text = KeyBindingManager.Instance.GetBindingDisplayString(
                    row.actionName, row.bindingIndex);
                row.keyDisplayText.color = normalColor;
            }
        }
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        foreach (var row in bindingRows)
        {
            if (row.keyButton != null)
                row.keyButton.interactable = interactable;
        }
        if (resetDefaultsButton != null)
            resetDefaultsButton.interactable = interactable;
    }

    private void OnBindingChanged(string actionName, int bindingIndex)
    {
        RefreshAllBindingDisplay();
    }
}
