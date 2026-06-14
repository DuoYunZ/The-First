using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyBindingManager : MonoBehaviour
{
    public enum BindingDevice
    {
        KeyboardMouse,
        Gamepad
    }

    [Serializable]
    public class BindableAction
    {
        public string actionName;
        public int bindingIndex;
        public string displayName;
        public BindingDevice device;

        public BindableAction(string actionName, int bindingIndex, string displayName, BindingDevice device)
        {
            this.actionName = actionName;
            this.bindingIndex = bindingIndex;
            this.displayName = displayName;
            this.device = device;
        }
    }

    public static KeyBindingManager Instance { get; private set; }
    public static BindingDevice ActiveBindingDevice => activeBindingDevice;
    public static event Action<BindingDevice> OnActiveDeviceChanged;

    public event Action<string, int> OnBindingChanged;

    private const string BINDINGS_PREFS_KEY = "InputBindingOverrides";
    private const float STICK_ACTIVE_THRESHOLD_SQR = 0.25f;
    private const float MOUSE_ACTIVE_THRESHOLD_SQR = 1f;

    private static BindingDevice activeBindingDevice = BindingDevice.KeyboardMouse;

    private static readonly List<BindableAction> keyboardMouseActions = new List<BindableAction>
    {
        new BindableAction("Move", 1, "\u4e0a\u79fb", BindingDevice.KeyboardMouse),
        new BindableAction("Move", 2, "\u4e0b\u79fb", BindingDevice.KeyboardMouse),
        new BindableAction("Move", 3, "\u5de6\u79fb", BindingDevice.KeyboardMouse),
        new BindableAction("Move", 4, "\u53f3\u79fb", BindingDevice.KeyboardMouse),
        new BindableAction("Interact", 0, "\u4ea4\u4e92", BindingDevice.KeyboardMouse),
        new BindableAction("Dash", 0, "\u51b2\u523a", BindingDevice.KeyboardMouse),
        new BindableAction("Ultimate", 0, "\u5927\u62db", BindingDevice.KeyboardMouse),
    };

    private static readonly List<BindableAction> gamepadActions = new List<BindableAction>
    {
        new BindableAction("Move", 5, "\u79fb\u52a8", BindingDevice.Gamepad),
        new BindableAction("Look", 0, "\u7784\u51c6", BindingDevice.Gamepad),
        new BindableAction("Interact", 1, "\u4ea4\u4e92", BindingDevice.Gamepad),
        new BindableAction("Dash", 1, "\u51b2\u523a", BindingDevice.Gamepad),
        new BindableAction("Ultimate", 1, "\u5927\u62db", BindingDevice.Gamepad),
        new BindableAction("ScrollWeapon", 2, "\u5207\u6362\u5de6", BindingDevice.Gamepad),
        new BindableAction("ScrollWeapon", 3, "\u5207\u6362\u53f3", BindingDevice.Gamepad),
    };

    private static readonly List<BindableAction> allBindableActions = new List<BindableAction>();

    private PlayerControls controls;
    private InputActionAsset inputActionAsset;
    private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;
    private int lastRebindEndFrame = -1;

    static KeyBindingManager()
    {
        allBindableActions.AddRange(keyboardMouseActions);
        allBindableActions.AddRange(gamepadActions);
    }

    public static IReadOnlyList<BindableAction> GetBindableActions()
    {
        return GetBindableActions(activeBindingDevice);
    }

    public static IReadOnlyList<BindableAction> GetBindableActions(BindingDevice device)
    {
        return device == BindingDevice.Gamepad ? gamepadActions : keyboardMouseActions;
    }

    public static void SetActiveBindingDevice(BindingDevice device)
    {
        if (activeBindingDevice == device)
        {
            return;
        }

        activeBindingDevice = device;
        OnActiveDeviceChanged?.Invoke(activeBindingDevice);
    }

    public static void UpdateActiveDeviceFromCurrentInput()
    {
        if (Instance != null && (Instance.IsRebinding || Instance.RebindEndedThisFrame))
        {
            return;
        }

        if (HasGamepadInputThisFrame())
        {
            SetActiveBindingDevice(BindingDevice.Gamepad);
            return;
        }

        if (HasKeyboardMouseInputThisFrame())
        {
            SetActiveBindingDevice(BindingDevice.KeyboardMouse);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        controls = new PlayerControls();
        inputActionAsset = controls.asset;

        if (Gamepad.current != null)
        {
            SetActiveBindingDevice(BindingDevice.Gamepad);
        }

        InputSystem.onDeviceChange += HandleInputDeviceChange;
    }

    private void Start()
    {
        LoadBindingOverrides();
    }

    private void Update()
    {
        UpdateActiveDeviceFromCurrentInput();
    }

    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= HandleInputDeviceChange;
        currentRebindOperation?.Dispose();
        controls?.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ApplyOverrides(PlayerControls targetControls)
    {
        if (targetControls == null) return;

        string overridesJson = PlayerPrefs.GetString(BINDINGS_PREFS_KEY, string.Empty);
        if (!string.IsNullOrEmpty(overridesJson))
        {
            targetControls.asset.LoadBindingOverridesFromJson(overridesJson);
        }
    }

    public void StartInteractiveRebind(
        string actionName,
        int bindingIndex,
        Action onComplete = null,
        Action onCancel = null)
    {
        BindableAction bindable = FindBindableAction(actionName, bindingIndex);
        if (bindable == null)
        {
            Debug.LogError($"[KeyBindingManager] Binding is not configurable: {actionName}[{bindingIndex}]");
            onCancel?.Invoke();
            return;
        }

        StartInteractiveRebind(bindable, onComplete, onCancel);
    }

    public void CancelCurrentRebind()
    {
        currentRebindOperation?.Cancel();
    }

    public string GetBindingDisplayString(string actionName, int bindingIndex)
    {
        if (inputActionAsset == null) return "???";

        InputAction action = inputActionAsset.FindAction(actionName);
        if (action == null) return "???";

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return "???";
        }

        return action.GetBindingDisplayString(
            bindingIndex,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }

    public void ResetAllBindings()
    {
        if (inputActionAsset == null) return;

        inputActionAsset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(BINDINGS_PREFS_KEY);
        PlayerPrefs.Save();
        OnBindingChanged?.Invoke(string.Empty, -1);
    }

    public bool IsRebinding => currentRebindOperation != null;
    public bool RebindEndedThisFrame => lastRebindEndFrame == Time.frameCount;

    private static bool HasGamepadInputThisFrame()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return false;
        }

        return gamepad.buttonSouth.wasPressedThisFrame
            || gamepad.buttonNorth.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame
            || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.leftShoulder.wasPressedThisFrame
            || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.startButton.wasPressedThisFrame
            || gamepad.selectButton.wasPressedThisFrame
            || gamepad.leftStickButton.wasPressedThisFrame
            || gamepad.rightStickButton.wasPressedThisFrame
            || gamepad.dpad.ReadValue().sqrMagnitude > STICK_ACTIVE_THRESHOLD_SQR
            || gamepad.leftStick.ReadValue().sqrMagnitude > STICK_ACTIVE_THRESHOLD_SQR
            || gamepad.rightStick.ReadValue().sqrMagnitude > STICK_ACTIVE_THRESHOLD_SQR;
    }

    private static bool HasKeyboardMouseInputThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        return mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame
            || mouse.forwardButton.wasPressedThisFrame
            || mouse.backButton.wasPressedThisFrame
            || mouse.delta.ReadValue().sqrMagnitude > MOUSE_ACTIVE_THRESHOLD_SQR
            || mouse.scroll.ReadValue().sqrMagnitude > MOUSE_ACTIVE_THRESHOLD_SQR;
    }

    private void HandleInputDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad)
        {
            return;
        }

        if (change == InputDeviceChange.Added
            || change == InputDeviceChange.Reconnected
            || change == InputDeviceChange.Enabled)
        {
            SetActiveBindingDevice(BindingDevice.Gamepad);
            return;
        }

        if ((change == InputDeviceChange.Removed
                || change == InputDeviceChange.Disconnected
                || change == InputDeviceChange.Disabled)
            && activeBindingDevice == BindingDevice.Gamepad
            && Gamepad.current == null)
        {
            SetActiveBindingDevice(BindingDevice.KeyboardMouse);
        }
    }

    private void StartInteractiveRebind(BindableAction bindable, Action onComplete, Action onCancel)
    {
        if (inputActionAsset == null) return;

        InputAction action = inputActionAsset.FindAction(bindable.actionName);
        if (action == null)
        {
            Debug.LogError($"[KeyBindingManager] Action not found: {bindable.actionName}");
            onCancel?.Invoke();
            return;
        }

        if (bindable.bindingIndex < 0 || bindable.bindingIndex >= action.bindings.Count)
        {
            Debug.LogError($"[KeyBindingManager] Invalid binding index: {bindable.actionName}[{bindable.bindingIndex}]");
            onCancel?.Invoke();
            return;
        }

        currentRebindOperation?.Cancel();
        action.Disable();

        InputActionRebindingExtensions.RebindingOperation operation = action
            .PerformInteractiveRebinding(bindable.bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Touchscreen>")
            .WithTimeout(10f)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithCancelingThrough("<Gamepad>/start");

        if (bindable.device == BindingDevice.Gamepad)
        {
            operation
                .WithControlsExcluding("<Keyboard>")
                .WithControlsExcluding("<Mouse>");
        }
        else
        {
            operation.WithControlsExcluding("<Gamepad>");
        }

        currentRebindOperation = operation
            .OnComplete(completedOperation =>
            {
                string newPath = action.bindings[bindable.bindingIndex].effectivePath;
                bool invalidDevice = !IsPathAllowedForDevice(newPath, bindable.device);
                bool hasConflict = !invalidDevice && CheckForConflict(bindable, newPath);

                if (invalidDevice || hasConflict)
                {
                    action.RemoveBindingOverride(bindable.bindingIndex);
                    Debug.LogWarning(invalidDevice
                        ? "[KeyBindingManager] Binding rejected because it belongs to the wrong device."
                        : "[KeyBindingManager] Binding rejected because it conflicts with another binding.");
                }

                action.Enable();
                completedOperation.Dispose();
                currentRebindOperation = null;
                lastRebindEndFrame = Time.frameCount;

                if (!invalidDevice && !hasConflict)
                {
                    SaveBindingOverrides();
                    OnBindingChanged?.Invoke(bindable.actionName, bindable.bindingIndex);
                }

                onComplete?.Invoke();
            })
            .OnCancel(cancelledOperation =>
            {
                action.Enable();
                cancelledOperation.Dispose();
                currentRebindOperation = null;
                lastRebindEndFrame = Time.frameCount;
                onCancel?.Invoke();
            });

        currentRebindOperation.Start();
    }

    private BindableAction FindBindableAction(string actionName, int bindingIndex)
    {
        foreach (BindableAction bindable in allBindableActions)
        {
            if (bindable.actionName == actionName && bindable.bindingIndex == bindingIndex)
            {
                return bindable;
            }
        }

        return null;
    }

    private bool CheckForConflict(BindableAction currentBinding, string newPath)
    {
        if (string.IsNullOrEmpty(newPath)) return true;

        foreach (BindableAction bindable in allBindableActions)
        {
            if (bindable == currentBinding || bindable.device != currentBinding.device)
            {
                continue;
            }

            InputAction otherAction = inputActionAsset.FindAction(bindable.actionName);
            if (otherAction == null || bindable.bindingIndex >= otherAction.bindings.Count)
            {
                continue;
            }

            if (otherAction.bindings[bindable.bindingIndex].effectivePath == newPath)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPathAllowedForDevice(string path, BindingDevice device)
    {
        if (string.IsNullOrEmpty(path)) return false;

        if (device == BindingDevice.Gamepad)
        {
            return path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
        }

        return path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase);
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
}
