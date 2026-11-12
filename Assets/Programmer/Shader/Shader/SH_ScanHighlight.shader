// スキャンで見えている相手の強調・透過表示用シェーダー(CS_PlayerScanHighlightが差し替えて使う)
// 縁(フレネル)を強く光らせ、内側は薄い半透明にしたシルエットを、壁や障害物を越えて描く
//
// 制作者：　吉田京志郎
//
// メモ
// ・HDRP用のUnlit。ライトの影響は受けない
// ・ZTest Alwaysなので、建物などの向こうにいても描かれる(手前の物の上にも描かれる)
// ・縁の強さは_RimPower(大きいほど縁が細くなる)、内側の濃さは_FillAlpha、縁の濃さは_RimAlpha
// ・色は_ScanColor(HDR。縁の光り方はこの明るさで変わる)
// ・元のマテリアルのプロパティ(_BaseColorなど)は使わないので、他のスクリプトのPropertyBlockとはぶつからない
Shader "Custom/SH_ScanHighlight"
{
    Properties
    {
        [HDR] _ScanColor("Scan Color", Color) = (3, 0.2, 0.2, 1)
        _FillAlpha("Fill Alpha(内側の濃さ)", Range(0, 1)) = 0.15
        _RimAlpha("Rim Alpha(縁の濃さ)", Range(0, 1)) = 0.9
        _RimPower("Rim Power(大きいほど縁が細い)", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // HDRPはカメラ相対のワールド座標を使う
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionRWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // 正面向きの面ほど0、縁(視線と面が平行)ほど1
                float rim = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _RimPower);
                float alpha = saturate(lerp(_FillAlpha, _RimAlpha, rim));
                // 内側は薄く、縁は色を明るく(HDR)見せる
                float3 color = _ScanColor.rgb * lerp(1.0, 2.0, rim);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
