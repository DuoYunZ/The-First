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

    [Header("掉落物预制件")]
    [Tooltip("当这个能量石在游戏中掉落时，应该实例化的预制件 (Prefab)")]
    public GameObject pickupPrefab;

    [Header("视觉表现")]
    [Tooltip("这颗石头对应的发光颜色 (HDR)")]
    [ColorUsage(true, true)]
    public Color stoneGlowColor = Color.white; // 默认为白

    [Header("光环特效覆盖 (可选)")]
    [Tooltip("用于替换光环默认VFX的预制件")]
    public GameObject auraVfxOverride;
    [Tooltip("VFX预制件的基础缩放乘数 (用于校准视觉和碰撞器半径)")]
    public float overrideVfxScaleMultiplier = 1.0f;

    [Header("能量石效果")]
    [Tooltip("这个能量石所赋予的所有效果。")]
    public List<EnergyStoneEffectType> stoneEffects;

    [Header("元素 (燃烧)")]
    public bool applyBurn = false;
    [Tooltip("燃烧几率 (0.5 = 50%)")]
    [Range(0f, 1f)]
    public float burnChance = 0.5f;
    [Tooltip("燃烧每跳伤害")]
    public int burnDamage = 2;
    [Tooltip("燃烧总持续时间 (秒)")]
    public float burnDuration = 3f;
    [Tooltip("燃烧伤害间隔 (秒)")]
    public float burnTickInterval = 1f;

    [Header("元素 (减速)")]
    public bool applySlow = false;
    [Tooltip("减速百分比 (0.3 = 减速30%)")]
    [Range(0f, 1f)]
    public float slowPercentage = 0.3f;
    [Tooltip("减速持续时间 (秒) - 对于光环，这个值可以很短，因为会持续刷新")]
    public float slowDuration = 1.0f;
    public Color slowColor = Color.cyan;

    [Tooltip("2+ 寒冰石堆叠：冰冻几率 (0.15 = 15%)")]
    [Range(0f, 1f)]
    public float freezeChance = 0.15f;
    [Tooltip("2+ 寒冰石堆叠：冰冻持续时间 (秒)")]
    public float freezeDuration = 1.0f;
    [Tooltip("2+ 寒冰石堆叠：触发冰冻时播放的专属VFX")]
    public GameObject freezeVfxPrefab;


    [Header("元素 (雷电)")]
    public bool applyChain = false;
    [Tooltip("连锁闪电额外弹射的目标数量")]
    public int chainTargets = 2;
    [Tooltip("连锁闪电的弹射半径")]
    public float chainRange = 5f;
    [Tooltip("连锁伤害百分比 (0.5 = 造成光环50%的伤害)")]
    public float chainDamageMultiplier = 0.5f;
    [Tooltip("连锁闪电的VFX预制件 (需要一个能处理起点和终点的脚本)")]
    public GameObject chainVfxPrefab; // (复用 WeaponPart 的 lightningChainPrefab)
    [Tooltip("连锁闪电 *击中敌人时* 播放的专属受击特效")]
    public GameObject chainImpactVfxPrefab;

    [Tooltip("1+ 雷电石：是否触发雷击")]
    public bool applySmite = false;
    [Tooltip("雷击造成的伤害 (会受玩家属性加成)")]
    public int smiteDamage = 10;
    [Tooltip("雷击的VFX预制件 (从天上劈下来)")]
    public GameObject smiteVfxPrefab;

    [Header("元素 (风暴)")]
    public bool applyKnockback = false;
    [Tooltip("击退力度")]
    public float knockbackForce = 10f;
    [Tooltip("击退效果的触发间隔 (秒)")]
    public float knockbackInterval = 1.0f;

    [Tooltip("2+ 风暴石堆叠：直线子弹 施加的击退力度")]
    public float knockbackForce_Stacked = 20f;

    [Header("元素 (大地 - 弱化)")]
    public bool applyWeaken = false;
    [Tooltip("弱化百分比 (0.2 = 降低敌人20%的伤害)")]
    [Range(0f, 1f)]
    public float weakenPercentage = 0.2f;
    [Tooltip("弱化持续时间 (秒) - 光环会持续刷新")]
    public float weakenDuration = 1.0f;

    [Header("元素 (剧毒 - 腐蚀)")]
    public bool applyCorrode = false;
    [Tooltip("易伤百分比 (1.2 = 受到伤害增加20%)")]
    public float corrodeMultiplier = 1.2f;

    [Tooltip("腐蚀效果施加的颜色")]
    public Color corrodeColor = new Color(0.5f, 1f, 0.5f); // 默认浅绿色

    [Tooltip("2+ 腐蚀石堆叠：易伤百分比 (1.5 = 受到伤害增加50%)")]
    public float corrodeMultiplier_Stacked = 1.5f;

    [Header("元素 (眩晕)")]
    public bool applyStun = false;
    [Tooltip("眩晕几率 (0.25 = 25%)")]
    [Range(0f, 1f)]
    public float stunChance = 0.25f;
    [Tooltip("眩晕时长 (秒)")]
    public float stunDuration = 1.0f;

    [Tooltip("2+ 大地石堆叠：眩晕几率 (0.5 = 50%)")]
    [Range(0f, 1f)]
    public float stunChance_Stacked = 0.5f;
    // ... (你可以添加更多) ...

    [Header("机制 (磁力)")]
    public bool applyMagnet = false;
    [Tooltip("磁力光环会使基础光环半径额外增加多少百分比 (0.5 = +50%) 来吸取物品")]
    public float magnetRadiusBonusPercent = 0.5f;

    [Header("数值修改 (百分比/固定值)")]
    public float damageModifier = 0f;       // 0.2 = +20%
    public float fireRateModifier = 0f;     // 0.1 = +10% 射速 (冷却 * 0.9)
    public float scaleModifier = 0f;        // 0.25 = +25% 范围/体积
    public float pierceModifier = 0;        // 2 = +2 穿透
}