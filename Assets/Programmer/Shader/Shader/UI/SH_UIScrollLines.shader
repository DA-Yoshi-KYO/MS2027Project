/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-24 | 初回作成
 * ================================================ */


// ============================================================
// 【これは何?】
//   線(ライン)の画像を、UVをずらして「下から上へ」流し続けるシェーダー。UI(uGUI)用。
//   タイトルメニューの「選択中ボタンの背景」に重ねる、線のアニメーション用。
//
// 【仕組み】
//   画像を読む位置(UV)の縦方向を、時間とともに少しずつずらす。
//   ずらした量が「1周期ぶん」になると元の見た目に戻るので、切れ目なくループする。
//     ・Scroll Speed が + : 線が下から上へ登っていく
//     ・Scroll Speed が - : 線が上から下へ降りていく
//
// 【使い方】
//   1. このシェーダーから MT_UIScrollLines というマテリアルを作る
//      (シェーダーを右クリック > Create > Material)
//   2. 線だけの画像を「Raw Image」に設定し、Material に MT_UIScrollLines を設定する
//      (背景の枠とは別のオブジェクト(子)として、背景の上に重ねる)
//   3. 線の画像(Texture)の Import 設定で Wrap Mode を「Repeat」にする
//
// 【線の画像の条件】
//   ・縦方向に「継ぎ目なく繰り返せる」画像にする
//     (上端と下端がつながって見える画像。そうでないと、ループの境目で線が途切れる)
//   ・背景が透明の画像にする(背景付きだと、背景ごと流れる)
//
// 【角が欠けた四角形(面取りした形)のボタンで使う場合】
//   線の画像をそのまま流すと、画像の形(角の欠け)ごと動いたり、繰り返しで何個も現れたりする。
//   → 「形のマスク画像」を別に設定して、形は動かさず、線だけを流す。
//     1. 線の画像は、ふつうの長方形(全面に線が並んだ画像)にする
//     2. マテリアルの「Use Shape Mask」をONにする
//     3. 「Shape Mask」に、ボタンの形の画像(角が欠けた背景など。形の外が透明の画像)を設定する
//     → マスクの透明な所(角の外)では、線が表示されない。マスクは流れない
//   (マテリアルを使わずに、Unity の Mask コンポーネントで同じことをする方法もある)
//
// 【注意】
//   ・Image(スプライト)で使う場合は、Sprite Atlas に入れない・Mesh Type を Full Rect にする
//     (Atlas に入れると、繰り返しの時に隣の画像が見える)。Raw Image なら気にしなくてよい
//   ・時間は _Time を使うので、Time.timeScale が 0 だと止まる
// ============================================================
Shader "Custom/SH_UIScrollLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // 線が流れる速さ。1秒間に何周期ぶん流れるか(+で下から上、-で上から下)
        _ScrollSpeed ("Scroll Speed Cycles Per Sec", Float) = 0.5

        // 縦に何周期ぶんの線を並べるか(大きいほど線が細かく・数が増える)
        _TilingY ("Tiling Y Repeat Count", Float) = 1.0

        // 横に何周期ぶんの線を並べるか(通常は1)
        _TilingX ("Tiling X Repeat Count", Float) = 1.0

        // 上下の端をぼかして消す幅(0=ぼかさない、0.5=中央に向かって大きくぼかす)
        _EdgeFade ("Edge Fade Top Bottom", Range(0, 0.5)) = 0.0

        // 形のマスク(流れない)。ONにすると、この画像の不透明な所にだけ線が表示される
        [Toggle(_USE_SHAPE_MASK)] _UseShapeMask ("Use Shape Mask", Float) = 0
        [NoScaleOffset] _ShapeMask ("Shape Mask", 2D) = "white" {}

        // --- UI用の標準プロパティ(マスクやStencilのため。触らなくてよい) ---
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]

        // 色は「アルファを掛けた状態(premultiplied)」で出力するので、この合成式にする
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ _USE_SHAPE_MASK

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _ShapeMask;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;

            float _ScrollSpeed;
            float _TilingY;
            float _TilingX;
            float _EdgeFade;

            // ----------------------------------------------------
            // 頂点シェーダー(UIの標準的な処理)
            // ----------------------------------------------------

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;

                return OUT;
            }

            // ----------------------------------------------------
            // フラグメントシェーダー(1ピクセルごとの色を決める)
            // ----------------------------------------------------

            float4 frag(v2f IN) : SV_Target
            {
                // ずらす量(0〜1。1になると1周期ぶん進んで、0に戻る)
                // frac で 0〜1 に収めておくと、長時間動かしても値が大きくならず、細かい動きがぶれない
                float offset = frac(_Time.y * _ScrollSpeed);

                // 画像を読む位置
                //   縦: 位置に応じて並べ、そこから offset を引く
                //       → 読む位置が下がっていくので、線は上へ登っていく
                float2 uv = IN.texcoord;
                uv.x = uv.x * _TilingX;
                uv.y = uv.y * _TilingY - offset;

                // 画像の色(Wrap Mode が Repeat なら、範囲外も繰り返して読まれる)
                float4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                // 上下の端をぼかして消す(使わない時は 0 のまま)
                float fade = 1.0;

                if (_EdgeFade > 0.0)
                {
                    float y = IN.texcoord.y;
                    fade = smoothstep(0.0, _EdgeFade, y) * smoothstep(0.0, _EdgeFade, 1.0 - y);
                }

                color.a *= fade;

            #if defined(_USE_SHAPE_MASK)
                // 形のマスク: 流れる前の位置(IN.texcoord)で読むので、形は動かない。
                // マスクが透明な所(角の欠けなど)では、線が消える
                color.a *= tex2D(_ShapeMask, IN.texcoord).a;
            #endif

            #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
            #endif

            #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
            #endif

                // アルファを掛けた色にして出力する
                color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }
}


