// 在 MechSaveData.cs (或类似的脚本) 中
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

[System.Serializable] // 使其可以在检视面板中查看，并且如果需要，可以被序列化器使用
public class PartData // 零件数据
{
    public string partPrefabName; // 零件预制件的名称（或者一个ID，或者更复杂情况下直接引用预制件）
    public Vector3 localPosition; // 局部位置
    public Quaternion localRotation; // 局部旋转
    public Vector3 localScale; // <--- 新增
    // 未来如果需要，可以在这里添加其他零件特有的数据

    public PartData(string name, Vector3 pos, Quaternion rot, Vector3 scale) // <--- 修改构造函数
    {
        partPrefabName = name;
        localPosition = pos;
        localRotation = rot;
        localScale = scale; // <--- 新增
    }
}

[System.Serializable]
public class MechConfiguration
{
    public string chassisCorePrefabName;
    public List<PartData> attachedParts = new List<PartData>();

    // --- 新增：用于存储已装备武器的数据蓝图 ---
    public List<WeaponStatBlock> equippedWeaponStatBlocks = new List<WeaponStatBlock>();
}