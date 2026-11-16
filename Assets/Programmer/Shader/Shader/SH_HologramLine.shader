// ホログラム風に光る線のシェーダー(動くSAポイントの軌道の点線に使う仮のシェーダー。本番のシェーダーができたら差し替える)
// 線の進む方向へ流れる点線に、細かい走査線のちらつきを乗せ、線の両ふちをやわらかく消す
//
// 制作者：　吉田京志郎
//
// メモ
// ・HDRP用のUnlit。ライトの影響は受けない。加算(Blend SrcAlpha One)で光って見える。Bloomがあればにじむ
// ・LineRendererに使う。Texture ModeをTile、Texture Scaleを(1, 1)にすると、UVのUは「線の始まりからの長さ(m)」、UVのVは「線の幅の中での位置(0〜1)」になる
//   点線の長さ(_DashPeriod)はワールドの長さ(m)で決まるので、線の長さに関係なく一定
// ・色(アルファ)は_HologramColor。アルファは、スクリプトが出し入れのフェードに使う(MaterialPropertyBlockで渡す)
//   本番のシェーダーに差し替える時は、CS_UISAPathViewの「色のプロパティ名」を、そのシェーダーの名前に合わせる
// ・流れ・走査線のちらつきは、シェーダーの中の時間(_TimeParameters.x)で動かす。スクリプトからは渡さない
// ・明るさは_Brightness(HDRの色に掛ける)。点線の長さは_DashPeriod、点の割合は_DashRatio、流れる速さは_FlowSpeed
// ・走査線: 高さ(ワールドY)方向の細かい縞(_ScanlineFrequency)が上へ流れ(_ScanlineSpeed)、_FlickerStrengthの強さで明るさが揺れる
// ・線の両ふちは、_EdgeSoftness(線の幅に対する割合)の分だけやわらかく消える
Shader "Custom/SH_HologramLine"
{
    Properties
    {
        [HDR] _HologramColor("Hologram Color(アルファは出し入れのフェード)", Color) = (0.5, 1.8, 2.4, 1)
        _Brightness("Brightness(HDRの色に掛ける)", Range(0, 8)) = 2
        _DashPeriod("Dash Period(点線の1周期の長さ m)", Range(0.1, 5)) = 1
        _DashRatio("Dash Ratio(1周期のうち光る割合)", Range(0.05, 0.95)) = 0.55
        _FlowSpeed("Flow Speed(点線の流れる速さ m/秒。負で逆向き)", Range(-10, 10)) = 2
        _ScanlineFrequency("Scanline Frequency(走査線の細かさ)", Range(1, 200)) = 60
        _ScanlineSpeed("Scanline Speed(走査線の流れる速さ)", Range(-20, 20)) = 6
        _FlickerStrength("Flicker Strength(ちらつきの強さ)", Range(0, 1)) = 0.35
        _EdgeSoftness("Edge Softness(両ふちをやわらかく消す幅)", Range(0.01, 0.5)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HologramColor;
                float _Brightness;
                float _DashPeriod;
                float _DashRatio;
                float _FlowSpeed;
                float _ScanlineFrequency;
                float _ScanlineSpeed;
                float _FlickerStrength;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionAWS : TEXCOORD1;   // 絶対ワールド座標(走査線の高さに使う)
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // HDRPはカメラ相対のワールド座標を使う
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.uv = input.uv;
                output.positionAWS = GetAbsolutePositionWS(positionRWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float time = _TimeParameters.x;

                // 流れる点線(Uは線の始まりからの長さ[m])。端はやわらかく
                float dashPosition = frac((input.uv.x - time * _FlowSpeed) / _DashPeriod);
                float dashMask = smoothstep(0.0, 0.08, dashPosition) * (1.0 - smoothstep(_DashRatio - 0.08, _DashRatio, dashPosition));

                // 走査線のちらつき(高さ方向の細かい縞が流れて、明るさが揺れる)
                float scanline = 0.5 + 0.5 * sin(input.positionAWS.y * _ScanlineFrequency - time * _ScanlineSpeed);
                float flicker = 1.0 - _FlickerStrength * scanline;

                // 線の両ふち(Vは線の幅の中での位置0〜1)をやわらかく消す
                float edge = smoothstep(0.0, _EdgeSoftness, input.uv.y) * smoothstep(0.0, _EdgeSoftness, 1.0 - input.uv.y);

                float alpha = saturate(dashMask * edge * flicker * _HologramColor.a);
                return float4(_HologramColor.rgb * _Brightness, alpha);
            }
            ENDHLSL
        }
    }
}
