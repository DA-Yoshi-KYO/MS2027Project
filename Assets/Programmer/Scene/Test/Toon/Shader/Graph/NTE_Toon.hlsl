// =====================================================================
// NTE Toon – per-material stylised shading for HDRP Unlit Shader Graphs
// (Star Dive / anime-game look: light, soft shading, colour-kept shadows,
//  cast shadows via Shadow Matte, highlights per material type).
// Every function shares one signature so the Shader Graphs are identical
// apart from the function name and property defaults.
// =====================================================================
#ifndef NTE_TOON_INCLUDED
#define NTE_TOON_INCLUDED

struct NTEToonInput
{
    float3 albedo, N, V, L, T;
    float3 shadowColor, rimColor, specColor;
    float threshold, softness, saturation, rimStrength, rimWidth;
    float shadowStrength, brightness, specStrength, specSize;
};

NTEToonInput NTE_MakeInput(float4 Tex, float4 BaseColor, float3 ShadowColor,
    float3 NormalWS, float3 ViewWS, float3 TangentWS, float3 LightDir,
    float Threshold, float Softness, float Saturation,
    float3 RimColor, float RimStrength, float RimWidth,
    float ShadowStrength, float Brightness,
    float3 SpecColor, float SpecStrength, float SpecSize)
{
    NTEToonInput i;
    i.albedo = Tex.rgb * BaseColor.rgb;
    i.N = NormalWS * rsqrt(max(dot(NormalWS, NormalWS), 1e-6));
    i.V = ViewWS * rsqrt(max(dot(ViewWS, ViewWS), 1e-6));
    i.T = TangentWS * rsqrt(max(dot(TangentWS, TangentWS), 1e-6));
    i.L = dot(LightDir, LightDir) > 1e-6 ? normalize(LightDir) : normalize(float3(0.3, 0.7, 0.6));
    i.shadowColor = saturate(ShadowColor); i.rimColor = RimColor; i.specColor = SpecColor;
    i.threshold = Threshold; i.softness = max(Softness, 0.001); i.saturation = saturate(Saturation);
    i.rimStrength = max(RimStrength, 0.0); i.rimWidth = saturate(RimWidth);
    i.shadowStrength = saturate(ShadowStrength); i.brightness = Brightness;
    i.specStrength = max(SpecStrength, 0.0); i.specSize = saturate(SpecSize);
    return i;
}

// 0 = shadow, 1 = lit (half-Lambert with a wide soft edge)
float NTE_LitBand(NTEToonInput i)
{
    float hl = dot(i.N, i.L) * 0.5 + 0.5;
    return smoothstep(i.threshold - i.softness, i.threshold + i.softness, hl);
}

// Shadow colour taken from the texture: tinted, darkened, keeps its hue (no grey mud).
float3 NTE_ShadowColor(NTEToonInput i)
{
    float m = max(max(i.albedo.r, i.albedo.g), max(i.albedo.b, 1e-4));
    return i.albedo * i.shadowColor * lerp(1.0.xxx, i.albedo / m, i.saturation);
}

float3 NTE_Diffuse(NTEToonInput i, float lit)
{
    return lerp(i.albedo, NTE_ShadowColor(i), (1.0 - lit) * i.shadowStrength);
}

float NTE_Fresnel(NTEToonInput i) { return 1.0 - saturate(dot(i.N, i.V)); }

float3 NTE_Rim(NTEToonInput i, float lit)
{
    float e = 1.0 - i.rimWidth;
    return i.rimColor * smoothstep(e, e + 0.08, NTE_Fresnel(i)) * lerp(0.35, 1.0, lit) * i.rimStrength;
}

// View-space normal (for matcap-style reflections that follow the camera)
float3 NTE_ViewNormal(float3 N) { return normalize(mul((float3x3)UNITY_MATRIX_V, N)); }

// Gold / brass detection from the texture (hue ~30-60deg, saturated, not dark)
float NTE_GoldMask(float3 c)
{
    float mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b));
    float sat = (mx - mn) / max(mx, 1e-4);
    float hueOk = step(c.b, c.g) * step(c.g, c.r) * smoothstep(0.25, 0.45, (c.g - c.b) / max(c.r - c.b, 1e-4));
    return hueOk * smoothstep(0.3, 0.5, sat) * smoothstep(0.25, 0.4, mx);
}

// Stylised toon metal: bright top / dark bottom reflection band + sharp camera highlight.
float3 NTE_MetalShade(NTEToonInput i, float3 base, float lit)
{
    float3 nv = NTE_ViewNormal(i.N);
    float band = smoothstep(-0.25, 0.35, nv.y);                 // sky-ish upper reflection
    float3 col = lerp(base * 0.45, base * 1.35, band);
    col = lerp(col * 0.85, col, lit);
    float3 hv = normalize(float3(-0.35, 0.55, 1.0));            // fixed camera-space key
    float spec = smoothstep(1.0 - i.specSize * 0.25, 1.0 - i.specSize * 0.25 + 0.02, dot(nv, hv));
    col += i.specColor * spec * i.specStrength;
    col += base * pow(NTE_Fresnel(i), 3.0) * 0.6;              // metallic edge glint
    return col;
}

#define NTE_ARGS float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS, float3 MainLightDirection, \
    float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor, float RimStrength, float RimWidth, \
    float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength, float SpecSize, out float3 Color
#define NTE_INPUT NTEToonInput i = NTE_MakeInput(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, \
    ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength, SpecSize)
#define NTE_HALF_ARGS half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS, half3 MainLightDirection, \
    half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor, half RimStrength, half RimWidth, \
    half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength, half SpecSize, out half3 Color
#define NTE_HALF_FORWARD(name) void name##_half(NTE_HALF_ARGS) { float3 c; name##_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, \
    MainLightDirection, ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, \
    SpecColor, SpecStrength, SpecSize, c); Color = c; }

// ---------------- Cloth: soft band, fabric sheen, auto gold buttons ----------------
void NTE_Cloth_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    col += i.albedo * pow(NTE_Fresnel(i), 3.0) * 0.12 * lit;     // soft fabric sheen at grazing angles
    float gold = NTE_GoldMask(i.albedo) * step(0.001, i.specStrength);
    col = lerp(col, NTE_MetalShade(i, i.albedo, lit), gold);
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Cloth)

// ---------------- Skin: almost no directional shading, warm cast shadows ----------------
void NTE_Skin_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    float terminator = 4.0 * lit * (1.0 - lit);                 // faint warm blush at the shadow edge
    col += i.albedo * float3(0.10, 0.03, 0.02) * terminator * i.shadowStrength;
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Skin)

// ---------------- Hair: soft band, top-lit gradient, anisotropic angel ring ----------------
void NTE_Hair_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    col *= lerp(0.93, 1.04, saturate(i.N.y * 0.5 + 0.5));       // light from above, cooler underneath
    // Kajiya-Kay along the strand (tangent), using the view as the highlight light so the ring stays visible.
    float3 H = normalize(i.L + i.V * 2.0);
    // VRoid hair UVs run along the strand in V, so the strand direction is the bitangent.
    float3 strand = normalize(cross(i.N, i.T));
    float tdh = dot(strand, H);
    float sinTH = sqrt(saturate(1.0 - tdh * tdh));
    float ring = smoothstep(1.0 - i.specSize * 0.2, 1.0 - i.specSize * 0.2 + 0.03, sinTH);
    col += i.specColor * lerp(1.0.xxx, i.albedo, 0.35) * ring * i.specStrength * lerp(0.4, 1.0, lit);
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Hair)

// ---------------- Leather: dark base with a broad satin sheen ----------------
void NTE_Leather_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    float3 nv = NTE_ViewNormal(i.N);
    float3 hv = normalize(float3(-0.3, 0.6, 1.0));
    float ndh = saturate(dot(nv, hv));
    float expo = lerp(8.0, 96.0, 1.0 - i.specSize);
    float sheen = smoothstep(0.15, 0.6, pow(ndh, expo));        // toon-ish but soft sheen blob
    col += i.specColor * sheen * i.specStrength;
    col += i.specColor * pow(NTE_Fresnel(i), 4.0) * 0.15 * lit; // leather edge highlight
    float gold = NTE_GoldMask(i.albedo);                         // buckles / studs
    col = lerp(col, NTE_MetalShade(i, i.albedo, lit), gold);
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Leather)

// ---------------- Metal: whole material is metal ----------------
void NTE_Metal_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    Color = (NTE_MetalShade(i, i.albedo, lit) + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Metal)

// ---------------- Eye (transparent iris): brighter iris, glowing painted highlights, camera catchlight ----------------
void NTE_Eye_float(float4 Tex, float4 BaseColor, float3 NormalWS, float Brightness,
    float HighlightBoost, float IrisSaturation, float CatchlightStrength, float CatchlightSize, out float3 Color)
{
    float3 c = Tex.rgb * BaseColor.rgb;
    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
    c = max(lerp(l.xxx, c, 1.0 + IrisSaturation), 0.0);         // richer iris colour
    float hl = smoothstep(0.7, 0.95, max(c.r, max(c.g, c.b)) * (1.0 - (max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b)))));
    c += hl * HighlightBoost;                                    // painted highlights go HDR -> bloom sparkle
    float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, NormalWS));
    float cl = smoothstep(1.0 - CatchlightSize * 0.05, 1.0 - CatchlightSize * 0.05 + 0.004,
                          dot(nv, normalize(float3(-0.3, 0.45, 1.0))));
    c += cl * CatchlightStrength;
    Color = c * Brightness;
}
void NTE_Eye_half(half4 Tex, half4 BaseColor, half3 NormalWS, half Brightness, half HighlightBoost,
    half IrisSaturation, half CatchlightStrength, half CatchlightSize, out half3 Color)
{
    float3 c; NTE_Eye_float(Tex, BaseColor, NormalWS, Brightness, HighlightBoost, IrisSaturation, CatchlightStrength, CatchlightSize, c); Color = c;
}
#endif
