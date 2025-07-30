using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("角色数据列表")]
    [Tooltip("将所有可选择的角色Data资产拖拽到这里")]
    public List<CharacterData> availableCharacters;

    public Transform characterPreviewContainer;
    private GameObject currentPreviewModel;

    [Header("UI 元素引用")]
    public Image characterIconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public Button previousButton;
    public Button nextButton;
    public Button startButton;

    [Header("场景设置")]
    [Tooltip("要加载的战斗场景名称")]
    public string combatSceneName = "CombatArena01";

    // 内部变量
    private int currentCharacterIndex = 0;

    void Start()
    {
        // 为按钮绑定点击事件
        previousButton.onClick.AddListener(PreviousCharacter);
        nextButton.onClick.AddListener(NextCharacter);
        startButton.onClick.AddListener(StartCombat);

        // 检查是否有可选角色
        if (availableCharacters == null || availableCharacters.Count == 0)
        {
            Debug.LogError("没有可选择的角色数据！请在CharacterSelectManager的Inspector中设置。");
            // 禁用所有按钮
            previousButton.interactable = false;
            nextButton.interactable = false;
            startButton.interactable = false;
            return;
        }

        // 初始化UI，显示第一个角色
        SelectCharacter(0);
    }

    // 切换到指定索引的角色并更新UI
    private void SelectCharacter(int index)
    {
        if (index < 0 || index >= availableCharacters.Count) return;
        currentCharacterIndex = index;
        CharacterData selectedChar = availableCharacters[(int)currentCharacterIndex];
        // 更新UI显示
        characterIconImage.sprite = selectedChar.characterIcon;
        nameText.text = selectedChar.characterName;
        descriptionText.text = selectedChar.description;
        // 更新按钮状态
        previousButton.interactable = (availableCharacters.Count > 1);
        nextButton.interactable = (availableCharacters.Count > 1);
        // 处理3D预览模型
        if (currentPreviewModel != null)
        {
            Destroy(currentPreviewModel);
        }
        if (selectedChar.characterPreviewPrefab != null && characterPreviewContainer != null)
        {
            currentPreviewModel = Instantiate(selectedChar.characterPreviewPrefab, characterPreviewContainer);
            SetLayerRecursively(currentPreviewModel, LayerMask.NameToLayer("UI_CharacterPreview"));
            // 确保模型在正确的位置和旋转
            currentPreviewModel.transform.localPosition = Vector3.zero;
            currentPreviewModel.transform.localRotation = Quaternion.identity;
            // 尝试获取 Animator 组件并播放待机动画
            Animator previewAnimator = currentPreviewModel.GetComponent<Animator>();
            if (previewAnimator != null)
            {
                // 假设您的待机动画状态名称是 "Idle"
                previewAnimator.Play("Idle");
            }
            else
            {
                Debug.LogWarning($"角色 '{selectedChar.characterName}' 的预览模型上没有 Animator 组件。");
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
    // “上一个角色”按钮的响应方法
    public void PreviousCharacter()
    {
        int newIndex = currentCharacterIndex - 1;
        if (newIndex < 0)
        {
            newIndex = availableCharacters.Count - 1; // 循环到最后一个
        }
        SelectCharacter(newIndex);
    }

    // “下一个角色”按钮的响应方法
    public void NextCharacter()
    {
        int newIndex = currentCharacterIndex + 1;
        if (newIndex >= availableCharacters.Count)
        {
            newIndex = 0; // 循环到第一个
        }
        SelectCharacter(newIndex);
    }

    // “开始战斗”按钮的响应方法
    public void StartCombat()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogError("DataManager 未找到！无法开始游戏。");
            return;
        }

        // 将当前选择的角色数据存入 DataManager
        DataManager.Instance.selectedCharacter = availableCharacters[currentCharacterIndex];

        Debug.Log($"选择了角色: {DataManager.Instance.selectedCharacter.characterName}，准备进入战斗场景...");

        // 加载战斗场景
        SceneManager.LoadScene(combatSceneName);
    }
}