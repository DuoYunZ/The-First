// EnergyStoneSO.cs
using UnityEngine;
using System.Collections.Generic;

// [System.Serializable]
// public struct StoneEffect
// {
//     public EnergyStoneEffectType effectType;
//     public float value; // 用于 ModifyDamage (0.2 = +20%), AddPierce (2 = +2 穿透) 等
// }

[CreateAssetMenu(fileName = "EnergyStone_", menuName = "Mech Survivors/Energy Stone")]
public class EnergyStoneSO : ScriptableObject
{
    [Header("基础信息")]
    public string stoneName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;

    [Header("能量石效果")]
    [Tooltip("这个能量石所赋予的所有效果。")]
    public List<EnergyStoneEffectType> stoneEffects;

    // --- 纯数值修改 ---
    // (为了简化第一版，我们先用布尔值。后续可以扩展为上面的 List<StoneEffect>)

    [Header("元素 (布尔值)")]
    public bool applyBurn = false;
    public bool applySlow = false;
    public bool applyStun = false;
    // ... (你可以添加更多) ...

    [Header("数值修改 (百分比/固定值)")]
    public float damageModifier = 0f;       // 0.2 = +20%
    public float fireRateModifier = 0f;     // 0.1 = +10% 射速 (冷却 * 0.9)
    public float scaleModifier = 0f;        // 0.25 = +25% 范围/体积
    public float pierceModifier = 0;        // 2 = +2 穿透
}