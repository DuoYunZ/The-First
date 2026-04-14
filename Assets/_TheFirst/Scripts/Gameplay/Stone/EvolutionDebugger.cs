using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class EvolutionDebugger : MonoBehaviour
{
    [Header("测试资源")]
    [Tooltip("按Y键时，要强行塞给武器的石头")]
    public EnergyStoneSO stoneToGive;

    void Update()
    {
        // --- 1. 按 T 键：将所有已拥有的武器直接升到 5 级 ---
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (WeaponController.Instance != null)
            {
                // 遍历所有持有的武器数据 (OwnedWeapon)
                foreach (var ownedWrapper in WeaponController.Instance.ownedWeapons)
                {
                    // 1. 修改数据层面的等级
                    // (注意：OwnedWeapon 通常有 stats 或 data 字段，也应该有 currentLevel)
                    if (ownedWrapper.currentLevel < 8)
                    {
                        ownedWrapper.currentLevel = 8;
                    }

                    // 2. 同步给场景里的实体 (WeaponPart)
                    // 【关键修复】这里需要通过 assignedPart (或你代码里的名字) 来访问 WeaponPart
                    var part = ownedWrapper.weaponPartInstance;

                    if (part != null)
                    {
                        // 强制把 WeaponPart 里的等级也同步了
                        part.currentLevel = ownedWrapper.currentLevel;
                    }
                }

                // 刷新一下状态，确保进化检查被触发
                WeaponController.Instance.RefreshAllWeaponStates();
            }
        }

        // --- 2. 按 Y 键：给所有武器插上石头 ---
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            if (WeaponController.Instance != null && stoneToGive != null)
            {
                foreach (var ownedWrapper in WeaponController.Instance.ownedWeapons)
                {
                    // 【关键修复】通过 assignedPart 找到实体，调用融合方法
                    var part = ownedWrapper.weaponPartInstance;
                    if (part != null)
                    {
                        part.FuseEnergyStone(stoneToGive);
                    }
                }
            }
        }

        // --- 3. 按 U 键：强制触发一次升级面板（检查是否弹出进化卡） ---
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            if (PlayerLevelManager.Instance != null)
            {
                // 【关键修复】随便加点经验触发升级，不用管 currentLevelXP 叫什么
                PlayerLevelManager.Instance.AddExperience(100);
            }
        }
    }
}