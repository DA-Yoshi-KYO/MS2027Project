/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成(CS_PlayerHideMarkerの仮の見た目をMVPに分けた)
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// ハイドマーカー(今ダッシュボタンを押したら飛べるハイドポイントの目印)の Model。
///
/// ・目印を出す位置(ハイドポイントの立つ位置の Transform)を ReactiveProperty で保持する
///   → SetTarget を呼ぶだけで目印が動く(null なら非表示)
/// ・目印は自分の画面に1つだけなので、アイテムスロットと同じく Bind() で公開する(番号なし)
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIHideMarkerModel : CS_BaseModel
{
    // ---------------- 公開されたModel ----------------

    /// <summary>
    /// 現在 Bind されている Model(画面に1つだけなので static で保持する)
    /// </summary>
    private static CS_UIHideMarkerModel _current;

    /// <summary>
    /// Bind された時に通知するイベント
    /// </summary>
    public static event Action<CS_UIHideMarkerModel> OnBound;

    /// <summary>
    /// Bind が外れた時に通知するイベント
    /// </summary>
    public static event Action OnUnbound;

    /// <summary>
    /// 現在の Model を取得する(存在しなければ false)
    /// Presenter が後から有効になった場合に使う
    /// </summary>
    public static bool TryGet(out CS_UIHideMarkerModel model)
    {
        model = _current;
        return model != null;
    }

    //Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    private readonly ReactiveProperty<Transform> _target = new(null);

    /// <summary>
    /// 目印を出す位置(null = 目印なし)
    /// </summary>
    public ReadOnlyReactiveProperty<Transform> target => _target;
    public Transform currentTarget => _target.Value;

    // ---------------- 公開 ----------------

    /// <summary>
    /// このModelを目印の表示元として公開する
    /// </summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>
    /// 公開をやめる(目印は非表示になる)
    /// </summary>
    public void Unbind()
    {
        //別のModelで上書きされていなければ外す
        if (_current != this) return;

        _current = null;
        OnUnbound?.Invoke();
    }

    // ---------------- 値の変更 ----------------

    /// <summary>
    /// 目印を出す位置をセットする(null を渡すと非表示)
    /// </summary>
    public void SetTarget(Transform targetTransform)
    {
        _target.Value = targetTransform;
    }

    /// <summary>
    /// 目印を消す(SetTarget(null) と同じ)
    /// </summary>
    public void ClearTarget()
    {
        SetTarget(null);
    }

    public override void Dispose()
    {
        Unbind();
        _target.Dispose();
    }
}
