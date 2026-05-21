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
    public List<WeaponStatBlock> inheritedSkillSources = new List<WeaponStatBlock>();

    public bool InheritsSkillSource(WeaponStatBlock source)
    {
        if (source == null) return false;
        if (stats == source) return true;
        return inheritedSkillSources != null && inheritedSkillSources.Contains(source);
    }

    public void EnsureSkillSource(WeaponStatBlock source)
    {
        if (source == null) return;
        if (inheritedSkillSources == null) inheritedSkillSources = new List<WeaponStatBlock>();
        if (!inheritedSkillSources.Contains(source)) inheritedSkillSources.Add(source);
    }
}

public class WeaponController : MonoBehaviour
{
    public static WeaponController Instance { get; private set; }
    private const int DefaultEvolutionWeaponLevel = 5;

    private Transform weaponMountPoint;

    [Header("静态/自带武器")]
    public WeaponPart builtInBladeWeapon;

    [Header("自动开火设置")]
    public bool autoFire = true;

    [Header("瞄准设置")]
    public float aimTurnSpeed = 25f;
    [Tooltip("Minimum right-stick magnitude before gamepad aim takes over.")]
    public float gamepadAimDeadzone = 0.2f;

    [Header("武器库")]
    public List<OwnedWeapon> ownedWeapons = new List<OwnedWeapon>();

    [Header("融合系统 (Fusion)")]
    [Tooltip("在这里配置所有的融合配方 (A+B=C)")]
    public List<FusionRecipeSO> fusionRecipes; // <--- 【修改】使用新的配方列表

    public HashSet<WeaponStatBlock> banList = new HashSet<WeaponStatBlock>();

    private Camera mainCamera;
    private PlayerControls playerControls;
    private Vector3 lastAimDirection = Vector3.forward;
    private bool usingGamepadAim;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        playerControls = new PlayerControls();
        KeyBindingManager.ApplyOverrides(playerControls);

        weaponMountPoint = transform.Find("WeaponMounts");
        if (weaponMountPoint == null)
        {
            weaponMountPoint = transform;
        }
    }

    void OnDestroy()
    {
        // 角色切换时清除单例引用，防止新角色自毁
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingChanged += OnBindingChanged;
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingChanged -= OnBindingChanged;
    }

    private void OnBindingChanged(string actionName, int bindingIndex)
    {
        KeyBindingManager.ApplyOverrides(playerControls);
    }

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
                initialWeapon.EnsureSkillSource(builtInBladeWeapon.StatBlock);
                ownedWeapons.Insert(0, initialWeapon);
                builtInBladeWeapon.Activate(); // 确保激活
                PlayerProgressManager.Instance?.RecordWeaponLevelReached(initialWeapon.stats, initialWeapon.currentLevel);
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
            PlayerProgressManager.Instance?.RecordWeaponLevelReached(targetWeaponData.stats, targetWeaponData.currentLevel);
        }
        else
        {
            // 可以在这里给予金币或回血作为满级补偿
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
        if (fusionRecipes == null || ownedWeapons == null) return null;

        foreach (var recipe in fusionRecipes)
        {
            if (recipe == null || recipe.weaponA == null || recipe.weaponB == null || recipe.resultWeapon == null) continue;
            if (FindOwnedWeaponForSource(recipe.resultWeapon) != null) continue;
            // 1. 检查是否拥有配方中的武器 A 和 B
            OwnedWeapon weaponA = FindOwnedWeaponForSource(recipe.weaponA);
            OwnedWeapon weaponB = FindOwnedWeaponForSource(recipe.weaponB, weaponA);

            if (weaponA != null && weaponB != null)
            {
                // 2. 检查是否都达到满级 (假设满级是 8 或 WeaponStatBlock.maxLevel)
                // 为了保险，我们检查它是否达到该武器设定的 maxLevel
                bool isAMaxed = IsOwnedWeaponAtEvolutionLevel(weaponA);
                bool isBMaxed = IsOwnedWeaponAtEvolutionLevel(weaponB);

                if (isAMaxed && isBMaxed)
                {
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
        if (recipe.resultWeapon != null && FindOwnedWeaponForSource(recipe.resultWeapon) != null) return;

        // 1. 查找并移除旧武器
        OwnedWeapon weaponA = FindOwnedWeaponForSource(recipe.weaponA);
        OwnedWeapon weaponB = FindOwnedWeaponForSource(recipe.weaponB, weaponA);

        // 【新增】将它们加入黑名单
        if (weaponA != null) banList.Add(weaponA.stats);
        if (weaponB != null) banList.Add(weaponB.stats);

        OwnedWeapon primary = weaponA ?? weaponB;
        OwnedWeapon consumed = primary == weaponA ? weaponB : weaponA;

        if (primary != null && primary.weaponPartInstance != null)
        {
            MergeSkillSources(primary, consumed);
            if (consumed != null) RemoveWeapon(consumed);
            EvolveOwnedWeapon(primary, recipe.resultWeapon, WeaponStage.Evolved);
        }
        else
        {
            RemoveWeapon(weaponA);
            RemoveWeapon(weaponB);
            AddNewWeapon(recipe.resultWeapon);
        }

        // 2. 添加新武器 (超武)

        // 3. 刷新 UI
        if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }
    }

    public void PerformFusion(WeaponFusionRecipeSO recipe)
    {
        if (recipe == null || recipe.resultWeapon == null) return;
        if (FindOwnedWeaponForSource(recipe.resultWeapon) != null) return;

        OwnedWeapon primary = FindOwnedWeaponForSource(recipe.triggerWeapon);

        if (primary == null) return;

        List<OwnedWeapon> consumedWeapons = new List<OwnedWeapon>();
        if (recipe.conditions != null)
        {
            foreach (FusionCondition condition in recipe.conditions)
            {
                if (condition == null || condition.type != ConditionType.Weapon || condition.requiredWeapon == null) continue;

                OwnedWeapon consumed = FindOwnedWeaponForSource(condition.requiredWeapon, primary);

                if (consumed != null && !consumedWeapons.Contains(consumed))
                {
                    consumedWeapons.Add(consumed);
                }
            }
        }

        foreach (OwnedWeapon consumed in consumedWeapons)
        {
            MergeSkillSources(primary, consumed);
        }

        if (recipe.fusionType == FusionType.Merge)
        {
            foreach (OwnedWeapon consumed in consumedWeapons)
            {
                RemoveWeapon(consumed);
            }
        }

        banList.Add(primary.stats);
        foreach (OwnedWeapon consumed in consumedWeapons)
        {
            if (consumed.stats != null) banList.Add(consumed.stats);
        }

        EvolveOwnedWeapon(primary, recipe.resultWeapon, WeaponStage.Evolved);
    }

    private OwnedWeapon FindOwnedWeaponForSource(WeaponStatBlock source, OwnedWeapon exclude = null)
    {
        if (source == null || ownedWeapons == null) return null;

        return ownedWeapons.FirstOrDefault(w =>
            w != null &&
            w != exclude &&
            MatchesOwnedWeaponSource(w, source));
    }

    private bool MatchesOwnedWeaponSource(OwnedWeapon owned, WeaponStatBlock source)
    {
        if (owned == null || source == null) return false;
        if (owned.InheritsSkillSource(source)) return true;
        if (owned.stats == source) return true;
        if (owned.weaponPartInstance != null && owned.weaponPartInstance.StatBlock == source) return true;

        string sourceId = source.weaponID;
        if (!string.IsNullOrEmpty(sourceId))
        {
            if (owned.stats != null && string.Equals(owned.stats.weaponID, sourceId, System.StringComparison.OrdinalIgnoreCase)) return true;
            WeaponStatBlock partStats = owned.weaponPartInstance != null ? owned.weaponPartInstance.StatBlock : null;
            if (partStats != null && string.Equals(partStats.weaponID, sourceId, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        string sourceName = source.weaponName;
        if (!string.IsNullOrEmpty(sourceName))
        {
            if (owned.stats != null && string.Equals(owned.stats.weaponName, sourceName, System.StringComparison.OrdinalIgnoreCase)) return true;
            WeaponStatBlock partStats = owned.weaponPartInstance != null ? owned.weaponPartInstance.StatBlock : null;
            if (partStats != null && string.Equals(partStats.weaponName, sourceName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private bool IsOwnedWeaponAtEvolutionLevel(OwnedWeapon owned)
    {
        if (owned == null) return false;
        int maxLevel = GetDynamicMaxLevel(owned);
        int targetLevel = Mathf.Min(DefaultEvolutionWeaponLevel, maxLevel);
        return owned.currentLevel >= targetLevel;
    }

    public void EvolveWeapon(WeaponStatBlock baseStats, WeaponStatBlock targetStats)
    {
        // 1. 找到旧武器
        var oldWeaponWrapper = ownedWeapons.FirstOrDefault(w => w.stats == baseStats);

        if (oldWeaponWrapper != null)
        {
            // 2. 彻底销毁旧武器的物体
            // 这一点至关重要！因为闪电链的 Prefab 和雷击的 Prefab 结构完全不同
            EvolveOwnedWeapon(oldWeaponWrapper, targetStats, WeaponStage.Evolved);

            // 3. 从列表中移除旧数据

            // 4. 添加新武器 (会自动实例化新的 Prefab)

            // 5. 【可选】局外解锁逻辑 (保存到 PlayerProgressManager)
            if (PlayerProgressManager.Instance != null)
            {
                // 确保新武器的 ID 记录到存档
                string newID = targetStats.weaponID;
                if (!string.IsNullOrEmpty(newID) && !PlayerProgressManager.Instance.unlockedItems.Contains(newID))
                {
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

    public void EvolveOwnedWeapon(OwnedWeapon ownedWeapon, WeaponStatBlock targetStats, WeaponStage stage = WeaponStage.Evolved)
    {
        if (ownedWeapon == null || targetStats == null) return;

        ownedWeapon.EnsureSkillSource(ownedWeapon.stats);
        if (ownedWeapon.weaponPartInstance != null)
        {
            ownedWeapon.EnsureSkillSource(ownedWeapon.weaponPartInstance.StatBlock);
            ownedWeapon.weaponPartInstance.ApplyBranch(targetStats);
            ownedWeapon.weaponPartInstance.currentStage = stage;
            ownedWeapon.weaponPartInstance.currentLevel = Mathf.Max(ownedWeapon.currentLevel, ownedWeapon.weaponPartInstance.currentLevel);
        }

        ownedWeapon.stats = targetStats;
        ownedWeapon.currentLevel = Mathf.Max(ownedWeapon.currentLevel, 1);
        ownedWeapon.EnsureSkillSource(targetStats);
        PlayerProgressManager.Instance?.RecordWeaponLevelReached(targetStats, ownedWeapon.currentLevel);
        PlayerStats.Instance?.RefreshStats();
        WeaponUI.Instance?.UpdateWeaponIcons();
    }

    private void MergeSkillSources(OwnedWeapon primary, OwnedWeapon consumed)
    {
        if (primary == null) return;
        primary.EnsureSkillSource(primary.stats);

        if (consumed == null) return;
        primary.EnsureSkillSource(consumed.stats);
        if (consumed.inheritedSkillSources == null) return;

        foreach (WeaponStatBlock source in consumed.inheritedSkillSources)
        {
            primary.EnsureSkillSource(source);
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
        PlayerStats.Instance?.RefreshStats();
    }

    /// <summary>
    /// 根据武器StatBlock移除武器（融合系统使用）
    /// </summary>
    public void RemoveWeaponByStatBlock(WeaponStatBlock statBlock)
    {
        if (statBlock == null) return;
        
        var toRemove = ownedWeapons.FirstOrDefault(w => 
            w.weaponPartInstance != null && 
            w.weaponPartInstance.StatBlock == statBlock);
        
        if (toRemove != null)
        {
            RemoveWeapon(toRemove);
        }
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

            OwnedWeapon newOwnedWeapon = new OwnedWeapon
            {
                stats = weaponData,
                currentLevel = 1,
                weaponPartInstance = part,
                inheritedSkillSources = new List<WeaponStatBlock> { weaponData }
            };
            ownedWeapons.Add(newOwnedWeapon);
            PlayerProgressManager.Instance?.RecordWeaponLevelReached(weaponData, newOwnedWeapon.currentLevel);

            PlayerStats.Instance?.RefreshStats();

        }

        if (WeaponUI.Instance != null)
        {
            WeaponUI.Instance.UpdateWeaponIcons();
        }
    }

    public List<OwnedWeapon> GetUpgradeableWeapons()
    {
        List<OwnedWeapon> result = new List<OwnedWeapon>();
        foreach (OwnedWeapon owned in ownedWeapons)
        {
            if (owned == null || owned.stats == null || owned.weaponPartInstance == null) continue;
            if (!CanGrantNormalWeaponLevels(owned)) continue;

            int max = GetDynamicMaxLevel(owned);
            if (owned.currentLevel < max)
            {
                result.Add(owned);
            }
        }
        return result;
    }

    public int GrantWeaponLevels(OwnedWeapon owned, int levels)
    {
        if (owned == null || owned.stats == null || levels <= 0) return 0;
        if (!CanGrantNormalWeaponLevels(owned)) return 0;

        int applied = 0;
        int max = GetDynamicMaxLevel(owned);
        while (applied < levels && owned.currentLevel < max)
        {
            owned.currentLevel++;
            applied++;
        }

        if (owned.weaponPartInstance != null)
        {
            owned.weaponPartInstance.currentLevel = owned.currentLevel;
            owned.weaponPartInstance.OnWeaponLevelUp?.Invoke(owned.currentLevel);
        }

        if (applied > 0)
        {
            PlayerProgressManager.Instance?.RecordWeaponLevelReached(owned.stats, owned.currentLevel);
            RefreshAllWeaponStates();
            PlayerStats.Instance?.RefreshStats();
            WeaponUI.Instance?.UpdateWeaponIcons();
        }

        return applied;
    }

    public int GrantWeaponLevels(WeaponStatBlock weaponStats, int levels)
    {
        if (weaponStats == null) return 0;
        OwnedWeapon owned = ownedWeapons.FirstOrDefault(w => w != null && w.InheritsSkillSource(weaponStats));
        return GrantWeaponLevels(owned, levels);
    }

    public int GetMaxLevel(OwnedWeapon owned)
    {
        return GetDynamicMaxLevel(owned);
    }

    private bool CanGrantNormalWeaponLevels(OwnedWeapon owned)
    {
        if (owned == null || owned.weaponPartInstance == null) return true;
        return owned.weaponPartInstance.currentStage < WeaponStage.Evolved;
    }

    private int GetDynamicMaxLevel(OwnedWeapon owned)
    {
        if (owned == null) return 1;
        int max = owned.stats != null ? owned.stats.maxLevel : 1;
        if (owned.weaponPartInstance != null)
        {
            max = Mathf.Max(max, owned.weaponPartInstance.maxLevel);
        }
        return Mathf.Max(1, max);
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
        bool gamepadDrivingPlayer = IsGamepadDrivingPlayer();

        if (TryReadStickDirection(playerControls.Player.Look, out aimDirection))
        {
            hasAimInput = true;
            gamepadDrivingPlayer = true;
            usingGamepadAim = true;
        }
        else if (gamepadDrivingPlayer && TryReadStickDirection(playerControls.Player.Move, out aimDirection))
        {
            hasAimInput = true;
            usingGamepadAim = true;
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        if (!hasAimInput && (!usingGamepadAim || HasMouseAimMovement()) && mainCamera != null && Mouse.current != null)
        {
            Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            if (groundPlane.Raycast(mouseRay, out float distance))
            {
                Vector3 mouseWorldPos = mouseRay.GetPoint(distance);
                aimDirection = mouseWorldPos - transform.position;
                aimDirection.y = 0;
                if (aimDirection.sqrMagnitude > 0.01f)
                {
                    hasAimInput = true;
                    usingGamepadAim = false;
                }
            }
        }
#endif

        if (hasAimInput)
        {
            lastAimDirection = aimDirection.normalized;
            AimAndFire(aimDirection);
        }
        else if (usingGamepadAim && lastAimDirection.sqrMagnitude > 0.01f)
        {
            AimAndFire(lastAimDirection);
        }
    }

    private bool TryReadStickDirection(InputAction action, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (action == null) return false;

        Vector2 input = action.ReadValue<Vector2>();
        if (input.sqrMagnitude < gamepadAimDeadzone * gamepadAimDeadzone) return false;

        direction = new Vector3(input.x, 0f, input.y);
        return direction.sqrMagnitude > 0.01f;
    }

    private bool IsGamepadDrivingPlayer()
    {
        return playerControls.Player.Look.activeControl?.device is Gamepad
            || playerControls.Player.Move.activeControl?.device is Gamepad;
    }

    private static bool HasMouseAimMovement()
    {
        return Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;
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
