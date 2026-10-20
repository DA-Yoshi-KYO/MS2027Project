using System;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 画面効果をどう出し入れするか(時間)を決めるVolumeComponent
 * 効果(CSV_SpeedLinesやVignetteなど)と同じVolume Profileに追加して使う(Add Override → ScreenEffect/Timing)
 * CS_PlayerScreenEffectがProfileから読み取る
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・これ自体は何も描画しない(HDRPへの登録もいらない)
 * ・値はProfileから直接読むため、Inspectorのチェック(Override)の有無に関係なく、表示されている値が使われる
 * ・Profileに入っていない時は、各項目の既定値(下の初期値)を使う
 * ・出し方は2通り(CS_PlayerScreenEffect)
 *   SetHeld : 状態が続く間ずっとかける(ダッシュ中など)。Fade In Time / Fade Out Timeで出し入れする
 *   Play    : 1回だけかける(攻撃を当てた時など)。Play Duration秒かけてPlay Weight Curveの強さで出す
 *   強さはVolumeのweight(0〜1)で、Profileに設定した見た目に対する割合になる
 */
// ========================================

[Serializable, VolumeComponentMenu("ScreenEffect/Timing")]
public sealed class CSV_ScreenEffectTiming : VolumeComponent
{
    [Tooltip("Volumeの優先度(ほかのVolumeより上にする)")]
    public FloatParameter priority = new FloatParameter(100f);

    [Tooltip("SetHeldで出しきるまでの時間(秒)")]
    public MinFloatParameter fadeInTime = new MinFloatParameter(0.05f, 0f);

    [Tooltip("SetHeldで消えきるまでの時間(秒)")]
    public MinFloatParameter fadeOutTime = new MinFloatParameter(0.25f, 0f);

    [Tooltip("Playの長さ(秒)")]
    public MinFloatParameter playDuration = new MinFloatParameter(0.3f, 0.01f);

    [Tooltip("Playの強さ(横軸: 経過の割合0〜1、縦軸: 強さ0〜1)")]
    public AnimationCurveParameter playWeightCurve = new AnimationCurveParameter(AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
}
