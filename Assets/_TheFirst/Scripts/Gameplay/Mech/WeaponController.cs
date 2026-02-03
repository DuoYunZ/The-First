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

    private Transform weaponMountPoint;

    [Header("静态/自带武器")]
    public WeaponPart builtInBladeWeapon;

    [Header("自动开火设置")]
    public bool autoFire = true;

    [Header("瞄准设置")]
    public float aimTurnSpeed = 25f;

    [Header("武器库")]
    public List<OwnedWeapon> ownedWeapons = new List<OwnedWeapon>();

    [Header("融合系统 (Fusion)")]
    [Tooltip("在这里配置所有的融合配方 (A+B=C)")]
    public List<FusionRecipeSO> fusionRecipes; // <--- 【修改】使用新的配方列表

    public HashSet<WeaponStatBlock> banList = new HashSet<WeaponStatBlock>();

    private Camera mainCamera;
    private PlayerControls playerControls;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        playerControls = new PlayerControls();

        weaponMountPoint = transform.Find("WeaponMounts");
        if (weaponMountPoint == null)
        {
            weaponMountPoint = transform;
        }
    }

    private void OnEnable() => playerControls.Player.Enable();
    private void OnDisable() => playerControls.Player.Disable();

    void Start()
    {
        mainCamera = Camera.main;

        // 初始化自带武器
        if (builtInBladeWeapon != null)
        {
            if (!ownedWeapons.Any(w => w.weaponPartInstance == builtInBladeWeapon))
            {
                OwnedWeapon initialWeapon = new OwnedWeapon
                {
                    stats = builtInBladeWeapon.StatBlock,
                    currentLevel = builtInBladeWeapon.currentLevel,
                    weaponPartInstance = builtInBladeWeapon
                };
                ownedWeapons.Insert(0, initialWeapon);
                builtInBladeWeapon.Activate(); // 确保激活
            }
        }
    }

    // --- 升级逻辑 ---
    public void TryUpgradeWeapon(string weaponName)
    {
        var targetWeaponData = ownedWeapons.FirstOrDefault(w => w.stats != null && w.stats.weaponName == weaponName);

        if (targetWeaponData == null)
        {
            Debug.LogError($"找不到名为 {weaponName} 的武器数据!");
            return;
        }

        WeaponPart targetPart = targetWeaponData.weaponPartInstance;

        // 简单升级逻辑 (不再在这里检查进化，进化移交给了融合系统)
        if (targetPart.currentLevel < targetWeaponData.stats.maxLevel)
        {
            targetPart.currentLevel++;
            targetWeaponData.currentLevel++;
            Debug.Log($"普通升级完成: {weaponName} -> Lv.{targetPart.currentLevel}");
        }
        else
        {
            // 可以在这里给予金币或回血作为满级补偿
            Debug.Log($"{weaponName} 已满级!");
        }
    }

    // --- 【核心新增】融合系统 API ---

    /// <summary>
    /// 检查当前是否满足任意一个融合配方。
    /// 通常由宝箱 (TreasureChest) 调用。
    /// </summary>
    /// <returns>返回满足条件的配方，如果没有则返回 null</returns>
    public FusionRecipeSO CheckForAvailableFusion()
    {
        foreach (var recipe in fusionRecipes)
        {
            // 1. 检查是否拥有配方中的武器 A 和 B
            OwnedWeapon weaponA = ownedWeapons.FirstOrDefault(w => w.stats == recipe.weaponA);
            OwnedWeapon weaponB = ownedWeapons.FirstOrDefault(w => w.stats == recipe.weaponB);

            if (weaponA != null && weaponB != null)
            {
                // 2. 检查是否都达到满级 (假设满级是 8 或 WeaponStatBlock.maxLevel)
                // 为了保险，我们检查它是否达到该武器设定的 maxLevel
                bool isAMaxed = weaponA.currentLevel >= weaponA.stats.maxLevel;
                bool isBMaxed = weaponB.currentLevel >= weaponB.stats.maxLevel;

                if (isAMaxed && isBMaxed)
                {
                    Debug.Log($"<color=cyan>发现可融合配方: {recipe.weaponA.weaponName} + {recipe.weaponB.weaponName} -> {recipe.resultWeapon.weaponName}</color>");
                    return recipe;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 执行融合：移除 A 和 B，添加 C。
    /// </summary>
    public void PerformFusion(FusionRecipeSO recipe)
    {
        if (recipe == null) return;

        Debug.Log($"<color=yellow>开始执行融合: {recipe.resultWeapon.weaponName}</color>");

        // 1. 查找并移除旧武器
        OwnedWeapon weaponA = ownedWeapons.FirstOrDefault(w => w.stats == recipe.weaponA);
        OwnedWeapon weaponB = ownedWeapons.FirstOrDefault(w => w.stats == recipe.weaponB);

        // 【新增】将它们加入黑名单
        if (weaponA != null) banList.Add(weaponA.stats);
        if (weaponB != null) banList.Add(weaponB.stats);

        RemoveWeapon(weaponA);
        RemoveWeapon(weaponB);

        // 2. 添加新武器 (超武)
        AddNewWeapon(recipe.resultWeapon);

        // 3. 刷新 UI
        if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }
    }

    public void EvolveWeapon(WeaponStatBlock baseStats, WeaponStatBlock targetStats)
    {
        // 1. 找到旧武器
        var oldWeaponWrapper = ownedWeapons.FirstOrDefault(w => w.stats == baseStats);

        if (oldWeaponWrapper != null)
        {
            Debug.Log($"[WeaponController] 执行进化: {baseStats.weaponName} -> {targetStats.weaponName}");

            // 2. 彻底销毁旧武器的物体
            // 这一点至关重要！因为闪电链的 Prefab 和雷击的 Prefab 结构完全不同
            if (oldWeaponWrapper.weaponPartInstance != null)
            {
                Destroy(oldWeaponWrapper.weaponPartInstance.gameObject);
            }

            // 3. 从列表中移除旧数据
            ownedWeapons.Remove(oldWeaponWrapper);

            // 4. 添加新武器 (会自动实例化新的 Prefab)
            AddNewWeapon(targetStats);

            // 5. 【可选】局外解锁逻辑 (保存到 PlayerProgressManager)
            if (PlayerProgressManager.Instance != null)
            {
                // 确保新武器的 ID 记录到存档
                string newID = targetStats.weaponID;
                if (!string.IsNullOrEmpty(newID) && !PlayerProgressManager.Instance.unlockedItems.Contains(newID))
                {
                    Debug.Log($"[系统] 进化解锁新图鉴: {targetStats.weaponName}");
                    PlayerProgressManager.Instance.UnlockItem(newID);
                }

                // 记录成就 (如 Evolve_ChainLightning)
                string achievementKey = "Evolve_" + newID;
                PlayerProgressManager.Instance.IncreaseAchievementStat(achievementKey, 1);
            }
        }
        else
        {
            Debug.LogError($"[WeaponController] 进化失败：找不到基础武器 {baseStats.weaponName}");
        }
    }

    private void RemoveWeapon(OwnedWeapon weaponToRemove)
    {
        if (weaponToRemove == null) return;

        WeaponPart part = weaponToRemove.weaponPartInstance;

        if (part != null)
        {
            // --- 【核心修复】防自杀检查 ---
            // 检查这个武器的 GameObject 是否就是玩家自己 (WeaponController 所在的物体)
            if (part.gameObject == this.gameObject)
            {
                Debug.LogWarning($"[WeaponController] 试图移除挂在玩家身上的初始武器: {part.name}。只禁用组件，不销毁物体！");

                // 1. 禁用脚本组件 (停止 Update)
                part.enabled = false;

                // 2. 如果有 PlayerBladeAttack (近战脚本)，也禁用掉
                var melee = part.GetComponent<PlayerBladeAttack>();
                if (melee != null) melee.enabled = false;

                // 3. 尝试隐藏视觉模型 (如果有独立引用的 FloatingVisual)
                if (part.floatingVisual != null)
                {
                    // 只隐藏模型，不关整个物体
                    part.floatingVisual.HideWeapon();
                    part.floatingVisual.gameObject.SetActive(false);
                }
            }
            // 如果是自带武器引用 (作为子物体)，通常我们只隐藏不销毁
            else if (part == builtInBladeWeapon)
            {
                part.gameObject.SetActive(false);
            }
            // 其他情况：是后来生成的独立子物体，可以安全销毁
            else
            {
                Destroy(part.gameObject);
            }
        }

        // 从列表中移除
        ownedWeapons.Remove(weaponToRemove);
    }

    // --- 武器管理 ---

    public void AddNewWeapon(WeaponStatBlock weaponData)
    {
        if (weaponData == null || weaponMountPoint == null) return;
        if (ownedWeapons.Any(w => w.stats.weaponName == weaponData.weaponName)) return;

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

            Debug.Log($"[WeaponController] 装备新武器: '{weaponData.weaponName}'。当前持有数量: {ownedWeapons.Count}");
        }

        if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }
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

    // --- 战斗循环 ---

    void Update()
    {
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

        if (weaponMountPoint != null)
        {
            weaponMountPoint.rotation = Quaternion.Slerp(weaponMountPoint.rotation, targetRotation, aimTurnSpeed * Time.deltaTime);
        }

        Vector3 fireDirection = targetDirection;

        // 倒序遍历，防止如果在开火过程中列表发生变化（虽然不太可能）
        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            var weapon = ownedWeapons[i];
            if (weapon.weaponPartInstance != null && weapon.weaponPartInstance.gameObject.activeInHierarchy && weapon.weaponPartInstance.enabled)
            {
                // 兼容旧的 PlayerBladeAttack: 如果它存在且启用，让它自己控制，我们不调用 Fire
                var meleeScript = weapon.weaponPartInstance.GetComponent<PlayerBladeAttack>();
                if (meleeScript != null && meleeScript.enabled)
                {
                    continue;
                }

                weapon.weaponPartInstance.Fire(fireDirection);
            }
        }
    }
}