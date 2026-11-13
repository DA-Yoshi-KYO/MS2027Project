// Hologram effect for HDRP Unlit Shader Graph (SHG_Hologram).
// Ported from "SFHologram/HologramShader" by Andy Duboc (MIT License, see ../Original/LICENSE.txt).
// The math follows the original Built-in RP shader; each effect also gets a strength value
// so it can be turned off with 0 instead of the original shader_feature toggles.
#ifndef HOLOGRAM_HDRP_INCLUDED
#define HOLOGRAM_HDRP_INCLUDED

// ---------------------------------------------------------------------------
// Vertex: glitch. Shifts the mesh sideways (object X) for short random bursts.
// ---------------------------------------------------------------------------
void Hologram_Glitch_float(float3 PositionOS, float GlitchIntensity, float GlitchSpeed, out float3 Out)
{
    float t = _TimeParameters.x;
    float burst = step(0.5, sin(t * 2.0 + PositionOS.y)) * step(0.99, sin(t * GlitchSpeed * 0.5));
    Out = PositionOS + float3(GlitchIntensity * burst, 0.0, 0.0);
}

void Hologram_Glitch_half(half3 PositionOS, half GlitchIntensity, half GlitchSpeed, out half3 Out)
{
    float3 o;
    Hologram_Glitch_float(PositionOS, GlitchIntensity, GlitchSpeed, o);
    Out = o;
}

// ---------------------------------------------------------------------------
// Fragment: scanlines, glow band, rim (fresnel), flicker.
// ---------------------------------------------------------------------------
void Hologram_Surface_float(
    float4 MainTex, float4 MainColor, float4 RimColor,
    float3 PositionAWS, float3 NormalWS, float3 ViewDirWS,
    float4 Direction, float Brightness, float Alpha, float RimPower,
    float ScanTiling, float ScanSpeed, float ScanStrength,
    float GlowTiling, float GlowSpeed, float GlowStrength,
    UnityTexture2D FlickerTex, float FlickerSpeed, float FlickerStrength,
    float ExposureWeight,
    out float3 Color, out float OutAlpha)
{
    float t = _TimeParameters.x;

    // Position along the effect direction (same formula as the original, so old values still match).
    float4 dir4 = normalize(float4(Direction.xyz, 1.0));
    float dirVertex = (dot(float4(PositionAWS, 1.0), dir4) + 1.0) * 0.5;

    // Scanlines: hard stripes moving along Direction. (_Time.w = 3t in Built-in)
    float scan = step(frac(dirVertex * ScanTiling + t * 3.0 * ScanSpeed), 0.5) * ScanStrength;

    // Glow: wide sawtooth band sweeping along Direction. (_Time.x = t/20 in Built-in)
    float glow = frac(dirVertex * GlowTiling - t * 0.05 * GlowSpeed) * GlowStrength;

    // Flicker: read the control texture along time. (Built-in used _Time.xy as UV)
    float2 flickerUV = float2(t * 0.05, t) * FlickerSpeed;
    float flickerTex = SAMPLE_TEXTURE2D_LOD(FlickerTex.tex, FlickerTex.samplerstate, flickerUV, 0).r;
    float flicker = lerp(1.0, flickerTex, saturate(FlickerStrength));

    // Rim light (fresnel).
    float rim = 1.0 - saturate(dot(normalize(ViewDirWS), normalize(NormalWS)));
    float3 rimColor = RimColor.rgb * pow(rim, RimPower);

    float3 col = MainTex.rgb * MainColor.rgb + glow * 0.35 * MainColor.rgb + rimColor;
    col *= Brightness;

    // HDRP exposure: weight 0 = ignore scene exposure (looks the same in any scene),
    // weight 1 = affected by exposure like a normal emissive surface.
#ifdef SHADERGRAPH_PREVIEW
    float inverseExposure = 1.0;
#else
    float inverseExposure = GetInverseCurrentExposureMultiplier();
#endif
    Color = col * lerp(inverseExposure, 1.0, saturate(ExposureWeight));

    OutAlpha = saturate(MainTex.a * MainColor.a * Alpha * (scan + rim + glow) * flicker);
}

void Hologram_Surface_half(
    half4 MainTex, half4 MainColor, half4 RimColor,
    float3 PositionAWS, half3 NormalWS, half3 ViewDirWS,
    half4 Direction, half Brightness, half Alpha, half RimPower,
    half ScanTiling, half ScanSpeed, half ScanStrength,
    half GlowTiling, half GlowSpeed, half GlowStrength,
    UnityTexture2D FlickerTex, half FlickerSpeed, half FlickerStrength,
    half ExposureWeight,
    out half3 Color, out half OutAlpha)
{
    float3 c; float a;
    Hologram_Surface_float(MainTex, MainColor, RimColor, PositionAWS, NormalWS, ViewDirWS,
        Direction, Brightness, Alpha, RimPower, ScanTiling, ScanSpeed, ScanStrength,
        GlowTiling, GlowSpeed, GlowStrength, FlickerTex, FlickerSpeed, FlickerStrength,
        ExposureWeight, c, a);
    Color = c;
    OutAlpha = a;
}

#endif // HOLOGRAM_HDRP_INCLUDED
