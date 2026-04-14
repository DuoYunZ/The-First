using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 键位绑定管理器 - 单例
/// 负责运行时改键、保存/加载自定义键位到 PlayerPrefs
/// 内部自建 PlayerControls 实例来执行改键操作
/// </summary>
public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    /// <summary>
    /// 当键位发生变化时触发（actionName, bindingIndex）
    /// </summary>
    public event Action<string, int> OnBindingChanged;

    // PlayerPrefs 中保存键位覆盖的键名
    private const string BINDINGS_PREFS_KEY = "InputBindingOverrides";

    // 内部持有一个 PlayerControls 实例，用于改键和显示
    private PlayerControls controls;
    private InputActionAsset inputActionAsset;

    // 当前正在进行的交互式改键操作
    private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;

    // 可改键的操作配置
    private static readonly List<BindableAction> bindableActions = new List<BindableAction>
    {
        new BindableAction("Move", 1, "上移"),   // 2D Vector composite: up
        new BindableAction("Move", 2, "下移"),   // down
        new BindableAction("Move", 3, "左移"),   // left
        new BindableAction("Move", 4, "右移"),   // right
        new BindableAction("Interact", 0, "交互"),
        new BindableAction("Dash", 0, "冲刺"),
        new BindableAction("Ultimate", 0, "大招"),
    };

    [Serializable]
    public class BindableAction
    {
        public string actionName;
        public int bindingIndex;
        public string displayName;

        public BindableAction(string actionName, int bindingIndex, string displayName)
        {
            this.actionName = actionName;
            this.bindingIndex = bindingIndex;
            this.displayName = displayName;
        }
    }

    public static List<BindableAction> GetBindableActions() => bindableActions;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景持久化

        // 内部创建 PlayerControls 实例（与 MechController 等使用相同的 JSON 定义）
        controls = new PlayerControls();
        inputActionAsset = controls.asset;
    }

    void Start()
    {
        // 加载保存的键位覆盖到内部实例
        LoadBindingOverrides();
    }

    void OnDestroy()
    {
        currentRebindOperation?.Dispose();
        controls?.Dispose();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 【静态工具方法】将已保存的键位覆盖应用到任意 PlayerControls 实例
    /// 在各个控制器（MechController 等）创建 PlayerControls 后调用此方法
    /// </summary>
    public static void ApplyOverrides(PlayerControls targetControls)
    {
        if (targetControls == null) return;

        string overridesJson = PlayerPrefs.GetString(BINDINGS_PREFS_KEY, string.Empty);
        if (!string.IsNullOrEmpty(overridesJson))
        {
            targetControls.asset.LoadBindingOverridesFromJson(overridesJson);
        }
    }

    /// <summary>
    /// 开始交互式改键
    /// </summary>
    public void StartInteractiveRebind(string actionName, int bindingIndex,
        Action onComplete = null, Action onCancel = null)
    {
        if (inputActionAsset == null) return;

        InputAction action = inputActionAsset.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"[KeyBindingManager] 找不到 Action: {actionName}");
            return;
        }

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            Debug.LogError($"[KeyBindingManager] binding 索引 {bindingIndex} 无效！");
            return;
        }

        currentRebindOperation?.Cancel();

        // 改键期间禁用该 Action
        action.Disable();

        currentRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")   // 排除鼠标位置
            .WithControlsExcluding("<Mouse>/delta")       // 排除鼠标移动增量
            .WithTimeout(10f)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation =>
            {
                // 检查冲突
                string newPath = action.bindings[bindingIndex].effectivePath;
                bool hasConflict = CheckForConflict(actionName, bindingIndex, newPath);

                if (hasConflict)
                {
                    action.RemoveBindingOverride(bindingIndex);
                    Debug.LogWarning("[KeyBindingManager] 按键冲突！已撤销。");
                }

                action.Enable();
                operation.Dispose();
                currentRebindOperation = null;

                if (!hasConflict)
                {
                    SaveBindingOverrides();
                    OnBindingChanged?.Invoke(actionName, bindingIndex);
                }

                onComplete?.Invoke();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                operation.Dispose();
                currentRebindOperation = null;
                onCancel?.Invoke();
            })
            .Start();
    }

    public void CancelCurrentRebind()
    {
        currentRebindOperation?.Cancel();
    }

    private bool CheckForConflict(string currentActionName, int currentBindingIndex, string newPath)
    {
        foreach (var bindable in bindableActions)
        {
            if (bindable.actionName == currentActionName && bindable.bindingIndex == currentBindingIndex)
                continue;

            InputAction otherAction = inputActionAsset.FindAction(bindable.actionName);
            if (otherAction == null) continue;

            if (bindable.bindingIndex < otherAction.bindings.Count)
            {
                if (otherAction.bindings[bindable.bindingIndex].effectivePath == newPath)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取指定操作当前绑定的显示文本
    /// </summary>
    public string GetBindingDisplayString(string actionName, int bindingIndex)
    {
        if (inputActionAsset == null) return "???";

        InputAction action = inputActionAsset.FindAction(actionName);
        if (action == null) return "???";

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return "???";

        return action.GetBindingDisplayString(bindingIndex,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }

    /// <summary>
    /// 重置所有键位为默认值
    /// </summary>
    public void ResetAllBindings()
    {
        if (inputActionAsset == null) return;

        foreach (var bindable in bindableActions)
        {
            InputAction action = inputActionAsset.FindAction(bindable.actionName);
            action?.RemoveBindingOverride(bindable.bindingIndex);
        }

        PlayerPrefs.DeleteKey(BINDINGS_PREFS_KEY);
        PlayerPrefs.Save();
        OnBindingChanged?.Invoke("", -1);
    }

    private void SaveBindingOverrides()
    {
        if (inputActionAsset == null) return;

        string json = inputActionAsset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(BINDINGS_PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadBindingOverrides()
    {
        if (inputActionAsset == null) return;

        string json = PlayerPrefs.GetString(BINDINGS_PREFS_KEY, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            inputActionAsset.LoadBindingOverridesFromJson(json);
        }
    }

    public bool IsRebinding => currentRebindOperation != null;
}
