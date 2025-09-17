using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("功能设置")]
    [Tooltip("这个NPC关联的武器技能树")]
    public WeaponSkillTree skillTreeToOpen;

    [Header("UI提示")]
    [Tooltip("当玩家进入范围时显示的交互提示UI（例如一个'E'键图标）")]
    public GameObject interactionPromptUI;

    private bool playerIsInRange = false;

    void Start()
    {
        if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
    }

    void Update()
    {
        // 如果玩家在范围内，并且按下了交互键（这里以E键为例）
        if (playerIsInRange && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"与NPC交互，准备打开 {skillTreeToOpen.associatedWeapon.weaponName} 的技能树...");

            // 在这里，我们将调用UI管理器来打开技能树界面
            // UIManager.Instance.OpenSkillTree(skillTreeToOpen); // (这行代码将在我们完成UI后启用)
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