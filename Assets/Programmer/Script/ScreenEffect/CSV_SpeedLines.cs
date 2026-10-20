using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * 画面に集中線(中心から外へ向かう線)を描くポストプロセス(HDRPのカスタムポストプロセス)
 * Volume Profileに追加して使う(Add Override → Post-processing/Custom/Speed Lines)
 * 描画はSH_SpeedLines.shaderで行う
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・HDRPのカスタムポストプロセスは、Project Settings → Graphics → HDRP の
 *   Custom Post Process Orders(After Post Process)に登録しないと描画されない(登録済み)
 * ・Intensityが0の時は描画しない。Volumeのweightでなめらかに出し入れできるよう、
 *   既定値(Volumeが効いていない時の値)は0にしている
 * ・線の出方は_flickerRate回/秒で切り替わる(漫画のようにパラパラ変わる)
 * ・シェーダーはShader.Findで探すため、Graphics設定のAlways Included Shadersに入れている(ビルドで消えないように)
 */
// ========================================

[Serializable, VolumeComponentMenu("Post-processing/Custom/Speed Lines")]
public sealed class CSV_SpeedLines : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("線の濃さ(0で描画しない)")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("線の色(アルファで透け具合を決める)")]
    public ColorParameter color = new ColorParameter(new Color(1f, 1f, 1f, 0.8f), false, true, true);

    [Tooltip("集中する点(画面の左下が0、右上が1)")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Tooltip("一周を何本分に区切るか")]
    public ClampedIntParameter lineCount = new ClampedIntParameter(120, 8, 512);

    [Tooltip("区切りのうち線を出す割合")]
    public ClampedFloatParameter density = new ClampedFloatParameter(0.4f, 0f, 1f);

    [Tooltip("線の太さ(1区切りに対する割合)")]
    public ClampedFloatParameter lineWidth = new ClampedFloatParameter(0.5f, 0.01f, 1f);

    [Tooltip("線が出ない中心部分の半径(画面の高さを1とした長さ)")]
    public ClampedFloatParameter innerRadius = new ClampedFloatParameter(0.35f, 0f, 1.5f);

    [Tooltip("線の内側の端をぼかす長さ")]
    public ClampedFloatParameter softness = new ClampedFloatParameter(0.25f, 0.01f, 1f);

    [Tooltip("線の出方が1秒に何回切り替わるか")]
    public ClampedFloatParameter flickerRate = new ClampedFloatParameter(20f, 0f, 60f);

    private const string _shaderName = "Hidden/Shader/SH_SpeedLines";

    private static readonly int _mainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
    private static readonly int _lineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int _centerId = Shader.PropertyToID("_Center");
    private static readonly int _lineCountId = Shader.PropertyToID("_LineCount");
    private static readonly int _densityId = Shader.PropertyToID("_Density");
    private static readonly int _lineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int _innerRadiusId = Shader.PropertyToID("_InnerRadius");
    private static readonly int _softnessId = Shader.PropertyToID("_Softness");
    private static readonly int _flickerRateId = Shader.PropertyToID("_FlickerRate");
    private static readonly int _aspectRatioId = Shader.PropertyToID("_AspectRatio");
    private static readonly int _effectTimeId = Shader.PropertyToID("_EffectTime");

    private Material _material;

    public bool IsActive() => _material != null && intensity.value > 0f;

    // 色調補正などが終わった後の画面に重ねる
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    // シーンビューでは出さない(ゲーム中の演出のため)
    public override bool visibleInSceneView => false;

    public override void Setup()
    {
        Shader shader = Shader.Find(_shaderName);
        if (shader == null)
        {
            Debug.LogError($"CSV_SpeedLines: シェーダー {_shaderName} が見つかりません");
            return;
        }

        _material = new Material(shader);
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (_material == null) return;

        _material.SetTexture(_mainTexId, source);
        _material.SetFloat(_intensityId, intensity.value);
        _material.SetColor(_lineColorId, color.value);
        _material.SetVector(_centerId, center.value);
        _material.SetFloat(_lineCountId, lineCount.value);
        _material.SetFloat(_densityId, density.value);
        _material.SetFloat(_lineWidthId, lineWidth.value);
        _material.SetFloat(_innerRadiusId, innerRadius.value);
        _material.SetFloat(_softnessId, softness.value);
        _material.SetFloat(_flickerRateId, flickerRate.value);
        _material.SetFloat(_aspectRatioId, (float)camera.actualWidth / camera.actualHeight);
        // ヒットストップなどでTime.timeScaleが変わっても線は動き続けるようにする
        _material.SetFloat(_effectTimeId, Time.unscaledTime);

        HDUtils.DrawFullScreen(cmd, _material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(_material);
    }
}
