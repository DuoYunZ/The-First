using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponStatusSlot : MonoBehaviour
{
    [Header("UI 组件")]
    public Image iconImage;
    public TextMeshProUGUI levelText;
    public Slider expSlider;
    public TextMeshProUGUI nameText;

    private WeaponPart boundWeapon;

    // 初始化：把 UI 和具体的武器绑定
    public void BindWeapon(WeaponPart weapon)
    {
        boundWeapon = weapon;

        // 1. 初始化基础信息
        if (boundWeapon.StatBlock != null)
        {
            if (iconImage != null) iconImage.sprite = boundWeapon.StatBlock.weaponIcon;
            if (nameText != null) nameText.text = boundWeapon.StatBlock.weaponName;
        }

        // 2. 订阅事件 (核心！)
        // 当武器经验变化时，更新滑动条
        boundWeapon.OnWeaponXpChanged += UpdateExpBar;
        // 当武器升级时，更新等级文字
        boundWeapon.OnWeaponLevelUp += UpdateLevelText;

        // 3. 初始刷新一次
        UpdateExpBar(boundWeapon.currentProficiencyXP, boundWeapon.xpToNextLevel);
        UpdateLevelText(boundWeapon.currentLevel);
    }

    private void UpdateExpBar(float current, float max)
    {
        if (expSlider != null)
        {
            // 防止除以0
            float value = (max > 0) ? current / max : 1;
            expSlider.value = value;
        }
    }

    private void UpdateLevelText(int newLevel)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv.{newLevel}";
        }
        // 升级时可以加个简单的 DoTween 动画或者特效
    }

    private void OnDestroy()
    {
        // 记得解绑事件，防止报错
        if (boundWeapon != null)
        {
            boundWeapon.OnWeaponXpChanged -= UpdateExpBar;
            boundWeapon.OnWeaponLevelUp -= UpdateLevelText;
        }
    }
}