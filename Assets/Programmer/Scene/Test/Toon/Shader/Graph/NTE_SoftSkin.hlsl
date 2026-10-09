// Stylized approximation: wrap diffuse + warm terminator + restrained broad specular.
// References: NVIDIA GPU Gems, chapter 16; see the accompanying implementation notes.
#ifndef NTE_SOFT_SKIN_INCLUDED
#define NTE_SOFT_SKIN_INCLUDED
void NTE_SoftSkin_float(float3 Albedo, float3 NormalWS, float3 ViewWS,
    float3 SkinShadowTint, float SkinShadowStrength, float SkinShadowThreshold,
    float SkinShadowSoftness, float SkinLightWrap, float SkinFormShading,
    float3 SkinScatterTint, float SkinScatterStrength, float SkinSpecularStrength,
    float SkinRoughness, float SkinRimStrength, float3 MainLightDirection,
    out float3 Color)
{
    float3 N = NormalWS * rsqrt(max(dot(NormalWS, NormalWS), 1e-6));
    float3 V = ViewWS * rsqrt(max(dot(ViewWS, ViewWS), 1e-6));
    float3 L = dot(MainLightDirection, MainLightDirection) > 1e-6
        ? normalize(MainLightDirection) : normalize(float3(0.3, 0.7, 0.6));
    float ndl = dot(N, L);
    float wrap = saturate(SkinLightWrap);
    float wrapped = saturate((ndl + wrap) / (1.0 + wrap));
    float width = max(SkinShadowSoftness, 0.02);
    float lightBand = smoothstep(SkinShadowThreshold - width * 0.5,
                                SkinShadowThreshold + width * 0.5, wrapped);
    float shadow = (1.0 - lightBand) * saturate(SkinShadowStrength);
    float3 diffuse = Albedo * lerp(1.0.xxx, saturate(SkinShadowTint), shadow);
    diffuse *= 1.0 - saturate(SkinFormShading) * (1.0 - wrapped);
    // Warmth follows the terminator, not a uniform red overlay. This is not physical SSS.
    float scatter = 4.0 * lightBand * (1.0 - lightBand);
    diffuse += Albedo * SkinScatterTint * max(SkinScatterStrength, 0.0) * scatter;
    float3 H = L + V;
    H *= rsqrt(max(dot(H, H), 1e-6));
    float exponent = lerp(96.0, 8.0, saturate(SkinRoughness));
    float specular = pow(saturate(dot(N, H)), exponent) * saturate(ndl)
                   * max(SkinSpecularStrength, 0.0);
    float rim = pow(1.0 - saturate(dot(N, V)), 4.0) * lightBand
              * max(SkinRimStrength, 0.0);
    Color = max(diffuse + float3(1.0, 0.96, 0.91) * (specular + rim), 0.0);
}
void NTE_SoftSkin_half(half3 Albedo, half3 NormalWS, half3 ViewWS,
    half3 SkinShadowTint, half SkinShadowStrength, half SkinShadowThreshold,
    half SkinShadowSoftness, half SkinLightWrap, half SkinFormShading,
    half3 SkinScatterTint, half SkinScatterStrength, half SkinSpecularStrength,
    half SkinRoughness, half SkinRimStrength, half3 MainLightDirection,
    out half3 Color)
{
    float3 result;
    NTE_SoftSkin_float(Albedo, NormalWS, ViewWS, SkinShadowTint, SkinShadowStrength,
        SkinShadowThreshold, SkinShadowSoftness, SkinLightWrap, SkinFormShading,
        SkinScatterTint, SkinScatterStrength, SkinSpecularStrength, SkinRoughness,
        SkinRimStrength, MainLightDirection, result);
    Color = result;
}
#endif
