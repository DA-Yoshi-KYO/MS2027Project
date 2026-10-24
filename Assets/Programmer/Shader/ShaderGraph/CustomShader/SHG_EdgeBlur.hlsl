// 円形ぼかしのぼかし部分(SHG_EdgeBlur.shadergraphのCustom Functionから使う)
// 元の画面を、注目点の周りの円盤状の点から集めて平均し、ぼかした色を返す
//
// 制作者：　吉田京志郎
//
// メモ
// ・どこをどれだけぼかすか(Mask)はグラフ側のノードで決める(中心からの距離でInnerRadius〜InnerRadius+Softnessの間を0→1)
// ・ぼかしの半径は BlurRadius × Mask(画面の高さを1とした長さ)。Maskが0の所は元の色をそのまま返す
// ・点の並べ方は黄金角(約137.5°)ずつ回しながら外へ広げる形。少ない点数でも円盤の中に偏りなく散らばる
// ・元の画面はHDRPがカスタムポストプロセスの直前に_CustomPostProcessInputへ渡している
//   (HD Sample BufferノードのPost Process Inputと同じもの。ノードは1点しか読めないので、ここで直接読む)
// ・Shader Graphのプレビューでは_CustomPostProcessInputがないので、灰色を返す
#ifndef SHG_EDGE_BLUR_INCLUDED
#define SHG_EDGE_BLUR_INCLUDED

#define EDGE_BLUR_SAMPLE_COUNT 32
#define EDGE_BLUR_GOLDEN_ANGLE 2.39996323

#ifndef SHADERGRAPH_PREVIEW
TEXTURE2D_X(_CustomPostProcessInput);

float3 EdgeBlur_SampleSource(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(_CustomPostProcessInput, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv), 0).rgb;
}
#endif

void EdgeBlur_float(float2 UV, float Mask, float BlurRadius, float AspectRatio, out float3 Color)
{
#ifdef SHADERGRAPH_PREVIEW
    Color = float3(0.5, 0.5, 0.5);
#else
    float radius = BlurRadius * saturate(Mask);
    if (radius <= 0.0)
    {
        Color = EdgeBlur_SampleSource(UV);
        return;
    }

    // 横長の画面でも円盤になるよう、横方向のずれを画面比で縮める
    float2 scale = float2(radius / max(AspectRatio, 0.0001), radius);

    float3 sum = 0.0;
    [loop]
    for (int i = 0; i < EDGE_BLUR_SAMPLE_COUNT; i++)
    {
        // 面積が均等になるよう、距離は個数の平方根で広げる
        float dist = sqrt((i + 0.5) / EDGE_BLUR_SAMPLE_COUNT);
        float angle = i * EDGE_BLUR_GOLDEN_ANGLE;
        float2 offset = float2(cos(angle), sin(angle)) * dist * scale;
        sum += EdgeBlur_SampleSource(UV + offset);
    }
    Color = sum / EDGE_BLUR_SAMPLE_COUNT;
#endif
}

void EdgeBlur_half(half2 UV, half Mask, half BlurRadius, half AspectRatio, out half3 Color)
{
    float3 color;
    EdgeBlur_float(UV, Mask, BlurRadius, AspectRatio, color);
    Color = color;
}

#endif
