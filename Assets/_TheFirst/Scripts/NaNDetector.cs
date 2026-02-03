using UnityEngine;

public class NaNDetector : MonoBehaviour
{
    void Update()
    {
        // 检查所有敌人
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            if (IsNaN(enemy.transform.position))
            {
                Debug.LogError($"[NaN 警报] 发现坐标损坏的敌人: {enemy.name}。位置: {enemy.transform.position}。已紧急销毁！");
                Destroy(enemy);
            }
        }
    }

    bool IsNaN(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
               float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
    }
}