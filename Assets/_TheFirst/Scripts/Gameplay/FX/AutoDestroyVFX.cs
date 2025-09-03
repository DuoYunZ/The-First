// --- AutoDestroyVFX.cs ---
using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    [Tooltip("特效的生命周期（秒），结束后会自动销毁")]
    public float lifetime = 2f;

    void Start()
    {
        // 在 lifetime 秒后，销毁挂载此脚本的游戏对象
        Destroy(gameObject, lifetime);
    }
}