using UnityEngine;

/// <summary>
/// 首次进入提示 — 挂到新手提示的根物体上
/// 仅在第一次进入 Hub 时显示，之后自动隐藏
/// </summary>
public class FirstTimeHint : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("用于存档判断的唯一 Key（不同提示可用不同 Key）")]
    public string prefsKey = "hub_tutorial_shown";

    [Tooltip("显示持续时间（秒），0 = 需要手动关闭")]
    public float autoDismissTime = 0f;

    void Start()
    {
        // 检查是否已经看过
        if (PlayerPrefs.GetInt(prefsKey, 0) == 1)
        {
            // 已看过，直接隐藏
            gameObject.SetActive(false);
            return;
        }

        // 首次显示，标记为已看过
        PlayerPrefs.SetInt(prefsKey, 1);
        PlayerPrefs.Save();

        // 如果设置了自动消失时间
        if (autoDismissTime > 0f)
        {
            Invoke(nameof(Dismiss), autoDismissTime);
        }
    }

    /// <summary>
    /// 手动关闭（可绑定到关闭按钮的 OnClick）
    /// </summary>
    public void Dismiss()
    {
        gameObject.SetActive(false);
    }
}
