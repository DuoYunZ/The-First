using System.Collections.Generic; // 用于 List
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 用于 UI 元素

public class FusionUIManager : MonoBehaviour
{
    public static FusionUIManager Instance { get; private set; }

    [Header("UI 面板")]
    [Tooltip("包含所有融合UI元素的父面板")]
    public GameObject fusionPanel;

    [Header("能量石信息 (UI)")]
    public Image stoneIcon;
    public TextMeshProUGUI stoneNameText;
    public TextMeshProUGUI stoneDescriptionText;

    [Header("武器选择 (UI)")]
    [Tooltip("显示武器图标的Image")]
    public Image weaponIcon; // (例如截图 中灰色方块)
    [Tooltip("显示武器名称的Text (例如 '光环')")]
    public TextMeshProUGUI weaponNameText;
    [Tooltip("显示插槽状态的Text (例如 '[ 空插槽 ]')")]
    public TextMeshProUGUI weaponSlotText;
    [Tooltip("“上一个”武器按钮 (<)")]
    public Button prevWeaponButton;
    [Tooltip("“下一个”武器按钮 (>)")]
    public Button nextWeaponButton;
    [Tooltip("“融合”按钮")]
    public Button fuseButton;

    private EnergyStoneSO pendingStone;
    private List<WeaponPart> availableWeapons = new List<WeaponPart>();
    private int currentWeaponIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }

        if (prevWeaponButton != null)
            prevWeaponButton.onClick.AddListener(SelectPreviousWeapon);
        if (nextWeaponButton != null)
            nextWeaponButton.onClick.AddListener(SelectNextWeapon);
        if (fuseButton != null)
            fuseButton.onClick.AddListener(OnFuseClicked);

        fusionPanel.SetActive(false); // 默认隐藏
    }

    /// <summary>
    /// 由 EnergyStonePickup 调用
    /// </summary>
    public void StartFusion(EnergyStoneSO stoneToFuse)
    {
        this.pendingStone = stoneToFuse;
        if (this.pendingStone == null) return;

        // 1. 暂停游戏
        Time.timeScale = 0f;

        // 2. 更新能量石信息 (保持不变)
        stoneIcon.sprite = pendingStone.icon;
        stoneNameText.text = pendingStone.stoneName;
        stoneDescriptionText.text = pendingStone.description;

        // 3. [新逻辑] 填充可用的武器列表
        availableWeapons.Clear();

        var controller = WeaponController.Instance; //
        if (controller == null)
        {
            Debug.LogError("FusionUIManager: 找不到 WeaponController!");
            // (安全起见，关闭面板)
            OnFuseClicked(); // (调用 OnFuseClicked 来关闭面板并恢复游戏)
            return;
        }
        if (controller.builtInBladeWeapon != null)
        {
            availableWeapons.Add(controller.builtInBladeWeapon);
        }

        var ownedWeapons = WeaponController.Instance.ownedWeapons; //
        foreach (var ownedWeapon in ownedWeapons)
        {
            if (ownedWeapon.weaponPartInstance != null) //
            {
                availableWeapons.Add(ownedWeapon.weaponPartInstance); //
            }
        }

        // 4. [新逻辑] 显示第一个武器
        currentWeaponIndex = 0;
        UpdateWeaponDisplay();

        // 5. 显示面板
        fusionPanel.SetActive(true);
    }

    private void UpdateWeaponDisplay()
    {
        if (availableWeapons.Count == 0)
        {
            // (处理没有武器的罕见情况)
            weaponNameText.text = "没有武器";
            weaponSlotText.text = "";
            weaponIcon.sprite = null; // (或一个默认的 'X' 图标)
            fuseButton.interactable = false; // 禁用融合按钮
            return;
        }

        fuseButton.interactable = true; // 确保按钮可用

        // 获取当前选中的武器
        WeaponPart selectedWeapon = availableWeapons[currentWeaponIndex]; //
        if (selectedWeapon == null || selectedWeapon.StatBlock == null) return; //

        // 更新UI
        weaponNameText.text = selectedWeapon.StatBlock.weaponName; //                                                         
                                                                 
        weaponIcon.sprite = selectedWeapon.StatBlock.weaponIcon; 

        if (selectedWeapon.currentStone != null) //
        {
            weaponSlotText.text = $"[已镶嵌: {selectedWeapon.currentStone.stoneName}]"; //
        }
        else
        {
            weaponSlotText.text = "[ 空插槽 ]"; //
        }
    }

    /// <summary>
    /// (新) ' > ' 按钮调用的方法
    /// </summary>
    public void SelectNextWeapon()
    {
        if (availableWeapons.Count == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % availableWeapons.Count; // 循环到下一个
        UpdateWeaponDisplay();
    }

    /// <summary>
    /// (新) ' < ' 按钮调用的方法
    /// </summary>
    public void SelectPreviousWeapon()
    {
        if (availableWeapons.Count == 0) return;
        currentWeaponIndex--;
        if (currentWeaponIndex < 0)
        {
            currentWeaponIndex = availableWeapons.Count - 1; // 循环到末尾
        }
        UpdateWeaponDisplay();
    }

    /// <summary>
    /// (新) “融合” 按钮调用的方法
    /// </summary>
    public void OnFuseClicked()
    {
        if (pendingStone == null || availableWeapons.Count == 0) return;

        // 1. 获取当前选中的武器
        WeaponPart selectedWeapon = availableWeapons[currentWeaponIndex]; //

        // 2. 执行融合
        selectedWeapon.FuseEnergyStone(pendingStone); //

        if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }

        // 3. 清理和恢复游戏
        pendingStone = null;
        availableWeapons.Clear();
        fusionPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}