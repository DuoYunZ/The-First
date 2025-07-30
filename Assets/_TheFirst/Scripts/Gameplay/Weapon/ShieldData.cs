using UnityEngine;

[CreateAssetMenu(fileName = "NewShieldData", menuName = "Game/Shield Data")]
public class ShieldData : ScriptableObject
{
    [Header("护盾基础信息")]
    public string shieldName;
    public Sprite shieldIcon;

    [Header("核心属性")]
    [Tooltip("这种护盾的基础最大值")]
    public int baseMaxValue;
    [Tooltip("这种护盾被击破后的基础冷却时间（秒）")]
    public float baseCooldown;

    [Header("视觉效果")]
    [Tooltip("护盾激活时，要实例化的视觉特效预制件")]
    public GameObject shieldVisualPrefab;
}