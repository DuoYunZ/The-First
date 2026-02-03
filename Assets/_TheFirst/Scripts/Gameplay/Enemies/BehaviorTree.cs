// --- BehaviorTree.cs (带冷却管理功能) ---
using UnityEngine;
using System.Collections.Generic; // 需要引入这个命名空间

[RequireComponent(typeof(Node))]
public class BehaviorTree : MonoBehaviour
{
    private Node rootNode;

    // 【新增】用于存储所有攻击技能冷却时间的字典
    private Dictionary<string, float> attackCooldowns = new Dictionary<string, float>();

    void Start()
    {
        rootNode = GetComponent<Node>();
    }

    void Update()
    {
        if (rootNode != null)
        {
            rootNode.Evaluate();
        }
    }

    // --- 【新增】公共方法，供其他节点调用 ---

    /// <summary>
    /// 开始一个技能的冷却
    /// </summary>
    /// <param name="attackName">技能的唯一名称</param>
    /// <param name="duration">冷却时长（秒）</param>
    public void StartCooldown(string attackName, float duration)
    {
        // 记录下这个技能可以再次使用的“未来时间点”
        attackCooldowns[attackName] = Time.time + duration;
    }

    /// <summary>
    /// 检查一个技能是否正在冷却中
    /// </summary>
    /// <param name="attackName">要检查的技能名称</param>
    /// <returns>如果在冷却中，返回true</returns>
    public bool IsOnCooldown(string attackName)
    {
        // 如果字典里有这个技能，并且它的冷却结束时间点还没到
        if (attackCooldowns.ContainsKey(attackName) && attackCooldowns[attackName] > Time.time)
        {
            return true; // 正在冷却
        }
        return false; // 已冷却完毕
    }
}