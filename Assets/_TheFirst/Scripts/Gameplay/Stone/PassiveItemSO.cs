using UnityEngine;

[CreateAssetMenu(fileName = "New Passive Item", menuName = "Inventory/Passive Item")]
public class PassiveItemSO : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("属性加成 (示例)")]
    public float bonusDamageMultiplier = 0f;
    public float bonusMoveSpeed = 0f;
    // 你可以在这里继续扩展被动道具的具体效果
}