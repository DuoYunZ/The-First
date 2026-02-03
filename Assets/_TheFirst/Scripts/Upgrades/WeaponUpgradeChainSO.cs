using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWeaponChain", menuName = "Game/Weapon Upgrade Chain")]
public class WeaponUpgradeChainSO : ScriptableObject
{
    public string weaponName;
    public Sprite icon;
    public WeaponStatBlock targetWeapon;

    [Header("解锁设置")]
    [Tooltip("勾选此项，则新账号一开始就能抽到这把武器（如火球术）。不勾选则需要达成条件解锁（如燃烧瓶）。")]
    public bool isDefaultUnlocked = false; // <--- 【新增】默认为 false

    [Header("解锁 (Lv.1)")]
    public UpgradeOption unlockOption;

    [Header("升级 (Lv.2 -> Max)")]
    public List<LevelUpgradeData> levels;
}

[System.Serializable]
public struct LevelUpgradeData
{
    public List<UpgradeOption> options;
}