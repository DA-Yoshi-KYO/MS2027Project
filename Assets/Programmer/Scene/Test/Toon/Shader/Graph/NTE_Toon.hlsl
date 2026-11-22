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
    // 全体の彩度 (_NTESaturation, material property of every NTE graph; 1 = as painted)
    float albedoLum = dot(i.albedo, float3(0.2126, 0.7152, 0.0722));
    i.albedo = max(lerp(albedoLum.xxx, i.albedo, _NTESaturation), 0.0);
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

// 濃い影の色 (_DeepShadowColor / _DeepShadowStrength): the further the surface turns away from the
// light, the more the shade takes this colour (e.g. a red-brown deep shade on skin). Strength 0 = off.
float3 NTE_Diffuse(NTEToonInput i, float lit)
{
    float3 shade = NTE_ShadowColor(i);
    float deep = smoothstep(0.0, 0.8, -dot(i.N, i.L)) * saturate(_DeepShadowStrength);
    shade = lerp(shade, i.albedo * _DeepShadowColor.rgb, deep);
    return lerp(i.albedo, shade, (1.0 - lit) * i.shadowStrength);
}

// Without the deep shade colour (skin does not use it).
float3 NTE_DiffusePlain(NTEToonInput i, float lit, float strength)
{
    return lerp(i.albedo, NTE_ShadowColor(i), (1.0 - lit) * strength);
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
// Screen-space derivatives (fwidth/ddx/ddy) are not allowed in HDRP ray tracing (DXR) passes.
// There we return 0 so the pattern is simply drawn without distance fade.
float2 NTE_FWidth(float2 p)
{
#if defined(SHADER_STAGE_RAY_TRACING)
    return float2(0.0, 0.0);
#else
    return fwidth(p);
#endif
}
float NTE_Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
float NTE_Fabric(float2 uv, float scale)
{
    float2 p = uv * max(scale, 1.0);
    float2 fw = NTE_FWidth(p);
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
    float2 fw = NTE_FWidth(p);
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

// Direction of the main light. A script that chose a light (CS_ToonLightDirection) says so with
// _LightDirectionGiven and wins. Everything else (models from Maya, exported prefabs) follows the
// scene's directional light itself, the one that casts the shadows.
float3 NTE_LightDirection(float3 given)
{
#ifdef NTE_HDRP_LIGHTS
    if (_LightDirectionGiven < 0.5 && _DirectionalLightCount > 0)
        return -_DirectionalLightDatas[max(_DirectionalShadowIndex, 0)].forward;
#endif
    return given;
}

// ---------------- In the shade of something ----------------
// The cast shadow (Shadow Matte) must fade out where a surface turns sideways to the light (see NTE_CastFade):
// under a roof only the tops went dark and everything else stayed as bright as in the open.
// "Is this point in the shade" has nothing to do with the way it faces: from the point toward the light to
// just outside the character's box (CS_ToonShade), then the shadow map there. What is inside the box (the
// character's own head over its body) cannot shade all of it; a roof, a building, somebody else does.
// Without the direct light only the light of the surroundings is left, and it is the same for every part:
// all darken alike (NTE_SHADE_LEVEL), folds and pattern kept, the hue leans a little to the part's shade colour.
// (Each part's own cast-shadow colour at full area turned faces crimson and left clothes nearly bright.)
#if defined(SHADERPASS) && !defined(SHADERGRAPH_PREVIEW) && !defined(SHADER_STAGE_RAY_TRACING)
    #if defined(NTE_HDRP_LIGHTS) || (SHADERPASS == SHADERPASS_FORWARD)
        #define NTE_SHADE_LOOKUP 1
    #endif
#endif

#define NTE_SHADE_BOXES 16          // CS_ToonShade
#define NTE_SHADE_SOFT 0.04         // m, half the soft edge
#define NTE_SHADE_LEVEL 0.33        // what is left of the light in the shade
#define NTE_SHADE_FULL_AT 0.8       // _CelCastShadowStrength from which it is full: the shaders' default, so
                                    // skin (1) and clothes / hair (0.8) darken alike. Below it fades to none.
#define NTE_SHADE_HUE 0.3           // how far the shade leans to the part's shade colour
float4 _NTEShadeBoxMin[NTE_SHADE_BOXES];
float4 _NTEShadeBoxMax[NTE_SHADE_BOXES];
float _NTEShadeBoxCount;

float NTE_FindShade(float3 posAbsWS)
{
    float shade = 0.0;
#ifdef NTE_SHADE_LOOKUP
    int count = min((int)_NTEShadeBoxCount, NTE_SHADE_BOXES);
    if (count > 0 && _DirectionalLightCount > 0 && _DirectionalShadowIndex >= 0)
    {
        DirectionalLightData light = _DirectionalLightDatas[_DirectionalShadowIndex];
        float3 L = -light.forward;
        float3 across = float3(L.x < 0.0 ? min(L.x, -1e-5) : max(L.x, 1e-5),
                               L.y < 0.0 ? min(L.y, -1e-5) : max(L.y, 1e-5),
                               L.z < 0.0 ? min(L.z, -1e-5) : max(L.z, 1e-5));
        float3 p = posAbsWS;
        float found = 0.0;
        [loop] for (int k = 0; k < count; k++)
        {
            float3 low = _NTEShadeBoxMin[k].xyz, high = _NTEShadeBoxMax[k].xyz;
            if (all(p >= low) && all(p <= high))
            {
                float3 exits = max((low - p) / across, (high - p) / across);
                p += L * (min(exits.x, min(exits.y, exits.z)) + 0.01);
                found = 1.0;
            }
        }
        if (found > 0.0 && light.lightDimmer > 0.0 && light.shadowDimmer > 0.0)
        {
            float3 P = GetCameraRelativePositionWS(p);
            HDShadowContext context = InitShadowContext();
            // biased along L: the jump of the shadow lookup at N.L = 0 cannot happen here
    #if defined(DIRECTIONAL_SHADOW_HIGH)
            float lit = GetDirectionalShadowAttenuation(context, float2(0.0, 0.0), P, L, light.shadowIndex, L);     // soft by itself
    #else
            float3 a = abs(L.y) < 0.99 ? normalize(cross(L, float3(0.0, 1.0, 0.0))) : normalize(cross(L, float3(1.0, 0.0, 0.0)));
            float3 b = cross(L, a);
            a *= NTE_SHADE_SOFT;
            b *= NTE_SHADE_SOFT;
            float lit = GetDirectionalShadowAttenuation(context, float2(0.0, 0.0), P + a, L, light.shadowIndex, L);
            lit += GetDirectionalShadowAttenuation(context, float2(0.0, 0.0), P - a, L, light.shadowIndex, L);
            lit += GetDirectionalShadowAttenuation(context, float2(0.0, 0.0), P + b, L, light.shadowIndex, L);
            lit += GetDirectionalShadowAttenuation(context, float2(0.0, 0.0), P - b, L, light.shadowIndex, L);
            lit *= 0.25;
    #endif
            shade = (1.0 - saturate(lit)) * saturate(light.shadowDimmer);
        }
    }
#endif
    return shade;
}

// Looked up once per pixel: the colour and the cast shadow's tint both ask.
static float s_nteShade = -1.0;
float NTE_InShade(float3 posAbsWS)
{
    if (s_nteShade < 0.0)
        s_nteShade = NTE_FindShade(posAbsWS);
    return s_nteShade;
}

// 0 = in the open (then nothing below changes anything), 1 = fully in the shade at full strength.
float NTE_ShadeAmount(float3 posAbsWS)
{
    float amount = 0.0;
    if (_CelCastShadowStrength > 0.0)
        amount = NTE_InShade(posAbsWS) * saturate(_CelCastShadowStrength / NTE_SHADE_FULL_AT);
    return amount;
}

// No direct light: the light / dark band is weaker.
float NTE_ShadeBand(float lit, float shade) { return lerp(lit, 1.0, 0.5 * shade); }

// col = without rim, with the spot / point lights (they are not what is hidden: they keep their strength).
float3 NTE_Shaded(NTEToonInput i, float3 col, float3 punctual, float3 rim, float shade)
{
    float3 result = col + rim;
    if (shade > 0.0)
    {
        float3 tint = saturate(i.shadowColor);
        float tintLum = dot(tint, float3(0.2126, 0.7152, 0.0722));
        float3 hue = tintLum > 0.01 ? lerp(1.0, tint / tintLum, NTE_SHADE_HUE) : float3(1.0, 1.0, 1.0);
        float3 factor = lerp(1.0, NTE_SHADE_LEVEL * hue, shade);
        result = max(col - punctual + rim * (1.0 - shade), 0.0) * factor + punctual;
    }
    return result;
}

// Shadow maps see the real (faceted) mesh, the shading sees smooth normals: right at the light / shade edge the
// mesh shadows itself in polygon-shaped blocks. Faded only there (N.L -0.02 .. 0.10), so surfaces lit at a
// grazing angle (legs under an overhead light) keep the shadows cast on them (skirt -> thighs).
float NTE_SelfShadowFade(float ndl) { return smoothstep(-0.02, 0.10, ndl); }

#ifdef NTE_HDRP_LIGHTS
// Spot / point light shadow with a fixed, smooth 7x7 tent filter (16 taps). Same shadow map, position and bias
// as HDRP's own lookup, but no per-pixel noise (HDRP "High" uses noisy PCSS): the edge is a clean soft line
// instead of stair-steps and dots, whatever the quality setting. Cheaper than PCSS too.
float NTE_PunctualShadowTent(HDShadowContext shadowContext, float3 positionWS, float3 normalWS, int shadowIndex,
    float3 L, float L_dist, bool pointLight)
{
    normalWS *= FastSign(dot(normalWS, L));
    HDShadowData sd = shadowContext.shadowDatas[shadowIndex];
    if (pointLight)
    {
        const int face = CubeMapFaceID(-L);
        sd.rot0 = shadowContext.shadowDatas[shadowIndex + face].rot0;
        sd.rot1 = shadowContext.shadowDatas[shadowIndex + face].rot1;
        sd.rot2 = shadowContext.shadowDatas[shadowIndex + face].rot2;
        sd.atlasOffset = shadowContext.shadowDatas[shadowIndex + face].atlasOffset;
    }
    bool cached = sd.isInCachedAtlas > 0;
    float4 atlas = cached ? _CachedShadowAtlasSize : _ShadowAtlasSize;
    float3 normalBias = EvalShadow_NormalBiasProj(sd.worldTexelSize, sd.normalBias, normalWS, L_dist);
    float3 posTC;
    if (!EvalShadow_PositionTC(sd, atlas.zw, positionWS, normalBias, true, posTC))
        return 1.0;
    return cached
        ? SampleShadow_PCF_Tent_7x7(atlas.zwxy, posTC, _CachedShadowmapAtlas, s_linear_clamp_compare_sampler, FIXED_UNIFORM_BIAS)
        : SampleShadow_PCF_Tent_7x7(atlas.zwxy, posTC, _ShadowmapAtlas, s_linear_clamp_compare_sampler, FIXED_UNIFORM_BIAS);
}
#endif

// Sharpness (0..1): 0 = soft photographic edge, 1 = hard cel edge. The light term and the shadow-map term
// are each pushed through a step + feather (as in Unity Toon Shader's "Step / Feather"), after a small blur
// that removes shadow-map stair-steps - so the edge is crisp like anime, but never jagged.
// skin = 1: the shadow is looked up 2 cm+ off the skin, so the limb's own small bumps (knee cap over the
// shin, calf) never shadow it - only real overhangs do (skirt, sleeves, hair volume, chin over the neck).
float3 NTE_PunctualLightsCore(NTEToonInput i, float3 posAbsWS, float faceSign, float sharpness, float skin)
{
    float3 sum = 0.0;
    float feather = lerp(0.35, 0.03, saturate(sharpness));
#ifdef NTE_HDRP_LIGHTS
    float3 posRWS = GetCameraRelativePositionWS(posAbsWS);
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
            // Looked up a little off the surface (along the normal) so the faceted mesh does not shadow itself.
            float lift = skin > 0.5 ? 0.02 + 0.001 * distances.x : 0.003 + 0.0008 * distances.x;
            float3 p = posRWS + i.N * lift;
            float s = NTE_PunctualShadowTent(shadowContext, p, i.N, light.shadowIndex, L, distances.x,
                                             light.lightType == GPULIGHTTYPE_POINT);
            shadow = smoothstep(0.5 - feather - 0.1, 0.5 + feather + 0.1, s);
            // Where the surface turns away from the light the shading above already darkens it smoothly:
            // the shadow map is faded out there, so it cannot draw blotches along the light / shade edge.
            // (Skin needs no fade: the large offset already keeps the edge clean, and the shadow stays
            //  even on surfaces lit at a grazing angle.)
            shadow = lerp(1.0, shadow, light.shadowDimmer * (skin > 0.5 ? 1.0 : NTE_SelfShadowFade(ndl)));
        }
        sum += light.color * (att * diff * shadow * light.diffuseDimmer);
    }
    sum *= INV_PI * GetCurrentExposureMultiplier();                      // HDRP Lambert + camera exposure
#endif
    // Divided by the material brightness: final = base * Brightness + light (light keeps HDRP strength).
    return i.albedo * sum * lerp(0.15, 1.0, saturate(faceSign)) / max(i.brightness, 0.05);   // inside of skirts stays dark
}
float3 NTE_PunctualLights(NTEToonInput i, float3 posAbsWS, float faceSign, float sharpness)
{
    return NTE_PunctualLightsCore(i, posAbsWS, faceSign, sharpness, 0.0);
}

// Skin and the moon: how much of the moon-side shade a skin pixel gets, by N.L only.
// Legs, knees, lips etc. tilt only a little (N.L about -0.3 .. 0.25 under an overhead moon) and must not
// split into light / dark patches: that whole range gets the same light tone (0.15). The shade only grows
// where the skin clearly turns away (under the chin, N.L below -0.4), and the moon's cast shadow (Shadow
// Matte, graph node NTE_CastShadowTint) only lands where the skin clearly faces the moon (N.L above 0.3).
float NTE_SkinMoonMatte(float ndl) { return smoothstep(0.30, 0.60, ndl); }
float NTE_SkinMoonShade(float ndl) { return (1.0 - NTE_SkinMoonMatte(ndl)) * lerp(0.15, 1.0, smoothstep(-0.40, -0.80, ndl)); }

// ---------------- Moon shadow (HDRP Shadow Matte) without blocks at the light / shade edge ----------------
// HDRP darkens shadowed pixels towards the ShadowTint colour (graph node NTE_CastShadowTint). That node fades
// its strength out where the surface turns away from the moon (NTE_SelfShadowFade), and this function puts
// exactly that darkening back by N.L instead: same colour, same depth, but a smooth edge that follows the
// shape, not the polygons of the shadow map. Shadows cast onto lit surfaces (hair -> face) are unchanged.
// skin = 1: the skin shadow colour (warm), else the plain one; both match the ShadowTint node formulas.
float3 NTE_SmoothSelfShadow(float3 color, float3 albedo, float3 tint, float3 normalWS, float strength, float skin)
{
#ifdef NTE_HDRP_LIGHTS
    if (_DirectionalShadowIndex >= 0 && (asuint(_ShadowMatteFilter) & LIGHTFEATUREFLAGS_DIRECTIONAL) != 0)
    {
        DirectionalLightData light = _DirectionalLightDatas[_DirectionalShadowIndex];
        if (light.lightDimmer > 0.0 && light.shadowDimmer > 0.0)
        {
            float3 N = normalWS * rsqrt(max(dot(normalWS, normalWS), 1e-6));
            float ndl = dot(N, -light.forward);
            // Skin: one even tone over the small tilts (knees, calves), see NTE_SkinMoonShade.
            float away = skin > 0.5 ? NTE_SkinMoonShade(ndl) : 1.0 - NTE_SelfShadowFade(ndl);
            float3 a = max(albedo, 0.0);
            float3 shade = a * saturate(tint) * 0.5;
            if (skin > 0.5)
            {
                float3 warm = a * a / max(max(a.r, max(a.g, a.b)), 1e-4);
                shade = lerp(a, warm, 0.3) * float3(1.05, 0.90, 0.86) * saturate(tint) * 0.55;
            }
            color = lerp(color, shade, saturate(strength) * light.shadowDimmer * away);
        }
    }
#endif
    return color;
}

// View-space normal (for matcap-style reflections that follow the camera)
float3 NTE_ViewNormal(float3 N) { return normalize(mul((float3x3)UNITY_MATRIX_V, N)); }

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
    ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength, SpecSize); \
    i.L = NTE_LightDirection(i.L)
#define NTE_HALF_ARGS half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS, half3 MainLightDirection, \
    half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor, half RimStrength, half RimWidth, \
    half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength, half SpecSize, \
    half FaceSign, half4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale, half DetailStrength, \
    float3 PositionWS, half FabricSheen, half LightSharpness, out half3 Color
#define NTE_HALF_FORWARD(name) void name##_half(NTE_HALF_ARGS) { float3 c; name##_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, \
    MainLightDirection, ShadowThreshold, ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, \
    SpecColor, SpecStrength, SpecSize, FaceSign, UV, SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen, LightSharpness, c); Color = c; }
#define NTE_ENV(col, lit) col = NTE_Environment(i, col, lit, FaceSign, SkyLight, BottomShade, BackFaceShade) + NTE_PunctualLights(i, PositionWS, FaceSign, LightSharpness)

// ---------------- Cloth: soft band, fabric weave and sheen ----------------
void NTE_Cloth_float(NTE_ARGS)
{
    NTE_INPUT;
    float lit = NTE_LitBand(i);
    float3 col = NTE_Diffuse(i, lit);
    float weave = 0.0, grain = 0.0;
    [branch] if (DetailStrength > 0.001)                                  // material-uniform: free to skip
    {
        weave = NTE_Fabric(UV.xy, DetailScale) * DetailStrength;                     // close-up threads
        grain = NTE_FabricGrain(UV.xy, DetailScale * 0.1) * DetailStrength;           // visible at game distance
    }
    col *= 1.0 + (weave * 2.0 + grain * 1.6) * lerp(0.6, 1.0, lit);
    // Velvet / cloth profile: fibres catch light at grazing angles (bright fuzzy edge),
    // faces looking straight at the camera go slightly matte and dusty.
    float nv = saturate(dot(i.N, i.V));
    float sheen = pow(1.0 - nv, 2.0) * (1.0 + max(weave, 0.0) * 3.0);
    float3 sheenCol = lerp(i.albedo, 1.0.xxx, 0.35);
    col *= lerp(1.0, 0.9, FabricSheen * nv * nv);
    col += sheenCol * sheen * FabricSheen * 0.45 * lerp(0.35, 1.0, lit);
    NTE_ENV(col, lit);
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

// sdfOn / sdfLit: the face shadow map replaces the N.L band (and the top / bottom shading that follows
// the normals). Back faces are shaded like front faces: skin has no "inside of a skirt".
float3 NTE_SkinShade(NTEToonInput i, float3 posWS, float absorption, float scatter, float sharpness,
    float sky, float bottom, float sdfOn, float sdfLit, float sdfStrength)
{
    float absorb = saturate(absorption);
    // Light sinks into skin: wrap the lighting so the gradient is long and soft instead of a cel step.
    float ndl = dot(i.N, i.L);
    float wrap = saturate((ndl + 0.6) / 1.6);
    wrap = wrap * wrap * (3.0 - 2.0 * wrap);
    float lit = lerp(lerp(NTE_LitBand(i), wrap, absorb * 0.7), sdfLit, sdfOn);
    float shade = NTE_ShadeAmount(posWS);
    lit = NTE_ShadeBand(lit, shade);
    float3 col = NTE_DiffusePlain(i, lit, lerp(i.shadowStrength, saturate(sdfStrength), sdfOn));
    // Absorption: the lit side never goes chalky white, the deep side keeps a faint warm glow from inside.
    col *= lerp(1.0, lerp(0.93, 1.0, 1.0 - lit), absorb);
    col += i.albedo * float3(0.03, 0.006, 0.0) * (1.0 - lit) * absorb;
    // Spot / point lights on skin get a softer edge as well (same "light sinks in" idea), so they do not
    // draw hard lit / unlit patches on the bumps of knees and calves.
    float3 punctual = NTE_PunctualLightsCore(i, posWS, 1.0, sharpness * (1.0 - 0.7 * absorb), 1.0);
    col = NTE_Environment(i, col, lit, 1.0, sky * (1.0 - sdfOn) * (1.0 - shade), bottom * (1.0 - sdfOn), 0.0) + punctual;
    col = NTE_SkinScatter(i, col, scatter);
    // Very soft, velvety sheen (fine hair on the skin) instead of a plastic edge.
    col += i.albedo * pow(NTE_Fresnel(i), 4.0) * 0.08 * absorb * lit;
    return NTE_Shaded(i, col, punctual, NTE_Rim(i, lit), shade) * i.brightness;
}

void NTE_Skin_float(NTE_ARGS)
{
    NTE_INPUT;
    Color = NTE_SkinShade(i, PositionWS, DetailStrength, FabricSheen, LightSharpness, SkyLight, BottomShade, 0.0, 1.0, 0.0);
}
NTE_HALF_FORWARD(NTE_Skin)

// ---------------- Hair: soft band, top-lit gradient, highlight ----------------
// Back faces are shaded like front faces (hair cards are seen from both sides).
float3 NTE_HairShade(NTEToonInput i, float3 posWS, float sharpness, float sky, float bottom, float3 highlight)
{
    float shade = NTE_ShadeAmount(posWS);
    float lit = NTE_ShadeBand(NTE_LitBand(i), shade);
    float3 col = NTE_Diffuse(i, lit);
    float3 punctual = NTE_PunctualLights(i, posWS, 1.0, sharpness);
    col = NTE_Environment(i, col, lit, 1.0, sky * (1.0 - shade), bottom, 0.0)     // crown catches the moonlight, underside darker
        + punctual;
    highlight *= 1.0 - shade;
    // The article adds the highlight to the colour as it is shown (a gamma space project). Added to the linear
    // colour here it would come out twice as wide and as bright.
    if (any(highlight > 0.0))
    {
        float3 colour = max(col, 1e-6), addColour = max(highlight, 1e-6);
        col = pow(pow(colour, 1.0 / 2.2) + addColour, 2.2);
    }
    return NTE_Shaded(i, col, punctual, NTE_Rim(i, lit), shade) * i.brightness;
}

void NTE_Hair_float(NTE_ARGS)
{
    NTE_INPUT;
    Color = NTE_HairShade(i, PositionWS, LightSharpness, SkyLight, BottomShade, float3(0.0, 0.0, 0.0));
}
NTE_HALF_FORWARD(NTE_Hair)

// ---------------- Leather: dark base with a broad satin sheen ----------------
void NTE_Leather_float(NTE_ARGS)
{
    NTE_INPUT;
    float shade = NTE_ShadeAmount(PositionWS);
    float lit = NTE_ShadeBand(NTE_LitBand(i), shade);
    float3 col = NTE_Diffuse(i, lit);
    float3 punctual = NTE_PunctualLights(i, PositionWS, FaceSign, LightSharpness);
    col = NTE_Environment(i, col, lit, FaceSign, SkyLight * (1.0 - shade), BottomShade, BackFaceShade) + punctual;
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
    col += i.specColor * (specL + specC * 0.7 + skyRef + edge) * i.specStrength * (1.0 - shade);
    Color = NTE_Shaded(i, col, punctual, NTE_Rim(i, lit), shade) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Leather)

// ---------------- Metal: whole material is metal ----------------
void NTE_Metal_float(NTE_ARGS)
{
    NTE_INPUT;
    float shade = NTE_ShadeAmount(PositionWS);
    float lit = NTE_ShadeBand(NTE_LitBand(i), shade);
    i.specStrength *= 1.0 - shade;                      // nothing bright to mirror in the shade
    float3 col = NTE_MetalShade(i, i.albedo, lit);
    float3 punctual = NTE_PunctualLights(i, PositionWS, FaceSign, LightSharpness);
    col = NTE_Environment(i, col, lit, FaceSign, SkyLight * (1.0 - shade), BottomShade, BackFaceShade) + punctual;
    Color = NTE_Shaded(i, col, punctual, NTE_Rim(i, lit), shade) * i.brightness;
}
NTE_HALF_FORWARD(NTE_Metal)

// ---------------- Same functions + moon shadow strength (smooth light / shade edge) ----------------
#define NTE_ARGS_C float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS, float3 MainLightDirection, \
    float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor, float RimStrength, float RimWidth, \
    float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength, float SpecSize, \
    float FaceSign, float4 UV, float SkyLight, float BottomShade, float BackFaceShade, float DetailScale, float DetailStrength, \
    float3 PositionWS, float FabricSheen, float LightSharpness, float CastShadowStrength, out float3 Color
#define NTE_HALF_ARGS_C half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS, half3 MainLightDirection, \
    half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor, half RimStrength, half RimWidth, \
    half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength, half SpecSize, \
    half FaceSign, half4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale, half DetailStrength, \
    float3 PositionWS, half FabricSheen, half LightSharpness, half CastShadowStrength, out half3 Color
#define NTE_PASS_ARGS Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, ShadowThreshold, ShadowSoftness, \
    ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength, SpecSize, FaceSign, UV, \
    SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen, LightSharpness
#define NTE_SMOOTH_SHADOW(name, skin) \
void name##_float(NTE_ARGS_C) \
{ \
    float3 c; name##_float(NTE_PASS_ARGS, c); \
    Color = NTE_SmoothSelfShadow(c, Tex.rgb * BaseColor.rgb, ShadowColor, NormalWS, CastShadowStrength, skin); \
} \
void name##_half(NTE_HALF_ARGS_C) { float3 c; name##_float(NTE_PASS_ARGS, (float)CastShadowStrength, c); Color = c; }

NTE_SMOOTH_SHADOW(NTE_Skin, 1.0)
NTE_SMOOTH_SHADOW(NTE_Cloth, 0.0)
NTE_SMOOTH_SHADOW(NTE_Hair, 0.0)
NTE_SMOOTH_SHADOW(NTE_Leather, 0.0)
NTE_SMOOTH_SHADOW(NTE_Metal, 0.0)

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

// ---------------- Cast shadow of the moon (HDRP Shadow Matte) ----------------
// HDRP's shadow lookup turns its normal offset round where N.L changes sign (SHADOW_AUTO_FLIP_NORMAL):
// the looked-up position jumps there and the shadow gets a straight, hard cut along that line (with a
// light from straight above: a horizontal cut across every head, at its widest). The tint's strength
// falls to 0 exactly there, so the jump cannot show. The side turned away is shaded by the band (影の濃さ).
#define NTE_CAST_FADE 0.35      // N.L over which the cast shadow comes in (about 20 degrees)

float NTE_CastFade(float3 normalWS, float3 given)
{
    float3 L = given;
#ifdef NTE_HDRP_LIGHTS
    if (_DirectionalLightCount > 0)
        L = -_DirectionalLightDatas[max(_DirectionalShadowIndex, 0)].forward;     // the light that casts it
#endif
    float ndl = dot(normalWS * rsqrt(max(dot(normalWS, normalWS), 1e-6)), L * rsqrt(max(dot(L, L), 1e-6)));
    return smoothstep(0.0, NTE_CAST_FADE, ndl);
}

void NTE_CastShadow_float(float3 Albedo, float3 BaseColor, float3 Tint, float Strength, float Brightness, float3 NormalWS, float3 LightDir,
    float3 PositionWS, out float4 ShadowTint)
{
    float3 shade = max(Albedo * BaseColor.rgb, 0.0) * saturate(Tint.rgb) * 0.5;
    ShadowTint = float4(shade, saturate(Strength) * NTE_CastFade(NormalWS, LightDir) * (1.0 - NTE_InShade(PositionWS)));
}
void NTE_CastShadow_half(half3 Albedo, half3 BaseColor, half3 Tint, half Strength, half Brightness, half3 NormalWS, half3 LightDir,
    float3 PositionWS, out half4 ShadowTint)
{
    float4 tint;
    NTE_CastShadow_float(Albedo, BaseColor, Tint, Strength, Brightness, NormalWS, LightDir, PositionWS, tint);
    ShadowTint = tint;
}

// Skin: shadows falling on it turn red-orange (subsurface scattering), not grey.
void NTE_SkinCastShadow_float(float3 Albedo, float3 BaseColor, float3 Tint, float Strength, float Brightness, float3 NormalWS, float3 LightDir,
    float3 PositionWS, out float4 ShadowTint)
{
    float3 a = max(Albedo * BaseColor.rgb, 0.0);
    float3 warm = a * a / max(max(a.r, max(a.g, a.b)), 1e-4);
    float3 shade = lerp(a, warm, 0.3) * float3(1.05, 0.90, 0.86) * saturate(Tint.rgb) * 0.55;
    ShadowTint = float4(shade, saturate(Strength) * NTE_CastFade(NormalWS, LightDir) * (1.0 - NTE_InShade(PositionWS)));
}
void NTE_SkinCastShadow_half(half3 Albedo, half3 BaseColor, half3 Tint, half Strength, half Brightness, half3 NormalWS, half3 LightDir,
    float3 PositionWS, out half4 ShadowTint)
{
    float4 tint;
    NTE_SkinCastShadow_float(Albedo, BaseColor, Tint, Strength, Brightness, NormalWS, LightDir, PositionWS, tint);
    ShadowTint = tint;
}

// ---------------- Texture slots that may be empty ----------------
// An empty slot gets Unity's 4x4 default texture: anything larger is a map someone assigned.
float NTE_HasMap(UnityTexture2D map) { return step(4.5, max(map.texelSize.z, map.texelSize.w)); }

// Unit directions of the texture's x / y on the surface, from screen derivatives (works with mirrored UVs
// and does not need the mesh tangents). No derivatives in ray tracing passes: no bump there.
void NTE_UVFrame(float3 positionWS, float3 N, float2 uv, out float3 T, out float3 B)
{
    T = 0.0; B = 0.0;
#if !defined(SHADER_STAGE_RAY_TRACING)
    float3 dp1 = ddx(positionWS), dp2 = ddy(positionWS);
    float2 du1 = ddx(uv), du2 = ddy(uv);
    float3 p2 = cross(dp2, N), p1 = cross(N, dp1);
    float side = dot(dp1, p2) < 0.0 ? -1.0 : 1.0;
    T = (p2 * du1.x + p1 * du2.x) * side;
    B = (p2 * du1.y + p1 * du2.y) * side;
    T *= rsqrt(max(dot(T, T), 1e-20));
    B *= rsqrt(max(dot(B, B), 1e-20));
#endif
}

// ---------------- Face: SDF shadow map ----------------
// The map is painted for a light coming from the side where U grows (right of the picture); for the other
// side it is read mirrored. Brighter = stays lit longer while the light goes from the front to the back.
// FaceFront / FaceRight: object space, by convention (a model from Maya looks to +Z there, an imported
// model to +Z here); FaceAngle turns both for a model that looks somewhere else.
#define NTE_PART_FACE 0.0     // _SkinPart: one shader, three kinds of part
#define NTE_PART_BODY 1.0
#define NTE_PART_NOSE 2.0

float3 NTE_TurnY(float3 v, float degrees_)
{
    float s, c;
    sincos(radians(degrees_), s, c);
    return float3(v.x * c + v.z * s, v.y, v.z * c - v.x * s);
}

float NTE_FaceSDF(UnityTexture2D map, float2 uv, float3 front, float3 right, float3 L, float softness, float flip)
{
    float3 F = TransformObjectToWorldDir(front);
    float3 R = TransformObjectToWorldDir(right);
    float2 flatL = float2(dot(R, L), dot(F, L));    // the light seen from above the head
    flatL = dot(flatL, flatL) > 1e-5 ? normalize(flatL) : float2(0.0, 1.0);
    bool mirrored = (flatL.x < 0.0) != (flip > 0.5);
    float value = SAMPLE_TEXTURE2D_LOD(map.tex, map.samplerstate, float2(mirrored ? 1.0 - uv.x : uv.x, uv.y), 0).r;
    float soft = max(softness, 0.002);
    float away = (1.0 - flatL.y) * 0.5 * (1.0 + 2.0 * soft) - soft;     // 0 = light in front, 1 = behind
    return smoothstep(-soft, soft, value - away);
}

void NTE_FaceSkin_float(float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS,
    float3 MainLightDirection, float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor,
    float RimStrength, float RimWidth, float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength,
    float SpecSize, float FaceSign, float4 UV, float SkyLight, float BottomShade, float BackFaceShade, float DetailScale,
    float DetailStrength, float3 PositionWS, float FabricSheen, float LightSharpness,
    UnityTexture2D SDFMap, float SDFStrength, float SDFSoftness, float SDFFlip, float4 FaceFront, float4 FaceRight, float FaceAngle, float Part,
    out float3 Color)
{
    NTE_INPUT;
    if (Part > NTE_PART_NOSE - 0.5)
    {
        // the nose is a painted mark: no band, no rim. In the shade of something it darkens with the face
        Color = NTE_Shaded(i, i.albedo, 0.0, 0.0, NTE_ShadeAmount(PositionWS)) * i.brightness;
        return;
    }
    float on = NTE_HasMap(SDFMap) * step(0.001, SDFStrength) * step(Part, NTE_PART_BODY - 0.5);
    float lit = 1.0;
    if (on > 0.5)
        lit = NTE_FaceSDF(SDFMap, UV.xy, NTE_TurnY(FaceFront.xyz, FaceAngle), NTE_TurnY(FaceRight.xyz, FaceAngle), i.L, SDFSoftness, SDFFlip);
    Color = NTE_SkinShade(i, PositionWS, DetailStrength, FabricSheen, LightSharpness, SkyLight, BottomShade, on, lit, SDFStrength);
}
void NTE_FaceSkin_half(half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS,
    half3 MainLightDirection, half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor,
    half RimStrength, half RimWidth, half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength,
    half SpecSize, half FaceSign, half4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale,
    half DetailStrength, float3 PositionWS, half FabricSheen, half LightSharpness,
    UnityTexture2D SDFMap, half SDFStrength, half SDFSoftness, half SDFFlip, half4 FaceFront, half4 FaceRight, half FaceAngle, half Part,
    out half3 Color)
{
    float3 c;
    NTE_FaceSkin_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, ShadowThreshold,
        ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength,
        SpecSize, FaceSign, UV, SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen,
        LightSharpness, SDFMap, SDFStrength, SDFSoftness, SDFFlip, FaceFront, FaceRight, FaceAngle, Part, c);
    Color = c;
}

// ---------------- Skin part: nose ----------------
// The nose is a part of its own (its own material, Part = nose). Seen from the front it stays; as the view
// turns to the profile the whole part sinks into the face and disappears (vertex stage: clean silhouette).
// NoseSize = size of the part in object units (set from the mesh), Depth = how many times that it sinks.
void NTE_NoseSink_float(float3 PositionOS, float4 FaceFront, float FaceAngle, float Angle, float Depth, float NoseSize,
    float Part, out float3 Out)
{
    Out = PositionOS;
#ifndef SHADERGRAPH_PREVIEW
    if (Part > NTE_PART_NOSE - 0.5 && Depth > 0.0)
    {
        float3 up = float3(0.0, 1.0, 0.0);
        float3 F = NTE_TurnY(normalize(FaceFront.xyz), FaceAngle);
        // The main camera is the origin of HDRP's camera-relative world. Only the turn around the head counts.
        float3 V = TransformWorldToObjectDir(-TransformObjectToWorld(PositionOS));
        V -= up * dot(V, up);
        float facing = dot(V * rsqrt(max(dot(V, V), 1e-8)), F);
        float shown = smoothstep(cos(radians(min(Angle + 10.0, 180.0))), cos(radians(max(Angle - 10.0, 0.0))), facing);
        Out -= F * (NoseSize * Depth * (1.0 - shown));
    }
#endif
}
void NTE_NoseSink_half(float3 PositionOS, float4 FaceFront, half FaceAngle, half Angle, half Depth, float NoseSize,
    half Part, out float3 Out)
{
    NTE_NoseSink_float(PositionOS, FaceFront, FaceAngle, Angle, Depth, NoseSize, Part, Out);
}

// ---------------- Hair: highlight ----------------
// The procedural anisotropic highlight of https://light11.hatenadiary.com/entry/2021/02/25/202403 :
// the binormal (not the normal) against the half vector, shifted along the normal by a jitter value.
// One band, nothing else.
//   _HairHighlightFlow 1 = the binormal and the jitter follow the UVs, as in the article (UVs laid along the strands).
//                      0 = they run from the crown down (object up on the surface). On a sphere that is the same
//                          as the article; it does not break on models whose hair UVs are cut up or turned.
float NTE_StreakNoise(float2 p, float period)
{
    float2 c = floor(p), f = p - c;
    f = f * f * (3.0 - 2.0 * f);
    float a = c.x - period * floor(c.x / period);
    float b = a + 1.0 < period ? a + 1.0 : 0.0;
    return lerp(lerp(NTE_Hash(float2(a, c.y)), NTE_Hash(float2(b, c.y)), f.x),
                lerp(NTE_Hash(float2(a, c.y + 1.0)), NTE_Hash(float2(b, c.y + 1.0)), f.x), f.y);
}

// The jitter picture of the article, made here: grey streaks along the strands. at.x closes after 1.
float NTE_HairStreaks(float2 at, float count, float pixel)
{
    float n = max(floor(count + 0.5), 2.0);
    float2 p = at * float2(n, n * 0.0625);
    float v = NTE_StreakNoise(p, n) * 0.5 + NTE_StreakNoise(p * float2(2.0, 1.5) + float2(0.0, 7.0), n * 2.0) * 0.5;
    v = saturate((v - 0.5) * 1.1 + 0.58);        // light grey streaks, like the picture of the article
    return lerp(v, 0.58, saturate(pixel * n - 0.5));     // streaks under a pixel: their average, no shimmer
}

float NTE_HairHighlightShape(float3 N, float3 binormal, float3 V, float3 L, float position, float sharpness,
    float jitter, float jitterStrength)
{
    float3 sum = L + V;
    float3 halfDir = sum * rsqrt(max(dot(sum, sum), 1e-6));
    float3 b = binormal - N * (position + (jitter * 0.5 - 0.5) * jitterStrength);
    b *= rsqrt(max(dot(b, b), 1e-6));
    float bdoth = dot(b, halfDir) * 0.5 + 0.5;
    float highlight = saturate(bdoth * (1.0 - bdoth) * 4.0);
    return pow(max(highlight, 1e-5), max(sharpness, 1.0));
}

float NTE_HairHighlight(NTEToonInput i, float3 bitangentWS, float2 uv, UnityTexture2D jitterMap,
    float position, float sharpness, float jitterStrength, float streaks, float flow)
{
    float3 binormal;
    float2 at;
    float pixel;
    float fade = 1.0;
    if (flow >= 0.5)
    {
        binormal = bitangentWS * rsqrt(max(dot(bitangentWS, bitangentWS), 1e-6));
        at = uv;
        pixel = NTE_FWidth(uv).x;
    }
    else
    {
        float3 up = normalize(TransformObjectToWorldDir(float3(0.0, 1.0, 0.0)));
        float3 side = TransformObjectToWorldDir(float3(1.0, 0.0, 0.0));
        side -= up * dot(side, up);
        side *= rsqrt(max(dot(side, side), 1e-6));
        float high = clamp(dot(i.N, up), -1.0, 1.0);
        binormal = up - i.N * high;
        binormal *= rsqrt(max(dot(binormal, binormal), 1e-6));
        float2 around = float2(dot(i.N, side), dot(i.N, cross(side, up)));
        around *= rsqrt(max(dot(around, around), 1e-6));
        at = float2(atan2(around.x, around.y + 1e-6) / 6.2831853 + 0.5, asin(high) / 3.1415927 + 0.5);
        pixel = length(NTE_FWidth(around)) / 6.2831853;     // of the direction: no jump where the turn closes
        fade = 1.0 - smoothstep(0.990, 0.999, abs(high));   // the strands meet at the crown
    }
    float hasMap = NTE_HasMap(jitterMap);
    float painted = SAMPLE_TEXTURE2D_LOD(jitterMap.tex, jitterMap.samplerstate, at, 0).r;
    float jitter = lerp(NTE_HairStreaks(at, streaks, pixel), painted, hasMap);
    return NTE_HairHighlightShape(i.N, binormal, i.V, i.L, position, sharpness, jitter, jitterStrength) * fade;
}

// ---------------- Cloth: threads, fuzz, sheen, stitches ----------------
// Built like HDRP's Cotton/Wool and Silk shaders (https://www.ame-name.com/archives/13793), shaded as toon:
//   thread map = R occlusion, G & A normal (x = A, y = G), B smoothness
//   fuzz map   = one channel, added to the colour
// Sizes are real lengths, so one setting looks the same on every model: CS_MayaPreviewMeshData writes the
// metres in one UV unit of each island into the second UV set (x = 4 + along U, y = 4 + along V).
// Without it one UV unit counts as one metre.
// The maps are Unity's fabric sample maps (Texture/Fabric/LICENSE.md).
#define NTE_CLOTH_UV_OFFSET 4.0

#if defined(SHADER_STAGE_RAY_TRACING)
    #define NTE_DDX(v) 0.0
    #define NTE_DDY(v) 0.0
#else
    #define NTE_DDX(v) ddx(v)
    #define NTE_DDY(v) ddy(v)
#endif

float2 NTE_ClothMetres(float2 uv, float4 uv1)
{
    float2 size = uv1.x >= NTE_CLOTH_UV_OFFSET - 0.01 && uv1.y >= NTE_CLOTH_UV_OFFSET - 0.01
        ? uv1.xy - NTE_CLOTH_UV_OFFSET : float2(1.0, 1.0);
    return uv * size;
}

float2 NTE_Turn(float2 p, float degrees_)
{
    float s, c;
    sincos(radians(degrees_), s, c);
    return float2(c * p.x + s * p.y, c * p.y - s * p.x);
}

// x = how much of the pixel is thread, y = thread shading, z = the dent the thread pulls into the cloth.
float3 NTE_Stitch(float2 m, float spacing, float offset, float stitchLength, float width, float twin, float pixel)
{
    float across = m.x - offset;
    across -= spacing * floor(across / spacing + 0.5);
    if (twin >= 0.5) across = abs(across) - width * 1.8;
    float period = stitchLength * 1.6;
    float along = m.y - period * floor(m.y / period + 0.5);
    float d = length(float2(across, max(abs(along) - max(stitchLength - width, 0.0) * 0.5, 0.0)));
    float radius = width * 0.5;
    float aa = max(pixel, 1e-6);
    float thin = saturate(width / aa);                      // thinner than a pixel: fainter, no shimmer
    float cover = saturate((radius - d) / aa + 0.5) * thin;
    float r = saturate(d / radius);
    float twist = sin((along + across) / width * 6.2831853) * saturate(width / (aa * 4.0));
    float dent = saturate(1.0 - (d - radius) / (radius + aa)) * thin;
    return float3(cover, 1.0 - 0.35 * r * r + 0.1 * twist, dent * (1.0 - cover));
}

#define NTE_SEAM_REACH 0.025      // CS_MayaPreviewClothSeams
#define NTE_SEAM_WRAP 20.0

// The seam map at uv: x = distance from the edge of the cloth piece, y = length along it (metres), z = 1 near one.
float3 NTE_SeamAt(UnityTexture2D map, float2 uv, float2 size)
{
    uint width, height;
    map.tex.GetDimensions(width, height);
    float2 res = float2(width, height);
    float2 p = frac(uv) * res - 0.5;
    float2 corner = floor(p), f = p - corner;
    float distance_ = 0.0, best = -1.0;
    float2 bestTexel = corner;
    float4 bestValue = float4(1.0, 0.0, 0.0, 0.0);
    [unroll] for (int k = 0; k < 4; k++)
    {
        float2 o = float2(k == 1 || k == 3 ? 1.0 : 0.0, k >= 2 ? 1.0 : 0.0);
        float2 texel = corner + o;
        float2 wrapped = texel - res * floor(texel / res);
        float4 value = map.tex.Load(int3((int2)wrapped, 0));
        float weight = (o.x > 0.5 ? f.x : 1.0 - f.x) * (o.y > 0.5 ? f.y : 1.0 - f.y);
        distance_ += value.r * weight;
        if (value.r < 0.999 && weight > best)
        {
            best = weight;
            bestTexel = texel;
            bestValue = value;
        }
    }
    float s, c;
    sincos(bestValue.a * 6.2831853, s, c);
    float stored = (floor(bestValue.g * 255.0 + 0.5) * 256.0 + floor(bestValue.b * 255.0 + 0.5)) / 65535.0;
    // exact between the texels: along the edge from the nearest one
    float along = stored * NTE_SEAM_WRAP + dot((p - bestTexel) / res * size, float2(c, s));
    return float3(distance_ * NTE_SEAM_REACH, along, best >= 0.0 && distance_ < 0.96 ? 1.0 : 0.0);
}

void NTE_ClothFabric_float(float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS,
    float3 MainLightDirection, float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor,
    float RimStrength, float RimWidth, float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength,
    float SpecSize, float FaceSign, float4 UV, float SkyLight, float BottomShade, float BackFaceShade, float DetailScale,
    float DetailStrength, float3 PositionWS, float FabricSheen, float LightSharpness,
    float3 BitangentWS, float4 UV1,
    float Weave, float ThreadSize, float ThreadAspect, float ThreadAngle, float ThreadBump, float ThreadShade,
    UnityTexture2D ThreadMap,
    float FuzzStrength, float FuzzSize, float4 FuzzColor, float Mottle, UnityTexture2D FuzzMap,
    float Smoothness, float ThreadSmoothness, float GlossStrength, float4 GlossColor, float Anisotropy,
    float SheenStrength, float4 SheenColor,
    float StitchStrength, float4 StitchColor, float StitchSpacing, float StitchOffset, float StitchLength,
    float StitchWidth, float StitchAngle, float StitchTwin, float StitchGrid, UnityTexture2D StitchMask,
    UnityTexture2D JerseyTex, UnityTexture2D TwillTex, UnityTexture2D PlainTex, UnityTexture2D JacketTex,
    UnityTexture2D RibTex, UnityTexture2D FuzzTex, UnityTexture2D MottleTex,
    float StitchEdge, float StitchInset, UnityTexture2D SeamMap,
    out float3 Color)
{
    NTE_INPUT;
    float shade = NTE_ShadeAmount(PositionWS);
    float lit = NTE_ShadeBand(NTE_LitBand(i), shade);
    float3 B = BitangentWS * rsqrt(max(dot(BitangentWS, BitangentWS), 1e-6));
    float3 sum = i.L + i.V;
    float3 H = sum * rsqrt(max(dot(sum, sum), 1e-6));

    // one sampler for every fabric map (HDRP has few to spare)
    SamplerState repeatSampler = JerseyTex.samplerstate;
    float2 metres = NTE_ClothMetres(UV.xy, UV1);
    float2 p = NTE_Turn(metres, ThreadAngle);
    float2 dpx = NTE_DDX(p), dpy = NTE_DDY(p);
    float2 uvDx = NTE_DDX(UV.xy), uvDy = NTE_DDY(UV.xy);

    float4 thread = float4(1.0, 0.5, 0.5, 0.5), average = thread;
    int kind = (int)floor(Weave + 0.5);
    if (kind >= 1)
    {
        // threads across one picture of each map
        float threads = kind == 1 ? 14.0 : kind == 2 ? 20.0 : kind == 3 ? 12.0 : 16.0;
        float2 tile = max(ThreadSize, 0.05) * 0.001 * threads * float2(1.0, max(ThreadAspect, 0.05));
        float2 at = p / tile, ax = dpx / tile, ay = dpy / tile;
        if (NTE_HasMap(ThreadMap) > 0.5)
        {
            thread = SAMPLE_TEXTURE2D_GRAD(ThreadMap.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(ThreadMap.tex, repeatSampler, at, 16);
        }
        else if (kind == 1)
        {
            thread = SAMPLE_TEXTURE2D_GRAD(JerseyTex.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(JerseyTex.tex, repeatSampler, at, 16);
        }
        else if (kind == 2)
        {
            thread = SAMPLE_TEXTURE2D_GRAD(TwillTex.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(TwillTex.tex, repeatSampler, at, 16);
        }
        else if (kind == 3)
        {
            thread = SAMPLE_TEXTURE2D_GRAD(PlainTex.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(PlainTex.tex, repeatSampler, at, 16);
        }
        else if (kind == 4)
        {
            thread = SAMPLE_TEXTURE2D_GRAD(JacketTex.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(JacketTex.tex, repeatSampler, at, 16);
        }
        else
        {
            thread = SAMPLE_TEXTURE2D_GRAD(RibTex.tex, repeatSampler, at, ax, ay);
            average = SAMPLE_TEXTURE2D_LOD(RibTex.tex, repeatSampler, at, 16);
        }
    }

    // around their own average, so the cloth keeps its colour and brightness at every distance
    float2 bump = NTE_Turn(float2(thread.a - average.a, thread.g - average.g) * 2.0, -ThreadAngle) * ThreadBump;
    float3 bumped = i.N + i.T * bump.x + B * bump.y;
    bumped *= rsqrt(max(dot(bumped, bumped), 1e-6));

    float3 col = NTE_Diffuse(i, lit);
    // The threads are lit from a little above and to the side of the light: straight from the front a bump
    // changes nothing. Done on the colour as it is shown, with a small lift: otherwise navy / black cloth
    // swallows the weave while white cloth shows it.
    float3 key = H - UNITY_MATRIX_V[0].xyz * 0.6 + UNITY_MATRIX_V[1].xyz * 0.6;
    key *= rsqrt(max(dot(key, key), 1e-6));
    float relief = (dot(bumped, key) - dot(i.N, key)) * 0.7 + (thread.r - average.r) * ThreadShade;
    if (abs(relief) > 0.0)
    {
        float3 shownColor = pow(max(col, 1e-6), 1.0 / 2.2);
        col = pow(max(shownColor * (1.0 + relief * 0.5) + relief * 0.11, 1e-6), 2.2);
    }

    if (Mottle > 0.0)
    {
        float size = max(FuzzSize, 0.1) * 0.01 * 1.5;
        float v = SAMPLE_TEXTURE2D_GRAD(MottleTex.tex, repeatSampler, p / size, dpx / size, dpy / size).r;
        float mean = SAMPLE_TEXTURE2D_LOD(MottleTex.tex, repeatSampler, p / size, 16).r;
        col *= max(1.0 + (v - mean) * Mottle, 0.0);
    }
    if (FuzzStrength > 0.0)
    {
        float size = max(FuzzSize, 0.1) * 0.01;
        float fuzz;
        if (NTE_HasMap(FuzzMap) > 0.5) fuzz = SAMPLE_TEXTURE2D_GRAD(FuzzMap.tex, repeatSampler, p / size, dpx / size, dpy / size).r;
        else fuzz = SAMPLE_TEXTURE2D_GRAD(FuzzTex.tex, repeatSampler, p / size, dpx / size, dpy / size).r;
        col += lerp(i.albedo, 1.0, 0.5) * FuzzColor.rgb * fuzz * FuzzStrength * lerp(0.5, 1.0, lit);
    }

    float nv = saturate(abs(dot(i.N, i.V)));
    if (GlossStrength > 0.0)
    {
        // GGX's shape with a peak of 1, stretched along / across the threads for silk
        float smoothHere = saturate(Smoothness + (thread.b - average.b) * 2.0 * ThreadSmoothness);
        float rough = max((1.0 - smoothHere) * (1.0 - smoothHere), 0.03);
        float at_ = max(rough * (1.0 + Anisotropy), 0.01), ab = max(rough * (1.0 - Anisotropy), 0.01);
        float2 turnedT = NTE_Turn(float2(1.0, 0.0), -ThreadAngle);
        float3 T2 = i.T * turnedT.x + B * turnedT.y;
        float3 B2 = B * turnedT.x - i.T * turnedT.y;
        float th = dot(T2, H) / at_, bh = dot(B2, H) / ab, nh = dot(bumped, H);
        float d = max(th * th + bh * bh + nh * nh, 1e-4);
        float gloss = saturate(1.0 / (d * d)) * step(0.0, nh);
        col += GlossColor.rgb * lerp(i.albedo, 1.0, 0.5) * gloss * GlossStrength * lerp(0.2, 1.0, lit);
    }
    if (SheenStrength > 0.0)
    {
        // the fibres catch the light at grazing angles, the middle goes a little dusty
        float sheen = pow(max(1.0 - nv, 1e-4), 2.5) * max(1.0 + (thread.r - average.r) * ThreadShade, 0.0);
        col *= 1.0 - 0.12 * saturate(SheenStrength) * nv * nv;
        col += SheenColor.rgb * lerp(i.albedo, 1.0, 0.35) * sheen * SheenStrength * 0.6 * lerp(0.35, 1.0, lit);
    }

    if (StitchStrength > 0.0)
    {
        float2 m = NTE_Turn(metres, StitchAngle);
        float pixel = max(max(abs(NTE_DDX(m.x)) + abs(NTE_DDY(m.x)), abs(NTE_DDX(m.y)) + abs(NTE_DDY(m.y))), 1e-6);
        float spacing = max(StitchSpacing, 0.2) * 0.01, offset = StitchOffset * 0.01;
        float stitchLength = max(StitchLength, 0.2) * 0.001, width = max(StitchWidth, 0.05) * 0.001;
        float3 stitch = 0.0;
        if (StitchEdge >= 0.5)
        {
            // along the edge of the piece of cloth
            if (NTE_HasMap(SeamMap) > 0.5)
            {
                float2 islandSize = UV1.x >= NTE_CLOTH_UV_OFFSET - 0.01 ? UV1.xy - NTE_CLOTH_UV_OFFSET : float2(1.0, 1.0);
                float3 seam = NTE_SeamAt(SeamMap, UV.xy, islandSize);
                float inset = max(StitchInset, 0.0) * 0.001 + width * (StitchTwin >= 0.5 ? 2.3 : 0.5);
                stitch = NTE_Stitch(seam.xy, 1000.0, inset, stitchLength, width, StitchTwin, pixel);
                stitch.xz *= seam.z;
            }
        }
        else
        {
            stitch = NTE_Stitch(m, spacing, offset, stitchLength, width, StitchTwin, pixel);
            if (StitchGrid >= 0.5)
            {
                float3 crossing = NTE_Stitch(m.yx, spacing, offset, stitchLength, width, StitchTwin, pixel);
                if (crossing.x > stitch.x) stitch.xy = crossing.xy;
                stitch.z = max(stitch.z, crossing.z) * (1.0 - stitch.x);
            }
        }
        // white = stitched here (an empty slot = everywhere)
        float mask = 1.0;
        if (NTE_HasMap(StitchMask) > 0.5) mask = SAMPLE_TEXTURE2D_GRAD(StitchMask.tex, repeatSampler, UV.xy, uvDx, uvDy).r;
        float amount = saturate(StitchStrength) * mask;
        col *= 1.0 - 0.3 * stitch.z * amount;
        float3 sewn = StitchColor.rgb * stitch.y * lerp(1.0, saturate(i.shadowColor), (1.0 - lit) * i.shadowStrength);
        col = lerp(col, sewn, stitch.x * amount);
    }

    float3 punctual = NTE_PunctualLights(i, PositionWS, FaceSign, LightSharpness);
    col = NTE_Environment(i, col, lit, FaceSign, SkyLight * (1.0 - shade), BottomShade, BackFaceShade) + punctual;
    Color = NTE_Shaded(i, col, punctual, NTE_Rim(i, lit), shade) * i.brightness;
}
void NTE_ClothFabric_half(half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS,
    half3 MainLightDirection, half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor,
    half RimStrength, half RimWidth, half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength,
    half SpecSize, half FaceSign, float4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale,
    half DetailStrength, float3 PositionWS, half FabricSheen, half LightSharpness,
    half3 BitangentWS, float4 UV1,
    half Weave, half ThreadSize, half ThreadAspect, half ThreadAngle, half ThreadBump, half ThreadShade,
    UnityTexture2D ThreadMap,
    half FuzzStrength, half FuzzSize, half4 FuzzColor, half Mottle, UnityTexture2D FuzzMap,
    half Smoothness, half ThreadSmoothness, half GlossStrength, half4 GlossColor, half Anisotropy,
    half SheenStrength, half4 SheenColor,
    half StitchStrength, half4 StitchColor, half StitchSpacing, half StitchOffset, half StitchLength,
    half StitchWidth, half StitchAngle, half StitchTwin, half StitchGrid, UnityTexture2D StitchMask,
    UnityTexture2D JerseyTex, UnityTexture2D TwillTex, UnityTexture2D PlainTex, UnityTexture2D JacketTex,
    UnityTexture2D RibTex, UnityTexture2D FuzzTex, UnityTexture2D MottleTex,
    half StitchEdge, half StitchInset, UnityTexture2D SeamMap,
    out half3 Color)
{
    float3 c;
    NTE_ClothFabric_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, ShadowThreshold,
        ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength,
        SpecSize, FaceSign, UV, SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen,
        LightSharpness, BitangentWS, UV1, Weave, ThreadSize, ThreadAspect, ThreadAngle, ThreadBump, ThreadShade, ThreadMap,
        FuzzStrength, FuzzSize, FuzzColor, Mottle, FuzzMap, Smoothness, ThreadSmoothness, GlossStrength, GlossColor, Anisotropy,
        SheenStrength, SheenColor, StitchStrength, StitchColor, StitchSpacing, StitchOffset, StitchLength, StitchWidth,
        StitchAngle, StitchTwin, StitchGrid, StitchMask, JerseyTex, TwillTex, PlainTex, JacketTex, RibTex, FuzzTex, MottleTex,
        StitchEdge, StitchInset, SeamMap, c);
    Color = c;
}

// ---------------- Hair: height map ----------------
// White = raised, black = sunk. It bends the normal, so the shade and the highlight follow the strands.
void NTE_HairMapped_float(float4 Tex, float4 BaseColor, float3 ShadowColor, float3 NormalWS, float3 ViewWS, float3 TangentWS,
    float3 MainLightDirection, float ShadowThreshold, float ShadowSoftness, float ShadowSaturation, float3 RimColor,
    float RimStrength, float RimWidth, float ShadowStrength, float Brightness, float3 SpecColor, float SpecStrength,
    float SpecSize, float FaceSign, float4 UV, float SkyLight, float BottomShade, float BackFaceShade, float DetailScale,
    float DetailStrength, float3 PositionWS, float FabricSheen, float LightSharpness,
    UnityTexture2D HeightMap, float HeightStrength,
    float3 BitangentWS, UnityTexture2D JitterMap, float4 HighlightColor, float HighlightStrength, float HighlightPosition,
    float HighlightSharpness, float JitterStrength, float JitterStreaks, float HighlightFlow,
    out float3 Color)
{
    NTE_INPUT;
    if (NTE_HasMap(HeightMap) * step(0.001, HeightStrength) > 0.5)
    {
        float3 T, B;
        NTE_UVFrame(GetCameraRelativePositionWS(PositionWS), i.N, UV.xy, T, B);
        float2 d = max(HeightMap.texelSize.xy, 1.0 / 1024.0);       // the same look for a 1K and a 4K map
        float hx = SAMPLE_TEXTURE2D_LOD(HeightMap.tex, HeightMap.samplerstate, UV.xy + float2(d.x, 0.0), 0).r
                 - SAMPLE_TEXTURE2D_LOD(HeightMap.tex, HeightMap.samplerstate, UV.xy - float2(d.x, 0.0), 0).r;
        float hy = SAMPLE_TEXTURE2D_LOD(HeightMap.tex, HeightMap.samplerstate, UV.xy + float2(0.0, d.y), 0).r
                 - SAMPLE_TEXTURE2D_LOD(HeightMap.tex, HeightMap.samplerstate, UV.xy - float2(0.0, d.y), 0).r;
        float2 slope = float2(hx / d.x, hy / d.y) * (0.5 / 64.0) * HeightStrength;
        i.N = normalize(i.N - T * slope.x - B * slope.y);
    }
    float3 highlight = float3(0.0, 0.0, 0.0);
    if (HighlightStrength > 0.0)
        highlight = pow(max(HighlightColor.rgb, 1e-6), 1.0 / 2.2) * (HighlightStrength * NTE_HairHighlight(i, BitangentWS, UV.xy, JitterMap,
            HighlightPosition, HighlightSharpness, JitterStrength, JitterStreaks, HighlightFlow));     // follows the height map
    Color = NTE_HairShade(i, PositionWS, LightSharpness, SkyLight, BottomShade, highlight);
}
void NTE_HairMapped_half(half4 Tex, half4 BaseColor, half3 ShadowColor, half3 NormalWS, half3 ViewWS, half3 TangentWS,
    half3 MainLightDirection, half ShadowThreshold, half ShadowSoftness, half ShadowSaturation, half3 RimColor,
    half RimStrength, half RimWidth, half ShadowStrength, half Brightness, half3 SpecColor, half SpecStrength,
    half SpecSize, half FaceSign, half4 UV, half SkyLight, half BottomShade, half BackFaceShade, half DetailScale,
    half DetailStrength, float3 PositionWS, half FabricSheen, half LightSharpness,
    UnityTexture2D HeightMap, half HeightStrength,
    half3 BitangentWS, UnityTexture2D JitterMap, half4 HighlightColor, half HighlightStrength, half HighlightPosition,
    half HighlightSharpness, half JitterStrength, half JitterStreaks, half HighlightFlow,
    out half3 Color)
{
    float3 c;
    NTE_HairMapped_float(Tex, BaseColor, ShadowColor, NormalWS, ViewWS, TangentWS, MainLightDirection, ShadowThreshold,
        ShadowSoftness, ShadowSaturation, RimColor, RimStrength, RimWidth, ShadowStrength, Brightness, SpecColor, SpecStrength,
        SpecSize, FaceSign, UV, SkyLight, BottomShade, BackFaceShade, DetailScale, DetailStrength, PositionWS, FabricSheen,
        LightSharpness, HeightMap, HeightStrength, BitangentWS, JitterMap, HighlightColor, HighlightStrength,
        HighlightPosition, HighlightSharpness, JitterStrength, JitterStreaks, HighlightFlow, c);
    Color = c;
}

// ---------------- Eye: the painted texture + highlight, pasted texture, camera reflection ----------------
// SHG_NTE_Eye (HDRP Lit, transparent). The colour is the painted texture at screen brightness: no light, no sky
// and no shading changes it (the Lit part is switched off: no albedo, no F0). On top of it:
//   - the highlight: where its mask is white, or without a mask where the painting is at least as white
//     as HighlightThreshold
//   - a pasted texture (sky etc.): fixed on the eye (Follow 0) .. mirrored, sliding with the camera (Follow 1)
//   - what the eye looks at, mirrored on the cornea: the picture of CS_ToonEyeReflection's HDRP camera, or
//     HDRP's sky where there is none. The viewing camera's mirror direction picks the place: it slides with it.
//   - one reflection computed from the camera on the cornea
// The cornea: a spherical cap on each eyeball, every eyeball looking along the gaze both eyes share. The mesh's
// own normals are not used for it: eye meshes are bent round the face (left to the left, right to the right), with
// them each eye mirrored something else. All eyes also use one view direction (from between them) and one light,
// so reflection = reflect(-V, n) comes out at the same place in both eyes.
//   UV1 (CS_ToonEyeShape) = where in its eyeball, seen along the gaze: x = 2 + 0..1 + 4 * loss, y = 2 + 0..1.
//                           loss (0..8): how far the piece is from round (a thin strip is no cornea)
//   Gaze / GazeCenter     = the gaze / the point between the eyes, object space (w = 1 when set)
// A mesh without them: the mesh normals, the pasted texture the normal UV.
#if defined(SHADERPASS) && !defined(SHADERGRAPH_PREVIEW)
    #if SHADERPASS == SHADERPASS_FORWARD
        #define NTE_EYE_FORWARD 1
    #endif
#endif

#define NTE_EYE_UV_OFFSET 2.0
#define NTE_EYE_LOSS_SPACING 4.0
#define NTE_EYE_LOSS_STEPS 8.0

#ifdef NTE_EYE_FORWARD
// The world in direction R, as screen colour. forward.xyz / up.xyz = the eye's camera, forward.w = tan(half its angle).
float3 NTE_EyeWorld(UnityTexture2D map, float4 forward, float4 up, float3 R, float blur)
{
    float3 F = forward.xyz * rsqrt(max(dot(forward.xyz, forward.xyz), 1e-6));
    float3 U = up.xyz * rsqrt(max(dot(up.xyz, up.xyz), 1e-6));
    float3 d = float3(dot(R, cross(U, F)), dot(R, U), dot(R, F));
    float reach = max(forward.w, 1e-3);
    float widest = atan(reach);
    float angle = acos(clamp(d.z, -1.0, 1.0));
    // directions wider than the picture are drawn together toward its rim: no edge inside the eye
    float seen = widest * tanh(angle / widest) * 0.995;
    float2 side = d.xy * rsqrt(max(dot(d.xy, d.xy), 1e-8));
    float2 uv = 0.5 + side * (tan(seen) / reach) * 0.5;
    float2 texels = NTE_FWidth(uv) * max(map.texelSize.z, 1.0);
    float lod = max(saturate(blur) * 6.0, log2(max(max(texels.x, texels.y), 1.0)));      // a small eye does not sparkle
    float3 world;                                       // one way out: no "uninitialized" warning
    if (forward.w > 0.0)
        world = SAMPLE_TEXTURE2D_LOD(map.tex, s_trilinear_clamp_sampler, uv, lod).rgb;
    else
        world = SampleSkyTexture(R, saturate(blur) * 5.0, 0).rgb * GetCurrentExposureMultiplier();
    return world;
}
#endif

void NTE_EyeSimple_float(float4 Tex, float4 BaseColor, float4 UV, float4 UV1, float3 NormalWS, float3 PositionWS,
    UnityTexture2D HighlightMask, float HighlightThreshold, float4 HighlightColor, float HighlightStrength,
    UnityTexture2D OverlayMap, float4 OverlayColor, float OverlayStrength, float OverlayFollow,
    float4 GlintColor, float GlintStrength, float GlintSize,
    UnityTexture2D ViewMap, float4 ViewForward, float4 ViewUp,
    float4 MirrorColor, float MirrorStrength, float MirrorBlur, float MirrorBulge,
    float4 Gaze, float4 GazeCenter,
    out float3 Albedo, out float3 Emission, out float Smoothness, out float Metallic, out float Occlusion)
{
    Albedo = 0.0; Smoothness = 0.0; Metallic = 1.0; Occlusion = 0.0;

    float3 color = Tex.rgb * BaseColor.rgb;
    // 0 = none, 1 = the colour, above 1 = glows
    float hasMask = NTE_HasMap(HighlightMask);
    float white = smoothstep(HighlightThreshold - 0.06, HighlightThreshold + 0.06, min(Tex.r, min(Tex.g, Tex.b)));
    float mask = lerp(white, SAMPLE_TEXTURE2D_LOD(HighlightMask.tex, HighlightMask.samplerstate, UV.xy, 0).r, hasMask);
    float shade = NTE_ShadeAmount(PositionWS);
    float glow = lerp(max(HighlightStrength, 1.0), 1.0, shade);      // nothing glows in the shade
    float3 highlight = lerp(Tex.rgb, 1.0, hasMask) * HighlightColor.rgb * glow;
    color = lerp(color, highlight, mask * saturate(HighlightStrength));

#ifdef NTE_EYE_FORWARD
    // Position on this eye: -1..1 across its island.
    float mapped = step(NTE_EYE_UV_OFFSET - 0.5, min(UV1.x, UV1.y));
    float loss = max(floor((UV1.x - 1.5) / NTE_EYE_LOSS_SPACING), 0.0) * mapped;
    float2 e = ((float2(UV1.x - loss * NTE_EYE_LOSS_SPACING, UV1.y) - NTE_EYE_UV_OFFSET) * 2.0 - 1.0) * mapped;
    float inside = (1.0 - smoothstep(0.95, 1.05, length(e))) * (1.0 - mask)      // nothing on top of a highlight
                 * saturate(1.0 - loss / NTE_EYE_LOSS_STEPS);

    float3 N = normalize(NormalWS);
    float3 posRWS = GetCameraRelativePositionWS(PositionWS);
    float3 V = GetWorldSpaceNormalizeViewDir(posRWS);
    float3 axisX, axisY, axisZ = N;                         // without a measured gaze: the mesh's surface
    NTE_UVFrame(posRWS, N, UV.xy, axisX, axisY);
    if (mapped > 0.5 && Gaze.w > 0.5)
    {
        float3 f = Gaze.xyz * rsqrt(max(dot(Gaze.xyz, Gaze.xyz), 1e-12));
        float3 u = abs(f.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(0.0, 0.0, 1.0);
        u = normalize(u - f * dot(u, f));
        axisX = TransformObjectToWorldDir(cross(f, u));     // CS_ToonEyeShape's frame
        axisY = TransformObjectToWorldDir(u);
        axisZ = TransformObjectToWorldDir(f);
    }
    // Spherical cap: bulge = tan of the angle its rim turns away from the gaze.
    float bulge = max(MirrorBulge, 0.0);
    float capSin = bulge * rsqrt(1.0 + bulge * bulge);
    float2 c = e * capSin / max(length(e), 1.0);
    float3 Nd = normalize(axisX * c.x + axisY * c.y + axisZ * sqrt(saturate(1.0 - dot(c, c))));     // the cornea
    if (GazeCenter.w > 0.5)
        V = GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(GazeCenter.xyz));
    float3 R = reflect(-V, Nd);

    color *= lerp(1.0, NTE_SHADE_LEVEL, shade);

    float2 fixedUV = lerp(UV.xy, e * 0.5 + 0.5, mapped);
    float2 pasteUV = lerp(fixedUV, NTE_ViewNormal(Nd).xy * 0.5 + 0.5, saturate(OverlayFollow));
    float4 paste = SAMPLE_TEXTURE2D_LOD(OverlayMap.tex, OverlayMap.samplerstate, pasteUV, 0);
    float pasteAmount = paste.a * OverlayStrength * OverlayColor.a * NTE_HasMap(OverlayMap);
    color = lerp(color, paste.rgb * OverlayColor.rgb, saturate(pasteAmount) * inside);

    // Added like a reflection on glass: the painting stays, the bright things around show on it.
    float3 world = NTE_EyeWorld(ViewMap, ViewForward, ViewUp, R, MirrorBlur) * MirrorColor.rgb;
    float grazing = 1.0 - saturate(dot(Nd, V));
    float mirror = MirrorStrength * inside * lerp(0.75, 1.0, grazing * grazing);
    color = color * (1.0 - 0.2 * saturate(mirror)) + world * mirror * saturate(1.0 - color * 0.5);

    // A round light far away at the upper left of the camera, reflected by the same cornea: lit where the
    // mirrored view direction hits the disc. GlintSize = the disc's angular radius (4 .. 30 degrees).
    float3 camX = normalize(cross(V, UNITY_MATRIX_V[1].xyz));
    float3 camY = cross(camX, V);
    float3 key = normalize(V - camX * 0.4 + camY * 0.5);
    float away = atan2(length(cross(R, key)), dot(R, key));
    float disc = radians(lerp(4.0, 30.0, saturate(GlintSize)));
    float glint = saturate((disc - away) / max(NTE_FWidth(float2(away, 0.0)).x, 1e-4) + 0.5);
    float glintPower = lerp(GlintStrength, min(GlintStrength, 1.0), shade) * (1.0 - 0.7 * shade);
    color += GlintColor.rgb * (glint * glintPower * GlintColor.a * inside);

    color *= GetInverseCurrentExposureMultiplier();                     // screen brightness -> emission
#endif
    Emission = color;
}
void NTE_EyeSimple_half(half4 Tex, half4 BaseColor, half4 UV, float4 UV1, half3 NormalWS, float3 PositionWS,
    UnityTexture2D HighlightMask, half HighlightThreshold, half4 HighlightColor, half HighlightStrength,
    UnityTexture2D OverlayMap, half4 OverlayColor, half OverlayStrength, half OverlayFollow,
    half4 GlintColor, half GlintStrength, half GlintSize,
    UnityTexture2D ViewMap, float4 ViewForward, float4 ViewUp,
    half4 MirrorColor, half MirrorStrength, half MirrorBlur, half MirrorBulge,
    float4 Gaze, float4 GazeCenter,
    out half3 Albedo, out half3 Emission, out half Smoothness, out half Metallic, out half Occlusion)
{
    float3 a, em;
    float sm, me, oc;
    NTE_EyeSimple_float(Tex, BaseColor, UV, UV1, NormalWS, PositionWS, HighlightMask, HighlightThreshold, HighlightColor, HighlightStrength,
        OverlayMap, OverlayColor, OverlayStrength, OverlayFollow, GlintColor, GlintStrength, GlintSize,
        ViewMap, ViewForward, ViewUp, MirrorColor, MirrorStrength, MirrorBlur, MirrorBulge, Gaze, GazeCenter, a, em, sm, me, oc);
    Albedo = a; Emission = em; Smoothness = sm; Metallic = me; Occlusion = oc;
}
#endif
