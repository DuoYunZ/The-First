// EnemyAttackData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyAttack", menuName = "Enemies/Enemy Attack Data")]
public class EnemyAttackData : ScriptableObject
{
    [Header("通用设置")]
    [Tooltip("定义此攻击的类型，以决定与玩家护盾的交互方式")]
    public AttackType attackType = AttackType.Standard;

    [Header("光束攻击属性")]
    public GameObject beamVfxPrefab;
    public GameObject beamImpactVfxPrefab;
    public float beamMaxDistance = 25f;
    public int beamDamagePerSecond = 10;
    public float beamDamageTickRate = 5f;
    public float beamDuration = 3f;
    public float beamCooldown = 5f;

    // 您未来可以为其他攻击类型（如抛物线、追踪弹等）在这里添加专属属性
    // [Header("抛物线攻击属性")]
    // public ...
}