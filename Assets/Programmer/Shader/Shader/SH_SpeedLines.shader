// 集中線のポストプロセス(CSV_SpeedLinesから使う)
// 画面の中心から外へ向かう線を、元の画面に重ねて描く
//
// 制作者：　吉田京志郎
//
// メモ
// ・中心からの角度で画面を_LineCount個に区切り、区切りごとに乱数で「線を出すか」「太さ」「内側の端の位置」を決める
// ・乱数は_FlickerPhaseの整数部分が変わるたびに変わるので、線がパラパラ切り替わる
// ・_FlickerPhase / _FlowDistanceはCSV_SpeedLinesが毎フレーム「経過時間 × 今の速さ」を足し込んだ値
// ・角度で区切っているため、線は中心に近いほど細くなる(集中線らしい形になる)
// ・各線は「中心側から外へ流れていく筋」が繰り返し並んだ形で、_FlowDistanceの分だけ外へ流れる
//   (Weightが1になって強さが一定になった後も、線が動き続けて勢いが落ちないようにするため)
//   筋は外側の先端が一番濃く、中心側へ向かって薄くなる。筋と筋の間には筋と同じ長さの隙間を空ける
Shader "Hidden/Shader/SH_SpeedLines"
{
    Properties
    {
        // CommandBuffer.Blitで元の画面を_MainTexに渡すために必要
        _MainTex("Main Texture", 2DArray) = "grey" {}
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_MainTex);
    float _Intensity;
    float4 _LineColor;
    float2 _Center;
    float _LineCount;
    float _Density;
    float _LineWidth;
    float _InnerRadius;
    float _Softness;
    float _FlickerPhase;
    float _FlowDistance;
    float _StreakLength;
    float _AspectRatio;

    // 0〜1の乱数(同じ入力なら同じ値)
    float Hash11(float p)
    {
        p = frac(p * 0.1031);
        p *= p + 33.33;
        p *= p + p;
        return frac(p);
    }

    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 sourceColor = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(input.texcoord.xy)).xyz;

        // 中心からの向きと距離(横長の画面でも円になるよう、横方向を画面比で伸ばす)
        float2 offset = input.texcoord - _Center;
        offset.x *= _AspectRatio;
        float distFromCenter = length(offset);

        // 角度を0〜1にして_LineCount個に区切る(1区切りに線1本)
        float lineCount = max(floor(_LineCount), 1.0);
        float slot = (atan2(offset.y, offset.x) * INV_TWO_PI + 0.5) * lineCount;
        float lineId = floor(slot);
        float fromLineCenter = abs(frac(slot) - 0.5);

        // 一定間隔で乱数を変え、線の出方を切り替える
        float frame = floor(_FlickerPhase);
        float randomShow = Hash11(lineId + frame * 37.0);
        float randomShape = Hash11(lineId * 1.37 + frame * 11.0 + 0.5);

        // 線を出すか(_Densityの割合で出す)
        float show = step(1.0 - _Density, randomShow);

        // 線の太さ(本ごとに少し変える)。端は少しぼかす
        float halfWidth = _LineWidth * 0.5 * lerp(0.3, 1.0, randomShape);
        float lineMask = 1.0 - smoothstep(halfWidth * 0.5, halfWidth, fromLineCenter);

        // 外へ流れる筋(周期は筋の長さの2倍。前半が筋、後半が隙間)
        // 流れる速さと筋の並び始めの位置は本ごとにずらす(切り替わっても同じ線なら途切れないよう、frameは使わない)
        float flowDistance = _FlowDistance * lerp(0.7, 1.3, Hash11(lineId * 3.17 + 0.25));
        float flowPhase = Hash11(lineId * 7.31 + 0.75);
        float period = max(_StreakLength, 0.001) * 2.0;
        float flow = frac((distFromCenter - flowDistance) / period + flowPhase);
        float headFade = 1.0 - smoothstep(0.46, 0.5, flow);   // 外側の先端をわずかにぼかす
        float streakMask = step(flow, 0.5) * saturate(flow / 0.5) * headFade;

        //---------------ここまで完了

        // 中心付近には出さない(内側の端の位置は本ごとにずらす)
        float inner = _InnerRadius * lerp(0.8, 1.3, randomShape);
        float radialMask = smoothstep(inner, inner + _Softness, distFromCenter);

        float alpha = saturate(show * lineMask * streakMask * radialMask * _Intensity * _LineColor.a);
        float3 color = lerp(sourceColor, _LineColor.rgb, alpha);

        return float4(color, 1);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "SpeedLines"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
