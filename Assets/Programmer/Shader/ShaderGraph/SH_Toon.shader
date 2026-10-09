/* ================================================
 * 
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

/// <summary>
/// 
/// </summary>
Shader "Custom/SH_ure"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "ForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            float4 _Color;
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
