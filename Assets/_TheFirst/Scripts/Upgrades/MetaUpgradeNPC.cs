using UnityEngine;
using UnityEngine.InputSystem;

public class MetaUpgradeNPC : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject metaUpgradeUIPanel;
    public GameObject interactionPromptUI;

    private bool playerIsInRange = false;
    private PlayerControls playerControls;

    void Awake()
    {
        playerControls = new PlayerControls();
    }

    private void OnEnable() => playerControls.Player.Enable();
    private void OnDisable() => playerControls.Player.Disable();

    void Update()
    {
        // 1. 如果玩家在范围内
        if (playerIsInRange)
        {
            // 2. 检测按键
            if (playerControls.Player.Interact.WasPressedThisFrame())
            {
                Debug.Log(">>> [MetaNPC] 检测到 E 键按下！准备切换 UI...");

                if (metaUpgradeUIPanel != null)
                {
                    bool isActive = metaUpgradeUIPanel.activeSelf;
                    metaUpgradeUIPanel.SetActive(!isActive);

                    // 暂停/恢复时间
                    Time.timeScale = !isActive ? 0f : 1f;
                    Debug.Log($">>> [MetaNPC] UI 状态已切换为: {!isActive}");
                }
                else
                {
                    Debug.LogError(">>> [MetaNPC] 错误！UI Panel 没赋值！请在 Inspector 里拖进去。");
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 打印碰到了谁，帮你确认 Tag 是否正确
        Debug.Log($"[MetaNPC] 有物体进入触发器: {other.name}, Tag: {other.tag}");

        if (other.CompareTag("Player"))
        {
            playerIsInRange = true;
            if (interactionPromptUI != null) interactionPromptUI.SetActive(true);
            Debug.Log("[MetaNPC] 玩家进入范围！现在按 E 应该有效。");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInRange = false;
            if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
            if (metaUpgradeUIPanel != null) metaUpgradeUIPanel.SetActive(false);
            Debug.Log("[MetaNPC] 玩家离开范围。");
        }
    }
}