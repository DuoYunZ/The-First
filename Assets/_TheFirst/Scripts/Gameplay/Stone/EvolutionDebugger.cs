using UnityEngine;
using UnityEngine.InputSystem; // <--- 【关键 1】引入新输入系统的命名空间

public class EvolutionDebugger : MonoBehaviour
{
    [Header("测试设置")]
    public string weaponToTest = "斩击";
    public EnergyStoneSO stoneToGive;

    void Update()
    {
        // --- 【关键 2】使用新输入系统的检测方式 ---

        // 检测 T 键：模拟升级
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (WeaponController.Instance != null)
            {
                Debug.Log($"[测试] 按下T键 -> 尝试升级 {weaponToTest}...");
                WeaponController.Instance.TryUpgradeWeapon(weaponToTest);
            }
        }

        // 检测 Y 键：模拟插石头
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            if (WeaponController.Instance != null && stoneToGive != null)
            {
                WeaponPart part = FindWeaponPart(weaponToTest);
                if (part != null)
                {
                    Debug.Log($"[测试] 按下Y键 -> 给 {weaponToTest} 强行插入 {stoneToGive.stoneName}");
                    part.FuseEnergyStone(stoneToGive);
                }
                else
                {
                    Debug.LogWarning($"[测试失败] 找不到名为 '{weaponToTest}' 的武器组件，请检查名字是否匹配！");
                }
            }
        }
    }

    private WeaponPart FindWeaponPart(string name)
    {
        if (WeaponController.Instance == null) return null;
        var parts = WeaponController.Instance.GetComponentsInChildren<WeaponPart>();
        foreach (var p in parts)
        {
            // 防止空引用报错
            if (p.StatBlock != null && p.StatBlock.weaponName == name) return p;
        }
        return null;
    }
}