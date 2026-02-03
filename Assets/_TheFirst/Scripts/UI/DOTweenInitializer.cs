using UnityEngine;
using DG.Tweening; // 引入 DOTween 命名空间

public class DOTweenInitializer : MonoBehaviour
{
    void Awake()
    {
        // 手动初始化 DOTween
        // 这行代码会检查 DOTween 是否已初始化，
        // 如果没有，则进行初始化。如果已经初始化，则什么也不做。
        // 这是一个非常安全的操作。
        DOTween.Init();

        Debug.Log("DOTween Initialized via Code.");
    }
}