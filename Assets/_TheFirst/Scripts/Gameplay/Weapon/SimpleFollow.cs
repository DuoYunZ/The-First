using UnityEngine;

public class SimpleFollow : MonoBehaviour
{
    // 这个 target 将由创建它的脚本来指定
    public Transform target;

    // 使用 LateUpdate 可以确保我们在目标（玩家）完成所有物理移动之后，再更新自己的位置
    void LateUpdate()
    {
        if (target != null)
        {
            // 在每一帧的最后，都将自己的位置和旋转，强制设置为与目标完全一致
            transform.position = target.position;
            transform.rotation = target.rotation;
        }
    }
}