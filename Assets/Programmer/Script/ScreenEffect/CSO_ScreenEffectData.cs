using UnityEngine;
using UnityEngine.Rendering;

/*
 * 画面効果1つ分のデータ(ScriptableObject)
 * どの効果(Volume Profile)を、どんな速さで出し入れするかを決める
 * CS_PlayerScreenEffectに渡して使う
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ■ 新しい画面効果を作るとき
 *   1. Volume Profileを作り、効果(CSV_SpeedLinesやVignetteなど)を追加して見た目を決める
 *   2. Projectウィンドウで右クリック → Create → ScreenEffect → Screen Effect Data でアセットを作り、1のProfileを入れる
 *   3. アクションのイベントを受ける演出クラス(例: CS_PlayerDashScreenEffect)から、CS_PlayerScreenEffectへ渡す
 *
 * ■ 出し方は2通り(CS_PlayerScreenEffect)
 *   SetHeld : 状態が続く間ずっとかける(ダッシュ中など)。_fadeInTime / _fadeOutTimeで出し入れする
 *   Play    : 1回だけかける(攻撃を当てた時など)。_playDuration秒かけて_playWeightCurveの強さで出す
 *   強さはVolumeのweight(0〜1)で、Profileに設定した見た目に対する割合になる
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ScreenEffect", menuName = "ScreenEffect/Screen Effect Data")]
public class CSO_ScreenEffectData : ScriptableObject
{
    [Header("効果")]
    [SerializeField] private VolumeProfile _profile;            // かける効果
    [SerializeField] private float _priority = 100f;            // Volumeの優先度(ほかのVolumeより上にする)

    [Header("続けてかける時(SetHeld)")]
    [SerializeField][Min(0f)] private float _fadeInTime = 0.05f;   // 出しきるまでの時間(秒)
    [SerializeField][Min(0f)] private float _fadeOutTime = 0.25f;  // 消えきるまでの時間(秒)

    [Header("1回だけかける時(Play)")]
    [SerializeField][Min(0.01f)] private float _playDuration = 0.3f;   // 長さ(秒)
    [SerializeField] private AnimationCurve _playWeightCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);   // 横軸: 経過の割合(0〜1)、縦軸: 強さ(0〜1)

    public VolumeProfile profile => _profile;
    public float priority => _priority;
    public float fadeInTime => _fadeInTime;
    public float fadeOutTime => _fadeOutTime;
    public float playDuration => _playDuration;
    public AnimationCurve playWeightCurve => _playWeightCurve;
}
