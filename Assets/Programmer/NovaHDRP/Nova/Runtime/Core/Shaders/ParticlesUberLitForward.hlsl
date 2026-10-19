#ifndef NOVA_PARTICLESUBERLITFORWARD_INCLUDED
#define NOVA_PARTICLESUBERLITFORWARD_INCLUDED
#define FRAGMENT_USE_NORMAL_WS
#define FRAGMENT_USE_VIEW_DIR_WS
#define SHADERPASS SHADERPASS_FORWARD
#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#define PUNCTUAL_SHADOW_MEDIUM
#define DIRECTIONAL_SHADOW_MEDIUM
#define AREA_SHADOW_MEDIUM
#define SCREEN_SPACE_SHADOWS_OFF
#define _DISABLE_SSR
#define _DISABLE_SSR_TRANSPARENT
#define _DISABLE_DECALS
// Nova applies its own blend state; HDRP utility functions still require this constant.
static const float _BlendMode = 0;
#ifndef _RECEIVE_SHADOWS_ENABLED
#define _SURFACE_TYPE_TRANSPARENT
#define _DISABLE_RECEIVE_SHADOWS
#endif
#include "NovaHDRPCommon.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
#define HAS_LIGHTLOOP
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "ParticlesUberLit.hlsl"
#include "NovaHDRPFog.hlsl"
half GetMetallic(float3 uvw)
{
    #ifdef _SPECULAR_SETUP
    return 1;
    #else
    #ifdef _METALLIC_MAP_ENABLED
    half4 metallic = SAMPLE_METALLIC_MAP(uvw.xy, uvw.z);
    return metallic[(int)_MetallicMapChannelsX.x] * _Metallic;
    #else
    return _Metallic;
    #endif
    #endif
}

/**
 * \brief
 *  Get Smoothness Value.
 * \param uvw
 *  If smoothness map is Texture2D, uv.xy is used.<br/>
 *  But if it is TextureArray or Texture 3D, uv.xyz is is used.
 * \return
 *   If _SMOOTHNESS_MAP_ENABLED is defined, \n
 *  the returned value is multiplied the sampled value from smooth map by _Smoothness.\n
 *  It isn't defined, _Smoothness property value is returned.\n
 */
half GetSmoothness(float3 uvw)
{
    #ifdef _SMOOTHNESS_MAP_ENABLED
    const half4 smoothness = SAMPLE_SMOOTHNESS_MAP(uvw.xy, uvw.z);
    // The reason for multiplying _Smoothness is because it was done in URP's build-in shaders.
    return smoothness[(int)_SmoothnessMapChannelsX.x] * _Smoothness;
    #else
    return _Smoothness;
    #endif
}

/**
 * \brief
 *  Get Specular Color.
 * \param uvw
 *  If specular map is Texture2D, uv.xy is used.\n
 * But if it is TextureArray or Texture 3D, uv.xyz is is used.
 * \return
 *  If _SPECULAR_SETUP isn't defined, returned value is always half3( 0, 0, 0 ).\n
 *  If it is defined and _SPECULAR_MAP_ENABLED is defined,
 *  the returned value is  multiplied the sampled value from specular map by _SpecularColor.\n
 *  If _SPECULAR_MAP_ENABLED isn't defined, the returned value is _SpecularColor. 
 */
half3 GetSpecular(float3 uvw)
{
    #ifdef _SPECULAR_SETUP
    #ifdef _SPECULAR_MAP_ENABLED
    const half4 specular = SAMPLE_SPECULAR_MAP(uvw.xy, uvw.z);
    return specular.xyz * _SpecularColor.xyz;
    #else
    return _SpecularColor.xyz;
    #endif

    #else
    return half3(0, 0, 0);
    #endif
}


VaryingsLit vertLit(AttributesLit input)
{
    VaryingsLit output = (VaryingsLit)0;
    output.varyingsUnlit = vertUnlit(input.attributesUnlit, output.positionWS, true, false);
    #ifdef _NORMAL_MAP_ENABLED
    CalculateTangetAndBinormalInWorldSpace(output.tangentWS, output.binormalWS,
        output.varyingsUnlit.normalWS, input.tangentOS);
    #endif
    return output;
}
half4 fragLit(VaryingsLit input) : SV_Target
{
    half4 albedo = fragUnlit(input.varyingsUnlit, true);
    float3 uv = input.varyingsUnlit.baseMapUVAndProgresses.xyz;
    float3 normalWS = normalize(input.varyingsUnlit.normalWS);
    #ifdef _NORMAL_MAP_ENABLED
    float3 normalTS = SAMPLE_NORMAL_MAP(uv.xy, uv.z, _NormalMapBumpScale);
    normalWS = GetNormalWS(normalTS, input.tangentWS.xyz, input.binormalWS, normalWS);
    #endif
    float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
    PositionInputs posInput = GetPositionInput(input.varyingsUnlit.positionHCS.xy,
        _ScreenSize.zw, input.varyingsUnlit.positionHCS.z, input.varyingsUnlit.positionHCS.w, input.positionWS);
    SurfaceData surface = (SurfaceData)0;
    surface.materialFeatures = MATERIALFEATUREFLAGS_LIT_STANDARD;
    surface.baseColor = albedo.rgb;
    surface.normalWS = normalWS;
    surface.geomNormalWS = normalize(input.varyingsUnlit.normalWS);
    surface.tangentWS = GetLocalFrame(normalWS)[0];
    surface.perceptualSmoothness = GetSmoothness(uv);
    surface.metallic = GetMetallic(uv);
    surface.ambientOcclusion = 1;
    surface.specularOcclusion = 1;
    surface.ior = 1.5;
    #ifdef _SPECULAR_SETUP
    surface.materialFeatures |= MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR;
    surface.specularColor = GetSpecular(uv);
    #endif
    BuiltinData builtin;
    InitBuiltinData(posInput, albedo.a, normalWS, -normalWS, 0, 0, builtin);
    PostInitBuiltinData(V, posInput, surface, builtin);
    BSDFData bsdf = ConvertSurfaceDataToBSDFData(posInput.positionSS, surface);
    #ifndef _SPECULAR_HIGHLIGHTS_ENABLED
    bsdf.fresnel0 = 0;
    #endif
    PreLightData preLight = GetPreLightData(V, posInput, bsdf);
    uint flags = LIGHT_FEATURE_MASK_FLAGS_TRANSPARENT;
    #ifndef _ENVIRONMENT_REFLECTIONS_ENABLED
    flags &= ~(LIGHTFEATUREFLAGS_ENV | LIGHTFEATUREFLAGS_SKY);
    #endif
    LightLoopOutput lighting;
    LightLoop(V, posInput, preLight, bsdf, builtin, flags, lighting);
    float3 color = (lighting.diffuseLighting + lighting.specularLighting) * GetCurrentExposureMultiplier();
    return NovaApplyFog(half4(color, albedo.a), input.varyingsUnlit.positionHCS);
}
#endif
