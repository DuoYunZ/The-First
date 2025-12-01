// --- FloatingWeaponController.cs ---
using UnityEngine;

public class FloatingWeaponController : MonoBehaviour
{
    [Header("跟随设置")]
    [Tooltip("武器需要跟随的目标点（例如玩家背后的一个空对象）")]
    public Transform targetToFollow;
    [Tooltip("跟随的平滑时间，数值越大，延迟感越强")]
    public float followSmoothTime = 0.3f;
    [Tooltip("旋转跟随的平滑时间")]
    public float rotationSmoothTime = 0.2f;

    [Header("漂浮设置")]
    [Tooltip("上下漂浮的速度")]
    public float bobSpeed = 2f;
    [Tooltip("上下漂浮的幅度")]
    public float bobAmount = 0.1f;

    [Header("视觉组件")]
    [Tooltip("武器的模型/视觉部分，用于显示和隐藏")]
    public GameObject weaponVisual;

    private Renderer[] visualRenderers;

    // 私有变量
    private Vector3 positionVelocity;
    private Vector3 baseLocalPosition;

    void Start()
    {
        if (weaponVisual != null)
        {
            RefreshRenderers();
        }

        if (targetToFollow != null)
        {
            transform.position = targetToFollow.position;
            transform.rotation = targetToFollow.rotation;
        }
    }

    public GameObject SwapModel(GameObject newModelPrefab)
    {
        // 1. 如果没有新模型，或者新模型和当前一样，就不动
        // (简单的名字检查，防止重复生成)
        if (newModelPrefab == null) return null;
        if (weaponVisual != null && weaponVisual.name.StartsWith(newModelPrefab.name)) return weaponVisual;

        // 2. 销毁旧模型
        if (weaponVisual != null)
        {
            Destroy(weaponVisual);
        }

        // 3. 生成新模型
        weaponVisual = Instantiate(newModelPrefab, transform);
        weaponVisual.transform.localPosition = Vector3.zero;
        weaponVisual.transform.localRotation = Quaternion.identity;

        // 4. 重置状态
        baseLocalPosition = Vector3.zero;

        // 5. 刷新缓存
        RefreshRenderers();

        return weaponVisual;
    }

    private void RefreshRenderers()
    {
        if (weaponVisual != null)
        {
            // includeInactive = true 确保刚生成还没激活也能找到
            visualRenderers = weaponVisual.GetComponentsInChildren<Renderer>(true);
        }
    }

    // 使用LateUpdate可以防止角色移动时的抖动
    void LateUpdate()
    {
        if (targetToFollow == null) return;

        // 跟随逻辑 (保持不变)
        transform.position = Vector3.SmoothDamp(transform.position, targetToFollow.position, ref positionVelocity, followSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetToFollow.rotation, Time.deltaTime / rotationSmoothTime);

        // 漂浮逻辑 (保持不变)
        if (weaponVisual != null)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            weaponVisual.transform.localPosition = baseLocalPosition + new Vector3(0, bobOffset, 0);
        }
    }

    // 公开方法，用于从其他脚本控制武器的显隐
    public void ShowWeapon()
    {
        if (visualRenderers != null)
            foreach (var r in visualRenderers) r.enabled = true;
    }

    public void HideWeapon()
    {
        if (visualRenderers != null)
            foreach (var r in visualRenderers) r.enabled = false;
    }
}