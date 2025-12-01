// WeaponController.cs (最终角色选择流程版)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class OwnedWeapon
{
    public WeaponStatBlock stats;
    public int currentLevel = 1;
    public WeaponPart weaponPartInstance;
}

public class WeaponController : MonoBehaviour
{
    public static WeaponController Instance { get; private set; }

    // 【修改】我们不再需要将其设为 public，将在代码中自动查找
    private Transform weaponMountPoint;

    [Header("静态/自带武器")]
    [Tooltip("将玩家身上自带的“刀光” 武器（即挂载了 PlayerBladeAttack 和 WeaponPart 脚本的那个GameObject）拖到这里")]
    public WeaponPart builtInBladeWeapon;

    [Header("自动开火设置")]
    public bool autoFire = true;

    [Header("瞄准设置")]
    public float aimTurnSpeed = 25f;

    public List<OwnedWeapon> ownedWeapons = new List<OwnedWeapon>();

    [Header("进化系统")]
    [Tooltip("这里放入所有创建好的配方文件")]
    public List<EvolutionRecipeSO> allEvolutionRecipes;

    private Camera mainCamera;
    private PlayerControls playerControls;
    private Vector2 lookInput;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        playerControls = new PlayerControls();

        weaponMountPoint = transform.Find("WeaponMounts");
        if (weaponMountPoint == null)
        {
            // 如果找不到挂载点，为了防止自带武器报错，我们临时用自己的 Transform
            weaponMountPoint = transform;
            // Debug.LogWarning("[WeaponController] 未找到 'WeaponMounts'，将使用自身作为挂载点。");
        }
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
    }

    void Start()
    {
        mainCamera = Camera.main;

        // --- 【核心重构】初始化自带武器 ---
        if (builtInBladeWeapon != null)
        {
            // 检查是否已经在列表里了（防止重复）
            if (!ownedWeapons.Any(w => w.weaponPartInstance == builtInBladeWeapon))
            {
                OwnedWeapon initialWeapon = new OwnedWeapon
                {
                    stats = builtInBladeWeapon.StatBlock,
                    currentLevel = builtInBladeWeapon.currentLevel,
                    weaponPartInstance = builtInBladeWeapon
                };

                // 把它加到列表第一个位置
                ownedWeapons.Insert(0, initialWeapon);
                Debug.Log($"[WeaponController] 系统初始化：已将自带武器 '{initialWeapon.stats.weaponName}' 注册为 OwnedWeapon。");
            }
        }
        // ------------------------------
    }

    public void TryUpgradeWeapon(string weaponName)
    {
        // --- 【核心重构】统一查找逻辑 ---
        // 直接在列表里找，不需要分情况讨论了
        var targetWeaponData = ownedWeapons.FirstOrDefault(w => w.stats != null && w.stats.weaponName == weaponName);

        if (targetWeaponData == null)
        {
            Debug.LogError($"找不到名为 {weaponName} 的武器数据!");
            return;
        }

        WeaponPart targetPart = targetWeaponData.weaponPartInstance;
        if (targetPart == null) return;

        // 1. 进化检查
        if (targetPart.currentLevel >= 5 && targetPart.currentStone != null)
        {
            EvolutionRecipeSO recipe = FindValidRecipe(targetPart);
            if (recipe != null)
            {
                EvolveWeapon(targetPart, recipe, targetWeaponData); // 传入数据引用以便更新
                return;
            }
        }

        // 2. 普通升级
        targetPart.currentLevel++;
        targetWeaponData.currentLevel++; // 同步更新数据
        Debug.Log($"普通升级完成: {weaponName} -> Lv.{targetPart.currentLevel}");
    }

    private EvolutionRecipeSO FindValidRecipe(WeaponPart weapon)
    {
        if (weapon.currentStone == null) return null;
        foreach (var recipe in allEvolutionRecipes)
        {
            if (recipe.baseWeapon != weapon.StatBlock) continue;
            if (weapon.currentStone.stoneEffects.Contains(recipe.requiredStoneType)) return recipe;
        }
        return null;
    }

    public EvolutionRecipeSO GetPendingEvolution()
    {
        foreach (var owned in ownedWeapons)
        {
            // 必须满级 + 有石头
            if (owned.weaponPartInstance != null &&
                owned.weaponPartInstance.currentLevel >= 5 &&
                owned.weaponPartInstance.currentStone != null)
            {
                // 查找匹配的配方
                EvolutionRecipeSO recipe = FindValidRecipe(owned.weaponPartInstance);
                if (recipe != null)
                {
                    return recipe;
                }
            }
        }
        return null;
    }

    private void EvolveWeapon(WeaponPart weaponPart, EvolutionRecipeSO recipe, OwnedWeapon weaponData)
    {
        Debug.Log($"<color=cyan>【武器进化】{weaponPart.StatBlock.weaponName} -> {recipe.evolvedWeapon.weaponName}</color>");

        // 1. 替换核心数据
        weaponPart.StatBlock = recipe.evolvedWeapon;

        // 【重要】同步更新 OwnedWeapon 里的数据引用，防止下次升级找不到
        if (weaponData != null) weaponData.stats = recipe.evolvedWeapon;

        // 2. 重置等级
        weaponPart.currentLevel = 1;
        if (weaponData != null) weaponData.currentLevel = 1;

        // 3. 消耗石头
        weaponPart.RemoveEnergyStone();

        // 4. 刷新状态
        weaponPart.RefreshWeaponStateFromStone();

        // 5. 【关键】禁用旧的挥砍脚本
        // 这会让 AimAndFire 中的逻辑切换，开始调用 .Fire()
        var meleeAttackScript = weaponPart.GetComponent<PlayerBladeAttack>();
        if (meleeAttackScript != null)
        {
            Debug.Log("禁用 PlayerBladeAttack，转由 WeaponPart 接管远程攻击。");
            meleeAttackScript.enabled = false;
        }
    }
    // AddNewWeapon 方法现在可以安全地使用 weaponMountPoint 了
    public void AddNewWeapon(WeaponStatBlock weaponData)
    {
        if (weaponData == null || weaponMountPoint == null) return;

        // 这个检查现在变得至关重要，如果 Awake 中没找到，这里会直接返回
        if (ownedWeapons.Any(w => w.stats.weaponName == weaponData.weaponName)) return;

        // ... (检查是否已拥有的逻辑保持不变) ...

        GameObject weaponPartGO = Instantiate(weaponData.weaponPartPrefab, weaponMountPoint);
        WeaponPart part = weaponPartGO.GetComponent<WeaponPart>();

        if (part != null)
        {
            part.StatBlock = weaponData;
            part.currentLevel = 1;
            part.Activate();

            ownedWeapons.Add(new OwnedWeapon
            {
                stats = weaponData,
                currentLevel = 1,
                weaponPartInstance = part
            });
        }

            if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }

        part.Activate();
        Debug.Log($"[WeaponController] 成功装备全新武器: '{weaponData.weaponName}'。");
    }
    public void RefreshAllWeaponStates()
    {
        foreach (OwnedWeapon owned in ownedWeapons)
        {
            if (owned.weaponPartInstance != null)
            {
                owned.weaponPartInstance.RefreshWeaponStateFromStone();
            }
        }
    }
    // Update, AimAndFire 等其他方法保持不变...
    void Update()
    {
        // 【核心修复】现在只需要检查 ownedWeapons 数量即可
        // 因为自带武器已经在列表里了，所以 Count 至少为 1
        if (!autoFire || ownedWeapons.Count == 0) return;

        Vector3 aimDirection = Vector3.zero;
        bool hasAimInput = false;

#if UNITY_STANDALONE || UNITY_EDITOR
        if (mainCamera != null && Mouse.current != null)
        {
            Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            if (groundPlane.Raycast(mouseRay, out float distance))
            {
                Vector3 mouseWorldPos = mouseRay.GetPoint(distance);
                // 使用 transform.position 更加稳定，防止挂载点旋转导致的抖动
                aimDirection = mouseWorldPos - transform.position;
                aimDirection.y = 0;
                if (aimDirection.sqrMagnitude > 0.01f) hasAimInput = true;
            }
        }
#endif

        if (hasAimInput)
        {
            AimAndFire(aimDirection);
        }
    }

    private void AimAndFire(Vector3 targetDirection)
    {
        targetDirection.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        // 旋转挂载点 (如果有)
        if (weaponMountPoint != null)
        {
            weaponMountPoint.rotation = Quaternion.Slerp(weaponMountPoint.rotation, targetRotation, aimTurnSpeed * Time.deltaTime);
        }

        Vector3 fireDirection = targetDirection; // 直接使用瞄准方向

        // --- 【核心重构】统一循环遍历 ---
        // 不再单独处理 builtInBladeWeapon，所有人都在这个循环里
        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            var weapon = ownedWeapons[i];
            if (weapon.weaponPartInstance != null && weapon.weaponPartInstance.enabled)
            {
                // 【智能防冲突逻辑】
                // 检查这个武器是否挂载了 PlayerBladeAttack (旧挥砍脚本)
                var meleeScript = weapon.weaponPartInstance.GetComponent<PlayerBladeAttack>();

                // 如果旧脚本存在 且 开启中，说明还没进化
                // 此时不调用 Fire()，让 PlayerBladeAttack 自己控制挥砍
                if (meleeScript != null && meleeScript.enabled)
                {
                    continue;
                }

                // 如果旧脚本不存在(是捡来的枪) 或者 被禁用了(已进化成风刃)
                // 则由 Controller 统一控制开火！
                weapon.weaponPartInstance.Fire(fireDirection);
            }
        }
    }

    // 我们不再需要 RegisterExistingPart 方法，因为武器都是在新流程中动态添加的
    // public void RegisterExistingPart(WeaponPart partInstance) { ... }
}