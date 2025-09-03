// WeaponController.cs (最终角色选择流程版)
using UnityEngine;
using System.Collections.Generic;
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

    [Header("自动开火设置")]
    public bool autoFire = true;

    [Header("瞄准设置")]
    public float aimTurnSpeed = 25f;

    public List<OwnedWeapon> ownedWeapons = new List<OwnedWeapon>();

    private Camera mainCamera;
    private PlayerControls playerControls;
    private Vector2 lookInput;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return; // 如果不是单例，则不执行后续Awake代码
        }

        playerControls = new PlayerControls();

        // --- 【核心修改】 ---
        // 在 Awake 中，自动查找自己层级下的 "WeaponMounts" 子对象
        // 这能确保我们永远得到的是场景中这个实例的挂载点，而不是预制件资产的
        weaponMountPoint = transform.Find("WeaponMounts");
        if (weaponMountPoint == null)
        {
            Debug.LogError("[WeaponController] 在 '" + gameObject.name + "' 的子级中未能找到名为 'WeaponMounts' 的对象！", this);
            enabled = false; // 禁用此脚本以防止后续错误
        }
        // --- 修改结束 ---
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
        if (mainCamera == null)
        {
            Debug.LogError("WeaponController: 未找到主摄像机!", this);
            enabled = false;
        }
    }

    // AddNewWeapon 方法现在可以安全地使用 weaponMountPoint 了
    public void AddNewWeapon(WeaponStatBlock weaponData)
    {
        if (weaponData == null || weaponData.weaponPartPrefab == null)
        {
            Debug.LogError("[WeaponController] AddNewWeapon 失败：传入的 weaponData 或其 weaponPartPrefab 为空！");
            return;
        }

        // 这个检查现在变得至关重要，如果 Awake 中没找到，这里会直接返回
        if (weaponMountPoint == null)
        {
            Debug.LogError("[WeaponController] AddNewWeapon 失败：'weaponMountPoint' 未找到！");
            return;
        }

        // ... (检查是否已拥有的逻辑保持不变) ...

        GameObject weaponPartGO = Instantiate(weaponData.weaponPartPrefab);
        weaponPartGO.transform.SetParent(weaponMountPoint, false); // 现在这里的 weaponMountPoint 一定是场景中的实例

        WeaponPart part = weaponPartGO.GetComponent<WeaponPart>();
        if (part == null)
        {
            Debug.LogError($"[WeaponController] 致命错误：预制件 '{weaponData.weaponPartPrefab.name}' 上缺少 WeaponPart 组件！");
            Destroy(weaponPartGO);
            return;
        }

        part.StatBlock = weaponData;

        OwnedWeapon newWeapon = new OwnedWeapon
        {
            stats = weaponData,
            currentLevel = 1,
            weaponPartInstance = part
        };
        ownedWeapons.Add(newWeapon);

        part.Activate();
        Debug.Log($"[WeaponController] 成功装备全新武器: '{weaponData.weaponName}'。");
    }
    public void RefreshAllWeaponStates()
    {
        Debug.Log("<color=yellow>[WeaponController] 接收到全局刷新指令，正在刷新所有武器...</color>");

        // 遍历当前所有激活的 WeaponPart 实例
        foreach (var weapon in ownedWeapons)
        {
            if (weapon.weaponPartInstance != null)
            {
                // 调用我们为 WeaponPart 准备的刷新方法
                weapon.weaponPartInstance.RefreshOrbiters();

                // 未来如果还有其他需要刷新的武器（比如光束武器），也可以在这里调用
                // weapon.weaponPartInstance.RefreshBeam();
            }
        }
    }
    // Update, AimAndFire 等其他方法保持不变...
    void Update()
    {
        if (!autoFire || ownedWeapons.Count == 0 || weaponMountPoint == null) return;

        Vector3 aimDirection = Vector3.zero;
        bool hasAimInput = false;

#if UNITY_STANDALONE || UNITY_EDITOR
        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            Vector3 mouseWorldPos = mouseRay.GetPoint(distance);
            aimDirection = mouseWorldPos - weaponMountPoint.position;
            aimDirection.y = 0;
            if (aimDirection.sqrMagnitude > 0.01f) hasAimInput = true;
        }
#elif UNITY_ANDROID || UNITY_IOS
        lookInput = playerControls.Player.Look.ReadValue<Vector2>();
        if (lookInput.sqrMagnitude > 0.1f)
        {
            aimDirection = new Vector3(lookInput.x, 0, lookInput.y);
            hasAimInput = true;
        }
#endif

        if (hasAimInput)
        {
            AimAndFire(aimDirection);
        }
    }

    private void AimAndFire(Vector3 targetDirection)
    {
        if (weaponMountPoint == null) return;
        targetDirection.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        weaponMountPoint.rotation = Quaternion.Slerp(weaponMountPoint.rotation, targetRotation, aimTurnSpeed * Time.deltaTime);
        Vector3 fireDirection = weaponMountPoint.forward;

        foreach (OwnedWeapon weapon in ownedWeapons)
        {
            if (weapon.weaponPartInstance != null && weapon.weaponPartInstance.enabled)
            {
                weapon.weaponPartInstance.Fire(fireDirection);
            }
        }
    }

    // 我们不再需要 RegisterExistingPart 方法，因为武器都是在新流程中动态添加的
    // public void RegisterExistingPart(WeaponPart partInstance) { ... }
}