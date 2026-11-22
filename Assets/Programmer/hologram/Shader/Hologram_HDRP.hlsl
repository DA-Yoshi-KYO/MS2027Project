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
// Image: scroll the main texture, and cut one color out of it.
// The defaults (scroll 0, key strength 0) return the texture untouched.
// ---------------------------------------------------------------------------
float3 Hologram_ToSRGB(float3 c)
{
    c = saturate(c);
    return lerp(c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055, step(0.0031308, c));
}

float3 Hologram_ToLinear(float3 c)
{
    c = saturate(c);
    return lerp(c / 12.92, pow((c + 0.055) / 1.055, 2.4), step(0.04045, c));
}

void Hologram_Image_float(
    float4 UV, UnityTexture2D Tex, UnityTexture2D KeepMask,
    float ScrollX, float ScrollY,
    float4 KeyColor, float KeyStrength, float KeyTolerance, float KeySoftness,
    out float4 Out)
{
    // Subtracting moves the picture itself to +X (right) / +Y (up).
    float2 scroll = frac(_TimeParameters.x * float2(ScrollX, ScrollY));
    float2 uv = Tex.GetTransformedUV(UV.xy) - scroll;

    float4 tex = SAMPLE_TEXTURE2D(Tex.tex, Tex.samplerstate, uv);
    float keep = smoothstep(0.5, 1.0, SAMPLE_TEXTURE2D(KeepMask.tex, KeepMask.samplerstate, uv).r);

    // Compare in sRGB, so the tolerance means the same for dark and bright key colors.
    float3 c = Hologram_ToSRGB(tex.rgb);
    float3 k = Hologram_ToSRGB(KeyColor.rgb);
    float3 diff = abs(c - k);
    float keyed = saturate((max(diff.r, max(diff.g, diff.b)) - KeyTolerance) / max(KeySoftness, 0.001));
    keyed = max(keyed, keep);

    // Take the key color out of the half transparent edge, otherwise it stays as a fringe.
    float3 cleaned = Hologram_ToLinear(k + (c - k) / max(keyed, 0.001));

    float amount = saturate(KeyStrength);
    Out.rgb = lerp(tex.rgb, cleaned, amount);
    Out.a = tex.a * lerp(1.0, keyed, amount);
}

void Hologram_Image_half(
    half4 UV, UnityTexture2D Tex, UnityTexture2D KeepMask,
    half ScrollX, half ScrollY,
    half4 KeyColor, half KeyStrength, half KeyTolerance, half KeySoftness,
    out half4 Out)
{
    float4 o;
    Hologram_Image_float(UV, Tex, KeepMask, ScrollX, ScrollY, KeyColor, KeyStrength, KeyTolerance, KeySoftness, o);
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
    float ExposureWeight, float BaseVisibility,
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

    // BaseVisibility 0 = only lit parts are visible (original), 1 = the whole picture stays visible.
    float visibility = lerp(scan + rim + glow, 1.0, saturate(BaseVisibility));
    OutAlpha = saturate(MainTex.a * MainColor.a * Alpha * visibility * flicker);
}

void Hologram_Surface_half(
    half4 MainTex, half4 MainColor, half4 RimColor,
    float3 PositionAWS, half3 NormalWS, half3 ViewDirWS,
    half4 Direction, half Brightness, half Alpha, half RimPower,
    half ScanTiling, half ScanSpeed, half ScanStrength,
    half GlowTiling, half GlowSpeed, half GlowStrength,
    UnityTexture2D FlickerTex, half FlickerSpeed, half FlickerStrength,
    half ExposureWeight, half BaseVisibility,
    out half3 Color, out half OutAlpha)
{
    float3 c; float a;
    Hologram_Surface_float(MainTex, MainColor, RimColor, PositionAWS, NormalWS, ViewDirWS,
        Direction, Brightness, Alpha, RimPower, ScanTiling, ScanSpeed, ScanStrength,
        GlowTiling, GlowSpeed, GlowStrength, FlickerTex, FlickerSpeed, FlickerStrength,
        ExposureWeight, BaseVisibility, c, a);
    Color = c;
    OutAlpha = a;
}

#endif // HOLOGRAM_HDRP_INCLUDED
