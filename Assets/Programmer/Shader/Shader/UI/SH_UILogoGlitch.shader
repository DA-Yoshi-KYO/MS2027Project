/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-24 | 初回作成
 * ================================================ */

// ============================================================
// 【これは何?】
//   タイトルロゴ用のグリッチ(ノイズ + ぶれ)シェーダー。UI(uGUI)の Image / Raw Image 用。
//   一定間隔(例: 3秒に1回)で、短い時間だけグリッチが出る。それ以外はふつうのロゴ。
//
// 【出る効果】
//   1. 帯ずれ     : ロゴを横の帯に分け、帯ごとに左右へずらす(太い帯 + 細い帯の2種類)
//   2. 横に伸びる線: 細い横の行だけ、ロゴの一部が横へ引き伸ばされる
//   3. 色ずれ     : 左右にずれた色の分身が重なる(下の2モードで見た目が変わる)
//   4. ノイズ     : ロゴの形の中に白黒のザラつき + 周りに色付きの小さな点
//   5. 全体のぶれ : ロゴ全体が小さく揺れる
//
// 【色の重ね方の切り替え】 マテリアルの「Additive RGB Split」
//   OFF(通常モード / ORDER:ZERO 風)
//     ロゴ本体はそのまま。赤系と水色系の細い分身が、本体の外側だけに見える。
//   ON(足し算モード / ON SPECIAL 風)
//     ロゴの色を R / G / B の3つに分けて、それぞれ別の位置にずらして足し算で重ねる。
//     3つが重なった所は元の色(白いロゴなら白)、ずれた縁だけ赤・緑・青に分かれる。
//     縦にもずらす(RGBの縦ずれ量)と、色の帯が上下にも分かれる。
//
// 【使い方】
//   1. このシェーダーを右クリック > Create > Material で MT_UILogoGlitch を作る
//   2. ロゴの Image / Raw Image の Material に設定する
//   3. マテリアルの Inspector で、間隔・強さ・重ね方を調整する
//   (スクリプトから切り替える場合: material.EnableKeyword("_GHOSTADD_ON") / DisableKeyword)
//
// 【注意】
//   ・ずれた部分がはみ出せるように、ロゴ画像の周りに透明の余白を付けておく
//     (余白がないと、画像の枠で切れる)
//   ・ロゴ画像は背景が透明の PNG を使う(背景付きの画像だと、背景ごとずれる)
//   ・Sprite Atlas に入れた画像は、ずれた時に隣の画像が見えることがある
//   ・時間は _Time を使うので、Time.timeScale が 0 だと止まる
// ============================================================
Shader "Custom/SH_UILogoGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // ---------- タイミング ----------
        [Header(Timing)]
        _Interval ("Glitch Interval Sec", Float) = 3.0              // グリッチの間隔(秒)
        _Duration ("Glitch Duration Sec", Float) = 0.35             // 1回のグリッチの長さ(秒)
        _TimeOffset ("Start Offset Sec", Float) = 0.0               // 開始のずらし(秒)
        _StepRate ("Steps Per Sec", Float) = 24.0                   // コマ送りの速さ(回/秒)

        // ---------- 帯ずれ ----------
        [Header(Slice Shake)]
        _SliceCount ("Coarse Slice Count", Range(2, 32)) = 10       // 太い帯の数
        _SliceShift ("Coarse Slice Shift", Range(0, 0.2)) = 0.03    // 太い帯のずれ量(UV)
        _SliceChance ("Coarse Slice Chance", Range(0, 1)) = 0.35    // 太い帯がずれる確率
        _FineSliceCount ("Fine Slice Count", Range(16, 256)) = 96   // 細い帯の数
        _FineSliceShift ("Fine Slice Shift", Range(0, 0.2)) = 0.02  // 細い帯のずれ量(UV)
        _FineSliceChance ("Fine Slice Chance", Range(0, 1)) = 0.2   // 細い帯がずれる確率
        _JitterAmount ("Jitter Amount", Range(0, 0.05)) = 0.003     // 全体のぶれ量(UV)

        // ---------- 横に伸びる線 ----------
        [Header(Streak)]
        _StreakChance ("Streak Amount", Range(0, 1)) = 0.06         // 横に伸びる線の量
        _StreakCellWidth ("Streak Length", Range(0.02, 0.5)) = 0.12 // 横に伸びる線の長さ(UV)

        // ---------- 色ずれ 通常モード ----------
        [Header(Color Split Normal Mode)]
        _SplitAmount ("Split Amount", Range(0, 0.05)) = 0.006       // 色ずれ量(UV)
        _GhostColorA ("Ghost Color A", Color) = (1, 0.15, 0.35, 1)  // 分身の色A
        _GhostColorB ("Ghost Color B", Color) = (0.1, 0.9, 1, 1)    // 分身の色B
        _GhostStrength ("Ghost Strength", Range(0, 2)) = 1.0        // 分身の濃さ

        // ---------- 色ずれ 足し算モード ----------
        [Header(Color Split Additive Mode)]
        [Toggle(_GHOSTADD_ON)] _GhostAdditive ("Additive RGB Split", Float) = 0 // 色を足し算で重ねる(RGB分離)
        _RgbSplitX ("RGB Split X", Range(0, 0.1)) = 0.02            // RGBの横ずれ量(UV)
        _RgbSplitY ("RGB Split Y", Range(0, 0.1)) = 0.0             // RGBの縦ずれ量(UV)
        _RgbSliceVariance ("RGB Slice Variance", Range(0, 1)) = 0.5 // RGBごとの帯ずれのばらつき

        // ---------- ノイズ ----------
        [Header(Noise)]
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.25           // ロゴ内のノイズの量
        _NoiseScale ("Noise Scale", Float) = 160.0                  // ノイズの細かさ
        _SpeckAmount ("Speck Amount", Range(0, 1)) = 0.15           // 周りの色付きの点の量

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
            #pragma multi_compile_local _ _GHOSTADD_ON

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
            float4 _MainTex_ST;
            float4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Interval;
            float _Duration;
            float _TimeOffset;
            float _StepRate;

            float _SliceCount;
            float _SliceShift;
            float _SliceChance;
            float _FineSliceCount;
            float _FineSliceShift;
            float _FineSliceChance;
            float _JitterAmount;

            float _StreakChance;
            float _StreakCellWidth;

            float _SplitAmount;
            float4 _GhostColorA;
            float4 _GhostColorB;
            float _GhostStrength;

            float _RgbSplitX;
            float _RgbSplitY;
            float _RgbSliceVariance;

            float _NoiseAmount;
            float _NoiseScale;
            float _SpeckAmount;

            // ----------------------------------------------------
            // 乱数(同じ入力なら同じ値。入力を変えるとバラバラな値になる)
            // 結果は 0 以上 1 未満
            // ----------------------------------------------------

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ----------------------------------------------------
            // グリッチの強さ(0 = 出ていない、1 = 最大)
            //   _Interval 秒ごとに、最初の _Duration 秒だけ出る。
            //   出ている間は、コマごとに強さをランダムに変えて、カクカクしたぶれにする。
            // ----------------------------------------------------

            float GetGlitchAmount(float time, float stepIndex)
            {
                float interval = max(_Interval, 0.01);
                float duration = clamp(_Duration, 0.0, interval);

                // 今、間隔の何秒目か(0 〜 interval)
                float t = frac((time + _TimeOffset) / interval) * interval;

                if (duration <= 0.0 || t >= duration)
                    return 0.0;

                // コマごとに強さをランダムにする(0.35 〜 1.0)
                float flicker = lerp(0.35, 1.0, Hash11(stepIndex * 1.37));

                // 終わりに近づくほど、少し弱くする
                float fade = 1.0 - (t / duration) * 0.5;

                return flicker * fade;
            }

            // ----------------------------------------------------
            // 帯ずれ(横方向のずれ量。UV単位)
            //   太い帯と細い帯の2種類を重ねる。
            //   channelSeed を変えると、別の帯のずれ方になる(RGB分離で使う)。
            // ----------------------------------------------------

            float SliceOffsetX(float y, float stepIndex, float amount, float channelSeed)
            {
                // 太い帯
                float coarseId = floor(y * _SliceCount);
                float coarseRand = Hash21(float2(coarseId + channelSeed, stepIndex));
                float coarseDir = Hash21(float2(coarseId + 17.0 + channelSeed, stepIndex)) * 2.0 - 1.0;
                float coarseOn = step(1.0 - _SliceChance, coarseRand);

                // 細い帯
                float fineId = floor(y * _FineSliceCount);
                float fineRand = Hash21(float2(fineId + 101.0 + channelSeed, stepIndex));
                float fineDir = Hash21(float2(fineId + 211.0 + channelSeed, stepIndex)) * 2.0 - 1.0;
                float fineOn = step(1.0 - _FineSliceChance, fineRand);

                return (coarseDir * _SliceShift * coarseOn + fineDir * _FineSliceShift * fineOn) * amount;
            }

            // ----------------------------------------------------
            // 横に伸びる線
            //   細い行のうち一部だけ、横位置を大きなマスに丸める。
            //   → ロゴの一部が、マスの幅だけ横へ引き伸ばされた線になる。
            //   返すのは、丸めた後の横位置(UV)。
            // ----------------------------------------------------

            float StreakX(float x, float rowY, float stepIndex, float amount)
            {
                float rowId = floor(rowY * _FineSliceCount * 0.5);
                float r = Hash21(float2(rowId + 331.0, stepIndex + 7.0));
                float isStreak = step(1.0 - _StreakChance * amount, r);

                float cell = max(_StreakCellWidth, 0.01);
                float snapped = (floor(x / cell) + 0.5) * cell;

                return lerp(x, snapped, isStreak);
            }

            // ----------------------------------------------------
            // 画像の色を取る / アルファ掛け済みの色にする
            // ----------------------------------------------------

            float4 SampleLogo(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            float4 ToPremultiplied(float4 c)
            {
                return float4(c.rgb * c.a, c.a);
            }

            // ----------------------------------------------------
            // 足し算モード: R / G / B それぞれが画像を読む位置(UV)
            //   共通の帯ずれと、チャンネルごとの帯ずれを _RgbSliceVariance で混ぜる。
            //   (0 なら3色とも同じ帯ずれ、1 なら3色とも別々の帯ずれ)
            // ----------------------------------------------------

            float2 ChannelUV(float2 uv, float2 jitter, float sharedShift, float channelSeed,
                             float stepIndex, float amount, float2 splitOffset)
            {
                float specificShift = SliceOffsetX(uv.y, stepIndex, amount, channelSeed);
                float shift = lerp(sharedShift, specificShift, _RgbSliceVariance);

                float2 p = uv + jitter + float2(shift, 0.0) + splitOffset;
                p.x = StreakX(p.x, uv.y, stepIndex, amount);

                return p;
            }

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
                float time = _Time.y;

                // コマ番号。_StepRate 回/秒で変わり、乱数の種になる
                float stepIndex = floor(time * max(_StepRate, 0.01));

                // 今のグリッチの強さ
                float amount = GetGlitchAmount(time, stepIndex);

                float2 uv = IN.texcoord;

                // --- 全体の小さなぶれ ---
                float2 jitter = (float2(Hash11(stepIndex * 3.1), Hash11(stepIndex * 5.7)) * 2.0 - 1.0)
                                * _JitterAmount * amount;

                // --- 帯ずれ(全モード共通の分) ---
                float sharedShift = SliceOffsetX(uv.y, stepIndex, amount, 0.0);

                // 太い帯ごとの乱数(色ずれ量を帯ごとに少し変えるために使う)
                float rowRand = Hash21(float2(floor(uv.y * _SliceCount), stepIndex));

                // --- ロゴを読む位置(帯ずれ + ぶれ + 横に伸びる線) ---
                float2 baseUV = uv + jitter + float2(sharedShift, 0.0);
                baseUV.x = StreakX(baseUV.x, uv.y, stepIndex, amount);

                float4 pm; // アルファ掛け済みの色

            #if defined(_GHOSTADD_ON)
                // ====================================================
                // 足し算モード(ON SPECIAL 風): R / G / B を別々の位置から読んで重ねる
                // ====================================================

                float splitScale = lerp(0.6, 1.4, rowRand) * amount;

                // R は右上へ、B は左下へ、G はそのまま
                float2 offsetR = float2(_RgbSplitX, _RgbSplitY) * splitScale;
                float2 offsetG = float2(0.0, 0.0);
                float2 offsetB = float2(-_RgbSplitX, -_RgbSplitY) * splitScale;

                float4 colorR = SampleLogo(ChannelUV(uv, jitter, sharedShift, 1.0, stepIndex, amount, offsetR));
                float4 colorG = SampleLogo(ChannelUV(uv, jitter, sharedShift, 2.0, stepIndex, amount, offsetG));
                float4 colorB = SampleLogo(ChannelUV(uv, jitter, sharedShift, 3.0, stepIndex, amount, offsetB));

                // 各チャンネルの色を、そのチャンネルの不透明度を掛けて足し合わせる。
                // 3つとも重なった所は元のロゴの色になり、ずれた縁だけ単色になる
                pm.rgb = float3(colorR.r * colorR.a, colorG.g * colorG.a, colorB.b * colorB.a);
                pm.a = max(colorR.a, max(colorG.a, colorB.a));
            #else
                // ====================================================
                // 通常モード(ORDER:ZERO 風): 本体はそのまま、外側にだけ細い分身
                // ====================================================

                float split = _SplitAmount * amount * lerp(0.6, 1.4, rowRand);

                float4 mainColor = SampleLogo(baseUV);

                // 分身は、ロゴの形(アルファ)だけを使って、指定した色で塗る
                float4 ghostA = float4(_GhostColorA.rgb, SampleLogo(baseUV + float2(split, 0.0)).a * _GhostColorA.a);
                float4 ghostB = float4(_GhostColorB.rgb, SampleLogo(baseUV - float2(split, 0.0)).a * _GhostColorB.a);

                // グリッチが出ていない時は、分身を完全に消す
                float ghostWeight = saturate(amount * _GhostStrength);
                ghostA.a *= ghostWeight;
                ghostB.a *= ghostWeight;

                float4 pmMain = ToPremultiplied(mainColor);
                float4 pmA = ToPremultiplied(ghostA);
                float4 pmB = ToPremultiplied(ghostB);

                // 下から 分身A → 分身B → ロゴ本体 の順に重ねる
                pm = pmMain + (pmB + pmA * (1.0 - pmB.a)) * (1.0 - pmMain.a);
            #endif

                // --- ノイズ(ロゴの形の中) ---
                // 横長の小さなマスに分け、一部のマスだけ白か黒に塗る
                float2 cell = floor(float2(uv.x * _NoiseScale, uv.y * _NoiseScale * 0.2));
                float noiseRand = Hash21(cell + float2(stepIndex * 1.7, stepIndex * 9.2));
                float isNoise = step(1.0 - _NoiseAmount * amount, noiseRand);
                float noiseValue = step(0.5, Hash21(cell + float2(stepIndex * 4.1, 3.3))); // 0=黒、1=白

                pm.rgb = lerp(pm.rgb, noiseValue * pm.a, isNoise);

                // --- 色付きの小さな点(ロゴの少し外側だけ) ---
                float2 speckCell = floor(float2(uv.x * _NoiseScale * 0.5, uv.y * _NoiseScale * 0.15));
                float speckRand = Hash21(speckCell + float2(stepIndex * 2.3, stepIndex * 6.1));
                float isSpeck = step(1.0 - _SpeckAmount * amount, speckRand);
                float speckPick = step(0.5, Hash21(speckCell + float2(7.7, stepIndex * 1.9)));
                float3 speckColor = lerp(_GhostColorA.rgb, _GhostColorB.rgb, speckPick);

                // 少し離れた左右にロゴがある場所 = ロゴの近く
                float haloDist = max(_SplitAmount, 0.004) * 4.0;
                float halo = max(SampleLogo(baseUV + float2(haloDist, 0.0)).a,
                                 SampleLogo(baseUV - float2(haloDist, 0.0)).a);

                // ロゴ本体の上には出さない(まだ透明な所にだけ足す)
                float speckWeight = isSpeck * halo * (1.0 - pm.a);
                pm.rgb += speckColor * speckWeight;
                pm.a += speckWeight;

                // --- 仕上げ(UIの標準的な処理) ---
                pm.rgb *= IN.color.rgb; // Tint
                pm *= IN.color.a;

            #ifdef UNITY_UI_CLIP_RECT
                pm *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
            #endif

            #ifdef UNITY_UI_ALPHACLIP
                clip(pm.a - 0.001);
            #endif

                return pm;
            }
            ENDCG
        }
    }
}
