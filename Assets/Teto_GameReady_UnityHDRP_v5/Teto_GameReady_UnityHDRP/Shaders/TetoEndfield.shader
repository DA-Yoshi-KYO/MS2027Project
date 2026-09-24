/* ================================================
 * HDRP用のテト専用NPR・PBRハイブリッドシェーダー
 * 制作者：吉本竜 / 制作日：2026-09-25
 * 三段階の色付き陰影、GGX反射、髪の異方性、顔の方向補正を合成する。
 * 考え方の参考：https://techartnomad.tistory.com/735
 * 記事のURPコードには依存せず、HDRPの座標・露出に合わせて実装する。
 * ================================================ */
Shader "MS2027/Teto/Endfield HDRP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base color", Color) = (1,1,1,1)
        [Normal] _NormalMap("Normal map", 2D) = "bump" {}
        _NormalStrength("Normal strength", Range(0,1)) = 0.3
        [Enum(Cloth,0,Skin,1,Face,2,Hair,3,Eye,4)] _MaterialKind("Material kind", Float) = 0
        _ShadowColor("Shadow tint", Color) = (0.53,0.49,0.6,1)
        _MidColor("Middle tint", Color) = (0.85,0.8,0.84,1)
        _Softness("Shadow softness", Range(0.01,0.5)) = 0.12
        _Roughness("Roughness", Range(0.08,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0
        _Specular("Specular strength", Range(0,2)) = 0.3
        _HairColor("Hair highlight", Color) = (1,0.55,0.5,1)
        _RimColor("Rim tint", Color) = (0.65,0.78,1,1)
        _RimStrength("Rim strength", Range(0,1)) = 0.15
        _DayStrength("Daylight influence", Range(0,1)) = 0.85
        _Brightness("Brightness", Range(0.1,4)) = 1
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    HLSLINCLUDE
    #pragma target 4.5
    #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
    #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
    #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadow.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _ShadowColor, _MidColor, _HairColor, _RimColor;
        float _NormalStrength, _MaterialKind, _Softness, _Roughness, _Metallic;
        float _Specular, _RimStrength, _DayStrength, _Brightness, _Cutoff;
    CBUFFER_END
    // 各キャラクターのMaterialPropertyBlockから受け取るため、別個体にも対応する。
    float4 _TetoLightDirection, _TetoHeadForward, _TetoHeadRight;
    struct Attributes
    {
        float3 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float4 tangentWS : TEXCOORD2;
        float2 uv : TEXCOORD3;
        UNITY_VERTEX_OUTPUT_STEREO
    };
    Varyings Vert(Attributes i)
    {
        Varyings o;
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.positionWS = TransformObjectToWorld(i.positionOS);
        o.positionCS = TransformWorldToHClip(o.positionWS);
        o.normalWS = TransformObjectToWorldNormal(i.normalOS);
        o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
        o.uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
        return o;
    }
    // 色と深度で同じアルファ判定を使い、前髪・まつ毛の四角い透過面を残さない。
    float4 ReadBase(float2 uv)
    {
        float4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        clip(c.a - _Cutoff);
        return c;
    }
    float4 Depth(Varyings i) : SV_Target { ReadBase(i.uv); return 0; }
    float4 Shade(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float3 base = ReadBase(i.uv).rgb;
        float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(facing, 1.0, -1.0);
        float3 t = normalize(i.tangentWS.xyz);
        float3 b = normalize(cross(n,t)) * i.tangentWS.w;
        float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
        n = normalize(lerp(n, t*normalTS.x+b*normalTS.y+n*normalTS.z, _NormalStrength));
        float3 l = normalize(_TetoLightDirection.xyz + float3(0,0.0001,0));
        float3 v = normalize(GetWorldSpaceNormalizeViewDir(i.positionWS));
        float3 h = normalize(l+v);
        float nl = dot(n,l);
        float skin = step(0.5,_MaterialKind) * (1-step(2.5,_MaterialKind));
        float face = step(1.5,_MaterialKind) * (1-step(2.5,_MaterialKind));
        float hair = step(2.5,_MaterialKind) * (1-step(3.5,_MaterialKind));
        float eye = step(3.5,_MaterialKind);
        // HDRPの太陽光シャドウを参照し、前髪や腕が落とす実際の影も色付きの暗部へ合成する。
        float shadow = 1;
        if (_DirectionalShadowIndex >= 0)
        {
            HDShadowContext shadowContext = InitShadowContext();
            DirectionalLightData sun = _DirectionalLightDatas[_DirectionalShadowIndex];
            if (sun.shadowIndex >= 0)
                shadow = GetDirectionalShadowAttenuation(shadowContext, i.positionCS.xy, i.positionWS,
                    normalize(i.normalWS), sun.shadowIndex, -sun.forward);
        }

        // 専用SDFを持たないテトでは頭の正面・左右方向で顔の陰影を丸める。
        // 鼻周りの細かい法線だけで顔全体が黒くなるのを防ぐ近似。
        float3 headForward = normalize(_TetoHeadForward.xyz + float3(0,0,-0.0001));
        float faceLight = dot(headForward,l) * 0.7 + nl * 0.3 + 0.25;
        nl = lerp(nl,faceLight,face);
        float lightBand = smoothstep(0.0-_Softness,0.0+_Softness,nl);
        float highBand = smoothstep(0.48-_Softness,0.48+_Softness,nl);
        lightBand *= lerp(shadow,1.0,face*0.35);
        highBand *= shadow;
        float3 ramp = lerp(_ShadowColor.rgb,_MidColor.rgb,lightBand);
        ramp = lerp(ramp,1.0,highBand);
        // 日陰でも上方からの柔らかい補助光を残す。
        float topLight = saturate(n.y * 0.5+0.5);
        float3 diffuse = base * lerp(lerp(_ShadowColor.rgb,0.9,topLight),ramp,_DayStrength);
        diffuse += base * topLight * 0.055;

        // GGXの鏡面反射を布・靴に限定し、肌のプラスチック感を抑える。
        float nh = saturate(dot(n,h)), nv = max(0.08,saturate(dot(n,v)));
        float a = _Roughness*_Roughness, a2 = a*a;
        float denominator = nh*nh*(a2-1)+1;
        float distribution = a2 / max(0.001,PI*denominator*denominator);
        float k = (_Roughness+1)*(_Roughness+1)*0.125;
        float geometry = nv/(nv*(1-k)+k) * saturate(nl)/(saturate(nl)*(1-k)+k);
        float3 f0 = lerp(0.04.xxx,base,_Metallic);
        float3 fresnel = f0+(1-f0)*pow(1-saturate(dot(v,h)),5);
        float3 spec = distribution*geometry*fresnel/max(0.1,4*nv*saturate(nl));
        spec *= _Specular * (1-hair) * lerp(1,0.2,skin);
        // 髪の流れに直交する帯状ハイライト。細い帯と幅広い帯を重ねる。
        float strand = dot(b,h);
        float aniso = pow(saturate(1-strand*strand),48) * 0.7 + pow(saturate(1-strand*strand),10)*0.12;
        spec += hair * aniso * _HairColor.rgb * _Specular * smoothstep(-0.2,0.6,nl);
        spec += eye * pow(nh,90) * 0.45;
        float rim = pow(1-saturate(dot(n,v)),4) * smoothstep(-0.2,0.8,nl);
        float3 color = diffuse + spec + rim*_RimColor.rgb*_RimStrength*(1-face);
        // 逆光時の肌の赤みを薄く加える。
        color += skin * base * float3(0.2,0.055,0.035) * pow(saturate(dot(-l,v)),3) * (1-lightBand)*0.3;
        return float4(color * _Brightness * GetCurrentExposureMultiplier(),1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="AlphaTest" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull [_Cull] ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Shade
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull [_Cull] ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
