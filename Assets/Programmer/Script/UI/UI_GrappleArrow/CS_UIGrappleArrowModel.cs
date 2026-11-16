/* ================================================
 * グラップルの矢印(飛んでいく道筋に並べる山形)のModel
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * 2026-11-16 | 的の上に出す矢印から、プレイヤーから的へ向かう道筋に山形を並べる形に変更(始点・終点を持つ)
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// グラップルの矢印(狙っているグラップルのSAポイントへ飛んでいく道筋に、山形を並べて流す表示)の Model。
///
/// ・道筋の始点(プレイヤーの体の中心あたり)・終点(的へ引っ張られる先)・表示の有無を保持する
/// ・矢印は自分の画面に1つだけなので、ハイドマーカーと同じく Bind() で公開する(番号なし)
/// ・Show で始点・終点を渡すと矢印が出る(毎フレーム呼んでよい。プレイヤーが動くと道筋も追従する)。Hide で消える(始点・終点は残るので、消える間もそこに出る)
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIGrappleArrowModel : CS_BaseModel
{
    // ---------------- 公開されたModel ----------------

    /// <summary>現在 Bind されている Model(画面に1つだけなので static で保持する)</summary>
    private static CS_UIGrappleArrowModel _current;

    /// <summary>Bind された時に通知するイベント</summary>
    public static event Action<CS_UIGrappleArrowModel> OnBound;

    /// <summary>Bind が外れた時に通知するイベント</summary>
    public static event Action OnUnbound;

    /// <summary>現在の Model を取得する(存在しなければ false)。Presenter が後から有効になった場合に使う</summary>
    public static bool TryGet(out CS_UIGrappleArrowModel model)
    {
        model = _current;
        return model != null;
    }

    // Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    private readonly ReactiveProperty<bool> _isVisible = new(false);
    private Vector3 _startPosition;
    private Vector3 _endPosition;

    /// <summary>矢印を出すか</summary>
    public ReadOnlyReactiveProperty<bool> isVisible => _isVisible;

    /// <summary>道筋の始点(ワールド。プレイヤーの体の中心あたり)</summary>
    public Vector3 startPosition => _startPosition;

    /// <summary>道筋の終点(ワールド。的へ引っ張られる先)</summary>
    public Vector3 endPosition => _endPosition;

    // ---------------- 公開 ----------------

    /// <summary>このModelを矢印の表示元として公開する</summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>公開をやめる(矢印は消える)</summary>
    public void Unbind()
    {
        // 別のModelで上書きされていなければ外す
        if (_current != this) return;

        _current = null;
        OnUnbound?.Invoke();
    }

    // ---------------- 値の変更 ----------------

    /// <summary>矢印を出す。道筋の始点・終点(ワールド)を渡す</summary>
    public void Show(Vector3 worldStart, Vector3 worldEnd)
    {
        _startPosition = worldStart;
        _endPosition = worldEnd;
        _isVisible.Value = true;
    }

    /// <summary>矢印を消す(始点・終点は残る)</summary>
    public void Hide()
    {
        _isVisible.Value = false;
    }

    public override void Dispose()
    {
        Unbind();
        _isVisible.Dispose();
    }
}
