using UnityEngine;

/*
 * パスの制御点(CS_PathFollowerの子に置く空オブジェクトに付ける)
 * 点の位置はこのオブジェクトの位置。点ごとの待ち時間と、次の点までの速さ・動き方のカーブを持つ
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・CS_PathFollowerは、自分の子(孫も含む)にあるこのコンポーネントを、ヒエラルキーの上から順に、パスの点として使う
 * ・点の追加・移動・削除は、CS_PathFollowerのインスペクター・シーンビューで行える(CSED_PathFollowerEditor)
 * ・動き方のカーブ(イーズ): 区間(この点から次の点まで)の中の進み方を決める
 *   横: 区間にかかる時間の進み具合(0〜1)、縦: 区間の距離の進み具合(0〜1)。既定は直線(一定の速さ)
 *   区間の持ち主は、区間の始点(この点)。区間にかかる時間(長さ÷速さ)・待ち時間は、カーブでは変わらない(区間の中の進み方だけが変わる)
 *   カーブが端で0・1にならなくても、頭を0、終わりを1にそろえる(区間のつなぎ目で位置が飛ばない)
 * ・行きのカーブ(_goCurve): この点から次の点へ向かう時の進み方
 * ・帰りの選び方(_backMode): 往復の帰り道(次の点からこの点へ戻る区間)の進み方
 *   Copy: 行きと同じカーブを使う / Inverse: 行きの逆再生 / Custom: 帰り専用のカーブ(_backCurve)を使う
 *   一方向・周回では帰りは使わない。最後の点は、次の区間が無いので帰りを持たない(周回の最後→最初の区間は、行きのカーブを使う)
 * ・速さ(_speedToNext)は、カーブとは違い、区間の平均の速さ。帰りの速さは、出発する点(次の点)の値を使う
 */
// ========================================

public class CS_PathFollowerPoint : MonoBehaviour
{
    [SerializeField][Min(0f)] private float _waitTime = 0f;         // この点に着いてから、動き出すまでの待ち時間(秒)
    [SerializeField][Min(0.1f)] private float _speedToNext = 4f;    // この点から次の点までの速さ(m/秒。往復の帰り道は、この点から1つ前の点まで。一方向の最後の点では使わない)
    [SerializeField] private AnimationCurve _goCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);       // 行き(この点から次の点へ)の進み方
    [SerializeField] private CSE_PathFollowEaseMode _backMode = CSE_PathFollowEaseMode.Copy;        // 帰り(次の点からこの点へ)の進み方の選び方
    [SerializeField] private AnimationCurve _backCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);     // 帰りの進み方(Customの時だけ使う)

    public float waitTime => _waitTime;
    public float speedToNext => _speedToNext;
    public AnimationCurve goCurve => _goCurve;
    public CSE_PathFollowEaseMode backMode => _backMode;
    public AnimationCurve backCurve => _backCurve;

    // 新しい点を作る時に、値を決める(エディタから呼ぶ)
    public void Setup(float waitTime, float speedToNext)
    {
        _waitTime = Mathf.Max(0f, waitTime);
        _speedToNext = Mathf.Max(0.1f, speedToNext);
    }
}
