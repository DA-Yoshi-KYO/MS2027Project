// 警察の視界を、光のように見せるシェーダー(CS_PoliceVisionLight が作る立体のメッシュに使う)
// HDRP用のUnlit。加算の半透明で、色と濃さは頂点カラー(rgb: 色 / a: 濃さ)で決める。両面を描き、深度は書かない
// 光の束の面(UVのxが1の頂点)は、視線とほぼ平行になる輪郭の所を薄くして、懐中電灯の光のように、縁をぼかす
// 壁などの手前の物には隠れる。カメラが光の中に入っても真っ白にならないよう、カメラに近い所は薄くする
//
// 制作者：宇留野陸斗
Shader "Custom/SH_PoliceVisionLight"
{
    Properties
    {
        _Intensity("Intensity(全体の濃さ)", Range(0, 4)) = 1
        _SilhouettePower("Silhouette Power(光の束の輪郭のぼけ具合。大きいほど、ぼける)", Range(0, 4)) = 1.4
        _NearFadeStart("Near Fade Start(これより近いと見えない・m)", Range(0, 5)) = 0.5
        _NearFadeDistance("Near Fade Distance(ここから先で、元の濃さになる・m)", Range(0.1, 10)) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "VisionLight"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _SilhouettePower;
                float _NearFadeStart;
                float _NearFadeDistance;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 color       : COLOR;
                float3 positionRWS : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  softWeight  : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // HDRPはカメラ相対のワールド座標を使う
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.positionRWS = positionRWS;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.softWeight = input.uv.x;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // カメラ相対の座標なので、長さがそのままカメラからの距離になる
                float nearFade = saturate((length(input.positionRWS) - _NearFadeStart) / _NearFadeDistance);
                // 面を正面から見ている所ほど1、輪郭(視線と面が平行)ほど0
                float facing = abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionRWS)));
                float silhouette = lerp(1.0, pow(saturate(facing), _SilhouettePower), input.softWeight);
                return float4(input.color.rgb, saturate(input.color.a * _Intensity * nearFade * silhouette));
            }
            ENDHLSL
        }
    }
}
