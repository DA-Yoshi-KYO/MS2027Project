using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * 画面の中心はくっきり残し、周りだけを円形にぼかすポストプロセス(HDRPのカスタムポストプロセス)
 * Volume Profileに追加して使う(Add Override → Post-processing/Custom/Edge Blur)
 * 描画はSHG_EdgeBlur.shadergraph(ぼかし本体はSHG_EdgeBlur.hlsl)で行う
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・HDRPのカスタムポストプロセスは、Project Settings → Graphics → HDRP の
 *   Custom Post Process Orders(After Post Process Blurs)に登録しないと描画されない(登録済み)
 *   HDRP標準のぼかし(被写界深度・モーションブラー)の後にかかり、集中線(After Post Process)はこの上に乗る
 * ・Intensityが0の時は描画しない。Volumeのweightでなめらかに出し入れできるよう、既定値は0にしている
 *   Intensityはぼかしの強さの割合(ぼかしの半径 = Blur Radius × 円の外側の度合い × Intensity)
 * ・円は画面の縦横比で歪まないよう、横方向を画面比で伸ばして距離を測る
 * ・元の画面はHDRPが_CustomPostProcessInputとして渡すので、こちらからは渡さない
 * ・シェーダーはShader.Findで探すため、Graphics設定のAlways Included Shadersに入れている(ビルドで消えないように)
 */
// ========================================

[Serializable, VolumeComponentMenu("Post-processing/Custom/Edge Blur")]
public sealed class CSV_EdgeBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Header("強さ")]
    [Tooltip("ぼかしの強さ(0で描画しない)")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("一番外側のぼけの大きさ(画面の高さを1とした長さ)")]
    public ClampedFloatParameter blurRadius = new ClampedFloatParameter(0.015f, 0f, 0.1f);

    [Header("くっきり残す範囲")]
    [Tooltip("円の中心(画面の左下が0、右上が1)")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Tooltip("ぼかさない円の半径(画面の高さを1とした長さ)")]
    public ClampedFloatParameter innerRadius = new ClampedFloatParameter(0.45f, 0f, 1.5f);

    [Tooltip("ぼかさない所からぼかしきる所までの幅(画面の高さを1とした長さ)")]
    public ClampedFloatParameter softness = new ClampedFloatParameter(0.3f, 0.01f, 1f);

    private const string _shaderName = "Shader Graphs/SHG_EdgeBlur";

    private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
    private static readonly int _blurRadiusId = Shader.PropertyToID("_BlurRadius");
    private static readonly int _centerId = Shader.PropertyToID("_Center");
    private static readonly int _innerRadiusId = Shader.PropertyToID("_InnerRadius");
    private static readonly int _softnessId = Shader.PropertyToID("_Softness");
    private static readonly int _aspectRatioId = Shader.PropertyToID("_AspectRatio");

    private Material _material;

    public bool IsActive() => _material != null && intensity.value > 0f && blurRadius.value > 0f;

    // HDRP標準のぼかしの後、ブルームや色調補正の前にかける(集中線はぼかさない)
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcessBlurs;

    // シーンビューでは出さない(ゲーム中の演出のため)
    public override bool visibleInSceneView => false;

    public override void Setup()
    {
        Shader shader = Shader.Find(_shaderName);
        if (shader == null)
        {
            Debug.LogError($"CSV_EdgeBlur: シェーダー {_shaderName} が見つかりません");
            return;
        }

        _material = new Material(shader);
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (_material == null) return;

        _material.SetFloat(_intensityId, intensity.value);
        _material.SetFloat(_blurRadiusId, blurRadius.value);
        _material.SetVector(_centerId, center.value);
        _material.SetFloat(_innerRadiusId, innerRadius.value);
        _material.SetFloat(_softnessId, softness.value);
        _material.SetFloat(_aspectRatioId, (float)camera.actualWidth / camera.actualHeight);

        HDUtils.DrawFullScreen(cmd, _material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(_material);
    }
}
