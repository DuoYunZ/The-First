using UnityEngine;
using System.Collections.Generic;

public class WeaponHUDManager : MonoBehaviour
{
    [Header("配置")]
    public Transform uiContainer;      // 拖入 Canvas 里用来放图标的父节点 (例如一个 HorizontalLayoutGroup)
    public GameObject weaponSlotPrefab; // 拖入你做好的 WeaponSlot UI 预制体 (挂了 WeaponStatusSlot 脚本的那个)

    private List<WeaponStatusSlot> activeSlots = new List<WeaponStatusSlot>();

    void Start()
    {
        // 延迟一帧初始化，确保玩家和武器都已经生成好了
        Invoke(nameof(InitWeaponUI), 0.1f);
    }

    void InitWeaponUI()
    {
        // 1. 找到玩家 (假设有 GameManager 或者直接找 Tag)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // 2. 找到玩家身上所有的武器
        WeaponPart[] weapons = player.GetComponentsInChildren<WeaponPart>();

        foreach (var weapon in weapons)
        {
            // 过滤掉没激活的，或者召唤物自带的武器
            if (!weapon.gameObject.activeInHierarchy) continue;
            if (weapon.StatBlock == null) continue;
            // 如果不想显示“无人机”这种子武器，可以在这里加过滤条件

            CreateSlotForWeapon(weapon);
        }
    }

    public void CreateSlotForWeapon(WeaponPart weapon)
    {
        if (weaponSlotPrefab == null || uiContainer == null) return;

        // 1. 生成 UI
        GameObject slotObj = Instantiate(weaponSlotPrefab, uiContainer);

        // 2. 获取脚本
        WeaponStatusSlot slotScript = slotObj.GetComponent<WeaponStatusSlot>();
        if (slotScript != null)
        {
            // 3. 【核心步骤】绑定数据！
            // 这会让 UI 开始监听这把武器的经验变化
            slotScript.BindWeapon(weapon);
            activeSlots.Add(slotScript);
        }
    }
}