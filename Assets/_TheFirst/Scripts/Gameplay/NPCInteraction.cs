using UnityEngine;
using UnityEngine.InputSystem;

public class NPCInteraction : MonoBehaviour
{
    [Header("功能设置")]
    [Tooltip("这个NPC关联的武器技能树")]
    public WeaponSkillTree skillTreeToOpen;

    [Header("UI提示")]
    [Tooltip("当玩家进入范围时显示的交互提示UI（例如一个'E'键图标）")]
    public GameObject interactionPromptUI;

    private bool playerIsInRange = false;
    private PlayerControls playerControls;

    void Awake()
    {
        playerControls = new PlayerControls();
        KeyBindingManager.ApplyOverrides(playerControls);
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
    }

    void Start()
    {
        if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
    }

    void Update()
    {
        if (playerIsInRange && playerControls.Player.Interact.WasPressedThisFrame())
        {
            if (UIManager.Instance != null && UIManager.Instance.skillTreeUIManager != null)
            {
                // 【关键修改】检查技能树界面是否已经打开
                if (UIManager.Instance.skillTreeUIManager.IsPanelOpen())
                {
                    // 如果界面已经打开，则关闭它
                    UIManager.Instance.skillTreeUIManager.ClosePanel();
                }
                else
                {
                    // 如果界面没有打开，则打开它
                    UIManager.Instance.skillTreeUIManager.OpenPanel();
                }
            }
            else
            {
                Debug.LogError("UIManager 或 SkillTreeUIManager 未找到！无法打开/关闭技能树。");
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

            // 【可选】当玩家离开NPC范围时，如果技能树界面是打开的，则关闭它
            // 这取决于你的设计需求
            if (UIManager.Instance != null &&
                UIManager.Instance.skillTreeUIManager != null &&
                UIManager.Instance.skillTreeUIManager.IsPanelOpen())
            {
                UIManager.Instance.skillTreeUIManager.ClosePanel();
            }
        }
    }
}