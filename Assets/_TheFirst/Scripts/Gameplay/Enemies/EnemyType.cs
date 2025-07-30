using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyType", menuName = "Game/Enemy Type")]
public class EnemyType : ScriptableObject
{
    [Header("基本信息")]
    public string enemyName = "New Enemy";
    public bool isBoss = false; // <--- 新增：标记这是否是一个Boss
    public GameObject enemyPrefab; // 对应的怪物预制件

    [Header("基础属性")]
    public float baseHealth = 100f;
    public float baseDamage = 10f; // 如果怪物有攻击行为
    public float baseSpeed = 2f;
    // public int scoreValue = 10; // 击杀得分 (可选)

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
}