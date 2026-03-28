using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UnlockNotificationUI : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("用于通过名字查找图标 (Icon) 的脚本，如果没有可以留空")]
    // public ItemDatabase itemDatabase; // 假设有一个 ItemDatabase 脚本
    public GameObject notificationPanel;
    public TextMeshProUGUI itemNameText;
    public Image itemIconImage;

    [Header("设置")]
    public float displayDuration = 3f;
    public float fadeDuration = 0.5f;

    private Queue<string> notificationQueue = new Queue<string>();
    private bool isDisplaying = false;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (notificationPanel != null)
        {
            canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
            }
            notificationPanel.SetActive(false);
        }
    }

    void OnEnable()
    {
        // 订阅解锁事件
        PlayerProgressManager.OnItemUnlocked += EnqueueNotification;
    }

    void OnDisable()
    {
        // 取消订阅
        PlayerProgressManager.OnItemUnlocked -= EnqueueNotification;
    }

    public void EnqueueNotification(string itemName)
    {
        notificationQueue.Enqueue(itemName);
        if (!isDisplaying)
        {
            StartCoroutine(DisplayNotificationRoutine());
        }
    }

    IEnumerator DisplayNotificationRoutine()
    {
        isDisplaying = true;

        while (notificationQueue.Count > 0)
        {
            string itemName = notificationQueue.Dequeue();
            ShowUI(itemName);
            
            // 淡入
            yield return FadeCanvasGroup(0f, 1f, fadeDuration);
            
            // 显示
            yield return new WaitForSeconds(displayDuration);
            
            // 淡出
            yield return FadeCanvasGroup(1f, 0f, fadeDuration);
        }

        notificationPanel.SetActive(false);
        isDisplaying = false;
    }

    private void ShowUI(string itemName)
    {
        if (notificationPanel == null) return;

        notificationPanel.SetActive(true);
        if (itemNameText != null) itemNameText.text = LocalizationManager.T("ui.unlocked", itemName);

        // 如果有图标逻辑，可以在这里更新
        // if (itemDatabase != null) itemIconImage.sprite = itemDatabase.GetIcon(itemName);
    }

    IEnumerator FadeCanvasGroup(float start, float end, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 以防游戏暂停
            canvasGroup.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = end;
    }
}
