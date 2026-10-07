// Cel shading for hair / cloth (HDRP Unlit Shader Graph).
// Half-Lambert band with a narrow soft edge, saturated shadow colour, and a lit-side rim light.
#ifndef NTE_CEL_BODY_INCLUDED
#define NTE_CEL_BODY_INCLUDED
void NTE_CelBody_float(float4 Tex, float4 BaseColor, float3 ShadowColor,
    float3 NormalWS, float3 ViewWS, float3 MainLightDirection,
    float ShadowThreshold, float ShadowSoftness, float ShadowSaturation,
    float3 RimColor, float RimStrength, float RimWidth,
    float ShadowStrength, float Brightness,
    out float3 Color)
{
    float3 N = NormalWS * rsqrt(max(dot(NormalWS, NormalWS), 1e-6));
    float3 V = ViewWS * rsqrt(max(dot(ViewWS, ViewWS), 1e-6));
    float3 L = dot(MainLightDirection, MainLightDirection) > 1e-6
        ? normalize(MainLightDirection) : normalize(float3(0.3, 0.7, 0.6));

    float halfLambert = dot(N, L) * 0.5 + 0.5;
    float w = max(ShadowSoftness, 0.001);
    float lit = smoothstep(ShadowThreshold - w, ShadowThreshold + w, halfLambert);

    float3 albedo = Tex.rgb * BaseColor.rgb;
    // Shadow colour is derived from the texture itself: darken slightly and push toward
    // the texture's own hue (albedo / max channel), so red stays red and white turns soft grey-blue.
    float maxC = max(max(albedo.r, albedo.g), max(albedo.b, 1e-4));
    float3 hue = albedo / maxC;
    float3 shadowCol = albedo * saturate(ShadowColor) * lerp(1.0.xxx, hue, saturate(ShadowSaturation));

    // Directional light only darkens slightly; real shadows come from cast shadows (Shadow Matte).
    float3 col = lerp(albedo, shadowCol, (1.0 - lit) * saturate(ShadowStrength)) * Brightness;

    float fresnel = 1.0 - saturate(dot(N, V));
    float rimEdge = 1.0 - saturate(RimWidth);
    float rim = smoothstep(rimEdge, rimEdge + 0.08, fresnel) * lit * max(RimStrength, 0.0);
    Color = col + RimColor * rim * Brightness;
}
void NTE_CelBody_half(half4 Tex, half4 BaseColor, half3 ShadowColor,
    half3 NormalWS, half3 ViewWS, half3 MainLightDirection,
    half ShadowThreshold, half ShadowSoftness, half ShadowSaturation,
    half3 RimColor, half RimStrength, half RimWidth,
    half ShadowStrength, half Brightness,
    out half3 Color)
{
    float3 r;
    NTE_CelBody_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, MainLightDirection,
        ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth,
        ShadowStrength, Brightness, r);
    Color = r;
}
#endif
