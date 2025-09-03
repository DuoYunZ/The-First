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

    // 私有变量
    private Vector3 positionVelocity;
    private Vector3 baseLocalPosition;

    void Start()
    {
        if (weaponVisual != null)
        {
            baseLocalPosition = weaponVisual.transform.localPosition;
        }

        // 【核心修正】在开始时，立即将武器传送到目标位置
        if (targetToFollow != null)
        {
            transform.position = targetToFollow.position;
            transform.rotation = targetToFollow.rotation;
        }
    }

    // 使用LateUpdate可以防止角色移动时的抖动
    void LateUpdate()
    {
        Debug.Log("Time.timeScale is: " + Time.timeScale); // <-- 添加这行
        if (targetToFollow == null) return;

        // 1. 延迟位置跟随
        transform.position = Vector3.SmoothDamp(transform.position, targetToFollow.position, ref positionVelocity, followSmoothTime);

        // 2. 延迟旋转跟随
        transform.rotation = Quaternion.Slerp(transform.rotation, targetToFollow.rotation, Time.deltaTime / rotationSmoothTime);

        // 3. 上下漂浮
        if (weaponVisual != null)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            weaponVisual.transform.localPosition = baseLocalPosition + new Vector3(0, bobOffset, 0);
        }
    }

    // 公开方法，用于从其他脚本控制武器的显隐
    public void ShowWeapon()
    {
        if (weaponVisual != null) weaponVisual.SetActive(true);
    }

    public void HideWeapon()
    {
        if (weaponVisual != null) weaponVisual.SetActive(false);
    }
}