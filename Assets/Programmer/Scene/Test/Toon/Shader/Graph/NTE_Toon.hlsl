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

// ---------------- Environment from above (moon / sky) ----------------
// Light comes from above: up-facing surfaces (crown of the head, shoulders, back of the hand) get extra light,
// down-facing surfaces (palms, skirt underside, under the chin) and back faces (inside of skirts / sleeves)
// fall into the texture-coloured shadow. FaceSign: 1 = front face, 0 = back face.
float3 NTE_Environment(NTEToonInput i, float3 col, float lit, float faceSign, float sky, float bottom, float back)
{
    float front = saturate(faceSign);
    float up = i.N.y;
    float top = smoothstep(0.25, 0.95, up) * front;
    float under = smoothstep(0.1, -0.6, up) * front;
    float3 shade = NTE_ShadowColor(i);
    col *= 1.0 + max(sky, 0.0) * top * lerp(0.6, 1.0, lit);
    col = lerp(col, min(col, shade), saturate(bottom) * under);
    col = lerp(col, shade * 0.7, saturate(back) * (1.0 - front));          // inside of skirts / sleeves
    return col;
}

// ---------------- Fabric weave (fine fibres) ----------------
// Plain-weave thread pattern in UV space + per-thread fibre noise. Fades out when it gets
// smaller than ~1.5 px so it never shimmers in the distance. Returns -0.5..0.5.
float NTE_Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
float NTE_Fabric(float2 uv, float scale)
{
    float2 p = uv * max(scale, 1.0);
    float2 fw = fwidth(p);
    float fade = saturate(1.5 - max(fw.x, fw.y) * 2.0);
    float2 cell = floor(p);
    float2 f = frac(p);
    float checker = abs(fmod(cell.x + cell.y, 2.0));
    float thread = lerp(sin(f.x * 3.14159), sin(f.y * 3.14159), checker);     // warp / weft on top
    float fibre = NTE_Hash(cell + floor(f * 4.0) * 0.37);                      // fine fibre noise inside a thread
    return (thread * 0.75 + fibre * 0.25 - 0.5) * fade;
}

// Slub / heather grain: thread-direction streaks at a much coarser scale than the weave,
// so cloth still reads as fabric at game-camera distance. Returns -0.5..0.5.
float NTE_ValueNoise(float2 p)
{
    float2 c = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(NTE_Hash(c), NTE_Hash(c + float2(1, 0)), f.x),
                lerp(NTE_Hash(c + float2(0, 1)), NTE_Hash(c + float2(1, 1)), f.x), f.y);
}
float NTE_FabricGrain(float2 uv, float scale)
{
    float2 p = uv * max(scale, 1.0);
    float2 fw = fwidth(p);
    float fade = saturate(2.0 - max(fw.x, fw.y) * 2.5);
    float warp = NTE_ValueNoise(p * float2(1.0, 7.0));          // streaks along U threads
    float weft = NTE_ValueNoise(p * float2(7.0, 1.0) + 17.0);    // streaks along V threads
    float blotch = NTE_ValueNoise(p * 0.35 + 5.0);               // soft uneven dye
    return ((warp + weft) * 0.35 + blotch * 0.3 - 0.5) * fade;
}

// ---------------- Spot / point lights for the Unlit toon (read straight from HDRP) ----------------
// HDRP Unlit does not light itself, but with Shadow Matte enabled the forward pass already includes the
// HDRP light list and shadow code (LightLoopDef / HDShadow / PunctualLightCommon). So the scene's real
// spot & point lights are evaluated here with HDRP's own data: same intensity (physical units), same
// range / cone falloff, same exposure and the light's real shadow map -> it matches the HDRP Lit model.
// Directional lights are excluded (the main light is the toon band). Nothing to set up per light.
#if defined(SHADERPASS) && defined(SHADERPASS_FORWARD_UNLIT) && defined(_ENABLE_SHADOW_MATTE) && !defined(SHADERGRAPH_PREVIEW)
    #if SHADERPASS == SHADERPASS_FORWARD_UNLIT
        #define NTE_HDRP_LIGHTS 1
    #endif
#endif

// Sharpness (0..1): 0 = soft photographic edge, 1 = hard cel edge. The light term and the shadow-map term
// are each pushed through a step + feather (as in Unity Toon Shader's "Step / Feather"), after a small blur
// that removes shadow-map stair-steps - so the edge is crisp like anime, but never jagged.
float3 NTE_PunctualLights(NTEToonInput i, float3 posAbsWS, float faceSign, float sharpness)
{
    float3 sum = 0.0;
    float feather = lerp(0.35, 0.03, saturate(sharpness));
#ifdef NTE_HDRP_LIGHTS
    float3 posRWS = GetCameraRelativePositionWS(posAbsWS);
    float2 posSS = ComputeNormalizedDeviceCoordinates(posRWS, UNITY_MATRIX_VP) * _ScreenSize.xy;
    HDShadowContext shadowContext = InitShadowContext();
    uint count = _PunctualLightCount;
    uint used = 0;
    [loop] for (uint k = 0; k < count && used < 4; k++)                  // at most 4 lights per pixel
    {
        LightData light = FetchLight(k);
        if (light.lightType != GPULIGHTTYPE_POINT && light.lightType != GPULIGHTTYPE_SPOT && light.lightType != GPULIGHTTYPE_PROJECTOR_PYRAMID)
            continue;
        if (light.diffuseDimmer <= 0.0)
            continue;

        float3 L; float4 distances;                                      // {d, d^2, 1/d, d_proj}
        GetPunctualLightVectors(posRWS, light, L, distances);
        if (distances.x >= light.range)
            continue;
        float att = PunctualLightAttenuation(distances, light.rangeAttenuationScale, light.rangeAttenuationBias,
                                             light.angleScale, light.angleOffset);   // inverse square + range + cone
        float ndl = dot(i.N, L);
        if (att <= 1e-4 || ndl <= -feather)
            continue;                                                     // cheap early outs before any shadow lookup
        used++;

        // Lambert like HDRP Lit, terminator turned into a cel edge with a feather.
        float diff = smoothstep(-feather * 0.5, feather * 1.5, ndl) * lerp(0.6, 1.0, saturate(ndl));

        // Real shadow map of this light (skirt -> thighs, hair -> face, arm -> body ...).
        // 4 rotated taps + centre on a small disc perpendicular to the light (HDRP filters each tap too),
        // then step + feather: clean anime edge without shadow-map stair-steps.
        float shadow = 1.0;
        if (light.shadowIndex >= 0 && light.shadowDimmer > 0.0)
        {
            float3 t1 = normalize(cross(L, abs(L.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
            float3 t2 = cross(L, t1);
            float r = 0.004 * distances.x + 0.003;                       // ~2.3 cm at 5 m
            bool isPoint = light.lightType == GPULIGHTTYPE_POINT;
            float acc = GetPunctualShadowAttenuation(shadowContext, posSS, posRWS, i.N, light.shadowIndex, L, distances.x, isPoint, true);
            acc += GetPunctualShadowAttenuation(shadowContext, posSS, posRWS + (t1 * 0.92 + t2 * 0.38) * r, i.N, light.shadowIndex, L, distances.x, isPoint, true);
            acc += GetPunctualShadowAttenuation(shadowContext, posSS, posRWS + (-t1 * 0.38 + t2 * 0.92) * r, i.N, light.shadowIndex, L, distances.x, isPoint, true);
            acc += GetPunctualShadowAttenuation(shadowContext, posSS, posRWS + (-t1 * 0.92 - t2 * 0.38) * r, i.N, light.shadowIndex, L, distances.x, isPoint, true);
            acc += GetPunctualShadowAttenuation(shadowContext, posSS, posRWS + (t1 * 0.38 - t2 * 0.92) * r, i.N, light.shadowIndex, L, distances.x, isPoint, true);
            shadow = smoothstep(0.5 - feather, 0.5 + feather, acc * 0.2);
            shadow = lerp(1.0, shadow, light.shadowDimmer);
        }
        sum += light.color * (att * diff * shadow * light.diffuseDimmer);
    }
    sum *= INV_PI * GetCurrentExposureMultiplier();                      // HDRP Lambert + camera exposure
#endif
    // Divided by the material brightness: final = base * Brightness + light (light keeps HDRP strength).
    return i.albedo * sum * lerp(0.15, 1.0, saturate(faceSign)) / max(i.brightness, 0.05);   // inside of skirts stays dark
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
    float mx = max(base.r, max(base.g, base.b));
    // Small parts (buttons) are flat on the mesh, so their shape comes from the painted texture:
    // bright painted areas = facing the sky, dark painted areas = reflecting the ground.
    float painted = smoothstep(0.35, 0.85, mx);
    float band = saturate(smoothstep(-0.25, 0.35, nv.y) * 0.55 + painted * 0.65);
    float3 tint = base / max(mx, 1e-4);                          // pure metal hue
    float3 col = lerp(base * 0.3, tint * mx * 1.7, band);       // high contrast = metallic
    col = lerp(col * 0.8, col, lit);
    float3 hv = normalize(float3(-0.35, 0.55, 1.0));            // fixed camera-space key
    float spec = smoothstep(1.0 - i.specSize * 0.25, 1.0 - i.specSize * 0.25 + 0.02, dot(nv, hv));
    float3 H = normalize(i.L + i.V);
    float specL = smoothstep(0.93, 0.96, saturate(dot(i.N, H))) * lit;   // real moon glint
    float glint = smoothstep(0.8, 0.95, mx);                     // painted highlight -> HDR sparkle
    col += i.specColor * lerp(1.0.xxx, tint, 0.4) * (spec + specL + glint * 0.8) * i.specStrength;
    col += tint * mx * pow(NTE_Fresnel(i), 3.0) * 0.6;          // metallic edge glint
    return col;
}

#define NTE_ARGS float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS, float3 MainLightDirection, \
    float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor, float RimStrength, float RimWidth, \
    float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength, float SpecSize, \
    float FaceSign, float4 UV, float SkyLight, float BottomShade, float BackFaceShade, float DetailScale, float DetailStrength, \
    float3 PositionWS, float FabricSheen, float LightSharpness, out float3 Color
#define NTE_INPUT NTEToonInput i = NTE_MakeInput(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, \
    ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength, SpecSize)
#define NTE_HALF_ARGS half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS, half3 MainLightDirection, \
    half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor, half RimStrength, half RimWidth, \
    half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength, half SpecSize, \
    half FaceSign, half4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale, half DetailStrength, \
    float3 PositionWS, half FabricSheen, half LightSharpness, out half3 Color
#define NTE_HALF_FORWARD(name) void name##_half(NTE_HALF_ARGS) { float3 c; name##_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, \
    MainLightDirection, ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, \
    SpecColor, SpecStrength, SpecSize, FaceSign, UV, SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen, LightSharpness, c); Color = c; }
#define NTE_ENV(col, lit) col = NTE_Environment(i, col, lit, FaceSign, SkyLight, BottomShade, BackFaceShade) + NTE_PunctualLights(i, PositionWS, FaceSign, LightSharpness)

// ---------------- Cloth: soft band, fabric sheen, auto gold buttons ----------------
void NTE_Cloth_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    float gold = NTE_GoldMask(i.albedo) * step(0.001, i.specStrength);
    float notGold = 1.0 - gold;
    float weave = 0.0, grain = 0.0;
    [branch] if (DetailStrength > 0.001)                                  // material-uniform: free to skip
    {
        weave = NTE_Fabric(UV.xy, DetailScale) * DetailStrength * notGold;           // close-up threads
        grain = NTE_FabricGrain(UV.xy, DetailScale * 0.1) * DetailStrength * notGold; // visible at game distance
    }
    col *= 1.0 + (weave * 2.0 + grain * 1.6) * lerp(0.6, 1.0, lit);
    // Velvet / cloth profile: fibres catch light at grazing angles (bright fuzzy edge),
    // faces looking straight at the camera go slightly matte and dusty.
    float nv = saturate(dot(i.N, i.V));
    float sheen = pow(1.0 - nv, 2.0) * (1.0 + max(weave, 0.0) * 3.0);
    float3 sheenCol = lerp(i.albedo, 1.0.xxx, 0.35);
    col *= lerp(1.0, 0.9, FabricSheen * nv * nv * notGold);
    col += sheenCol * sheen * FabricSheen * 0.45 * lerp(0.35, 1.0, lit) * notGold;
    NTE_ENV(col, lit);
    [branch] if (gold > 0.001) col = lerp(col, NTE_MetalShade(i, i.albedo, lit), gold);   // only on buttons
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Cloth)

// ---------------- Skin: soft absorbed light + red subsurface scattering in the shade ----------------
// Skin graph re-uses two shared inputs (labels differ in SHG_NTE_Skin):
//   FabricSheen    -> "Skin Scatter (Red Shade)" : how red the shade turns near the lit skin
//   DetailStrength -> "Skin Absorption"         : how much light sinks into the skin (soft wrap, no hard white)
float3 NTE_SkinScatter(NTEToonInput i, float3 col, float scatter)
{
    // How far this pixel has been darkened from the plain texture (band, cast-shadow side, underside, back face).
    float lumA = dot(i.albedo, float3(0.2126, 0.7152, 0.0722));
    float lumC = dot(col, float3(0.2126, 0.7152, 0.0722));
    float d = saturate(1.0 - lumC / max(lumA, 1e-4));
    // Light scattered under the skin comes back out red: strongest in the half-shade next to the lit skin,
    // weaker in deep shade.
    float edge = saturate(d * 3.0) * lerp(1.0, 0.45, saturate(d * 1.5 - 0.3));
    float3 red = col * float3(1.06, 0.90, 0.86);
    red += i.albedo * float3(0.05, 0.008, 0.0) * edge;              // a little blood colour bleeding back
    return lerp(col, red, saturate(scatter) * edge);
}

void NTE_Skin_float(NTE_ARGS)
{
    NTE_INPUT;
    float absorb = saturate(DetailStrength);
    // Light sinks into skin: wrap the lighting so the gradient is long and soft instead of a cel step.
    float ndl = dot(i.N, i.L);
    float wrap = saturate((ndl + 0.6) / 1.6);
    wrap = wrap * wrap * (3.0 - 2.0 * wrap);
    float lit = lerp(NTE_LitBand(i), wrap, absorb * 0.7);
    float3 col = NTE_Diffuse(i, lit);
    // Absorption: the lit side never goes chalky white, the deep side keeps a faint warm glow from inside.
    col *= lerp(1.0, lerp(0.93, 1.0, 1.0 - lit), absorb);
    col += i.albedo * float3(0.03, 0.006, 0.0) * (1.0 - lit) * absorb;
    NTE_ENV(col, lit);
    col = NTE_SkinScatter(i, col, FabricSheen);
    // Very soft, velvety sheen (fine hair on the skin) instead of a plastic edge.
    col += i.albedo * pow(NTE_Fresnel(i), 4.0) * 0.08 * absorb * lit;
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Skin)

// ---------------- Hair: soft band, top-lit gradient, anisotropic angel ring ----------------
void NTE_Hair_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    NTE_ENV(col, lit);                                           // crown catches the moonlight, underside darker
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
    NTE_ENV(col, lit);
    // Glossy leather: sharp toon highlight from the moon + a camera-fixed key so it always reads as shiny,
    // a sky reflection on top-facing parts and a bright lacquer edge.
    float3 nv = NTE_ViewNormal(i.N);
    float expo = lerp(8.0, 96.0, 1.0 - i.specSize);
    float3 H = normalize(i.L + i.V);
    float specL = smoothstep(0.35, 0.45, pow(saturate(dot(i.N, H)), expo)) * lit;
    float3 hv = normalize(float3(-0.3, 0.6, 1.0));
    float specC = smoothstep(0.2, 0.5, pow(saturate(dot(nv, hv)), expo * 0.35));
    float skyRef = smoothstep(0.45, 0.85, nv.y) * 0.35;
    float edge = smoothstep(0.55, 0.85, NTE_Fresnel(i)) * 0.4 * lerp(0.4, 1.0, lit);
    col += i.specColor * (specL + specC * 0.7 + skyRef + edge) * i.specStrength;
    float gold = NTE_GoldMask(i.albedo);                         // buckles / studs
    [branch] if (gold > 0.001) col = lerp(col, NTE_MetalShade(i, i.albedo, lit), gold);   // only on buttons
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Leather)

// ---------------- Metal: whole material is metal ----------------
void NTE_Metal_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_MetalShade(i, i.albedo, lit);
    NTE_ENV(col, lit);
    Color = (col + NTE_Rim(i, lit)) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Metal)

// ---------------- Eye (transparent iris) ----------------
// Mask (35_EyeHighlightMask): R = highlight shapes, G = lower-iris glow, B = upper-iris eyelid shade.
void NTE_Eye_float(float4 Tex, float4 BaseColor, float4 Mask, float3 NormalWS, float Brightness,
    float HighlightBoost, float IrisSaturation, float HighlightStrength, float IrisGradient, out float3 Color)
{
    float3 c = Tex.rgb * BaseColor.rgb;
    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
    c = max(lerp(l.xxx, c, 1.0 + IrisSaturation), 0.0);                    // richer iris colour
    float mx = max(c.r, max(c.g, c.b));
    float painted = smoothstep(0.65, 0.9, mx * (1.0 - (mx - min(c.r, min(c.g, c.b)))));
    c *= lerp(1.0, 0.55, Mask.b * IrisGradient * 2.0);                       // eyelid shade at the top
    c += c * Mask.g * IrisGradient * 2.5 + Mask.g * IrisGradient * 0.15;     // glowing lower iris
    c += painted * HighlightBoost;                                           // painted highlights -> HDR
    c = lerp(c, float3(1.0, 1.0, 1.0) * (1.0 + HighlightStrength), saturate(Mask.r * 1.2) * step(0.001, HighlightStrength)); // baked highlights
    Color = c * Brightness;
}
void NTE_Eye_half(half4 Tex, half4 BaseColor, half4 Mask, half3 NormalWS, half Brightness, half HighlightBoost,
    half IrisSaturation, half HighlightStrength, half IrisGradient, out half3 Color)
{
    float3 c; NTE_Eye_float(Tex, BaseColor, Mask, NormalWS, Brightness, HighlightBoost, IrisSaturation, HighlightStrength, IrisGradient, c); Color = c;
}
#endif
