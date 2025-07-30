using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Game/Upgrade Database")]
public class UpgradeDatabase : ScriptableObject
{
    // 这个列表将存储项目中所有的 UpgradeData 资产
    public List<SkillTreeNodeData> allUpgrades;
}