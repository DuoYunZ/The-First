using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("角色基本信息")]
    public string characterName;
    [TextArea(3, 5)]
    public string description;
    public Sprite characterIcon; // 用于UI选择界面
    public GameObject characterPreviewPrefab; // 用于UI选择界面的3D预览模型

    [Header("战斗设置")]
    [Tooltip("这个角色在战斗中实际使用的机甲核心预制件")]
    public GameObject chassisPrefab;

    [Tooltip("这个角色的初始武器列表")]
    public List<WeaponStatBlock> initialWeapons;
}