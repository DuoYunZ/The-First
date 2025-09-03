// --- SpawnEffectAction.cs ---
using UnityEngine;

public class SpawnEffectAction : Node
{
    [Header("特效设置")]
    public GameObject effectPrefab;
    [Tooltip("（可选）指定特效生成的位置，默认为Boss自身位置")]
    public Transform spawnPoint;
    [Tooltip("特效生成后多久自动销毁，0表示不自动销毁")]
    public float destroyAfter = 5f;

    private Transform selfTransform;

    void Awake()
    {
        Rigidbody bossRb = GetComponentInParent<Rigidbody>();
        if (bossRb != null) selfTransform = bossRb.transform;
    }

    public override NodeState Evaluate()
    {
        if (effectPrefab == null) return NodeState.FAILURE;

        Transform spawnLocation = spawnPoint != null ? spawnPoint : selfTransform;
        if (spawnLocation == null) return NodeState.FAILURE;

        GameObject effect = Instantiate(effectPrefab, spawnLocation.position, spawnLocation.rotation);

        if (destroyAfter > 0)
        {
            Destroy(effect, destroyAfter);
        }

        return NodeState.SUCCESS;
    }
}