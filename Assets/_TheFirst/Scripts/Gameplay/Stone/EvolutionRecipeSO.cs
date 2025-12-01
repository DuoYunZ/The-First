using UnityEngine;

[CreateAssetMenu(menuName = "Game/Evolution Recipe")]
public class EvolutionRecipeSO : ScriptableObject
{
    [Header("合成公式")]
    [Tooltip("哪把武器需要进化？")]
    public WeaponStatBlock baseWeapon;

    [Tooltip("需要插着什么属性的石头？")]
    public EnergyStoneEffectType requiredStoneType;

    [Header("进化结果")]
    [Tooltip("进化成什么新武器？")]
    public WeaponStatBlock evolvedWeapon;

    [Header("UI描述")]
    public string evolutionName; // 例如 "疾风之刃"
    [TextArea] public string description; // 例如 "斩击进化！发射远程风刃，穿透敌人。"
}