#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pre-sizes URP global shader arrays in the editor so switching between Android
/// and desktop targets does not lock the array at the smaller mobile size.
/// </summary>
[InitializeOnLoad]
internal static class UrpShaderArrayWarmup
{
    private const int MaxVisibleAdditionalLightsNonMobile = 256;
    private static readonly int AdditionalShadowParamsId = Shader.PropertyToID("_AdditionalShadowParams");
    private static readonly Vector4[] EmptyAdditionalShadowParams = new Vector4[MaxVisibleAdditionalLightsNonMobile];

    static UrpShaderArrayWarmup()
    {
        Warmup();
        EditorApplication.delayCall -= Warmup;
        EditorApplication.delayCall += Warmup;
    }

    [MenuItem("Tools/TheFirst/Rendering/Warm Up URP Shader Arrays")]
    private static void Warmup()
    {
        Shader.SetGlobalVectorArray(AdditionalShadowParamsId, EmptyAdditionalShadowParams);
    }
}
#endif
