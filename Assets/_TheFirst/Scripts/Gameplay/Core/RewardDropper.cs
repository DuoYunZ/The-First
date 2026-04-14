using UnityEngine;

public class RewardDropper : MonoBehaviour
{
    public GameObject experienceGemPrefab; // 引用经验宝石预制件
    public int gemsToDrop = 10; // 掉落数量
    public float dropRadius = 1.0f; // 在多大半径内随机掉落

    // 你需要在 Health 组件的 OnDeath 事件中，将这个方法关联上
    public void DropRewards()
    {
        if (experienceGemPrefab == null) return;

        for (int i = 0; i < gemsToDrop; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * dropRadius;
            Vector3 dropPosition = transform.position + new Vector3(randomCircle.x, 0.5f, randomCircle.y);
            Instantiate(experienceGemPrefab, dropPosition, Quaternion.identity);
        }
    }
}