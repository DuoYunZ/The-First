using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 用于 FindObjectsOfType (或者用 GetComponentsInChildren)

// 挂载在 MechRoot 上
public class WeaponController : MonoBehaviour
{
    [Header("自动开火设置")]
    [Tooltip("是否启用自动开火")]
    public bool autoFire = true; // 让武器自动持续开火

    private List<WeaponPart> weaponParts = new List<WeaponPart>(); // 存储机甲上的所有武器部件
    private Camera mainCamera; // 战斗相机，用于获取鼠标方向

    void Start()
    {
        mainCamera = Camera.main; // 获取主相机
        if (mainCamera == null)
        {
            Debug.LogError("WeaponController: 未找到主摄像机!", this);
            enabled = false;
            return;
        }

        FindAndRegisterWeapons();
    }

    // (可选) 提供一个方法，当机甲结构变化时（比如移除/添加武器）重新查找武器
    public void FindAndRegisterWeapons()
    {
        Debug.Log("WeaponController: 查找武器部件...");
        // 从 MechRoot 的所有子对象中查找 WeaponPart (包括深层子对象)
        weaponParts = GetComponentsInChildren<WeaponPart>(true).ToList(); // true 表示包括非激活的？按需调整
        Debug.Log($"WeaponController: 找到了 {weaponParts.Count} 个武器部件。");
    }


    void Update()
    {
        if (!autoFire || weaponParts.Count == 0)
        {
            return; // 如果不自动开火或没有武器，则不执行
        }

        // --- 计算鼠标方向 ---
        Vector3 mouseWorldPos = Vector3.zero;
        bool mousePosValid = false;
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position); // 水平面
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            mouseWorldPos = mouseRay.GetPoint(distance);
            mousePosValid = true;
        }
        // -------------------

        if (mousePosValid)
        {
            // 计算从机甲中心指向鼠标的水平方向
            Vector3 targetDirection = mouseWorldPos - transform.position; // 用 MechRoot 的位置
            targetDirection.y = 0;
            targetDirection.Normalize();

            if (targetDirection.sqrMagnitude > 0.01f) // 确保方向有效
            {
                // --- 遍历所有武器并尝试开火 ---
                foreach (WeaponPart weapon in weaponParts)
                {
                    if (weapon != null && weapon.enabled) // 确保武器脚本是启用的
                    {
                        // WeaponPart 内部自己管理冷却计时器
                        // 直接调用 Fire，它会检查冷却
                        weapon.Fire(targetDirection);
                    }
                }
            }
        }
    }
}