// --- PlayerWeaponManager.cs (最终修正版 V2) ---
using UnityEngine;

public class PlayerWeaponManager : MonoBehaviour
{
    [Header("武器预制件")]
    public GameObject floatingWeaponPrefab;

    [Header("挂点设置")]
    public Transform weaponFollowTarget;
    [Tooltip("指定武器实例化后的父对象，应设为Visuals")]
    public Transform weaponParent; // 【新增】用于指定父对象的变量

    void Start()
    {
        if (floatingWeaponPrefab == null || weaponFollowTarget == null)
        {
            Debug.LogError("PlayerWeaponManager: 未设置浮游武器Prefab或武器的跟随目标点 (Weapon Follow Target)！", this);
            return;
        }

        Transform parent = weaponParent != null ? weaponParent : transform;

        // 1. 实例化武器
        GameObject weaponInstance = Instantiate(floatingWeaponPrefab, weaponFollowTarget.position, weaponFollowTarget.rotation, parent);

        // 2. 【安全保障】确保实例化的对象是激活状态，这样它的 Awake() 才会执行
        weaponInstance.SetActive(true);

        // 3. 获取新武器实例上的所有必要组件
        FloatingWeaponController weaponController = weaponInstance.GetComponent<FloatingWeaponController>();
        WeaponCooldownMaterial cooldownMaterial = weaponInstance.GetComponent<WeaponCooldownMaterial>(); // <--- 获取材质控制器

        // 4. 获取玩家身上的攻击脚本
        PlayerBladeAttack attackScript = GetComponent<PlayerBladeAttack>();

        // 5. 【核心修正】建立所有必需的引用链接
        if (attackScript != null)
        {
            if (weaponController != null)
            {
                // 链接1：让攻击脚本能控制武器显隐
                attackScript.floatingWeapon = weaponController;
            }

            if (cooldownMaterial != null)
            {
                // 链接2：让攻击脚本能控制材质冷却效果
                attackScript.weaponCooldownMaterial = cooldownMaterial;
            }
        }

        // 6. 设置武器自身的跟随目标
        if (weaponController != null)
        {
            weaponController.targetToFollow = this.weaponFollowTarget;
        }

        // (可选建议) 避免将实例命名为与场景中其他物体相同的名字，以防混淆
        weaponInstance.name = "FloatingWeapon_Instance";
    }
}