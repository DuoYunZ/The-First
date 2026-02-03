using UnityEngine;

public enum AIType
{
    Chasing, // 默认的追逐玩家行为
    StraightLineStampede, // 新的直线移动行为
    Pinball

}

[CreateAssetMenu(fileName = "NewEnemyType", menuName = "Game/Enemy Type")]
public class EnemyType : ScriptableObject
{
    [Header("基本信息")]
    public string enemyName = "New Enemy";
    public bool isBoss = false; // <--- 新增：标记这是否是一个Boss
    public GameObject enemyPrefab; // 对应的怪物预制件
    [Tooltip("敌人死亡时（非自爆）生成的特效")]
    public GameObject deathVfxPrefab;

    [Header("AI 行为")]
    [Tooltip("选择此怪物使用的AI逻辑")]
    public AIType aiType = AIType.Chasing; // <--- 新增
    [Tooltip("如果AI类型是StraightLineStampede，此为其在屏幕上的存活时间")]
    public float lifetime = 15f; // <--- 新增

    [Header("基础属性")]
    public float baseHealth = 100f;
    public float baseDamage = 10f; // 如果怪物有攻击行为
    public float baseSpeed = 2f;
    // public int scoreValue = 10; // 击杀得分 (可选)

    [Header("掉落设置")]
    [Tooltip("这个敌人掉落能量石的几率 (0.01 = 1%)")]
    [Range(0f, 1f)]
    public float energyStoneDropChance = 0.01f; // 默认 1%

    [Header("波次控制")]
    [Tooltip("该怪物最早在哪一波开始出现")]
    public int firstAppearanceWave = 1;

    [Header("精英版本设置 (Elite Version Settings)")]
    [Tooltip("此类型怪物是否可以成为精英")]
    public bool canBeElite = true;

    [Tooltip("精英状态下的生命值乘数 (例如 1.5 表示为普通版本的150%)")]
    public float eliteHealthMultiplier = 1.5f;

    [Tooltip("精英状态下的伤害乘数")]
    public float eliteDamageMultiplier = 1.2f;

    [Tooltip("精英状态下的速度乘数")]
    public float eliteSpeedMultiplier = 1.1f;

    [Tooltip("精英状态下的模型缩放大小")]
    public Vector3 eliteScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Tooltip("精英状态下的颜色渲染（一个简单的视觉区分方法）")]
    public Color eliteColorTint = Color.red;

    // --- 【新增】自爆属性 ---
    [Header("自爆属性 (Suicide Attack)")]
    [Tooltip("勾选此项，如果这种类型的怪物是自爆怪")]
    public bool isSuicideBomber = false;

    [Tooltip("在准备自爆前，是否会有一个跳向玩家的动作")]
    public bool hasJumpAttack = true;
    [Tooltip("怪物进入此范围后，会触发跳跃攻击")]
    public float jumpTriggerRange = 8f; // 替换了 armingRange
    [Tooltip("跳跃抛物线的最高点")]
    public float jumpArcHeight = 3f;   // 替换了 jumpForce
    [Tooltip("跳跃在空中飞行的时间")]
    public float jumpAirTime = 1.2f;

    [Tooltip("怪物需要靠近玩家到这个距离，才会开始准备自爆")]
    public float armingRange = 2.5f;

    [Tooltip("准备自爆的持续时间（秒），期间会有预警")]
    public float armingTime = 1.5f;

    [Tooltip("自爆的伤害范围半径")]
    public float explosionRadius = 4f;

    [Tooltip("自爆造成的伤害")]
    public int explosionDamage = 50;

    [Tooltip("自爆时的视觉特效预制件")]
    public GameObject explosionVfxPrefab;

    [Tooltip("准备自爆期间的预警特效预制件")]
    public GameObject armingWarningPrefab;
}