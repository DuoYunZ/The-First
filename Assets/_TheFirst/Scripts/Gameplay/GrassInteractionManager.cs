// GrassInteractionManager.cs (修改版)
using UnityEngine;
using System.Collections.Generic;

public class GrassInteractionManager : MonoBehaviour
{
    [Tooltip("将需要与草地交互的物体（例如玩家机甲）的 Transform 拖到这里")]
    public List<Transform> interactors;
    [Tooltip("交互物体影响草地的默认半径")]
    public float defaultInteractionRadius = 2.0f;
    [Tooltip("交互强度，如果 Shader Graph 中也用此属性控制强度")]
    public float interactionStrength = 1f; // 如果你的Shader Graph也用这个属性

    // Shader Graph 中定义的属性的引用名称 (用作全局变量名)
    // 确保这些字符串与你在 Shader Graph Blackboard 中对应属性的 "Reference" 名称完全一致
    private static readonly int InteractorCountID = Shader.PropertyToID("_GrassInteractorCount");
    private static readonly int InteractorPosRadius0ID = Shader.PropertyToID("_GrassInteractorPosRadius0");
    private static readonly int InteractorPosRadius1ID = Shader.PropertyToID("_GrassInteractorPosRadius1");
    private static readonly int InteractionStrengthID = Shader.PropertyToID("_GrassInteractionStrength"); // 如果 Shader Graph 中有这个
    // 如果有更多交互体，继续添加 ...ID
    // private static readonly int InteractionStrengthID = Shader.PropertyToID("_GrassInteractionStrength");


    // 假设我们最多处理2个交互体，对应 Shader Graph 中的 _GrassInteractorPosRadius0 和 _GrassInteractorPosRadius1
    private const int MAX_INTERACTORS_SUPPORTED = 2;
    private Vector4[] _interactorShaderData = new Vector4[MAX_INTERACTORS_SUPPORTED];

    void Awake() // 使用 Awake 确保在任何 Start 和第一帧 Update 之前执行
    {
        Debug.Log("GrassInteractionManager: Awake - Initializing global shader variables for grass interaction to safe defaults.");
        // 设置一个明确的“无交互”状态
        Shader.SetGlobalFloat(InteractorCountID, 0.0f);

        Vector4 inactiveInteractorData = new Vector4(0, -10000, 0, 0); // 位置远，半径为0
        Shader.SetGlobalVector(InteractorPosRadius0ID, inactiveInteractorData);
        if (MAX_INTERACTORS_SUPPORTED > 1) // 如果 Shader Graph 支持更多
        {
            Shader.SetGlobalVector(InteractorPosRadius1ID, inactiveInteractorData);
        }
        // 根据需要设置更多 ...

        // 如果交互强度也由全局变量控制，也在这里初始化
        // Shader.SetGlobalFloat(InteractionStrengthID, 0.0f); // 例如，0强度表示无影响
        // 或者如果你在Shader Graph中直接使用了你Inspector设置的InteractionStrength，那就不需要这行
    }

    void Update()
    {
        int validInteractorCount = 0;
        if (interactors != null && interactors.Count > 0)
        {
            for (int i = 0; i < interactors.Count && i < MAX_INTERACTORS_SUPPORTED; ++i)
            {
                if (interactors[i] != null && interactors[i].gameObject.activeInHierarchy)
                {
                    Vector3 pos = interactors[i].position;
                    float radius = defaultInteractionRadius;
                    // --- 添加日志 ---
                    if (Time.frameCount % 60 == 0 && validInteractorCount == 0) // 只在第一个有效交互体时打印一次，避免刷屏
                    {
                        //Debug.Log($"Interactor {i} Data: Pos={pos}, Radius={radius}");
                    }
                    _interactorShaderData[validInteractorCount] = new Vector4(pos.x, pos.y, pos.z, defaultInteractionRadius);
                    validInteractorCount++;
                }
            }
        }

        Shader.SetGlobalFloat(InteractorCountID, (float)validInteractorCount);
        // if (Time.frameCount % 60 == 0) Debug.Log($"GrassInteractionManager: Setting _GrassInteractorCount = {validInteractorCount}");

        for (int i = 0; i < MAX_INTERACTORS_SUPPORTED; ++i)
        {
            Vector4 dataToSend = (i < validInteractorCount) ? _interactorShaderData[i] : new Vector4(0, -10000, 0, 0);
            if (i == 0)
            {
                Shader.SetGlobalVector(InteractorPosRadius0ID, dataToSend);
                // if (validInteractorCount > 0 && Time.frameCount % 60 == 0) Debug.Log($"GrassInteractionManager: Setting _GrassInteractorPosRadius0 = {dataToSend}");
            }
            else if (i == 1)
            {
                Shader.SetGlobalVector(InteractorPosRadius1ID, dataToSend);
                // if (validInteractorCount > 1 && Time.frameCount % 60 == 0) Debug.Log($"GrassInteractionManager: Setting _GrassInteractorPosRadius1 = {dataToSend}");
            }
            // ...
        }
        // 如果交互强度由脚本控制并作为全局变量传递
        // Shader.SetGlobalFloat(InteractionStrengthID, this.interactionStrength);
    }

    // 在不需要交互时（例如切换场景或游戏结束），可能需要清除这些全局变量
    void OnDisable()
    {
        Debug.Log("GrassInteractionManager: OnDisable - Resetting global shader variables for grass interaction.");
        Shader.SetGlobalFloat(InteractorCountID, 0.0f);
        Vector4 inactiveInteractorData = new Vector4(0, -10000, 0, 0);
        Shader.SetGlobalVector(InteractorPosRadius0ID, inactiveInteractorData);
        if (MAX_INTERACTORS_SUPPORTED > 1)
        {
            Shader.SetGlobalVector(InteractorPosRadius1ID, inactiveInteractorData);
        }
        // Shader.SetGlobalFloat(InteractionStrengthID, 0.0f);
    }
}