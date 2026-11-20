// スキャンで見えている相手の強調・透過表示用シェーダー(CS_ScanHighlightPass(カスタムパス)が、見えている相手を描き直す時に使う)
// 4つのパスを使い分ける(どれを描くかはCS_ScanHighlightPassが決める)
//   0: 隠れている部分だけのシルエット(縁を強く光らせ、内側は薄い半透明)
//   1: どこでも描くシルエット(波が当たった直後の短い間だけ、モデル全体に出す)
//   2: マスク書き込み(映っている部分の形だけを、一時的な画像に描く。画面には出ない)
//   3: アウトライン合成(マスクの外側で、マスクの近く(_OutlinePixels以内)の画素だけに、線を重ねる。画面全体を1回描く)
//
// 制作者：　吉田京志郎
//
// メモ
// ・HDRP用のUnlit。ライトの影響は受けない
// ・元のモデルに重ねて描き直す前提(モデルのマテリアルとして差し替えると、パス0は何も描かれない部分ができる)
// ・アウトラインは、モデルを膨らませる方式ではなく、画面上で形の外側に線を足す方式
//   (角ばった箱型のモデルは、膨らませると面がバラバラに離れて、内側が透けて見えるため)
//   太さは_OutlinePixels(ピクセル)。色は_ScanColorの色味(HDRの明るさは使わず、光らない明るさにそろえる)
// ・「隠れているか」は、ZTestではなく、カメラの深度テクスチャを読んで自分で判定する(深度の向きや、元のモデルと同じ深度でのぶれに左右されないため)
//   パス0: 手前の物の深度が、この面より_HiddenMargin(m)以上手前なら隠れている → 描く
//   パス2: 手前の物の深度が、この面より_OutlineMargin(m)以上手前なら隠れている → マスクに描かない(アウトラインは映っている部分だけ)
//   そのため、深度テクスチャを読める位置(CS_ScanHighlightPassのBeforePostProcess)で描く前提
// ・縁の強さは_RimPower(大きいほど縁が細くなる)、内側の濃さは_FillAlpha、縁の濃さは_RimAlpha
// ・色は_ScanColor(HDR。シルエットの縁の光り方はこの明るさで変わる)
// ・元のマテリアルのプロパティ(_BaseColorなど)は使わないので、他のスクリプトのPropertyBlockとはぶつからない
Shader "Custom/SH_ScanHighlight"
{
    Properties
    {
        [HDR] _ScanColor("Scan Color", Color) = (3, 0.2, 0.2, 1)
        _FillAlpha("Fill Alpha(内側の濃さ)", Range(0, 1)) = 0.15
        _RimAlpha("Rim Alpha(縁の濃さ)", Range(0, 1)) = 0.9
        _RimPower("Rim Power(大きいほど縁が細い)", Range(0.5, 8)) = 2.5
        _OutlinePixels("Outline Pixels(アウトラインの太さ・ピクセル)", Range(1, 8)) = 3
        _OutlineAlpha("Outline Alpha(アウトラインの濃さ)", Range(0, 1)) = 1
        _HiddenMargin("Hidden Margin(シルエットを出す、手前の物との距離・m)", Range(0, 1)) = 0.1
        _OutlineMargin("Outline Margin(アウトラインを消す、手前の物との距離・m)", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }

        // 0: 隠れている部分だけのシルエット
        Pass
        {
            Name "HiddenSilhouette"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimPower;
                float _OutlinePixels;
                float _OutlineAlpha;
                float _HiddenMargin;
                float _OutlineMargin;
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

            // この面と、同じ画素の手前の物(深度テクスチャ)の、カメラからの距離の差(正なら、手前に物がある)
            float OccluderDistance(float4 positionCS)
            {
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(positionCS.xy)), _ZBufferParams);
                float selfDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
                return selfDepth - sceneDepth;
            }

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

            float4 SilhouetteColor(Varyings input)
            {
                // 正面向きの面ほど0、縁(視線と面が平行)ほど1
                float rim = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _RimPower);
                float alpha = saturate(lerp(_FillAlpha, _RimAlpha, rim));
                // 内側は薄く、縁は色を明るく(HDR)見せる
                float3 color = _ScanColor.rgb * lerp(1.0, 2.0, rim);
                return float4(color, alpha);
            }

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                // 手前に物がある(隠れている)画素だけ描く
                clip(OccluderDistance(input.positionCS) - _HiddenMargin);
                return SilhouetteColor(input);
            }
            ENDHLSL
        }

        // 1: どこでも描くシルエット(波が当たった直後の短い間)
        Pass
        {
            Name "AlwaysSilhouette"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimPower;
                float _OutlinePixels;
                float _OutlineAlpha;
                float _HiddenMargin;
                float _OutlineMargin;
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

            // この面と、同じ画素の手前の物(深度テクスチャ)の、カメラからの距離の差(正なら、手前に物がある)
            float OccluderDistance(float4 positionCS)
            {
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(positionCS.xy)), _ZBufferParams);
                float selfDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
                return selfDepth - sceneDepth;
            }

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

            float4 SilhouetteColor(Varyings input)
            {
                // 正面向きの面ほど0、縁(視線と面が平行)ほど1
                float rim = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _RimPower);
                float alpha = saturate(lerp(_FillAlpha, _RimAlpha, rim));
                // 内側は薄く、縁は色を明るく(HDR)見せる
                float3 color = _ScanColor.rgb * lerp(1.0, 2.0, rim);
                return float4(color, alpha);
            }

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                return SilhouetteColor(input);
            }
            ENDHLSL
        }

        // 2: マスク書き込み(映っている部分の形)
        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "ForwardOnly" }

            Blend Off
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimPower;
                float _OutlinePixels;
                float _OutlineAlpha;
                float _HiddenMargin;
                float _OutlineMargin;
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

            // この面と、同じ画素の手前の物(深度テクスチャ)の、カメラからの距離の差(正なら、手前に物がある)
            float OccluderDistance(float4 positionCS)
            {
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(positionCS.xy)), _ZBufferParams);
                float selfDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
                return selfDepth - sceneDepth;
            }

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

            float4 SilhouetteColor(Varyings input)
            {
                // 正面向きの面ほど0、縁(視線と面が平行)ほど1
                float rim = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _RimPower);
                float alpha = saturate(lerp(_FillAlpha, _RimAlpha, rim));
                // 内側は薄く、縁は色を明るく(HDR)見せる
                float3 color = _ScanColor.rgb * lerp(1.0, 2.0, rim);
                return float4(color, alpha);
            }

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                // 手前に物がある(隠れている)画素はマスクに描かない
                clip(_OutlineMargin - OccluderDistance(input.positionCS));
                // 光って見えないよう、HDRの明るさは使わず、色味だけ使う(最大成分を1にそろえる)
                float3 color = _ScanColor.rgb / max(max(_ScanColor.r, max(_ScanColor.g, _ScanColor.b)), 1.0);
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        // 3: アウトライン合成(画面全体に1回)
        Pass
        {
            Name "OutlineComposite"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimPower;
                float _OutlinePixels;
                float _OutlineAlpha;
                float _HiddenMargin;
                float _OutlineMargin;
            CBUFFER_END

            #pragma vertex VertFull
            #pragma fragment FragFull

            TEXTURE2D(_ScanMaskTexture);   // パス2が描いたマスク(rgb: 線の色 / a: 映っている部分の形)

            struct VaryingsFull
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsFull VertFull(uint vertexID : SV_VertexID)
            {
                VaryingsFull output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return output;
            }

            float4 FragFull(VaryingsFull input) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);

                // 形の内側には描かない(線は外側だけ)
                if (LOAD_TEXTURE2D(_ScanMaskTexture, pixel).a > 0.5) discard;

                // 周り(_OutlinePixels以内)に形があれば、その色で線を描く。近い所と遠い所の2つの輪を、16方向で見る
                float radius = _OutlinePixels;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * (TWO_PI / 16.0);
                    float2 dir = float2(cos(angle), sin(angle));
                    for (int j = 1; j <= 2; j++)
                    {
                        int2 p = pixel + int2(round(dir * (radius * j * 0.5)));
                        float4 mask = LOAD_TEXTURE2D(_ScanMaskTexture, p);
                        if (mask.a > 0.5) return float4(mask.rgb, _OutlineAlpha);
                    }
                }
                discard;
                return 0;
            }
            ENDHLSL
        }
    }
}
