using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色雕像NPC — 挂在HubScene中每个角色雕像上
/// 玩家靠近按E打开该角色的信息面板
/// </summary>
public class CharacterStatueNPC : MonoBehaviour
{
    [Header("角色配置")]
    [Tooltip("这个雕像对应的角色数据")]
    public CharacterData characterData;

    [Header("UI 引用")]
    [Tooltip("角色信息面板（场景中的 CharacterSelectManager 所在对象）")]
    public CharacterSelectManager characterSelectUI;
    [Tooltip("'按E互动'提示UI")]
    public GameObject interactionPromptUI;

    private bool playerIsInRange = false;
    private PlayerControls playerControls;

    void Awake()
    {
        playerControls = new PlayerControls();
        KeyBindingManager.ApplyOverrides(playerControls);
    }

    private void OnEnable() => playerControls.Player.Enable();
    private void OnDisable() => playerControls.Player.Disable();

    void Start()
    {
        if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
    }

    void Update()
    {
        if (!playerIsInRange) return;

        if (playerControls.Player.Interact.WasPressedThisFrame())
        {
            if (characterSelectUI == null)
            {
                Debug.LogError("[角色雕像] CharacterSelectManager 未赋值！请在 Inspector 里拖入。");
                return;
            }
            if (characterData == null)
            {
                Debug.LogError("[角色雕像] CharacterData 未赋值！请在 Inspector 里拖入。");
                return;
            }

            // 切换面板：如果已经打开且显示的是同一个角色，则关闭；否则打开/切换
            if (characterSelectUI.IsOpen && characterSelectUI.CurrentCharacter == characterData)
            {
                characterSelectUI.ClosePanel();
            }
            else
            {
                characterSelectUI.ShowCharacter(characterData);
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
            if (characterSelectUI != null && characterSelectUI.IsOpen)
            {
                characterSelectUI.ClosePanel();
            }
        }
    }
}

