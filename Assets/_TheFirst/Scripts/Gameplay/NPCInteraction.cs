using UnityEngine;
using UnityEngine.InputSystem; // 【关键】引入新的输入系统命名空间

public class NPCInteraction : MonoBehaviour
{
    [Header("功能设置")]
    [Tooltip("这个NPC关联的武器技能树")]
    public WeaponSkillTree skillTreeToOpen;

    [Header("UI提示")]
    [Tooltip("当玩家进入范围时显示的交互提示UI（例如一个'E'键图标）")]
    public GameObject interactionPromptUI;

    private bool playerIsInRange = false;
    private PlayerControls playerControls; // 【新增】PlayerControls的引用

    void Awake()
    {
        // 【新增】在Awake中初始化PlayerControls
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        // 【新增】启用输入操作
        playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        // 【新增】禁用输入操作，防止内存泄漏
        playerControls.Player.Disable();
    }

    void Start()
    {
        if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
    }

    void Update()
    {
        // 【核心修正】将 Input.GetKeyDown 替换为新的输入系统检测方式
        // 我们假设您在PlayerControls中有一个名为 "Interact" 的 Action
        if (playerIsInRange && playerControls.Player.Interact.WasPressedThisFrame())
        {
            Debug.Log("--- 1. E键按下，准备调用UIManager ---"); // <-- 新增

            // 确保UIManager和其下的skillTreeUIManager都已设置
            if (UIManager.Instance != null && UIManager.Instance.skillTreeUIManager != null)
            {
                UIManager.Instance.skillTreeUIManager.OpenPanel(skillTreeToOpen);
            }
            else
            {
                Debug.LogError("UIManager 或 SkillTreeUIManager 未找到！无法打开技能树。");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInRange = true;
            if (interactionPromptUI != null) interactionPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInRange = false;
            if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
        }
    }
}