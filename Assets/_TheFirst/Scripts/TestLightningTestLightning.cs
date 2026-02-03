// TestLightning.cs
using UnityEngine;
public class TestLightning : MonoBehaviour
{
    public GameObject lightningPrefab; // 拖入你的闪电链预制件
    public Transform targetA; // 拖入场景中的球体A
    public Transform targetB; // 拖入场景中的球体B

    void Update()
    {
        // 按下空格键时，在两个球体之间生成一条闪电
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameObject chain = Instantiate(lightningPrefab);
            chain.GetComponent<ChainLightningVFX>().Setup(targetA.position, targetB.position);
        }
    }
}