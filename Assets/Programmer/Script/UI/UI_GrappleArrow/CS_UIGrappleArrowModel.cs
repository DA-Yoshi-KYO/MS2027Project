/* ================================================
 * グラップルの矢印(飛んでいく方向を示す矢印)のModel
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// グラップルの矢印(狙っているグラップルのSAポイントの上に出す、飛んでいく方向の矢印)の Model。
///
/// ・矢印を出す位置(ワールド)・向き(ワールド)・表示の有無を保持する
/// ・矢印は自分の画面に1つだけなので、ハイドマーカーと同じく Bind() で公開する(番号なし)
/// ・Show で位置・向きを渡すと矢印が出る(毎フレーム呼んでよい)。Hide で消える(位置・向きは残るので、消える間もそこに出る)
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
    private Vector3 _position;
    private Vector3 _direction = Vector3.forward;

    /// <summary>矢印を出すか</summary>
    public ReadOnlyReactiveProperty<bool> isVisible => _isVisible;

    /// <summary>矢印を出す位置(ワールド。矢印の中心)</summary>
    public Vector3 position => _position;

    /// <summary>矢印が指す向き(ワールド。長さ1)</summary>
    public Vector3 direction => _direction;

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

    /// <summary>矢印を出す。位置と向きを渡す(向きは長さ1にそろえる。ゼロなら今の向きのまま)</summary>
    public void Show(Vector3 worldPosition, Vector3 worldDirection)
    {
        _position = worldPosition;
        if (worldDirection.sqrMagnitude > 0.0001f) _direction = worldDirection.normalized;

        _isVisible.Value = true;
    }

    /// <summary>矢印を消す(位置・向きは残る)</summary>
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
