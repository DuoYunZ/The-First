using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyBindingUI : MonoBehaviour
{
    [Header("UI Template")]
    public GameObject bindingRowPrefab;

    [Header("UI Container")]
    public Transform bindingRowContainer;

    [Header("Buttons")]
    public Button resetDefaultsButton;

    [Header("Style")]
    public string waitingForKeyText = "\u8bf7\u6309\u4e0b\u65b0\u6309\u952e...";
    public Color waitingColor = Color.yellow;
    public Color normalColor = Color.white;

    private readonly List<BindingRowUI> bindingRows = new List<BindingRowUI>();
    private KeyBindingManager.BindingDevice displayDevice = KeyBindingManager.BindingDevice.KeyboardMouse;
    private int currentRebindingRow = -1;
    private bool rowsGenerated;
    private ScrollRect scrollRect;
    private GameObject lastSelectedObject;

    private class BindingRowUI
    {
        public GameObject rowObject;
        public TextMeshProUGUI actionNameText;
        public TextMeshProUGUI keyDisplayText;
        public Button keyButton;
        public string actionName;
        public int bindingIndex;
    }

    private void Awake()
    {
        scrollRect = bindingRowContainer != null ? bindingRowContainer.GetComponentInParent<ScrollRect>() : null;

        if (resetDefaultsButton != null)
        {
            resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);
        }
    }

    private void OnEnable()
    {
        KeyBindingManager.OnActiveDeviceChanged += OnActiveDeviceChanged;

        if (KeyBindingManager.Instance != null)
        {
            KeyBindingManager.Instance.OnBindingChanged += OnBindingChanged;
        }

        SetDisplayDevice(KeyBindingManager.ActiveBindingDevice, false);
        RefreshAllBindingDisplay();
    }

    private void OnDisable()
    {
        KeyBindingManager.OnActiveDeviceChanged -= OnActiveDeviceChanged;

        if (KeyBindingManager.Instance != null)
        {
            if (KeyBindingManager.Instance.IsRebinding)
            {
                KeyBindingManager.Instance.CancelCurrentRebind();
            }

            KeyBindingManager.Instance.OnBindingChanged -= OnBindingChanged;
        }

        currentRebindingRow = -1;
    }

    private void Update()
    {
        if (KeyBindingManager.Instance != null
            && KeyBindingManager.Instance.IsRebinding
            && Gamepad.current != null
            && Gamepad.current.startButton.wasPressedThisFrame)
        {
            KeyBindingManager.Instance.CancelCurrentRebind();
        }
        else
        {
            KeyBindingManager.UpdateActiveDeviceFromCurrentInput();
        }

        UpdateScrollToSelection();
    }

    public Selectable GetFirstSelectable()
    {
        EnsureRowsGenerated();

        foreach (BindingRowUI row in bindingRows)
        {
            if (row.keyButton != null && row.keyButton.gameObject.activeInHierarchy && row.keyButton.IsInteractable())
            {
                return row.keyButton;
            }
        }

        return resetDefaultsButton != null && resetDefaultsButton.gameObject.activeInHierarchy && resetDefaultsButton.IsInteractable()
            ? resetDefaultsButton
            : null;
    }

    public void SelectFirstBinding()
    {
        Selectable selectable = GetFirstSelectable();
        if (selectable == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }

    public void RefreshAllBindingDisplay()
    {
        if (KeyBindingManager.Instance == null) return;

        foreach (BindingRowUI row in bindingRows)
        {
            if (row.keyDisplayText == null) continue;

            row.keyDisplayText.text = KeyBindingManager.Instance.GetBindingDisplayString(
                row.actionName,
                row.bindingIndex);
            row.keyDisplayText.color = normalColor;
        }
    }

    private void SetDisplayDevice(KeyBindingManager.BindingDevice device, bool selectFirst)
    {
        if (rowsGenerated && displayDevice == device)
        {
            return;
        }

        displayDevice = device;
        RebuildBindingRows();
        RefreshAllBindingDisplay();

        if (selectFirst && gameObject.activeInHierarchy)
        {
            SelectFirstBinding();
        }
    }

    private void EnsureRowsGenerated()
    {
        if (!rowsGenerated)
        {
            SetDisplayDevice(KeyBindingManager.ActiveBindingDevice, false);
        }
    }

    private void RebuildBindingRows()
    {
        ClearBindingRows();
        GenerateBindingRows();
    }

    private void ClearBindingRows()
    {
        foreach (BindingRowUI row in bindingRows)
        {
            if (row.rowObject != null)
            {
                Destroy(row.rowObject);
            }
        }

        bindingRows.Clear();
        rowsGenerated = false;
        currentRebindingRow = -1;
        lastSelectedObject = null;
    }

    private void GenerateBindingRows()
    {
        if (rowsGenerated) return;
        rowsGenerated = true;

        if (bindingRowPrefab == null || bindingRowContainer == null)
        {
            Debug.LogError("[KeyBindingUI] Binding row prefab or container is missing.");
            return;
        }

        IReadOnlyList<KeyBindingManager.BindableAction> actions = KeyBindingManager.GetBindableActions(displayDevice);
        PlayerControls tempControls = new PlayerControls();
        KeyBindingManager.ApplyOverrides(tempControls);

        for (int i = 0; i < actions.Count; i++)
        {
            KeyBindingManager.BindableAction bindable = actions[i];
            GameObject rowObject = Instantiate(bindingRowPrefab, bindingRowContainer);
            rowObject.SetActive(true);
            rowObject.name = $"BindingRow_{bindable.actionName}_{bindable.bindingIndex}";

            BindingRowUI row = new BindingRowUI
            {
                rowObject = rowObject,
                actionName = bindable.actionName,
                bindingIndex = bindable.bindingIndex,
                actionNameText = rowObject.transform.Find("ActionNameText")?.GetComponent<TextMeshProUGUI>(),
                keyDisplayText = rowObject.transform.Find("KeyDisplayText")?.GetComponent<TextMeshProUGUI>()
            };

            if (row.actionNameText != null)
            {
                row.actionNameText.text = bindable.displayName;
            }

            Transform keyDisplay = rowObject.transform.Find("KeyDisplayText");
            if (keyDisplay != null)
            {
                row.keyButton = keyDisplay.GetComponent<Button>();
                if (row.keyButton == null)
                {
                    row.keyButton = keyDisplay.gameObject.AddComponent<Button>();
                }

                row.keyButton.transition = Selectable.Transition.None;
                Navigation navigation = row.keyButton.navigation;
                navigation.mode = Navigation.Mode.Automatic;
                row.keyButton.navigation = navigation;

                int rowIndex = i;
                row.keyButton.onClick.AddListener(() => OnKeyDisplayClicked(rowIndex));
            }

            if (row.keyDisplayText != null)
            {
                UnityEngine.InputSystem.InputAction action = tempControls.asset.FindAction(bindable.actionName);
                if (action != null && bindable.bindingIndex < action.bindings.Count)
                {
                    row.keyDisplayText.text = action.GetBindingDisplayString(
                        bindable.bindingIndex,
                        UnityEngine.InputSystem.InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
                }
            }

            bindingRows.Add(row);
        }

        tempControls.Dispose();
        bindingRowPrefab.SetActive(false);
    }

    private void OnKeyDisplayClicked(int rowIndex)
    {
        if (KeyBindingManager.Instance == null)
        {
            Debug.LogError("[KeyBindingUI] KeyBindingManager.Instance is null.");
            return;
        }

        if (rowIndex < 0 || rowIndex >= bindingRows.Count)
        {
            return;
        }

        if (KeyBindingManager.Instance.IsRebinding)
        {
            KeyBindingManager.Instance.CancelCurrentRebind();
        }

        currentRebindingRow = rowIndex;
        BindingRowUI row = bindingRows[rowIndex];

        if (row.keyDisplayText != null)
        {
            row.keyDisplayText.text = waitingForKeyText;
            row.keyDisplayText.color = waitingColor;
        }

        SetAllButtonsInteractable(false);

        KeyBindingManager.Instance.StartInteractiveRebind(
            row.actionName,
            row.bindingIndex,
            onComplete: () => FinishRebind(rowIndex),
            onCancel: () => FinishRebind(rowIndex));
    }

    private void FinishRebind(int rowIndex)
    {
        currentRebindingRow = -1;
        RefreshAllBindingDisplay();
        SetAllButtonsInteractable(true);
        SelectRowButton(rowIndex);
    }

    private void SelectRowButton(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= bindingRows.Count || EventSystem.current == null)
        {
            return;
        }

        Button button = bindingRows[rowIndex].keyButton;
        if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
        button.Select();
        ScrollToBindingRow(rowIndex);
    }

    private void ScrollToBindingRow(int rowIndex)
    {
        if (scrollRect == null
            || scrollRect.content == null
            || scrollRect.viewport == null
            || bindingRows.Count <= 1)
        {
            return;
        }

        if (scrollRect.content.rect.height <= scrollRect.viewport.rect.height)
        {
            return;
        }

        float normalized = 1f - Mathf.Clamp01(rowIndex / (float)(bindingRows.Count - 1));
        scrollRect.verticalNormalizedPosition = normalized;
    }

    private void UpdateScrollToSelection()
    {
        if (scrollRect == null || EventSystem.current == null)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null || selectedObject == lastSelectedObject)
        {
            return;
        }

        lastSelectedObject = selectedObject;

        for (int i = 0; i < bindingRows.Count; i++)
        {
            if (bindingRows[i].keyButton != null && bindingRows[i].keyButton.gameObject == selectedObject)
            {
                ScrollToBindingRow(i);
                return;
            }
        }

        if (resetDefaultsButton != null && resetDefaultsButton.gameObject == selectedObject)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void OnResetDefaultsClicked()
    {
        if (KeyBindingManager.Instance == null) return;

        KeyBindingManager.Instance.ResetAllBindings();
        RefreshAllBindingDisplay();

        if (resetDefaultsButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(resetDefaultsButton.gameObject);
            resetDefaultsButton.Select();
        }
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        foreach (BindingRowUI row in bindingRows)
        {
            if (row.keyButton != null)
            {
                row.keyButton.interactable = interactable;
            }
        }

        if (resetDefaultsButton != null)
        {
            resetDefaultsButton.interactable = interactable;
        }
    }

    private void OnBindingChanged(string actionName, int bindingIndex)
    {
        RefreshAllBindingDisplay();
    }

    private void OnActiveDeviceChanged(KeyBindingManager.BindingDevice device)
    {
        SetDisplayDevice(device, true);
    }
}
