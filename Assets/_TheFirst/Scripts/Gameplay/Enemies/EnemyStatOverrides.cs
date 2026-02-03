// EnemyStatOverrides.cs
using UnityEngine;

[System.Serializable]
public class EnemyStatOverrides
{
    [Tooltip("新的生命值乘数。1 表示使用 EnemyType 中的基础值，1.5 表示 150% 生命值。")]
    public float healthMultiplier = 1f;

    [Tooltip("新的伤害乘数。1 表示不改变。")]
    public float damageMultiplier = 1f;

    [Tooltip("新的速度乘数。1 表示不改变。")]
    public float speedMultiplier = 1f;

    [Tooltip("新的模型缩放大小。1,1,1 表示不改变。")]
    public Vector3 scale = Vector3.one;

    // 您未来可以添加更多需要覆盖的属性，例如颜色
    // public Color colorTint;
}