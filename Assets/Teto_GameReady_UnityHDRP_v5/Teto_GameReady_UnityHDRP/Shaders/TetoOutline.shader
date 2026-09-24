/* 吉本竜 / 2026-09-25
 * 表面の裏側だけを法線方向へ少し広げ、細い色付き輪郭線を描く。
 * キャラクターと同じボーンを使う別Rendererから描画する。
 */
Shader "MS2027/Teto/Outline HDRP"
{
    Properties
    {
        _BaseMap("Alpha texture", 2D) = "white" {}
        _OutlineColor("Outline color", Color) = (0.065,0.025,0.035,1)
        _Width("Width (metres)", Range(0,0.005)) = 0.001
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="AlphaTest+1" }
        Pass
        {
            Tags { "LightMode"="ForwardOnly" }
            Cull Front ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float _Width, _Cutoff;
            CBUFFER_END
            struct A { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                float3 p = TransformObjectToWorld(i.positionOS);
                p += TransformObjectToWorldNormal(i.normalOS)*_Width;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = i.uv;
                return o;
            }
            float4 Frag(V i):SV_Target
            {
                clip(_Width-0.000001);
                clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);
                return float4(_OutlineColor.rgb*GetCurrentExposureMultiplier(),1);
            }
            ENDHLSL
        }
    }
}
