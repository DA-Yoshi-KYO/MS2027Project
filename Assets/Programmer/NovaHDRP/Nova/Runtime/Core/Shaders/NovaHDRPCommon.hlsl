#ifndef NOVA_HDRP_COMMON_INCLUDED
#define NOVA_HDRP_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
float4 ComputeScreenPos(float4 positionCS)
{
    float4 result = positionCS * 0.5;
    result.xy = float2(result.x, result.y * _ProjectionParams.x) + result.w;
    result.zw = positionCS.zw;
    return result;
}
float3 NormalizeNormalPerPixel(float3 normalWS) { return SafeNormalize(normalWS); }
float NovaLinearEyeDepth(float deviceDepth)
{
    if (IsPerspectiveProjection()) return LinearEyeDepth(deviceDepth, _ZBufferParams);
    #if UNITY_REVERSED_Z
    deviceDepth = 1.0 - deviceDepth;
    #endif
    return lerp(_ProjectionParams.y, _ProjectionParams.z, deviceDepth);
}
// HDRP evaluates atmospheric scattering in the fragment, rather than using URP fog factors.
float ComputeFogFactor(float clipZ) { return 0.0; }
#endif
